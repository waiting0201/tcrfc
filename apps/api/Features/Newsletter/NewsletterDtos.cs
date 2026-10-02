namespace Tcrfc.Api.Features.Newsletter;

/// <summary>頁尾電子報訂閱表單送出（主站規劃書 G-09）。</summary>
public sealed record SubscribeNewsletterRequest
{
    public string? Email { get; init; }

    /// <summary>是否勾選同意（個人資料蒐集與隱私權政策，G-07）。必須為 <c>true</c>，否則 400；同意時間記在 <c>subscribed_at</c>。</summary>
    public bool Consent { get; init; }

    /// <summary>訂閱入口代碼：<c>footer</c>（頁尾，預設）／<c>home</c>／<c>news</c>／<c>app</c>。
    /// 只收白名單代碼並換算成固定的中文來源標籤存入名單，<b>不接受自由文字</b>（避免名單的來源統計被灌入任意字串）。</summary>
    public string? Source { get; init; }

    /// <summary>蜜罐欄位：前台表單放一個人看不到的輸入框，真人不會填；有值就靜默丟棄（回成功，不讓機器人知道被擋）。</summary>
    public string? Website { get; init; }
}

/// <summary>
/// 訂閱結果。<b>刻意不透露</b>這個信箱先前是否已訂閱、是否曾經退訂（避免被拿來探測名單）——一律回 <c>ok</c>。
/// </summary>
public sealed record NewsletterSubscribeResultDto
{
    public string Status { get; init; } = "ok";
}

/// <summary>以退訂連結上的憑證退訂。憑證由 <see cref="NewsletterUnsubscribeTokens"/> 產生，只有名單擁有者知道。</summary>
public sealed record UnsubscribeNewsletterRequest
{
    public string? Token { get; init; }
}

public sealed record NewsletterUnsubscribeResultDto
{
    /// <summary>true＝這次呼叫真的把訂閱中改成已退訂；false＝本來就已退訂（冪等，仍視為成功）。</summary>
    public required bool Changed { get; init; }
}
