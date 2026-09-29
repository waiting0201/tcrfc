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
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public string? HeroKey { get; init; }
    public string? Name { get; init; }
    public string? Intro { get; init; }

    /// <summary><see cref="HeroKey"/> 完整可公開存取網址（E-64 修正，2026-09-29）——比照
    /// <c>Features/Staff/StaffDto.PhotoUrl</c> 的既有慣例，由
    /// <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出。跟 <see cref="LogoUrl"/>
    /// 不是同一件事：這裡只回這支球隊自己的識別圖片，沒有回退俱樂部隊徽——回退後的網址已經由
    /// <see cref="LogoUrl"/> 提供，兩者分工不同（<see cref="HeroUrl"/> 給球隊頁 Hero 版位用，
    /// <see cref="LogoUrl"/> 給 GEO-05 結構化資料用）。<c>null</c>＝這支球隊沒有自己的識別圖片。
    /// </summary>
    public string? HeroUrl { get; init; }

    /// <summary>球隊識別圖片完整可公開存取網址（S1-12f 新增）。優先序：這支球隊自己的
    /// <c>HeroKey</c> &gt; 所屬俱樂部的隊徽（<c>Club.LogoLightKey</c>）——<see cref="SchemaRequiredFields"/>
    /// 檔頭「logo（Club.LogoLightKey／Team.HeroKey）」的「／」判讀為「擇一即可」，比照既有
    /// OG 圖片優先序（專屬 &gt; 全站預設）同一種寫法，不是另開一份規則。<c>null</c>＝兩者皆無。</summary>
    public string? LogoUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這支球隊的資料是否足以輸出 SportsTeam 結構化資料
    /// （<see cref="SchemaType.SportsTeam"/> 必填欄位——隊名、網址、識別圖片——齊全）。判斷條件
    /// 單一來源見 <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。🔴 現況：多數球隊
    /// 沒有自己的 <c>HeroKey</c>，且俱樂部隊徽 <c>LogoLightKey</c> 目前恆為 null（見
    /// <c>Tcrfc.Api.Features.Clubs.ClubDto.SchemaEligible</c> 同一個已知現況），本欄位在現況下多半
    /// 為 <c>false</c>，是 GEO-05「缺漏者不輸出該型別」的正確行為。</summary>
    public required bool SchemaEligible { get; init; }
}
