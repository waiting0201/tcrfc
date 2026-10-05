using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Features.MembershipPayments;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>會籍付款訂單：建立（冪等鍵）→ 請款 → 確認 → 開通；內部開通端點；K2 客服開通結案同一份申請（App 規劃書 §5.3／§5.4／§9.7）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class MembershipOrderTests(AdminWriteApiFixture fixture) : IAsyncLifetime
{
    private const string Credential = "test-activate-credential-0123456789abcdef";

    private readonly MemberTestScope _scope = new(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _scope.DisposeAsync();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static HttpRequestMessage Create(string planCode, string? key, string club = "tcrfc", object? extra = null)
    {
        var body = extra ?? new { planCode };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{club}/member/membership-orders") { Content = BizTest.Json(body) };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return request;
    }

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private static Task<int> CountAsync(string sql, params (string, object?)[] p) => CountScalarAsync(sql, p);

    private static async Task<(Guid PlanId, DateOnly? EndsOn)> PlanOfOrderAsync(string orderNo)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT p.id, p.ends_on FROM membership_plans p JOIN membership_orders o ON o.membership_plan_id = p.id WHERE o.order_no = @N";
        command.Parameters.AddWithValue("@N", orderNo);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetGuid(0), reader.IsDBNull(1) ? null : DateOnly.FromDateTime(reader.GetDateTime(1)));
    }

    private static async Task<int> CountScalarAsync(string sql, (string Name, object? Value)[] parameters)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    // ═════════════ 建立與冪等 ═════════════

    [Fact]
    public async Task 建立訂單_金額由伺服器依方案計算_請求本文的金額被忽略_需要冪等鍵與驗證過的Email()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-create");
        using var anonymous = _scope.Client();

        // 未登入 401；沒有冪等鍵 400；方案不存在 404
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(Create("single", Key()))).StatusCode);
        var noKey = await m.Client.SendAsync(Create("single", null));
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal("idempotency_key_required", (await ReadJsonAsync(noKey)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.SendAsync(Create("single", "short"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await m.Client.SendAsync(Create("no-such-plan", Key()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await m.Client.SendAsync(Create("single", Key(), club: "nope"))).StatusCode);

        // 夾帶金額欄位 → 忽略，金額一律是方案費用 1200
        var response = await m.Client.SendAsync(Create("single", Key(), extra: new { planCode = "single", amount = 1, fee = 1 }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await ReadJsonAsync(response);
        Assert.Equal(1200, order.GetProperty("amount").GetInt32());
        Assert.Equal("created", order.GetProperty("status").GetString());
        Assert.Equal("待確認", order.GetProperty("statusLabel").GetString());
        Assert.StartsWith("MO", order.GetProperty("orderNo").GetString());
        Assert.Equal("tcrfc", order.GetProperty("clubCode").GetString());
        Assert.Equal("2026-27", order.GetProperty("seasonCode").GetString());
        Assert.True(order.GetProperty("canCancel").GetBoolean());
        Assert.Equal(JsonValueKind.Null, order.GetProperty("paymentUrl").ValueKind);
        // 收款主體恆為俱樂部、受益俱樂部是本次俱樂部
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders o JOIN clubs c ON c.id = o.collecting_club_id WHERE o.order_no = @N AND c.is_collecting_subject = 1", ("@N", order.GetProperty("orderNo").GetString())));

        // Email 尚未驗證 → 403（模擬：驗證後又被改成未驗證）
        await BizTest.ExecuteSqlAsync("UPDATE members SET email_verified_at = NULL WHERE email = @E", ("@E", m.Email));
        var unverified = await m.Client.SendAsync(Create("family", Key()));
        Assert.Equal(HttpStatusCode.Forbidden, unverified.StatusCode);
        Assert.Equal("email_not_verified", (await ReadJsonAsync(unverified)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task 冪等鍵_同一鍵同內容只成立一張_不同內容409_同方案不能同時有兩張未完成訂單()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-idem");
        var key = Key();
        var first = await m.Client.SendAsync(Create("single", key));
        var second = await m.Client.SendAsync(Create("single", key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // 重送回原訂單，不是新建
        Assert.Equal((await ReadJsonAsync(first)).GetProperty("orderNo").GetString(), (await ReadJsonAsync(second)).GetProperty("orderNo").GetString());
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders WHERE member_id = @M", ("@M", m.MemberId)));

        var reused = await m.Client.SendAsync(Create("family", key)); // 同一鍵、不同內容
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("idempotency_key_reused", (await ReadJsonAsync(reused)).GetProperty("code").GetString());

        var open = await m.Client.SendAsync(Create("single", Key())); // 新的鍵但已有未完成訂單
        Assert.Equal(HttpStatusCode.Conflict, open.StatusCode);
        Assert.Equal("open_order_exists", (await ReadJsonAsync(open)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Created, (await m.Client.SendAsync(Create("family", Key()))).StatusCode); // 不同方案不衝突

        // 並行同一個冪等鍵：只成立一張
        var racer = await _scope.CreateVerifiedMemberAsync("order-race");
        var raceKey = Key();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => racer.Client.SendAsync(Create("single", raceKey))));
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, r.StatusCode.ToString()));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders WHERE member_id = @M", ("@M", racer.MemberId)));
    }

    [Fact]
    public async Task 冪等鍵_高強度並行_每一輪都只回201或200且只成立一張_不得回open_order_exists_409()
    {
        // 回歸 E-172：同一鍵並行時，慢的請求在「查冪等鍵」之後、「查未完成訂單」之前，被快的請求插入訂單，
        // 於是把「自己這把鍵的那張訂單」當成別張未完成訂單而回 409。多輪、高並行以穩定抓到。
        for (var round = 0; round < 15; round++)
        {
            var racer = await _scope.CreateVerifiedMemberAsync($"order-race-{round}");
            var raceKey = Key();
            var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => racer.Client.SendAsync(Create("single", raceKey)))));
            var codes = results.Select(r => (int)r.StatusCode).ToList();
            Assert.True(results.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK), $"第 {round} 輪：{string.Join(",", codes)}");
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders WHERE member_id = @M", ("@M", racer.MemberId)));
        }
    }

    [Fact]
    public async Task 別人的訂單一律404_取消後可以重新建立()
    {
        var a = await _scope.CreateVerifiedMemberAsync("order-owner");
        var b = await _scope.CreateVerifiedMemberAsync("order-other");
        var orderNo = (await ReadJsonAsync(await a.Client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString();

        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.GetAsync($"/api/v1/member/membership-orders/{orderNo}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null)).StatusCode);
        Assert.Equal(0, (await ReadJsonAsync(await b.Client.GetAsync("/api/v1/member/membership-orders"))).GetArrayLength());

        var list = await ReadJsonAsync(await a.Client.GetAsync("/api/v1/member/membership-orders?lang=en"));
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Awaiting confirmation", list[0].GetProperty("statusLabel").GetString());

        var cancelled = await ReadJsonAsync(await a.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/cancel", null));
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await a.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/cancel", null)).StatusCode); // 冪等
        Assert.Equal(HttpStatusCode.Created, (await a.Client.SendAsync(Create("single", Key()))).StatusCode);
    }

    // ═════════════ 付款與開通 ═════════════

    [Fact]
    public async Task 付款流程_請款_確認_開通_會籍與付款紀錄與會員卡都建立_重複確認不重複開通()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-pay");
        var orderNo = (await ReadJsonAsync(await m.Client.SendAsync(Create("family", Key())))).GetProperty("orderNo").GetString()!;

        // 請款：待付款、有付款網址與 15 分鐘期限；重複請款回同一個網址（冪等）
        var pay = await ReadJsonAsync(await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null));
        Assert.Equal("pending_payment", pay.GetProperty("status").GetString());
        var url = pay.GetProperty("paymentUrl").GetString();
        Assert.Contains(orderNo, url);
        Assert.Equal("linepay", pay.GetProperty("paymentMethod").GetString());
        Assert.True(pay.GetProperty("expiresAt").GetDateTime() > DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(url, (await ReadJsonAsync(await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null))).GetProperty("paymentUrl").GetString());

        // 確認：交易識別不符 400；用戶端無法自己宣稱付款成功
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/confirm", BizTest.Json(new { transactionId = "FAKE-OTHER" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/confirm", BizTest.Json(new { transactionId = "" }))).StatusCode);
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M AND tier = 'fan_club'", ("@M", m.MemberId)));

        var confirmed = await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/confirm", BizTest.Json(new { transactionId = "FAKE-" + orderNo }));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var done = await ReadJsonAsync(confirmed);
        Assert.Equal("activated", done.GetProperty("status").GetString());
        Assert.False(done.GetProperty("paidAt").ValueKind == JsonValueKind.Null);
        Assert.False(done.GetProperty("activatedAt").ValueKind == JsonValueKind.Null);
        Assert.Equal(JsonValueKind.Null, done.GetProperty("paymentUrl").ValueKind);

        // 會籍：球迷會員、有效至球季末；付款紀錄記受益與收款主體與來源訂單；有會員卡；家庭方案發卡額度 3
        var membership = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"))).GetProperty("memberships")[0];
        Assert.Equal("fan_club", membership.GetProperty("tier").GetString());
        Assert.Equal("active", membership.GetProperty("status").GetString());
        Assert.Equal("2027-05-02", membership.GetProperty("endOn").GetString());
        Assert.Equal(3, membership.GetProperty("cardQuota").GetInt32());
        Assert.Equal(1, membership.GetProperty("cards").GetArrayLength());
        Assert.Equal(1, await CountAsync("""
            SELECT COUNT(*) FROM membership_payments p JOIN membership_orders o ON o.id = p.membership_order_id
            WHERE o.order_no = @N AND p.amount = 3000 AND p.method = 'linepay' AND p.club_id = o.club_id AND p.collecting_club_id = o.collecting_club_id
            """, ("@N", orderNo)));
        Assert.NotNull(MemberTestDoubles.Email.LastTo(m.Email, "membership_activated")); // 會籍開通確認信

        // 重複確認：回同一張已開通訂單，不重複延長、不重複寫付款紀錄
        Assert.Equal(HttpStatusCode.OK, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/confirm", BizTest.Json(new { transactionId = "FAKE-" + orderNo }))).StatusCode);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_payments p JOIN membership_orders o ON o.id = p.membership_order_id WHERE o.order_no = @N", ("@N", orderNo)));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M", ("@M", m.MemberId)));

        // 本球季已是球迷會員，不能再買
        var again = await m.Client.SendAsync(Create("single", Key()));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_fan_club", (await ReadJsonAsync(again)).GetProperty("code").GetString());
        // 已開通的訂單不能取消
        Assert.Equal(HttpStatusCode.Conflict, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task 免費會員升級_沿用同一份會籍列_不產生第二份()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-upgrade"); // 驗證後已有 registered 會籍
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M AND tier = 'registered'", ("@M", m.MemberId)));
        await MemberTestScope.BuyFanClubAsync(m, "single");
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M", ("@M", m.MemberId)));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M AND tier = 'fan_club' AND status = 'active'", ("@M", m.MemberId)));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM member_cards c JOIN memberships ms ON ms.id = c.membership_id WHERE ms.member_id = @M", ("@M", m.MemberId))); // 沿用既有那張卡
    }

    [Fact]
    public async Task 逾時與取消_待付款超過15分鐘讀到時換算為已逾時_不能再確認_也不能付款()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-expire");
        var orderNo = (await ReadJsonAsync(await m.Client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString()!;
        await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null);
        await BizTest.ExecuteSqlAsync("UPDATE membership_orders SET expires_at = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE order_no = @N", ("@N", orderNo));

        var confirm = await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/confirm", BizTest.Json(new { transactionId = "FAKE-" + orderNo }));
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("order_expired", (await ReadJsonAsync(confirm)).GetProperty("code").GetString());
        var read = await ReadJsonAsync(await m.Client.GetAsync($"/api/v1/member/membership-orders/{orderNo}"));
        Assert.Equal("expired", read.GetProperty("status").GetString());
        Assert.Equal("已逾時", read.GetProperty("statusLabel").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await m.Client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null)).StatusCode);
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M AND tier = 'fan_club'", ("@M", m.MemberId)));
        // 逾時的訂單不佔用「同方案一張未完成」的名額：可以重新建立
        Assert.Equal(HttpStatusCode.Created, (await m.Client.SendAsync(Create("single", Key()))).StatusCode);
    }

    [Fact]
    public async Task 金流尚未串接時_請款回503_訂單仍可建立_且不會被誤標成待付款()
    {
        using var factory = fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IPaymentGateway>();
            s.AddSingleton<IPaymentGateway, NotConfiguredPaymentGateway>();
        }));
        var m = await _scope.CreateVerifiedMemberAsync("order-nogateway");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = m.Client.DefaultRequestHeaders.Authorization;

        var created = await client.SendAsync(Create("single", Key()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = await ReadJsonAsync(created);
        Assert.False(order.GetProperty("canPayOnline").GetBoolean()); // 畫面據此隱藏「線上付款」，維持收款連結／現場收款＋客服開通
        var orderNo = order.GetProperty("orderNo").GetString()!;
        var pay = await client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, pay.StatusCode);
        Assert.Equal("payment_not_configured", (await ReadJsonAsync(pay)).GetProperty("code").GetString());
        Assert.Equal("created", (await ReadJsonAsync(await client.GetAsync($"/api/v1/member/membership-orders/{orderNo}"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task 金流方請款失敗時訂單退回已建立_使用者可重試()
    {
        using var factory = fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IPaymentGateway>();
            s.AddSingleton<IPaymentGateway, ThrowingGateway>();
        }));
        var m = await _scope.CreateVerifiedMemberAsync("order-gwfail");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = m.Client.DefaultRequestHeaders.Authorization;
        var orderNo = (await ReadJsonAsync(await client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString()!;
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync($"/api/v1/member/membership-orders/{orderNo}/pay", null)).StatusCode);
        Assert.Equal("created", (await ReadJsonAsync(await client.GetAsync($"/api/v1/member/membership-orders/{orderNo}"))).GetProperty("status").GetString());
    }

    private sealed class ThrowingGateway : IPaymentGateway
    {
        public string Method => "linepay";

        public bool IsConfigured => true;

        public Task<PaymentReservation> ReserveAsync(PaymentReserveRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("金流方暫時無法連線");

        public Task<PaymentConfirmation> ConfirmAsync(string transactionId, string orderNo, int amount, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    // ═════════════ 內部開通端點 ═════════════

    private WebApplicationFactory<Program> WithCredential(string? credential)
        => fixture.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["MEMBERSHIP_ACTIVATE_CREDENTIAL"] = credential })));

    private static HttpRequestMessage Activate(string orderNo, string? credential)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/membership/activate") { Content = BizTest.Json(new { orderNo }) };
        if (credential is not null)
        {
            request.Headers.Add("X-Internal-Credential", credential);
        }

        return request;
    }

    [Fact]
    public async Task 內部開通端點_憑證保護_未設定憑證時整個端點停用503()
    {
        var m = await _scope.CreateVerifiedMemberAsync("internal-cred");
        var orderNo = (await ReadJsonAsync(await m.Client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString()!;

        using var disabled = WithCredential(null);
        using var disabledClient = disabled.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await disabledClient.SendAsync(Activate(orderNo, Credential))).StatusCode);
        using var tooShort = WithCredential("short");
        using var tooShortClient = tooShort.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await tooShortClient.SendAsync(Activate(orderNo, "short"))).StatusCode); // 太短的憑證視同沒設

        using var enabled = WithCredential(Credential);
        using var client = enabled.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Activate(orderNo, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Activate(orderNo, Credential + "x"))).StatusCode);
        // 會員權杖不是內部憑證
        client.DefaultRequestHeaders.Authorization = m.Client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Activate(orderNo, null))).StatusCode);

        // 憑證正確但訂單還沒付款 → 409；不存在 → 404
        using var good = enabled.CreateClient();
        var notPaid = await good.SendAsync(Activate(orderNo, Credential));
        Assert.Equal(HttpStatusCode.Conflict, notPaid.StatusCode);
        Assert.Equal("order_not_paid", (await ReadJsonAsync(notPaid)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await good.SendAsync(Activate("MO000000-XXXXXX", Credential))).StatusCode);
    }

    [Fact]
    public async Task 內部開通端點_冪等_重複與並行呼叫只開通一次()
    {
        var m = await _scope.CreateVerifiedMemberAsync("internal-idem");
        var orderNo = (await ReadJsonAsync(await m.Client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString()!;
        await BizTest.ExecuteSqlAsync("UPDATE membership_orders SET status = 'paid', paid_at = SYSUTCDATETIME(), payment_method = 'linepay' WHERE order_no = @N", ("@N", orderNo));

        using var factory = WithCredential(Credential);
        using var client = factory.CreateClient();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.SendAsync(Activate(orderNo, Credential))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(results.Select(ReadJsonAsync));
        Assert.Equal(1, bodies.Count(b => !b.GetProperty("alreadyActivated").GetBoolean())); // 只有一個呼叫者真的做了開通
        Assert.All(bodies, b => Assert.Equal("activated", b.GetProperty("status").GetString()));

        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_payments p JOIN membership_orders o ON o.id = p.membership_order_id WHERE o.order_no = @N", ("@N", orderNo)));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M AND tier = 'fan_club'", ("@M", m.MemberId)));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders WHERE order_no = @N AND status = 'activated' AND activation_source = 'internal'", ("@N", orderNo)));

        var again = await ReadJsonAsync(await client.SendAsync(Activate(orderNo, Credential)));
        Assert.True(again.GetProperty("alreadyActivated").GetBoolean());
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_payments p JOIN membership_orders o ON o.id = p.membership_order_id WHERE o.order_no = @N", ("@N", orderNo)));
    }

    [Fact]
    public async Task 已付款但開通出錯_訂單標為開通失敗並回409_不靜默失敗()
    {
        var m = await _scope.CreateVerifiedMemberAsync("internal-fail");
        var orderNo = (await ReadJsonAsync(await m.Client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString()!;
        // 模擬方案期間已結束（付款後才發現）：開通必須失敗並留下可追蹤的狀態。
        // 不依賴方案原本的 ends_on（本機庫被手動改過、新建庫是種子值）：先快照原值，finally 原樣還原。
        var (planId, originalEndsOn) = await PlanOfOrderAsync(orderNo);
        await BizTest.ExecuteSqlAsync("UPDATE membership_orders SET status = 'paid', paid_at = SYSUTCDATETIME() WHERE order_no = @N", ("@N", orderNo));
        await BizTest.ExecuteSqlAsync("UPDATE membership_plans SET ends_on = '2020-01-01' WHERE id = @P", ("@P", planId));
        try
        {
            using var factory = WithCredential(Credential);
            using var client = factory.CreateClient();
            var response = await client.SendAsync(Activate(orderNo, Credential));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("activation_failed", (await ReadJsonAsync(response)).GetProperty("code").GetString());
            Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders WHERE order_no = @N AND status = 'activation_failed' AND failure_reason IS NOT NULL", ("@N", orderNo)));
            Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM memberships WHERE member_id = @M AND tier = 'fan_club'", ("@M", m.MemberId)));
            Assert.Equal("activation_failed", (await ReadJsonAsync(await m.Client.GetAsync($"/api/v1/member/membership-orders/{orderNo}"))).GetProperty("status").GetString());
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("UPDATE membership_plans SET ends_on = @E WHERE id = @P", ("@P", planId), ("@E", originalEndsOn.HasValue ? (object)originalEndsOn.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value));
        }
    }

    // ═════════════ 與後台 K2 的銜接 ═════════════

    [Fact]
    public async Task 客服在K2開通後_會員先前送出的升級申請一併結案_狀態顯示已開通()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-k2");
        var orderNo = (await ReadJsonAsync(await m.Client.SendAsync(Create("single", Key())))).GetProperty("orderNo").GetString()!;
        var memberships = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"))).GetProperty("memberships")[0];
        Assert.Equal("created", memberships.GetProperty("pendingOrder").GetProperty("status").GetString()); // 網頁顯示「待確認」

        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var planId = await B1Test.PlanIdAsync("tcrfc", "2026-27", "single");
        var activate = await admin.PostAsync("/api/v1/admin/tcrfc/memberships/activate", BizTest.Json(new
        {
            memberId = m.MemberId, planId, paymentMethod = "linepay", amount = 1200, paidOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        }));
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var order = await ReadJsonAsync(await m.Client.GetAsync($"/api/v1/member/membership-orders/{orderNo}"));
        Assert.Equal("activated", order.GetProperty("status").GetString());
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM membership_orders WHERE order_no = @N AND activation_source = 'admin' AND membership_id IS NOT NULL", ("@N", orderNo)));
        var after = (await ReadJsonAsync(await m.Client.GetAsync("/api/v1/member/memberships"))).GetProperty("memberships")[0];
        Assert.Equal("fan_club", after.GetProperty("tier").GetString());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("pendingOrder").ValueKind);
    }

    [Fact]
    public async Task 藍鯨方案_沒有可購買的球季時建立訂單被擋_訂單綁定的是藍鯨俱樂部()
    {
        var m = await _scope.CreateVerifiedMemberAsync("order-bw", club: "bw");
        // bw 的 2025 方案期間早已結束 → 方案已結束
        var response = await m.Client.SendAsync(Create("single", Key(), club: "bw"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("plan_closed", (await ReadJsonAsync(response)).GetProperty("code").GetString());
        // 磐石的方案代碼不能拿去藍鯨用（方案一律限定本俱樂部）
        Assert.Equal(HttpStatusCode.NotFound, (await m.Client.SendAsync(Create("family", Key(), club: "bw"))).StatusCode);
    }
}
