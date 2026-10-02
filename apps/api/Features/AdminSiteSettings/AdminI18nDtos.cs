namespace Tcrfc.Api.Features.AdminSiteSettings;

public sealed record AdminLocaleDto
{
    /// <summary>語系代碼：<c>zh-Hant</c>（預設）、<c>en</c>。</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required bool IsDefault { get; init; }

    /// <summary>未翻譯時回退到哪個語系；預設語系為 <c>null</c>。</summary>
    public string? FallbackCode { get; init; }

    public required bool IsEnabled { get; init; }

    public required int SortOrder { get; init; }
}

public sealed record UpdateAdminLocaleRequest
{
    public string? Name { get; init; }

    public bool IsEnabled { get; init; }

    public string? FallbackCode { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>多語系規則：未翻譯時的處理方式、日期與數字格式。每俱樂部一份。</summary>
public sealed record AdminI18nSettingsDto
{
    /// <summary><c>show_default</c>（顯示繁中並標示「本頁尚無此語系版本」，預設）／<c>hide</c>（隱藏該頁）。</summary>
    public required string FallbackMode { get; init; }

    /// <summary>日期格式樣式，例 <c>YYYY/MM/DD</c>、<c>MMM D, YYYY</c>、<c>YYYY年M月D日</c>。</summary>
    public string? DateFormatZh { get; init; }

    public string? DateFormatEn { get; init; }

    /// <summary>數字格式範例，例 <c>1,234.56</c>、<c>1.234,56</c>、<c>1 234,56</c>。</summary>
    public string? NumberFormatZh { get; init; }

    public string? NumberFormatEn { get; init; }
}

public sealed record UpdateAdminI18nSettingsRequest
{
    public string? FallbackMode { get; init; }

    public string? DateFormatZh { get; init; }

    public string? DateFormatEn { get; init; }

    public string? NumberFormatZh { get; init; }

    public string? NumberFormatEn { get; init; }
}

public sealed record AdminTranslationRowDto
{
    /// <summary><c>article</c>／<c>faq</c>／<c>program</c>／<c>player</c>／<c>staff</c>／<c>charity_program</c>／<c>partner</c>／<c>sponsor</c>／<c>banner</c>。</summary>
    public required string Type { get; init; }

    public required string TypeLabel { get; init; }

    public required Guid Id { get; init; }

    /// <summary>繁中主要文字（標題／名稱）；繁中也沒有時為「（未命名）」。</summary>
    public required string Label { get; init; }

    /// <summary>兩隊共同內容（<c>club_id</c> 為空）。</summary>
    public required bool IsShared { get; init; }

    /// <summary>各啟用語系的完成狀態：語系代碼 → 主要文字欄位是否有值。</summary>
    public required IReadOnlyDictionary<string, bool> Done { get; init; }
}

public sealed record AdminTranslationSummaryDto
{
    public required string Type { get; init; }

    public required string TypeLabel { get; init; }

    public required int Total { get; init; }

    /// <summary>各語系尚未完成的筆數：語系代碼 → 筆數（預設語系也列出，通常為 0）。</summary>
    public required IReadOnlyDictionary<string, int> Missing { get; init; }
}

/// <summary>翻譯狀態總覽（矩陣）。</summary>
public sealed record AdminTranslationOverviewDto
{
    /// <summary>矩陣的欄：啟用中的語系，依排序。</summary>
    public required IReadOnlyList<string> Locales { get; init; }

    /// <summary>各類別筆數與缺漏數（不受 <c>type</c>／<c>missing</c>／<c>keyword</c> 篩選影響）。</summary>
    public required IReadOnlyList<AdminTranslationSummaryDto> Summary { get; init; }

    public required IReadOnlyList<AdminTranslationRowDto> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}
