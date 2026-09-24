using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Common;

/// <summary>
/// 取得 **資料庫伺服器自己的目前時間**（UTC），不是應用程式行程的 <see cref="DateTime.UtcNow"/>。
///
/// 🔴🔴🔴 根因（2026-09-24，S1-4 續作，`PagesPublicEndpointTests` 間歇性失敗排查）：
/// 公開讀取查詢（<c>ArticlesRepository</c>／<c>PagesRepository</c>）判斷「已到發布時間」一律用
/// <c>published_at &lt;= SYSUTCDATETIME()</c>——這個比較的兩邊如果分別來自**不同的時鐘來源**，
/// 只要兩個時鐘之間有任何飄移（本機環境是應用程式行程的作業系統時鐘 vs. `sqlserver` 容器自己的
/// 作業系統時鐘，Docker Desktop for Mac 在主機睡眠喚醒後尤其容易出現數毫秒到數十毫秒的飄移），
/// 就會產生「剛發布的內容，發布時間比資料庫自己認定的『現在』還晚」這種矛盾狀態，導致剛發布的頁面
/// 在下一個瞬間的公開查詢裡**暫時**（直到資料庫時鐘追上）被判定為「還沒到發布時間」而查不到。
///
/// **實測**（`dotnet test --filter FullyQualifiedName~Pages`，`--no-build` 連跑 15 次）：2 次失敗，
/// 兩次診斷輸出都顯示 `publishStatus=OK`（發布本身完全成功，不是樂觀並行衝突 409）、
/// 資料庫實際列出的 `status=published`，但緊接著的公開查詢仍回 404——排除了「並行權杖精度」
/// 這個原本懷疑的方向（`updated_at` 從未參與任何 `SYSUTCDATETIME()` 比較，樂觀並行檢查本身是
/// EF Core 產生的 `WHERE updated_at = @原始值` 純值比對，不牽涉即時時鐘）。
///
/// **修法**：`AdminArticlesRepository.PublishAsync`／`AdminPagesRepository.PublishAsync`
/// 這種「立即發布，`published_at` 設為現在」的寫入路徑，改用**資料庫自己的「現在」**
/// （本類別），確保寫進去的 `published_at` 保證不晚於資料庫自己接下來任何一次 `SYSUTCDATETIME()`
/// 讀取——時間只會往前走，同一個時鐘來源內不可能有「剛寫入的值比現在還晚」這種矛盾。
///
/// ⚠️ **`ScheduleAsync`／`ScheduledPublishRunner` 不需要這個修法**：前者的 `published_at`
/// 是呼叫端指定的未來時間，不是「現在」；後者的 `UPDATE ... WHERE published_at &lt;= SYSUTCDATETIME()`
/// 整句都在同一個 SQL 陳述式內求值，天生是同一個時鐘來源，不受這個問題影響
/// （見兩個 repository 上對應方法的註解）。⚠️ **`updated_at`（樂觀並行權杖）也不需要**——
/// 沒有任何查詢拿它跟即時時鐘比較，這個問題只發生在「寫入時的時間戳」與「讀取時的即時時鐘」
/// 兩者分屬不同機器時鐘、且中間比較「是否已經到了」這種會被時鐘方向敏感的地方。
/// </summary>
internal static class DatabaseClock
{
    public static async Task<DateTime> GetUtcNowAsync(ClubDbContext dbContext, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQueryRaw<DateTime>("SELECT SYSUTCDATETIME() AS [Value]")
            .ToListAsync(cancellationToken);

        return rows[0];
    }
}
