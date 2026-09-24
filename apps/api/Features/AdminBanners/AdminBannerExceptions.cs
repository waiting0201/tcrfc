namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>B3 首頁輪播後台寫入例外，集中由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 轉成 HTTP 狀態碼。</summary>
public abstract class AdminBannerException(string message) : Exception(message);

/// <summary>呼叫端輸入不合法（缺圖片、上架時間早於下架時間……）。對應 400。</summary>
public sealed class AdminBannerValidationException(string message) : AdminBannerException(message);
