namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>單筆 301 轉址（<c>redirects</c>，S1-12）。</summary>
public sealed record AdminRedirectDto
{
    public required Guid Id { get; init; }
    public required string FromPath { get; init; }
    public required string ToPath { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record CreateRedirectRequest
{
    /// <summary>舊網址（畫面上叫「來源網址」），對應 <c>redirects.from_path</c>，站內相對路徑
    /// （見 <see cref="RedirectPathPolicy"/>）。唯一鍵 <c>(club_id, from_path)</c>。</summary>
    public required string FromPath { get; init; }

    /// <summary>新網址（「目的網址」），對應 <c>redirects.to_path</c>。</summary>
    public required string ToPath { get; init; }

    public bool IsActive { get; init; } = true;
}

/// <summary>更新請求不接受改 <c>FromPath</c>——來源網址是這筆轉址的識別鍵，要換來源網址等於
/// 建一筆新的、刪一筆舊的，不是「編輯」，避免呼叫端把改鍵當成一般欄位更新，跟樂觀並行、
/// 唯一鍵衝突的例外處理混在一起。</summary>
public sealed record UpdateRedirectRequest
{
    public required string ToPath { get; init; }
    public required bool IsActive { get; init; }
}

public sealed record RedirectCsvImportRowErrorDto
{
    public required int RowNumber { get; init; }
    public required string Reason { get; init; }
}

/// <summary>CSV 匯入結果。🔴 整批驗證、任一列有錯就整批不寫入（比照
/// <c>Features/AdminFaqs/AdminFaqsRepository.ImportCsvAsync</c> 的既有先例）：
/// <see cref="Errors"/> 非空時 <see cref="ImportedCount"/> 恆為 0。</summary>
public sealed record RedirectCsvImportResultDto
{
    public required int ImportedCount { get; init; }
    public required IReadOnlyList<RedirectCsvImportRowErrorDto> Errors { get; init; }
}
