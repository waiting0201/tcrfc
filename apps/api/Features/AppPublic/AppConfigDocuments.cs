namespace Tcrfc.Api.Features.AppPublic;

/// <summary>雙語文案（對外語系代碼 <c>zh</c>／<c>en</c>；英文缺漏時 App 端回退繁中）。</summary>
public sealed record AppBilingualText(string? Zh, string? En);

public sealed record AppMaintenanceNode
{
    public required bool Enabled { get; init; }
    public AppBilingualText? Message { get; init; }
}

/// <summary>單一平台的設定：最低支援版本（低於它強制更新、不可略過）、建議版本（低於它建議更新、可略過並每 7 天再提醒）、維護模式與功能開關。</summary>
public sealed record AppPlatformConfig
{
    public string? MinSupportedVersion { get; init; }
    public string? RecommendedVersion { get; init; }
    public AppBilingualText? ForceUpdateMessage { get; init; }
    public AppBilingualText? RecommendUpdateMessage { get; init; }
    public AppBilingualText? WhatsNew { get; init; }
    public required AppMaintenanceNode Maintenance { get; init; }
    public required IReadOnlyDictionary<string, bool> FeatureFlags { get; init; }
}

/// <summary>
/// App 設定文件（App 規劃書 §9.2「設定 讀取」、docs/19 §7 設定下發三層來源的內容）。
/// 同一份內容有兩個出口：<c>GET /api/v1/app/config</c>（第 2 層，API 活著時的即時值）與後台 M1／M5 存檔時 write-through 推到
/// Cloudflare 的靜態 JSON（第 1 層，VM 全滅時仍可讀；<see cref="IAppConfigPublisher"/>，尚未串接）。兩邊由 <see cref="AppConfigComposer"/> 產生，內容一致。
/// </summary>
public sealed record AppConfigDocument
{
    public required DateTime GeneratedAt { get; init; }
    public required AppPlatformConfig Ios { get; init; }
    public required AppPlatformConfig Android { get; init; }
}

/// <summary>帶了 <c>platform</c> 與 <c>appVersion</c> 查詢時，伺服器順手替 App 算好的判斷（App 也可自己用文件內容判斷，兩者必須一致）。</summary>
public sealed record AppConfigEvaluation
{
    public required bool Maintenance { get; init; }
    public required bool UpdateRequired { get; init; }
    public required bool UpdateRecommended { get; init; }
}

public sealed record AppConfigResponse
{
    public required DateTime GeneratedAt { get; init; }
    public required AppPlatformConfig Ios { get; init; }
    public required AppPlatformConfig Android { get; init; }
    public AppConfigEvaluation? Evaluation { get; init; }
}
