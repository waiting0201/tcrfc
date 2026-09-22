namespace Tcrfc.Api.Security;

/// <summary>
/// 🔴 寫入端點的唯一總開關。這組後台寫入端點（<c>Features/AdminNews</c>）在接上登入與權限
/// 之前**不得在任何對外環境啟用**——目前完全沒有身分驗證，任何打得到這支 API 的人都能寫入。
///
/// 判斷式故意要求兩個條件同時成立，缺一都視為關閉：
/// 1. <c>ASPNETCORE_ENVIRONMENT=Development</c>——正式環境的 <c>docker-compose.yml</c>／
///    <c>docs/20-cicd.md</c> §7.2 一律把這個變數設為 <c>Production</c>，就算忘記設定下面那個
///    旗標，光是環境判斷這關就先擋住。
/// 2. <c>ENABLE_UNSAFE_DEV_WRITES=true</c>——刻意用「unsafe」命名，且要求明確設成字串
///    <c>"true"</c>（不是「有設定這個變數就算」），降低「複製一份 .env 忘記砍掉」被誤帶到
///    正式環境的機率。
///
/// 🔴 關閉時的行為是**路由完全不註冊**（見 <c>Program.cs</c> 只在這裡回傳 <c>true</c> 時才呼叫
/// <c>app.MapAdminNewsEndpoints()</c>），呼叫端拿到的是 404，不是 403——不透露「這裡本來有一組
/// 寫入端點」這件事。
/// </summary>
public static class DevWriteGate
{
    public const string EnableFlagKey = "ENABLE_UNSAFE_DEV_WRITES";

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
        => environment.IsDevelopment()
           && string.Equals(configuration[EnableFlagKey], "true", StringComparison.OrdinalIgnoreCase);
}
