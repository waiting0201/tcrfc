namespace Tcrfc.Api.Features.AdminSessions;

public abstract class AdminSessionException(string message) : Exception(message);

public sealed class AdminSessionValidationException(string message) : AdminSessionException(message);

/// <summary><c>ProgramId</c> 指向的課程項目不存在，或不屬於這個俱樂部——對應 400
/// （不是 404：梯次本身的路由沒有問題，是請求內容裡指定的關聯目標有問題，比照
/// <c>Features/AdminNews/AdminArticlesRepository</c> 對關聯目標的既有處理）。</summary>
public sealed class ProgramNotFoundForSessionException()
    : AdminSessionException("找不到這個俱樂部的課程／營隊項目，請確認課程項目是否存在。");
