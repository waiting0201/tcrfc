using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Search;

/// <summary>
/// G-02 全站搜尋（主站規劃書 §3.0：跨新聞、球員、教練、課程、FAQ、慈善事蹟；關鍵字高亮與分類篩選）。
///
/// ### 搜尋引擎取捨：LIKE（<c>Contains</c>），不用 SQL Server 全文檢索
/// 全站內容量級是「一個足球俱樂部的官網」（新聞數百篇、球員教練數十人、FAQ 數十題），單次搜尋每類最多掃數百列，
/// <c>LIKE '%關鍵字%'</c> 毫秒級即可。全文檢索需要額外的全文目錄與索引（獨立的 DDL、migration 不能放在交易內、Azure SQL Basic 層容量與重建成本），
/// 而且繁體中文的斷詞（word breaker）品質本身就需要另外驗證；現階段得不償失。<b>改用全文檢索的觸發條件</b>：任一搜尋類別資料量超過約 1 萬列，
/// 或實測 p95 超過 300ms。換法：只要換掉各類別 <c>Where</c> 裡的 <c>Contains</c>，端點與回應形狀不變。不引入外部搜尋服務（規劃書與 docs/17 都沒有）。
/// EF 對參數化 <c>Contains</c> 翻成 <c>CHARINDEX</c>，沒有萬用字元問題，使用者輸入的 %、_、[ 只是普通字元。
///
/// ### 範圍與可見性（與各類公開端點一致，搜尋不得成為繞過）
/// 新聞：已發布且到達發布時間、本俱樂部或共同內容；FAQ：已發布、本俱樂部或共同；課程：已發布、僅本俱樂部；
/// 球員：僅本俱樂部（照片遵守肖像同意 fail-closed）；教練（含團隊成員，同公開名單）：本俱樂部或共同；
/// 慈善：已發布的慈善計畫＋事蹟紀錄，本俱樂部或共同。<b>只比對名稱／標題／摘要／簡介／職稱這類公開文字欄位</b>，
/// 不比對任何個資欄位（球員的出生日期、身高體重、教練證照都不在搜尋範圍），新聞與課程的區塊內文（json）也不比對。
///
/// ### 語系
/// 同時比對「要求語系」與「繁中」兩份文字（英文版使用者用繁中關鍵字也能找到尚未翻譯的內容）；顯示文字取要求語系、空白回退繁中並標示
/// <see cref="SearchResultItemDto.IsFallbackLocale"/>。
///
/// ### 排序與分頁
/// 每類先取最近／預設排序的前 <see cref="SearchResponseDto.Limits.PerTypeLimit"/> 筆，在記憶體合併後依「標題含全部關鍵字（2 分）＞ 只有內文命中（1 分）」
/// 排序，同分依 <see cref="SearchTypes.All"/> 的類別順序、再依各類原順序；然後在記憶體分頁。
/// 命中數（facets）每類單獨 COUNT，精確且不受每類上限影響。
/// </summary>
public sealed partial class SearchRepository(ClubDbContext db, IImagePublicUrlResolver imageUrls)
{
    public const int MaxTokens = 5;
    public const int MaxTokenLength = 50;
    public const int MaxQueryLength = 100;
    private const int PerTypeLimit = SearchResponseDto.Limits.PerTypeLimit;
    private const int SnippetLength = 120;

    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> TypeLabels = new Dictionary<string, (string, string)>
    {
        [SearchTypes.News] = ("新聞", "News"),
        [SearchTypes.Faq] = ("常見問題", "FAQ"),
        [SearchTypes.Program] = ("課程", "Programs"),
        [SearchTypes.Player] = ("球員", "Players"),
        [SearchTypes.Coach] = ("教練", "Coaches"),
        [SearchTypes.Charity] = ("慈善", "Charity"),
    };

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex MarkupTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>一筆暫存命中：<see cref="Title"/> 與 <see cref="Text"/> 是已依語系挑選後的顯示文字，<see cref="Rank"/> 是該類別內的原順序。</summary>
    private sealed record Hit(
        string Type, string? SubType, Guid Id, string? Slug, string? Title, string? Text, DateTime? Date, string? CategoryCode,
        string? TeamCode, string? ImageKey, bool IsFallback, int Rank);

    /// <summary>把查詢字串正規化並拆成關鍵字；不合法回 <see cref="PublicValidationException"/>（400）。</summary>
    public static (string Query, IReadOnlyList<string> Tokens) ParseQuery(string? raw)
    {
        var normalized = SearchKeywordNormalizer.Normalize(raw);
        normalized = Whitespace().Replace(normalized, " ");
        if (normalized.Length == 0)
        {
            throw new PublicValidationException("請輸入要搜尋的關鍵字。");
        }

        if (normalized.Length > MaxQueryLength)
        {
            throw new PublicValidationException($"搜尋關鍵字不可超過 {MaxQueryLength} 個字。");
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Length > MaxTokenLength ? t[..MaxTokenLength] : t)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTokens)
            .ToList();

        // 單一個拉丁字母／數字會命中幾乎所有內容，沒有搜尋意義也最耗資源；中日韓文字單字有意義，放行。
        var hasCjk = normalized.Any(c => c >= '㐀');
        if (!hasCjk && normalized.Replace(" ", "").Length < 2)
        {
            throw new PublicValidationException("請至少輸入 2 個字再搜尋。");
        }

        return (string.Join(' ', tokens), tokens);
    }

    public async Task<SearchResponseDto> SearchAsync(
        ClubScope scope, string? rawQuery, string? type, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (query, tokens) = ParseQuery(rawQuery);
        if (type is not null && !SearchTypes.All.Contains(type, StringComparer.Ordinal))
        {
            throw new PublicValidationException("搜尋分類不正確。");
        }

        var ctx = new Ctx(scope.ClubId, dbLocale, RequestLocale.DefaultDbLocale, tokens);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var hits = new List<Hit>();

        // 各類別依序查（共用同一個 DbContext，EF 不允許同一個內容並行查詢）；每類 1 次 COUNT ＋ 命中該分類時 1 次取前 N 筆。
        foreach (var t in SearchTypes.All)
        {
            var wanted = type is null || type == t;
            var (count, typeHits) = t switch
            {
                SearchTypes.News => await SearchNewsAsync(ctx, wanted, cancellationToken),
                SearchTypes.Faq => await SearchFaqsAsync(ctx, wanted, cancellationToken),
                SearchTypes.Program => await SearchProgramsAsync(ctx, wanted, cancellationToken),
                SearchTypes.Player => await SearchPlayersAsync(ctx, wanted, cancellationToken),
                SearchTypes.Coach => await SearchStaffAsync(ctx, wanted, cancellationToken),
                _ => await SearchCharityAsync(ctx, wanted, cancellationToken),
            };
            counts[t] = count;
            hits.AddRange(typeHits);
        }

        var typeOrder = SearchTypes.All.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i, StringComparer.Ordinal);
        var ranked = hits
            .Select(h => (Hit: h, Score: Score(h, tokens)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => typeOrder[x.Hit.Type])
            .ThenBy(x => x.Hit.Rank)
            .Select(x => x.Hit)
            .ToList();

        var pageItems = ranked.Skip((page - 1) * pageSize).Take(pageSize).Select(h => ToDto(h, tokens)).ToList();
        var english = dbLocale != RequestLocale.DefaultDbLocale;
        return new SearchResponseDto
        {
            Query = query,
            Tokens = tokens,
            Items = pageItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = ranked.Count,
            Facets = SearchTypes.All.Select(t => new SearchFacetDto
            {
                Type = t, Label = english ? TypeLabels[t].En : TypeLabels[t].Zh, Count = counts[t],
            }).ToList(),
            Truncated = counts.Values.Any(c => c > PerTypeLimit),
            IsEmpty = counts.Values.Sum() == 0,
        };
    }

    private sealed record Ctx(Guid ClubId, string Locale, string DefaultLocale, IReadOnlyList<string> Tokens);

    // ── 新聞 ─────────────────────────────────────────────────────────────────────
    private async Task<(int, List<Hit>)> SearchNewsAsync(Ctx c, bool wanted, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = db.Articles.AsNoTracking()
            .Where(a => (a.ClubId == c.ClubId || a.ClubId == null) && a.Status == "published" && (a.PublishedAt == null || a.PublishedAt <= now));
        foreach (var token in c.Tokens)
        {
            var tk = token;
            q = q.Where(a => a.ArticlesI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Title != null && i.Title.Contains(tk)) || (i.Summary != null && i.Summary.Contains(tk)))));
        }

        var count = await q.CountAsync(ct);
        if (!wanted || count == 0)
        {
            return (count, []);
        }

        var rows = await q.OrderByDescending(a => a.PublishedAt).ThenBy(a => a.RowSeq).Take(PerTypeLimit)
            .Select(a => new
            {
                a.Id, a.Slug, a.PublishedAt, a.CoverKey, CategoryCode = a.ArticleCategory.Code,
                TitleReq = a.ArticlesI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Title).FirstOrDefault(),
                TitleDef = a.ArticlesI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Title).FirstOrDefault(),
                SumReq = a.ArticlesI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Summary).FirstOrDefault(),
                SumDef = a.ArticlesI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Summary).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return (count, rows.Select((r, i) => Make(c, SearchTypes.News, null, r.Id, r.Slug, r.TitleReq, r.TitleDef, r.SumReq, r.SumDef,
            r.PublishedAt, r.CategoryCode, null, r.CoverKey, i)).ToList());
    }

    // ── FAQ ──────────────────────────────────────────────────────────────────────
    private async Task<(int, List<Hit>)> SearchFaqsAsync(Ctx c, bool wanted, CancellationToken ct)
    {
        var q = db.Faqs.AsNoTracking().Where(f => (f.ClubId == c.ClubId || f.ClubId == null) && f.Status == "published");
        foreach (var token in c.Tokens)
        {
            var tk = token;
            q = q.Where(f => f.FaqsI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Question != null && i.Question.Contains(tk)) || (i.Answer != null && i.Answer.Contains(tk)))));
        }

        var count = await q.CountAsync(ct);
        if (!wanted || count == 0)
        {
            return (count, []);
        }

        var rows = await q.OrderBy(f => f.SortOrder).ThenBy(f => f.RowSeq).Take(PerTypeLimit)
            .Select(f => new
            {
                f.Id, f.Slug,
                QReq = f.FaqsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Question).FirstOrDefault(),
                QDef = f.FaqsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Question).FirstOrDefault(),
                AReq = f.FaqsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Answer).FirstOrDefault(),
                ADef = f.FaqsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Answer).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return (count, rows.Select((r, i) => Make(c, SearchTypes.Faq, null, r.Id, r.Slug, r.QReq, r.QDef, r.AReq, r.ADef,
            null, null, null, null, i)).ToList());
    }

    // ── 課程 ─────────────────────────────────────────────────────────────────────
    private async Task<(int, List<Hit>)> SearchProgramsAsync(Ctx c, bool wanted, CancellationToken ct)
    {
        var q = db.Programs.AsNoTracking().Where(p => p.ClubId == c.ClubId && p.Status == "published");
        foreach (var token in c.Tokens)
        {
            var tk = token;
            q = q.Where(p => p.ProgramsI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Name != null && i.Name.Contains(tk)) || (i.Intro != null && i.Intro.Contains(tk)))));
        }

        var count = await q.CountAsync(ct);
        if (!wanted || count == 0)
        {
            return (count, []);
        }

        var rows = await q.OrderBy(p => p.RowSeq).Take(PerTypeLimit)
            .Select(p => new
            {
                p.Id, p.Slug, p.CoverKey,
                NReq = p.ProgramsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Name).FirstOrDefault(),
                NDef = p.ProgramsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Name).FirstOrDefault(),
                IReq = p.ProgramsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Intro).FirstOrDefault(),
                IDef = p.ProgramsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Intro).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return (count, rows.Select((r, i) => Make(c, SearchTypes.Program, null, r.Id, r.Slug, r.NReq, r.NDef, r.IReq, r.IDef,
            null, null, null, r.CoverKey, i)).ToList());
    }

    // ── 球員（照片遵守肖像同意）────────────────────────────────────────────────────
    private async Task<(int, List<Hit>)> SearchPlayersAsync(Ctx c, bool wanted, CancellationToken ct)
    {
        var q = db.Players.AsNoTracking().Where(p => p.ClubId == c.ClubId);
        foreach (var token in c.Tokens)
        {
            var tk = token;
            q = q.Where(p => p.PlayersI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Name != null && i.Name.Contains(tk)) || (i.Bio != null && i.Bio.Contains(tk)))));
        }

        var count = await q.CountAsync(ct);
        if (!wanted || count == 0)
        {
            return (count, []);
        }

        var rows = await q.OrderBy(p => p.Team.SortOrder).ThenBy(p => p.ShirtNo).ThenBy(p => p.RowSeq).Take(PerTypeLimit)
            .Select(p => new
            {
                p.Id, TeamCode = p.Team.Code, p.PhotoKey, p.PortraitConsentStatus,
                NReq = p.PlayersI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Name).FirstOrDefault(),
                NDef = p.PlayersI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Name).FirstOrDefault(),
                BReq = p.PlayersI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Bio).FirstOrDefault(),
                BDef = p.PlayersI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Bio).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return (count, rows.Select((r, i) => Make(c, SearchTypes.Player, null, r.Id, null, r.NReq, r.NDef, r.BReq, r.BDef,
            null, null, r.TeamCode, ConsentedPhoto(r.PortraitConsentStatus, r.PhotoKey), i)).ToList());
    }

    // ── 教練與團隊成員（同公開名單）──────────────────────────────────────────────────
    private async Task<(int, List<Hit>)> SearchStaffAsync(Ctx c, bool wanted, CancellationToken ct)
    {
        var q = db.Staff.AsNoTracking().Where(s => s.ClubId == c.ClubId || s.ClubId == null);
        foreach (var token in c.Tokens)
        {
            var tk = token;
            q = q.Where(s => s.StaffI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Name != null && i.Name.Contains(tk)) || (i.Title != null && i.Title.Contains(tk)) || (i.Bio != null && i.Bio.Contains(tk)))));
        }

        var count = await q.CountAsync(ct);
        if (!wanted || count == 0)
        {
            return (count, []);
        }

        var rows = await q.OrderBy(s => s.RowSeq).Take(PerTypeLimit)
            .Select(s => new
            {
                s.Id, s.PhotoKey, s.PortraitConsentStatus,
                NReq = s.StaffI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Name).FirstOrDefault(),
                NDef = s.StaffI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Name).FirstOrDefault(),
                TReq = s.StaffI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Title).FirstOrDefault(),
                TDef = s.StaffI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Title).FirstOrDefault(),
                BReq = s.StaffI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Bio).FirstOrDefault(),
                BDef = s.StaffI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Bio).FirstOrDefault(),
            })
            .ToListAsync(ct);
        // 摘錄優先用職稱，沒有職稱才用簡介。
        return (count, rows.Select((r, i) => Make(c, SearchTypes.Coach, null, r.Id, null, r.NReq, r.NDef,
            RequestLocale.Pick(r.TReq, r.TDef) ?? RequestLocale.Pick(r.BReq, r.BDef), null,
            null, null, null, ConsentedPhoto(r.PortraitConsentStatus, r.PhotoKey), i)).ToList());
    }

    // ── 慈善：計畫＋事蹟紀錄 ────────────────────────────────────────────────────────
    private async Task<(int, List<Hit>)> SearchCharityAsync(Ctx c, bool wanted, CancellationToken ct)
    {
        var programs = db.CharityPrograms.AsNoTracking()
            .Where(p => (p.ClubId == c.ClubId || p.ClubId == null) && p.Status == "published");
        var records = db.ImpactRecords.AsNoTracking().Where(r => r.ClubId == c.ClubId || r.ClubId == null);
        foreach (var token in c.Tokens)
        {
            var tk = token;
            programs = programs.Where(p => p.CharityProgramsI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Name != null && i.Name.Contains(tk)) || (i.TargetAudience != null && i.TargetAudience.Contains(tk)))));
            records = records.Where(r => r.ImpactRecordsI18ns.Any(i => (i.Locale == c.Locale || i.Locale == c.DefaultLocale)
                && ((i.Location != null && i.Location.Contains(tk)) || (i.BriefDescription != null && i.BriefDescription.Contains(tk))
                    || (i.DonationContent != null && i.DonationContent.Contains(tk)))));
        }

        var programCount = await programs.CountAsync(ct);
        var recordCount = await records.CountAsync(ct);
        var count = programCount + recordCount;
        if (!wanted || count == 0)
        {
            return (count, []);
        }

        var hits = new List<Hit>();
        if (programCount > 0)
        {
            var rows = await programs.OrderByDescending(p => p.IsPinned).ThenBy(p => p.SortOrder).ThenBy(p => p.RowSeq).Take(PerTypeLimit)
                .Select(p => new
                {
                    p.Id, p.Slug, p.CoverKey,
                    NReq = p.CharityProgramsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Name).FirstOrDefault(),
                    NDef = p.CharityProgramsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Name).FirstOrDefault(),
                    TReq = p.CharityProgramsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.TargetAudience).FirstOrDefault(),
                    TDef = p.CharityProgramsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.TargetAudience).FirstOrDefault(),
                })
                .ToListAsync(ct);
            hits.AddRange(rows.Select((r, i) => Make(c, SearchTypes.Charity, "program", r.Id, r.Slug, r.NReq, r.NDef, r.TReq, r.TDef,
                null, null, null, r.CoverKey, i)));
        }

        if (recordCount > 0)
        {
            var rows = await records.OrderByDescending(r => r.IsPinned).ThenBy(r => r.SortOrder).ThenByDescending(r => r.HappenedOn).ThenBy(r => r.RowSeq)
                .Take(PerTypeLimit)
                .Select(r => new
                {
                    r.Id, r.HappenedOn, r.ImageKey,
                    LReq = r.ImpactRecordsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Location).FirstOrDefault(),
                    LDef = r.ImpactRecordsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Location).FirstOrDefault(),
                    BReq = r.ImpactRecordsI18ns.Where(i => i.Locale == c.Locale).Select(i => i.BriefDescription).FirstOrDefault(),
                    BDef = r.ImpactRecordsI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.BriefDescription).FirstOrDefault(),
                    CReq = r.Charity.CharitiesI18ns.Where(i => i.Locale == c.Locale).Select(i => i.Name).FirstOrDefault(),
                    CDef = r.Charity.CharitiesI18ns.Where(i => i.Locale == c.DefaultLocale).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(ct);
            // 事蹟紀錄沒有標題欄位：標題用「受贈單位（地點）」，兩者都沒有就用固定的「慈善事蹟」。
            hits.AddRange(rows.Select((r, i) =>
            {
                var location = RequestLocale.Pick(r.LReq, r.LDef);
                var charity = RequestLocale.Pick(r.CReq, r.CDef);
                var title = (charity, location) switch
                {
                    ({ } ch, { } loc) => $"{ch}（{loc}）",
                    ({ } ch, null) => ch,
                    (null, { } loc) => loc,
                    _ => "慈善事蹟",
                };
                return Make(c, SearchTypes.Charity, "record", r.Id, null, title, title, r.BReq, r.BDef,
                    r.HappenedOn is { } d ? d.ToDateTime(TimeOnly.MinValue) : null, null, null, r.ImageKey, programCount + i);
            }));
        }

        return (count, hits);
    }

    // ── 共用 ─────────────────────────────────────────────────────────────────────
    private static string? ConsentedPhoto(string consent, string? key)
        => consent is "consented" or "consented_by_guardian" ? key : null; // 白名單，fail-closed（同 PlayersRepository.Map）

    private static Hit Make(
        Ctx c, string type, string? subType, Guid id, string? slug, string? titleReq, string? titleDef, string? textReq, string? textDef,
        DateTime? date, string? categoryCode, string? teamCode, string? imageKey, int rank)
    {
        var title = RequestLocale.Pick(titleReq, titleDef);
        var fallback = c.Locale != c.DefaultLocale && string.IsNullOrWhiteSpace(titleReq) && !string.IsNullOrWhiteSpace(titleDef);
        return new Hit(type, subType, id, slug, title, RequestLocale.Pick(textReq, textDef), date, categoryCode, teamCode, imageKey, fallback, rank);
    }

    /// <summary>2＝每個關鍵字都出現在標題；1＝其餘（命中在內文）。</summary>
    private static int Score(Hit h, IReadOnlyList<string> tokens)
        => h.Title is { } title && tokens.All(t => title.Contains(t, StringComparison.OrdinalIgnoreCase)) ? 2 : 1;

    private SearchResultItemDto ToDto(Hit h, IReadOnlyList<string> tokens) => new()
    {
        Type = h.Type,
        SubType = h.SubType,
        Id = h.Id,
        Slug = h.Slug,
        Title = h.Title ?? string.Empty,
        Snippet = BuildSnippet(h.Text, tokens),
        Date = h.Date,
        CategoryCode = h.CategoryCode,
        TeamCode = h.TeamCode,
        ImageUrl = imageUrls.Resolve(h.ImageKey),
        IsFallbackLocale = h.IsFallback,
    };

    /// <summary>去除標記、壓縮空白後，圍繞第一個命中的關鍵字截出約 <see cref="SnippetLength"/> 字；沒有命中（命中在標題）就取開頭。</summary>
    public static string? BuildSnippet(string? text, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var plain = Whitespace().Replace(MarkupTag().Replace(text, " "), " ").Trim();
        if (plain.Length == 0)
        {
            return null;
        }

        var first = tokens.Select(t => plain.IndexOf(t, StringComparison.OrdinalIgnoreCase)).Where(i => i >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, first - 30);
        var length = Math.Min(SnippetLength, plain.Length - start);
        var slice = plain.Substring(start, length);
        return (start > 0 ? "…" : string.Empty) + slice + (start + length < plain.Length ? "…" : string.Empty);
    }
}
