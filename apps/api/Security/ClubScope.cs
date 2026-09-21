namespace Tcrfc.Api.Security;

/// <summary>
/// 已驗證的俱樂部範圍。這是本 API「club_id 過濾不得依賴呼叫端」（docs/14-invariants.md、
/// 主站規劃書 §5.4）在程式碼層的落點。
///
/// 為什麼繞不過去：
/// 1. 建構子是 internal——只有本組件（<see cref="IClubResolver"/> 的實作）能建立實例，
///    端點與 repository 都拿不到「憑空組一個 Guid」的手段。
/// 2. 所有 repository 方法的簽章都要求 <see cref="ClubScope"/> 而不是 <c>Guid</c> 或
///    <c>string</c>——型別系統本身就擋掉「忘記驗證就查資料庫」這條路，不是靠 code review 記住。
/// 3. <see cref="ClubResolver"/> 是唯一能建立 <see cref="ClubScope"/> 的地方，而它一律先對
///    <c>clubs</c> 資料表做參數化查詢驗證 club 代碼真實存在、狀態為 <c>active</c>，查不到就丟例外，
///    端點永遠拿不到指向不存在俱樂部的範圍。
/// </summary>
public readonly struct ClubScope
{
    public Guid ClubId { get; }

    /// <summary>俱樂部代碼（如 "tcrfc"、"bw"），僅供記錄與回應標註用，不用於任何 SQL 組字串。</summary>
    public string ClubCode { get; }

    internal ClubScope(Guid clubId, string clubCode)
    {
        ClubId = clubId;
        ClubCode = clubCode;
    }
}
