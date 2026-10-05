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

    /// <summary>網址代稱（2026-10-05）：App 規劃書 §2.3 深連結 <c>tcrfc://player/{slug}</c> → <c>/zh/club/first-team/player/{slug}</c>。
    /// 同一俱樂部內唯一（<c>(club_id, slug)</c>），<c>[a-z0-9-]</c>。</summary>
    public required string Slug { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5「未翻譯 fallback 繁中並標示」、主站 G-01）：請求的是英文、而這筆的英文主要欄位（姓名）是空的，回應內容是回退的繁中時為 true。
    /// 請求繁中時恆為 false。用戶端據此顯示「本內容尚無英文版本」。</summary>
    public required bool IsFallbackLocale { get; init; }
    public required string TeamCode { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string? Nationality { get; init; }
    public string? PreferredFoot { get; init; }
    public string? PhotoKey { get; init; }
    /// <summary>
    /// 肖像同意是否已取得（Android 缺口 D1，2026-10-05）：<c>players.portrait_consent_status</c> 為 <c>consented</c>（本人）或 <c>consented_by_guardian</c>（未成年，監護人）時為 true，
    /// <c>not_consented</c>（預設，fail-closed）為 false。<b>刻意只給布林</b>，不洩漏是否為未成年（那是監護人同意的細節）。
    /// 契約：<see cref="PhotoUrl"/>／<see cref="PhotoKey"/> <b>非 null ⇒ 此值為 true</b>；此值為 false 時照片欄位一律 null，用戶端不得自行取得或顯示該人照片。
    /// ⚠️ <b>同意的「涵蓋範圍」目前沒有資料欄位</b>：現有三態不分辨「涵蓋官網」或「涵蓋 App 與商店頁面」（App 規劃書 §12.2、§16.2 第 14 項「同意書是否涵蓋 App 與商店頁面」是客戶未回覆的待決事項）。
    /// 所以這個值只代表「官網層級的肖像同意已取得」；App 要不要顯示照片須待該項決定，不可由此欄位推定已涵蓋 App。
    /// </summary>
    public required bool PortraitConsented { get; init; }
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
