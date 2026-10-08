namespace Tcrfc.Api.Security;

/// <summary>
/// 一次性維運指令：<c>dotnet Tcrfc.Api.dll --hash-password</c>——從標準輸入讀一行密碼，把
/// <see cref="PasswordHasher"/>（Argon2id）算出的雜湊印到標準輸出。
///
/// 為什麼存在：正式庫首次初始化（<c>deploy/prod-db-init.sh create-admin</c>，docs/20 §5）要建立
/// 第一個後台管理員，而後台「新增帳號」本身需要已登入的管理員（雞生蛋）。雜湊必須由與登入驗證
/// <b>同一份</b>程式（參數、格式、Base64 填充）產生，用別的語言／工具算的雜湊格式只要差一個
/// 填充字元，<see cref="PasswordHasher.Verify"/> 就會把它當成損毀而永遠登入不了。
///
/// 安全設計：
/// <list type="bullet">
/// <item>密碼<b>只走標準輸入</b>，不接受命令列參數（會出現在 <c>ps</c>／shell history）與環境變數。</item>
/// <item>不啟動 Web 主機、不讀任何設定、不連資料庫；stdout 只印雜湊一行，錯誤訊息走 stderr 且不含密碼。</item>
/// <item>套用與後台一致的密碼政策（<see cref="Features.AdminAuth.AdminAuthService.ValidatePasswordPolicy"/>）：
/// 至少 6 字元（<see cref="Features.AdminAuth.AdminAuthService.MinPasswordLength"/>）。帳號名稱不在此處理（呼叫端另行比對「密碼不得等於帳號」）。</item>
/// </list>
/// 退出碼：0 成功、1 密碼不合政策或未提供、2 內部錯誤。
/// </summary>
public static class PasswordHashCli
{
    public const string Flag = "--hash-password";

    public static int Run(TextReader input, TextWriter output, TextWriter error)
    {
        try
        {
            var password = input.ReadLine();
            if (string.IsNullOrEmpty(password))
            {
                error.WriteLine("未從標準輸入收到密碼。");
                return 1;
            }

            // 與後台一致的長度下限；username 傳 null 以外的值只為比對「密碼不得等於帳號」，這裡沒有帳號，
            // 傳一個不可能與密碼相等的字面值即可（呼叫端另行檢查）。
            Features.AdminAuth.AdminAuthService.ValidatePasswordPolicy(password, "\0");

            output.WriteLine(PasswordHasher.Hash(password));
            return 0;
        }
        catch (Features.AdminAuth.AdminAuthValidationException ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            // 只印例外型別，不印 Message／堆疊（理論上不含密碼，但維運指令寧可保守）。
            error.WriteLine($"雜湊失敗：{ex.GetType().Name}");
            return 2;
        }
    }
}
