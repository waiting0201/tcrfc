using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 慈善憑證的所有寫入路徑共用同一把鎖（<c>charity-invoice:{捐款}</c>，E-338 後續）。
/// 開立在「呼叫加值中心」期間持鎖，若其他路徑（退款連動作廢、人工登記號碼、人工作廢、人工折讓）不取鎖，會在這段窗口內改寫同一張憑證：
/// 退款把 pending 標成作廢（沒呼叫加值中心）、開立完成後又寫回 issued → 加值中心留下一張有效憑證；人工登記的號碼被背景開立蓋掉 → 同一筆捐款兩個號碼。
/// 每個測試都用延遲 600 毫秒的加值中心拉長窗口，並在開立進行中（150 毫秒後）才進場。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityInvoiceLockTests(CharityApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<T> InScopeAsync<T>(Func<CharityInvoiceService, Task<T>> action)
    {
        await using var scope = fx.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<CharityInvoiceService>());
    }

    private async Task<Donation> LoadDonationAsync(Guid donationId)
    {
        await using var scope = fx.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CharityDbContext>().Donations.AsNoTracking().SingleAsync(d => d.Id == donationId);
    }

    private async Task<(Guid DonationId, Guid InvoiceId, string OrderNo)> SeedAsync(string issueStatus, string? invoiceNo = null)
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        var d = await SeedPaidDonationAsync(fx, projectId, null, 500, DateTime.UtcNow, invoiceIssueStatus: issueStatus, invoiceNo: invoiceNo);
        var invoiceId = await fx.ScalarAsync<Guid>("SELECT id FROM donation_invoices WHERE donation_id = @d", ("@d", d.Id));
        return (d.Id, invoiceId, d.OrderNo);
    }

    private async Task<(string IssueStatus, string VoidStatus, string? InvoiceNo)> StateAsync(Guid invoiceId)
    {
        var issue = await fx.ScalarAsync<string>("SELECT issue_status FROM donation_invoices WHERE id = @i", ("@i", invoiceId));
        var voided = await fx.ScalarAsync<string>("SELECT void_status FROM donation_invoices WHERE id = @i", ("@i", invoiceId));
        var no = await fx.ScalarAsync<string>("SELECT invoice_no FROM donation_invoices WHERE id = @i", ("@i", invoiceId));
        return (issue, voided, no);
    }

    /// <summary>啟動背景開立並確認它已經進入「呼叫加值中心」才回傳，之後進場的路徑才一定落在窗口內。</summary>
    private async Task<Task<InvoiceIssueOutcome>> StartSlowIssueAsync(Guid donationId)
    {
        fx.Invoices.IssueDelayMilliseconds = 600;
        var before = fx.Invoices.IssueCalls;
        var issuing = Task.Run(() => InScopeAsync(s => s.TryIssueAsync(donationId, false, default)));
        for (var i = 0; i < 100 && fx.Invoices.IssueCalls == before; i++)
        {
            await Task.Delay(20);
        }

        Assert.Equal(before + 1, fx.Invoices.IssueCalls);
        await Task.Delay(100);
        return issuing;
    }

    [Fact]
    public async Task 退款連動作廢_背景開立進行中_等開立完成後對加值中心作廢_不留有效憑證()
    {
        var (donationId, invoiceId, _) = await SeedAsync("pending");
        var admin = await fx.CreateAdminAsync(true);
        var issuing = await StartSlowIssueAsync(donationId);

        var donation = await LoadDonationAsync(donationId);
        var voided = await InScopeAsync(s => s.VoidOrAllowForRefundAsync(donation, "誤捐退款", admin.Id, default));
        await issuing;

        Assert.True(voided);
        var state = await StateAsync(invoiceId);
        Assert.Equal(("issued", "voided"), (state.IssueStatus, state.VoidStatus));
        Assert.Equal(1, fx.Invoices.IssueCalls);
        Assert.Equal(1, fx.Invoices.VoidCalls); // 加值中心端那張已開立的憑證確實被作廢，沒有殘留
    }

    [Fact]
    public async Task 人工登記號碼_背景開立進行中_回409_加值中心開出的號碼不被蓋掉也不蓋別人()
    {
        var (donationId, invoiceId, _) = await SeedAsync("pending");
        var admin = await fx.CreateAdminAsync(true);
        var issuing = await StartSlowIssueAsync(donationId);

        await Assert.ThrowsAsync<CharityConflictException>(() => InScopeAsync(s =>
            s.ManualIssueAsync(invoiceId, "MN-12345678", DateTime.UtcNow, admin.Id, default)));
        await issuing;

        var state = await StateAsync(invoiceId);
        Assert.Equal("issued", state.IssueStatus);
        Assert.NotEqual("MN-12345678", state.InvoiceNo); // 同一筆捐款只有一個號碼：加值中心開出的那個
    }

    [Fact]
    public async Task 人工作廢_背景開立進行中_回409_憑證維持已開立_不會變成作廢卻在加值中心有效()
    {
        var (donationId, invoiceId, _) = await SeedAsync("pending");
        var admin = await fx.CreateAdminAsync(true);
        var issuing = await StartSlowIssueAsync(donationId);

        await Assert.ThrowsAsync<CharityConflictException>(() => InScopeAsync(async s =>
        {
            await s.VoidManuallyAsync(invoiceId, "測試作廢", admin.Id, default);
            return 0;
        }));
        await issuing;

        var state = await StateAsync(invoiceId);
        Assert.Equal(("issued", "none"), (state.IssueStatus, state.VoidStatus));
    }

    [Fact]
    public async Task 人工作廢與折讓_並行_加值中心只被呼叫一次_另一方回409()
    {
        var (_, invoiceId, _) = await SeedAsync("issued");
        var admin = await fx.CreateAdminAsync(true);
        fx.Invoices.VoidDelayMilliseconds = 500;

        async Task<bool> Run(Func<CharityInvoiceService, Task> action)
        {
            try
            {
                await InScopeAsync(async s =>
                {
                    await action(s);
                    return 0;
                });
                return true;
            }
            catch (CharityConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            Task.Run(() => Run(s => s.VoidManuallyAsync(invoiceId, "作廢 A", admin.Id, default))),
            Task.Run(() => Run(s => s.VoidManuallyAsync(invoiceId, "作廢 B", admin.Id, default))),
            Task.Run(() => Run(s => s.AllowanceManuallyAsync(invoiceId, "折讓 C", admin.Id, default))));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(1, fx.Invoices.VoidCalls + fx.Invoices.AllowanceCalls); // 不會對同一張憑證又作廢又折讓
        Assert.NotEqual("none", (await StateAsync(invoiceId)).VoidStatus);
    }

    [Fact]
    public async Task 已人工作廢的待開立憑證_背景開立不得再對加值中心開立()
    {
        var (donationId, invoiceId, _) = await SeedAsync("pending");
        var admin = await fx.CreateAdminAsync(true);
        await InScopeAsync(async s =>
        {
            await s.VoidManuallyAsync(invoiceId, "測試作廢", admin.Id, default);
            return 0;
        });

        var outcome = await InScopeAsync(s => s.TryIssueAsync(donationId, false, default));

        Assert.Equal(InvoiceIssueOutcome.Skipped, outcome);
        Assert.Equal(0, fx.Invoices.IssueCalls);
        Assert.Equal(("pending", "voided"), ((await StateAsync(invoiceId)).IssueStatus, (await StateAsync(invoiceId)).VoidStatus));
    }
}
