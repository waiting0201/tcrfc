using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Reconciliation;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 每日對帳（規劃書 §4.5）：本站 paid 捐款單 vs 金流交易明細，三種差異進異常佇列；結果保留供稽核（批次與差異不可刪除，只能更新處理狀態）。
/// 金流明細來源用可編排的替身（<see cref="ScriptedReconciliationSource"/>，來源代號 ct-recon）；測試日期放在 2025-05（遠在過去，與種子互不干擾）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityReconciliationTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Reconciliation = AdminBase + "/reconciliation";
    private static readonly DateOnly Day = new(2025, 5, 14);

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private async Task<Guid> ProjectAsync() => (await CreateProjectAsync(fx)).Id;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    private Task<HttpResponseMessage> RunAsync(HttpClient client, DateOnly? date = null)
        => client.PostAsJsonAsync($"{Reconciliation}/runs", new { date = date ?? Day }, TestJson.WriteOptions);

    private void GatewayHas(string txId, int amount, DateTime at) => fx.Reconciliation.Transactions.Add(new GatewayTransaction(txId, amount, at));

    [Fact]
    public async Task 完全一致_批次完成_沒有差異()
    {
        var projectId = await ProjectAsync();
        var a = await SeedPaidDonationAsync(fx, projectId, null, 500, NoonUtc(Day));
        var b = await SeedPaidDonationAsync(fx, projectId, null, 300, NoonUtc(Day));
        GatewayHas(a.TransactionId, 500, NoonUtc(Day));
        GatewayHas(b.TransactionId, 300, NoonUtc(Day));
        using var sys = await SysAsync();

        var result = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));

        Assert.Equal("completed", result.Status);
        Assert.Equal(2, result.ComparedCount);
        Assert.Equal(2, result.MatchedCount);
        Assert.Equal(0, result.DiscrepancyCount);
        Assert.Equal(Day, result.RunOn);
    }

    [Fact]
    public async Task 三種差異_本站有金流無_金流有本站無_金額不符_都進異常佇列_並連到捐款單()
    {
        var projectId = await ProjectAsync();
        var matched = await SeedPaidDonationAsync(fx, projectId, null, 100, NoonUtc(Day));
        var siteOnly = await SeedPaidDonationAsync(fx, projectId, null, 700, NoonUtc(Day));
        var mismatch = await SeedPaidDonationAsync(fx, projectId, null, 1000, NoonUtc(Day));
        // 「確認結果未知」的 pending 單：金流端其實已扣款 → 金流有本站無，並連到那張 pending 單
        var pending = await SeedPaidDonationAsync(fx, projectId, null, 250, NoonUtc(Day), status: "pending");
        await fx.ExecuteAsync("UPDATE donation_payments SET status = N'failed', confirmed_at = NULL WHERE donation_id = @d", ("@d", pending.Id));
        GatewayHas(matched.TransactionId, 100, NoonUtc(Day));
        GatewayHas(mismatch.TransactionId, 990, NoonUtc(Day));       // 金額不符：本站 1000、金流 990
        GatewayHas(pending.TransactionId, 250, NoonUtc(Day));
        GatewayHas("TEST-TX-ORPHAN-0001", 350, NoonUtc(Day));         // 本站完全沒有的交易
        using var sys = await SysAsync();

        var run = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));
        Assert.Equal(1, run.MatchedCount);
        Assert.Equal(4, run.DiscrepancyCount);
        Assert.Equal(4, run.NewDiscrepancies);

        var detail = await ReadAsync<AdminReconciliationRunDetailDto>(await sys.GetAsync($"{Reconciliation}/runs/{run.RunId}"));
        Assert.Equal(4, detail.Run.PendingCount);
        var site = Assert.Single(detail.Discrepancies, d => d.Type == "site_only");
        Assert.Equal(siteOnly.OrderNo, site.OrderNo);
        Assert.Equal(700, site.SiteAmount);
        Assert.Null(site.GatewayAmount);
        Assert.Null(site.GatewayTransactionId);

        var amount = Assert.Single(detail.Discrepancies, d => d.Type == "amount_mismatch");
        Assert.Equal(mismatch.OrderNo, amount.OrderNo);
        Assert.Equal(1000, amount.SiteAmount);
        Assert.Equal(990, amount.GatewayAmount);
        Assert.Equal(mismatch.TransactionId, amount.GatewayTransactionId);

        var gateway = detail.Discrepancies.Where(d => d.Type == "gateway_only").ToList();
        Assert.Equal(2, gateway.Count);
        Assert.Contains(gateway, g => g.OrderNo == pending.OrderNo && g.GatewayAmount == 250);
        var orphan = Assert.Single(gateway, g => g.OrderNo is null);
        Assert.Equal("TEST-TX-ORPHAN-0001", orphan.GatewayTransactionId);
        Assert.Equal(350, orphan.GatewayAmount);

        // 異常佇列的「對帳差異」計數與清單會包含它們
        var anomalies = await ReadAsync<List<AdminAnomalyDto>>(await sys.GetAsync($"{AdminBase}/donations/anomalies?kind=reconciliation"));
        Assert.True(anomalies.Count(a => a.DiscrepancyId is not null && detail.Discrepancies.Any(d => d.Id == a.DiscrepancyId)) == 4);
    }

    [Fact]
    public async Task 當天付款後來被退款的單_不會被誤判成本站有金流無()
    {
        var projectId = await ProjectAsync();
        var refunded = await SeedPaidDonationAsync(fx, projectId, null, 400, NoonUtc(Day), status: "refunded", refundReason: "誤按");
        GatewayHas(refunded.TransactionId, 400, NoonUtc(Day));
        using var sys = await SysAsync();

        var result = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(0, result.DiscrepancyCount);
    }

    [Fact]
    public async Task 重跑同一天_冪等_既有差異保留_已一致的待處理差異自動標記已處理_已處理的決定不被覆蓋()
    {
        var projectId = await ProjectAsync();
        var d1 = await SeedPaidDonationAsync(fx, projectId, null, 500, NoonUtc(Day));
        var d2 = await SeedPaidDonationAsync(fx, projectId, null, 600, NoonUtc(Day));
        GatewayHas(d1.TransactionId, 500, NoonUtc(Day)); // d2 金流端沒有 → site_only
        using var sys = await SysAsync();

        var first = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));
        Assert.Equal(1, first.DiscrepancyCount);

        // 沒有任何變動再跑一次：同一個批次、沒有新增差異
        var second = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));
        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(0, second.NewDiscrepancies);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM reconciliation_runs WHERE run_on = @d AND source = N'ct-recon'", ("@d", Day.ToDateTime(TimeOnly.MinValue))));

        // 金流端補上 d2 的交易（客服確認後）→ 重跑，原本待處理的差異自動標記已處理
        GatewayHas(d2.TransactionId, 600, NoonUtc(Day));
        var third = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));
        Assert.Equal(1, third.AutoResolved);
        Assert.Equal(0, third.DiscrepancyCount);
        var detail = await ReadAsync<AdminReconciliationRunDetailDto>(await sys.GetAsync($"{Reconciliation}/runs/{third.RunId}"));
        var kept = Assert.Single(detail.Discrepancies); // 差異記錄沒有被刪除
        Assert.Equal("resolved", kept.ResolutionStatus);
        Assert.Contains("自動標記", kept.ResolveNote);
    }

    [Fact]
    public async Task 取不到金流明細_記成失敗批次_回503_不會把當天所有捐款判成差異_已有成功批次不被覆蓋()
    {
        var projectId = await ProjectAsync();
        var d = await SeedPaidDonationAsync(fx, projectId, null, 500, NoonUtc(Day));
        fx.Reconciliation.Unavailable = true;
        using var sys = await SysAsync();

        var failed = await RunAsync(sys);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("failed", await fx.ScalarAsync<string>("SELECT status FROM reconciliation_runs WHERE run_on = @d AND source = N'ct-recon'", ("@d", Day.ToDateTime(TimeOnly.MinValue))));
        Assert.Equal(0, await fx.ScalarAsync<int>(
            "SELECT COUNT(*) FROM reconciliation_discrepancies x JOIN reconciliation_runs r ON r.id = x.reconciliation_run_id WHERE r.source = N'ct-recon'"));

        // 恢復後重跑成功，批次由 failed 轉 completed；之後再失敗不會把成功的批次改回 failed。
        fx.Reconciliation.Unavailable = false;
        GatewayHas(d.TransactionId, 500, NoonUtc(Day));
        Assert.Equal("completed", (await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys))).Status);
        fx.Reconciliation.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await RunAsync(sys)).StatusCode);
        Assert.Equal("completed", await fx.ScalarAsync<string>("SELECT status FROM reconciliation_runs WHERE run_on = @d AND source = N'ct-recon'", ("@d", Day.ToDateTime(TimeOnly.MinValue))));
    }

    [Fact]
    public async Task 處理差異_需要處理說明_只能處理一次_差異記錄保留_稽核留下紀錄()
    {
        var projectId = await ProjectAsync();
        await SeedPaidDonationAsync(fx, projectId, null, 700, NoonUtc(Day)); // 金流端沒有 → site_only
        var admin = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(admin);
        var run = await ReadAsync<AdminReconciliationRunResultDto>(await RunAsync(sys));
        var discrepancy = (await ReadAsync<AdminReconciliationRunDetailDto>(await sys.GetAsync($"{Reconciliation}/runs/{run.RunId}"))).Discrepancies.Single();

        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PostAsJsonAsync($"{Reconciliation}/discrepancies/{discrepancy.Id}/resolve", new { note = "" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.PostAsJsonAsync($"{Reconciliation}/discrepancies/{Guid.NewGuid()}/resolve", new { note = "找不到的差異" }, TestJson.WriteOptions)).StatusCode);

        var resolved = await ReadAsync<AdminReconciliationDiscrepancyDto>(
            await sys.PostAsJsonAsync($"{Reconciliation}/discrepancies/{discrepancy.Id}/resolve", new { note = "已與 LINE Pay 客服確認，該筆為測試扣款" }, TestJson.WriteOptions));
        Assert.Equal("resolved", resolved.ResolutionStatus);
        Assert.Equal("測試後台人員", resolved.ResolvedByName);
        Assert.Contains("LINE Pay", resolved.ResolveNote);

        Assert.Equal(HttpStatusCode.Conflict, (await sys.PostAsJsonAsync($"{Reconciliation}/discrepancies/{discrepancy.Id}/resolve", new { note = "再處理一次" }, TestJson.WriteOptions)).StatusCode);

        // 差異還在紀錄裡（供稽核），待處理計數歸零；沒有任何刪除端點。
        var after = await ReadAsync<AdminReconciliationRunDetailDto>(await sys.GetAsync($"{Reconciliation}/runs/{run.RunId}"));
        Assert.Single(after.Discrepancies);
        Assert.Equal(0, after.Run.PendingCount);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await sys.DeleteAsync($"{Reconciliation}/runs/{run.RunId}")).StatusCode);
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'reconciliation.resolve' AND target_id = @t", ("@a", admin.Id), ("@t", discrepancy.Id)));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'reconciliation.run'", ("@a", admin.Id)));
    }

    [Fact]
    public async Task 授權_手動對帳與處理差異需要重新確認付款權限_檢視者與商務不行_客服可以_列表只需檢視權限()
    {
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));
        using var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"{Reconciliation}/runs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Reconciliation}/runs", new { date = Day }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.GetAsync($"{Reconciliation}/runs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RunAsync(cs)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RunAsync(cs, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)))).StatusCode); // 未來的日期
    }

    [Fact]
    public async Task 排程_RunIfDue_昨天已有成功批次就不重跑_失敗批次30分鐘內不重試()
    {
        // 排程用的實際來源是 DI 裡的替身；這裡直接從容器取 runner 呼叫（不用等計時器）。
        using var scope = fx.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<CharityReconciliationRunner>();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)).AddDays(-1);

        // 先確保昨天（來源 ct-recon）沒有批次；清理會刪掉 ct-% 的批次。
        await fx.CleanupAsync();
        var started = await runner.RunAsync(yesterday, null, default);
        Assert.Equal("completed", started.Status);

        // 已有成功批次 → RunIfDue 不再跑（不論現在是幾點，都回傳 null）
        Assert.Null(await runner.RunIfDueAsync(default));
    }
}
