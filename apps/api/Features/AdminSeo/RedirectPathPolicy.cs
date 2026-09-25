namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 301 轉址網址格式驗證（S1-12，主站規劃書 §4.8 H「301 轉址管理」）。規劃書沒有給格式規則，
/// 本輪判斷：一律是**站內相對路徑**（以 <c>/</c> 開頭），不接受完整網址（<c>https://...</c>）——
/// 舊官網網域即將停用，轉址目標一律是本站內的新網址；來源網址雖然理論上可能是外部網域的舊路徑，
/// 但 <c>content/migration/舊官網URL盤點.csv</c> 盤點到的舊網址本身也全是可以取出路徑部分的
/// 一般網頁（不含 querystring 導向），因此**不支援含 host 的完整網址**這條限制對現有待遷移清單
/// 沒有影響，見 apps/api/README.md「S1-12」段。
/// </summary>
internal static class RedirectPathPolicy
{
    private const int MaxLength = 500;

    public static void Validate(string? path, string fieldLabel)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AdminSeoValidationException($"{fieldLabel}為必填欄位。");
        }

        if (!path.StartsWith('/'))
        {
            throw new AdminSeoValidationException($"{fieldLabel}必須以「/」開頭（僅支援站內相對路徑，不支援完整網址）。");
        }

        if (path.Length > MaxLength)
        {
            throw new AdminSeoValidationException($"{fieldLabel}長度不能超過 {MaxLength} 字元。");
        }

        if (path.Any(char.IsWhiteSpace))
        {
            throw new AdminSeoValidationException($"{fieldLabel}不能包含空白字元。");
        }
    }
}
