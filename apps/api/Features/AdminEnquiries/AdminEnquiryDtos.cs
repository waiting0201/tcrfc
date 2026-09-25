namespace Tcrfc.Api.Features.AdminEnquiries;

/// <summary>G2 詢問收件匣清單項目。<see cref="ApplicantName"/>／<see cref="ContactInfo"/> 取自
/// <c>field_key = "name"</c>／<c>"contact"</c> 兩個慣例欄位鍵（見
/// <c>AdminEnquiriesRepository</c> 檔頭「姓名／聯絡方式怎麼從動態欄位取出」的完整說明）——
/// 若該表單被後台改成沒有這兩個鍵，這裡就會是 <c>null</c>，不是錯誤。</summary>
public sealed record AdminEnquiryListItemDto
{
    public required Guid Id { get; init; }
    public required string FormCode { get; init; }
    public required string FormNameZh { get; init; }
    public string? ApplicantName { get; init; }
    public string? ContactInfo { get; init; }

    /// <summary>來源欄位由 G1 表單設計器標記（<c>form_fields.is_summary</c>），沒有任何欄位被標記
    /// 為摘要的表單（例如沒有敘述性文字欄位的 <c>camp_registration</c>）維持 <c>null</c>，
    /// 不是錯誤，見 <c>AdminEnquiriesRepository</c> 檔頭說明。</summary>
    public string? ContentSummary { get; init; }

    public string? SourcePath { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }
    public string? Status { get; init; }
    public Guid? AssigneeAdminUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminEnquiryAnswerDto
{
    public required string FieldKey { get; init; }
    public required string FieldType { get; init; }
    public string? Value { get; init; }
}

public sealed record AdminEnquiryDetailDto
{
    public required Guid Id { get; init; }
    public required string FormCode { get; init; }
    public required string FormNameZh { get; init; }
    public string? SourcePath { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }
    public string? Status { get; init; }
    public string? InternalNote { get; init; }
    public string? Tags { get; init; }
    public Guid? AssigneeAdminUserId { get; init; }
    public required IReadOnlyList<AdminEnquiryAnswerDto> Answers { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>後台只能改這四項——來源表單與逐筆回答內容是訪客送出的原始資料，後台不得竄改
/// （規劃書 G2：「指派負責人、內部備註、標籤」＋狀態管理）。</summary>
public sealed record UpdateAdminEnquiryRequest
{
    public required string Status { get; init; }
    public Guid? AssigneeAdminUserId { get; init; }
    public string? InternalNote { get; init; }
    public string? Tags { get; init; }
}

/// <summary>G2「指派負責人」姓名選單的候選人——S1-10 修正（2026-09-25）新增。**只回傳必要欄位**
/// （<see cref="Id"/>／<see cref="DisplayName"/>），不含 Email 或其他帳號資料，見
/// <c>AdminEnquiriesRepository.ListAssignableUsersAsync</c> 檔頭「為什麼不重用
/// AdminAccountListItemDto」的說明。</summary>
public sealed record AssignableAdminUserDto
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
}
