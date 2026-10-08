using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <c>--hash-password</c> 維運指令（正式庫第一個管理員，deploy/prod-db-init.sh create-admin）。
/// 純記憶體測試，不連資料庫。
/// </summary>
public sealed class PasswordHashCliTests
{
    [Fact]
    public void 合法密碼_stdout只有一行雜湊_且能通過登入用的Verify()
    {
        using var input = new StringReader("Correct-Horse-Battery-Staple-1\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = PasswordHashCli.Run(input, output, error);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error.ToString());
        var hash = output.ToString().TrimEnd('\r', '\n');
        Assert.StartsWith("$argon2id$v=19$", hash);
        Assert.DoesNotContain('\n', hash);
        // 與後台登入驗證同一份 PasswordHasher：維運指令產的雜湊必須真的登入得了。
        Assert.True(PasswordHasher.Verify("Correct-Horse-Battery-Staple-1", hash));
        // 密碼本身不得出現在任何輸出。
        Assert.DoesNotContain("Correct-Horse-Battery-Staple-1", output.ToString() + error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345")] // 5 字元：低於下限 6
    public void 空白或過短的密碼_拒絕並退出碼1_stdout沒有任何輸出(string password)
    {
        using var input = new StringReader(password + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = PasswordHashCli.Run(input, output, error);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, output.ToString());
        Assert.DoesNotContain(password.Length == 0 ? "\0never" : password, error.ToString());
    }

    [Fact]
    public void 剛好6字元的密碼_通過政策_產出雜湊()
    {
        using var input = new StringReader("123456\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, PasswordHashCli.Run(input, output, error));
        Assert.True(PasswordHasher.Verify("123456", output.ToString().TrimEnd('\r', '\n')));
    }

    [Fact]
    public void 標準輸入沒有資料_退出碼1()
    {
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(1, PasswordHashCli.Run(input, output, error));
        Assert.Equal(string.Empty, output.ToString());
    }
}
