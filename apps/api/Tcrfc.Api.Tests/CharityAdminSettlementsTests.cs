using System.Net;
using System.Net.Http.Json;
using System.Text;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// N4 回饋金結算（規劃書 §6.4、§8）。重點：①只計 paid、用捐款當下的快照金額 ②店家與項目分開兩份 ③產生結算單冪等、並發安全
/// ④退款沖回三種情況（未結算排除／已結算未付款重出／已付款以負項計入下期並明列原因與原單號）⑤「已付款」登記與「執行結算」權限分離
/// ⑥已結算與已付款的結算單是帳務紀錄，鎖定不可刪改。
/// 測試資料的捐款付款日期放在 2025 年 6 月（遠在過去，與種子資料互不干擾），並以 payeeId 限定只結算測試建立的店家或項目。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminSettlementsTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Settlements = AdminBase + "/settlements";
    private static readonly DateOnly PeriodStart = new(2025, 6, 1);
    private static readonly DateOnly PeriodEnd = new(2025, 6, 30);

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private async Task<HttpClient> ClientAsync(params string[] roles) => fx.CreateClientFor(await fx.CreateAdminAsync(false, roles));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private static object RunBody(Guid? payeeId = null, string? payeeType = null, DateOnly? start = null, DateOnly? end = null)
        => new { periodStart = start ?? PeriodStart, periodEnd = end ?? PeriodEnd, payeeType, payeeId };

    private async Task<(Guid ProjectId, Guid StoreId)> NewPayeesAsync(decimal storePct = 10m, decimal projectPct = 5m)
    {
        var (projectId, _) = await CreateProjectAsync(fx, projectPct: projectPct);
        var (storeId, _) = await CreateStoreAsync(fx, storePct);
        return (projectId, storeId);
    }

    /// <summary>一筆 2025-06 的已付款捐款，分潤金額依快照百分比無條件捨去（與正式流程同一個公式）。</summary>
    private async Task<(Guid Id, string OrderNo)> DonateAsync(Guid projectId, Guid? storeId, int amount, int day = 10, decimal storePct = 10m, decimal projectPct = 5m)
    {
        var storeShare = storeId is null ? 0 : (int)Math.Floor(amount * storePct / 100m);
        var projectShare = (int)Math.Floor(amount * projectPct / 100m);
        var d = await SeedPaidDonationAsync(fx, projectId, storeId, amount, NoonUtc(2025, 6, day), storeShare, projectShare,
            storeId is null ? 0m : storePct, projectPct);
        return (d.Id, d.OrderNo);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 產生、冪等、並發
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 產生結算單_店家與項目各一份_只計已付款_金額用快照_捨去尾差()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);           // 店家 100、項目 50
        await DonateAsync(projectId, storeId, 333);            // 店家 33（捨去 33.3）、項目 16（捨去 16.65）
        await DonateAsync(projectId, null, 500);               // 無店家歸屬：只進項目結算（項目 25）
        await SeedPaidDonationAsync(fx, projectId, storeId, 800, NoonUtc(2025, 6, 11), 80, 40, 10m, 5m, status: "failed"); // 非 paid：不計
        using var sys = await SysAsync();

        var storeRun = await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions));
        var projectRun = await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(projectId, "project"), TestJson.WriteOptions));

        var store = Assert.Single(storeRun.Created);
        Assert.Equal("store", store.PayeeType);
        Assert.Equal("pending", store.Status);
        Assert.Equal(2, store.DonationCount);
        Assert.Equal(1333, store.DonationTotal);
        Assert.Equal(133, store.PayableAmount); // 100 + 33
        Assert.Equal(PeriodStart, store.PeriodStart);

        var project = Assert.Single(projectRun.Created);
        Assert.Equal("project", project.PayeeType);
        Assert.Equal(3, project.DonationCount);
        Assert.Equal(1833, project.DonationTotal);
        Assert.Equal(91, project.PayableAmount); // 50 + 16 + 25

        var detail = await ReadAsync<AdminSettlementDetailDto>(await sys.GetAsync($"{Settlements}/{project.Id}"));
        Assert.Equal(3, detail.Lines.Count);
        Assert.All(detail.Lines, l => { Assert.False(l.IsClawback); Assert.Equal(5m, l.SharePct); });
        Assert.Equal(91, detail.Lines.Sum(l => l.ShareAmount));
    }

    [Fact]
    public async Task 產生結算單_冪等_重跑不會重複結算同一筆捐款_期間重疊的對象被略過並說明原因()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);
        using var sys = await SysAsync();

        var first = await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions));
        Assert.Single(first.Created);

        var again = await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions));
        Assert.Empty(again.Created);
        Assert.Empty(again.Skipped); // 那筆捐款已在草稿裡，沒有任何新東西可結算

        // 草稿建立之後才新增一筆同期間的捐款：再按一次「產生」會因期間重疊被略過（要用「重算」把它納進草稿）。
        var late = await DonateAsync(projectId, storeId, 200, day: 20);
        var overlapping = await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions));
        Assert.Empty(overlapping.Created);
        var skip = Assert.Single(overlapping.Skipped);
        Assert.Equal(storeId, skip.PayeeId);
        Assert.Contains("已經有結算單", skip.Reason);

        var recalculated = await ReadAsync<AdminSettlementRecalculationDto>(
            await sys.PostAsync($"{Settlements}/{first.Created[0].Id}/recalculate", null));
        Assert.Equal(1, recalculated.AddedCount);
        Assert.Equal(2, recalculated.Detail.Settlement.DonationCount);
        Assert.Equal(120, recalculated.Detail.Settlement.PayableAmount); // 100 + 20
        Assert.Contains(recalculated.Detail.Lines, l => l.OrderNo == late.OrderNo);
    }

    [Fact]
    public async Task 並發產生結算單_四個請求同時送出_只會產生一份_沒有任何捐款被重複計入()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        for (var i = 0; i < 5; i++)
        {
            await DonateAsync(projectId, storeId, 100 * (i + 1), day: 5 + i);
        }

        using var sys = await SysAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions)));

        var created = 0;
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            created += (await response.Content.ReadFromJsonAsync<AdminSettlementRunResultDto>(TestJson.Options))!.Created.Count;
        }

        Assert.Equal(1, created);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM settlements WHERE payee_id = @p", ("@p", storeId)));
        Assert.Equal(5, await fx.ScalarAsync<int>(
            "SELECT COUNT(*) FROM settlement_lines l JOIN settlements s ON s.id = l.settlement_id WHERE s.payee_id = @p", ("@p", storeId)));
    }

    [Fact]
    public async Task 產生結算單_期間必須已結束_日期顛倒與格式錯誤回400()
    {
        using var sys = await SysAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

        var notEnded = await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(start: today.AddDays(-3), end: today), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notEnded.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(start: PeriodEnd, end: PeriodStart), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(payeeType: "bogus"), TestJson.WriteOptions)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 退款沖回（規劃書 §8.5 三種情況）
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 退款沖回_未結算_草稿確認結算時直接排除已退款的捐款_不產生回饋金()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        var keep = await DonateAsync(projectId, storeId, 1000);
        var refunded = await DonateAsync(projectId, storeId, 500);
        using var sys = await SysAsync();
        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();
        Assert.Equal(150, draft.PayableAmount);

        await RefundAsync(fx, refunded.Id);
        var settled = await ReadAsync<AdminSettlementDetailDto>(await sys.PostAsync($"{Settlements}/{draft.Id}/settle", null));

        Assert.Equal("settled", settled.Settlement.Status);
        Assert.Equal(100, settled.Settlement.PayableAmount);
        var line = Assert.Single(settled.Lines);
        Assert.Equal(keep.OrderNo, line.OrderNo);
    }

    [Fact]
    public async Task 退款沖回_已結算未付款_重算後從該期對帳單扣除_並列出被扣除的單號()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);
        var refunded = await DonateAsync(projectId, storeId, 500);
        using var sys = await SysAsync();
        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();
        await ReadAsync<AdminSettlementDetailDto>(await sys.PostAsync($"{Settlements}/{draft.Id}/settle", null));
        Assert.Equal(150, (await ReadAsync<AdminSettlementDetailDto>(await sys.GetAsync($"{Settlements}/{draft.Id}"))).Settlement.PayableAmount);

        await RefundAsync(fx, refunded.Id);
        var result = await ReadAsync<AdminSettlementRecalculationDto>(await sys.PostAsync($"{Settlements}/{draft.Id}/recalculate", null));

        Assert.Equal([refunded.OrderNo], result.RemovedOrderNos);
        Assert.Equal("settled", result.Detail.Settlement.Status); // 仍是已結算（重出對帳單）
        Assert.Equal(100, result.Detail.Settlement.PayableAmount);
        Assert.Equal(1, result.Detail.Settlement.DonationCount);
        Assert.Equal(1000, result.Detail.Settlement.DonationTotal);
        Assert.Equal(0, result.AddedCount); // 已結算的對帳單只扣除、不新增
    }

    [Fact]
    public async Task 退款沖回_已結算且已付款_不追討_以負項計入下一期_明列原因與原單號_且只沖回一次()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        var refunded = await DonateAsync(projectId, storeId, 1000);                       // 6/10：店家 100
        await DonateAsync(projectId, storeId, 400, day: 28);                              // 6/28：店家 40
        using var sys = await SysAsync();
        var june = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();
        await sys.PostAsync($"{Settlements}/{june.Id}/settle", null);
        var paid = await ReadAsync<AdminSettlementDetailDto>(await sys.PostAsJsonAsync($"{Settlements}/{june.Id}/mark-paid",
            new { remittedOn = new DateOnly(2025, 7, 5), remitMethod = "銀行轉帳", remitNote = "測試匯款" }, TestJson.WriteOptions));
        Assert.Equal("paid", paid.Settlement.Status);
        Assert.Equal(140, paid.Settlement.PayableAmount);

        // 付款之後，6/10 那筆被退款；七月又有一筆新捐款（七月結算單才會一起處理）。
        await RefundAsync(fx, refunded.Id, "誤按金額");
        var julyDonation = await SeedPaidDonationAsync(fx, projectId, storeId, 600, NoonUtc(2025, 7, 8), 60, 30, 10m, 5m);
        var july = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run",
            RunBody(storeId, "store", new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31)), TestJson.WriteOptions))).Created.Single();

        Assert.Equal(1, july.ClawbackCount);
        Assert.Equal(-100, july.ClawbackAmount);
        Assert.Equal(-40, july.PayableAmount); // 60 + (-100)
        var detail = await ReadAsync<AdminSettlementDetailDto>(await sys.GetAsync($"{Settlements}/{july.Id}"));
        var clawback = Assert.Single(detail.Lines, l => l.IsClawback);
        Assert.Equal(refunded.OrderNo, clawback.OrderNo);
        Assert.Equal(-100, clawback.ShareAmount);
        Assert.Contains(refunded.OrderNo, clawback.ClawbackReason);
        Assert.Contains("誤按金額", clawback.ClawbackReason);
        Assert.Contains(detail.Lines, l => !l.IsClawback && l.OrderNo == julyDonation.OrderNo);

        // 已付款的六月結算單完全沒被改動（不追討、不重算已付款期間）。
        var juneAfter = await ReadAsync<AdminSettlementDetailDto>(await sys.GetAsync($"{Settlements}/{june.Id}"));
        Assert.Equal(140, juneAfter.Settlement.PayableAmount);
        Assert.Equal(2, juneAfter.Lines.Count);

        // 同一筆退款不會被沖回第二次：八月再結算，沒有新的負項。
        await SeedPaidDonationAsync(fx, projectId, storeId, 100, NoonUtc(2025, 8, 3), 10, 5, 10m, 5m);
        var august = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run",
            RunBody(storeId, "store", new DateOnly(2025, 8, 1), new DateOnly(2025, 8, 31)), TestJson.WriteOptions))).Created.Single();
        Assert.Equal(0, august.ClawbackCount);
        Assert.Equal(10, august.PayableAmount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 狀態機、鎖定、權限分離
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 狀態機_待結算到已結算到已付款_已付款只登記日期方式備註_鎖定後不可刪除或重算_每次異動有稽核()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);
        var admin = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(admin);
        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();

        // 待結算不能直接登記付款
        var early = await sys.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid", new { remittedOn = new DateOnly(2025, 7, 5), remitMethod = "銀行轉帳" }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        var settled = await ReadAsync<AdminSettlementDetailDto>(await sys.PostAsync($"{Settlements}/{draft.Id}/settle", null));
        Assert.Equal("settled", settled.Settlement.Status);
        Assert.NotNull(settled.Settlement.SettledAt);
        Assert.NotNull(settled.Settlement.SettledByName);
        Assert.Equal(HttpStatusCode.Conflict, (await sys.PostAsync($"{Settlements}/{draft.Id}/settle", null)).StatusCode); // 不可重複確認

        // 匯款方式必填、日期不可是未來
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid", new { remittedOn = new DateOnly(2025, 7, 5) }, TestJson.WriteOptions)).StatusCode);
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)).AddDays(3);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid", new { remittedOn = future, remitMethod = "銀行轉帳" }, TestJson.WriteOptions)).StatusCode);

        var paid = await ReadAsync<AdminSettlementDetailDto>(await sys.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid",
            new { remittedOn = new DateOnly(2025, 7, 5), remitMethod = "銀行轉帳", remitNote = "第三季回饋" }, TestJson.WriteOptions));
        Assert.Equal("paid", paid.Settlement.Status);
        Assert.Equal(new DateOnly(2025, 7, 5), paid.Settlement.RemittedOn);
        Assert.Equal("銀行轉帳", paid.Settlement.RemitMethod);
        Assert.Equal("第三季回饋", paid.Settlement.RemitNote);
        Assert.NotNull(paid.Settlement.PaidRegisteredAt);

        // 鎖定：不可再付款、不可重算、不可刪除
        Assert.Equal(HttpStatusCode.Conflict, (await sys.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid", new { remittedOn = new DateOnly(2025, 7, 6), remitMethod = "現金" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await sys.PostAsync($"{Settlements}/{draft.Id}/recalculate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await sys.DeleteAsync($"{Settlements}/{draft.Id}")).StatusCode);

        // 每次異動都寫稽核（經辦人＋時間）
        var actions = await fx.ScalarAsync<int>(
            "SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND target_id = @t AND action IN (N'settlement.run', N'settlement.settle', N'settlement.mark_paid')",
            ("@a", admin.Id), ("@t", draft.Id));
        Assert.Equal(2, actions); // settle＋mark_paid 以結算單為對象；run 的對象是整批（target_id 為空）
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'settlement.run'", ("@a", admin.Id)));
    }

    [Fact]
    public async Task 草稿可刪除_刪除後捐款回到可結算狀態()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);
        using var sys = await SysAsync();
        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();

        Assert.Equal(HttpStatusCode.NoContent, (await sys.DeleteAsync($"{Settlements}/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.GetAsync($"{Settlements}/{draft.Id}")).StatusCode);
        Assert.Single((await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created);
    }

    [Fact]
    public async Task 已付款登記與執行結算權限分離_只有執行權限的人不能登記付款_只有登記權限的人不能執行結算()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);
        var executorRole = await fx.CreateRoleAsync("n4.settlement.view", "n4.settlement.execute");
        var payerRole = await fx.CreateRoleAsync("n4.settlement.view", "n4.settlement.mark_paid");
        using var executor = await ClientAsync(executorRole);
        using var payer = await ClientAsync(payerRole);

        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await executor.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();
        Assert.Equal(HttpStatusCode.Forbidden, (await payer.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await payer.PostAsync($"{Settlements}/{draft.Id}/settle", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await executor.PostAsync($"{Settlements}/{draft.Id}/settle", null)).StatusCode);
        var body = new { remittedOn = new DateOnly(2025, 7, 5), remitMethod = "銀行轉帳" };
        Assert.Equal(HttpStatusCode.Forbidden, (await executor.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid", body, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await payer.PostAsJsonAsync($"{Settlements}/{draft.Id}/mark-paid", body, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 授權矩陣_檢視者只能看_客服與商務沒有N4權限_匯出需要匯出權限()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        await DonateAsync(projectId, storeId, 1000);
        using var sys = await SysAsync();
        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();
        using var viewer = await ClientAsync("viewer");
        using var cs = await ClientAsync("customer_service_admin");
        using var biz = await ClientAsync("business_sponsorship");

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Settlements)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{Settlements}/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync($"{Settlements}/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"{Settlements}/{draft.Id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cs.GetAsync(Settlements)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync(Settlements)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().GetAsync(Settlements)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 對帳單 CSV
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 對帳單CSV_含期間筆數總額分潤率與應付_沖回負項註明原因_不含任何捐款人資料()
    {
        var (projectId, storeId) = await NewPayeesAsync();
        var d1 = await DonateAsync(projectId, storeId, 1000);
        using var sys = await SysAsync();
        var draft = (await ReadAsync<AdminSettlementRunResultDto>(await sys.PostAsJsonAsync($"{Settlements}/run", RunBody(storeId, "store"), TestJson.WriteOptions))).Created.Single();

        var response = await sys.GetAsync($"{Settlements}/{draft.Id}/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // UTF-8 BOM：Excel 直接開啟
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("對帳單", text);
        Assert.Contains("2025-06-01 ~ 2025-06-30", text);
        Assert.Contains("應付金額,100", text);
        Assert.Contains(d1.OrderNo, text);
        Assert.Contains("10", text); // 分潤率
        Assert.DoesNotContain("charity-test.invalid", text); // 不含捐款人 Email
        Assert.DoesNotContain("測試捐款人", text);
    }
}
