using Dapper;
using Microsoft.Extensions.Logging;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.News;

/// <summary>
/// 🔴🔴🔴 S0-7g 定案：排程發布「時間到了誰把 <c>scheduled</c> 改成 <c>published</c>」的落點。
///
/// <see cref="Data.ClubSqlConnectionFactory"/> 依 <c>docs/17-deployment.md</c>「排程」一項的既有
/// 定案——**「Azure SQL 無 SQL Agent，排程發布、逾時取消訂單、每日對帳一律由 .NET 的 hosted
/// service 承擔」**——本類別就是那個定案在「排程發布」這個功能上的實作，
/// <see cref="ScheduledPublishBackgroundService"/> 只是定時呼叫它的殼，實際的 SQL 與快取失效邏輯
/// 全部在這裡，方便測試直接呼叫 <see cref="PublishDueArticlesAsync"/> 而不必等待計時器。
///
/// 這不是自己發明的機制：使用者裁決「規劃書沒寫的地方，開發端不得自行發明，執行層決定由開發端
/// 依 docs/17 既有結論拍板」——docs/17 早已把「排程發布用 hosted service」定案，只是先前的新聞
/// 垂直切片（S0-8）還沒有真的接上，這裡把它接上。
///
/// 🔴 目前只掃 <c>articles</c> 一張表。<c>db/club-schema.sql</c> 另外還有 8 張表帶
/// <c>CHECK (status IN ('draft','published','scheduled'))</c>：
/// <list type="bullet">
/// <item><c>pages</c>——**有** <c>published_at</c> 欄位，但 <c>apps/api/Features</c> 還沒有任何
/// Pages 的公開讀取或後台寫入端點，沒有讀取路徑就不會有這個 bug 的實際後果。等 Pages 端點開發時
/// 把它加進下面的 SQL（改成 <c>UNION ALL</c> 或另開一個 <c>PublishDuePagesAsync</c>），欄位已經
/// 備妥，不需要新的 migration。</item>
/// <item><c>press_resources</c>／<c>faqs</c>／<c>competitions</c>／<c>sponsor_packages</c>／
/// <c>collections</c>／<c>products</c>／<c>charity_programs</c>——**連 <c>published_at</c> 欄位
/// 都沒有**（已逐張 grep <c>db/club-schema.sql</c> 核對過）。CHECK 約束允許寫入 <c>'scheduled'</c>，
/// 但資料庫裡沒有任何欄位記錄「排定何時發布」，這是既有的欄位缺漏，不是本輪任務範圍能修的——
/// 改資料表結構要先走 `docs/12` 的同步鏈（CLAUDE.md 第 2、3 條）再走 EF migration，本輪任務指示
/// 明講「需要改資料表結構就停下來回報，不要自己加 migration」。回報見 STATUS.md S0-7g 與
/// <c>docs/12d-field-audit.md</c>。</item>
/// </list>
/// </summary>
public sealed class ScheduledPublishRunner(
    IClubSqlConnectionFactory connectionFactory,
    IQueryCache cache,
    ILogger<ScheduledPublishRunner> logger)
{
    // 🔴 必須與 ArticlesRepository.ListEntity／DetailEntity、AdminArticlesRepository
    // .PublicListEntity／PublicDetailEntity 三處的字面值完全一致——IQueryCache 用字串比對
    // 版本號命名空間（ver:{entity}:{club}），字面值對不起來就是「失效呼叫看似成功、實際上
    // 失效了一個沒人在讀的 key」。這裡沒有直接 reference 那兩個 private const，是延續既有
    // 程式碼的既有慣例（AdminArticlesRepository 本來就已經是「各處各自宣告相同字面值」，不是
    // 本輪放大的風險）；三處字面值目前一致，已用 grep 核對過。
    private const string ArticlesListEntity = "articles";
    private const string ArticleDetailEntity = "article-detail";

    /// <summary>
    /// 把「排定時間已到」的文章從 <c>scheduled</c> 轉成 <c>published</c>，並讓公開讀取的快取失效。
    /// 回傳這一次實際轉換的筆數（給呼叫端記錄／測試斷言用，0 是正常情況，不代表出錯）。
    /// </summary>
    public async Task<int> PublishDueArticlesAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        // 🔴 冪等且對多實例安全：這是一個條件式 UPDATE，不是「先 SELECT 一批 id 再逐筆 UPDATE」。
        // 兩個 API 容器（docs/17：目前只有一個，但這裡的寫法不假設永遠只有一個）同時執行這支
        // 語句時，SQL Server 對第二個語句會在同一批列上取鎖，若列已被第一個語句鎖住就阻塞到它
        // commit，之後對這些列重新求值 WHERE——此時 status 已經是 'published'，自然 0 筆命中，
        // 不會重複發布也不會拋例外，不需要額外的分散式鎖或 leader election。
        //
        // 🔴 刻意不改 published_at：這一欄代表「排定／實際生效的時間點」，維持原本排定的時間，
        // 不覆寫成「輪詢器真正跑到這一列的時間」——否則同一天排程的多篇文章，彼此的先後順序會
        // 被輪詢間隔的抖動打亂（ArticlesRepository.ListAsync 用 published_at DESC 排序），
        // 而且「10:00 設定發布」的使用者期待也會失真成「10:00 到 10:00+間隔之間某個不確定時刻」。
        const string sql = """
            UPDATE articles
            SET status = 'published',
                updated_at = SYSUTCDATETIME()
            OUTPUT inserted.club_id
            WHERE status = 'scheduled' AND published_at <= SYSUTCDATETIME();
            """;

        var affected = (await connection.QueryAsync<Guid?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).AsList();

        if (affected.Count == 0)
        {
            return 0;
        }

        logger.LogInformation(
            "排程發布：{Count} 篇文章的排定時間已到，狀態已由 scheduled 轉為 published。", affected.Count);

        // 有共用內容（club_id IS NULL）可能同時影響兩個俱樂部的公開頁面（ClubOrSharedSql：
        // 俱樂部專屬優先、沒有才回退共同內容）。這裡不逐一解析受影響的俱樂部代碼，直接對
        // 「目前所有啟用俱樂部」失效——俱樂部只有 2 個（docs/14-invariants.md），這個查詢與
        // 失效呼叫的成本可忽略，換來的是不用另外處理「OUTPUT 出來的 club_id 有 NULL 時要展開
        // 成哪些俱樂部代碼」這個額外分支，日後俱樂部數量變多才需要重新評估這個取捨。
        const string clubCodesSql = "SELECT code FROM clubs WHERE status = 'active'";
        var clubCodes = (await connection.QueryAsync<string>(
            new CommandDefinition(clubCodesSql, cancellationToken: cancellationToken))).AsList();

        foreach (var code in clubCodes)
        {
            await cache.InvalidateAsync(ArticlesListEntity, code, cancellationToken);
            await cache.InvalidateAsync(ArticleDetailEntity, code, cancellationToken);
        }

        return affected.Count;
    }
}
