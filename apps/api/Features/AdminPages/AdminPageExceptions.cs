namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// B1 頁面管理的例外，集中由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 轉成 HTTP 狀態碼，
/// 形狀比照 <c>Features/AdminNews/AdminArticleExceptions.cs</c>。
/// </summary>
public abstract class AdminPageException(string message) : Exception(message);

/// <summary>輸入不合法：網址名稱格式錯誤、區塊型別不支援、區塊內容缺必填欄位、排程時間不在未來……。對應 400。</summary>
public sealed class AdminPageValidationException(string message) : AdminPageException(message);

/// <summary><c>UQ_pages_club_slug (club_id, slug)</c> 已被同俱樂部的其他頁面使用。對應 409。</summary>
public sealed class PageSlugConflictException(string slug)
    : AdminPageException($"網址名稱「{slug}」在這個俱樂部底下已經被使用，請換一個。");

/// <summary>樂觀並行衝突：呼叫端宣稱看到的 <c>updated_at</c> 跟資料庫目前的值對不起來。對應 409，⛔ 不做「後寫的贏」。</summary>
public sealed class PageConcurrencyConflictException()
    : AdminPageException("這個頁面已被其他人變更過，請重新整理後再試一次。");

/// <summary>狀態轉換不合法（例如已發布的頁面不能再排程）。對應 409。</summary>
public sealed class PageInvalidStatusTransitionException(string message) : AdminPageException(message);

/// <summary>指定的版本編號不存在於這個頁面。對應 404（由 repository 回傳 <c>null</c>，端點自行轉換，
/// 這個例外只在「版本存在但不屬於這個頁面／俱樂部」等需要明確訊息的情境下使用）。</summary>
public sealed class PageVersionNotFoundException(int versionNo)
    : AdminPageException($"找不到版本編號 {versionNo}。");
