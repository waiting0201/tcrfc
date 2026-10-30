namespace Tcrfc.Api.Common;

/// <summary>梯次報名時窗（<c>sessions.signup_opens_at</c>／<c>signup_closes_at</c>，UTC）相對於某一時刻的位置。</summary>
public enum SignupWindowState
{
    /// <summary>在時窗內（或兩端皆未設定）。</summary>
    Within,
    /// <summary>現在早於報名開始時間。</summary>
    NotYetOpen,
    /// <summary>現在晚於報名截止時間。</summary>
    Closed,
}

/// <summary>
/// 報名時窗判斷的**唯一實作**：報名送出的閘門（<c>ProgramsRepository.SubmitRegistrationAsync</c>）與公開輸出的梯次狀態
/// （<see cref="EnrollmentStatus.OfSlotAt"/>）都呼叫這裡，避免「畫面說開放、送出卻被拒」（2026-10-30 Android 實機驗收發現）。
/// 邊界：恰等於開始／截止時刻視為仍在時窗內（與原閘門 <c>now &lt; opensAt</c>、<c>now &gt; closesAt</c> 才拒絕一致）。
/// </summary>
public static class SignupWindow
{
    public static SignupWindowState Evaluate(DateTime? opensAtUtc, DateTime? closesAtUtc, DateTime nowUtc)
    {
        if (closesAtUtc is DateTime closesAt && nowUtc > closesAt)
        {
            return SignupWindowState.Closed;
        }
        if (opensAtUtc is DateTime opensAt && nowUtc < opensAt)
        {
            return SignupWindowState.NotYetOpen;
        }
        return SignupWindowState.Within;
    }
}
