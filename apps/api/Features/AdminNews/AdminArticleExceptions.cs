namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 這一輪新增的後台寫入例外，全部由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/>
/// 集中轉成對應的 HTTP 狀態碼與可讀訊息（跟既有 <c>ClubNotFoundException</c> 同一套機制）。
/// 全部繼承 <see cref="AdminArticleException"/> 方便例外處理器用一個 pattern 涵蓋。
/// </summary>
public abstract class AdminArticleException(string message) : Exception(message);

/// <summary>呼叫端輸入不合法（缺必填欄位、分類代碼不存在、排程時間不在未來……）。對應 400。</summary>
public sealed class AdminArticleValidationException(string message) : AdminArticleException(message);

/// <summary><c>articles.slug</c> 全站唯一（<c>UQ_articles_slug</c>，不是複合鍵，見 README）已被其他文章使用。對應 409。</summary>
public sealed class ArticleSlugConflictException(string slug)
    : AdminArticleException($"網址名稱「{slug}」已經被使用，請換一個。");

/// <summary>
/// 樂觀並行衝突：呼叫端宣稱看到的 <c>updated_at</c> 跟資料庫目前的值對不起來，代表這段時間
/// 有其他人（或其他分頁）已經先寫入過。對應 409，⛔ 不做「後寫的贏」。
/// </summary>
public sealed class ArticleConcurrencyConflictException()
    : AdminArticleException("這篇文章已被其他人變更過，請重新整理後再試一次。");

/// <summary>
/// 共同內容（<c>club_id IS NULL</c>，兩隊共用）對受範圍限制的請求一律唯讀
/// （docs/14-invariants.md「共同內容對受範圍限制的帳號一律唯讀」）。目前沒有「超管」角色可以
/// 略過這條限制——J4／角色與權限系統還沒做，先完全擋下，不留後門。對應 403。
/// </summary>
public sealed class SharedArticleReadOnlyException()
    : AdminArticleException("這是兩隊共用的內容，目前僅系統管理員可以編輯。");

/// <summary>狀態轉換不合法（例如已發布的文章不能再排程）。對應 409。</summary>
public sealed class ArticleInvalidStatusTransitionException(string message) : AdminArticleException(message);

/// <summary>置頂精選同時最多 3 篇（B2 規格），超過對應 409。</summary>
public sealed class ArticleFeaturedLimitExceededException()
    : AdminArticleException("置頂精選最多同時 3 篇，請先取消其他文章的置頂再試一次。");
