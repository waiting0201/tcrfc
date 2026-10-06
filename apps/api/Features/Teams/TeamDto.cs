using Tcrfc.Api.Features.Seo;

namespace Tcrfc.Api.Features.Teams;

/// <summary>球隊公開欄位（C1）。<c>teams</c> 不在 docs/12b-database-tables.md §8 受限欄位清單內——
/// 球隊主檔（代號、類型、性別、代表色）是球隊官網例行公開的資訊。S1-7 新增：既有的 S0-7b 唯讀端點
/// 只做了球員／教練與職員／新聞／賽程／俱樂部主檔五組，球隊本身沒有獨立的公開清單端點——
/// 前台的球隊選單、行事曆分類（13）、學院年齡層頁需要這份清單，本輪補上，是新增端點不是契約變更。</summary>
public sealed record TeamDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }

    /// <summary>所屬俱樂部代碼（<c>tcrfc</c>／<c>bw</c>…）。App 把兩個俱樂部的球隊放在同一份清單時用它分區（缺口 A2）；
    /// 等於請求路徑的 <c>{club}</c>。⚠️ 球隊與賽事系列沒有直接關聯（<c>competitions</c> 掛在賽季上、賽事掛在系列上），所以這裡沒有賽事系列代碼。</summary>
    public required string ClubCode { get; init; }

    /// <summary>球隊類型，<b>值域固定兩個</b>（db/club-schema.sql 的 CHECK）：<c>first_team</c>（一線隊）、<c>academy</c>（學院／青年梯隊）。
    /// 契約見 <c>shared/enums.json</c>（缺口 A10）。</summary>
    public required string Type { get; init; }

    /// <summary>性別，值域固定三個（db/club-schema.sql 的 CHECK）：<c>men</c>、<c>women</c>、<c>mixed</c>。</summary>
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public string? HeroKey { get; init; }
    public string? Name { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5「未翻譯 fallback 繁中並標示」、主站 G-01）：請求的是英文、而這筆的英文主要欄位（球隊名稱）是空的，回應內容是回退的繁中時為 true。
    /// 請求繁中時恆為 false。用戶端據此顯示「本內容尚無英文版本」。</summary>
    public required bool IsFallbackLocale { get; init; }
    public string? Intro { get; init; }

    /// <summary><see cref="HeroKey"/> 完整可公開存取網址（E-64 修正，2026-09-29）——比照
    /// <c>Features/Staff/StaffDto.PhotoUrl</c> 的既有慣例，由
    /// <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出。這裡只回這支球隊自己的識別圖片
    /// （球隊頁 Hero 版位用）。<c>null</c>＝這支球隊沒有自己的識別圖片。🔴 v3.20 起俱樂部標誌由前台靜態資產定義，
    /// 本 DTO 不再有 <c>LogoUrl</c>。
    /// </summary>
    public string? HeroUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這支球隊的資料是否足以輸出 SportsTeam 結構化資料
    /// （<see cref="SchemaType.SportsTeam"/> 必填欄位——隊名、網址——齊全）。判斷條件
    /// 單一來源見 <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。🔴 v3.20：標誌由前台靜態資產輸出，不再是資料庫必填欄位。</summary>
    public required bool SchemaEligible { get; init; }
}
