using System.Text.Json;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// App 深連結契約（<c>shared/deeplinks.json</c>）的後端解析器。兩個 App 打包的是同一份檔案，後台存進資料庫、之後要被 App 點開的連結，
/// 必須能被這份契約解析——否則後台設了等於沒設（稽核 docs/23 M-3）。
/// <para>
/// 🔴 路由表<b>不手抄</b>：資源 <c>deeplinks.contract.json</c> 是 <c>shared/deeplinks.json</c> 的逐字複本（Docker build context 是
/// <c>apps/api</c>，執行期讀不到 <c>shared/</c>），<c>AppLinkContractTests</c> 比對兩份位元組相同，且用契約檔的 <c>parseCases</c> 逐案驗證本解析器。
/// 要改路由先改規劃書 §2.3 與 <c>shared/deeplinks.json</c>，再 <c>shared/scripts/gen-all.sh</c>（會重新複製）。
/// </para>
/// 解析規則刻意與 App 一致（Android <c>DeepLinkParser.parseCustom／bind</c>）：scheme 不分大小寫、字面段不分大小寫、有 enum 的參數轉小寫比對、
/// 尾端斜線忽略、參數值空白或含大括號視為無法解析。與 App 不同的地方只有一處：<b>本解析器不接受 <c>?</c>／<c>#</c></b>
/// （App 會丟掉查詢字串；後台不需要存查詢字串，存了反而是下錯指令的徵兆）。
/// </summary>
public sealed class AppLinkContract
{
    public const string Scheme = "tcrfc";

    public sealed record Route(string Id, string Pattern, IReadOnlyList<string> Segments, IReadOnlyDictionary<string, IReadOnlyList<string>?> Params);

    public sealed record Parsed(string RouteId, IReadOnlyDictionary<string, string> Params);

    private readonly IReadOnlyList<Route> routes;

    public static AppLinkContract Instance { get; } = Load();

    public IReadOnlyList<Route> Routes => routes;

    private AppLinkContract(IReadOnlyList<Route> routes) => this.routes = routes;

    private static AppLinkContract Load()
    {
        var assembly = typeof(AppLinkContract).Assembly;
        using var stream = assembly.GetManifestResourceStream("deeplinks.contract.json")
                           ?? throw new InvalidOperationException("找不到內嵌的 deeplinks.contract.json。");
        return Parse(JsonDocument.Parse(stream).RootElement);
    }

    internal static AppLinkContract Parse(JsonElement root)
    {
        var list = new List<Route>();
        foreach (var r in root.GetProperty("routes").EnumerateArray())
        {
            var pattern = r.GetProperty("pattern").GetString()!;
            var ps = new Dictionary<string, IReadOnlyList<string>?>(StringComparer.Ordinal);
            foreach (var p in r.GetProperty("params").EnumerateObject())
            {
                IReadOnlyList<string>? allowed = null;
                if (p.Value.TryGetProperty("enum", out var e))
                {
                    allowed = e.EnumerateArray().Select(x => x.GetString()!).ToList();
                }

                ps[p.Name] = allowed;
            }

            list.Add(new Route(r.GetProperty("id").GetString()!, pattern, Segments(pattern[(pattern.IndexOf("://", StringComparison.Ordinal) + 3)..]), ps));
        }

        return new AppLinkContract(list);
    }

    private static string[] Segments(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>連結是不是帶有大括號（沒填完的樣板）。</summary>
    public static bool HasPlaceholder(string link) => link.Contains('{') || link.Contains('}');

    /// <summary>
    /// 解析 <c>tcrfc://…</c>。無法辨識（不是 tcrfc、路徑不存在、缺參數、參數不在列舉內、參數含大括號）回 <c>null</c>。
    /// </summary>
    public Parsed? TryParse(string link)
    {
        var trimmed = link.Trim();
        var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0 || !trimmed[..schemeEnd].Equals(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = trimmed[(schemeEnd + 3)..];
        if (rest.Any(c => c is '?' or '#' || char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return null;
        }

        var input = Segments(rest);
        if (input.Length == 0)
        {
            return null;
        }

        foreach (var route in routes)
        {
            if (Bind(route, input) is { } bound)
            {
                return new Parsed(route.Id, bound);
            }
        }

        return null;
    }

    private static Dictionary<string, string>? Bind(Route route, string[] input)
    {
        if (route.Segments.Count != input.Length)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < input.Length; i++)
        {
            var p = route.Segments[i];
            if (p.Length > 2 && p[0] == '{' && p[^1] == '}')
            {
                var name = p[1..^1];
                var value = input[i];
                if (route.Params.TryGetValue(name, out var allowed) && allowed is not null)
                {
                    value = value.ToLowerInvariant();
                    if (!allowed.Contains(value))
                    {
                        return null;
                    }
                }

                if (string.IsNullOrWhiteSpace(value) || HasPlaceholder(value))
                {
                    return null;
                }

                result[name] = value;
            }
            else if (!p.Equals(input[i], StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return result;
    }

    /// <summary>
    /// 帶大括號的連結是不是「某條路由的樣板」（例如 <c>tcrfc://news/{slug}</c>）：是的話回傳那條路由的代號，
    /// 讓錯誤訊息能指出「還缺哪一種對象」。不是（含大括號但對不上任何路由）回 <c>null</c>。
    /// </summary>
    public string? MatchTemplateRoute(string link)
    {
        var trimmed = link.Trim();
        var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0 || !trimmed[..schemeEnd].Equals(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var input = Segments(trimmed[(schemeEnd + 3)..]);
        foreach (var route in routes)
        {
            if (route.Segments.Count != input.Length)
            {
                continue;
            }

            var ok = true;
            for (var i = 0; i < input.Length && ok; i++)
            {
                var p = route.Segments[i];
                ok = p.Length > 2 && p[0] == '{' && p[^1] == '}'
                    ? HasPlaceholder(input[i])
                    : p.Equals(input[i], StringComparison.OrdinalIgnoreCase);
            }

            if (ok && route.Segments.Any(s => s.StartsWith('{')))
            {
                return route.Id;
            }
        }

        return null;
    }

    /// <summary>依路由與參數組出完整連結（<c>tcrfc://news/opening-day</c>）；參數對不上路由拋例外（程式錯誤，不是使用者輸入）。</summary>
    public string Build(string routeId, IReadOnlyDictionary<string, string>? values = null)
    {
        var route = routes.First(r => r.Id == routeId);
        var link = route.Pattern;
        foreach (var name in route.Params.Keys)
        {
            link = link.Replace("{" + name + "}", values is not null && values.TryGetValue(name, out var v) ? v : throw new ArgumentException($"缺少參數 {name}。"), StringComparison.Ordinal);
        }

        return link;
    }
}
