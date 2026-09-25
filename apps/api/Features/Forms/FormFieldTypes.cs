namespace Tcrfc.Api.Features.Forms;

/// <summary>
/// <c>form_fields.field_type</c> 值域——逐字對應主站規劃書 G1（行 1159）「文字、下拉、多選、
/// 日期、檔案上傳、同意條款」六種欄位型別，比照 <c>db/club-schema.sql</c> 的
/// <c>CK_form_fields_field_type</c>（db 層是最後一道防線，這裡是應用層提早擋、給出中文訊息）。
/// **不多加規劃書沒要求的型別**（例如另開 <c>email</c>／<c>tel</c> 專屬型別）——格式驗證一律
/// 透過 <c>text</c> 型別搭配 <see cref="Text"/> 以外欄位的 <c>validation_rule</c>（正規表示式）達成。
/// </summary>
public static class FormFieldTypes
{
    public const string Text = "text";
    public const string Textarea = "textarea";
    public const string Select = "select";
    public const string Multiselect = "multiselect";
    public const string Date = "date";

    /// <summary>檔案上傳。🔴 **本輪未建立真正的檔案上傳通路**——公開送出端點把這個型別的答案當成
    /// 一般文字／URL 字串收下（例如履歷放雲端連結），不接受 <c>multipart/form-data</c> 的實體檔案。
    /// 全系統既有的 <c>IImageStorageService</c> 是「驗證格式→去 EXIF→縮圖→轉 WebP」的圖片專用管線
    /// （見 <c>Images/IImageStorageService.cs</c>），履歷等一般文件（PDF／Word）不是圖片、也不需要
    /// 縮圖，直接沿用會誤用圖片轉檔邏輯；建立一套獨立的通用檔案上傳服務（儲存體容器、型別與大小
    /// 驗證、防毒掃描與否）是獨立的基礎建設決定，不在「G1 表單設計器／G2 詢問收件匣」這次任務範圍，
    /// 見 apps/api/README.md「S1-10」段「規劃書沒寫清楚、本輪自行判斷的地方」。</summary>
    public const string File = "file";

    /// <summary>同意條款——一律以布林真值表示，公開送出時必須是 <c>"true"</c>／<c>"1"</c>／<c>"on"</c>
    /// 才視為已勾選（見 <c>Features/Forms/FormsRepository.cs</c> 的驗證邏輯）。</summary>
    public const string Consent = "consent";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        Text, Textarea, Select, Multiselect, Date, File, Consent,
    };

    /// <summary>需要 <c>options_json</c> 的型別——下拉／多選。</summary>
    public static readonly IReadOnlySet<string> RequiresOptions = new HashSet<string>(StringComparer.Ordinal)
    {
        Select, Multiselect,
    };

    private static readonly HashSet<string> TruthyValues = new(StringComparer.OrdinalIgnoreCase) { "true", "1", "on", "yes" };

    public static bool IsTruthy(string? value) => value is not null && TruthyValues.Contains(value.Trim());
}
