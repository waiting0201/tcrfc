namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 301 轉址網址格式驗證（S1-12，主站規劃書 §4.8 H「301 轉址管理」）。規劃書沒有給格式規則，
/// 本輪判斷：一律是**站內相對路徑**（以 <c>/</c> 開頭），不接受完整網址（<c>https://...</c>）——
/// 舊官網網域即將停用，轉址目標一律是本站內的新網址；來源網址雖然理論上可能是外部網域的舊路徑，
/// 但 <c>content/migration/舊官網URL盤點.csv</c> 盤點到的舊網址本身也全是可以取出路徑部分的
/// 一般網頁（不含 querystring 導向），因此**不支援含 host 的完整網址**這條限制對現有待遷移清單
/// 沒有影響，見 apps/api/README.md「S1-12」段。
/// </summary>
internal static class RedirectPathPolicy
{
    private const int MaxLength = 500;

    public static void Validate(string? path, string fieldLabel, string? field = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AdminSeoValidationException($"{fieldLabel}為必填欄位。", field);
        }

        if (!path.StartsWith('/'))
        {
            throw new AdminSeoValidationException($"{fieldLabel}必須以「/」開頭（僅支援站內相對路徑，不支援完整網址）。", field);
        }

        if (path.Length > MaxLength)
        {
            throw new AdminSeoValidationException($"{fieldLabel}長度不能超過 {MaxLength} 字元。", field);
        }

        if (path.Any(char.IsWhiteSpace))
        {
            throw new AdminSeoValidationException($"{fieldLabel}不能包含空白字元。", field);
        }
    }

    /// <summary>
    /// 比對用的正規化形式：去掉結尾的斜線（根路徑 <c>/</c> 除外）。<c>/zh/about</c> 與 <c>/zh/about/</c> 視為同一個網址
    /// （2026-10 稽核 F 類：<c>/zh/about</c> 原本打不到存成 <c>/zh/about/</c> 的規則）。只用於「比對」與「衝突／迴圈偵測」，
    /// 儲存時保留管理者輸入的原樣，前台公開端點會同時輸出有無結尾斜線兩種寫法，見 <see cref="ExpandForMatching"/>。
    /// </summary>
    public static string Normalize(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }

    /// <summary>前台中介層以 <c>fromPath === 請求路徑</c> 精確比對；同一條規則展開成「有／無結尾斜線」兩種寫法，前台不用改也能命中兩種網址。根路徑只有一種寫法。</summary>
    public static IEnumerable<string> ExpandForMatching(string fromPath)
    {
        var normalized = Normalize(fromPath);
        yield return fromPath;
        if (normalized == "/")
        {
            yield break;
        }

        var alternate = fromPath.EndsWith('/') ? normalized : normalized + "/";
        if (!string.Equals(alternate, fromPath, StringComparison.Ordinal))
        {
            yield return alternate;
        }
    }

    /// <summary>
    /// 迴圈偵測：從 <paramref name="toPath"/> 沿著「生效中規則」一路追，若走回 <paramref name="fromPath"/> 就是迴圈
    /// （A→B 且 B→A，或更長的環）。<paramref name="activeRules"/> 以正規化後的來源網址為鍵。回傳迴圈路徑說明，沒有迴圈回 null。
    /// 沿途若遇到其他既有的環（與本條無關）以 visited 集合中止，不會無窮迴圈。
    /// </summary>
    public static string? FindLoop(string fromPath, string toPath, IReadOnlyDictionary<string, string> activeRules)
    {
        var start = Normalize(fromPath);
        var chain = new List<string> { start };
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var current = Normalize(toPath);

        while (true)
        {
            chain.Add(current);
            if (string.Equals(current, start, StringComparison.Ordinal))
            {
                return string.Join(" → ", chain);
            }

            if (!visited.Add(current) || !activeRules.TryGetValue(current, out var next))
            {
                return null;
            }

            current = Normalize(next);
        }
    }
}

