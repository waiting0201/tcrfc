using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.Forms;

/// <summary>公開表單找不到（俱樂部沒有這個 <c>form_code</c>）——轉 404。</summary>
public sealed class PublicFormNotFoundException(string message) : Exception(message);

/// <summary>公開送出驗證失敗（必填欄位缺漏、值不在選項內、格式不符驗證規則等）——轉 400，
/// 比照既有 <c>ProgramRegistrationValidationException</c> 家族。</summary>
public sealed class PublicFormSubmissionValidationException(string message) : Exception(message);

/// <summary>人機驗證（Cloudflare Turnstile）未通過或缺 token——轉 422，錯誤碼 <c>captcha_failed</c>。
/// 只有該表單 <c>captcha_enabled = true</c> 且部署端已設定 <c>TURNSTILE_SECRET_KEY</c> 時才會拋出。</summary>
public sealed class CaptchaFailedException(string message = "人機驗證未通過，請重新整理頁面後再試一次。") : Exception(message), ICodedApiException
{
    public string Code => "captcha_failed";
}
