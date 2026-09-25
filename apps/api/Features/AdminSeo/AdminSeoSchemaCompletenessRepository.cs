using Dapper;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// GEO-05／主站規劃書 §4.8 H「結構化資料完整性檢查」：逐型別掃描本俱樂部（或俱樂部＋共同）
/// 資料，列出必填欄位缺漏的資料筆與缺哪些欄位。必填欄位定義**唯一來源**是
/// <see cref="SchemaRequiredFields"/>（docs/18-work-errors.md E-39），這裡只負責把各資料表的值
/// 組成 <see cref="SchemaRequiredFields.GetMissingFields"/> 需要的字典、呼叫它，再組裝成報表列。
///
/// 🔴 **只回傳有缺漏的列**（GEO-05「列出必填欄位缺漏的頁面與型別」），資料完整的列不出現在報表——
/// 這是報表而不是「全部資料的健檢清單」，比照既有 <see cref="AdminSeoReportRepository"/>
/// （孤立頁面偵測）同一種「只列有問題的」設計。
///
/// **範圍與已知簡化**（規劃書沒有列出逐型別掃描範圍，以下是本輪判斷，見
/// apps/api/README.md「S1-12c」段）：
/// - <c>Event</c>／<c>SportsTeam</c>／<c>Person</c>／<c>Course</c>／<c>BreadcrumbList</c>／
///   <c>FAQPage</c> 六型別**目前尚未接上任何前台輸出**（只有 <c>Article</c>／<c>SportsEvent</c>
///   已在 <c>apps/web</c> 真的輸出 JSON-LD，見 S1-12c 任務範圍）——這裡仍然掃描並回報缺漏，
///   讓 S1-12f 等後續任務把這些型別接上輸出時，資料現況已經看得到，不用等到那時候才發現缺口。
/// - <c>BreadcrumbList</c> 只檢查「這一頁本身有沒有可用的標題與網址」這個最小前提，不驗證完整的
///   頁面階層（B1 頁面尚未接上動態路由，見 <see cref="AdminSeoReportRepository"/> 檔頭同一個
///   已知落差）。
/// </summary>
public sealed class AdminSeoSchemaCompletenessRepository(IClubSqlConnectionFactory connectionFactory)
{
    private const string Locale = RequestLocale.DefaultDbLocale;

    private sealed record ClubRow(Guid Id, string Domain, string? LogoLightKey, string? OgImageKey, string? Name);
    private sealed record TeamRow(Guid Id, string? HeroKey, string? Name);
    private sealed record EventRow(Guid Id, DateTime StartsAt, string? VenueName, string? Title);
    private sealed record MatchRow(
        Guid Id, DateTime MatchOn, string? Kickoff, string? HomeAway, string? BaseOpponent,
        string? I18nOpponent, string? Venue, string? CompetitionName);
    private sealed record PlayerRow(Guid Id, string? Name);
    private sealed record ArticleRow(
        Guid Id, string Slug, string? Title, DateTime? PublishedAt,
        string? OgImageKey, string? CoverKey);
    private sealed record ProgramRow(Guid Id, string Slug, string? Name, string? Intro);
    private sealed record PageRow(Guid Id, string Slug, string? SeoTitle);
    private sealed record FaqRow(Guid Id, string Slug, string? Question, string? Answer);

    public async Task<SchemaCompletenessReportDto> GetReportAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var items = new List<SchemaCompletenessIssueDto>();

        // ── Organization（clubs，每個俱樂部恰一列）───────────────────────────────
        var club = await connection.QuerySingleOrDefaultAsync<ClubRow>(new CommandDefinition("""
            SELECT c.id AS Id, c.domain AS Domain, c.logo_light_key AS LogoLightKey,
                   c.og_image_key AS OgImageKey, ci.name AS Name
            FROM clubs c
            LEFT JOIN clubs_i18n ci ON ci.club_id = c.id AND ci.locale = @Locale
            WHERE c.id = @ClubId
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken));

        if (club is not null)
        {
            AddIfIncomplete(items, SchemaType.Organization, "club", club.Id, club.Name, path: null, new Dictionary<string, object?>
            {
                ["name"] = club.Name,
                ["url"] = club.Domain,
                ["logo"] = club.LogoLightKey,
            });
        }

        // ── SportsTeam（teams）。url 用俱樂部網域（Organization 那一層的事實，見類別檔頭），
        //    logo 回退俱樂部隊徽——沒有專屬隊徽美術時，用俱樂部隊徽代表這支球隊仍然正確。──────
        var teams = (await connection.QueryAsync<TeamRow>(new CommandDefinition("""
            SELECT t.id AS Id, t.hero_key AS HeroKey, ti.name AS Name
            FROM teams t
            LEFT JOIN teams_i18n ti ON ti.team_id = t.id AND ti.locale = @Locale
            WHERE t.club_id = @ClubId
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var team in teams)
        {
            AddIfIncomplete(items, SchemaType.SportsTeam, "team", team.Id, team.Name, path: null, new Dictionary<string, object?>
            {
                ["name"] = team.Name,
                ["url"] = club?.Domain,
                ["logo"] = team.HeroKey ?? club?.LogoLightKey,
            });
        }

        // ── Event（calendar_custom_events，只看公開活動——非公開活動本來就不會對外輸出）────
        var events = (await connection.QueryAsync<EventRow>(new CommandDefinition("""
            SELECT e.id AS Id, e.starts_at AS StartsAt, vi.name AS VenueName, ei.title AS Title
            FROM calendar_custom_events e
            LEFT JOIN venues v ON v.id = e.venue_id
            LEFT JOIN venues_i18n vi ON vi.venue_id = v.id AND vi.locale = @Locale
            LEFT JOIN calendar_custom_events_i18n ei ON ei.calendar_custom_event_id = e.id AND ei.locale = @Locale
            WHERE e.club_id = @ClubId AND e.is_public = 1
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var ev in events)
        {
            AddIfIncomplete(items, SchemaType.Event, "event", ev.Id, ev.Title, path: null, new Dictionary<string, object?>
            {
                ["name"] = ev.Title,
                ["startDate"] = ev.StartsAt,
                ["location"] = ev.VenueName,
            });
        }

        // ── SportsEvent（matches）。逐欄位定義與 Features/Schedule/MatchesRepository 的公開讀取
        //    一致（opponent 的英文回退鏈那組規則跟本報表無關，這裡只看「有沒有值」，不分語系挑值，
        //    zh-Hant 側表為空就退回基礎表 opponent，兩者只要有一個有值就算有值）。────────────────
        var matches = (await connection.QueryAsync<MatchRow>(new CommandDefinition("""
            SELECT m.id AS Id, m.match_on AS MatchOn, m.kickoff AS Kickoff, m.home_away AS HomeAway,
                   m.opponent AS BaseOpponent, mi.opponent AS I18nOpponent, mi.venue AS Venue,
                   ci.name AS CompetitionName
            FROM matches m
            LEFT JOIN matches_i18n mi ON mi.match_id = m.id AND mi.locale = @Locale
            LEFT JOIN competitions c ON c.id = m.competition_id
            LEFT JOIN competitions_i18n ci ON ci.competition_id = c.id AND ci.locale = @Locale
            WHERE m.club_id = @ClubId
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var match in matches)
        {
            var opponent = match.I18nOpponent ?? match.BaseOpponent;
            AddIfIncomplete(items, SchemaType.SportsEvent, "match", match.Id, opponent, path: null, new Dictionary<string, object?>
            {
                ["matchOn"] = match.MatchOn,
                ["kickoff"] = match.Kickoff,
                ["homeAway"] = match.HomeAway,
                ["opponent"] = opponent,
                ["venue"] = match.Venue,
                ["competitionName"] = match.CompetitionName,
            });
        }

        // ── Person（players）─────────────────────────────────────────────────
        var players = (await connection.QueryAsync<PlayerRow>(new CommandDefinition("""
            SELECT p.id AS Id, pi.name AS Name
            FROM players p
            LEFT JOIN players_i18n pi ON pi.player_id = p.id AND pi.locale = @Locale
            WHERE p.club_id = @ClubId
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var player in players)
        {
            AddIfIncomplete(items, SchemaType.Person, "player", player.Id, player.Name, path: null, new Dictionary<string, object?>
            {
                ["name"] = player.Name,
            });
        }

        // ── Article（已發布，含俱樂部＋共同）。image 優先序比照
        //    Features/News/ArticlesRepository.ResolveOgImageAsync：這篇專屬 OG 圖片 > 全站預設
        //    OG 圖片（clubs.og_image_key） > 封面圖片。這裡只需要知道「有沒有圖可用」，不需要把
        //    key 換成公開網址，所以不呼叫 IImagePublicUrlResolver，直接看三個鍵有沒有非空值即可；
        //    ⚠️ 這三層優先序若改動，兩處要一起改（同一個已知的小範圍重複，見
        //    ArticlesRepository.ResolveOgImageAsync 上的檔頭說明）。──────────────────────────
        var articles = (await connection.QueryAsync<ArticleRow>(new CommandDefinition($"""
            SELECT a.id AS Id, a.slug AS Slug, ai.title AS Title, a.published_at AS PublishedAt,
                   a.og_image_key AS OgImageKey, a.cover_key AS CoverKey
            FROM articles a
            LEFT JOIN articles_i18n ai ON ai.article_id = a.id AND ai.locale = @Locale
            WHERE {ClubOrSharedSql.WhereClubOrShared} AND a.status = 'published'
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var article in articles)
        {
            // 三層優先序：文章專屬 OG 圖片 > 全站預設 OG 圖片（clubs.og_image_key）> 封面圖片，
            // 逐字對應 ArticlesRepository.ResolveOgImageAsync 的既有邏輯（見上方類別檔頭說明）。
            var image = article.OgImageKey ?? club?.OgImageKey ?? article.CoverKey;

            AddIfIncomplete(items, SchemaType.Article, "article", article.Id, article.Title,
                path: $"/zh/news/{article.Slug}/", new Dictionary<string, object?>
                {
                    ["headline"] = article.Title,
                    ["datePublished"] = article.PublishedAt,
                    ["image"] = image,
                });
        }

        // ── Course（programs，已發布）。⚠️ 資料表叫 programs，不是 EF 實體類別名稱
        //    TrainingProgram（apps/api/Data/EfEntities/TrainingProgram.cs 對映的表名）。──────
        var programs = (await connection.QueryAsync<ProgramRow>(new CommandDefinition("""
            SELECT tp.id AS Id, tp.slug AS Slug, pi.name AS Name, pi.intro AS Intro
            FROM programs tp
            LEFT JOIN programs_i18n pi ON pi.program_id = tp.id AND pi.locale = @Locale
            WHERE tp.club_id = @ClubId AND tp.status = 'published'
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var program in programs)
        {
            AddIfIncomplete(items, SchemaType.Course, "program", program.Id, program.Name,
                path: $"/zh/programs/{program.Slug}/", new Dictionary<string, object?>
                {
                    ["name"] = program.Name,
                    ["description"] = program.Intro,
                });
        }

        // ── BreadcrumbList（pages＋articles，已發布）。pages 沒有頁面層級的「標題」欄位，
        //    只能用 SeoTitle 當可用的最小前提——見類別檔頭「已知簡化」。articles 用既有 Title。──
        var pages = (await connection.QueryAsync<PageRow>(new CommandDefinition("""
            SELECT p.id AS Id, p.slug AS Slug, pi.seo_title AS SeoTitle
            FROM pages p
            LEFT JOIN pages_i18n pi ON pi.page_id = p.id AND pi.locale = @Locale
            WHERE p.club_id = @ClubId AND p.status = 'published'
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var page in pages)
        {
            var path = $"/zh/{page.Slug}/";
            AddIfIncomplete(items, SchemaType.BreadcrumbList, "page", page.Id, page.SeoTitle, path, new Dictionary<string, object?>
            {
                ["name"] = page.SeoTitle,
                ["path"] = path,
            });
        }

        foreach (var article in articles)
        {
            var path = $"/zh/news/{article.Slug}/";
            AddIfIncomplete(items, SchemaType.BreadcrumbList, "article", article.Id, article.Title, path, new Dictionary<string, object?>
            {
                ["name"] = article.Title,
                ["path"] = path,
            });
        }

        // ── FAQPage（faqs，已發布，含俱樂部＋共同）──────────────────────────────
        var faqs = (await connection.QueryAsync<FaqRow>(new CommandDefinition($"""
            SELECT f.id AS Id, f.slug AS Slug, fi.question AS Question, fi.answer AS Answer
            FROM faqs f
            LEFT JOIN faqs_i18n fi ON fi.faq_id = f.id AND fi.locale = @Locale
            WHERE {ClubOrSharedSql.WhereClubOrShared} AND f.status = 'published'
            """, new { scope.ClubId, Locale }, cancellationToken: cancellationToken))).AsList();

        foreach (var faq in faqs)
        {
            AddIfIncomplete(items, SchemaType.FaqPage, "faq", faq.Id, faq.Question,
                path: $"/zh/faq/#q-{faq.Slug}", new Dictionary<string, object?>
                {
                    ["question"] = faq.Question,
                    ["answer"] = faq.Answer,
                });
        }

        return new SchemaCompletenessReportDto { Items = items };
    }

    private static void AddIfIncomplete(
        List<SchemaCompletenessIssueDto> items, SchemaType type, string entityType, Guid id, string? label,
        string? path, IReadOnlyDictionary<string, object?> values)
    {
        var missing = SchemaRequiredFields.GetMissingFields(type, values);
        if (missing.Count == 0)
        {
            return;
        }

        items.Add(new SchemaCompletenessIssueDto
        {
            SchemaTypeName = SchemaTypeCodes.ToCode(type),
            EntityType = entityType,
            Id = id,
            Label = label,
            Path = path,
            MissingFields = missing.Select(f => new SchemaCompletenessFieldDto { LabelZh = f.LabelZh, LabelEn = f.LabelEn }).ToList(),
        });
    }
}
