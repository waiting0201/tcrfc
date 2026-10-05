using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.MemberAuth;

/// <summary>
/// 註冊的年齡閘門與監護人同意（主站規劃書「會員資料安全要求」、App 規劃書 §4.5）。
/// 規則：生日必填；依台北當地日期算足歲，<b>未滿 18 歲</b>必須帶監護人同意（同意旗標為 true、監護人姓名、與當事人關係）才能註冊。
/// 成年註冊不儲存任何監護人資料。同意時間用伺服器時間。
/// 🔴 同意<b>文案</b>不是這裡的事（待法務 B-9）：這裡只驗證「有同意」並記錄文案版本號。
/// </summary>
public static class MemberAgeGate
{
    public const int AdultAge = 18;
    public const string RelationshipParent = "parent";
    public const string RelationshipLegalGuardian = "legal_guardian";

    /// <summary>通過閘門後要寫進 <c>members.guardian_*</c> 的值；成年為 <c>null</c>（四欄全空）。</summary>
    public sealed record GuardianRecord(DateTime ConsentedAtUtc, string GuardianName, string Relationship, string? ConsentVersion);

    /// <summary>以 <paramref name="today"/>（台北當地日期）計算的足歲。</summary>
    public static int AgeOn(DateOnly birthOn, DateOnly today)
    {
        var age = today.Year - birthOn.Year;
        return birthOn.AddYears(age) > today ? age - 1 : age;
    }

    public static bool IsMinor(DateOnly birthOn, DateOnly today) => AgeOn(birthOn, today) < AdultAge;

    /// <summary>
    /// 驗證生日必填並判斷是否需要監護人同意。通過回傳 <see cref="GuardianRecord"/>（未成年）或 <c>null</c>（成年）；不通過丟 400（專屬代碼）。
    /// 生日本身的範圍檢查（未來日期、超過 120 歲）由呼叫端既有的 <c>OptionalBirthOn</c> 先做。
    /// </summary>
    public static GuardianRecord? Evaluate(DateOnly? birthOn, MemberGuardianConsentRequest? consent, DateTime nowUtc)
    {
        if (birthOn is null)
        {
            throw new MemberValidationException("請填寫生日（註冊需要確認年齡）。", "birth_on_required");
        }

        if (!IsMinor(birthOn.Value, TaiwanClock.ToDate(nowUtc)))
        {
            return null; // 成年：不蒐集、不儲存監護人資料
        }

        if (consent is not { Consented: true })
        {
            throw new MemberValidationException("未滿 18 歲須經監護人同意才能註冊，請由監護人完成同意。", "guardian_consent_required");
        }

        var name = consent.GuardianName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 64)
        {
            throw new MemberValidationException("請填寫監護人姓名（64 字以內）。", "guardian_name_required");
        }

        if (consent.Relationship is not (RelationshipParent or RelationshipLegalGuardian))
        {
            throw new MemberValidationException("請選擇監護人與你的關係（父母或法定監護人）。", "invalid_guardian_relationship");
        }

        var version = consent.ConsentTextVersion?.Trim();
        if (string.IsNullOrEmpty(version))
        {
            version = null;
        }
        else if (version.Length > 32)
        {
            throw new MemberValidationException("同意文案版本不正確。", "invalid_guardian_consent_version");
        }

        return new GuardianRecord(nowUtc, name, consent.Relationship, version);
    }
}
