namespace Tcrfc.Api.Features.AdminHomeSections;

/// <summary>B3 首頁區塊編排的後台寫入例外，集中由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/>
/// 轉成 HTTP 狀態碼。</summary>
public abstract class AdminHomeSectionException(string message) : Exception(message);

/// <summary>呼叫端輸入不合法（非 Hero 區塊卻指定精選輪播、精選輪播不存在或不屬於這個俱樂部……）。對應 400。</summary>
public sealed class AdminHomeSectionValidationException(string message) : AdminHomeSectionException(message);
