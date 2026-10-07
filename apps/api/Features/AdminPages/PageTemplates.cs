using System.Text.Json.Nodes;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// B1 頁面管理的「版型」定義——<b>後端唯一來源</b>（使用者 2026-10-07 拍板：頁面管理改為「固定頁＋固定欄位」）。
///
/// 規則：
/// ① 頁面清單固定，只能編輯，不能新增、不能刪除（沒有對應的 POST／DELETE 端點）。
/// ② 每一頁的區塊數量、類型、順序由版型決定，不能增刪排序區塊。
/// ③ 區塊內的「可重複項目」（時間軸條目、步驟／價值卡片、數據卡、問答題目、藝廊圖片、表格列）是否可增刪列，
///    逐區塊標示（<see cref="PageTemplateBlock.AllowRowEdit"/>）；不可增刪列的區塊，列數固定為
///    <see cref="PageTemplateBlock.FixedRowCount"/>。
///
/// 🔴 區塊結構依 <c>apps/web/app/pages/zh/**</c> 各頁「CMS 已發布就取代主內文」那一段的備用內容設計
/// （<c>useCmsPage</c> → <c>ContentCmsPageBand</c>）：版型渲染後與前台寫死的主內文大致相同；hero、麵包屑、CTA 卡不在版型內。
/// 種子（<c>db/seed/backoffice_seed.py</c>）填入的區塊內容必須與本檔結構一致，由
/// <c>AdminPageTemplatesTests</c> 對資料庫逐頁核對。
///
/// 版型以「俱樂部＋slug」為鍵：多數頁兩隊同構，但藍鯨與磐石的敘事框架不同的頁
/// （願景使命、足球理念、歷程、球員故事）結構不同，不強行共用。
/// 單元對藍鯨關閉的頁（06 女子足球、11 慈善）只有磐石版型（apps/web/shared/utils/units.ts）。
/// </summary>
public sealed record PageTemplateBlock
{
    /// <summary>區塊代號（ASCII，同一版型內唯一）。</summary>
    public required string Key { get; init; }

    /// <summary>值域見 <see cref="PageBlockTypes"/>。</summary>
    public required string BlockType { get; init; }

    /// <summary>後台顯示的區塊名稱（日常中文，例：「開場引言」「核心價值」）。</summary>
    public required string LabelZh { get; init; }

    /// <summary>編輯提示（一句話，可空）。</summary>
    public string? HintZh { get; init; }

    /// <summary>可重複項目是否允許增刪列。沒有可重複項目的型別（文字、引言……）一律 <c>false</c>。</summary>
    public bool AllowRowEdit { get; init; }

    /// <summary>不允許增刪列時的固定列數；允許增刪列或型別沒有列時為 <c>null</c>。</summary>
    public int? FixedRowCount { get; init; }

    /// <summary>可重複項目在區塊內容 JSON 裡的屬性名：<c>items</c>／<c>images</c>／<c>rows</c>；沒有則 <c>null</c>。</summary>
    public string? RowsField => PageTemplates.RowsFieldOf(BlockType);
}

public sealed record PageTemplate
{
    public required string ClubCode { get; init; }

    /// <summary>頁面網址名稱，即 <c>pages.slug</c>（不可變更）。同一俱樂部內唯一，也作為版型鍵。</summary>
    public required string Slug { get; init; }

    public required string TitleZh { get; init; }
    public string? TitleEn { get; init; }
    public required IReadOnlyList<PageTemplateBlock> Blocks { get; init; }

    /// <summary>缺頁時，後台清單是否自動補建草稿骨架頁（<see cref="AdminPagesRepository.EnsureTemplatePagesAsync"/>）。
    /// 正式版型一律 <c>true</c>；只有測試主機的測試專用版型設為 <c>false</c>（避免清單呼叫在資料庫留下測試頁）。</summary>
    public bool ProvisionWhenMissing { get; init; } = true;
}

/// <summary>版型目錄（依賴注入接縫）。正式環境一律是 <see cref="PageTemplates.All"/>；測試主機可另外註冊
/// 測試專用版型（<c>test/…</c>），讓寫入、版本、圖片上傳這些「會改資料」的測試不必動到真正的固定頁。</summary>
public interface IPageTemplateCatalog
{
    IReadOnlyList<PageTemplate> ForClub(string clubCode);
    PageTemplate? Find(string clubCode, string slug);
}

public sealed class PageTemplateCatalog(IReadOnlyList<PageTemplate> templates) : IPageTemplateCatalog
{
    public static PageTemplateCatalog Default { get; } = new(PageTemplates.All);

    public IReadOnlyList<PageTemplate> ForClub(string clubCode)
        => templates.Where(t => string.Equals(t.ClubCode, clubCode, StringComparison.Ordinal)).ToList();

    public PageTemplate? Find(string clubCode, string slug)
        => templates.FirstOrDefault(t => t.ClubCode == clubCode && t.Slug == slug);
}

public static class PageTemplates
{
    /// <summary>每個俱樂部的版型清單，順序即後台清單顯示順序（依前台單元編號）。</summary>
    private static readonly IReadOnlyList<PageTemplate> AllTemplates = Build();

    public static IReadOnlyList<PageTemplate> All => AllTemplates;

    public static string? RowsFieldOf(string blockType) => blockType switch
    {
        PageBlockTypes.AccordionFaq or PageBlockTypes.Timeline or PageBlockTypes.Steps or PageBlockTypes.StatCards => "items",
        PageBlockTypes.Gallery => "images",
        PageBlockTypes.Table => "rows",
        _ => null,
    };

    // ───────────────────────────── 結構驗證 ─────────────────────────────

    /// <summary>
    /// 檢查「區塊清單的數量、類型、順序」與版型一致。<paramref name="blocks"/> 的 <c>Key</c> 可省略；
    /// 有帶就必須與版型的區塊代號相同。不符一律丟 <see cref="AdminPageValidationException"/>（400），
    /// 欄位鍵：數量不符 <c>blocks</c>；第 N 個區塊類型或代號不符 <c>blocks[N]</c>。
    /// </summary>
    public static void ValidateStructure(PageTemplate template, IReadOnlyList<(string BlockType, string? Key)> blocks)
    {
        if (blocks.Count != template.Blocks.Count)
        {
            throw new AdminPageValidationException(
                $"這個頁面固定有 {template.Blocks.Count} 個區塊，不能新增或刪除區塊（目前送出 {blocks.Count} 個）。請重新整理頁面後再編輯。", "blocks");
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var def = template.Blocks[i];
            if (!string.Equals(blocks[i].BlockType, def.BlockType, StringComparison.Ordinal)
                || (blocks[i].Key is { Length: > 0 } key && !string.Equals(key, def.Key, StringComparison.Ordinal)))
            {
                throw new AdminPageValidationException(
                    $"第 {i + 1} 個區塊「{def.LabelZh}」的類型或位置與頁面版型不符，不能更換或調換順序。請重新整理頁面後再編輯。",
                    FieldKeyOfBlock(i));
            }
        }
    }

    /// <summary>
    /// 檢查單一區塊（內容已通過 <see cref="PageBlockContentProcessor"/> 驗證）的列數是否符合版型：
    /// 不允許增刪列的區塊，列數必須等於 <see cref="PageTemplateBlock.FixedRowCount"/>。欄位鍵 <c>blocks[N].items</c>。
    /// </summary>
    public static void ValidateRows(PageTemplateBlock def, int blockIndex, JsonNode? content)
    {
        if (def.AllowRowEdit || def.FixedRowCount is not { } fixedCount || def.RowsField is not { } field)
        {
            return;
        }

        var count = (content as JsonObject)?[field] is JsonArray array ? array.Count : 0;
        if (count != fixedCount)
        {
            throw new AdminPageValidationException(
                $"第 {blockIndex + 1} 個區塊「{def.LabelZh}」的項目數量固定為 {fixedCount} 項，不能新增或刪除（目前 {count} 項）。",
                $"blocks[{blockIndex}].{field}");
        }
    }

    /// <summary>已存在的區塊列（例：版本快照）結構是否與版型完全一致（數量、類型、固定列數）。</summary>
    public static bool Matches(PageTemplate template, IReadOnlyList<(string BlockType, JsonNode? Content)> blocks)
    {
        if (blocks.Count != template.Blocks.Count)
        {
            return false;
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var def = template.Blocks[i];
            if (!string.Equals(blocks[i].BlockType, def.BlockType, StringComparison.Ordinal))
            {
                return false;
            }

            if (!def.AllowRowEdit && def.FixedRowCount is { } fixedCount && def.RowsField is { } field)
            {
                var count = (blocks[i].Content as JsonObject)?[field] is JsonArray array ? array.Count : 0;
                if (count != fixedCount)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string FieldKeyOfBlock(int index) => $"blocks[{index}]";

    // ───────────────────────────── 空白骨架 ─────────────────────────────

    /// <summary>
    /// 版型的空白內容（所有文字為空、固定列數的列已備好）。只用在「清單發現版型缺頁時補建草稿頁」
    /// （<see cref="AdminPagesRepository.EnsureTemplatePagesAsync"/>）：骨架不通過內容驗證（中文必填），
    /// 編輯者必須填完才能存檔；它不會是已發布狀態，公開端點看不到。
    /// </summary>
    public static JsonNode BuildSkeleton(PageTemplateBlock def)
    {
        static JsonObject Bi() => new() { ["zh"] = "", ["en"] = null };
        var rowCount = def.FixedRowCount ?? 1;

        switch (def.BlockType)
        {
            case PageBlockTypes.Text:
                return new JsonObject { ["body"] = Bi() };
            case PageBlockTypes.Quote:
                return new JsonObject { ["text"] = Bi() };
            case PageBlockTypes.Steps:
                return new JsonObject { ["items"] = new JsonArray(Enumerable.Range(0, rowCount).Select(_ => (JsonNode)new JsonObject { ["title"] = Bi() }).ToArray()) };
            case PageBlockTypes.Timeline:
                return new JsonObject { ["items"] = new JsonArray(Enumerable.Range(0, rowCount).Select(_ => (JsonNode)new JsonObject { ["date"] = "", ["title"] = Bi() }).ToArray()) };
            case PageBlockTypes.StatCards:
                return new JsonObject { ["items"] = new JsonArray(Enumerable.Range(0, rowCount).Select(_ => (JsonNode)new JsonObject { ["value"] = "", ["label"] = Bi() }).ToArray()) };
            case PageBlockTypes.AccordionFaq:
                return new JsonObject { ["items"] = new JsonArray(Enumerable.Range(0, rowCount).Select(_ => (JsonNode)new JsonObject { ["question"] = Bi(), ["answer"] = Bi() }).ToArray()) };
            default:
                throw new NotSupportedException($"版型區塊 {def.Key} 的類型 {def.BlockType} 尚未提供空白骨架。");
        }
    }

    // ───────────────────────────── 版型清單 ─────────────────────────────

    private static PageTemplateBlock Text(string key, string label, string? hint = null)
        => new() { Key = key, BlockType = PageBlockTypes.Text, LabelZh = label, HintZh = hint };

    private static PageTemplateBlock Quote(string key, string label, string? hint = null)
        => new() { Key = key, BlockType = PageBlockTypes.Quote, LabelZh = label, HintZh = hint };

    /// <summary>步驟／價值卡片：標題＋說明。列數固定。</summary>
    private static PageTemplateBlock FixedSteps(string key, string label, int rows, string? hint = null)
        => new() { Key = key, BlockType = PageBlockTypes.Steps, LabelZh = label, HintZh = hint, AllowRowEdit = false, FixedRowCount = rows };

    private static PageTemplateBlock EditableSteps(string key, string label, string? hint = null)
        => new() { Key = key, BlockType = PageBlockTypes.Steps, LabelZh = label, HintZh = hint, AllowRowEdit = true };

    private static PageTemplateBlock EditableTimeline(string key, string label, string? hint = null)
        => new() { Key = key, BlockType = PageBlockTypes.Timeline, LabelZh = label, HintZh = hint, AllowRowEdit = true };

    private static PageTemplate T(string club, string slug, string titleZh, string? titleEn, params PageTemplateBlock[] blocks)
        => new() { ClubCode = club, Slug = slug, TitleZh = titleZh, TitleEn = titleEn, Blocks = blocks };

    private const string Tcrfc = "tcrfc";
    private const string Bw = "bw";

    private static IReadOnlyList<PageTemplate> Build()
    {
        var list = new List<PageTemplate>();

        // ── 磐石（12 頁）──
        list.Add(T(Tcrfc, "about/our-story", "我們的故事", "Our Story",
            Text("story", "故事內文", "介紹俱樂部的成立與定位，段落之間空一行。")));

        list.Add(T(Tcrfc, "about/vision-mission", "願景與使命", "Vision & Mission",
            FixedSteps("visionMission", "願景與使命", 2, "固定兩項：願景、使命。")));

        list.Add(T(Tcrfc, "about/philosophy", "足球理念", "Our Philosophy",
            Text("valuesIntro", "核心價值說明", "五大核心價值上方的一句說明。"),
            FixedSteps("coreValues", "五大核心價值", 5, "固定五項：以球員為本、追求卓越、國際發展、社區共好、誠信專業。")));

        list.Add(T(Tcrfc, "about/governance", "治理與管理", "Governance",
            Text("documentsNote", "公開文件說明", "公開文件區的說明文字；可下載檔案清單尚未開放維護。")));

        list.Add(T(Tcrfc, "about/history", "俱樂部歷程", "Club History",
            Text("history", "歷程內文")));

        list.Add(T(Tcrfc, "club/player-development", "球員發展系統", "Player Development",
            FixedSteps("modules", "八大發展模組", 8, "固定八項，每項有名稱與說明。"),
            Text("summary", "總結說明", "第一段為小標題，第二段為說明。")));

        list.Add(T(Tcrfc, "club/opportunities", "球員機會", "Player Opportunities",
            Text("joinIntro", "加入說明", "「加入球隊」區的說明；試訓場次、常見問題與報名按鈕不在此編輯。")));

        list.Add(T(Tcrfc, "club/international-pathways", "國際發展通道", "International Pathways",
            FixedSteps("pathway", "路徑總覽", 4, "固定四個階段。")));

        list.Add(T(Tcrfc, "club/player-stories", "球員故事", "Player Stories",
            EditableSteps("stories", "球員故事案例", "每一列是一位球員：標題為姓名，說明為位置與背號等簡述。"),
            Text("note", "補充說明")));

        list.Add(T(Tcrfc, "womens", "女子足球", "Women's Football",
            Text("intro", "藍鯨女子隊介紹", "女子足球頁的簡介段；事實面板與前往官網按鈕不在此編輯。")));

        list.Add(T(Tcrfc, "partners/become-a-partner", "成為合作夥伴", "Become a Partner",
            FixedSteps("values", "六大合作價值", 6, "固定六項，每項有名稱與說明。"),
            Text("audienceNote", "受眾數據說明")));

        list.Add(T(Tcrfc, "charity/commitment", "慈善理念", "Our Commitment",
            Text("commitment", "慈善理念說明"),
            Text("focusIntro", "投入領域說明", "四大投入領域上方的一句說明。"),
            FixedSteps("focusAreas", "四大投入領域", 4, "固定四項：青少年扶助、偏鄉足球、弱勢家庭、公益義賽。")));

        // ── 藍鯨（10 頁；不設 06 女子足球、11 慈善）──
        list.Add(T(Bw, "about/our-story", "我們的故事", "Our Story",
            Text("story", "故事內文", "介紹球隊的定位與成立宗旨，段落之間空一行。")));

        list.Add(T(Bw, "about/vision-mission", "發展願景", "Vision",
            FixedSteps("visions", "發展願景", 5, "固定五項，每項有名稱與說明。")));

        list.Add(T(Bw, "about/philosophy", "俱樂部口號與培訓精神", "Club Slogan and Training Spirit",
            Quote("crest", "隊徽理念"),
            Quote("slogan", "俱樂部口號"),
            Quote("spirit", "培訓精神")));

        list.Add(T(Bw, "about/governance", "治理與管理", "Governance",
            Text("documentsNote", "公開文件說明", "公開文件區的說明文字；可下載檔案清單尚未開放維護。")));

        list.Add(T(Bw, "about/history", "俱樂部歷程", "Club History",
            EditableTimeline("years", "歷年沿革", "每一列是一個年度：日期填年份，說明填當年的大事（多件事以換行分開）。"),
            Text("note", "備註")));

        list.Add(T(Bw, "club/player-development", "球員發展重點", "Player Development",
            FixedSteps("modules", "八大發展面向", 8, "固定八項，每項有名稱與說明。"),
            Text("summary", "總結說明", "第一段為小標題，第二段為說明。")));

        list.Add(T(Bw, "club/opportunities", "球員機會", "Player Opportunities",
            Text("joinIntro", "加入說明", "「加入球隊」區的說明；試訓場次、常見問題與報名按鈕不在此編輯。")));

        list.Add(T(Bw, "club/international-pathways", "國際發展通道", "International Pathways",
            FixedSteps("pathway", "路徑總覽", 4, "固定四個階段。")));

        list.Add(T(Bw, "club/player-stories", "球員故事", "Player Stories",
            Text("note", "說明", "尚無球員故事時顯示的說明。")));

        list.Add(T(Bw, "partners/become-a-partner", "成為合作夥伴", "Become a Partner",
            FixedSteps("values", "六大合作價值", 6, "固定六項，每項有名稱與說明。"),
            Text("audienceNote", "受眾數據說明")));

        return list;
    }
}
