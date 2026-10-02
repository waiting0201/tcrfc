namespace Tcrfc.Api.Features.AdminDashboard;

/// <summary>
/// 儀表板首頁（規劃書 §4.1 A）。<b>每個區塊依呼叫者的權限決定有沒有</b>：沒有對應檢視權限的區塊為 <c>null</c>（或清單不含該項），
/// 不是回 0——「看不到」與「沒有資料」不能混為一談。所有數字都限定目前操作的俱樂部（含兩隊共同內容）。
/// </summary>
public sealed record AdminDashboardDto
{
    public required DateTime GeneratedAt { get; init; }

    /// <summary>待辦提醒（規劃書列的四項：未處理詢問、待審報名、即將截止的營隊、即將到期的贊助合約）；只含呼叫者有權限看的項目。</summary>
    public required IReadOnlyList<AdminDashboardTodoDto> Todos { get; init; }

    /// <summary>內容概況；新聞數字依新聞檢視權限、未翻譯內容數依各類別檢視權限（翻譯人員與語系管理者看全部類別）；一項都看不到時整個區塊為 <c>null</c>。</summary>
    public AdminDashboardContentDto? Content { get; init; }

    /// <summary>FAQ 概況；沒有 FAQ 檢視權限為 <c>null</c>。</summary>
    public AdminDashboardFaqDto? Faq { get; init; }

    /// <summary>未來 14 天的賽事／營隊／試訓／活動（含異常提醒）；只含呼叫者有權限看的來源。</summary>
    public required IReadOnlyList<AdminDashboardUpcomingItemDto> Upcoming { get; init; }

    /// <summary>會員概況；沒有會籍檢視權限為 <c>null</c>。</summary>
    public AdminDashboardMembersDto? Members { get; init; }

    /// <summary>快速入口（發布新聞、新增賽事、新增報名梯次、新增 FAQ、新增行事曆事件）；只含呼叫者有建立權限的項目。前端依 <c>code</c> 對應畫面路由。</summary>
    public required IReadOnlyList<AdminDashboardQuickEntryDto> QuickEntries { get; init; }
}

public sealed record AdminDashboardTodoDto
{
    /// <summary><c>enquiries_new</c>／<c>registrations_pending</c>／<c>sessions_closing_soon</c>／<c>sponsor_contracts_expiring</c>。</summary>
    public required string Code { get; init; }

    public required string Label { get; init; }

    public required int Count { get; init; }

    /// <summary>補充說明（例：「7 天內截止」「合約提醒日已到」），沒有為 <c>null</c>。</summary>
    public string? Hint { get; init; }

    /// <summary>最多 5 筆代表項目，供畫面直接點進去；只有「即將截止的營隊」與「即將到期的贊助合約」有。</summary>
    public required IReadOnlyList<AdminDashboardTodoItemDto> Items { get; init; }
}

public sealed record AdminDashboardTodoItemDto
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    /// <summary>截止日／到期日（台灣當地日期）。</summary>
    public DateOnly? Date { get; init; }
}

public sealed record AdminDashboardContentDto
{
    /// <summary>本月（台灣當地月份）已發布的文章數（含兩隊共同文章）；沒有新聞檢視權限（例如翻譯人員）為 <c>null</c>——看不到不等於 0。</summary>
    public int? PublishedThisMonth { get; init; }

    public int? DraftCount { get; init; }

    /// <summary>排程中尚未到發布時間的文章數。</summary>
    public int? ScheduledCount { get; init; }

    /// <summary>各非預設語系的未翻譯內容數（規劃書：英／日分列；目前只啟用英文）。只算呼叫者有權限看的內容類別；空陣列＝沒有可看的類別或沒有啟用的非預設語系。</summary>
    public required IReadOnlyList<AdminDashboardUntranslatedDto> Untranslated { get; init; }
}

public sealed record AdminDashboardUntranslatedDto
{
    public required string Locale { get; init; }

    public required string LocaleName { get; init; }

    /// <summary>已有繁中內容、但這個語系還沒有的筆數。</summary>
    public required int Count { get; init; }

    public required IReadOnlyList<AdminDashboardUntranslatedTypeDto> ByType { get; init; }
}

public sealed record AdminDashboardUntranslatedTypeDto
{
    public required string Type { get; init; }

    public required string TypeLabel { get; init; }

    public required int Count { get; init; }
}

public sealed record AdminDashboardFaqDto
{
    /// <summary>瀏覽次數 Top 10（已發布）。</summary>
    public required IReadOnlyList<AdminDashboardFaqItemDto> TopQuestions { get; init; }

    /// <summary>負評提醒：👎 數達 <see cref="AdminDashboardRepository.NegativeFeedbackMinCount"/> 以上且多於 👍 的題目（最多 10 題，👎 多的在前）。</summary>
    public required IReadOnlyList<AdminDashboardFaqItemDto> NegativeFeedback { get; init; }
}

public sealed record AdminDashboardFaqItemDto
{
    public required Guid Id { get; init; }

    public required string Question { get; init; }

    public required int ViewCount { get; init; }

    public required int HelpfulCount { get; init; }

    public required int UnhelpfulCount { get; init; }
}

public sealed record AdminDashboardUpcomingItemDto
{
    /// <summary><c>match</c>（賽事）／<c>session</c>（營隊梯次）／<c>trial</c>（試訓）／<c>event</c>（行事曆自建事件）／<c>fan_event</c>（球迷會活動）。</summary>
    public required string Source { get; init; }

    public required Guid Id { get; init; }

    /// <summary>台灣當地日期。</summary>
    public required DateOnly Date { get; init; }

    /// <summary>台灣當地時間 <c>HH:mm</c>；整天或沒有時間為 <c>null</c>。</summary>
    public string? Time { get; init; }

    public required string Title { get; init; }

    public string? TeamCode { get; init; }

    public string? VenueName { get; init; }

    /// <summary>異常提醒（日常中文）：尚未指派教練、名額未滿、資訊不完整。沒有異常為空陣列。</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record AdminDashboardMembersDto
{
    /// <summary>有效會籍數（本俱樂部，含免費與付費）。</summary>
    public required int ActiveMemberships { get; init; }

    /// <summary>有效的付費會籍數（球迷會員）。</summary>
    public required int ActivePaidMemberships { get; init; }

    /// <summary>30 天內到期的有效付費會籍數。</summary>
    public required int ExpiringIn30Days { get; init; }

    /// <summary>等待客服確認的升級申請數（會籍狀態「待確認」）。</summary>
    public required int PendingUpgrades { get; init; }
}

public sealed record AdminDashboardQuickEntryDto
{
    /// <summary><c>publish_news</c>／<c>add_match</c>／<c>add_session</c>／<c>add_faq</c>／<c>add_calendar_event</c>。</summary>
    public required string Code { get; init; }

    public required string Label { get; init; }
}

/// <summary>轉換概況（週／月趨勢）。各序列依權限：沒有權限的序列在 <see cref="Totals"/> 與每個 bucket 裡都是 <c>null</c>。</summary>
public sealed record AdminDashboardConversionDto
{
    /// <summary><c>week</c> 或 <c>month</c>。</summary>
    public required string Period { get; init; }

    /// <summary>趨勢涵蓋起訖（台灣當地日期，含頭含尾）。週：最近 8 週（含本週）；月：最近 6 個月（含本月）。</summary>
    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required AdminDashboardConversionBucketDto Totals { get; init; }

    /// <summary>由舊到新，沒有資料的 bucket 數字為 0。</summary>
    public required IReadOnlyList<AdminDashboardConversionBucketDto> Buckets { get; init; }

    /// <summary>各表單送出數（涵蓋期間合計）。</summary>
    public required IReadOnlyList<AdminDashboardFormCountDto> Forms { get; init; }
}

public sealed record AdminDashboardConversionBucketDto
{
    /// <summary>bucket 起日（週為週一）；<c>Totals</c> 為 <c>null</c>。</summary>
    public DateOnly? Start { get; init; }

    /// <summary>詢問／表單送出數（不含提案下載）。</summary>
    public int? Enquiries { get; init; }

    /// <summary>報名數（課程梯次＋試訓）。</summary>
    public int? Registrations { get; init; }

    /// <summary>提案下載數（提案下載表單的送出）。</summary>
    public int? ProposalDownloads { get; init; }

    /// <summary>新註冊會員數（在本俱樂部新建立會籍的人數）。</summary>
    public int? NewMembers { get; init; }

    /// <summary>新加入付費會籍（該會籍的第一筆付款）。</summary>
    public int? NewPaidMemberships { get; init; }

    /// <summary>續會（該會籍第二筆以後的付款）。</summary>
    public int? Renewals { get; init; }
}

public sealed record AdminDashboardFormCountDto
{
    public required string FormCode { get; init; }

    public required string FormName { get; init; }

    public required int Count { get; init; }
}

/// <summary>流量概況。<see cref="Configured"/> 為 false 時（GA4 尚未串接）<see cref="Overview"/> 為 <c>null</c>，<see cref="Message"/> 可直接顯示。</summary>
public sealed record AdminDashboardTrafficDto
{
    public required bool Configured { get; init; }

    public required string Message { get; init; }

    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public AnalyticsOverviewDto? Overview { get; init; }
}
