using System.Net;
using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminBenefits;
using Tcrfc.Api.Features.AdminJerseys;
using Tcrfc.Api.Features.AdminMemberships;
using Tcrfc.Api.Features.AdminPartnerStores;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>K3 球衣發放、K4 特約店家與權益對照表（S2-5）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminJerseysAndStoresTests(AdminWriteApiFixture fixture)
{
    private static readonly string Today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    /// <summary>建立測試會員並以 single 方案（含 1 件球衣）開通，回傳（會員、會籍 id）。</summary>
    private async Task<(Guid MemberId, Guid MembershipId)> NewFanMemberAsync(HttpClient client, string club, string tag, string season, string plan)
    {
        var member = await B1Test.CreateMemberAsync(client, club, tag);
        var planId = await B1Test.PlanIdAsync(club, season, plan);
        // bw 2025 方案的期間早已結束：補登會籍要明確指定開始日與到期日（見「開通」的說明）。
        var backfill = club == "bw";
        var result = await BizTest.ReadAsync<AdminMembershipDetailDto>(await client.PostAsync($"/api/v1/admin/{club}/memberships/activate",
            BizTest.Json(new
            {
                memberId = member.Id, planId, paymentMethod = "onsite", amount = 100, paidOn = Today,
                startOn = backfill ? "2025-04-23" : null, endOn = backfill ? "2025-06-15" : null,
            })));
        return (member.Id, result.Membership.MembershipId);
    }

    // ═════════════ K3 ═════════════

    [Fact]
    public async Task 球衣_權限_未登入401_沒有權限403_跨俱樂部403()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/jerseys")).StatusCode);
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/tcrfc/jerseys")).StatusCode);
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await service.GetAsync("/api/v1/admin/tcrfc/jerseys")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.GetAsync("/api/v1/admin/bw/jerseys")).StatusCode);
    }

    [Fact]
    public async Task 球衣_登記受方案件數限制_狀態流轉與寄送規則()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var (memberId, membershipId) = await NewFanMemberAsync(service, "tcrfc", "jersey", "2026-27", "single");
        try
        {
            // 寄送必須有電話與地址
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/jerseys",
                BizTest.Json(new { membershipId, recipientName = "【測試】領用人", size = "l", deliveryMethod = "ship" }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/jerseys",
                BizTest.Json(new { membershipId, recipientName = "【測試】領用人", size = "L", deliveryMethod = "courier" }))).StatusCode);

            var created = await BizTest.ReadAsync<AdminJerseyDto>(await service.PostAsync("/api/v1/admin/tcrfc/jerseys", BizTest.Json(new
            {
                membershipId, recipientName = "【測試】領用人", phone = "0900-000-888", size = "l", deliveryMethod = "ship", address = "【測試】台中市西屯區測試路 9 號",
            })));
            Assert.Equal("L", created.Size); // 尺寸正規化為大寫
            Assert.Equal("待處理", created.StatusLabel);
            Assert.False(created.IsMasked); // 客服可見完整收件資訊（出貨需要）

            // single 方案只含 1 件 → 第二件 409
            Assert.Equal(HttpStatusCode.Conflict, (await service.PostAsync("/api/v1/admin/tcrfc/jerseys", BizTest.Json(new
            {
                membershipId, recipientName = "【測試】第二件", size = "M", deliveryMethod = "pickup",
            }))).StatusCode);

            // 已寄出：記寄出日期；回待處理：清掉日期
            var shipped = await BizTest.ReadAsync<AdminJerseyDto>(await service.PutAsync($"/api/v1/admin/tcrfc/jerseys/{created.Id}", BizTest.Json(new { status = "shipped" })));
            Assert.Equal("已寄出", shipped.StatusLabel);
            Assert.NotNull(shipped.ShippedOn);
            var back = await BizTest.ReadAsync<AdminJerseyDto>(await service.PutAsync($"/api/v1/admin/tcrfc/jerseys/{created.Id}", BizTest.Json(new { status = "pending" })));
            Assert.Null(back.ShippedOn);
            // 改成到場領取後不能標已寄出，可以直接標已領取
            var pickup = await BizTest.ReadAsync<AdminJerseyDto>(await service.PutAsync($"/api/v1/admin/tcrfc/jerseys/{created.Id}", BizTest.Json(new { deliveryMethod = "pickup" })));
            Assert.Equal("到場領取", pickup.DeliveryMethodLabel);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/jerseys/{created.Id}", BizTest.Json(new { status = "shipped" }))).StatusCode);
            var received = await BizTest.ReadAsync<AdminJerseyDto>(await service.PutAsync($"/api/v1/admin/tcrfc/jerseys/{created.Id}", BizTest.Json(new { status = "received" })));
            Assert.NotNull(received.ReceivedOn);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/jerseys/{created.Id}", BizTest.Json(new { status = "lost" }))).StatusCode);

            // 會員詳情的球衣狀態同步反映
            var detail = await BizTest.ReadAsync<Tcrfc.Api.Features.AdminMembers.AdminMemberDetailDto>(await service.GetAsync($"/api/v1/admin/tcrfc/members/{memberId}"));
            Assert.Equal("received", Assert.Single(detail.JerseyIssues).Status);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(memberId);
        }
    }

    [Fact]
    public async Task 球衣_免費會籍不含球衣_依尺寸統計備貨量_批次狀態略過無法處理的()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var free = await B1Test.CreateMemberAsync(service, "tcrfc", "jerseyfree");
        var (memberId, membershipId) = await NewFanMemberAsync(service, "tcrfc", "jerseybatch", "2026-27", "family");
        try
        {
            var season = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
            var freeMembership = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships", BizTest.Json(new { memberId = free.Id, seasonId = season })));
            Assert.Equal(HttpStatusCode.Conflict, (await service.PostAsync("/api/v1/admin/tcrfc/jerseys", BizTest.Json(new
            {
                membershipId = freeMembership.Membership.MembershipId, recipientName = "x", size = "M", deliveryMethod = "pickup",
            }))).StatusCode);

            var ids = new List<Guid>();
            foreach (var (size, method) in new[] { ("XL", "pickup"), ("XL", "pickup"), ("S", "ship") })
            {
                var response = await service.PostAsync("/api/v1/admin/tcrfc/jerseys", BizTest.Json(new
                {
                    membershipId, recipientName = "【測試】備貨", phone = "0900-000-889", size, deliveryMethod = method, address = method == "ship" ? "【測試】地址" : null,
                }));
                ids.Add((await BizTest.ReadAsync<AdminJerseyDto>(response)).Id);
            }

            var summary = await BizTest.ReadAsync<List<AdminJerseySizeSummaryDto>>(await service.GetAsync("/api/v1/admin/tcrfc/jerseys/size-summary"));
            Assert.True(summary.First(s => s.Size == "XL").Total >= 2);
            Assert.True(summary.First(s => s.Size == "S").Ship >= 1);
            Assert.True(summary.FindIndex(s => s.Size == "S") < summary.FindIndex(s => s.Size == "XL")); // 依尺寸大小排序

            // 批次標已寄出：兩件到場領取的略過（不是寄送），寄送那件成功；不存在的 id 也略過
            var batch = await BizTest.ReadAsync<BatchOperationResultDto>(await service.PostAsync("/api/v1/admin/tcrfc/jerseys/batch/status",
                BizTest.Json(new { ids = ids.Append(Guid.NewGuid()), status = "shipped" })));
            Assert.Equal(1, batch.UpdatedCount);
            Assert.Equal(3, batch.Skipped.Count);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/jerseys/batch/status", BizTest.Json(new { ids = Array.Empty<Guid>(), status = "shipped" }))).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(memberId, free.Id);
        }
    }

    [Fact]
    public async Task 球衣_沒有解除遮罩權限的角色看到遮罩值_也不能修改收件人_匯出要權限與用途()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var (memberId, membershipId) = await NewFanMemberAsync(admin, "bw", "jerseymask", "2025", "single");
        try
        {
            var created = await BizTest.ReadAsync<AdminJerseyDto>(await admin.PostAsync("/api/v1/admin/bw/jerseys", BizTest.Json(new
            {
                membershipId, recipientName = "王大明", phone = "0912-345-678", size = "M", deliveryMethod = "ship", address = "台中市豐原區測試路 100 號",
            })));

            using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
            var masked = await BizTest.ReadAsync<AdminJerseyDto>(await partner.GetAsync($"/api/v1/admin/bw/jerseys/{created.Id}"));
            Assert.True(masked.IsMasked);
            Assert.Equal("王○明", masked.RecipientName);
            Assert.DoesNotContain("0912-345-678", masked.Phone);
            Assert.EndsWith("***", masked.Address);
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.PutAsync($"/api/v1/admin/bw/jerseys/{created.Id}", BizTest.Json(new { recipientName = "改名" }))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/bw/jerseys/export?purpose=x")).StatusCode);

            // 系統管理員匯出：沒有用途 400，有用途 200 且含完整收件資訊
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/bw/jerseys/export")).StatusCode);
            var csv = Encoding.UTF8.GetString(await (await admin.GetAsync("/api/v1/admin/bw/jerseys/export?purpose=%E5%87%BA%E8%B2%A8")).Content.ReadAsByteArrayAsync());
            Assert.Contains("台中市豐原區測試路 100 號", csv);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(memberId);
        }
    }

    // ═════════════ K4 特約店家 ═════════════

    private static object StorePayload(string tag, string tier = "all", string status = "draft", decimal? lat = null, decimal? lng = null, bool shared = false, string? map = "https://maps.example.com/x") => new
    {
        category = "餐飲", region = "台中市西屯區", lat, lng, phone = "04-0000-0000", businessHours = "週一至週五 11:00–21:00", mapUrl = map,
        websiteUrl = "https://example.com/s", applicableTier = tier, startOn = "2026-01-01", sortOrder = 50, status, isShared = shared,
        content = new
        {
            zh = new { name = $"【測試】店家{tag}", address = "【測試】台中市西屯區測試路 1 號", offerContent = "【測試】九折" },
            en = new { name = $"Test Store {tag}", address = "No. 1 Test Rd.", offerContent = "10% off" },
        },
    };

    [Fact]
    public async Task 特約店家_權限與遮蔽_合作球隊管理沒有K4權限()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/partner-stores")).StatusCode);
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/bw/partner-stores")).StatusCode);
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/v1/admin/tcrfc/partner-stores")).StatusCode);
    }

    [Fact]
    public async Task 特約店家_建立更新刪除_雙語地址_座標成對_網址驗證_篩選項目()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var ids = new List<Guid>();
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(StorePayload("lat", lat: 24.1m)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(StorePayload("range", lat: 124.1m, lng: 120.6m)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(StorePayload("url", map: "javascript:alert(1)")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(StorePayload("tier", tier: "gold")))).StatusCode);

            var tag = BizTest.Unique("s");
            var created = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/partner-stores",
                BizTest.Multipart(StorePayload(tag, tier: "fan_club", status: "published", lat: 24.18m, lng: 120.606m))));
            ids.Add(created.Id);
            Assert.False(created.IsShared);
            Assert.Equal("限付費會員", created.ApplicableTierLabel);
            Assert.Equal("上架", created.StatusLabel);
            Assert.Equal("【測試】台中市西屯區測試路 1 號", created.Zh.Address);
            Assert.Equal("No. 1 Test Rd.", created.En!.Address);
            Assert.Equal("週一至週五 11:00–21:00", created.BusinessHours);
            Assert.Equal(24.18m, created.Lat);
            // 營業時間存進 json 欄位必須是物件（原生 json 不收字串純量，E-111）：{"text":"…"}
            await BizTest.ScalarGuidAsync("SELECT id FROM partner_stores WHERE id = @I AND LEFT(CAST(business_hours AS nvarchar(max)), 1) = '{'", ("@I", created.Id));
            Assert.StartsWith("test-store-", created.Slug); // 有英文名稱：由英文名稱轉出

            var updated = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await service.PutAsync($"/api/v1/admin/tcrfc/partner-stores/{created.Id}",
                BizTest.Multipart(StorePayload(tag + "x", tier: "all", status: "draft"))));
            Assert.Equal("下架", updated.StatusLabel);
            Assert.Null(updated.Lat);
            Assert.Equal(created.Slug, updated.Slug); // 更新省略網址名稱＝維持不變

            var filters = await BizTest.ReadAsync<AdminPartnerStoreFiltersDto>(await service.GetAsync("/api/v1/admin/tcrfc/partner-stores/filters"));
            Assert.Contains("餐飲", filters.Categories);
            Assert.Contains("台中市西屯區", filters.Regions);

            var list = await BizTest.ReadAsync<List<AdminPartnerStoreListItemDto>>(await service.GetAsync("/api/v1/admin/tcrfc/partner-stores?status=draft&keyword=" + Uri.EscapeDataString(tag + "x")));
            Assert.Single(list);
            Assert.Equal(HttpStatusCode.NoContent, (await service.PutAsync("/api/v1/admin/tcrfc/partner-stores/order", BizTest.Json(new { ids = new[] { created.Id } }))).StatusCode);
        }
        finally
        {
            foreach (var id in ids)
            {
                Assert.Equal(HttpStatusCode.NoContent, (await service.DeleteAsync($"/api/v1/admin/tcrfc/partner-stores/{id}")).StatusCode);
            }
        }

        if (ids.Count > 0)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync($"/api/v1/admin/tcrfc/partner-stores/{ids[0]}")).StatusCode);
        }
    }

    [Fact]
    public async Task 特約店家_兩隊共同的店家所有俱樂部看得到_只有系統管理員能編輯()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var list = await BizTest.ReadAsync<List<AdminPartnerStoreListItemDto>>(await service.GetAsync("/api/v1/admin/tcrfc/partner-stores"));
        var shared = list.First(s => s.IsShared);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.PutAsync($"/api/v1/admin/tcrfc/partner-stores/{shared.Id}", BizTest.Multipart(StorePayload("x")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await service.DeleteAsync($"/api/v1/admin/tcrfc/partner-stores/{shared.Id}")).StatusCode);
        // 受限帳號也不能建立共同店家
        Assert.Equal(HttpStatusCode.Forbidden, (await service.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(StorePayload("s", shared: true)))).StatusCode);

        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var created = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(StorePayload(BizTest.Unique("sh"), shared: true))));
        try
        {
            Assert.True(created.IsShared);
            // 換到 bw 俱樂部一樣看得到（共同）；bw 的系統管理員視角
            var bwList = await BizTest.ReadAsync<List<AdminPartnerStoreListItemDto>>(await admin.GetAsync("/api/v1/admin/bw/partner-stores"));
            Assert.Contains(bwList, s => s.Id == created.Id && s.IsShared);
            // 排序不含共同店家
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/v1/admin/tcrfc/partner-stores/order", BizTest.Json(new { ids = new[] { created.Id } }))).StatusCode);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/partner-stores/{created.Id}")).StatusCode);
        }
    }

    // ═════════════ K4 權益對照表 ═════════════

    [Fact]
    public async Task 權益條目_建立更新排序刪除_分組驗證_方案必須屬於目前俱樂部()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var planId = await B1Test.PlanIdAsync("tcrfc", "2026-27", "family");
        var ids = new List<Guid>();
        try
        {
            object Payload(string name, string group = "event", string status = "published", Guid? plan = null) => new
            {
                planId = plan ?? planId, group, status,
                content = new { zh = new { name, description = "【測試】說明", freeValue = "✗", paidValue = "✓" }, en = new { name = "Test benefit", freeValue = "✗", paidValue = "✓" } },
            };

            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/membership-benefits", BizTest.Json(Payload("x", group: "shop")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/membership-benefits", BizTest.Json(Payload("x", plan: Guid.NewGuid())))).StatusCode);
            var bwPlan = await B1Test.PlanIdAsync("bw", "2025", "single");
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/membership-benefits", BizTest.Json(Payload("x", plan: bwPlan)))).StatusCode);

            var a = await BizTest.ReadAsync<AdminBenefitDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/membership-benefits", BizTest.Json(Payload("【測試】條目甲"))));
            var b = await BizTest.ReadAsync<AdminBenefitDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/membership-benefits", BizTest.Json(Payload("【測試】條目乙", group: "jersey"))));
            ids.AddRange([a.Id, b.Id]);
            Assert.Equal("活動", a.GroupLabel);
            Assert.Equal(a.SortOrder + 1, b.SortOrder); // 省略排序＝排在方案最後
            Assert.Equal("Test benefit", a.En!.Name);

            var updated = await BizTest.ReadAsync<AdminBenefitDetailDto>(await service.PutAsync($"/api/v1/admin/tcrfc/membership-benefits/{a.Id}",
                BizTest.Json(Payload("【測試】條目甲改", group: "store_discount", status: "draft"))));
            Assert.Equal("店家折扣", updated.GroupLabel);
            Assert.Equal("下架", updated.StatusLabel);
            // 不能搬到別的方案
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/membership-benefits/{a.Id}",
                BizTest.Json(Payload("x", plan: await B1Test.PlanIdAsync("tcrfc", "2026-27", "single"))))).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await service.PutAsync("/api/v1/admin/tcrfc/membership-benefits/order", BizTest.Json(new { planId, ids = new[] { b.Id, a.Id } }))).StatusCode);
            var list = await BizTest.ReadAsync<List<AdminBenefitListItemDto>>(await service.GetAsync($"/api/v1/admin/tcrfc/membership-benefits?planId={planId}"));
            Assert.True(list.FindIndex(x => x.Id == b.Id) < list.FindIndex(x => x.Id == a.Id));
            var groups = await BizTest.ReadAsync<List<AdminBenefitGroupDto>>(await service.GetAsync("/api/v1/admin/tcrfc/membership-benefits/groups"));
            Assert.Equal(4, groups.Count);
        }
        finally
        {
            foreach (var id in ids)
            {
                Assert.Equal(HttpStatusCode.NoContent, (await service.DeleteAsync($"/api/v1/admin/tcrfc/membership-benefits/{id}")).StatusCode);
            }
        }

        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync("/api/v1/admin/bw/membership-benefits")).StatusCode);
    }
}
