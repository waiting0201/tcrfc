namespace Tcrfc.Api.Common;

/// <summary>E 批（2026-10-01，S2-11 會員前台）：公開的「會員」端點共用的例外，集中由 <see cref="ApiExceptionHandler"/> 轉成狀態碼。
/// 與 <c>AdminValidationException</c> 一族分開：那一族的訊息語氣是給後台人員看的，這一族的訊息是給<b>一般會員／訪客</b>看的日常中文。
/// <see cref="Code"/> 是給前端判斷流程用的機器可讀代碼（放在 ProblemDetails 的 <c>code</c> 擴充欄位），不是給人看的。</summary>
public interface ICodedApiException
{
    string Code { get; }
}

/// <summary>輸入有誤。對應 400。</summary>
public sealed class MemberValidationException(string message, string code = "validation_failed") : Exception(message), ICodedApiException
{
    public string Code { get; } = code;
}

/// <summary>沒有登入、權杖無效或帳號已停用。對應 401。</summary>
public sealed class MemberUnauthenticatedException(string message = "請先登入。", string code = "unauthenticated") : Exception(message), ICodedApiException
{
    public string Code { get; } = code;
}

/// <summary>已登入但不被允許（例如 Email 尚未驗證、這個俱樂部不提供此功能）。對應 403。</summary>
public sealed class MemberForbiddenException(string message, string code = "forbidden") : Exception(message), ICodedApiException
{
    public string Code { get; } = code;
}

/// <summary>找不到資料（含「不是你的」——一律 404，不洩漏存在與否）。對應 404。</summary>
public sealed class MemberNotFoundException(string message = "找不到資料。", string code = "not_found") : Exception(message), ICodedApiException
{
    public string Code { get; } = code;
}

/// <summary>狀態衝突（重複、已額滿、已有未完成的訂單…）。對應 409。<see cref="Title"/> 是短標題。</summary>
public sealed class MemberConflictException(string title, string message, string code = "conflict") : Exception(message), ICodedApiException
{
    public string Title { get; } = title;
    public string Code { get; } = code;
}

/// <summary>帳號因連續登入失敗暫時鎖定。對應 423。</summary>
public sealed class MemberAccountLockedException(DateTime lockedUntilUtc)
    : Exception("登入失敗次數過多，帳號暫時鎖定，請稍後再試，或使用「忘記密碼」重設。"), ICodedApiException
{
    public string Code => "account_locked";
    public DateTime LockedUntilUtc { get; } = lockedUntilUtc;
}

/// <summary>需要的外部服務或憑證尚未設定（LINE Login 憑證、金流…）。對應 503，訊息不含任何設定值。</summary>
public sealed class FeatureNotConfiguredException(string message, string code = "not_configured") : Exception(message), ICodedApiException
{
    public string Code { get; } = code;
}
