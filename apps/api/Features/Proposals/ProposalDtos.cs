namespace Tcrfc.Api.Features.Proposals;

/// <summary>9.4 CTA「下載提案簡介」可選的提案（已發布且至少有一份檔案）。前台可依 A/B 版本自行挑一份，
/// 或全部列出。<b>不含檔案網址</b>：檔案要填寫表單後才取得限時下載連結。</summary>
public sealed record PublicProposalDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required int VersionNo { get; init; }

    /// <summary>有檔案的語言（<c>zh</c>／<c>en</c>）。</summary>
    public required IReadOnlyList<string> Locales { get; init; }
}

/// <summary>檔案下載表單送出內容（規劃書 §3.9 9.4：公司／姓名／Email → 取得下載連結，同時建立 Lead）。</summary>
public sealed record ProposalDownloadRequest
{
    public string? Company { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }

    /// <summary>必須為 <c>true</c>（個資同意條款）。</summary>
    public bool Consent { get; init; }

    /// <summary>想下載的語言（<c>zh</c>／<c>en</c>）；該語言沒有檔案時回退中文。</summary>
    public string? Lang { get; init; }
    public string? SourcePath { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }

    /// <summary>誘捕欄位（honeypot）：正常訪客看不到，填了值視為機器人，安靜回成功但不建立 Lead、不給連結。</summary>
    public string? Website { get; init; }
}

public sealed record ProposalDownloadResultDto
{
    /// <summary>限時下載連結（相對於 API 根目錄的路徑，有效 30 分鐘）。誘捕欄位被觸發時為 <c>null</c>。</summary>
    public string? DownloadPath { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
