namespace Tcrfc.Api.Features.AdminForms;

/// <summary>G1 表單設計器輸入驗證失敗——統一轉 400，比照既有 <c>AdminProgramValidationException</c> 家族。</summary>
public sealed class AdminFormValidationException(string message) : Exception(message);

/// <summary>同一張表單內 <c>field_key</c> 重複——比照 <c>ArticleSlugConflictException</c> 轉 409。
/// 🔴 DB 層沒有 <c>UNIQUE (form_id, field_key)</c>（本輪判斷只在應用層擋，見
/// <c>AdminFormsRepository</c> 檔頭「規劃書沒寫清楚、自行判斷」），這裡是唯一防線。</summary>
public sealed class AdminFormFieldKeyConflictException(string message) : Exception(message);

/// <summary>刪除的欄位已有詢問資料引用（<c>enquiry_answers.form_field_id</c> FK 沒有
/// <c>ON DELETE CASCADE</c>，見 <c>db/club-schema.sql</c> 註解）——轉 409，不是 500。</summary>
public sealed class AdminFormFieldInUseException(string message) : Exception(message);
