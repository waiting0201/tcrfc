namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>單一 AI 使用者代理的允許／拒絕設定（`GEO-02`）。<c>UserAgent</c> 是技術字串
/// （HTTP <c>User-agent</c> 標頭值／<c>robots.txt</c> 的 <c>User-agent:</c> 欄位），**不是人類
/// 語言，不進 i18n 側表**（跟 <c>seo.robots_custom_rules</c> 同一個判斷）。</summary>
public sealed record CrawlerAgentDto
{
    public required string UserAgent { get; init; }
    public required bool Allowed { get; init; }
}

/// <summary>
/// AI 爬蟲授權（S1-12a／`GEO-02`）——後台 GET 回應。權限碼 <c>sysadmin_only</c>，理由同
/// <see cref="AdminSeoSettingsDto"/> 檔頭。
/// </summary>
public sealed record AdminCrawlerSettingsDto
{
    /// <summary>目前設定的 AI 使用者代理清單。後台第一次進來、<c>settings</c> 還沒有這一列時，
    /// 回傳 <see cref="Seo.GeoCrawlerDefaults.DefaultUserAgents"/> 當作建議值（不會因此寫入
    /// 資料庫，管理員按下儲存才會真的落地）。</summary>
    public required IReadOnlyList<CrawlerAgentDto> UserAgents { get; init; }

    /// <summary>後台自行再加的排除路徑（在 <see cref="MandatoryExcludePaths"/> 之外）。</summary>
    public required IReadOnlyList<string> AdditionalExcludePaths { get; init; }

    /// <summary>🔴 **強制排除路徑，唯讀，這個 DTO 沒有對應的可寫入欄位**——後台畫面應該把這份
    /// 清單顯示成不可勾選移除的既定項目（陳列用途），實際的「不能被移除」是由
    /// <see cref="Seo.GeoCrawlerDefaults.GetMandatoryExcludePaths"/> 在程式碼層面保證，不是靠
    /// 這個欄位或前端畫面的克制。</summary>
    public required IReadOnlyList<string> MandatoryExcludePaths { get; init; }
}

/// <summary>更新 AI 爬蟲授權設定的請求。**沒有欄位可以帶入或覆蓋強制排除路徑**——
/// <see cref="AdminGeoCrawlerRepository.UpdateAsync"/> 只讀取、只儲存這兩個欄位，強制清單完全
/// 不經過這個型別。</summary>
public sealed record UpdateCrawlerSettingsRequest
{
    public required IReadOnlyList<CrawlerAgentDto> UserAgents { get; init; }
    public required IReadOnlyList<string> AdditionalExcludePaths { get; init; }
}
