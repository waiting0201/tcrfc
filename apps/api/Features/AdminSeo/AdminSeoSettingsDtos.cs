namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 全站 SEO 預設＋追蹤碼（S1-12，主站規劃書 §4.8 H「全站 SEO 預設」「追蹤碼管理」）——GET 回應。
/// 標題樣板、預設描述、robots.txt 自訂規則、追蹤碼**不新增資料表**，存於既有 <c>settings</c>／
/// <c>settings_i18n</c>；預設 OG 圖片存於**既有的** <c>clubs.og_image_key</c>／
/// <c>_width</c>／<c>_height</c>（J4 品牌欄位，早已存在但原本刻意唯讀，見
/// <c>Features/AdminClubs/AdminClubDtos.cs</c> 的既有說明）——本輪由 <c>Features/AdminSeo</c>
/// 補上寫入路徑（見 <see cref="AdminSeoSettingsRepository"/> 檔頭），理由是「全站預設 OG 圖」
/// 屬於這個模組的編輯情境（跟標題模板同一張表單），不是 J4 品牌／法人資料的編輯情境。
/// 設定鍵詞彙見 <see cref="AdminSeoSettingsRepository"/> 檔頭，理由見 docs/12 §12 第 41 點。
/// </summary>
public sealed record AdminSeoSettingsDto
{
    /// <summary>標題樣板（中文，必填）。例："{title}｜台中磐石足球俱樂部"——後台不驗證
    /// <c>{title}</c> 佔位字串是否存在，前台如何套用樣板不在本次後端範圍（見任務回報）。</summary>
    public string? TitleTemplateZh { get; init; }

    public string? TitleTemplateEn { get; init; }

    /// <summary>預設描述（中文，必填）。頁面本身沒有設定單頁 Meta Description 時的全站回退值。</summary>
    public string? DefaultDescriptionZh { get; init; }

    public string? DefaultDescriptionEn { get; init; }

    /// <summary>robots.txt 自訂規則（線上編輯）。**技術語法非人類語言，不進 i18n 側表**——
    /// 管理員直接輸入要附加在自動產生規則之後的原始 robots.txt 指令列（例如額外的
    /// <c>Disallow:</c> 路徑），前台輸出時原樣附加。可為空（不附加任何自訂規則）。
    /// ⚠️ 只有在正式環境旗標為真時才會真的輸出到 <c>/robots.txt</c>，見
    /// <c>apps/web/server/routes/robots.txt.ts</c> 檔頭與 docs/17-deployment.md §10.4。</summary>
    public string? RobotsCustomRules { get; init; }

    public string? Ga4MeasurementId { get; init; }
    public string? GtmContainerId { get; init; }
    public string? MetaPixelId { get; init; }
    public string? LineTagId { get; init; }

    /// <summary>目前設定的全站預設 OG 圖片完整網址（<see cref="Images.IImagePublicUrlResolver"/>
    /// 解析後的值，不是原始物件鍵——後台畫面顯示用），<c>null</c>＝尚未設定。</summary>
    public string? OgImageUrl { get; init; }

    public int? OgImageWidth { get; init; }
    public int? OgImageHeight { get; init; }
}

/// <summary>
/// 更新全站 SEO 預設＋追蹤碼的請求（<c>multipart/form-data</c> 的 <c>payload</c> 欄位 JSON 內容，
/// OG 圖片檔案走同一次請求的 <c>ogImage</c> 欄位，規劃書 §4.0「選檔不上傳、儲存才上傳」）。
/// 文字欄位一律整份送出（跟 <see cref="AdminSeoSettingsDto"/> 檔頭說明一致，不是「省略＝維持
/// 不變」）；OG 圖片是唯一的三態欄位（維持／清空／換新），語意比照
/// <c>Features/AdminNews/UpdateArticleRequest.RemoveCover</c>。
/// </summary>
public sealed record UpdateSeoSettingsRequest
{
    public string? TitleTemplateZh { get; init; }
    public string? TitleTemplateEn { get; init; }
    public string? DefaultDescriptionZh { get; init; }
    public string? DefaultDescriptionEn { get; init; }
    public string? RobotsCustomRules { get; init; }
    public string? Ga4MeasurementId { get; init; }
    public string? GtmContainerId { get; init; }
    public string? MetaPixelId { get; init; }
    public string? LineTagId { get; init; }

    /// <summary>勾選「移除全站預設 OG 圖片」。跟這次請求的 <c>ogImage</c> 檔案欄位互斥——
    /// 兩者都有視為請求矛盾，回 400。兩者都沒有＝維持目前的圖片不變。</summary>
    public bool RemoveOgImage { get; init; }
}
