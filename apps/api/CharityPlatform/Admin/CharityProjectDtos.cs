using System.Text.Json;

namespace Tcrfc.Api.CharityPlatform.Admin;

// N2 捐款項目管理（規劃書 §6.2）。⚠️ 沒有「目標金額」欄位（§3.2 v1.2：前台不呈現募款進度，各項目累計數字只存在於 N6 捐款報表，
// 所以本模組的列表與詳情<b>刻意沒有累計筆數或金額</b>）。項目<b>不得附回饋品</b>（v1.3）。

public sealed record AdminProjectListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string? NameZh { get; init; }
    public required string? NameEn { get; init; }

    /// <summary><c>draft</c>（草稿，前台看不到）／<c>published</c>（已上架）。</summary>
    public required string Status { get; init; }

    public required int SortOrder { get; init; }

    /// <summary><c>b2c_invoice</c>（電子發票）／<c>donation_receipt</c>（捐贈收據）。</summary>
    public required string InvoiceMode { get; init; }

    public required decimal ProjectSharePct { get; init; }
    public required int? MinAmount { get; init; }
    public required int? MaxAmount { get; init; }
    public required string? CoverUrl { get; init; }
    public required string? CharityName { get; init; }
}

public sealed record AdminProjectDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string? NameZh { get; init; }
    public required string? NameEn { get; init; }
    public required string? OneLinerZh { get; init; }
    public required string? OneLinerEn { get; init; }

    /// <summary>說明內文（區塊編輯器輸出的 JSON，原樣回傳）。</summary>
    public required JsonElement? DescriptionZh { get; init; }

    public required JsonElement? DescriptionEn { get; init; }
    public required string? FundUsageZh { get; init; }
    public required string? FundUsageEn { get; init; }
    public required string? CoverAltZh { get; init; }
    public required string? CoverAltEn { get; init; }
    public required string? CoverUrl { get; init; }
    public required string Status { get; init; }
    public required int SortOrder { get; init; }
    public required string InvoiceMode { get; init; }
    public required decimal ProjectSharePct { get; init; }
    public required int? MinAmount { get; init; }
    public required int? MaxAmount { get; init; }

    /// <summary>金額選項卡（由小到大）。</summary>
    public required IReadOnlyList<int> AmountOptions { get; init; }

    /// <summary>撥付對象與關聯慈善計畫：選定後<b>值複製</b>到本項目的快照欄位（不是外鍵），之後主站改名不影響已開立的憑證。</summary>
    public required string? CharityRefCode { get; init; }

    public required string? CharityName { get; init; }
    public required string? CharityProgramRefCode { get; init; }
    public required string? CharityProgramName { get; init; }
}

/// <summary>建立／更新項目。分潤百分比需要獨立的「設定分潤」權限；省略代表不變（更新）或 0（建立）。
/// 撥付對象與慈善計畫只傳參照碼（<c>charity_refs</c>／<c>charity_program_refs</c> 的 <c>ref_code</c>），名稱由伺服器從唯讀複本複製。</summary>
public sealed record UpsertProjectRequest
{
    /// <summary>網址名稱；省略時由英文名稱自動產生（更新時省略代表不變）。</summary>
    public string? Slug { get; init; }

    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? OneLinerZh { get; init; }
    public string? OneLinerEn { get; init; }
    public JsonElement? DescriptionZh { get; init; }
    public JsonElement? DescriptionEn { get; init; }
    public string? FundUsageZh { get; init; }
    public string? FundUsageEn { get; init; }
    public string? CoverAltZh { get; init; }
    public string? CoverAltEn { get; init; }

    public int? MinAmount { get; init; }
    public int? MaxAmount { get; init; }

    /// <summary>金額選項卡。省略代表不變；傳空陣列代表清空。</summary>
    public IReadOnlyList<int>? AmountOptions { get; init; }

    public decimal? ProjectSharePct { get; init; }
    public string? InvoiceMode { get; init; }
    public string? CharityRefCode { get; init; }
    public string? CharityProgramRefCode { get; init; }
    public int? SortOrder { get; init; }
}

/// <summary>只編輯項目內文（區塊編輯器儲存用）。<b>省略（<c>null</c>）的欄位不變</b>；說明內文送 <c>{}</c> 或 <c>[]</c>、其他欄位送空字串代表清空。</summary>
public sealed record UpdateProjectContentRequest
{
    public string? OneLinerZh { get; init; }
    public string? OneLinerEn { get; init; }
    public JsonElement? DescriptionZh { get; init; }
    public JsonElement? DescriptionEn { get; init; }
    public string? FundUsageZh { get; init; }
    public string? FundUsageEn { get; init; }
}
