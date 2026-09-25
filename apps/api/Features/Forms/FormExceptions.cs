namespace Tcrfc.Api.Features.Forms;

/// <summary>公開表單找不到（俱樂部沒有這個 <c>form_code</c>）——轉 404。</summary>
public sealed class PublicFormNotFoundException(string message) : Exception(message);

/// <summary>公開送出驗證失敗（必填欄位缺漏、值不在選項內、格式不符驗證規則等）——轉 400，
/// 比照既有 <c>ProgramRegistrationValidationException</c> 家族。</summary>
public sealed class PublicFormSubmissionValidationException(string message) : Exception(message);
