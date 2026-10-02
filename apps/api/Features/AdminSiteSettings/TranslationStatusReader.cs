using Dapper;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>翻譯狀態總覽涵蓋的一種內容。<see cref="ViewPermission"/> 是後台檢視該內容所需的權限碼——儀表板的「未翻譯內容數」只算呼叫者看得到的類別。</summary>
public sealed record TranslationEntityDefinition(
    string Type, string LabelZh, string Table, string I18nTable, string ForeignKey, string TextColumn, bool OwnOrShared, string ViewPermission);

/// <summary>
/// 規劃書 §4.9 多語系管理「翻譯狀態總覽：以矩陣列出每筆內容的 zh / en 完成狀態，可篩選缺英文」與 §4.1 儀表板「未翻譯內容數（英／日分列）」共用的讀取。
///
/// ### 什麼算「完成」
/// 該語系側表列存在，且<b>主要文字欄位</b>（新聞標題、FAQ 問題、課程／球員／教練／計畫／夥伴／贊助商名稱、首頁輪播標題）非空白。
/// 只看主要文字欄位是刻意的簡化：側表有十幾個欄位（摘要、內文、SEO…），逐欄比對會讓「完成」變成永遠達不到的標準；
/// 主要欄位有值＝這筆內容在該語系至少能被辨認、能出現在列表上。
///
/// ### 涵蓋範圍（九類）
/// 新聞、FAQ、課程、球員、教練、慈善計畫、夥伴、贊助商、首頁輪播。<b>不含</b>頁面（區塊內文是雙語 JSON，沒有單一標題欄位）、
/// 賽事、商品、漫畫、行事曆事件等——規劃書寫「每筆內容」，這裡只收有單一主要文字欄位、且前台列表直接顯示的類別，其餘列為待決（見 README）。
///
/// SQL 全部由本檔的常數目錄組字串（資料表與欄位名稱不來自使用者輸入），語系與俱樂部以參數傳入。
/// </summary>
public sealed class TranslationStatusReader(IClubSqlConnectionFactory connectionFactory)
{
    public static readonly IReadOnlyList<TranslationEntityDefinition> Catalog =
    [
        new("article", "新聞", "articles", "articles_i18n", "article_id", "title", OwnOrShared: true, "content.article.view"),
        new("faq", "常見問題", "faqs", "faqs_i18n", "faq_id", "question", OwnOrShared: true, "content.faq.view"),
        new("program", "課程", "programs", "programs_i18n", "program_id", "name", OwnOrShared: false, "program.item.view"),
        new("player", "球員", "players", "players_i18n", "player_id", "name", OwnOrShared: false, "team.player.view"),
        new("staff", "教練", "staff", "staff_i18n", "staff_id", "name", OwnOrShared: true, "team.staff.view"),
        new("charity_program", "慈善計畫", "charity_programs", "charity_programs_i18n", "charity_program_id", "name", OwnOrShared: true, "charity.content.view"),
        new("partner", "合作夥伴", "partners", "partners_i18n", "partner_id", "name", OwnOrShared: false, "business.partner.view"),
        new("sponsor", "贊助商", "sponsors", "sponsors_i18n", "sponsor_id", "name", OwnOrShared: false, "business.sponsor.view"),
        new("banner", "首頁輪播", "banners", "banners_i18n", "banner_id", "title", OwnOrShared: false, "content.banner.view"),
    ];

    public sealed record Row(string Type, Guid Id, string Label, bool IsShared, IReadOnlySet<string> DoneLocales);

    private sealed record RawRow(Guid Id, int IsShared, string? Label, string? DoneLocales);

    /// <summary>讀出指定類別的所有內容與各語系完成狀態。<paramref name="locales"/> 是要檢查的語系（含預設語系）。</summary>
    public async Task<IReadOnlyList<Row>> ReadAsync(
        Guid clubId, IEnumerable<TranslationEntityDefinition> types, IReadOnlyList<string> locales, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = new List<Row>();
        foreach (var t in types)
        {
            var scope = t.OwnOrShared ? "(t.club_id = @ClubId OR t.club_id IS NULL)" : "t.club_id = @ClubId";
            var shared = t.OwnOrShared ? "CASE WHEN t.club_id IS NULL THEN 1 ELSE 0 END" : "0";
            var sql = $"""
                SELECT t.id AS Id, {shared} AS IsShared, zh.{t.TextColumn} AS Label,
                       (SELECT STRING_AGG(i.locale, ',') FROM {t.I18nTable} i
                         WHERE i.{t.ForeignKey} = t.id AND i.locale IN @Locales
                           AND LEN(LTRIM(RTRIM(COALESCE(i.{t.TextColumn}, N'')))) > 0) AS DoneLocales
                FROM {t.Table} t
                LEFT JOIN {t.I18nTable} zh ON zh.{t.ForeignKey} = t.id AND zh.locale = @DefaultLocale
                WHERE {scope}
                ORDER BY t.row_seq
                """;
            var rows = await connection.QueryAsync<RawRow>(new CommandDefinition(
                sql, new { ClubId = clubId, Locales = locales, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: cancellationToken));
            result.AddRange(rows.Select(r => new Row(
                t.Type, r.Id, string.IsNullOrWhiteSpace(r.Label) ? "（未命名）" : r.Label.Trim(), r.IsShared == 1,
                (r.DoneLocales ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal))));
        }

        return result;
    }
}
