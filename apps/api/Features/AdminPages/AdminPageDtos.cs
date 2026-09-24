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
    /// <summary>值域見 <see cref="PageBlockTypes"/>。</summary>
    public required string BlockType { get; init; }

    public required JsonNode Content { get; init; }
}

/// <summary>
/// 建立頁面的請求（<c>payload</c> 這個 multipart 欄位的 JSON 內容）。⚠️ 沒有 <c>ClubId</c>——
/// 俱樂部由路由 <c>{club}</c> 決定（<see cref="Security.AdminClubScope"/>），不接受呼叫端指定。
/// </summary>
public sealed record CreatePageRequest
{
    /// <summary>畫面上叫「網址名稱」，對應 <c>pages.slug</c>，可含 <c>/</c> 表示分層路徑
    /// （見 <see cref="PageSlugPolicy"/>）。唯一鍵 <c>(club_id, slug)</c>。</summary>
    public required string Slug { get; init; }

    public required AdminPageSeoInput Seo { get; init; }

    /// <summary>區塊化編輯器的完整區塊清單，依陣列順序即排序（<c>page_blocks.sort_order</c>）——
    /// 「新增／排序／刪除」全部靠呼叫端送出這份完整清單來表達，不開獨立的單一區塊 CRUD 端點
    /// （見 apps/api/README.md「B1 頁面管理」一節「我的判斷」）。允許空陣列（頁面剛建立、還沒放
    /// 任何區塊，先存草稿的常見情境）。</summary>
    public required IReadOnlyList<AdminPageBlockInput> Blocks { get; init; }
}

/// <summary>更新頁面的請求。整份取代語意（跟 <c>UpdateArticleRequest</c> 一致）：
/// <see cref="Blocks"/> 是這個頁面之後應該有的**完整**區塊清單，省略的既有區塊視為被刪除。</summary>
public sealed record UpdatePageRequest
{
    public required string Slug { get; init; }
    public required AdminPageSeoInput Seo { get; init; }
    public required IReadOnlyList<AdminPageBlockInput> Blocks { get; init; }

    /// <summary>樂觀並行控制權杖：呼叫端上次讀到的 <c>pages.updated_at</c>。對不起來回 409。</summary>
    public required DateTime ExpectedUpdatedAt { get; init; }
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
    public required string Status { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
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
    public required int VersionNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public string? PreviewToken { get; init; }
    public required AdminPageSeoLocaleContent Zh { get; init; }
    public AdminPageSeoLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminPageBlockDto> Blocks { get; init; }
}
