namespace Tcrfc.Api.Features.AdminEnquiries;

/// <summary>G2 詢問處理輸入驗證失敗——統一轉 400，比照既有 <c>AdminRegistrationValidationException</c> 家族。</summary>
public sealed class AdminEnquiryValidationException(string message) : Exception(message);
