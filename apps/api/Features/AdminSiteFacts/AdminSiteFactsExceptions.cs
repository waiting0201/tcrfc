namespace Tcrfc.Api.Features.AdminSiteFacts;

/// <summary>
/// `I` 網站設定（S1-12d）後台寫入例外，集中由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/>
/// 轉成 HTTP 狀態碼（跟既有 <c>Features/AdminSeo</c> 等模組同一套機制）。
/// </summary>
public abstract class AdminSiteFactsException(string message) : Exception(message);

/// <summary>呼叫端輸入不合法（缺必填欄位、找不到指定的既有場地……）。對應 400。</summary>
public sealed class AdminSiteFactsValidationException(string message, string? field = null) : AdminSiteFactsException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}