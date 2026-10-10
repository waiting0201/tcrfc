namespace Tcrfc.Api.Features.MemberDraws;

/// <summary>主辦俱樂部（逐俱樂部顯示，App 規劃書 §3.10 v3.0）。</summary>
public sealed record MemberDrawClubDto
{
    public required string Code { get; init; }
    public string? Name { get; init; }
}

/// <summary>結果已公布時連往的那則最新消息（App 內不另建名單頁，只連往新聞）。</summary>
public sealed record MemberDrawAnnouncementDto
{
    /// <summary>新聞 slug；深連結 <c>tcrfc://news/{slug}</c>，官網 <c>/zh/news/{slug}</c>。</summary>
    public required string Slug { get; init; }
    public required string CategoryCode { get; init; }
}

/// <summary>
/// 會員可見的一場抽獎（App 規劃書 §3.10、Phase B「抽獎資訊 唯讀」，Android 缺口 B1）。
/// 🔴 <b>嚴格界線</b>：只含「活動公開欄位」與「<b>你自己</b>在這個俱樂部的資格布林」。<b>不含</b>序號、名單、合格人數、中獎人、備取、領獎狀態、名單雜湊、內部備註——
/// 這些全部不在這個 DTO 裡，也不讀 <c>draw_rosters</c>（資格由 <c>memberships</c> 的即時狀態推得，與名單快照無關）。
/// </summary>
public sealed record MemberDrawDto
{
    public required Guid Id { get; init; }

    /// <summary>活動代碼（如 <c>2026-OPEN</c>）。</summary>
    public required string DrawCode { get; init; }

    public required MemberDrawClubDto Club { get; init; }

    public string? Name { get; init; }

    /// <summary>獎品內容與名額（活動辦法文字）。</summary>
    public string? PrizeDescription { get; init; }

    /// <summary>活動辦法。App 規劃書 §3.10 要求辦法須載明「同時具備兩隊會籍者可分別參加兩隊抽獎」；文案由後台維護（法務確認 B-9），API 不自行補寫。</summary>
    public string? Rules { get; init; }

    public string? Notes { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而活動名稱沒有英文版時為 true，文字欄位是回退的繁中。</summary>
    public required bool IsFallbackLocale { get; init; }

    public string? CoverUrl { get; init; }

    /// <summary>封面寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有封面時三者皆 <c>null</c>。</summary>
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CoverAlt { get; init; }

    /// <summary>開獎場合：<c>home_match</c>／<c>livestream</c>／<c>other</c>；沒填為 null。</summary>
    public string? Occasion { get; init; }
    public string? OccasionLabel { get; init; }

    /// <summary>資格基準時間（名單凍結的時間點，UTC）。名單尚未鎖定（沒有基準時間）的活動不會出現在這個端點。</summary>
    public DateTime? SnapshotAt { get; init; }

    /// <summary>開獎時間（UTC）；尚未開獎為 null。App 不做任何開獎呈現（開獎在現場或直播）。</summary>
    public DateTime? DrawnAt { get; init; }

    /// <summary>領獎期限（台北日期）；沒填為 null。</summary>
    public DateOnly? ClaimDeadlineOn { get; init; }

    /// <summary><c>roster_locked</c>（名單已鎖定、尚未開獎）、<c>drawn</c>（已抽出、尚未公布）、<c>announced</c>（已公布）、<c>closed</c>（已結案）。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }

    /// <summary>
    /// <b>你的個人資格（布林，逐俱樂部）</b>：你在這個俱樂部的球迷會員會籍，在 <see cref="SnapshotAt"/> 當下是否有效（會員帳號啟用）。
    /// <c>true</c>＝「將自動列入／已列入」，<c>false</c>＝「會籍尚未涵蓋基準時間」。與「合格人數」無關，後者明確不提供。
    /// </summary>
    public required bool IsEligible { get; init; }

    /// <summary>結果已公布時才有（<see cref="Status"/> 為 announced／closed 且該新聞已發布）。</summary>
    public MemberDrawAnnouncementDto? Announcement { get; init; }
}
