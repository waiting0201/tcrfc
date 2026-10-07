using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>單一語系的 SEO 欄位。對應 <c>pages_i18n.seo_title</c>／<c>seo_description</c>——
/// ⚠️ <c>pages_i18n</c> 沒有 <c>title</c> 欄位，頁面標題與內文全部走區塊化編輯器
/// （docs/12c-i18n-tables.md §3.1「沒有列出 title／h1 欄位」），這裡不虛構一個資料庫沒有的欄位。</summary>
public sealed record AdminPageSeoLocaleContent
{
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }

    /// <summary>Meta Keywords（S1-12 新增，主站規劃書 §4.8 H「單頁 SEO」）。對應
    /// <c>pages_i18n.seo_keywords</c>，逐語系；規劃書沒有給格式規則，比照 <see cref="SeoTitle"/>
    /// 這類自由文字欄位不另外驗證分隔符號。</summary>
    public string? SeoKeywords { get; init; }

    /// <summary>OG 圖片替代文字（S1-12 驗收退回後補做）。對應 <c>pages_i18n.og_image_alt</c>，
    /// 逐語系。</summary>
    public string? OgImageAlt { get; init; }
}

/// <summary>建立／更新頁面時的雙語 SEO 輸入。<c>Zh</c> 必填其鍵本身（欄位可為 null 值），
/// <c>En</c> 可省略＝這個頁面目前沒有英文 SEO 設定（CLAUDE.md 全域規定 4：英文可空但欄位存在，
/// 側表 <c>pages_i18n</c> 沒有這一列即代表「空」，不需要用哨兵值）。</summary>
public sealed record AdminPageSeoInput
{
    public required AdminPageSeoLocaleContent Zh { get; init; }
    public AdminPageSeoLocaleContent? En { get; init; }
}

/// <summary>
/// 單一區塊的輸入。<see cref="Content"/> 是 <see cref="JsonNode"/>（可變動）而不是
/// <see cref="JsonElement"/>——<see cref="AdminPagesEndpoints"/> 需要把圖片上傳結果就地寫回這個
/// 節點（見 <see cref="PageBlockContentProcessor"/>），<c>JsonElement</c> 不可變動，做不到這件事。
/// </summary>
public sealed record AdminPageBlockInput
{
    /// <summary>值域見 <see cref="PageBlockTypes"/>。必須與頁面版型在同一位置的區塊類型一致。</summary>
    public required string BlockType { get; init; }

    /// <summary>區塊代號（<see cref="PageTemplateBlock.Key"/>）。可省略；有帶就必須與版型在同一位置的區塊代號一致
    /// （防止畫面讀到舊版型後送出錯位的內容）。</summary>
    public string? Key { get; init; }

    public required JsonNode Content { get; init; }
}

/// <summary>更新頁面的請求。整份取代語意：<see cref="Blocks"/> 是這個頁面之後的**完整**區塊清單，
/// 數量、類型、順序必須與頁面版型（<see cref="PageTemplates"/>）一致；不允許增刪列的區塊列數也須與版型一致，
/// 不符回 400（欄位鍵 <c>blocks</c>／<c>blocks[N]</c>／<c>blocks[N].items</c>）。</summary>
public sealed record UpdatePageRequest
{
    /// <summary>網址名稱不可變更。可省略；有帶就必須與現有值相同，否則 400（欄位鍵 <c>slug</c>）。</summary>
    public string? Slug { get; init; }
    public required AdminPageSeoInput Seo { get; init; }

    /// <summary>整份取代語意（省略＝清空覆寫值，
    /// 回到自動 canonical——這三個欄位跟頁面本體一樣採「整份取代」語意，不是跟標籤那組「省略＝
    /// 維持不變」，因為 B1 編輯頁本來就會把這些欄位一起讀出、一起存回，不存在「畫面上沒有這個
    /// 輸入框」的情境）。</summary>
    public string? CanonicalPath { get; init; }

    public bool IsNoindex { get; init; }
    public bool IsExcludedFromSitemap { get; init; }
    public required IReadOnlyList<AdminPageBlockInput> Blocks { get; init; }

    /// <summary>樂觀並行控制權杖：呼叫端上次讀到的 <c>pages.updated_at</c>。對不起來回 409。</summary>
    public required DateTime ExpectedUpdatedAt { get; init; }

    /// <summary>勾選「移除 OG 圖片」（S1-12 驗收退回後補做）。跟這次請求的 <c>ogImage</c> 檔案
    /// 欄位互斥，兩者都有視為請求矛盾，回 400。兩者都沒有＝維持目前的 OG 圖片不變。</summary>
    public bool RemoveOgImage { get; init; }
}

public sealed record PublishPageRequest
{
    public required DateTime ExpectedUpdatedAt { get; init; }
}

public sealed record SchedulePageRequest
{
    public required DateTime ExpectedUpdatedAt { get; init; }

    /// <summary>排程發布時間（UTC）。必須晚於呼叫當下，否則 400。</summary>
    public required DateTime PublishAt { get; init; }
}

/// <summary>還原到某個歷史版本。規劃書只寫「版本歷程與還原」，沒有定義還原後的行為——
/// 本次採「還原＝以舊版內容產生一個新版本」（見 apps/api/README.md「我的判斷」），因此還原本身
/// 也是一次內容變更，一樣要求並行權杖，行為與 <see cref="UpdatePageRequest"/> 一致。</summary>
public sealed record RestorePageVersionRequest
{
    public required DateTime ExpectedUpdatedAt { get; init; }
}

/// <summary>單一區塊的輸出。</summary>
public sealed record AdminPageBlockDto
{
    public required Guid Id { get; init; }

    /// <summary>版型區塊代號；結構與版型不符的舊資料或版本快照為 <c>null</c>。</summary>
    public string? Key { get; init; }

    /// <summary>後台顯示的區塊名稱（版型 <see cref="PageTemplateBlock.LabelZh"/>）；同上，不符時為 <c>null</c>。</summary>
    public string? LabelZh { get; init; }

    public required string BlockType { get; init; }
    public required JsonElement Content { get; init; }
    public required int SortOrder { get; init; }
}

/// <summary>後台頁面清單一列。⚠️ 不含標題（<c>pages_i18n</c> 沒有這個欄位），用 SEO 標題作為
/// 清單上唯一可辨識這一頁「大概是什麼」的雙語文字，僅供後台清單顯示，不是資料庫的正式標題欄位。</summary>
public sealed record AdminPageListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>版型鍵（同 <see cref="Slug"/>；每個俱樂部內版型以 slug 唯一）。</summary>
    public required string TemplateKey { get; init; }

    /// <summary>頁名（版型定義，日常中文／英文）。</summary>
    public required string TitleZh { get; init; }

    public string? TitleEn { get; init; }

    /// <summary>值域 <c>draft</c>／<c>published</c>／<c>scheduled</c>（<c>pages.status</c> 的 CHECK 約束）。</summary>
    public required string Status { get; init; }

    public DateTime? PublishedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? SeoTitleZh { get; init; }
    public string? SeoTitleEn { get; init; }
}

/// <summary>後台單頁詳情（編輯頁用）。<see cref="UpdatedAt"/> 是下一次寫入要帶回來的並行權杖。</summary>
public sealed record AdminPageDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>頁面版型：頁名、每個區塊的名稱／類型／可否增刪列。後台畫面依此渲染固定欄位。</summary>
    public required AdminPageTemplateDto Template { get; init; }

    public required string Status { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? CanonicalPath { get; init; }
    public bool IsNoindex { get; init; }
    public bool IsExcludedFromSitemap { get; init; }

    /// <summary>OG 圖片完整網址（S1-12 驗收退回後補做），<c>null</c>＝這個頁面沒有設定專屬
    /// OG 圖片。</summary>
    public string? OgImageUrl { get; init; }

    public int? OgImageWidth { get; init; }
    public int? OgImageHeight { get; init; }
    public required AdminPageSeoLocaleContent Zh { get; init; }
    public AdminPageSeoLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminPageBlockDto> Blocks { get; init; }

    /// <summary>最新一個版本的編號，對應 <c>page_versions.version_no</c> 的最大值。</summary>
    public required int LatestVersionNo { get; init; }

    /// <summary>最新版本的預覽權杖——未發布也可以把這個值組成分享連結（見 apps/api/README.md
    /// 「預覽連結：權杖何時產生」）。理論上不會是 <c>null</c>（每次建立／更新都會產生新版本與新權杖），
    /// 型別維持可為空只是防禦性寫法，避免舊資料或未來邏輯調整時讓呼叫端誤以為一定有值。</summary>
    public string? PreviewToken { get; init; }
}

/// <summary>版本歷程清單一列。</summary>
public sealed record AdminPageVersionListItemDto
{
    public required int VersionNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public string? PreviewToken { get; init; }
}

/// <summary>單一版本的完整快照內容（還原前先看內容用）。</summary>
public sealed record AdminPageVersionDetailDto
{
    /// <summary>這個版本的區塊結構（數量、類型、固定列數）是否與目前版型一致；<c>false</c> 時還原會被拒絕（400，鍵 <c>versionNo</c>）。</summary>
    public required bool StructureMatchesTemplate { get; init; }

    public required int VersionNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public string? PreviewToken { get; init; }
    public required AdminPageSeoLocaleContent Zh { get; init; }
    public AdminPageSeoLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminPageBlockDto> Blocks { get; init; }
}

/// <summary>版型中的單一區塊（給後台畫面依此渲染固定欄位）。</summary>
public sealed record AdminPageTemplateBlockDto
{
    public required string Key { get; init; }
    public required string BlockType { get; init; }
    public required string LabelZh { get; init; }
    public string? HintZh { get; init; }

    /// <summary>可重複項目是否允許增刪列。</summary>
    public required bool AllowRowEdit { get; init; }

    /// <summary>不允許增刪列時的固定列數，否則 <c>null</c>。</summary>
    public int? FixedRowCount { get; init; }

    /// <summary>可重複項目在區塊內容裡的屬性名（<c>items</c>／<c>images</c>／<c>rows</c>），沒有則 <c>null</c>。</summary>
    public string? RowsField { get; init; }
}

public sealed record AdminPageTemplateDto
{
    public required string Key { get; init; }
    public required string TitleZh { get; init; }
    public string? TitleEn { get; init; }
    public required IReadOnlyList<AdminPageTemplateBlockDto> Blocks { get; init; }

    public static AdminPageTemplateDto From(PageTemplate template) => new()
    {
        Key = template.Slug,
        TitleZh = template.TitleZh,
        TitleEn = template.TitleEn,
        Blocks = template.Blocks.Select(b => new AdminPageTemplateBlockDto
        {
            Key = b.Key, BlockType = b.BlockType, LabelZh = b.LabelZh, HintZh = b.HintZh,
            AllowRowEdit = b.AllowRowEdit, FixedRowCount = b.FixedRowCount, RowsField = b.RowsField,
        }).ToList(),
    };
}
