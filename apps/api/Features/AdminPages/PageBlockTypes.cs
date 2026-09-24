namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// B1 頁面管理的區塊化編輯器型別（主站規劃書 §4.2 B1，約行 1010–1013：「區塊化編輯器：文字、
/// 圖文左右、圖片藝廊、影音嵌入、引言、CTA、手風琴 FAQ、時間軸、步驟條、數據卡、表格、檔案下載」）。
///
/// 🔴 逐字數了規劃書那一句列出的名稱，只有 12 個，但 <c>docs/12b-database-tables.md</c>
/// §3.1（`PageBlock` → `page_block_i18n`）與 <c>db/club-schema.sql</c> 的 <c>page_blocks</c>
/// 表註解都寫「13 種型別」——這是既有文件本身的一個小落差（數字與逐字列出的清單對不上），
/// 不是本次任務能力範圍內的東西：規劃書本體只給了這 12 個中文名稱，沒有第 13 個名稱可以對應，
/// 依任務指示「規劃書已定的照做；沒寫的不自創使用者可見功能」，本次只實作這 12 種，
/// 差異已回報給派工者（見交付報告），不在這裡杜撰一個沒有名稱的第 13 種型別。
///
/// 每個型別的英文代碼（<see cref="Text"/> 等常數值）是本次新增的執行層決定——規劃書只給中文
/// 名稱，資料庫欄位 <c>page_blocks.block_type</c> 需要一個穩定的字串鍵；命名比照
/// <c>articles.status</c>／<c>matches.status</c> 的 snake_case 慣例。
/// </summary>
public static class PageBlockTypes
{
    /// <summary>文字。內容：<c>body</c>（富文本字串）。</summary>
    public const string Text = "text";

    /// <summary>圖文左右。內容：<c>body</c>、<c>imagePosition</c>（<c>left</c>／<c>right</c>）、
    /// <c>image</c>（單張圖片欄位組，見 <see cref="PageBlockContentProcessor"/>）。</summary>
    public const string TextImage = "text_image";

    /// <summary>圖片藝廊。內容：<c>images</c>（圖片欄位組陣列，至少 1 張）。</summary>
    public const string Gallery = "gallery";

    /// <summary>影音嵌入。內容：<c>provider</c>（<c>youtube</c>／<c>vimeo</c>）、<c>videoId</c>、
    /// 可選 <c>caption</c>。不支援任意 iframe 網址——規劃書與 docs/14 GEO 條文都沒有開放
    /// 「貼任意嵌入碼」這種需要額外 XSS 防線的功能，本次不自創。</summary>
    public const string VideoEmbed = "video_embed";

    /// <summary>引言。內容：<c>text</c>、可選 <c>attribution</c>（引言來源）。</summary>
    public const string Quote = "quote";

    /// <summary>CTA。內容：<c>text</c>、<c>buttonLabel</c>、<c>buttonUrl</c>。</summary>
    public const string Cta = "cta";

    /// <summary>手風琴 FAQ。內容：<c>items</c>（至少 1 筆 <c>question</c>／<c>answer</c>）。
    /// ⚠️ 與後台 <c>B4 常見問題</c>／<c>faqs</c> 表是兩件事——這裡是頁面內嵌的問答區塊，
    /// 內容直接存在這個區塊的 <c>content</c> JSON 裡，不關聯 <c>faqs.id</c>（規劃書沒有寫
    /// 兩者要共用資料，屬各自獨立的內容單元）。</summary>
    public const string AccordionFaq = "accordion_faq";

    /// <summary>時間軸。內容：<c>items</c>（至少 1 筆 <c>date</c>／<c>title</c>／可選 <c>description</c>）。</summary>
    public const string Timeline = "timeline";

    /// <summary>步驟條。內容：<c>items</c>（至少 1 筆 <c>title</c>／可選 <c>description</c>）。</summary>
    public const string Steps = "steps";

    /// <summary>數據卡。內容：<c>items</c>（至少 1 筆 <c>value</c>／<c>label</c>）。</summary>
    public const string StatCards = "stat_cards";

    /// <summary>表格。內容：<c>headers</c>（至少 1 欄）、<c>rows</c>（每列欄數須與 <c>headers</c> 相等）。</summary>
    public const string Table = "table";

    /// <summary>檔案下載。內容：<c>label</c>、<c>fileUrl</c>。⚠️ 見 README「已知缺口」——
    /// 本次不提供新檔案上傳（<see cref="Images.IImageStorageService"/> 只處理圖片，會把任意檔案
    /// 重新編碼為 WebP，PDF 等下載檔走這條路會損毀檔案），<c>fileUrl</c> 只接受呼叫端已經取得的
    /// 外部網址或既有物件鍵字串。</summary>
    public const string FileDownload = "file_download";

    public static readonly IReadOnlyList<string> All =
    [
        Text, TextImage, Gallery, VideoEmbed, Quote, Cta, AccordionFaq, Timeline, Steps, StatCards, Table, FileDownload,
    ];

    private static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);

    public static bool IsKnown(string blockType) => AllSet.Contains(blockType);
}
