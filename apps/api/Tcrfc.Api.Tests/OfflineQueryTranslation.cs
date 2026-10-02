using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 2026-10-02：<b>不需要資料庫</b>的 EF 查詢翻譯冒煙測試工具。
///
/// 為什麼要有：依賴資料庫的整合測試在沒有本機庫的環境（例如工作樹）跑不起來，而 EF 的 LINQ 查詢有一類錯誤只在<b>執行期翻譯成 SQL 時</b>才爆
/// （<c>The LINQ expression … could not be translated</c>），編譯器抓不到。這個工具建一個指向「必定連不上的位址」的 <see cref="ClubDbContext"/>
/// （埠 1，立即拒絕連線），然後執行被測的 repository 方法：
/// <list type="bullet">
/// <item>查詢<b>翻譯失敗</b> → 丟 <see cref="InvalidOperationException"/>（訊息含 "could not be translated"）→ 測試失敗；</item>
/// <item>查詢翻譯成功、嘗試連線被拒 → 丟 <see cref="SqlException"/> → 視為通過（翻譯階段沒問題）。</item>
/// </list>
/// 限制：只能證明「翻譯得過」，不證明 SQL 語意正確、欄位名稱與資料庫一致（那些仍靠整合測試與 <c>EfModelMatchesDatabaseTests</c>）；
/// 在 <c>BeginTransaction</c> 之後才執行的查詢、<c>ExecuteUpdate</c> 這類不經 <c>ToQueryString</c> 的寫入走不到翻譯階段（連線在更早的地方就被拒）。
/// </summary>
internal static class OfflineQueryTranslation
{
    public static ClubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ClubDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=offline_translation_only;Integrated Security=true;Encrypt=false;Connect Timeout=2")
            .Options;
        return new ClubDbContext(options);
    }

    /// <summary>執行 <paramref name="action"/>；只要不是「翻譯失敗」就通過。</summary>
    public static async Task AssertTranslatesAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (SqlException)
        {
            // 連線被拒：代表查詢已經走到「送出」這一步，翻譯成功。
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase)
                                                   || ex.Message.Contains("無法翻譯", StringComparison.Ordinal))
        {
            Assert.Fail("EF 查詢無法翻譯成 SQL：" + ex.Message);
        }
    }
}
