using Tcrfc.Api.Features.Seo;

namespace Tcrfc.Api.Features.Players;

/// <summary>
/// 球員名單公開欄位。<c>players</c> 不在 docs/12b-database-tables.md §8 受限欄位清單內——
/// 球員名冊（含生日／慣用腳等）是球隊官網例行公開的競技資訊，不是一般會員個資。
/// 🔴 <see cref="PhotoKey"/> 例外：<c>portrait_consent_status = 'not_consented'</c> 時一律回
/// <c>null</c>（fail-closed），見 <c>PlayersRepository.Map</c>——肖像同意未到位不得輸出照片，
/// docs/12 §12 第 32 點、藍鯨規劃書行 193。
/// </summary>
public sealed record PlayerDto
{
    public required Guid Id { get; init; }
    public required string TeamCode { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string? Nationality { get; init; }
    public string? PreferredFoot { get; init; }
    public string? PhotoKey { get; init; }
    public string? Name { get; init; }
    public string? Bio { get; init; }

    /// <summary>球員照片完整可公開存取網址（S1-12f 新增），由已套用肖像同意 fail-closed 規則後的
    /// <see cref="PhotoKey"/> 經 <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出——
    /// 未同意肖像使用的球員 <see cref="PhotoKey"/> 已經是 <c>null</c>，這裡不需要再檢查一次同意狀態，
    /// 沿用同一個 fail-closed 結果即可。</summary>
    public string? PhotoUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這筆球員資料是否足以輸出 Person 結構化資料
    /// （<see cref="SchemaType.Person"/> 只要求 <c>name</c>）。判斷條件單一來源見
    /// <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。肖像同意不影響本欄位——
    /// 只影響 <see cref="PhotoUrl"/> 有沒有值，見 <see cref="SchemaRequiredFields"/> 檔頭「Person」段。</summary>
    public required bool SchemaEligible { get; init; }
}
