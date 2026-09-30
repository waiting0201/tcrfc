namespace Tcrfc.Api.Features.AdminDraws;

public sealed record AdminDrawLocaleContent
{
    public required string Name { get; init; }

    /// <summary>獎品內容與名額（逐項列出，一行一項）。</summary>
    public string? PrizeDescription { get; init; }

    /// <summary>活動辦法（<b>中文必填</b>：須載明獎品內容、名額、資格條件、基準時間、開獎時間與場合、領獎期限、主辦單位保留變更權利之範圍；
    /// 且須明示「同時具備兩隊會籍者可分別參加兩隊抽獎」）。</summary>
    public string? Rules { get; init; }

    /// <summary>注意事項。</summary>
    public string? Notes { get; init; }
}

public sealed record AdminDrawContentInput
{
    public required AdminDrawLocaleContent Zh { get; init; }
    public AdminDrawLocaleContent? En { get; init; }
}

public sealed record AdminDrawVersionDto
{
    public required int Version { get; init; }
    public required DateTime SnapshotAt { get; init; }
    public required int TotalCount { get; init; }
    public required string RosterHash { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public string? GeneratedByName { get; init; }

    /// <summary>作廢時間；目前有效的版本為 <c>null</c>。</summary>
    public DateTime? VoidedAt { get; init; }
    public string? VoidReason { get; init; }
    public required bool IsCurrent { get; init; }
}

public sealed record AdminDrawListItemDto
{
    public required Guid Id { get; init; }
    public required string DrawCode { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }

    /// <summary>資格基準時間（UTC）。</summary>
    public DateTime? SnapshotAt { get; init; }

    /// <summary>開獎時間（UTC）。</summary>
    public DateTime? DrawnAt { get; init; }
    public string? DrawOccasion { get; init; }
    public string? DrawOccasionLabel { get; init; }
    public DateOnly? ClaimDeadlineOn { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required int RosterVersion { get; init; }

    /// <summary>合格人數（尚未產生名單為 <c>null</c>）。</summary>
    public int? TotalCount { get; init; }
    public required int WinnerCount { get; init; }
    public required int BackupCount { get; init; }

    /// <summary>已完成發放的中獎人數（已寄出或已領取）。</summary>
    public required int FulfilledCount { get; init; }
    public Guid? AnnouncementArticleId { get; init; }

    /// <summary>公布文章的狀態（草稿／已發布／排程發布中）；沒有連結為 <c>null</c>。</summary>
    public string? AnnouncementStatus { get; init; }
    public string? AnnouncementStatusLabel { get; init; }
    public string? CreatedByName { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminDrawDetailDto
{
    public required Guid Id { get; init; }
    public required string DrawCode { get; init; }
    public DateTime? SnapshotAt { get; init; }
    public DateTime? DrawnAt { get; init; }
    public string? DrawOccasion { get; init; }
    public string? DrawOccasionLabel { get; init; }
    public DateOnly? ClaimDeadlineOn { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required int RosterVersion { get; init; }
    public int? TotalCount { get; init; }
    public string? RosterHash { get; init; }
    public DateTime? LockedAt { get; init; }
    public string? LockedByName { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public string? InternalNote { get; init; }
    public required AdminDrawLocaleContent Zh { get; init; }
    public AdminDrawLocaleContent? En { get; init; }
    public required int WinnerCount { get; init; }
    public required int BackupCount { get; init; }
    public required int FulfilledCount { get; init; }
    public Guid? AnnouncementArticleId { get; init; }
    public string? AnnouncementStatus { get; init; }
    public string? AnnouncementStatusLabel { get; init; }

    /// <summary>名單版本歷程（含已作廢的舊版，保留供稽核，不可刪除）。</summary>
    public required IReadOnlyList<AdminDrawVersionDto> Versions { get; init; }

    /// <summary>目前狀態可以做的動作（畫面依此顯示按鈕）：<c>edit</c>、<c>generate_roster</c>、<c>regenerate_roster</c>、<c>record_winners</c>、
    /// <c>announce</c>、<c>mark_announced</c>、<c>close</c>、<c>void</c>、<c>delete</c>。</summary>
    public required IReadOnlyList<string> AvailableActions { get; init; }
    public string? CreatedByName { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminDrawRequest
{
    /// <summary>活動代碼（英數字與連字號，≤ 32，全俱樂部唯一；會出現在匯出檔名）。新增時省略＝自動產生；更新時省略＝不變。</summary>
    public string? DrawCode { get; init; }

    /// <summary>資格基準時間（UTC 時間戳）。省略且有開獎時間時，預設為開獎日（台灣時間）當天 00:00。名單鎖定後不可變更。</summary>
    public DateTime? SnapshotAt { get; init; }
    public DateTime? DrawnAt { get; init; }

    /// <summary><c>home_match</c> 主場賽事日／<c>livestream</c> 直播／<c>other</c> 其他。</summary>
    public string? DrawOccasion { get; init; }

    /// <summary>領獎期限；超過期限尚未領取的獎品自動顯示為「逾期」。</summary>
    public DateOnly? ClaimDeadlineOn { get; init; }
    public string? InternalNote { get; init; }
    public bool RemoveCover { get; init; }
    public required AdminDrawContentInput Content { get; init; }
}

public sealed record AdminDrawNoticeDto
{
    /// <summary>會員條款與註冊同意事項是否已增列抽獎蒐集告知（「會籍有效期間將自動列入球迷會員抽獎合格名單；中獎時，姓名將以遮罩方式於最新消息公布」）。
    /// <b>未完成此項告知前不得舉辦抽獎</b>——尚未確認時不能產生合格名單。</summary>
    public required bool Confirmed { get; init; }
    public DateTime? ConfirmedAt { get; init; }
}

public sealed record UpdateAdminDrawNoticeRequest
{
    public required bool Confirmed { get; init; }
}

public sealed record AdminRosterPreviewDto
{
    /// <summary>試算的資格基準時間（UTC）。</summary>
    public required DateTime AsOf { get; init; }

    /// <summary>基準時間當下的合格人數。只是試算，不寫入資料、不配發序號。</summary>
    public required int EligibleCount { get; init; }
}

public sealed record GenerateAdminRosterRequest
{
    /// <summary>作廢重產時必填：說明為什麼要作廢目前這一版名單（舊版保留供稽核）。</summary>
    public string? VoidReason { get; init; }
}

public sealed record AdminRosterEntryDto
{
    public required int SerialNo { get; init; }
    public required string MemberNo { get; init; }

    /// <summary>姓名快照。沒有「解除遮罩」權限時為遮罩值（王○明）。</summary>
    public string? Name { get; init; }
    public string? Tier { get; init; }
    public string? TierLabel { get; init; }
    public DateOnly? MembershipEndOn { get; init; }
    public required bool IsWinner { get; init; }
    public required bool IsBackup { get; init; }
    public string? PrizeName { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record AdminWinnerInput
{
    public required int SerialNo { get; init; }

    /// <summary>獎項名稱。中獎必填；備取可省略。</summary>
    public string? PrizeName { get; init; }

    /// <summary><c>true</c>＝備取（原中獎人逾期未領時遞補）。把備取改成中獎（遞補）就是同一個序號再送一次 <c>isBackup: false</c>。</summary>
    public bool IsBackup { get; init; }
}

public sealed record RecordAdminWinnersRequest
{
    public required IReadOnlyList<AdminWinnerInput> Winners { get; init; }

    /// <summary>異動原因。名單<b>已公布</b>後再修改必填。</summary>
    public string? Reason { get; init; }
}

public sealed record RemoveAdminWinnersRequest
{
    public required IReadOnlyList<int> SerialNos { get; init; }
    public string? Reason { get; init; }
}

public sealed record AdminWinnerResultDto
{
    public required int UpdatedCount { get; init; }
    public required AdminDrawDetailDto Draw { get; init; }
}

public sealed record AdminFulfilmentDto
{
    public required int SerialNo { get; init; }
    public required string MemberNo { get; init; }
    public string? MemberName { get; init; }
    public required bool IsBackup { get; init; }
    public string? PrizeName { get; init; }

    /// <summary><c>ship</c> 寄送／<c>pickup</c> 現場領取。</summary>
    public string? ClaimMethod { get; init; }
    public string? ClaimMethodLabel { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientAddress { get; init; }

    /// <summary>資料庫狀態：<c>pending</c>／<c>shipped</c>／<c>claimed</c>。</summary>
    public string? FulfilmentStatus { get; init; }

    /// <summary>有效狀態：待處理且已過領獎期限時為 <c>overdue</c>（逾期）。</summary>
    public string? EffectiveStatus { get; init; }
    public string? EffectiveStatusLabel { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? ClaimedAt { get; init; }
    public string? Note { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record UpdateAdminFulfilmentRequest
{
    /// <summary><c>ship</c> 或 <c>pickup</c>。省略＝不變。</summary>
    public string? ClaimMethod { get; init; }

    /// <summary>收件資訊：省略＝不變。<b>修改需要「檢視會員完整個資」權限</b>（客服／行政與系統管理員）。</summary>
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientAddress { get; init; }

    /// <summary><c>pending</c>／<c>shipped</c>／<c>claimed</c>。省略＝不變。</summary>
    public string? Status { get; init; }

    /// <summary>備註：省略＝不變；空字串＝清除。</summary>
    public string? Note { get; init; }
}

public sealed record BatchAdminFulfilmentRequest
{
    public required IReadOnlyList<int> SerialNos { get; init; }
    public required string Status { get; init; }
}

public sealed record AdminBatchFulfilmentResultDto
{
    public required int UpdatedCount { get; init; }
    public required IReadOnlyList<AdminBatchFulfilmentSkippedDto> Skipped { get; init; }
}

public sealed record AdminBatchFulfilmentSkippedDto
{
    public required int SerialNo { get; init; }
    public required string Reason { get; init; }
}

public sealed record AdminAnnouncementWinnerDto
{
    public required int SerialNo { get; init; }
    public required string MemberNo { get; init; }
    public string? MaskedName { get; init; }
    public string? PrizeName { get; init; }
}

public sealed record AdminAnnouncementPreviewDto
{
    public required string DrawName { get; init; }
    public string? PrizeDescription { get; init; }
    public DateTime? SnapshotAt { get; init; }
    public required int EligibleCount { get; init; }

    /// <summary>遮罩後的中獎名單（抽獎序號＋會員編號＋姓名遮罩）。公布內容一律遮罩：不含手機、Email、地址、生日或完整姓名。</summary>
    public required IReadOnlyList<AdminAnnouncementWinnerDto> Winners { get; init; }
}

public sealed record AdminAnnouncementDraftDto
{
    public required Guid ArticleId { get; init; }
    public required string ArticleSlug { get; init; }
    public required AdminDrawDetailDto Draw { get; init; }
}

public sealed record LinkAdminAnnouncementRequest
{
    public required Guid ArticleId { get; init; }
}

public sealed record VoidAdminDrawRequest
{
    public required string Reason { get; init; }
}
