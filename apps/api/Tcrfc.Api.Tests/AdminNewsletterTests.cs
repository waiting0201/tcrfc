using System.Net;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminNewsletter;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>G3 電子報訂閱名單（主站規劃書 §4.7 G3）。名單以俱樂部分開（唯一鍵 club_id＋email，同一人可只退訂其中一站）、視同個資，
/// 匯出須額外授權並填用途。EDM 平台尚未串接，接縫以假實作驗證「串接後」的流程。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminNewsletterTests(AdminWriteApiFixture fixture)
{
    private const string Tc = "/api/v1/admin/tcrfc/newsletter";
    private const string Bw = "/api/v1/admin/bw/newsletter";

    private static string Email(string tag) => $"zz-test-{tag}-{Guid.NewGuid():N}"[..40] + "@example.test";

    private static Task CleanupAsync() => BizTest.ExecuteSqlAsync("DELETE FROM newsletter_subscribers WHERE email LIKE '%zz-test-%'");

    private static async Task<AdminNewsletterSubscriberDto> AddAsync(HttpClient client, string prefix, string email, string? source = null)
    {
        var response = await AppTest.PostJsonAsync(client, prefix + "/subscribers", new { email, source });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AppTest.ReadAsync<AdminNewsletterSubscriberDto>(response);
    }

    [Fact]
    public async Task 權限_客服可看可改_檢視者與內容編輯不可_匯出僅系統管理員_合作球隊只有自家()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);

        Assert.Equal(HttpStatusCode.OK, (await service.GetAsync(Tc + "/subscribers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Tc + "/subscribers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync(Tc + "/subscribers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Tc + "/subscribers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync(Tc + "/export?purpose=test")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Tc + "/export?purpose=test")).StatusCode);

        // 合作球隊管理只被授權藍鯨：自家可看、對方俱樂部不行
        Assert.Equal(HttpStatusCode.OK, (await partner.GetAsync(Bw + "/subscribers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync(Tc + "/subscribers")).StatusCode);
    }

    [Fact]
    public async Task 新增_轉小寫_重複409_格式錯400_來源預設_退訂後不能直接加回_重新訂閱須註明原因並留日誌()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var email = Email("a");
        try
        {
            var created = await AddAsync(admin, Tc, email.ToUpperInvariant());
            Assert.Equal(email, created.Email);
            Assert.Equal("subscribed", created.Status);
            Assert.Equal("後台新增", created.Source);
            Assert.NotNull(created.SubscribedAt);

            var dup = await AppTest.PostJsonAsync(admin, Tc + "/subscribers", new { email });
            Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(admin, Tc + "/subscribers", new { email = "not-an-email" })).StatusCode);

            var unsub = await AppTest.ReadAsync<AdminNewsletterSubscriberDto>(await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{created.Id}/status", new { status = "unsubscribed" }));
            Assert.Equal("已退訂", unsub.StatusLabel);
            Assert.NotNull(unsub.UnsubscribedAt);

            // 退訂是法遵事實：不能用「新增」繞過
            var readd = await AppTest.PostJsonAsync(admin, Tc + "/subscribers", new { email });
            Assert.Equal(HttpStatusCode.Conflict, readd.StatusCode);
            Assert.Contains("退訂", await readd.Content.ReadAsStringAsync());

            // 重新訂閱：沒有原因 400、有原因才成立
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{created.Id}/status", new { status = "subscribed" })).StatusCode);
            var back = await AppTest.ReadAsync<AdminNewsletterSubscriberDto>(await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{created.Id}/status", new { status = "subscribed", reason = "訂閱者來電要求重新訂閱" }));
            Assert.Equal("subscribed", back.Status);
            Assert.Null(back.UnsubscribedAt);

            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{created.Id}/status", new { status = "weird" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Tc}/subscribers/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Tc}/subscribers/{created.Id}")).StatusCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 兩站名單各自獨立_同一信箱可各存一份_只退訂其中一站不影響另一站_跨站讀不到()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var email = Email("b");
        try
        {
            var tc = await AddAsync(admin, Tc, email);
            var bw = await AddAsync(admin, Bw, email);
            Assert.NotEqual(tc.Id, bw.Id);
            await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{tc.Id}/status", new { status = "unsubscribed" });

            var bwList = await AppTest.ReadAsync<PagedResult<AdminNewsletterSubscriberDto>>(await admin.GetAsync($"{Bw}/subscribers?keyword={email}"));
            Assert.Equal("subscribed", Assert.Single(bwList.Items).Status);
            var tcList = await AppTest.ReadAsync<PagedResult<AdminNewsletterSubscriberDto>>(await admin.GetAsync($"{Tc}/subscribers?keyword={email}"));
            Assert.Equal("unsubscribed", Assert.Single(tcList.Items).Status);

            // 用對方俱樂部的路由操作這一筆：找不到
            Assert.Equal(HttpStatusCode.NotFound, (await AppTest.PutJsonAsync(admin, $"{Bw}/subscribers/{tc.Id}/status", new { status = "unsubscribed" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Bw}/subscribers/{tc.Id}")).StatusCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 清單篩選與摘要_狀態_來源_關鍵字_分頁()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var before = await AppTest.ReadAsync<AdminNewsletterSummaryDto>(await admin.GetAsync(Tc + "/summary"));
            var a = await AddAsync(admin, Tc, Email("c1"), "zz-test-來源甲");
            await AddAsync(admin, Tc, Email("c2"), "zz-test-來源甲");
            var c = await AddAsync(admin, Tc, Email("c3"), "zz-test-來源乙");
            await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{c.Id}/status", new { status = "unsubscribed" });

            var after = await AppTest.ReadAsync<AdminNewsletterSummaryDto>(await admin.GetAsync(Tc + "/summary"));
            Assert.Equal(before.SubscribedCount + 2, after.SubscribedCount);
            Assert.Equal(before.UnsubscribedCount + 1, after.UnsubscribedCount);
            Assert.Equal(2, after.Sources.Single(s => s.Source == "zz-test-來源甲").Count);
            Assert.DoesNotContain(after.Sources, s => s.Source == "zz-test-來源乙"); // 摘要的來源分布只算目前訂閱中的人

            var bySource = await AppTest.ReadAsync<PagedResult<AdminNewsletterSubscriberDto>>(await admin.GetAsync(Tc + "/subscribers?source=" + Uri.EscapeDataString("zz-test-來源甲")));
            Assert.Equal(2, bySource.TotalCount);
            var unsubOnly = await AppTest.ReadAsync<PagedResult<AdminNewsletterSubscriberDto>>(await admin.GetAsync(Tc + "/subscribers?status=unsubscribed&keyword=zz-test-"));
            Assert.Equal(c.Id, Assert.Single(unsubOnly.Items).Id);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Tc + "/subscribers?status=nope")).StatusCode);
            var paged = await AppTest.ReadAsync<PagedResult<AdminNewsletterSubscriberDto>>(await admin.GetAsync(Tc + "/subscribers?keyword=zz-test-&pageSize=2&page=2"));
            Assert.Single(paged.Items);
            Assert.Equal(3, paged.TotalCount);
            Assert.Contains(bySource.Items, i => i.Id == a.Id);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 匯出_用途必填_含BOM與表頭_公式注入被中和_預設含退訂但可篩選()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            await AddAsync(admin, Tc, "=zz-test-cmd@example.test", "+惡意來源");
            var sub = await AddAsync(admin, Tc, Email("d"));
            await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{sub.Id}/status", new { status = "unsubscribed" });

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Tc + "/export?keyword=zz-test-")).StatusCode);
            var response = await admin.GetAsync(Tc + "/export?keyword=zz-test-&purpose=" + Uri.EscapeDataString("每月電子報寄送前對名單"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.StartsWith("﻿Email,來源,狀態,訂閱時間,退訂時間", text);
            Assert.Contains("'=zz-test-cmd@example.test", text);
            Assert.Contains("'+惡意來源", text);
            Assert.Contains("已退訂", text);

            var onlySubscribed = Encoding.UTF8.GetString(await (await admin.GetAsync(Tc + "/export?keyword=zz-test-&status=subscribed&purpose=x")).Content.ReadAsByteArrayAsync());
            Assert.DoesNotContain("已退訂", onlySubscribed);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private sealed class FakeEdm : INewsletterEdmSync
    {
        public static EdmSyncRequest? Last;
        public string? ProviderName => "測試平台";

        public Task<EdmSyncResult> SyncAsync(EdmSyncRequest request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new EdmSyncResult(true, request.SubscribedEmails.Count, "同步完成"));
        }
    }

    [Fact]
    public async Task EDM平台_尚未串接如實回報_串接後名單與退訂抑制清單一併送出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var live = Email("e1");
        var gone = Email("e2");
        try
        {
            await AddAsync(admin, Tc, live);
            var g = await AddAsync(admin, Tc, gone);
            await AppTest.PutJsonAsync(admin, $"{Tc}/subscribers/{g.Id}/status", new { status = "unsubscribed" });

            var status = await AppTest.ReadAsync<AdminNewsletterEdmStatusDto>(await admin.GetAsync(Tc + "/edm"));
            Assert.False(status.Configured);
            var notConfigured = await AppTest.ReadAsync<AdminNewsletterEdmSyncResultDto>(await AppTest.PostJsonAsync(admin, Tc + "/edm/sync", new { }));
            Assert.False(notConfigured.Configured);
            Assert.Equal(0, notConfigured.SyncedCount);
            Assert.Contains("尚未串接", notConfigured.Message);

            using var connected = fixture.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<INewsletterEdmSync>();
                s.AddSingleton<INewsletterEdmSync, FakeEdm>();
            }));
            using var adminConnected = await BizTest.ClientAsync(connected, "super.admin@tcrfc.test");
            Assert.True((await AppTest.ReadAsync<AdminNewsletterEdmStatusDto>(await adminConnected.GetAsync(Tc + "/edm"))).Configured);
            var result = await AppTest.ReadAsync<AdminNewsletterEdmSyncResultDto>(await AppTest.PostJsonAsync(adminConnected, Tc + "/edm/sync", new { }));
            Assert.True(result.Configured);
            Assert.Contains(live, FakeEdm.Last!.SubscribedEmails);
            Assert.Contains(gone, FakeEdm.Last.UnsubscribedEmails); // 退訂名單必須一併送出，平台端才不會再寄給他
            Assert.DoesNotContain(gone, FakeEdm.Last.SubscribedEmails);
            Assert.Equal("tcrfc", FakeEdm.Last.ClubCode);
        }
        finally
        {
            await CleanupAsync();
        }
    }
}
