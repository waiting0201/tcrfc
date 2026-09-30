using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminHonors;
using Tcrfc.Api.Features.AdminPress;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>B6 媒體專區與 C5 榮譽與里程碑後台 API（不含檔案／圖片上傳，見 <c>AdminBusinessUploadTests</c>）。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPressAndHonorsTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════ B6 媒體專區 ═════════════════════════

    [Fact]
    public async Task 媒體資源_授權_必須上傳檔案_類別驗證()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/tcrfc/press-resources")).StatusCode);

        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/admin/tcrfc/press-resources")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/admin/tcrfc/press-resources",
            BizTest.Multipart(NewPress("press_release")))).StatusCode);

        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        // 沒有檔案 → 400（發生在碰儲存體之前）
        var noFile = await pr.PostAsync("/api/v1/admin/tcrfc/press-resources", BizTest.Multipart(NewPress("press_release")));
        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
        Assert.Contains("檔案", await noFile.Content.ReadAsStringAsync());
        // 類別不合法 → 400
        Assert.Equal(HttpStatusCode.BadRequest, (await pr.PostAsync("/api/v1/admin/tcrfc/press-resources",
            BizTest.Multipart(NewPress("video"), ("file", BizTest.Pdf(), "a.pdf", "application/pdf")))).StatusCode);
        // 篩選參數不合法 → 400
        Assert.Equal(HttpStatusCode.BadRequest, (await pr.GetAsync("/api/v1/admin/tcrfc/press-resources?resourceType=foo")).StatusCode);
    }

    [Fact]
    public async Task 媒體資源_共同列唯讀_批次操作略過共同列與不存在的資源()
    {
        var sharedId = Guid.NewGuid();
        var slug = BizTest.Unique("shared-press");
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO press_resources (id, club_id, slug, resource_type, file_key, status) VALUES (@Id, NULL, @Slug, N'press_release', N'seed/none', N'draft'); " +
            "INSERT INTO press_resources_i18n (press_resource_id, locale, title) VALUES (@Id, N'zh-Hant', N'【測試】共同新聞稿');",
            ("@Id", sharedId), ("@Slug", slug));
        try
        {
            using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
            var list = await BizTest.ReadAsync<PagedResult<AdminPressListItemDto>>(await editor.GetAsync("/api/v1/admin/tcrfc/press-resources?pageSize=100"));
            Assert.Contains(list.Items, r => r.Id == sharedId && r.IsShared);

            Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsync($"/api/v1/admin/tcrfc/press-resources/{sharedId}",
                BizTest.Multipart(NewPress("press_release")))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.DeleteAsync($"/api/v1/admin/tcrfc/press-resources/{sharedId}")).StatusCode);

            var missing = Guid.NewGuid();
            var result = await BizTest.ReadAsync<BatchOperationResultDto>(await editor.PostAsync(
                "/api/v1/admin/tcrfc/press-resources/batch/show", BizTest.Json(new { ids = new[] { sharedId, missing } })));
            Assert.Equal(0, result.UpdatedCount);
            Assert.Equal(2, result.Skipped.Count);
            Assert.Contains(result.Skipped, s => s.Id == sharedId && s.Reason.Contains("共用"));
            // 空清單與過多筆 → 400
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PostAsync("/api/v1/admin/tcrfc/press-resources/batch/hide",
                BizTest.Json(new { ids = Array.Empty<Guid>() }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PostAsync("/api/v1/admin/tcrfc/press-resources/batch/type",
                BizTest.Json(new { ids = new[] { sharedId }, resourceType = "video" }))).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM press_resources WHERE id = @Id", ("@Id", sharedId));
        }
    }

    [Fact]
    public async Task 媒體資源_公開端點只回已發布_下載找不到回404_類別參數驗證()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        // 種子的三筆都是 draft（佔位檔案）→ 公開清單看不到
        var list = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Press.PressResourceDto>>(await anonymous.GetAsync("/api/v1/tcrfc/press"));
        Assert.DoesNotContain(list.Items, r => r.Slug.StartsWith("test-"));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/tcrfc/press/test-press-release/download")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/v1/tcrfc/press?type=foo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/nope/press")).StatusCode);
    }

    // ═════════════════════════ C5 榮譽 ═════════════════════════

    [Fact]
    public async Task 榮譽_建立驗證更新刪除_年份預設取球季_唯讀角色不可寫()
    {
        var seasonId = await BizTest.ScalarGuidAsync("SELECT s.id FROM seasons s JOIN clubs c ON c.id = s.club_id WHERE c.code = N'tcrfc' AND s.code = N'2026-27'");
        var teamId = await BizTest.ScalarGuidAsync("SELECT id FROM teams WHERE code = N'D1'");

        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/admin/tcrfc/achievements")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/admin/tcrfc/achievements",
            BizTest.Json(new { seasonId, teamId, competitionName = "測試盃", placing = "冠軍" }))).StatusCode);

        using var manager = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        // 賽事名稱空白 → 400；球季不存在 → 400
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync("/api/v1/admin/tcrfc/achievements",
            BizTest.Json(new { seasonId, teamId, competitionName = " ", placing = "冠軍" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync("/api/v1/admin/tcrfc/achievements",
            BizTest.Json(new { seasonId = Guid.NewGuid(), teamId, competitionName = "測試盃", placing = "冠軍" }))).StatusCode);

        var created = await manager.PostAsync("/api/v1/admin/tcrfc/achievements",
            BizTest.Json(new { seasonId, teamId, competitionName = "【測試】榮譽建立", placing = "冠軍" }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await BizTest.ReadAsync<AdminAchievementDto>(created);
        try
        {
            Assert.Equal(2026, item.Year); // 省略年份：取球季開始日的年份
            Assert.Equal("D1", item.TeamCode);

            var updated = await manager.PutAsync($"/api/v1/admin/tcrfc/achievements/{item.Id}",
                BizTest.Json(new { seasonId, teamId, year = 2025, competitionName = "【測試】榮譽改", placing = "亞軍" }));
            Assert.Equal(2025, (await BizTest.ReadAsync<AdminAchievementDto>(updated)).Year);

            var filtered = await BizTest.ReadAsync<List<AdminAchievementDto>>(await manager.GetAsync($"/api/v1/admin/tcrfc/achievements?year=2025&teamId={teamId}"));
            Assert.Contains(filtered, a => a.Id == item.Id);

            // 公開端點看得到
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var pub = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Honors.AchievementDto>>(await anonymous.GetAsync("/api/v1/tcrfc/achievements?team=D1"));
            Assert.Contains(pub, a => a.Id == item.Id && a.Placing == "亞軍");
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/api/v1/admin/tcrfc/achievements/{item.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task 榮譽_學院管理者只能寫學院梯隊_一線隊擋下()
    {
        // academy.manager@tcrfc.test：學院／課程管理（academy_only），授權 bw。bw 有 BW1（一線隊）與 BW-U15（學院）。
        var seasonId = await BizTest.ScalarGuidAsync("SELECT s.id FROM seasons s JOIN clubs c ON c.id = s.club_id WHERE c.code = N'bw' AND s.code = N'2025'");
        var academyTeam = await BizTest.ScalarGuidAsync("SELECT id FROM teams WHERE code = N'BW-U15'");
        var firstTeam = await BizTest.ScalarGuidAsync("SELECT id FROM teams WHERE code = N'BW1'");

        using var academy = await BizTest.ClientAsync(fixture, "academy.manager@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await academy.PostAsync("/api/v1/admin/bw/achievements",
            BizTest.Json(new { seasonId, teamId = firstTeam, competitionName = "【測試】越權", placing = "冠軍" }))).StatusCode);

        var ok = await academy.PostAsync("/api/v1/admin/bw/achievements",
            BizTest.Json(new { seasonId, teamId = academyTeam, competitionName = "【測試】學院梯隊榮譽", placing = "第三名" }));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var item = await BizTest.ReadAsync<AdminAchievementDto>(ok);
        try
        {
            // 不能把學院梯隊的榮譽改掛到一線隊逃脫限制
            Assert.Equal(HttpStatusCode.Forbidden, (await academy.PutAsync($"/api/v1/admin/bw/achievements/{item.Id}",
                BizTest.Json(new { seasonId, teamId = firstTeam, competitionName = "【測試】想逃脫", placing = "冠軍" }))).StatusCode);
            // 學院管理者沒有里程碑權限
            Assert.Equal(HttpStatusCode.Forbidden, (await academy.GetAsync("/api/v1/admin/bw/milestones")).StatusCode);
        }
        finally
        {
            using var superAdmin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
            await superAdmin.DeleteAsync($"/api/v1/admin/bw/achievements/{item.Id}");
        }
    }

    // ═════════════════════════ C5 里程碑 ═════════════════════════

    [Fact]
    public async Task 里程碑_建立驗證_顯示旗標決定公開時間軸()
    {
        using var manager = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsync("/api/v1/admin/tcrfc/milestones", BizTest.Multipart(new
        {
            happenedOn = "2026-01-01", content = new { zh = new { title = " " } },
        }))).StatusCode);

        var visible = await CreateMilestoneAsync(manager, "【測試】顯示的里程碑", "2031-01-01", true);
        var hidden = await CreateMilestoneAsync(manager, "【測試】隱藏的里程碑", "2031-02-01", false);
        try
        {
            Assert.False(hidden.IsVisible);
            using var anonymous = await BizTest.ClientAsync(fixture, null);
            var pub = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Honors.MilestoneDto>>(await anonymous.GetAsync("/api/v1/tcrfc/milestones?lang=zh"));
            Assert.Contains(pub, m => m.Id == visible.Id);
            Assert.DoesNotContain(pub, m => m.Id == hidden.Id);

            // 由舊到新排序
            Assert.True(pub.FindIndex(m => m.Id == visible.Id) >= 0);
            var upd = await manager.PutAsync($"/api/v1/admin/tcrfc/milestones/{hidden.Id}", BizTest.Multipart(new
            {
                happenedOn = "2031-02-01", isVisible = true,
                content = new { zh = new { title = "【測試】改為顯示", imageAlt = "替代文字" }, en = new { title = "Shown" } },
            }));
            var updated = await BizTest.ReadAsync<AdminMilestoneDto>(upd);
            Assert.True(updated.IsVisible);
            Assert.Equal("Shown", updated.En!.Title);
            var pubEn = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Honors.MilestoneDto>>(await anonymous.GetAsync("/api/v1/tcrfc/milestones?lang=en"));
            Assert.Equal("Shown", pubEn.First(m => m.Id == hidden.Id).Title);
        }
        finally
        {
            await manager.DeleteAsync($"/api/v1/admin/tcrfc/milestones/{visible.Id}");
            await manager.DeleteAsync($"/api/v1/admin/tcrfc/milestones/{hidden.Id}");
        }
    }

    private static object NewPress(string type)
        => new { resourceType = type, status = "draft", content = new { zh = new { title = "【測試】媒體資源" } } };

    private static async Task<AdminMilestoneDto> CreateMilestoneAsync(HttpClient client, string title, string day, bool visible)
    {
        var response = await client.PostAsync("/api/v1/admin/tcrfc/milestones", BizTest.Multipart(new
        {
            happenedOn = day, isVisible = visible, content = new { zh = new { title } },
        }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await BizTest.ReadAsync<AdminMilestoneDto>(response);
    }
}
