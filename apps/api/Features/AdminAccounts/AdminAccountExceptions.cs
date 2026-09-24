namespace Tcrfc.Api.Features.AdminAccounts;

/// <summary>J1 帳號管理的例外，集中由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 轉狀態碼
/// （跟既有 <c>AdminArticleException</c> 同一套機制）。</summary>
public abstract class AdminAccountException(string message) : Exception(message);

/// <summary>輸入不合法（帳號名稱格式、密碼不符政策、俱樂部代碼不存在……）。對應 400。</summary>
public sealed class AdminAccountValidationException(string message) : AdminAccountException(message);

/// <summary><c>admin_users.username</c> 全域唯一已被使用。對應 409。</summary>
public sealed class AdminAccountUsernameConflictException(string username)
    : AdminAccountException($"帳號「{username}」已經被使用，請換一個。");

/// <summary>
/// 🔴 這個操作會讓系統歸零到「沒有任何啟用中的最高管理權限帳號」——
/// 停用最後一個超管帳號、把最後一個超管的最高權限拿掉、或撤銷會讓其歸零的操作皆屬此類。
/// 規劃書沒有明文這條防呆，這是執行層安全措施（見 apps/api/README.md 的說明）。對應 409。
/// </summary>
public sealed class AdminAccountLastSuperAdminException()
    : AdminAccountException("系統至少要保留一個啟用中的最高管理權限帳號，這個操作會讓系統歸零，已被擋下。");
