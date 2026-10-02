namespace Tcrfc.Api.Features.Trials;

/// <summary>公開的試訓場次（主站規劃書 §3.3 Trials：日期、地點、對象、名額、報名截止 ＋ 線上報名；後台 P4 維護）。
/// 只回傳尚未結束且日期未過的場次；欄位對應前台表格「日期／地點／對象／名額／報名截止／狀態」。</summary>
public sealed record PublicTrialDto
{
    public required Guid Id { get; init; }

    /// <summary>試訓日期（台灣當地日期，無時區）。</summary>
    public required DateOnly TrialOn { get; init; }

    /// <summary>主辦球隊代碼（<c>D1</c> 這類全站唯一代碼）與依語系解析的名稱；未指定球隊為 <c>null</c>。</summary>
    public string? TeamCode { get; init; }

    public string? TeamName { get; init; }

    /// <summary>對象說明（例：「U15 男足，2011–2012 年出生」），依語系解析、缺英文回退繁中。</summary>
    public string? Audience { get; init; }

    public Guid? VenueId { get; init; }

    public string? VenueName { get; init; }

    public string? VenueAddress { get; init; }

    public decimal? VenueLat { get; init; }

    public decimal? VenueLng { get; init; }

    /// <summary>名額上限；<c>null</c>＝不限。</summary>
    public int? Capacity { get; init; }

    public required int EnrolledCount { get; init; }

    /// <summary>報名截止日（含當日，台灣當地日期）；<c>null</c>＝到試訓當日前都可報名。</summary>
    public DateOnly? DeadlineOn { get; init; }

    /// <summary>狀態：<c>開放</c>／<c>額滿</c>／<c>候補</c>（已結束的場次不會出現在公開清單）。</summary>
    public required string Status { get; init; }

    /// <summary>true＝現在送出報名會直接佔名額（狀態「開放」且未過截止日）。</summary>
    public required bool IsSignupOpen { get; init; }

    /// <summary>true＝名額已滿但仍接受候補登記（狀態「額滿」或「候補」且未過截止日）；前台顯示「額滿候補」。</summary>
    public required bool AcceptsWaitlist { get; init; }
}

/// <summary>試訓線上報名送出。欄位與課程報名（<c>SubmitProgramRegistrationRequest</c>）一致，另外多了未成年的家長聯絡必填規則。</summary>
public sealed record SubmitTrialRegistrationRequest
{
    public string? ApplicantName { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public DateOnly? BirthOn { get; init; }

    /// <summary>家長／監護人姓名與電話；<see cref="BirthOn"/> 未滿 18 歲時兩者必填。</summary>
    public string? GuardianName { get; init; }

    public string? GuardianPhone { get; init; }

    public string? HealthDeclaration { get; init; }

    public string? Note { get; init; }
}

public sealed record TrialRegistrationSubmittedDto
{
    public required string RegistrationNo { get; init; }

    /// <summary><c>待確認</c>（已佔名額）或 <c>候補</c>（名額已滿，排入候補）。</summary>
    public required string Status { get; init; }
}
