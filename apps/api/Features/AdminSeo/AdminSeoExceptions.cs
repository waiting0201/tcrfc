namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// H 搜尋與 AI 能見度（S1-12）後台寫入例外，集中由
/// <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 轉成 HTTP 狀態碼（跟既有
/// <c>Features/AdminNews</c>／<c>Features/AdminPages</c>／<c>Features/AdminFaqs</c> 同一套機制）。
/// </summary>
public abstract class AdminSeoException(string message) : Exception(message);

/// <summary>呼叫端輸入不合法（缺必填欄位、網址格式不對、CSV 表頭或欄位數不對……）。對應 400。</summary>
public sealed class AdminSeoValidationException(string message) : AdminSeoException(message);

/// <summary><c>UQ_redirects_club_path</c>（<c>club_id, from_path</c>）已被同俱樂部的其他轉址規則
/// 使用。對應 409。單筆建立時才會丟出——CSV 批次匯入走 upsert 語意，不會撞到這個例外，見
/// <see cref="AdminRedirectsRepository.ImportCsvAsync"/>。</summary>
public sealed class RedirectFromPathConflictException(string fromPath)
    : AdminSeoException($"來源網址「{fromPath}」已經有轉址設定，請直接編輯既有那一筆，或改用批次匯入整批覆寫。");
