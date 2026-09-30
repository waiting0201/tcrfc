using Tcrfc.Api.Documents;
using Tcrfc.Api.Images;

namespace Tcrfc.Api.Features.Uploads;

/// <summary>被換掉或被刪除的舊物件鍵：資料列寫入成功「之後」才刪（規劃書 §4.0「新圖寫入成功後才刪除舊物件」）。
/// 由 repository 在換圖／刪列時登記，endpoint 在寫入成功後交給 <see cref="UploadTransaction.CommitAsync"/>。</summary>
public sealed class OrphanedObjects
{
    public List<string> ImageKeys { get; } = [];
    public List<(DocumentBucket Bucket, string Key)> Documents { get; } = [];

    public void Image(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            ImageKeys.Add(key);
        }
    }

    public void Document(DocumentBucket bucket, string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            Documents.Add((bucket, key));
        }
    }
}

/// <summary>
/// 一次請求內的上傳補償交易（E1a 起新增模組共用）：先上傳、再寫資料列；寫資料列失敗就把本次已上傳的物件全部刪掉
/// （<c>CancellationToken.None</c>，理由見 docs/18 E-47：請求被取消時補償刪除若沿用同一個 token 會當場失敗）；
/// 成功就刪掉被換掉的舊物件。跟 <c>AdminStaffEndpoints</c> 的手寫 try/catch 同一套語意，只是多個欄位共用一份。
/// </summary>
public sealed class UploadTransaction(IImageStorageService images, IDocumentStorageService documents)
{
    private readonly List<string> _uploadedImages = [];
    private readonly List<(DocumentBucket Bucket, string Key)> _uploadedDocuments = [];

    public async Task<UploadedImageInfo> AddImageAsync(
        string entityType, string field, IFormFile file, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        UploadSlotPolicy.Validate(entityType, field);
        if (file.Length == 0)
        {
            throw new EmptyImageException();
        }

        if (file.Length > ImageUploadOptions.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        var bytes = await ReadAsync(file, cancellationToken);
        var info = await images.UploadAsync(bytes, objectKeyPrefix, cancellationToken);
        _uploadedImages.Add(info.Key);
        return info;
    }

    /// <summary>更新端點的圖片欄位三態：有新檔案＝上傳並換掉；<paramref name="remove"/>＝清空；都沒有＝維持不變。
    /// 同時給新檔案與 remove 視為互相矛盾，400。</summary>
    public async Task<ImageFieldUpdate> ResolveImageAsync(
        string entityType, string field, string label, IFormFile? file, bool remove, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        if (file is not null && remove)
        {
            throw new Common.AdminValidationException($"不能同時上傳新的{label}與移除{label}，請擇一。");
        }

        if (file is not null)
        {
            var info = await AddImageAsync(entityType, field, file, objectKeyPrefix, cancellationToken);
            return ImageFieldUpdate.Set(info.Key, info.Width, info.Height);
        }

        return remove ? ImageFieldUpdate.Remove : ImageFieldUpdate.Keep;
    }

    public async Task<UploadedDocumentInfo> AddDocumentAsync(
        DocumentBucket bucket, IFormFile file, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new EmptyDocumentException();
        }

        if (file.Length > DocumentUploadOptions.MaxUploadBytes)
        {
            throw new DocumentTooLargeException();
        }

        var bytes = await ReadAsync(file, cancellationToken);
        var info = await documents.UploadAsync(bucket, bytes, objectKeyPrefix, cancellationToken);
        _uploadedDocuments.Add((bucket, info.Key));
        return info;
    }

    /// <summary>資料列寫入失敗：刪掉本次請求已經上傳的全部物件。</summary>
    public async Task RollbackAsync()
    {
        foreach (var key in _uploadedImages)
        {
            await images.DeleteAsync(key, CancellationToken.None);
        }

        foreach (var (bucket, key) in _uploadedDocuments)
        {
            await documents.DeleteAsync(bucket, key, CancellationToken.None);
        }
    }

    /// <summary>資料列寫入成功：刪掉被換掉／被刪除的舊物件（fail-open，失敗只記警告）。</summary>
    public async Task CommitAsync(OrphanedObjects orphans)
    {
        foreach (var key in orphans.ImageKeys)
        {
            await images.DeleteAsync(key, CancellationToken.None);
        }

        foreach (var (bucket, key) in orphans.Documents)
        {
            await documents.DeleteAsync(bucket, key, CancellationToken.None);
        }
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
