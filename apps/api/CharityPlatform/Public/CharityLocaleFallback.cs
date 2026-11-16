using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Public;

/// <summary>
/// 公開前台的語系回退判斷與公益團體／慈善計畫名稱的語系化（K-2、K-3、K-10）。
/// </summary>
internal static class CharityLocaleFallback
{
    /// <summary>
    /// 請求的語系是否缺少譯文（要出現語系提示）：譯文空白，或<b>與預設語系名稱相同且含中日韓字元</b>。
    /// 後者是後台只填了英文說明、沒填英文名稱時，系統以中文名稱頂替英文名稱列（<c>name</c> 是必填欄位）留下的痕跡——
    /// 英文頁若不當成缺譯文，就會顯示中文卻不出現提示（K-10）。兩邊都是不含中文的相同名稱（例如品牌名）不受影響。
    /// </summary>
    public static bool IsMissingTranslation(string dbLocale, string? requestedName, string? defaultName)
    {
        if (string.Equals(dbLocale, RequestLocale.DefaultDbLocale, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return true;
        }

        return string.Equals(requestedName.Trim(), defaultName?.Trim(), StringComparison.Ordinal) && ContainsCjk(requestedName);
    }

    private static bool ContainsCjk(string text)
        => text.Any(ch => ch is >= '㐀' and <= '鿿' or >= '豈' and <= '﫿' or >= '぀' and <= 'ヿ' or >= '가' and <= '힯');

    /// <summary>撥付對象與慈善計畫的顯示名稱（已依語系）與是否因缺英文而回退中文。</summary>
    public sealed record RefNames(string? CharityName, string? ProgramName, bool IsFallback);

    /// <summary>
    /// 中文：一律用項目建立當下的名稱快照。英文：用唯讀複本（<c>charity_refs</c>／<c>charity_program_refs</c>）依 <c>ref_code</c> 查到的英文名稱；
    /// 複本沒有（從未同步、或主站沒填英文）就回退快照（中文）並標記回退。🔴 憑證與信件上的名稱仍是快照，不走這裡。
    /// </summary>
    public static async Task<RefNames> ResolveRefNamesAsync(
        CharityDbContext db, string dbLocale,
        string? charityRefCode, string? charityNameSnapshot, string? programRefCode, string? programNameSnapshot,
        CancellationToken cancellationToken)
    {
        if (string.Equals(dbLocale, RequestLocale.DefaultDbLocale, StringComparison.Ordinal))
        {
            return new RefNames(charityNameSnapshot, programNameSnapshot, false);
        }

        string? charityName = charityNameSnapshot;
        string? programName = programNameSnapshot;
        var fallback = false;

        if (!string.IsNullOrWhiteSpace(charityNameSnapshot))
        {
            var en = charityRefCode is null ? null
                : await db.CharityRefs.AsNoTracking().Where(c => c.RefCode == charityRefCode).Select(c => c.NameEn).SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(en))
            {
                fallback = true;
            }
            else
            {
                charityName = en;
            }
        }

        if (!string.IsNullOrWhiteSpace(programNameSnapshot))
        {
            var en = programRefCode is null ? null
                : await db.CharityProgramRefs.AsNoTracking().Where(c => c.RefCode == programRefCode).Select(c => c.NameEn).SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(en))
            {
                fallback = true;
            }
            else
            {
                programName = en;
            }
        }

        return new RefNames(charityName, programName, fallback);
    }
}
