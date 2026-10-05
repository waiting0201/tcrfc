using System.Runtime.CompilerServices;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 純掃原始碼（不需要資料庫）：資料庫時間戳一律存 UTC，輸出給人看的「日期＋時間」文字（CSV、信件、訊息）必須明確是台灣時間
/// （<c>TaiwanClock.ToText</c>／<c>AddHours(8)</c>）或明寫「UTC」。直接 <c>x.CreatedAt.ToString("yyyy-MM-dd HH:mm")</c>
/// 會輸出無標示的 UTC，使用者以為是台灣時間而差 8 小時（B-5，docs/18 E-151）。
/// </summary>
public sealed class TimestampFormatTests
{
    private static string ApiDir([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    [Fact]
    public void 日期時間文字輸出必須是台灣時間或明寫UTC()
    {
        var offenders = new List<string>();
        var files = Directory.EnumerateFiles(ApiDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Tcrfc.Api.Tests{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var asksTime = line.Contains("ToString(\"yyyy-MM-dd HH:mm", StringComparison.Ordinal) || line.Contains(":yyyy-MM-dd HH:mm", StringComparison.Ordinal);
                if (asksTime && !line.Contains("AddHours(8)", StringComparison.Ordinal) && !line.Contains("Add(Offset)", StringComparison.Ordinal) && !line.Contains("UTC", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(ApiDir(), file)}:{i + 1}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "下列位置輸出無標示的 UTC 日期時間，請改用 TaiwanClock.ToText（台灣時間）或在文字中明寫 UTC：" + string.Join("、", offenders));
    }
}
