namespace Tcrfc.Api.Features.AdminMatches;

/// <summary>
/// 進球類型值域（<c>match_goals.goal_type</c>，空值＝一般進球）。原本是自由文字，稽核 A-1 發現
/// 「烏龍球」會被自動彙總算成該球員的進球，所以後台寫入收斂為固定代碼；
/// 為了相容舊資料與後台舊畫面，寫入時也接受常見中文寫法並正規化成代碼。
/// </summary>
public static class MatchGoalTypes
{
    public const string Header = "header";
    public const string Penalty = "penalty";
    public const string FreeKick = "free_kick";
    public const string OwnGoal = "own_goal";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>([Header, Penalty, FreeKick, OwnGoal, Other], StringComparer.Ordinal);

    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["頭槌"] = Header, ["頭球"] = Header,
        ["點球"] = Penalty, ["十二碼"] = Penalty, ["罰球"] = Penalty, ["pk"] = Penalty,
        ["自由球"] = FreeKick, ["直接自由球"] = FreeKick, ["任意球"] = FreeKick, ["freekick"] = FreeKick, ["free-kick"] = FreeKick,
        ["烏龍球"] = OwnGoal, ["烏龍"] = OwnGoal, ["owngoal"] = OwnGoal, ["own-goal"] = OwnGoal, ["og"] = OwnGoal,
        ["其他"] = Other,
    };

    /// <summary>寫入用：空白回傳 null（一般進球）；代碼或同義詞回傳代碼；其餘回傳 false。</summary>
    public static bool TryNormalize(string? raw, out string? normalized)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            normalized = null;
            return true;
        }

        var lower = text.ToLowerInvariant();
        if (All.Contains(lower))
        {
            normalized = lower;
            return true;
        }

        if (Synonyms.TryGetValue(text, out var code))
        {
            normalized = code;
            return true;
        }

        normalized = null;
        return false;
    }

    /// <summary>讀取用：判斷是不是烏龍球。對尚未正規化的舊資料（自由文字，如「烏龍球」「own goal」）也要認得，
    /// 否則舊資料會繼續被算成進球。</summary>
    public static bool IsOwnGoal(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var t = stored.Trim();
        return t.Equals(OwnGoal, StringComparison.OrdinalIgnoreCase)
            || t.Contains("烏龍", StringComparison.Ordinal)
            || t.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Contains("owngoal", StringComparison.OrdinalIgnoreCase);
    }
}
