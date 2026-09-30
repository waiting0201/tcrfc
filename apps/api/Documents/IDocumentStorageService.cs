namespace Tcrfc.Api.Documents;

/// <summary>檔案放在哪個容器。<see cref="Public"/>＝可公開下載（新聞稿、品牌識別包，由部署層決定容器公開讀取或 CDN，
/// 跟圖片容器同一種處理）；<see cref="Private"/>＝**不可**公開存取，只能經 API 驗證後串流（贊助提案 PDF——
/// 規劃書 §3.9 9.4 要求「填寫公司／姓名／Email 後才取得下載連結」，若放公開容器就等於沒有表單關卡）。</summary>
public enum DocumentBucket
{
    Public,
    Private,
}

public sealed record UploadedDocumentInfo(string Key, long SizeBytes, string ContentType);

/// <summary>串流讀取結果，呼叫端負責 dispose <see cref="Content"/>。</summary>
public sealed record DocumentReadResult(Stream Content, string ContentType, long? Length) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// 檔案儲存層接縫（E1a 新增）。設計比照 <c>Videos.IVideoStorageService</c>：不轉檔，驗證後原封不動存成一個物件；
/// 刪除 fail-open（清理收尾動作不該讓請求失敗，只記警告）。
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>驗證（大小、檔頭）＋寫入。失敗一律丟 <see cref="DocumentProcessingException"/> 家族，此時還沒碰到儲存體。</summary>
    Task<UploadedDocumentInfo> UploadAsync(DocumentBucket bucket, byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken);

    Task DeleteAsync(DocumentBucket bucket, string? objectKey, CancellationToken cancellationToken);

    /// <summary>開啟串流讀取；物件不存在回傳 <c>null</c>。</summary>
    Task<DocumentReadResult?> OpenReadAsync(DocumentBucket bucket, string objectKey, CancellationToken cancellationToken);
}

/// <summary>公開容器（<see cref="DocumentBucket.Public"/>）物件鍵 → 完整網址。私有容器永遠不提供網址。</summary>
public interface IDocumentPublicUrlResolver
{
    string? Resolve(string? objectKey);
}
