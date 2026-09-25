namespace Tcrfc.Api.Features.Seo;

/// <summary>
/// GEO-05／主站規劃書 §4.8 H「結構化資料完整性檢查」涵蓋的九種 Schema.org 型別
/// （§4.8／§7 SEO 九項基礎「結構化資料 Schema Markup」逐一列出：
/// Organization、SportsTeam、Event、SportsEvent（行事曆）、Person、Article、Course、
/// BreadcrumbList、FAQPage）。<see cref="SchemaTypeCodes.ToCode"/> 對應到 schema.org 本身的
/// <c>@type</c> 字面值，供 DTO／JSON-LD 直接使用，不另外透過 <see cref="System.Text.Json"/> 的
/// enum 轉換器（本專案既有慣例是值域一律用字串，例如 <c>OrphanPageDto.EntityType</c>，這裡沿用
/// 同一種寫法，不引入 <c>JsonStringEnumConverter</c>）。
/// </summary>
public enum SchemaType
{
    Organization,
    SportsTeam,
    Event,
    SportsEvent,
    Person,
    Article,
    Course,
    BreadcrumbList,
    FaqPage,
}

/// <summary>schema.org <c>@type</c> 字面值的單一來源，避免各處各自拼一次字串（尤其
/// <c>FaqPage</c> 的正確大小寫是 <c>FAQPage</c>，容易手拼打錯）。</summary>
public static class SchemaTypeCodes
{
    public static string ToCode(SchemaType type) => type switch
    {
        SchemaType.Organization => "Organization",
        SchemaType.SportsTeam => "SportsTeam",
        SchemaType.Event => "Event",
        SchemaType.SportsEvent => "SportsEvent",
        SchemaType.Person => "Person",
        SchemaType.Article => "Article",
        SchemaType.Course => "Course",
        SchemaType.BreadcrumbList => "BreadcrumbList",
        SchemaType.FaqPage => "FAQPage",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}

/// <summary>一個必填欄位的描述（給後台報表顯示用的中英文標籤，介面不顯示欄位英文代碼本身，
/// 比照 CLAUDE.md 全域規定第 9 條「介面一律日常中文」）。<see cref="Key"/> 只在程式內部當字典鍵，
/// 不會被序列化到後台畫面。</summary>
public sealed record SchemaFieldDefinition(string Key, string LabelZh, string LabelEn);

/// <summary>
/// GEO-05「結構化資料完整輸出……必填欄位不得留空；資料不足時不輸出該型別」的必填欄位**單一來源**
/// （docs/18-work-errors.md E-39：後台報表與前台輸出判斷都讀這裡，不得各自寫一份必填欄位清單）。
///
/// 🔴 **跨語言的「單一來源」怎麼做到**：<c>apps/api</c>（C#）與 <c>apps/web</c>（TypeScript／Nuxt）
/// 是兩個獨立執行環境，沒有辦法真的共用同一份程式碼檔案。本檔的解法是「必填欄位清單只在這裡宣告
/// 一次，前台不重新宣告一次判斷條件」：後台報表（<c>AdminSeoSchemaCompletenessRepository</c>）
/// 直接呼叫這裡的 <see cref="GetMissingFields"/>；已經有動態內容可用的公開端點
/// （<c>ArticlesRepository.GetBySlugAsync</c>／<c>MatchesRepository.ListAsync</c>）用同一個方法
/// 算出 <c>SchemaEligible</c> 布林值放進 DTO，<c>apps/web</c> 只讀這個布林值決定輸不輸出 JSON-LD，
/// 不再自己判斷「哪些欄位算必填」。這是本次任務唯一可行的「單一來源」設計（規劃書沒有規定要怎麼
/// 做到跨語言單一來源，這是 backend-engineer 依 E-39 的精神做的判斷，見 apps/api/README.md
/// 「S1-12c」段的完整說明）。
///
/// 🔴 **必填欄位怎麼訂出來的**（規劃書只列型別清單，沒有列逐欄位規格，以下是判斷依據）：
/// 以 schema.org 官方詞彙表的 <c>required</c> 屬性與 Google 搜尋中心結構化資料指南的
/// 「必要屬性」為主，並對照本專案資料表**目前實際存在**的欄位——刻意不把「建議」屬性
/// （如 <c>SportsTeam.foundingDate</c>）訂為必填，那類欄位本專案根本沒有對應資料表欄位
/// （docs/12d-field-audit.md 已記錄 <c>clubs</c> 沒有 <c>founded_on</c>），訂為必填只會讓兩個
/// 型別永遠輸出不了，對 GEO 沒有幫助；那類事實單一來源的欄位缺口屬於 GEO-03／GEO-04
/// （S1-12d）的範圍，不在本次 GEO-05 任務新增資料欄位。逐型別理由：
/// - <b>Organization</b>／<b>SportsTeam</b>：schema.org 只要求 <c>name</c>；本專案另外要求
///   <c>url</c>（俱樂部網域，<c>Club.Domain</c>，Google 建議 Organization 一律帶 <c>url</c>）與
///   <c>logo</c>（<c>Club.LogoLightKey</c>／<c>Team.HeroKey</c>），三者皆為本專案既有欄位。
/// - <b>Event</b>：對應 <c>calendar_custom_events</c>（一般活動）。Google Event 結構化資料必要屬性
///   為 <c>name</c>／<c>startDate</c>／<c>location</c>（含地點名稱），三者皆為既有欄位。
/// - <b>SportsEvent</b>：對應 <c>matches</c>。沿用 <c>app/pages/zh/schedule.vue</c>
///   （S0-9j）既有已實作的六欄位判斷（<c>matchOn</c>／<c>kickoff</c>／<c>homeAway</c>／
///   <c>opponent</c>／<c>venue</c>／<c>competitionName</c>），本次只是把它從該頁面內的行內判斷
///   收斂進這個單一來源，判斷條件本身不變。
/// - <b>Person</b>：對應 <c>players</c>。schema.org 只要求 <c>name</c>（<c>players_i18n.name</c>）。
///   ⚠️ 肖像同意（<c>portrait_consent_status</c>）不影響 Person 型別**輸不輸出**，只影響
///   <c>image</c> 屬性能不能帶（既有規則見 <c>PlayersRepository</c>），不是 GEO-05 的必填欄位。
/// - <b>Article</b>：對應 <c>articles</c>。Google Article 結構化資料必要屬性為 <c>headline</c>
///   （<c>title</c>）／<c>image</c>／<c>datePublished</c>；<c>image</c> 直接使用
///   <c>ArticleDetailDto.OgImageUrl</c>（已經是後端算好「這篇專屬 &gt; 全站預設 &gt; 封面圖」優先序
///   的完整網址，不在這裡重新判斷一次圖片來源，避免又是另一份「有沒有圖片」的重複邏輯）。
///   <c>author</c>／<c>publisher</c> 固定為俱樂部本身（永遠有值），不列為資料庫必填欄位。
/// - <b>Course</b>：對應 <c>training_programs</c>。Google Course 結構化資料必要屬性為
///   <c>name</c>／<c>description</c>／<c>provider.name</c>；<c>provider.name</c> 固定為俱樂部本身
///   （永遠有值），不列為資料庫必填欄位，只列 <c>name</c>／<c>description</c>
///   （<c>programs_i18n.name</c>／<c>intro</c>）。
/// - <b>BreadcrumbList</b>：對應 <c>pages</c>／<c>articles</c>。Google 要求每個節點要有
///   <c>name</c> 與 <c>item</c>（網址）；本專案目前沒有頁面階層資料表（B1 頁面尚未接上動態路由，
///   見 <c>AdminSeoReportRepository</c> 檔頭同一個已知落差），這裡只能檢查「這一頁本身有沒有
///   可用的標題與網址」這個最小前提（<c>SeoTitle</c>／<c>Slug</c>），不是完整的階層驗證。
/// - <b>FAQPage</b>：對應 <c>faqs</c>。schema.org 要求每個 <c>Question</c> 要有 <c>name</c>
///   （問題文字）與 <c>acceptedAnswer.text</c>（答案文字），對應 <c>faqs_i18n.question</c>／
///   <c>answer</c>。
/// </summary>
public static class SchemaRequiredFields
{
    public static readonly IReadOnlyDictionary<SchemaType, IReadOnlyList<SchemaFieldDefinition>> ByType =
        new Dictionary<SchemaType, IReadOnlyList<SchemaFieldDefinition>>
        {
            [SchemaType.Organization] =
            [
                new SchemaFieldDefinition("name", "俱樂部名稱", "Name"),
                new SchemaFieldDefinition("url", "官網網址", "URL"),
                new SchemaFieldDefinition("logo", "隊徽圖片", "Logo"),
            ],
            [SchemaType.SportsTeam] =
            [
                new SchemaFieldDefinition("name", "球隊名稱", "Team Name"),
                new SchemaFieldDefinition("url", "官網網址", "URL"),
                new SchemaFieldDefinition("logo", "隊徽圖片", "Logo"),
            ],
            [SchemaType.Event] =
            [
                new SchemaFieldDefinition("name", "活動名稱", "Event Name"),
                new SchemaFieldDefinition("startDate", "開始時間", "Start Date"),
                new SchemaFieldDefinition("location", "地點名稱", "Location"),
            ],
            [SchemaType.SportsEvent] =
            [
                new SchemaFieldDefinition("matchOn", "比賽日期", "Match Date"),
                new SchemaFieldDefinition("kickoff", "開球時間", "Kickoff Time"),
                new SchemaFieldDefinition("homeAway", "主客場", "Home/Away"),
                new SchemaFieldDefinition("opponent", "對手球隊", "Opponent"),
                new SchemaFieldDefinition("venue", "場地", "Venue"),
                new SchemaFieldDefinition("competitionName", "賽事名稱", "Competition Name"),
            ],
            [SchemaType.Person] =
            [
                new SchemaFieldDefinition("name", "姓名", "Name"),
            ],
            [SchemaType.Article] =
            [
                new SchemaFieldDefinition("headline", "標題", "Headline"),
                new SchemaFieldDefinition("datePublished", "發布時間", "Date Published"),
                new SchemaFieldDefinition("image", "文章圖片", "Image"),
            ],
            [SchemaType.Course] =
            [
                new SchemaFieldDefinition("name", "課程名稱", "Course Name"),
                new SchemaFieldDefinition("description", "課程說明", "Description"),
            ],
            [SchemaType.BreadcrumbList] =
            [
                new SchemaFieldDefinition("name", "頁面標題", "Page Title"),
                new SchemaFieldDefinition("path", "頁面網址", "Path"),
            ],
            [SchemaType.FaqPage] =
            [
                new SchemaFieldDefinition("question", "問題", "Question"),
                new SchemaFieldDefinition("answer", "答案", "Answer"),
            ],
        };

    /// <summary>
    /// 逐欄位檢查是否缺漏。<paramref name="values"/> 的 key 必須對應 <see cref="ByType"/> 該型別
    /// 逐一 <see cref="SchemaFieldDefinition.Key"/>；**呼叫端漏傳某個必填欄位鍵，一律視為缺漏**
    /// （防呆：不會因為忘記塞值就被誤判為「有值」而放行輸出殘缺 Schema）。
    /// 字串以 <see cref="string.IsNullOrWhiteSpace"/> 判斷空值，其餘型別（<see cref="DateTime"/>／
    /// <see cref="DateOnly"/> 等實質型別、非 <c>Nullable</c>）一律視為有值，只有 <c>null</c> 算缺漏。
    /// </summary>
    public static IReadOnlyList<SchemaFieldDefinition> GetMissingFields(SchemaType type, IReadOnlyDictionary<string, object?> values)
    {
        var missing = new List<SchemaFieldDefinition>();
        foreach (var field in ByType[type])
        {
            if (!values.TryGetValue(field.Key, out var value) || IsEmpty(value))
            {
                missing.Add(field);
            }
        }
        return missing;
    }

    /// <summary>資料是否足以輸出該型別的 Schema（GEO-05：「資料不足時不輸出該型別」）。</summary>
    public static bool IsComplete(SchemaType type, IReadOnlyDictionary<string, object?> values) =>
        GetMissingFields(type, values).Count == 0;

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        _ => false,
    };
}
