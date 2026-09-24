namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// B4 常見問題（<c>faqs</c>／<c>faq_categories</c>）後台寫入例外，集中由
/// <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 轉成 HTTP 狀態碼（跟既有
/// <c>Features/AdminNews</c>／<c>Features/AdminPages</c> 同一套機制）。
/// </summary>
public abstract class AdminFaqException(string message) : Exception(message);

/// <summary>呼叫端輸入不合法（缺必填欄位、狀態值不合法、分類不存在……）。對應 400。</summary>
public sealed class AdminFaqValidationException(string message) : AdminFaqException(message);

/// <summary><c>UQ_faqs_club_slug</c>（<c>club_id, slug</c>）已被同俱樂部的其他題目使用。對應 409。</summary>
public sealed class FaqSlugConflictException(string slug)
    : AdminFaqException($"網址名稱「{slug}」已經被這個俱樂部的其他題目使用，請換一個。");

/// <summary>
/// 共同內容（<c>faqs.club_id IS NULL</c>，兩隊共用）對受範圍限制的請求一律唯讀
/// （docs/14-invariants.md「共同內容對受範圍限制的帳號一律唯讀」），比照
/// <c>Features/AdminNews/SharedArticleReadOnlyException</c>。對應 403。
/// </summary>
public sealed class SharedFaqReadOnlyException()
    : AdminFaqException("這是兩隊共用的常見問題，目前僅系統管理員可以編輯。");

/// <summary><c>UQ_faq_categories_slug</c> 已被其他分類使用。對應 409。</summary>
public sealed class FaqCategorySlugConflictException(string slug)
    : AdminFaqException($"分類網址名稱「{slug}」已經被使用，請換一個。");
