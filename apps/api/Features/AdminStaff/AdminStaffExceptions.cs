namespace Tcrfc.Api.Features.AdminStaff;

public abstract class AdminStaffException(string message) : Exception(message);

public sealed class AdminStaffValidationException(string message) : AdminStaffException(message);

/// <summary>
/// <c>staff.club_id IS NULL</c>（兩隊共同的教練／團隊成員）透過俱樂部範圍端點寫入時一律擋下——
/// 形狀與理由逐字比照 <c>Features/AdminNews/SharedArticleReadOnlyException</c>：
/// docs/14-invariants.md「共同內容...只有超管能建立與修改」，本輪與既有 <c>articles</c> 的做法
/// 一致，都是「這個俱樂部範圍端點目前完全不提供編輯共同內容的路徑」（不是「超管走同一個端點可以」），
/// 對應 403。
/// </summary>
public sealed class SharedStaffReadOnlyException()
    : AdminStaffException("這是兩隊共用的教練／團隊成員資料，目前僅系統管理員可以編輯。");

/// <summary>圖片欄位插槽把「照片」對到 <c>staff.photo_key</c> 三態，形狀比照
/// <c>Features/AdminNews/CoverKeyUpdate.cs</c>。</summary>
public readonly record struct StaffPhotoKeyUpdate(bool Change, string? NewKey)
{
    public static readonly StaffPhotoKeyUpdate Keep = new(false, null);
    public static StaffPhotoKeyUpdate Set(string? newKey) => new(true, newKey);
}
