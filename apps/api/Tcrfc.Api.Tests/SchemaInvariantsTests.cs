using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 綱要層級的不變量（docs/12 §13.1、docs/18 <c>E-44</c>）：<b>委託方明文指示「本次資料庫設計不含 log」</b>，使用者 2026-09-23 也裁決撤回過稽核與登入日誌表。
/// 規劃書 J3 與多處「匯出寫入稽核」的條文因此在資料庫層沒有落點（改寫結構化日誌 <c>SensitiveActionLogger</c>）。
/// 派工單一再把「規劃書寫了」讀成「要建表」，所以把這條指示寫成測試：<c>db/club-schema.sql</c> 不得出現任何日誌表。
/// 客戶重新確認稽核政策後，要先改 docs/12 §13.1，再放行這支測試。
/// </summary>
public sealed class SchemaInvariantsTests
{
    private static string RepoRoot([CallerFilePath] string thisFilePath = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "..", ".."));

    [Fact]
    public void 主站綱要沒有稽核_登入_匯出_操作日誌表()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "db", "club-schema.sql"));
        var forbidden = new Regex(@"CREATE\s+TABLE\s+(\w*(audit|login|export|operation)_?logs?\w*)\s*\(", RegexOptions.IgnoreCase);
        var hits = forbidden.Matches(sql).Select(m => m.Groups[1].Value).ToList();
        Assert.True(hits.Count == 0,
            "綱要出現了日誌表（委託方指示本次資料庫設計不含 log，見 docs/12 §13.1、docs/18 E-44）：" + string.Join("、", hits));
    }

    [Fact]
    public void 原始事件表沒有個資欄位_會員_IP_定位_廣告識別碼()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "db", "club-schema.sql"));
        foreach (var table in new[] { "ad_events", "app_diagnostic_reports" })
        {
            var body = Regex.Match(sql, $@"CREATE TABLE {table} \((.*?)\n\);", RegexOptions.Singleline).Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(body), $"找不到 {table} 的定義");
            foreach (var column in Regex.Matches(body, @"^\s{2}(\w+)\s", RegexOptions.Multiline).Select(m => m.Groups[1].Value))
            {
                Assert.DoesNotMatch(@"(?i)member|(^|_)ip($|_)|ip_address|latitude|longitude|(^|_)lat($|_)|(^|_)lng($|_)|idfa|aaid|advertising", column);
            }
        }
    }
}
