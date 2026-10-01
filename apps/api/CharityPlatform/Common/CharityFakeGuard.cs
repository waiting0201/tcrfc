namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>
/// 假實作（金流、電子發票、寄信）的環境防線。協會的 LINE Pay 商店號與電子發票管道尚未申請（STATUS B-7），
/// 目前的實作都是「讓流程端到端跑得通」的本機假實作：
/// 假金流會對任何交易回「扣款成功」、假發票會編出看起來像發票號碼的字串——<b>在正式環境這等於憑空確認收款、
/// 憑空開立發票</b>，比起「功能不能用」嚴重得多。
///
/// 所以假實作只在 <c>Development</c> 環境（或明確設定 <see cref="AllowFakeConfigKey"/> = <c>true</c>，例如預備環境
/// 的整合驗收）才會運作；其餘環境一律丟「尚未設定」例外，捐款頁會顯示金流服務暫時無法使用，而不是假裝成功。
/// 正式實作到位後，DI 改註冊真實作，這個防線就不會再被走到。
/// </summary>
public static class CharityFakeGuard
{
    public const string AllowFakeConfigKey = "CHARITY_ALLOW_FAKE_PROVIDERS";

    public static bool IsAllowed(IHostEnvironment environment, IConfiguration configuration)
        => environment.IsDevelopment()
           || string.Equals(configuration[AllowFakeConfigKey], "true", StringComparison.OrdinalIgnoreCase);
}
