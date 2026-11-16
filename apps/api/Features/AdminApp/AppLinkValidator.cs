using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// 後台「給 App 使用者點的連結」的唯一驗證入口（推播點擊目標、公告條連結、廣告素材點擊目的地、廣告版位備援連結）。
/// 規則：空白＝沒有連結；<c>http(s)://</c> 完整網址（不得含大括號）；<c>tcrfc://</c> 必須能被
/// <see cref="AppLinkContract"/>（<c>shared/deeplinks.json</c>）解析，不得是樣板，有參數時<b>指向的對象必須存在且已公開</b>。
/// 「深連結對照表」本身是路由規則，仍可存樣板，不走這裡。
/// 錯誤訊息一律日常中文，不露出 <c>tcrfc://</c>、slug、id 等技術詞（docs/06 §1）。
/// </summary>
public sealed class AppLinkValidator(ClubDbContext db)
{
    /// <summary>路由代號 → 「還缺哪一種對象」的日常說法（只放說法，路由本身以契約檔為準；契約新增路由而這裡沒補，測試會紅燈）。</summary>
    public static readonly IReadOnlyDictionary<string, string> TargetNouns = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["schedule"] = "球隊", ["match"] = "比賽", ["news"] = "最新消息", ["player"] = "球員", ["store"] = "特約店家", ["program"] = "課程",
    };

    /// <summary>不需要對象的路由與「有列舉參數」的路由，在選單上的名稱。</summary>
    internal static readonly IReadOnlyDictionary<string, string> FixedLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["membercard"] = "會員卡", ["upgrade"] = "會籍升級",
        ["schedule:d1"] = "賽程（台中磐石一線隊）", ["schedule:bw1"] = "賽程（台中藍鯨一線隊）",
    };

    /// <summary>需要挑對象的路由，在選單上的名稱。</summary>
    internal static readonly IReadOnlyDictionary<string, string> TargetLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["match"] = "某一場比賽", ["news"] = "某一則最新消息", ["player"] = "某位球員", ["store"] = "某間特約店家", ["program"] = "某個課程",
    };

    // ═════════ 驗證 ═════════

    /// <summary>
    /// 只做語法層檢查（不連資料庫）。回傳錯誤訊息；合法回 <c>null</c>。供靜態驗證（<see cref="AdminInput"/>）與本類別共用。
    /// 只處理 <c>tcrfc://</c> 與大括號；<c>http(s)</c> 的格式由呼叫端另外檢查。
    /// </summary>
    public static string? CheckSyntax(string link, string label)
    {
        var isApp = link.StartsWith("tcrfc://", StringComparison.OrdinalIgnoreCase);
        if (AppLinkContract.HasPlaceholder(link))
        {
            if (isApp && AppLinkContract.Instance.MatchTemplateRoute(link) is { } routeId && TargetNouns.TryGetValue(routeId, out var noun))
            {
                return routeId == "schedule"
                    ? $"「{label}」還缺少要前往的球隊，請從選單選擇賽程。"
                    : $"「{label}」還缺少要前往的對象，請選擇一個{noun}。";
            }

            return $"「{label}」含有還沒填入的內容，請從選單重新選擇要前往的位置。";
        }

        if (isApp && AppLinkContract.Instance.TryParse(link) is null)
        {
            return $"「{label}」不是 App 能開啟的位置，請從選單重新選擇。";
        }

        return null;
    }

    /// <summary>
    /// 驗證並回傳修剪後的值。<paramref name="field"/> 是錯誤要掛的欄位鍵。
    /// 對象存在性檢查會連資料庫；<paramref name="skipTargetCheck"/> 為真時只做語法（連結沒變動時用，見各呼叫端）。
    /// </summary>
    public async Task<string?> ValidateAsync(string? raw, string label, string field, CancellationToken cancellationToken, bool skipTargetCheck = false)
    {
        var text = AdminInput.OptionalText(raw, label, 500, field);
        if (text is null)
        {
            return null;
        }

        if (!text.StartsWith("tcrfc://", StringComparison.OrdinalIgnoreCase))
        {
            if (AppLinkContract.HasPlaceholder(text))
            {
                throw new AdminValidationException($"「{label}」含有還沒填入的內容，請重新輸入完整的網址。", field);
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new AdminValidationException($"「{label}」必須是以 http:// 或 https:// 開頭的完整網址，或從選單選擇 App 內的位置。", field);
            }

            return text;
        }

        if (CheckSyntax(text, label) is { } syntax)
        {
            throw new AdminValidationException(syntax, field);
        }

        var parsed = AppLinkContract.Instance.TryParse(text)!;
        if (!skipTargetCheck && parsed.Params.Count > 0 && !parsed.Params.ContainsKey("team"))
        {
            if (await LookupAsync(parsed, cancellationToken) is null)
            {
                var noun = TargetNouns.GetValueOrDefault(parsed.RouteId, "對象");
                throw new AdminValidationException($"「{label}」指向的{noun}找不到，或還沒有公開，請重新選擇。", field);
            }
        }

        return text;
    }

    // ═════════ 選單與還原 ═════════

    public sealed record Destination(string Key, string Label, string? Link, string? TargetKind);

    /// <summary>後台連結選單的第一步（要前往哪裡）。清單由契約檔的路由展開，名稱來自本類別的對照；順序依契約檔。</summary>
    public static IReadOnlyList<Destination> Destinations()
    {
        var list = new List<Destination>();
        foreach (var route in AppLinkContract.Instance.Routes)
        {
            var enumParam = route.Params.FirstOrDefault(p => p.Value is not null);
            if (enumParam.Key is not null)
            {
                foreach (var value in enumParam.Value!)
                {
                    var key = $"{route.Id}:{value}";
                    list.Add(new Destination(key, FixedLabels.GetValueOrDefault(key, "賽程（其他球隊）"),
                        AppLinkContract.Instance.Build(route.Id, new Dictionary<string, string> { [enumParam.Key] = value }), null));
                }
            }
            else if (route.Params.Count > 0)
            {
                list.Add(new Destination(route.Id, TargetLabels.GetValueOrDefault(route.Id, "App 內的其他內容"), null, route.Id));
            }
            else
            {
                list.Add(new Destination(route.Id, FixedLabels.GetValueOrDefault(route.Id, "App 內的其他畫面"), AppLinkContract.Instance.Build(route.Id), null));
            }
        }

        return list;
    }

    public sealed record DescribeResult(string Status, string? DestinationKey, string? TargetLabel, string? Message);

    /// <summary>
    /// 把已存的連結還原成「目的地＋對象」給畫面顯示。<c>Status</c>：<c>empty</c> 沒有連結／<c>app</c> 可用的 App 內位置／<c>web</c> 網址／
    /// <c>template</c> 舊的未填完樣板／<c>unrecognized</c> App 無法開啟／<c>missing</c> 指向的對象找不到或已下架。
    /// 後三種畫面要提示重新選擇，不得靜默清空（E-357）。
    /// </summary>
    public async Task<DescribeResult> DescribeAsync(string? link, CancellationToken cancellationToken)
    {
        var text = link?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return new DescribeResult("empty", null, null, null);
        }

        if (!text.StartsWith("tcrfc://", StringComparison.OrdinalIgnoreCase))
        {
            return AppLinkContract.HasPlaceholder(text)
                ? new DescribeResult("template", null, null, "這個連結還沒填完，App 目前點了不會有反應，請重新選擇。")
                : new DescribeResult("web", null, null, null);
        }

        if (CheckSyntax(text, "連結") is { } problem)
        {
            return new DescribeResult(AppLinkContract.HasPlaceholder(text) ? "template" : "unrecognized", null, null,
                problem.Replace("「連結」", "這個連結"));
        }

        var parsed = AppLinkContract.Instance.TryParse(text)!;
        var route = AppLinkContract.Instance.Routes.First(r => r.Id == parsed.RouteId);
        var enumName = route.Params.FirstOrDefault(p => p.Value is not null).Key;
        if (enumName is not null)
        {
            return new DescribeResult("app", $"{route.Id}:{parsed.Params[enumName]}", null, null);
        }

        if (parsed.Params.Count == 0)
        {
            return new DescribeResult("app", route.Id, null, null);
        }

        var target = await LookupAsync(parsed, cancellationToken);
        return target is null
            ? new DescribeResult("missing", route.Id, null, $"這個連結指向的{TargetNouns.GetValueOrDefault(route.Id, "對象")}找不到，或已經下架，App 點了不會有反應，請重新選擇。")
            : new DescribeResult("app", route.Id, target.Label, null);
    }

    // ═════════ 對象查詢（選擇器與存在性共用） ═════════

    public sealed record Target(string Link, string Label, string? Hint);

    internal static readonly IReadOnlySet<string> TargetKinds = new HashSet<string>(["match", "news", "program", "player", "store"], StringComparer.Ordinal);
    private const int SearchLimit = 30;

    /// <summary>搜尋可選的對象（只列已公開的）。不分俱樂部：App 是兩隊共用，每筆附上所屬俱樂部（兩隊共同的內容不附）。</summary>
    public async Task<IReadOnlyList<Target>> SearchAsync(string kind, string? query, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(kind, TargetKinds, "對象類型", "比賽、最新消息、課程、球員或特約店家", "kind");
        var q = query?.Trim();
        if (q is { Length: > 100 })
        {
            q = q[..100];
        }

        var clubNames = await ClubNamesAsync(cancellationToken);
        string? Club(Guid? id) => id is { } g ? clubNames.GetValueOrDefault(g) : null;
        var now = DateTime.UtcNow;
        var link = AppLinkContract.Instance;

        switch (kind)
        {
            case "news":
            {
                var rows = db.Articles.AsNoTracking().Where(a => a.Status == "published" && (a.PublishedAt == null || a.PublishedAt <= now));
                if (!string.IsNullOrEmpty(q))
                {
                    rows = rows.Where(a => a.ArticlesI18ns.Any(i => i.Title != null && i.Title.Contains(q)));
                }

                var list = await rows.OrderByDescending(a => a.PublishedAt).Take(SearchLimit)
                    .Select(a => new { a.Slug, a.ClubId, a.PublishedAt, Title = a.ArticlesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault() })
                    .ToListAsync(cancellationToken);
                return list.Select(a => new Target(Build(link, "news", "slug", a.Slug), a.Title ?? "（未命名）", Hint(Club(a.ClubId), a.PublishedAt?.ToString("yyyy-MM-dd")))).ToList();
            }

            case "program":
            {
                var rows = db.Programs.AsNoTracking().Where(p => p.Status == "published");
                if (!string.IsNullOrEmpty(q))
                {
                    rows = rows.Where(p => p.ProgramsI18ns.Any(i => i.Name != null && i.Name.Contains(q)));
                }

                var list = await rows.OrderBy(p => p.RowSeq).Take(SearchLimit)
                    .Select(p => new { p.Slug, p.ClubId, Name = p.ProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() })
                    .ToListAsync(cancellationToken);
                return list.Select(p => new Target(Build(link, "program", "slug", p.Slug), p.Name ?? "（未命名）", Hint(Club(p.ClubId), null))).ToList();
            }

            case "player":
            {
                var rows = db.Players.AsNoTracking().Where(p => p.Status == null || p.Status == "active");
                if (!string.IsNullOrEmpty(q))
                {
                    rows = rows.Where(p => p.PlayersI18ns.Any(i => i.Name != null && i.Name.Contains(q)));
                }

                var list = await rows.OrderBy(p => p.ClubId).ThenBy(p => p.ShirtNo).Take(SearchLimit)
                    .Select(p => new { p.Slug, p.ClubId, p.ShirtNo, Name = p.PlayersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() })
                    .ToListAsync(cancellationToken);
                return list.Select(p => new Target(Build(link, "player", "slug", p.Slug),
                    $"{p.Name ?? "（未命名球員）"}{(p.ShirtNo is { } n ? $" · 背號 {n}" : "")}", Hint(Club(p.ClubId), null))).ToList();
            }

            case "store":
            {
                var today = TaiwanClock.Today;
                var rows = db.PartnerStores.AsNoTracking().Where(s => s.Status == "published" && (s.StartOn == null || s.StartOn <= today) && (s.EndOn == null || s.EndOn >= today));
                if (!string.IsNullOrEmpty(q))
                {
                    rows = rows.Where(s => s.PartnerStoresI18ns.Any(i => i.Name != null && i.Name.Contains(q)));
                }

                var list = await rows.OrderBy(s => s.SortOrder).ThenBy(s => s.RowSeq).Take(SearchLimit)
                    .Select(s => new { s.Slug, s.ClubId, Name = s.PartnerStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() })
                    .ToListAsync(cancellationToken);
                return list.Select(s => new Target(Build(link, "store", "id", s.Slug), s.Name ?? "（未命名）", Hint(Club(s.ClubId), null))).ToList();
            }

            default: // match
            {
                var rows = db.Matches.AsNoTracking().AsQueryable();
                if (!string.IsNullOrEmpty(q))
                {
                    rows = rows.Where(m => (m.OpponentTeam != null && m.OpponentTeam.Name.Contains(q)) || (m.Opponent != null && m.Opponent.Contains(q)));
                }

                var list = await rows.OrderByDescending(m => m.MatchOn).Take(SearchLimit)
                    .Select(m => new { m.Id, m.ClubId, m.MatchOn, m.HomeAway, m.Status, m.Competition, TeamName = m.OpponentTeam != null ? m.OpponentTeam.Name : null, m.Opponent })
                    .ToListAsync(cancellationToken);
                return list.Select(m => new Target(Build(link, "match", "id", m.Id.ToString()), MatchLabel(m.MatchOn, m.TeamName ?? m.Opponent, m.HomeAway, m.Competition, m.Status), Club(m.ClubId))).ToList();
            }
        }
    }

    private static string Build(AppLinkContract c, string route, string param, string value)
        => c.Build(route, new Dictionary<string, string> { [param] = value });

    private static string? Hint(string? club, string? extra)
        => club is null && extra is null ? null : string.Join(" · ", new[] { club, extra }.Where(s => !string.IsNullOrEmpty(s)));

    private static string MatchLabel(DateOnly on, string? opponent, string? homeAway, string? competition, string? status)
    {
        var side = homeAway switch { "home" => "主場", "away" => "客場", _ => null };
        var tail = string.Join(" · ", new[] { side, competition?.Trim(), status == "cancelled" ? "已取消" : null }.Where(s => !string.IsNullOrEmpty(s)));
        return $"{on:yyyy-MM-dd} 對 {(string.IsNullOrWhiteSpace(opponent) ? "對手未定" : opponent.Trim())}{(tail.Length > 0 ? $"（{tail}）" : "")}";
    }

    private async Task<Dictionary<Guid, string>> ClubNamesAsync(CancellationToken cancellationToken)
        => await db.Clubs.AsNoTracking().Select(c => new { c.Id, Name = c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.ShortName ?? i.Name).FirstOrDefault() })
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? "", cancellationToken);

    /// <summary>已解析連結的對象是否存在且公開；存在回標籤，否則 <c>null</c>。</summary>
    internal async Task<Target?> LookupAsync(AppLinkContract.Parsed parsed, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        switch (parsed.RouteId)
        {
            case "match" when Guid.TryParse(parsed.Params["id"], out var id):
            {
                var m = await db.Matches.AsNoTracking().Where(x => x.Id == id)
                    .Select(x => new { x.MatchOn, x.HomeAway, x.Status, x.Competition, TeamName = x.OpponentTeam != null ? x.OpponentTeam.Name : null, x.Opponent })
                    .FirstOrDefaultAsync(cancellationToken);
                return m is null ? null : new Target(string.Empty, MatchLabel(m.MatchOn, m.TeamName ?? m.Opponent, m.HomeAway, m.Competition, m.Status), null);
            }

            case "news":
            {
                var slug = parsed.Params["slug"];
                var t = await db.Articles.AsNoTracking().Where(a => a.Slug == slug && a.Status == "published" && (a.PublishedAt == null || a.PublishedAt <= now))
                    .Select(a => new { Title = a.ArticlesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault() }).FirstOrDefaultAsync(cancellationToken);
                return t is null ? null : new Target(string.Empty, t.Title ?? "（未命名）", null);
            }

            case "program":
            {
                var slug = parsed.Params["slug"];
                var t = await db.Programs.AsNoTracking().Where(p => p.Slug == slug && p.Status == "published")
                    .Select(p => new { Title = p.ProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() }).FirstOrDefaultAsync(cancellationToken);
                return t is null ? null : new Target(string.Empty, t.Title ?? "（未命名）", null);
            }

            case "player":
            {
                var key = parsed.Params["slug"];
                var pid = Guid.TryParse(key, out var g) ? g : Guid.Empty; // App／官網的球員詳情同時接受網址代稱與編號
                var t = await db.Players.AsNoTracking().Where(p => (p.Slug == key || p.Id == pid) && (p.Status == null || p.Status == "active"))
                    .Select(p => new { Title = p.PlayersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() }).FirstOrDefaultAsync(cancellationToken);
                return t is null ? null : new Target(string.Empty, t.Title ?? "（未命名球員）", null);
            }

            case "store":
            {
                var key = parsed.Params["id"];
                var sid = Guid.TryParse(key, out var g) ? g : Guid.Empty;
                var today = TaiwanClock.Today;
                var t = await db.PartnerStores.AsNoTracking()
                    .Where(s => (s.Slug == key || s.Id == sid) && s.Status == "published" && (s.StartOn == null || s.StartOn <= today) && (s.EndOn == null || s.EndOn >= today))
                    .Select(s => new { Title = s.PartnerStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() }).FirstOrDefaultAsync(cancellationToken);
                return t is null ? null : new Target(string.Empty, t.Title ?? "（未命名）", null);
            }

            default:
                return null;
        }
    }
}
