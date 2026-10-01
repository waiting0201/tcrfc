namespace Tcrfc.Api.Features.Email;

/// <summary>
/// 會員系統信的中英文內容（規劃書 §3.14「Email 通知（僅五封，皆須中英模板）」）。本批做註冊驗證、密碼重設、會籍開通確認三封；
/// 到期前 30 天提醒與到期通知需要背景排程，不在本批（見 README E 批「只留介面」）。
/// 內容放在程式裡而不是 <c>email_templates</c> 資料表：該表目前沒有任何後台維護畫面與種子，且種子要求每個俱樂部各一份；
/// 日後要讓客服自行改文案時，改為「優先讀 email_templates、沒有才用這裡的預設」即可，呼叫端不用改。
/// 🔴 信件只放必要資訊，不放密碼、不放完整個資；連結的權杖是一次性或有時效的。
/// </summary>
public static class MemberEmailTemplates
{
    public static EmailMessage Verification(string to, string name, string clubName, string link, string lang)
        => lang == "en"
            ? new(to, $"[{clubName}] Please verify your email", $"Hi {name},\n\nThanks for joining {clubName}. Please verify your email address by opening the link below (valid for 24 hours):\n\n{link}\n\nIf you did not sign up, you can ignore this email.\n", "verify")
            : new(to, $"【{clubName}】請驗證你的 Email", $"{name} 你好，\n\n感謝加入{clubName}。請在 24 小時內點選下方連結完成 Email 驗證：\n\n{link}\n\n如果你沒有註冊過，請直接忽略這封信。\n", "verify");

    public static EmailMessage PasswordReset(string to, string name, string clubName, string link, string lang)
        => lang == "en"
            ? new(to, $"[{clubName}] Reset your password", $"Hi {name},\n\nWe received a request to reset your password. Open the link below within 1 hour to choose a new one:\n\n{link}\n\nIf you did not ask for this, you can ignore this email — your password stays the same.\n", "reset")
            : new(to, $"【{clubName}】重設密碼", $"{name} 你好，\n\n我們收到重設密碼的要求。請在 1 小時內點選下方連結設定新密碼：\n\n{link}\n\n如果不是你本人操作，請直接忽略這封信，原密碼不會改變。\n", "reset");

    public static EmailMessage MembershipActivated(string to, string name, string clubName, string planName, DateOnly? endOn, string lang)
        => lang == "en"
            ? new(to, $"[{clubName}] Your membership is active", $"Hi {name},\n\nYour {planName} membership with {clubName} is now active{(endOn is DateOnly endEn ? $" until {endEn:yyyy-MM-dd}" : string.Empty)}.\nYou can find your membership card in the member area.\n", "membership_activated")
            : new(to, $"【{clubName}】會籍已開通", $"{name} 你好，\n\n你的「{planName}」已開通{(endOn is DateOnly endZh ? $"，有效至 {endZh:yyyy-MM-dd}" : string.Empty)}。\n請到會員中心查看你的電子會員卡。\n", "membership_activated");
}
