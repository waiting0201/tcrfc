using System.Net;
using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.AdminMemberships;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// K2 會籍與方案（S2-5）：方案 CRUD、手動開通與續會、調整、會員卡張數、批次到期、付款紀錄、續會名單匯出、會員編號規則。
/// 種子方案：tcrfc 2026-27 <c>single</c>（1200 元、1 卡 1 衣）／<c>family</c>（3000 元、3 卡 3 衣），bw 2025 <c>single</c>；唯讀。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminMembershipsTests(AdminWriteApiFixture fixture)
{
    private static object PlanPayload(Guid seasonId, string code, int fee = 999, string status = "draft", int cardQuota = 1, int jerseyQuota = 0) => new
    {
        seasonId, code, fee, cardQuota, jerseyQuota, midSeasonRule = "測試規則", midSeasonRuleEn = "Pro-rated", startsOn = "2026-09-13", endsOn = "2027-05-02", sortOrder = 9, status,
        content = new { zh = new { name = $"【測試】方案{code}", benefitNote = "測試權益" }, en = new { name = $"Test {code}" } },
    };

    // ═════════════ 方案 ═════════════

    [Fact]
    public async Task 方案_權限與跨俱樂部()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/membership-plans")).StatusCode);

        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await service.GetAsync("/api/v1/admin/tcrfc/membership-plans")).StatusCode);
        // 客服只有方案檢視，沒有新增
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        Assert.Equal(HttpStatusCode.Forbidden, (await service.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, "denied")))).StatusCode);

        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        var bwPlans = await BizTest.ReadAsync<List<AdminPlanListItemDto>>(await partner.GetAsync("/api/v1/admin/bw/membership-plans"));
        Assert.NotEmpty(bwPlans);
        Assert.DoesNotContain(bwPlans, p => p.SeasonCode == "2026-27"); // 看不到磐石的方案
    }

    [Fact]
    public async Task 方案_建立更新排序刪除_驗證與重複代碼()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        var code = BizTest.Unique("t-plan");
        Guid? id = null;
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, "Bad Code")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, "neg", fee: -1)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, "quota", cardQuota: 0)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(Guid.NewGuid(), "noseason")))).StatusCode);
            // bw 的球季不能用在 tcrfc 的方案
            var bwSeason = await B1Test.SeasonIdAsync("bw", "2025");
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(bwSeason, "cross")))).StatusCode);

            var created = await BizTest.ReadAsync<AdminPlanDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, code))));
            id = created.Id;
            Assert.Equal("下架", created.StatusLabel);
            Assert.Equal("Test " + code, created.En!.Name);
            Assert.Equal("測試規則", created.MidSeasonRule);
            Assert.Equal("Pro-rated", created.MidSeasonRuleEn); // 季中入會規則走 membership_plans_i18n 側表（D 類雙語）
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, code)))).StatusCode);

            var updated = await BizTest.ReadAsync<AdminPlanDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/membership-plans/{id}",
                BizTest.Json(new
                {
                    seasonId, code, fee = 1500, cardQuota = 2, jerseyQuota = 1, sortOrder = 9, status = "published",
                    content = new { zh = new { name = "【測試】改名方案" } },
                })));
            Assert.Equal(1500, updated.Fee);
            Assert.Equal("上架", updated.StatusLabel);
            Assert.Null(updated.En); // PUT 整份取代：省略英文版＝移除
            Assert.Null(updated.MidSeasonRule);
            Assert.Null(updated.MidSeasonRuleEn);

            // 只有英文規則、沒有英文名稱：英文列仍存在（En 內容區塊為 null），公開英文版規則用英文、名稱回退繁中
            var ruleOnly = await BizTest.ReadAsync<AdminPlanDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/membership-plans/{id}",
                BizTest.Json(new { seasonId, code, fee = 1500, cardQuota = 2, jerseyQuota = 1, sortOrder = 9, status = "published", midSeasonRule = "照比例", midSeasonRuleEn = "Pro-rated", content = new { zh = new { name = "【測試】改名方案" } } })));
            Assert.Null(ruleOnly.En);
            Assert.Equal("Pro-rated", ruleOnly.MidSeasonRuleEn);
            using (var anonymousPlans = await BizTest.ClientAsync(fixture, null))
            {
                var pubPlans = await BizTest.ReadAsync<List<Tcrfc.Api.Features.MembershipPublic.MembershipPlanPublicDto>>(await anonymousPlans.GetAsync("/api/v1/tcrfc/membership/plans?lang=en"));
                var mine = pubPlans.Single(p => p.Code == code);
                Assert.Equal("Pro-rated", mine.MidSeasonRule);
                Assert.Equal("【測試】改名方案", mine.Name);
                Assert.True(mine.IsFallbackLocale);
            }

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/membership-plans/{id}",
                BizTest.Json(new { seasonId, code, fee = 1500, cardQuota = 2, jerseyQuota = 1, sortOrder = 9, status = "published", midSeasonRuleEn = new string('a', 256), content = new { zh = new { name = "x" } } }))).StatusCode);

            var list = await BizTest.ReadAsync<List<AdminPlanListItemDto>>(await admin.GetAsync("/api/v1/admin/tcrfc/membership-plans?status=published"));
            Assert.Contains(list, p => p.Id == id);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync("/api/v1/admin/tcrfc/membership-plans/order", BizTest.Json(new { ids = new[] { id } }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/v1/admin/tcrfc/membership-plans/order", BizTest.Json(new { ids = new[] { Guid.NewGuid() } }))).StatusCode);
        }
        finally
        {
            if (id is not null)
            {
                Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/membership-plans/{id}")).StatusCode);
            }
        }

        if (id is not null)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/tcrfc/membership-plans/{id}")).StatusCode);
        }
    }

    [Fact]
    public async Task 方案_有會籍使用時不能刪除也不能換球季()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var single = await B1Test.PlanIdAsync("tcrfc", "2026-27", "single");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/tcrfc/membership-plans/{single}")).StatusCode);

        // 同一份方案的內容可以改，但換球季不行（tcrfc 只有一個球季，所以用一個臨時球季）
        var tempSeason = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO seasons (id, club_id, code, start_on, end_on) VALUES (@Id, (SELECT id FROM clubs WHERE code = N'tcrfc'), N'TMP-B1', '2030-01-01', '2030-12-31')",
            ("@Id", tempSeason));
        try
        {
            var detail = await BizTest.ReadAsync<AdminPlanDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/membership-plans/{single}"));
            var response = await admin.PutAsync($"/api/v1/admin/tcrfc/membership-plans/{single}", BizTest.Json(new
            {
                seasonId = tempSeason, code = detail.Code, fee = detail.Fee, cardQuota = detail.CardQuota, jerseyQuota = detail.JerseyQuota,
                status = detail.Status, content = new { zh = new { name = detail.Zh.Name } },
            }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM seasons WHERE id = @Id", ("@Id", tempSeason));
        }
    }

    // ═════════════ 開通與續會 ═════════════

    [Fact]
    public async Task 開通_建立球迷會籍_付款紀錄記下受益俱樂部與收款法人_發一張卡()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "activate");
        try
        {
            var plan = await B1Test.PlanIdAsync("tcrfc", "2026-27", "single");
            var tcrfcId = await B1Test.ClubIdAsync("tcrfc");
            var paidOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            var request = new { memberId = member.Id, planId = plan, paymentMethod = "onsite", amount = 1000, paidOn, note = "【測試】季中入會折算" };

            var result = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate", BizTest.Json(request)));
            Assert.Equal("fan_club", result.Membership.Tier);
            Assert.Equal("有效", result.Membership.EffectiveStatusLabel);
            Assert.Equal(new DateOnly(2027, 5, 2), result.Membership.EndOn);
            Assert.Equal(1000, result.Membership.PaidTotal);
            var card = Assert.Single(result.Cards);
            Assert.Equal("使用中", card.StatusLabel);
            var payment = Assert.Single(result.Payments);
            Assert.Equal("現場收款", payment.MethodLabel);
            Assert.Equal("tcrfc", payment.BeneficiaryClubCode);
            Assert.Equal("tcrfc", payment.CollectingClubCode); // 收款主體俱樂部
            Assert.Equal(1000, payment.Amount);
            Assert.False(string.IsNullOrEmpty(payment.HandledByName));

            // 重複送出 → 409
            Assert.Equal(HttpStatusCode.Conflict, (await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate", BizTest.Json(request))).StatusCode);
            // 受益俱樂部不是目前操作的俱樂部 → 400
            var bwClub = await B1Test.ClubIdAsync("bw");
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = plan, beneficiaryClubId = bwClub, paymentMethod = "onsite", amount = 1, paidOn }))).StatusCode);
            // 付款方式不合法 / 金額為負 / 未來日期
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = plan, paymentMethod = "card", amount = 1, paidOn }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = plan, paymentMethod = "onsite", amount = -5, paidOn }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = plan, paymentMethod = "onsite", amount = 5, paidOn = "2999-01-01" }))).StatusCode);

            // 付款紀錄清單（對帳）
            var payments = await BizTest.ReadAsync<PagedResult<AdminPaymentListItemDto>>(
                await service.GetAsync($"/api/v1/admin/tcrfc/membership-payments?membershipId={result.Membership.MembershipId}"));
            Assert.Single(payments.Items);
            Assert.Equal(member.MemberNo, payments.Items[0].MemberNo);
            // 會員詳情帶得出這份會籍與付款
            var detail = await BizTest.ReadAsync<AdminMemberDetailDto>(await service.GetAsync($"/api/v1/admin/tcrfc/members/{member.Id}"));
            Assert.Single(detail.Memberships);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 開通_下架的方案_不存在的會員_合作球隊管理沒有開通權限()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        var member = await B1Test.CreateMemberAsync(admin, "tcrfc", "draftplan");
        Guid? planId = null;
        try
        {
            var draft = await BizTest.ReadAsync<AdminPlanDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/membership-plans", BizTest.Json(PlanPayload(seasonId, BizTest.Unique("t-draft")))));
            planId = draft.Id;
            var paidOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId, paymentMethod = "linepay", amount = 1, paidOn }))).StatusCode);

            var single = await B1Test.PlanIdAsync("tcrfc", "2026-27", "single");
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = Guid.NewGuid(), planId = single, paymentMethod = "linepay", amount = 1, paidOn }))).StatusCode);

            using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
            var bwPlan = await B1Test.PlanIdAsync("bw", "2025", "single");
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsync("/api/v1/admin/bw/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = bwPlan, paymentMethod = "linepay", amount = 1, paidOn }))).StatusCode);
            // 別的俱樂部的方案 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = bwPlan, paymentMethod = "linepay", amount = 1, paidOn }))).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
            if (planId is not null)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/membership-plans/{planId}");
            }
        }
    }

    [Fact]
    public async Task 調整_原因必填_取消會停用會員卡_家庭方案副卡不超過發卡上限()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "adjust");
        try
        {
            var family = await B1Test.PlanIdAsync("tcrfc", "2026-27", "family");
            var paidOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            var activated = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships/activate",
                BizTest.Json(new { memberId = member.Id, planId = family, paymentMethod = "linepay", amount = 3000, paidOn })));
            var id = activated.Membership.MembershipId;
            Assert.Equal(3, activated.CardQuota);
            Assert.Equal(3, activated.JerseyQuota);

            // 副卡：發到上限為止
            for (var i = 0; i < 2; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await service.PostAsync($"/api/v1/admin/tcrfc/memberships/{id}/cards", BizTest.Json(new { holderName = $"【測試】副卡{i}" }))).StatusCode);
            }

            Assert.Equal(HttpStatusCode.Conflict, (await service.PostAsync($"/api/v1/admin/tcrfc/memberships/{id}/cards", BizTest.Json(new { holderName = "第四張" }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync($"/api/v1/admin/tcrfc/memberships/{id}/cards", BizTest.Json(new { holderName = "" }))).StatusCode);

            // 調整：沒有原因 400；沒有任何要改的 400
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust", BizTest.Json(new { tier = "registered", reason = "" }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust", BizTest.Json(new { reason = "沒有要改的" }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust", BizTest.Json(new { tier = "gold", reason = "x" }))).StatusCode);

            var downgraded = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust",
                BizTest.Json(new { tier = "registered", reason = "【測試】降級" })));
            Assert.Equal("registered", downgraded.Membership.Tier);
            Assert.Equal("【測試】降級", downgraded.LastAdjustReason);

            var cancelled = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust",
                BizTest.Json(new { status = "cancelled", reason = "【測試】取消" })));
            Assert.Equal("已取消", cancelled.Membership.EffectiveStatusLabel);
            Assert.All(cancelled.Cards, c => Assert.Equal("revoked", c.Status));

            // 起訖顛倒
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust",
                BizTest.Json(new { startOn = "2027-01-01", endOn = "2026-01-01", reason = "x" }))).StatusCode);
            // 停用單張卡
            Assert.Equal(HttpStatusCode.NotFound, (await service.PostAsync($"/api/v1/admin/tcrfc/memberships/{id}/cards/{Guid.NewGuid()}/revoke", null)).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 批次到期_先試算再執行_只動符合基準日的會籍()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var member = await B1Test.CreateMemberAsync(service, "tcrfc", "expire");
        try
        {
            var season = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
            var membership = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships", BizTest.Json(new { memberId = member.Id, seasonId = season })));
            var id = membership.Membership.MembershipId;
            // 把這份測試會籍的到期日改到 2019（遠早於任何種子），基準日設 2020，其餘資料一份都不會被波及。
            Assert.Equal(HttpStatusCode.OK, (await service.PutAsync($"/api/v1/admin/tcrfc/memberships/{id}/adjust",
                BizTest.Json(new { startOn = "2019-01-01", endOn = "2019-12-31", reason = "【測試】造出已過期會籍" }))).StatusCode);

            var dry = await BizTest.ReadAsync<ExpireBatchResultDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships/expire-batch",
                BizTest.Json(new { asOf = "2020-01-01", dryRun = true })));
            Assert.True(dry.DryRun);
            Assert.Equal(1, dry.Count);
            var stillActive = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.GetAsync($"/api/v1/admin/tcrfc/memberships/{id}"));
            Assert.Equal("active", stillActive.Membership.Status);

            var real = await BizTest.ReadAsync<ExpireBatchResultDto>(await service.PostAsync("/api/v1/admin/tcrfc/memberships/expire-batch",
                BizTest.Json(new { asOf = "2020-01-01" })));
            Assert.Equal(1, real.Count);
            var expired = await BizTest.ReadAsync<AdminMembershipDetailDto>(await service.GetAsync($"/api/v1/admin/tcrfc/memberships/{id}"));
            Assert.Equal("expired", expired.Membership.Status);

            // 沒有授權的俱樂部不能對它批次到期
            Assert.Equal(HttpStatusCode.Forbidden, (await service.PostAsync("/api/v1/admin/bw/memberships/expire-batch", BizTest.Json(new { dryRun = true }))).StatusCode);
        }
        finally
        {
            await B1Test.DeleteMembersAsync(member.Id);
        }
    }

    [Fact]
    public async Task 會籍清單_篩選與到期提醒排序_名單不含個資明文()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var page = await BizTest.ReadAsync<PagedResult<AdminMembershipListItemDto>>(await service.GetAsync("/api/v1/admin/tcrfc/memberships?tier=fan_club&pageSize=100"));
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, i => Assert.Equal("fan_club", i.Tier));
        var body = await (await service.GetAsync("/api/v1/admin/tcrfc/memberships?keyword=M900001")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("member-a@example.com", body);
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/memberships?status=oops")).StatusCode);

        // 只看得到自己授權的俱樂部
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        var bw = await BizTest.ReadAsync<PagedResult<AdminMembershipListItemDto>>(await partner.GetAsync("/api/v1/admin/bw/memberships?pageSize=100"));
        Assert.DoesNotContain(bw.Items, i => i.SeasonCode == "2026-27");
        // 合作球隊管理只有檢視，沒有開通
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsync("/api/v1/admin/bw/memberships/expire-batch", BizTest.Json(new { dryRun = true }))).StatusCode);
    }

    [Fact]
    public async Task 續會名單匯出_需要匯出權限與用途()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/memberships/renewal-export?kind=expiring&days=400&purpose=x")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync("/api/v1/admin/tcrfc/memberships/renewal-export")).StatusCode);
        var response = await service.GetAsync("/api/v1/admin/tcrfc/memberships/renewal-export?kind=expired&purpose=%E7%BA%8C%E6%9C%83%E5%90%8D%E5%96%AE");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("剩餘天數", csv);

        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/admin/tcrfc/memberships/renewal-export?purpose=x")).StatusCode);
    }

    // ═════════════ 會員編號規則 ═════════════

    [Fact]
    public async Task 會員編號規則_修改後新建會員採新規則_驗證格式()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var original = await BizTest.ReadAsync<AdminMemberSettingsDto>(await admin.GetAsync("/api/v1/admin/tcrfc/member-settings"));
        Guid? memberId = null;
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/v1/admin/tcrfc/member-settings", BizTest.Json(new { memberNoPrefix = "TOO-LONG-PREFIX", memberNoDigits = 6 }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/v1/admin/tcrfc/member-settings", BizTest.Json(new { memberNoPrefix = "T", memberNoDigits = 2 }))).StatusCode);

            var changed = await BizTest.ReadAsync<AdminMemberSettingsDto>(await admin.PutAsync("/api/v1/admin/tcrfc/member-settings", BizTest.Json(new { memberNoPrefix = "TB", memberNoDigits = 5 })));
            Assert.Equal("TB00001", changed.NextMemberNoPreview);
            var member = await B1Test.CreateMemberAsync(admin, "tcrfc", "numbering");
            memberId = member.Id;
            Assert.Matches("^TB[0-9]{5}$", member.MemberNo);

            using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
            Assert.Equal(HttpStatusCode.OK, (await service.GetAsync("/api/v1/admin/tcrfc/member-settings")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await service.PutAsync("/api/v1/admin/tcrfc/member-settings", BizTest.Json(new { memberNoPrefix = "X", memberNoDigits = 6 }))).StatusCode);
        }
        finally
        {
            await admin.PutAsync("/api/v1/admin/tcrfc/member-settings", BizTest.Json(new { memberNoPrefix = original.MemberNoPrefix, memberNoDigits = original.MemberNoDigits }));
            if (memberId is Guid id)
            {
                await B1Test.DeleteMembersAsync(id);
            }
        }
    }
}
