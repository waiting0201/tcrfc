using System.Net;
using System.Text;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>E4 廣告主與版位、E5 投放檔期與素材（狀態機、審核、緊急暫停、衝突檢視）、E6 成效報表與維護作業（App 規劃書 §7、§8.7–8.9）。
/// 素材圖片上傳（對真實 Azurite）在 <see cref="AdminAdCreativeUploadTests"/>。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminAdsTests(AdminWriteApiFixture fixture)
{
    private const string Ads = "/api/v1/admin/ads";

    private static string Iso(TimeSpan fromNow) => DateTimeOffset.UtcNow.Add(fromNow).ToString("O");

    internal static async Task<AdminAdSlotDto> CreateSlotAsync(HttpClient client, string code, int rotationCap = 1, string? screen = "S01", bool allowVideo = false, string? ratio = null, int? minW = null, int? minH = null, int? maxKb = null)
    {
        var response = await client.PostAsync(Ads + "/slots", BizTest.Multipart(new
        {
            slotCode = code, screenCode = screen, aspectRatio = ratio, minWidth = minW, minHeight = minH, maxFileKb = maxKb, allowVideo, rotationCap, isActive = true,
            content = new { zh = new { name = "ZZTEST 版位 " + code, fallbackAlt = "自家內容" } },
        }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AppTest.ReadAsync<AdminAdSlotDto>(response);
    }

    internal static async Task<AdminAdvertiserDto> CreateAdvertiserAsync(HttpClient client, string name = "ZZTEST 廣告主", string? status = "active")
    {
        var response = await AppTest.PostJsonAsync(client, Ads + "/advertisers", new
        {
            status, contactName = "測試聯絡人", contactEmail = "zz@example.test", content = new { zh = new { name }, en = new { name = name + " EN" } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AppTest.ReadAsync<AdminAdvertiserDto>(response);
    }

    internal static async Task<HttpResponseMessage> PostCampaignAsync(HttpClient client, Guid advertiserId, Guid slotId, TimeSpan start, TimeSpan end, object? extra = null, string name = "ZZTEST 檔期")
    {
        var body = new Dictionary<string, object?>
        {
            ["advertiserId"] = advertiserId, ["slotId"] = slotId, ["name"] = name, ["startsAt"] = Iso(start), ["endsAt"] = Iso(end), ["weight"] = 1,
        };
        if (extra is not null)
        {
            foreach (var p in extra.GetType().GetProperties())
            {
                body[p.Name] = p.GetValue(extra);
            }
        }

        return await AppTest.PostJsonAsync(client, Ads + "/campaigns", body);
    }

    internal static async Task<AdminAdCampaignDetailDto> CreateCampaignAsync(HttpClient client, Guid advertiserId, Guid slotId, TimeSpan start, TimeSpan end, object? extra = null, string name = "ZZTEST 檔期")
    {
        var response = await PostCampaignAsync(client, advertiserId, slotId, start, end, extra, name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AppTest.ReadAsync<AdminAdCampaignDetailDto>(response);
    }

    /// <summary>建立一個「投放中」的檔期（含一個已通過審核的素材），走真實的送審與核可流程。</summary>
    internal static async Task<(AdminAdCampaignDetailDto Campaign, Guid CreativeId)> StartRunningAsync(
        HttpClient client, Guid advertiserId, Guid slotId, object? extra = null, string name = "ZZTEST 投放中", TimeSpan? start = null, TimeSpan? end = null, string locale = "zh-Hant")
    {
        var campaign = await CreateCampaignAsync(client, advertiserId, slotId, start ?? -TimeSpan.FromHours(1), end ?? TimeSpan.FromDays(3), extra, name);
        var creative = await AppTest.InsertCreativeAsync(campaign.Id, locale);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(client, campaign.Id, "submit")).StatusCode);
        var running = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(client, campaign.Id, "approve"));
        Assert.Equal("running", running.Status);
        return (running, creative);
    }

    private static async Task<AdminAdCampaignDetailDto> GetCampaignAsync(HttpClient client, Guid id)
        => await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await client.GetAsync($"{Ads}/campaigns/{id}"));

    internal static Task<HttpResponseMessage> ActAsync(HttpClient client, Guid id, string action, object? body = null)
        => AppTest.PostJsonAsync(client, $"{Ads}/campaigns/{id}/{action}", body ?? new { });

    // ═════════════ E4 版位 ═════════════

    [Fact]
    public async Task 版位_兒童向畫面與慈善版位不能建_代號建立後不能改_有檔期不能刪_權限矩陣()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_home_top", rotationCap: 2);
            Assert.Equal("app", slot.Surface);
            Assert.Equal("ZZTEST 版位 zztest_home_top", slot.NameZh);

            // 兒童向畫面（S15 課程列表、S16 課程報名表、S17 我的報名）不設版位
            var child = await biz.PostAsync(Ads + "/slots", BizTest.Multipart(new { slotCode = "zztest_program_top", screenCode = "S15", rotationCap = 1, content = new { zh = new { name = "x" } } }));
            Assert.Equal(HttpStatusCode.BadRequest, child.StatusCode);
            Assert.Contains("兒童向", await child.Content.ReadAsStringAsync());
            // 慈善相關版位一律不做
            var charity = await biz.PostAsync(Ads + "/slots", BizTest.Multipart(new { slotCode = "zztest_charity_top", screenCode = "S01", rotationCap = 1, content = new { zh = new { name = "x" } } }));
            Assert.Equal(HttpStatusCode.BadRequest, charity.StatusCode);
            // 格式、重複、輪播上限
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.PostAsync(Ads + "/slots", BizTest.Multipart(new { slotCode = "NoUnderscore", rotationCap = 1, content = new { zh = new { name = "x" } } }))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await biz.PostAsync(Ads + "/slots", BizTest.Multipart(new { slotCode = "zztest_home_top", rotationCap = 1, content = new { zh = new { name = "x" } } }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.PostAsync(Ads + "/slots", BizTest.Multipart(new { slotCode = "zztest_big_cap", rotationCap = 11, content = new { zh = new { name = "x" } } }))).StatusCode);

            // 代號不能改
            var rename = await biz.PutAsync($"{Ads}/slots/{slot.Id}", BizTest.Multipart(new { slotCode = "zztest_other", rotationCap = 2, isActive = true, content = new { zh = new { name = "改名" } } }));
            Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
            var renamed = await AppTest.ReadAsync<AdminAdSlotDto>(await biz.PutAsync($"{Ads}/slots/{slot.Id}", BizTest.Multipart(new { slotCode = "zztest_home_top", rotationCap = 3, isActive = true, content = new { zh = new { name = "ZZTEST 改名" } } })));
            Assert.Equal(3, renamed.RotationCap);
            Assert.Equal("ZZTEST 改名", renamed.NameZh);

            // 權限：檢視者只能看、公關／媒體與合作球隊管理完全沒有
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Ads + "/slots")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync(Ads + "/slots", BizTest.Multipart(new { slotCode = "zztest_v", rotationCap = 1, content = new { zh = new { name = "x" } } }))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync(Ads + "/slots")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync(Ads + "/slots")).StatusCode);

            // 有檔期不能刪
            var adv = await CreateAdvertiserAsync(biz);
            await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(3));
            var del = await biz.DeleteAsync($"{Ads}/slots/{slot.Id}");
            Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
            var empty = await CreateSlotAsync(biz, "zztest_empty");
            Assert.Equal(HttpStatusCode.NoContent, (await biz.DeleteAsync($"{Ads}/slots/{empty.Id}")).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 廣告主_關聯贊助商_有檔期不能刪_狀態與格式驗證()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        try
        {
            var options = await AppTest.ReadAsync<List<AdminSponsorOptionDto>>(await biz.GetAsync(Ads + "/sponsor-options"));
            var sponsorId = options.FirstOrDefault()?.Id;
            var response = await AppTest.PostJsonAsync(biz, Ads + "/advertisers", new { status = "negotiating", sponsorId, content = new { zh = new { name = "ZZTEST 關聯廣告主" } } });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var adv = await AppTest.ReadAsync<AdminAdvertiserDto>(response);
            Assert.Equal("洽談中", adv.StatusLabel);
            if (sponsorId is not null)
            {
                Assert.Equal(sponsorId, adv.SponsorId);
            }

            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(biz, Ads + "/advertisers", new { sponsorId = Guid.NewGuid(), content = new { zh = new { name = "ZZTEST x" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(biz, Ads + "/advertisers", new { status = "unknown", content = new { zh = new { name = "ZZTEST x" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await AppTest.PostJsonAsync(biz, Ads + "/advertisers", new { contactEmail = "bad", content = new { zh = new { name = "ZZTEST x" } } })).StatusCode);

            var updated = await AppTest.ReadAsync<AdminAdvertiserDto>(await AppTest.PutJsonAsync(biz, $"{Ads}/advertisers/{adv.Id}", new { status = "active", content = new { zh = new { name = "ZZTEST 改名" }, en = new { name = "ZZTEST Renamed" } } }));
            Assert.Equal("合作中", updated.StatusLabel);
            Assert.Equal("ZZTEST Renamed", updated.NameEn);

            var slot = await CreateSlotAsync(biz, "zztest_adv_slot");
            await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(2));
            Assert.Equal(HttpStatusCode.Conflict, (await biz.DeleteAsync($"{Ads}/advertisers/{adv.Id}")).StatusCode);
            var list = await AppTest.ReadAsync<List<AdminAdvertiserDto>>(await biz.GetAsync(Ads + "/advertisers?keyword=ZZTEST"));
            Assert.Equal(1, list.Single(a => a.Id == adv.Id).CampaignCount);

            // 合作已結束的廣告主不能再建新檔期
            var ended = await AppTest.ReadAsync<AdminAdvertiserDto>(await AppTest.PutJsonAsync(biz, $"{Ads}/advertisers/{adv.Id}", new { status = "ended", content = new { zh = new { name = "ZZTEST 改名" } } }));
            Assert.Equal(HttpStatusCode.BadRequest, (await PostCampaignAsync(biz, ended.Id, slot.Id, TimeSpan.FromDays(5), TimeSpan.FromDays(6))).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    // ═════════════ E5 檔期 ═════════════

    [Fact]
    public async Task 檔期_合約金額只有授權角色看得到_其他角色見不公開_沒有編輯權限送金額403()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_amount");
            var adv = await CreateAdvertiserAsync(biz);
            var created = await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(9), new { contractAmount = 30000, isAmountHidden = true });
            Assert.Equal(30000, created.ContractAmount);
            Assert.Equal("NT$ 30,000", created.ContractAmountLabel);

            var asViewer = await GetCampaignAsync(viewer, created.Id);
            Assert.Null(asViewer.ContractAmount);
            Assert.Equal("不公開", asViewer.ContractAmountLabel);
            Assert.Null(asViewer.IsAmountHidden);
            Assert.DoesNotContain("30000", await (await viewer.GetAsync($"{Ads}/campaigns/{created.Id}")).Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync($"{Ads}/campaigns/{created.Id}")).StatusCode); // 公關／媒體只有報表
            Assert.Equal(HttpStatusCode.Forbidden, (await PostCampaignAsync(viewer, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(2))).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 檔期狀態機_送審需素材_核可需已審素材_自動推進_緊急暫停恢復_作廢不可逆()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_flow");
            var adv = await CreateAdvertiserAsync(biz);
            var campaign = await CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromHours(1), TimeSpan.FromDays(2));
            Assert.Equal("draft", campaign.Status);
            Assert.Contains("submit", campaign.AvailableActions);
            AdminAdCampaignDetailDto viewerView = await GetCampaignAsync(viewer, campaign.Id);
            Assert.Empty(viewerView.AvailableActions); // 檢視者看不到任何操作按鈕

            Assert.Equal(HttpStatusCode.Conflict, (await ActAsync(biz, campaign.Id, "submit")).StatusCode); // 還沒有素材
            var pending = await AppTest.InsertCreativeAsync(campaign.Id, reviewStatus: "pending");
            var submitted = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, campaign.Id, "submit"));
            Assert.Equal("pending_review", submitted.Status);

            // 🔴 素材未通過審核的檔期不得排程或投放
            var refused = await ActAsync(biz, campaign.Id, "approve");
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Contains("審核", await refused.Content.ReadAsStringAsync());

            // 素材核可（被退回的要先修改）
            var approvedCreative = await AppTest.ReadAsync<AdminAdCreativeDto>(await ActAsync2(biz, $"{Ads}/creatives/{pending}/approve"));
            Assert.Equal("approved", approvedCreative.ReviewStatus);
            Assert.Equal(HttpStatusCode.Conflict, (await ActAsync2(biz, $"{Ads}/creatives/{pending}/approve")).StatusCode);

            // 核可後排程；開始時間已過，於是立刻被自動推進為投放中
            var approved = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, campaign.Id, "approve"));
            Assert.Equal("running", approved.Status);
            Assert.NotNull(approved.ReviewedAt);

            // 緊急暫停：原因必填
            Assert.Equal(HttpStatusCode.BadRequest, (await ActAsync(biz, campaign.Id, "pause")).StatusCode);
            var paused = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, campaign.Id, "pause", new { reason = "廣告主申訴素材爭議" }));
            Assert.Equal("paused", paused.Status);
            Assert.Equal("廣告主申訴素材爭議", paused.PauseReason);
            Assert.Contains("resume", paused.AvailableActions);

            // 恢復需要「通過審核且未暫停」的素材
            await ActAsync2(biz, $"{Ads}/creatives/{pending}/pause");
            Assert.Equal(HttpStatusCode.Conflict, (await ActAsync(biz, campaign.Id, "resume")).StatusCode);
            await ActAsync2(biz, $"{Ads}/creatives/{pending}/resume");
            Assert.Equal("running", (await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, campaign.Id, "resume"))).Status);

            // 作廢：原因必填、不可逆、之後不能修改
            Assert.Equal(HttpStatusCode.BadRequest, (await ActAsync(biz, campaign.Id, "void")).StatusCode);
            var voided = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, campaign.Id, "void", new { reason = "合約取消" }));
            Assert.Equal("voided", voided.Status);
            Assert.Empty(voided.AvailableActions);
            Assert.Equal(HttpStatusCode.Conflict, (await ActAsync(biz, campaign.Id, "void", new { reason = "again" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await ActAsync(biz, campaign.Id, "resume")).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    private static Task<HttpResponseMessage> ActAsync2(HttpClient client, string url) => AppTest.PostJsonAsync(client, url, new { });

    [Fact]
    public async Task 檔期_結束時間已過自動結束_可結案_已排程與投放中只能調整名稱權重與上限_草稿才能刪除()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_edit");
            var adv = await CreateAdvertiserAsync(biz);

            // 已經過期的檔期：核可後立刻被推進為已結束，再結案
            var past = await CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromDays(3), -TimeSpan.FromHours(2), name: "ZZTEST 過期");
            await AppTest.InsertCreativeAsync(past.Id);
            await ActAsync(biz, past.Id, "submit");
            var ended = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, past.Id, "approve"));
            Assert.Equal("ended", ended.Status);
            var closed = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, past.Id, "close"));
            Assert.Equal("closed", closed.Status);

            // 投放中的檔期
            var running = await CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromHours(1), TimeSpan.FromDays(5), new { dailyImpressionCap = 100 }, name: "ZZTEST 投放中");
            await AppTest.InsertCreativeAsync(running.Id);
            await ActAsync(biz, running.Id, "submit");
            Assert.Equal("running", (await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await ActAsync(biz, running.Id, "approve"))).Status);

            var otherSlot = await CreateSlotAsync(biz, "zztest_edit2");
            var changeSlot = await AppTest.PutJsonAsync(biz, $"{Ads}/campaigns/{running.Id}", new
            {
                advertiserId = adv.Id, slotId = otherSlot.Id, name = "ZZTEST 投放中", startsAt = running.StartsAt, endsAt = running.EndsAt, weight = 1,
            });
            Assert.Equal(HttpStatusCode.Conflict, changeSlot.StatusCode);
            var tweak = await AppTest.ReadAsync<AdminAdCampaignDetailDto>(await AppTest.PutJsonAsync(biz, $"{Ads}/campaigns/{running.Id}", new
            {
                advertiserId = adv.Id, slotId = slot.Id, name = "ZZTEST 改名", startsAt = running.StartsAt, endsAt = running.EndsAt, weight = 7, dailyImpressionCap = 500, perDeviceDailyCap = 3,
            }));
            Assert.Equal("ZZTEST 改名", tweak.Name);
            Assert.Equal(7, tweak.Weight);
            Assert.Equal(500, tweak.DailyImpressionCap);

            // 刪除：只有草稿
            Assert.Equal(HttpStatusCode.Conflict, (await biz.DeleteAsync($"{Ads}/campaigns/{running.Id}")).StatusCode);
            var draft = await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(10), TimeSpan.FromDays(11), name: "ZZTEST 草稿");
            Assert.Equal(HttpStatusCode.NoContent, (await biz.DeleteAsync($"{Ads}/campaigns/{draft.Id}")).StatusCode);

            // 欄位驗證
            Assert.Equal(HttpStatusCode.BadRequest, (await PostCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(2), TimeSpan.FromDays(1))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(2), new { weight = 101 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(2), new { goalType = "guaranteed" })).StatusCode); // 曝光保證必須填目標
            Assert.Equal(HttpStatusCode.BadRequest, (await PostCampaignAsync(biz, Guid.NewGuid(), slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(2))).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 衝突檢視_同版位同時段的檔期與權重佔比_超過輪播上限只提示不擋存檔_停用版位不能排檔期()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_sched", rotationCap: 1);
            var adv = await CreateAdvertiserAsync(biz);
            var a = await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(1), TimeSpan.FromDays(10), new { weight = 1 }, "ZZTEST A");
            var b = await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(3), TimeSpan.FromDays(12), new { weight = 3 }, "ZZTEST B");
            var later = await CreateCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(40), TimeSpan.FromDays(41), new { weight = 1 }, "ZZTEST C");
            // 草稿不進衝突檢視（還沒有送審）；送審後才算
            var empty = await AppTest.ReadAsync<AdminAdScheduleDto>(await biz.GetAsync($"{Ads}/slots/{slot.Id}/schedule?from={Uri.EscapeDataString(Iso(TimeSpan.Zero))}&to={Uri.EscapeDataString(Iso(TimeSpan.FromDays(30)))}"));
            Assert.Empty(empty.Items);

            foreach (var c in new[] { a, b, later })
            {
                await AppTest.InsertCreativeAsync(c.Id, reviewStatus: "approved");
                await ActAsync(biz, c.Id, "submit");
            }

            var schedule = await AppTest.ReadAsync<AdminAdScheduleDto>(await biz.GetAsync($"{Ads}/slots/{slot.Id}/schedule?from={Uri.EscapeDataString(Iso(TimeSpan.Zero))}&to={Uri.EscapeDataString(Iso(TimeSpan.FromDays(30)))}"));
            Assert.Equal(2, schedule.Items.Count); // C 在 40 天後，不在期間內
            Assert.Equal(25, schedule.Items.Single(i => i.CampaignId == a.Id).WeightSharePercent);
            Assert.Equal(75, schedule.Items.Single(i => i.CampaignId == b.Id).WeightSharePercent);
            Assert.Equal(2, schedule.MaxConcurrent);
            Assert.True(schedule.ExceedsRotationCap);
            Assert.Equal(HttpStatusCode.NotFound, (await biz.GetAsync($"{Ads}/slots/{Guid.NewGuid()}/schedule")).StatusCode);

            // 停用的版位不能排新檔期
            await biz.PutAsync($"{Ads}/slots/{slot.Id}", BizTest.Multipart(new { slotCode = "zztest_sched", rotationCap = 1, isActive = false, content = new { zh = new { name = "ZZTEST 停用" } } }));
            Assert.Equal(HttpStatusCode.BadRequest, (await PostCampaignAsync(biz, adv.Id, slot.Id, TimeSpan.FromDays(50), TimeSpan.FromDays(51))).StatusCode);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    // ═════════════ E6 報表與維護 ═════════════

    [Fact]
    public async Task 報表_維度與點擊率_不重複裝置_pacing_匯出須權限與用途_公關只能看不能匯出()
    {
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_report");
            var adv = await CreateAdvertiserAsync(biz);
            var campaign = await CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromDays(4), TimeSpan.FromDays(6), new { goalType = "guaranteed", goalImpressions = 1000 });
            var creative = await AppTest.InsertCreativeAsync(campaign.Id);
            await AppTest.InsertCreativeAsync(campaign.Id, "en");
            await ActAsync(biz, campaign.Id, "submit");
            await ActAsync(biz, campaign.Id, "approve");
            var today = Tcrfc.Api.Common.TaiwanClock.Today;
            foreach (var (day, platform, imp, clk, uniq) in new[] { (today.AddDays(-2), "ios", 100, 4, 80), (today.AddDays(-2), "android", 50, 1, 40), (today.AddDays(-1), "ios", 200, 10, 150) })
            {
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO ad_daily_stats (stat_date, campaign_id, creative_id, slot_id, platform, locale, impressions, clicks, unique_devices) VALUES (@D, @C, @R, @S, @P, N'zh-Hant', @I, @K, @U)",
                    ("@D", day.ToDateTime(TimeOnly.MinValue)), ("@C", campaign.Id), ("@R", creative), ("@S", slot.Id), ("@P", platform), ("@I", imp), ("@K", clk), ("@U", uniq));
            }

            var from = today.AddDays(-3).ToString("yyyy-MM-dd");
            var to = today.ToString("yyyy-MM-dd");
            var byPlatform = await AppTest.ReadAsync<AdminAdReportDto>(await pr.GetAsync($"{Ads}/reports?campaignId={campaign.Id}&from={from}&to={to}&groupBy=platform"));
            var ios = byPlatform.Rows.Single(r => r.Label == "iOS");
            Assert.Equal(300, ios.Impressions);
            Assert.Equal(14, ios.Clicks);
            Assert.Equal(4.67m, ios.Ctr);
            Assert.Equal(230, ios.UniqueDevices);
            Assert.Equal(350, byPlatform.Total.Impressions);
            Assert.Equal(15, byPlatform.Total.Clicks);
            Assert.Equal(4.29m, byPlatform.Total.Ctr);

            var byDate = await AppTest.ReadAsync<AdminAdReportDto>(await biz.GetAsync($"{Ads}/reports?campaignId={campaign.Id}&from={from}&to={to}&groupBy=date"));
            Assert.Equal(2, byDate.Rows.Count);
            Assert.Equal(today.AddDays(-2).ToString("yyyy-MM-dd"), byDate.Rows[0].Label); // 依日期由舊到新

            // 曝光保證型附「目標 vs 已達成」
            var pacing = Assert.Single(byPlatform.Pacing);
            Assert.Equal(1000, pacing.Pacing.GoalImpressions);
            Assert.Equal(100, pacing.Pacing.DailyTarget);

            // 個人層級資料一律不提供：回應只有彙總欄位
            var raw = await (await biz.GetAsync($"{Ads}/reports?campaignId={campaign.Id}&from={from}&to={to}")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("deviceInstallId", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("memberId", raw, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(HttpStatusCode.BadRequest, (await biz.GetAsync($"{Ads}/reports?groupBy=nope")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.GetAsync($"{Ads}/reports?from=2020-01-01&to=2026-01-01")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.GetAsync($"{Ads}/reports?from={to}&to={from}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync($"{Ads}/reports")).StatusCode);

            // 匯出：公關／媒體只能看不能匯；用途必填；CSV 含 BOM
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync($"{Ads}/reports/export?campaignId={campaign.Id}&purpose=x")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await biz.GetAsync($"{Ads}/reports/export?campaignId={campaign.Id}")).StatusCode);
            var csv = await biz.GetAsync($"{Ads}/reports/export?campaignId={campaign.Id}&from={from}&to={to}&purpose={Uri.EscapeDataString("寄送廣告主月報")}");
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
            Assert.StartsWith("﻿項目,曝光數,點擊數,點擊率（%）,不重複裝置數", text);
            Assert.Contains("合計,350,15,4.29,", text);
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }

    [Fact]
    public async Task 維護作業_聚合原始事件成日聚合_清除九十天前已聚合事件_未聚合逾期告警不清除_只有系統管理員能手動執行()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var biz = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        try
        {
            var slot = await CreateSlotAsync(biz, "zztest_maint");
            var adv = await CreateAdvertiserAsync(biz);
            var campaign = await CreateCampaignAsync(biz, adv.Id, slot.Id, -TimeSpan.FromDays(1), TimeSpan.FromDays(3));
            var creative = await AppTest.InsertCreativeAsync(campaign.Id);
            var now = DateTime.UtcNow;
            async Task Insert(string type, string device, DateTime at, DateTime? aggregated, string key)
                => await BizTest.ExecuteSqlAsync(
                    "INSERT INTO ad_events (event_type, creative_id, campaign_id, slot_id, occurred_at, received_at, device_install_id, platform, locale, dedupe_key, aggregated_at) VALUES (@T, @R, @C, @S, @A, @A, @D, N'ios', N'zh-Hant', @K, @G)",
                    ("@T", type), ("@R", creative), ("@C", campaign.Id), ("@S", slot.Id), ("@A", at), ("@D", device), ("@K", key.PadRight(32, '0')[..32]), ("@G", aggregated));

            await Insert("impression", "test-dev-aaaa1111", now.AddMinutes(-30), null, "zzt1");
            await Insert("impression", "test-dev-bbbb2222", now.AddMinutes(-20), null, "zzt2");
            await Insert("click", "test-dev-aaaa1111", now.AddMinutes(-10), null, "zzt3");
            await Insert("impression", "test-dev-old00001", now.AddDays(-100), now.AddDays(-99), "zzt4"); // 已聚合且超過 90 天 → 清除
            await Insert("impression", "test-dev-old00002", now.AddDays(-100), null, "zzt5"); // 未聚合且超過 90 天 → 聚合失敗的警訊，不清除

            Assert.Equal(HttpStatusCode.Forbidden, (await AppTest.PostJsonAsync(biz, Ads + "/maintenance/run", new { })).StatusCode);
            var result = await AppTest.ReadAsync<AdMaintenanceResultDto>(await AppTest.PostJsonAsync(admin, Ads + "/maintenance/run", new { }));
            Assert.True(result.EventsAggregated >= 3);
            Assert.True(result.EventsPurged >= 1);
            Assert.True(result.OverdueUnaggregated >= 1); // 聚合落後必須告警，不得靜默跳過

            var stat = await BizTest.ScalarGuidAsync("SELECT TOP 1 creative_id FROM ad_daily_stats WHERE campaign_id = @C AND stat_date = @D", ("@C", campaign.Id), ("@D", Tcrfc.Api.Common.TaiwanClock.ToDate(now.AddMinutes(-30)).ToDateTime(TimeOnly.MinValue)));
            Assert.Equal(creative, stat);
            Assert.Equal(2, await C1Test.ScalarAsync<int>("SELECT SUM(impressions) FROM ad_daily_stats WHERE campaign_id = @C AND stat_date = @D", ("@C", campaign.Id), ("@D", Tcrfc.Api.Common.TaiwanClock.ToDate(now.AddMinutes(-30)).ToDateTime(TimeOnly.MinValue))));
            Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT SUM(clicks) FROM ad_daily_stats WHERE campaign_id = @C AND stat_date = @D", ("@C", campaign.Id), ("@D", Tcrfc.Api.Common.TaiwanClock.ToDate(now.AddMinutes(-30)).ToDateTime(TimeOnly.MinValue))));
            Assert.Equal(2, await C1Test.ScalarAsync<int>("SELECT MAX(unique_devices) FROM ad_daily_stats WHERE campaign_id = @C AND stat_date = @D", ("@C", campaign.Id), ("@D", Tcrfc.Api.Common.TaiwanClock.ToDate(now.AddMinutes(-30)).ToDateTime(TimeOnly.MinValue))));
            Assert.Equal(0, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM ad_events WHERE campaign_id = @C AND dedupe_key = @K", ("@C", campaign.Id), ("@K", "zzt4".PadRight(32, '0')[..32])));
            Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM ad_events WHERE campaign_id = @C AND dedupe_key = @K AND aggregated_at IS NOT NULL", ("@C", campaign.Id), ("@K", "zzt3".PadRight(32, '0')[..32])));

            // 冪等：再跑一次不重複計數
            await AppTest.PostJsonAsync(admin, Ads + "/maintenance/run", new { });
            Assert.Equal(2, await C1Test.ScalarAsync<int>("SELECT SUM(impressions) FROM ad_daily_stats WHERE campaign_id = @C AND stat_date = @D", ("@C", campaign.Id), ("@D", Tcrfc.Api.Common.TaiwanClock.ToDate(now.AddMinutes(-30)).ToDateTime(TimeOnly.MinValue))));
        }
        finally
        {
            await AppTest.CleanupAdsAsync();
        }
    }
}
