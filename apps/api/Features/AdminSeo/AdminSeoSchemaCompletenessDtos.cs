namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>一個缺漏欄位（後台顯示用，介面一律日常中文，不顯示欄位英文代碼——比照
/// CLAUDE.md 全域規定第 9 條）。</summary>
public sealed record SchemaCompletenessFieldDto
{
    public required string LabelZh { get; init; }
    public string? LabelEn { get; init; }
}

/// <summary>
/// 一筆結構化資料完整性缺漏（GEO-05／S1-12c）。<see cref="SchemaTypeName"/> 是 schema.org
/// 本身的 <c>@type</c> 字面值（<see cref="Seo.SchemaTypeCodes"/>），<see cref="EntityType"/>
/// 是本專案既有的內部型別詞彙（沿用 <c>OrphanPageDto.EntityType</c> 同一套值域：
/// <c>club</c>／<c>team</c>／<c>event</c>／<c>match</c>／<c>player</c>／<c>article</c>／
/// <c>program</c>／<c>page</c>／<c>faq</c>），供後台前端依型別分組或加連結時判斷用。
/// </summary>
public sealed record SchemaCompletenessIssueDto
{
    public required string SchemaTypeName { get; init; }
    public required string EntityType { get; init; }
    public required Guid Id { get; init; }

    /// <summary>後台顯示用的辨識名稱（球員姓名、文章標題……），沒有可用名稱時為 <c>null</c>，
    /// 後台以 <c>Id</c> 顯示即可。</summary>
    public string? Label { get; init; }

    /// <summary>公開網址（只有文章／頁面／FAQ 這類有對外網址的型別才有值）。</summary>
    public string? Path { get; init; }

    public required IReadOnlyList<SchemaCompletenessFieldDto> MissingFields { get; init; }
}

public sealed record SchemaCompletenessReportDto
{
    public required IReadOnlyList<SchemaCompletenessIssueDto> Items { get; init; }
}
