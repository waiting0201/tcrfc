namespace Tcrfc.Api.Features.Forms;

public sealed record PublicFormFieldDto
{
    public required string FieldKey { get; init; }
    public required string FieldType { get; init; }

    /// <summary>題目文字，依請求語系回傳（請求語系沒有翻譯時回退顯示中文，見
    /// <see cref="Localization.RequestLocale.Pick"/>）。S1-10 修正（2026-09-25）新增——原本公開
    /// 表單完全沒有題目可顯示，違反 CLAUDE.md 全域規定第 4 條。</summary>
    public required string Label { get; init; }

    public required bool IsRequired { get; init; }
    public string? ValidationRule { get; init; }

    /// <summary>下拉／多選的**送出值**——送出答案（<see cref="SubmitFormRequest.Answers"/>）必須
    /// 是這份清單裡的字面值之一，不因語系而變。</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>下拉／多選的**顯示文字**，與 <see cref="Options"/> 同順序、同筆數，依請求語系回傳
    /// （沒有翻譯時回退成跟 <see cref="Options"/> 一樣的中文字面值）。前台應該渲染這份清單給使用者看，
    /// 但送出時仍要送 <see cref="Options"/> 對應位置的值——<c>null</c>＝這個欄位沒有選項。</summary>
    public IReadOnlyList<string>? OptionLabels { get; init; }

    public required int SortOrder { get; init; }
}

/// <summary>公開表單定義——G1 表單設計器的公開讀取半邊，供前台動態產生表單欄位（S1-17，不在本次
/// 範圍）。**刻意不含 <c>notify_emails</c>／<c>redirect_path</c>**：前者是後台內部設定，後者只在
/// 送出成功後由前端自行處理跳轉，不需要在載入表單時先暴露完整導向網址。</summary>
public sealed record PublicFormDto
{
    public required string FormCode { get; init; }
    public required string FormNameZh { get; init; }
    public required string FormNameEn { get; init; }

    /// <summary>⚠️ **這個旗標本身沒有對應的伺服器端驗證**——全系統目前沒有串接任何 CAPTCHA
    /// 服務（Turnstile／reCAPTCHA），見 <c>FormsRepository.SubmitAsync</c> 檔頭「濫用防護」段。
    /// 前端讀到 <c>true</c> 時應該渲染 CAPTCHA 元件，但送出端點目前不會真的驗證 token。</summary>
    public required bool CaptchaEnabled { get; init; }

    public required IReadOnlyList<PublicFormFieldDto> Fields { get; init; }
}

/// <summary>公開送出。<see cref="Answers"/> 的鍵必須是這張表單目前存在的 <c>field_key</c>，
/// 多選欄位的值用逗號分隔（比照後台儲存格式）。<see cref="Website"/> 是誘捕欄位（honeypot）——
/// 正常訪客看不到這個欄位（前端應該用 CSS 隱藏），填了值即視為機器人，見
/// <c>FormsRepository.SubmitAsync</c>。</summary>
public sealed record SubmitFormRequest
{
    public required IReadOnlyDictionary<string, string> Answers { get; init; }
    public string? SourcePath { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }
    public string? Website { get; init; }
}

public sealed record SubmitFormResultDto
{
    public required bool Success { get; init; }
}
