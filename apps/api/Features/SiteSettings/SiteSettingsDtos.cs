namespace Tcrfc.Api.Features.SiteSettings;

/// <summary>前台選單項目（I 選單管理，規劃書 §4.9）。<see cref="Url"/> 內部連結是不含語系前綴的路徑（例 <c>/about/</c>），前台自己接 <c>/zh</c>／<c>/en</c>；
/// 外部連結（<see cref="IsExternal"/>）是完整 http(s) 網址。群組標題（有子項目）可以沒有 <see cref="Url"/>。</summary>
public sealed record PublicMenuItemDto
{
    public required Guid Id { get; init; }

    /// <summary>依語系挑選的標籤，要求語系空白回退繁中。</summary>
    public required string Label { get; init; }

    public string? Url { get; init; }

    public required bool IsExternal { get; init; }

    public required IReadOnlyList<PublicMenuItemDto> Children { get; init; }
}

public sealed record PublicMenusDto
{
    /// <summary>主選單。</summary>
    public required IReadOnlyList<PublicMenuItemDto> Main { get; init; }

    /// <summary>Mega Menu。</summary>
    public required IReadOnlyList<PublicMenuItemDto> Mega { get; init; }

    /// <summary>頁尾選單。</summary>
    public required IReadOnlyList<PublicMenuItemDto> Footer { get; init; }
}

public sealed record PublicBrandDto
{
    public string? LogoLightUrl { get; init; }

    public string? LogoDarkUrl { get; init; }

    public string? FaviconUrl { get; init; }

    public string? BrandColor { get; init; }

    public string? BrandSecondaryColor { get; init; }
}

public sealed record PublicMaintenanceDto
{
    public required bool Enabled { get; init; }

    /// <summary>維護頁訊息（G-10）；要求語系空白回退繁中；沒設定為 <c>null</c>，前台用預設文案。</summary>
    public string? Message { get; init; }
}

public sealed record PublicLanguageDto
{
    /// <summary>語系代碼，與 <c>/zh</c>／<c>/en</c> 網址前綴的對應由前台決定（<c>zh-Hant</c> ↔ <c>zh</c>）。</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required bool IsDefault { get; init; }

    public string? FallbackCode { get; init; }
}

public sealed record PublicFormatsDto
{
    /// <summary>日期格式樣式（<c>YYYY</c>／<c>MM</c>／<c>M</c>／<c>DD</c>／<c>D</c>／<c>MMM</c>／<c>MMMM</c> 與分隔字元）；沒設定為 <c>null</c>，前台用語系預設。</summary>
    public string? DateFormat { get; init; }

    /// <summary>數字格式範例字串（如 <c>1,234.56</c>），已解析出的兩個分隔字元在下面兩欄。</summary>
    public string? NumberFormat { get; init; }

    public string? ThousandsSeparator { get; init; }

    public string? DecimalSeparator { get; init; }
}

public sealed record PublicPolicyIndexDto
{
    /// <summary>URL 片段：<c>cookie</c>／<c>privacy</c>／<c>member-terms</c>。</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>是否已有內文；沒有內文的政策頁前台不要連結過去（<c>GET /policies/{code}</c> 會回 404）。</summary>
    public required bool HasContent { get; init; }
}

/// <summary>前台一次取得的站台全域設定。維護模式與多語系規則每頁載入都要用，所以集中在這一支（短暫快取，後台儲存時立即失效）。</summary>
public sealed record PublicSiteSettingsDto
{
    public required PublicBrandDto Brand { get; init; }

    public required PublicMaintenanceDto Maintenance { get; init; }

    /// <summary>啟用中的語系，依排序。</summary>
    public required IReadOnlyList<PublicLanguageDto> Languages { get; init; }

    /// <summary><c>show_default</c>（未翻譯時顯示繁中並標示「本頁尚無此語系版本」）或 <c>hide</c>（隱藏該頁）。
    /// 🔴 後端只保存並提供這個設定；各內容端點<b>不會</b>依它自動隱藏，由前台依各 DTO 的語系回退標記執行。</summary>
    public required string FallbackMode { get; init; }

    public required PublicFormatsDto Formats { get; init; }

    public required IReadOnlyList<PublicPolicyIndexDto> Policies { get; init; }
}

public sealed record PublicPolicyDto
{
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary><b>純文字</b>（不是 HTML）：空行分段，前台必須用文字節點輸出（不得 <c>v-html</c>）。</summary>
    public required string Body { get; init; }

    public required DateTime UpdatedAt { get; init; }

    /// <summary>true＝要求英文但沒有英文內文，Body 是繁中回退。</summary>
    public required bool IsFallbackLocale { get; init; }
}

/// <summary>介面字串（I 字串翻譯表）。語系回退：要求語系沒有值就用繁中。</summary>
public sealed record PublicUiStringsDto
{
    /// <summary>實際使用的語系代碼（<c>zh-Hant</c> 或 <c>en</c>）。</summary>
    public required string Locale { get; init; }

    public required IReadOnlyDictionary<string, string> Strings { get; init; }
}

public sealed record PublicVenueDto
{
    public required Guid Id { get; init; }

    public string? Name { get; init; }

    public string? Address { get; init; }

    /// <summary>交通說明（純文字，空行分段）。</summary>
    public string? Directions { get; init; }

    public decimal? Lat { get; init; }

    public decimal? Lng { get; init; }

    public string? PhotoUrl { get; init; }

    public int? PhotoWidth { get; init; }

    public int? PhotoHeight { get; init; }

    public string? PhotoAlt { get; init; }

    /// <summary>是否為這個俱樂部的主場（站台事實 <c>home_venue_ids</c>）。</summary>
    public required bool IsHome { get; init; }
}
