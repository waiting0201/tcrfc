namespace Tcrfc.Api.Features.AdminEnquiries;

/// <summary>G2 詢問處理輸入驗證失敗——統一轉 400，比照既有 <c>AdminRegistrationValidationException</c> 家族。</summary>
public sealed class AdminEnquiryValidationException(string message, string? field = null) : Exception(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}