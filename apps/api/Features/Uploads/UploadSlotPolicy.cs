namespace Tcrfc.Api.Features.Uploads;

/// <summary>
/// 圖片上傳允許寫入的「欄位插槽」允許清單，形狀比照 <c>Features/AdminNews/SlugPolicy.cs</c>：
/// 一個獨立、集中、有清楚維護說明的檔案，而不是散在各處各自檢查。
///
/// 🔴🔴🔴 **S0-8 修正（2026-09-22）**：這個檔案原本是給一個獨立的「先上傳拿 key」HTTP 端點
/// （<c>Features/Uploads/UploadsEndpoints.cs</c>）用的允許清單，但那個端點的存在本身就是
/// 「選檔即上傳」的兩段式設計，違反規劃書 §4.0／第 53 行「選檔不上傳、儲存才上傳」——已經
/// 整支移除（不再是 <c>Program.cs</c> 掛路由的對象）。這份允許清單留下來，改成由**每個模組自己
/// 的建立／更新端點**在同一次 multipart 請求裡直接呼叫（見
/// <c>Features/AdminNews/AdminArticlesEndpoints.cs</c> 的示範），純粹當一份「這個 entityType／
/// field 真的有對應資料庫欄位」的集中檢查，不再對外開一個可以單獨打的路由。
///
/// 🔴🔴🔴 這份清單目前只有一格（<c>articles.cover</c>），刻意的：S0-8 的任務邊界是「把共用元件
/// 做好＋用一個真的有畫面可驗的模組示範接線」，不是把全部圖片欄位一次接完（那是各模組寫入端點
/// 自己的工作，等 B1／B6／K1／S1……等模組真的動工時，各自把自己的 <c>entityType</c>／
/// <c>field</c> 加進這裡，並比照 <c>AdminArticlesEndpoints</c> 把封面圖片／照片欄位併進自己的
/// 建立／更新端點，不要另外開一個獨立的上傳端點）。新增一格前先去
/// <c>docs/12b-database-tables.md</c> 確認資料表真的有對應的 <c>_key</c> 欄位
/// （docs/14-invariants.md「圖片一律欄位直傳」），不要假設。
/// </summary>
public static class UploadSlotPolicy
{
    private static readonly Dictionary<string, HashSet<string>> AllowedSlots =
        new(StringComparer.Ordinal)
        {
            // articles.cover_key（見 db/club-schema.sql 第 280 行）；PUT /admin/{club}/news/{id}
            // 的 CoverKey 欄位是目前唯一真的會把上傳結果寫回資料庫的地方。
            // S1-12 新增 "og" 插槽：articles.og_image_key（單頁 OG 圖片覆寫，驗收退回後補做）。
            ["articles"] = new HashSet<string>(StringComparer.Ordinal) { "cover", "og" },
            // S1-7 新增：C1–C3（teams.hero_key／players.photo_key／staff.photo_key，
            // 見 db/club-schema.sql 對應建表陳述式），比照上面 articles.cover 的接法。
            ["teams"] = new HashSet<string>(StringComparer.Ordinal) { "hero" },
            ["players"] = new HashSet<string>(StringComparer.Ordinal) { "photo" },
            ["staff"] = new HashSet<string>(StringComparer.Ordinal) { "photo" },
            // S1-6 新增：B3 首頁編排——banners.image_key（db/club-schema.sql「首頁 Hero 輪播」建表
            // 陳述式），比照上面 articles.cover 的接法。✅ S1-7a 已補齊 image_width／image_height
            // （由上傳結果自動填入，不經這份插槽清單）與 banners_i18n.image_alt（雙語，隨 payload
            // 一起送，不是檔案上傳）——這份清單本身只管「檔案上傳」這一個插槽，寬高與 alt 走
            // AdminBannersRepository 的 CreateAsync／UpdateAsync 參數與 AddOrReplaceI18n。
            // 🔴 v3.14 新增 "video" 插槽：banners.video_key，media_type="video" 時必填的第二個
            // 檔案欄位（"image" 插槽此時作為影片的海報格 poster，兩者同一次請求一起送，見
            // AdminBannersEndpoints 的 UploadVideoAsync）。
            ["banners"] = new HashSet<string>(StringComparer.Ordinal) { "image", "video" },
            // S1-9 新增：P1 課程／營隊項目——programs.cover_key（db/club-schema.sql「4.3 P 課程與活動」
            // 建表陳述式），比照上面 staff.photo 的接法。
            ["programs"] = new HashSet<string>(StringComparer.Ordinal) { "cover" },
            // S1-11 新增：L2 自建事件——calendar_custom_events.cover_key（db/club-schema.sql
            // 「4.10 L 行事曆管理」建表陳述式），比照上面 programs.cover 的接法。
            ["calendar_custom_events"] = new HashSet<string>(StringComparer.Ordinal) { "cover" },
            // S1-12 新增：B1 頁面——pages.og_image_key（單頁 OG 圖片覆寫，驗收退回後補做）。
            ["pages"] = new HashSet<string>(StringComparer.Ordinal) { "og" },
            // S1-12 新增：H 全站 SEO 預設——clubs.og_image_key 早已存在（J4 品牌欄位，
            // AdminClubDetailDto 原本刻意唯讀，見該檔案上的說明），這裡補上寫入路徑，但改由
            // Features/AdminSeo（不是 Features/AdminClubs）呼叫，理由是「全站預設 OG 圖」
            // 屬於 H 模組的編輯情境（跟標題模板、預設描述同一個表單），不是 J4 品牌／法人資料的
            // 編輯情境；標誌、favicon 欄位已於 v3.20 移除（由前台靜態資產定義），俱樂部只剩 ogImage 一格。
            ["clubs"] = new HashSet<string>(StringComparer.Ordinal) { "ogImage" },
            // E1a 新增（2026-09-30）：E1 夥伴、E2 贊助、B5 慈善、B6 媒體專區、C5 里程碑的圖片欄位。
            // 每一格都對應 db/club-schema.sql 真實存在的 *_key 欄位（或子表 image_key）。
            ["partners"] = new HashSet<string>(StringComparer.Ordinal) { "logoDark", "logoLight" },
            ["sponsors"] = new HashSet<string>(StringComparer.Ordinal) { "logoDark", "logoLight" },
            ["sponsor_activation_images"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["charities"] = new HashSet<string>(StringComparer.Ordinal) { "logo" },
            ["charity_programs"] = new HashSet<string>(StringComparer.Ordinal) { "cover" },
            ["charity_program_images"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["impact_records"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["impact_record_images"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["press_resources"] = new HashSet<string>(StringComparer.Ordinal) { "cover", "image" },
            ["milestones"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            // B1 新增（2026-09-30）：K4 特約店家照片／Logo（partner_stores.image_key）。
            ["partner_stores"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            // D 批新增（2026-09-30）：E4 版位備援素材、E5 廣告素材、M3 推播圖片。
            ["ad_slots"] = new HashSet<string>(StringComparer.Ordinal) { "fallbackImage" },
            ["ad_creatives"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["push_messages"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            // C1 新增（2026-09-30）：F1 漫畫、F2 球迷會活動、S1 商品圖集的圖片欄位。
            ["comic_characters"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["comic_episodes"] = new HashSet<string>(StringComparer.Ordinal) { "cover" },
            ["comic_pages"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["fan_events"] = new HashSet<string>(StringComparer.Ordinal) { "cover" },
            ["fan_event_images"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["product_images"] = new HashSet<string>(StringComparer.Ordinal) { "image" },
            ["member_draws"] = new HashSet<string>(StringComparer.Ordinal) { "cover" },
            // I 網站設定（2026-10-02）：I5 場地照片（venues.photo_key）。
            ["venues"] = new HashSet<string>(StringComparer.Ordinal) { "photo" },
        };

    public static void Validate(string entityType, string field)
    {
        if (!AllowedSlots.TryGetValue(entityType, out var fields) || !fields.Contains(field))
        {
            throw new UploadSlotNotAllowedException(entityType, field);
        }
    }
}

/// <summary>不在允許清單內的 <c>entityType</c>／<c>field</c> 組合。對應 400——這不是「這筆資料不存在」
/// （404），是「這個圖片欄位插槽根本沒有被定義」，兩者語意不同，故意分開。</summary>
public sealed class UploadSlotNotAllowedException(string entityType, string field)
    : Exception($"不支援的圖片欄位「{entityType}.{field}」。");
