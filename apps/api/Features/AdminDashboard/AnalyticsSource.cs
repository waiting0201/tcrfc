namespace Tcrfc.Api.Features.AdminDashboard;

/// <summary>流量查詢範圍（含頭含尾，台灣當地日期）。</summary>
public sealed record AnalyticsQuery(string ClubCode, DateOnly From, DateOnly To);

public sealed record AnalyticsPageViewDto(string Path, int Views);

public sealed record AnalyticsSourceShareDto(string Source, int Sessions);

/// <summary>儀表板「流量概況」（規劃書 §4.1 A：本週瀏覽量、熱門頁面、來源分佈）。</summary>
public sealed record AnalyticsOverviewDto(
    int PageViews, int Sessions, IReadOnlyList<AnalyticsPageViewDto> TopPages, IReadOnlyList<AnalyticsSourceShareDto> Sources);

/// <summary><see cref="Overview"/> 只在 <see cref="Configured"/> 為 true 時有值。<see cref="Message"/> 是給畫面顯示的日常中文。</summary>
public sealed record AnalyticsResult(bool Configured, string Message, AnalyticsOverviewDto? Overview);

/// <summary>
/// GA4 流量資料來源接縫（規劃書 §4.1 A「流量概況：串接 GA4」）。🔴 <b>GA4 Data API 的憑證（服務帳戶）與屬性 ID 尚未取得，本期不串接</b>——
/// 預設註冊 <see cref="NotConfiguredAnalyticsSource"/>，儀表板的流量區塊回「尚未串接」，其餘區塊照常運作。
/// 日後串接只需：實作這個介面（呼叫 GA4 Data API 的 <c>runReport</c>，維度 <c>pagePath</c>／<c>sessionDefaultChannelGroup</c>、指標 <c>screenPageViews</c>／<c>sessions</c>）、
/// 在 <c>Program.cs</c> 換掉註冊；呼叫端（儀表板端點與前端畫面）不用改。每個俱樂部各自一個 GA4 屬性，實作依 <see cref="AnalyticsQuery.ClubCode"/> 選屬性。
/// 實作必須自己處理快取（GA4 Data API 有每日配額，儀表板每次載入不得直接打外部服務）與逾時，失敗時回 <c>Configured=true</c> 但 <c>Overview=null</c> 與日常中文訊息，不得丟例外讓儀表板整頁失敗。
/// 接縫說明見 docs/17 §3。
/// </summary>
public interface IAnalyticsSource
{
    /// <summary>目前的供應商名稱；沒有串接時為 <c>null</c>。</summary>
    string? ProviderName { get; }

    Task<AnalyticsResult> GetOverviewAsync(AnalyticsQuery query, CancellationToken cancellationToken);
}

public sealed class NotConfiguredAnalyticsSource : IAnalyticsSource
{
    public string? ProviderName => null;

    public Task<AnalyticsResult> GetOverviewAsync(AnalyticsQuery query, CancellationToken cancellationToken)
        => Task.FromResult(new AnalyticsResult(false, "流量資料尚未串接（Google Analytics 4 憑證尚未提供），其餘儀表板項目不受影響。", null));
}
