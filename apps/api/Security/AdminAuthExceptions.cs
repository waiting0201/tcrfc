namespace Tcrfc.Api.Security;

/// <summary>未登入，或存取權杖缺漏／無效／過期。對應 401。
/// ⚠️ 訊息刻意不區分「權杖不存在」與「權杖已過期」與「權杖簽章錯誤」——
/// 對呼叫端而言處理方式都一樣（重新登入），區分反而洩露伺服器內部判斷細節。</summary>
public sealed class AdminUnauthenticatedException()
    : Exception("請先登入後台。");

/// <summary>已登入，但對這個俱樂部或這項操作沒有權限。對應 403。
/// 這是 docs/14-invariants.md「club_id 過濾不得依賴呼叫端」「資料範圍必須在資料存取層強制」
/// 在後台寫入路徑的落點——與既有的 ClubScope（公開唯讀端點）分屬不同機制，互不取代。</summary>
public sealed class AdminForbiddenException(string reason)
    : Exception(reason);
