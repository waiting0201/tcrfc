using System.Net;
using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminDraws;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>K5 抽獎名單管理（S3-8）。系統不做隨機抽出：所有「中獎」都是人工以序號回填。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminDrawsTests(AdminWriteApiFixture fixture)
{
    /// <summary>
    /// 本機驗收測資（<c>backoffice_seed.py</c> 區段 60）新增的兩位球迷會員（M900101／M900102，球衣登記用）同樣符合抽獎資格，
    /// 所以「種子合格名單」是 M900001／M900002 加上這兩位。<c>DevAcceptanceSeedTests</c> 守門種子仍存在；
    /// 這裡斷言總數時一律加上它，序號與雜湊相關斷言只看原本兩位（會員編號排序，新增的兩位排在後面）。
    /// </summary>
    private const int DevAcceptanceEligible = 2;

    private const string Draws = "/api/v1/admin/tcrfc/draws";

    private static string NewCode() => "T-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static object DrawPayload(string? code, bool rules = true, DateTime? snapshot = null, string? claimDeadline = null, string? extraNote = null) => new
    {
        drawCode = code,
        snapshotAt = (snapshot ?? DateTime.UtcNow).ToString("o"),
        drawnAt = DateTime.UtcNow.AddDays(1).ToString("o"),
        drawOccasion = "home_match",
        claimDeadlineOn = claimDeadline ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd"),
        internalNote = extraNote,
        content = new
        {
            zh = new { name = "【測試】球迷抽獎", prizeDescription = "【測試】簽名球衣兩件", rules = rules ? "【測試】活動辦法：資格為基準時間當下持有有效球迷會員會籍；同時具備兩隊會籍者可分別參加兩隊抽獎。" : null, notes = "注意事項" },
            en = new { name = "[Test] Fan prize draw" },
        },
    };

    private static async Task<AdminDrawDetailDto> NewDrawAsync(HttpClient client, string code, string club = "tcrfc", object? payload = null)
        => await BizTest.ReadAsync<AdminDrawDetailDto>(await client.PostAsync($"/api/v1/admin/{club}/draws", BizTest.Multipart(payload ?? DrawPayload(code))));

    private static async Task CleanupAsync(params string[] codes)
    {
        foreach (var code in codes)
        {
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @d TABLE (id uniqueidentifier, article uniqueidentifier NULL);
                INSERT INTO @d SELECT id, announcement_article_id FROM member_draws WHERE draw_code = @C;
                DELETE FROM draw_rosters WHERE member_draw_id IN (SELECT id FROM @d);
                DELETE FROM draw_roster_versions WHERE member_draw_id IN (SELECT id FROM @d);
                DELETE FROM member_draws_i18n WHERE member_draw_id IN (SELECT id FROM @d);
                DELETE FROM member_draws WHERE id IN (SELECT id FROM @d);
                DELETE FROM articles WHERE id IN (SELECT article FROM @d WHERE article IS NOT NULL);
                DELETE FROM tags WHERE slug = 'member-draw' AND NOT EXISTS (SELECT 1 FROM article_tags at JOIN tags t ON t.id = at.tag_id WHERE t.slug = 'member-draw');
                """, ("@C", code));
        }
    }

    private static async Task<AdminDrawDetailDto> GetAsync(HttpClient client, Guid id, string club = "tcrfc")
        => await BizTest.ReadAsync<AdminDrawDetailDto>(await client.GetAsync($"/api/v1/admin/{club}/draws/{id}"));

    // ═════════════ 權限、CRUD ═════════════

    [Fact]
    public async Task 抽獎活動_權限矩陣_建立驗證_活動代碼唯一_草稿可刪_跨俱樂部404()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var code = NewCode();
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Draws)).StatusCode);
            // 公關／媒體有「檢視（遮罩）」與公布稿交接，能打開活動清單撰寫公布稿；但不能建立、更新、匯出。內容編輯完全沒有 K5
            // （C 批畫面回報：原本只給公布稿權限，該角色打不開任何活動）
            Assert.Equal(HttpStatusCode.OK, (await pr.GetAsync(Draws)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync(Draws)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.PostAsync(Draws, BizTest.Multipart(DrawPayload(code)))).StatusCode);

            // 驗證
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync(Draws, BizTest.Multipart(DrawPayload("bad code!")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync(Draws, BizTest.Multipart(new { drawOccasion = "oops", content = new { zh = new { name = "x" } } }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.PostAsync(Draws, BizTest.Multipart(new { content = new { zh = new { name = " " } } }))).StatusCode);

            var created = await NewDrawAsync(service, code);
            Assert.Equal(code, created.DrawCode);
            Assert.Equal("草稿", created.StatusLabel);
            Assert.Equal(1, created.RosterVersion);
            Assert.Null(created.TotalCount);
            Assert.Equal("主場賽事日", created.DrawOccasionLabel);
            Assert.Equal("[Test] Fan prize draw", created.En?.Name);
            Assert.Contains("generate_roster", created.AvailableActions);
            Assert.NotNull(created.CreatedByName);
            Assert.Equal(HttpStatusCode.Conflict, (await service.PostAsync(Draws, BizTest.Multipart(DrawPayload(code)))).StatusCode);

            // 沒填活動代碼會自動產生；沒填基準時間但有開獎時間 → 預設開獎日（台灣時間）當天 00:00
            var auto = await BizTest.ReadAsync<AdminDrawDetailDto>(await service.PostAsync(Draws, BizTest.Multipart(new
            {
                drawnAt = "2027-01-10T04:00:00Z", content = new { zh = new { name = "【測試】自動代碼" } },
            })));
            try
            {
                Assert.Matches("^D\\d{8}-[0-9A-F]{4}$", auto.DrawCode);
                Assert.Equal(new DateTime(2027, 1, 9, 16, 0, 0), auto.SnapshotAt); // API 的時間戳是 UTC（JSON 不帶時區記號）：開獎日 2027-01-10（台灣）→ 前一天 16:00 UTC
                Assert.Equal(HttpStatusCode.NoContent, (await service.DeleteAsync($"{Draws}/{auto.Id}")).StatusCode); // 草稿可刪
                Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync($"{Draws}/{auto.Id}")).StatusCode);
            }
            finally
            {
                await CleanupAsync(auto.DrawCode);
            }

            // 草稿可編輯（含改活動代碼）；列表、篩選、搜尋
            var updated = await BizTest.ReadAsync<AdminDrawDetailDto>(await service.PutAsync($"{Draws}/{created.Id}", BizTest.Multipart(DrawPayload(code, extraNote: "【測試】內部備註"))));
            Assert.Equal("【測試】內部備註", updated.InternalNote);
            var list = await BizTest.ReadAsync<PagedResult<AdminDrawListItemDto>>(await service.GetAsync($"{Draws}?keyword={code}&status=draft"));
            Assert.Equal(created.Id, Assert.Single(list.Items).Id);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{Draws}?status=oops")).StatusCode);

            // 跨俱樂部 404
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/bw/draws/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await C1Test.PostEmptyAsync(admin, $"/api/v1/admin/bw/draws/{created.Id}/close")).StatusCode);
        }
        finally
        {
            await CleanupAsync(code);
        }
    }

    // ═════════════ 名單快照 ═════════════

    [Fact]
    public async Task 合格名單_蒐集告知與活動辦法前置_試算_序號依會員編號_雜湊_作廢重產保留舊版()
    {
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "member.draw_notice%");
        var code = NewCode();
        var noRulesCode = NewCode();
        try
        {
            // 蒐集告知未完成 → 不得產生名單
            await C1Test.PutJsonAsync(service, $"{Draws}/notice", new { confirmed = false });
            Assert.False((await BizTest.ReadAsync<AdminDrawNoticeDto>(await service.GetAsync($"{Draws}/notice"))).Confirmed);
            var draw = await NewDrawAsync(service, code);
            var blocked = await C1Test.PostJsonAsync(service, $"{Draws}/{draw.Id}/roster", new { });
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Contains("蒐集告知", await C1Test.BodyAsync(blocked));
            var confirmed = await BizTest.ReadAsync<AdminDrawNoticeDto>(await C1Test.PutJsonAsync(service, $"{Draws}/notice", new { confirmed = true }));
            Assert.True(confirmed.Confirmed);
            Assert.NotNull(confirmed.ConfirmedAt);

            // 活動辦法必填才能鎖定名單；沒有基準時間 → 不能試算
            var noRules = await NewDrawAsync(service, noRulesCode, payload: DrawPayload(noRulesCode, rules: false));
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{Draws}/{noRules.Id}/roster", new { })).StatusCode);
            var noSnapshot = await BizTest.ReadAsync<AdminDrawDetailDto>(await service.PostAsync(Draws, BizTest.Multipart(new { content = new { zh = new { name = "x", rules = "r" } } })));
            try
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostEmptyAsync(service, $"{Draws}/{noSnapshot.Id}/roster/preview")).StatusCode);
            }
            finally
            {
                await CleanupAsync(noSnapshot.DrawCode);
            }

            // 試算：不寫資料、不配號；種子的合格會員是 M900001／M900002（停用的 M900005、一般會員不算）
            var preview = await BizTest.ReadAsync<AdminRosterPreviewDto>(await C1Test.PostEmptyAsync(service, $"{Draws}/{draw.Id}/roster/preview"));
            Assert.Equal(2 + DevAcceptanceEligible, preview.EligibleCount);
            Assert.Equal(0, await ShopTest.CountAsync("SELECT COUNT(*) FROM draw_rosters WHERE member_draw_id = @D", ("@D", draw.Id)));

            // 產生並鎖定
            var locked = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostJsonAsync(service, $"{Draws}/{draw.Id}/roster", new { }));
            Assert.Equal("名單已鎖定", locked.StatusLabel);
            Assert.Equal(2 + DevAcceptanceEligible, locked.TotalCount);
            Assert.Equal(64, locked.RosterHash?.Length);
            Assert.NotNull(locked.LockedByName);
            Assert.Single(locked.Versions);
            Assert.True(locked.Versions[0].IsCurrent);
            Assert.Contains("regenerate_roster", locked.AvailableActions);
            // B-10：名單預設遮罩（與 K1 一致），要 reveal=true 才解除。
            var maskedByDefault = await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster"));
            Assert.All(maskedByDefault.Items, r => Assert.True(r.IsMasked));
            Assert.DoesNotContain(maskedByDefault.Items, r => r.Name == "【測試】會員甲");
            var roster = await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster?reveal=true"));
            Assert.All(roster.Items, r => Assert.False(r.IsMasked));
            using (var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test"))
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync($"{Draws}/{draw.Id}/roster?reveal=true")).StatusCode); // 沒有解除遮罩權限
            }

            Assert.Equal(new[] { 1, 2 }, roster.Items.Where(r => string.CompareOrdinal(r.MemberNo, "M900100") < 0).Select(r => r.SerialNo).ToArray());
            Assert.Equal(new[] { "M900001", "M900002" }, roster.Items.Select(r => r.MemberNo).Where(n => string.CompareOrdinal(n, "M900100") < 0).ToArray()); // 依會員編號升冪
            Assert.All(roster.Items, r => Assert.Equal("球迷會員", r.TierLabel));
            Assert.Equal("【測試】會員甲", roster.Items[0].Name); // 客服可看完整姓名快照
            // 名單雜湊可由名單內容重算
            var expectedHash = AdminDrawsRepository.ComputeHash(code, 1, roster.Items.Select(r => (r.SerialNo, r.MemberNo, r.Name, "fan_club", r.MembershipEndOn)).ToList());
            Assert.Equal(expectedHash, locked.RosterHash);

            // 鎖定後：不能逐列新增或刪除（沒有這種端點）、不能改基準時間與活動代碼
            Assert.Equal(HttpStatusCode.Conflict, (await service.PutAsync($"{Draws}/{draw.Id}", BizTest.Multipart(DrawPayload(code, snapshot: DateTime.UtcNow.AddDays(-1))))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await service.PutAsync($"{Draws}/{draw.Id}", BizTest.Multipart(DrawPayload(NewCode(), snapshot: locked.SnapshotAt)))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await service.DeleteAsync($"{Draws}/{draw.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await service.PutAsync($"{Draws}/{draw.Id}", BizTest.Multipart(DrawPayload(code, snapshot: locked.SnapshotAt, extraNote: "改備註")))).StatusCode);
            // 名單鎖定後再按產生 → 視為作廢重產，必須填原因
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{Draws}/{draw.Id}/roster", new { })).StatusCode);

            // 整份作廢重產：版本 +1，舊版保留（不刪除）並記作廢原因
            var regenerated = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostJsonAsync(service, $"{Draws}/{draw.Id}/roster", new { voidReason = "【測試】基準時間有誤" }));
            Assert.Equal(2, regenerated.RosterVersion);
            Assert.Equal(2, regenerated.Versions.Count);
            var oldVersion = regenerated.Versions.Single(v => v.Version == 1);
            Assert.Equal("【測試】基準時間有誤", oldVersion.VoidReason);
            Assert.NotNull(oldVersion.VoidedAt);
            Assert.False(oldVersion.IsCurrent);
            Assert.True(regenerated.Versions.Single(v => v.Version == 2).IsCurrent);
            Assert.NotEqual(locked.RosterHash, regenerated.RosterHash); // 雜湊含版本
            Assert.Equal(2 + DevAcceptanceEligible, await ShopTest.CountAsync("SELECT COUNT(*) FROM draw_rosters WHERE member_draw_id = @D AND roster_version = 1", ("@D", draw.Id)));
            Assert.Equal(2 + DevAcceptanceEligible, await ShopTest.CountAsync("SELECT COUNT(*) FROM draw_rosters WHERE member_draw_id = @D AND roster_version = 2", ("@D", draw.Id)));
            var oldRoster = await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster?version=1"));
            Assert.Equal(2 + DevAcceptanceEligible, oldRoster.TotalCount);
            Assert.Equal(1, (await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster?keyword=M900002"))).TotalCount);
            Assert.Equal(1, (await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster?keyword=1"))).Items.Count(r => r.SerialNo == 1));
        }
        finally
        {
            await restore();
            await CleanupAsync(code, noRulesCode);
        }
    }

    [Fact]
    public async Task 藍鯨抽獎_合格條件是本活動主辦俱樂部的有效球迷會籍_藍鯨沒有有效會員時不能產生名單_未確認告知也不行()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var restore = await C1Test.SnapshotSettingsAsync("bw", "member.draw_notice%");
        var code = NewCode();
        try
        {
            var draw = await NewDrawAsync(admin, code, "bw");
            // 藍鯨還沒確認蒐集告知：即使磐石已確認也不行（各俱樂部各自舉辦）
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(admin, $"/api/v1/admin/bw/draws/{draw.Id}/roster", new { })).StatusCode);
            await C1Test.PutJsonAsync(admin, "/api/v1/admin/bw/draws/notice", new { confirmed = true });
            // M900001 的藍鯨球迷會籍是 2025 年球季（已到期）→ 基準時間當下沒有合格會員
            var preview = await BizTest.ReadAsync<AdminRosterPreviewDto>(await C1Test.PostEmptyAsync(admin, $"/api/v1/admin/bw/draws/{draw.Id}/roster/preview"));
            Assert.Equal(0, preview.EligibleCount);
            var none = await C1Test.PostJsonAsync(admin, $"/api/v1/admin/bw/draws/{draw.Id}/roster", new { });
            Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);
            Assert.Contains("沒有任何合格", await C1Test.BodyAsync(none));

            // 把基準時間拉回 2025 年球季期間：藍鯨的 M900001 才合格（同一個人在磐石與藍鯨是兩份名單）
            var earlier = await admin.PutAsync($"/api/v1/admin/bw/draws/{draw.Id}", BizTest.Multipart(DrawPayload(code, snapshot: DateTime.Parse("2025-05-01T00:00:00Z").ToUniversalTime())));
            Assert.Equal(HttpStatusCode.OK, earlier.StatusCode);
            var locked = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostJsonAsync(admin, $"/api/v1/admin/bw/draws/{draw.Id}/roster", new { }));
            Assert.Equal(1, locked.TotalCount);
            var roster = await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await admin.GetAsync($"/api/v1/admin/bw/draws/{draw.Id}/roster"));
            Assert.Equal("M900001", Assert.Single(roster.Items).MemberNo);
        }
        finally
        {
            await restore();
            await CleanupAsync(code);
        }
    }

    // ═════════════ 中獎人、發放、匯出、公布 ═════════════

    private async Task<(AdminDrawDetailDto Draw, HttpClient Service, HttpClient Admin, Func<Task> Restore)> LockedDrawAsync(string code, string? claimDeadline = null)
    {
        var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var restore = await C1Test.SnapshotSettingsAsync("tcrfc", "member.draw_notice%");
        await C1Test.PutJsonAsync(service, $"{Draws}/notice", new { confirmed = true });
        // 呼叫端在拿到回傳值之後才進 try／finally：這裡建活動或鎖名單中途失敗（例如庫裡沒有合格會員 → 409）時，
        // 呼叫端的 finally 根本不會執行，草稿活動與被改成「已確認」的蒐集告知設定就留在共用本機庫（docs/18 E-293）。所以失敗要在這裡自己收拾。
        try
        {
            var draw = await NewDrawAsync(service, code, payload: DrawPayload(code, claimDeadline: claimDeadline));
            var locked = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostJsonAsync(service, $"{Draws}/{draw.Id}/roster", new { }));
            return (locked, service, admin, restore);
        }
        catch
        {
            await CleanupAsync(code);
            await restore();
            throw;
        }
    }

    [Fact]
    public async Task 中獎人回填_序號比對_備取與遞補_狀態流轉_公布後修改須填原因_取消中獎()
    {
        var code = NewCode();
        var (draw, service, admin, restore) = await LockedDrawAsync(code);
        try
        {
            var url = $"{Draws}/{draw.Id}/winners";
            // 序號不存在／重複／缺獎項名稱／空清單
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 99, prizeName = "獎" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 1, prizeName = "獎" }, new { serialNo = 1, prizeName = "獎" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 1 } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, url, new { winners = Array.Empty<object>() })).StatusCode);
            // 還沒有名單的活動不能回填
            var empty = await NewDrawAsync(service, NewCode());
            try
            {
                Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(service, $"{Draws}/{empty.Id}/winners", new { winners = new[] { new { serialNo = 1, prizeName = "獎" } } })).StatusCode);
            }
            finally
            {
                await CleanupAsync(empty.DrawCode);
            }

            // 備取先回填（不算中獎、不會進入「已抽出」）；再回填中獎 → 已抽出
            var backup = await BizTest.ReadAsync<AdminWinnerResultDto>(await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 2, isBackup = true } } }));
            Assert.Equal(1, backup.Draw.BackupCount);
            Assert.Equal(0, backup.Draw.WinnerCount);
            Assert.Equal("roster_locked", backup.Draw.Status);
            var winners = await BizTest.ReadAsync<AdminWinnerResultDto>(await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 1, prizeName = "【測試】簽名球衣" } } }));
            Assert.Equal("已抽出", winners.Draw.StatusLabel);
            Assert.Equal((1, 1), (winners.Draw.WinnerCount, winners.Draw.BackupCount));
            // 中獎人不能直接改備取；備取可以遞補成中獎
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 1, isBackup = true } } })).StatusCode);
            var promoted = await BizTest.ReadAsync<AdminWinnerResultDto>(await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 2, prizeName = "【測試】遞補獎" } } }));
            Assert.Equal((2, 0), (promoted.Draw.WinnerCount, promoted.Draw.BackupCount));
            var listed = await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster?winnersOnly=true"));
            Assert.Equal(2, listed.TotalCount);
            Assert.All(listed.Items, r => Assert.True(r.IsWinner));
            // 已抽出後名單不能重產
            var regen = await C1Test.PostJsonAsync(service, $"{Draws}/{draw.Id}/roster", new { voidReason = "x" });
            Assert.Equal(HttpStatusCode.Conflict, regen.StatusCode);

            // 取消中獎：清除發放資料；全部取消後回到「名單已鎖定」
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{url}/remove", new { serialNos = new[] { 99 } })).StatusCode);
            var removed = await BizTest.ReadAsync<AdminWinnerResultDto>(await C1Test.PostJsonAsync(service, $"{url}/remove", new { serialNos = new[] { 1, 2 }, reason = "【測試】誤勾" }));
            Assert.Equal(2, removed.UpdatedCount);
            Assert.Equal("roster_locked", removed.Draw.Status);
            Assert.Equal(0, removed.Draw.WinnerCount);

            // 公布後再修改必須填異動原因：直接把狀態推到已公布（測試用 SQL），驗證規則
            await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 1, prizeName = "獎" } } });
            await BizTest.ExecuteSqlAsync("UPDATE member_draws SET status = 'announced' WHERE id = @D", ("@D", draw.Id));
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 2, prizeName = "獎二" } } })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 2, prizeName = "獎二" } }, reason = "【測試】補登" })).StatusCode);
            await BizTest.ExecuteSqlAsync("UPDATE member_draws SET status = 'closed' WHERE id = @D", ("@D", draw.Id));
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(service, url, new { winners = new[] { new { serialNo = 1, prizeName = "X" } }, reason = "x" })).StatusCode);
        }
        finally
        {
            service.Dispose();
            admin.Dispose();
            await restore();
            await CleanupAsync(code);
        }
    }

    [Fact]
    public async Task 獎品發放_逾期自動標記_寄送須有收件資訊_收件資訊修改需解除遮罩權限_批次狀態_遮罩()
    {
        var code = NewCode();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8).AddDays(-1)).ToString("yyyy-MM-dd");
        var (draw, service, admin, restore) = await LockedDrawAsync(code, claimDeadline: yesterday);
        try
        {
            var fulfil = $"{Draws}/{draw.Id}/fulfilment";
            Assert.Equal(0, (await BizTest.ReadAsync<PagedResult<AdminFulfilmentDto>>(await service.GetAsync(fulfil))).TotalCount); // 尚未回填：清單為空但可讀
            await C1Test.PutJsonAsync(service, $"{Draws}/{draw.Id}/winners", new { winners = new[] { new { serialNo = 1, prizeName = "獎一" }, new { serialNo = 2, prizeName = "獎二" } } });

            // 領獎期限已過、狀態仍待處理 → 有效狀態顯示「逾期」
            var list = await BizTest.ReadAsync<PagedResult<AdminFulfilmentDto>>(await service.GetAsync(fulfil));
            Assert.Equal(2, list.TotalCount);
            Assert.All(list.Items, f => Assert.Equal("overdue", f.EffectiveStatus));
            Assert.Equal("逾期", list.Items[0].EffectiveStatusLabel);
            Assert.Equal(2, (await BizTest.ReadAsync<PagedResult<AdminFulfilmentDto>>(await service.GetAsync($"{fulfil}?status=overdue"))).TotalCount);
            Assert.Equal(0, (await BizTest.ReadAsync<PagedResult<AdminFulfilmentDto>>(await service.GetAsync($"{fulfil}?status=claimed"))).TotalCount);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{fulfil}?status=oops")).StatusCode);

            // 寄送：必須有領獎方式為寄送、收件人姓名／電話／地址才能標記已寄出
            var one = $"{fulfil}/1";
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, one, new { status = "shipped" })).StatusCode); // 還沒設領獎方式
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, one, new { claimMethod = "ship", status = "shipped" })).StatusCode); // 沒有收件資訊
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, one, new { claimMethod = "teleport" })).StatusCode);
            var shipped = await BizTest.ReadAsync<AdminFulfilmentDto>(await C1Test.PutJsonAsync(service, one, new
            {
                claimMethod = "ship", recipientName = "【測試】得獎人", recipientPhone = "0900-000-321", recipientAddress = "【測試】台中市西屯區測試路 3 號", status = "shipped", note = "【測試】備註",
            }));
            Assert.Equal("已寄出", shipped.EffectiveStatusLabel);
            Assert.NotNull(shipped.ShippedAt);
            Assert.Equal("寄送", shipped.ClaimMethodLabel);
            Assert.Equal("【測試】得獎人", shipped.RecipientName); // 客服看完整值
            // 現場領取：不能標已寄出，直接標已領取
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, $"{fulfil}/2", new { claimMethod = "pickup", status = "shipped" })).StatusCode);
            var claimed = await BizTest.ReadAsync<AdminFulfilmentDto>(await C1Test.PutJsonAsync(service, $"{fulfil}/2", new { claimMethod = "pickup", status = "claimed" }));
            Assert.NotNull(claimed.ClaimedAt);
            var back = await BizTest.ReadAsync<AdminFulfilmentDto>(await C1Test.PutJsonAsync(service, $"{fulfil}/2", new { status = "pending" }));
            Assert.Null(back.ClaimedAt);
            Assert.Equal("overdue", back.EffectiveStatus);
            // 非中獎人 → 404
            Assert.Equal(HttpStatusCode.NotFound, (await C1Test.PutJsonAsync(service, $"{fulfil}/99", new { status = "claimed" })).StatusCode);

            // 批次：能處理的處理、不符規則的略過
            var batch = await BizTest.ReadAsync<AdminBatchFulfilmentResultDto>(await C1Test.PostJsonAsync(service, $"{fulfil}/batch/status", new { serialNos = new[] { 1, 2, 99 }, status = "claimed" }));
            Assert.Equal(2, batch.UpdatedCount);
            Assert.Equal(99, Assert.Single(batch.Skipped).SerialNo);
            var done = await BizTest.ReadAsync<PagedResult<AdminDrawListItemDto>>(await service.GetAsync($"{Draws}?keyword={code}"));
            Assert.Equal((2, 2), (done.Items[0].WinnerCount, done.Items[0].FulfilledCount));

            // 沒有「檢視會員完整個資」權限：名單與收件資訊遮罩、不能改收件資訊、搜尋不比對姓名（暫時拿掉客服的解除遮罩權限，finally 還原）
            var removedRow = await BizTest.ExecuteSqlAsync(
                "DELETE rp FROM role_permissions rp JOIN admin_roles r ON r.id = rp.admin_role_id JOIN permissions p ON p.id = rp.permission_id WHERE r.code = 'customer_service_admin' AND p.code = 'member.pii.reveal'");
            try
            {
                Assert.Equal(1, removedRow);
                var masked = await BizTest.ReadAsync<PagedResult<AdminFulfilmentDto>>(await service.GetAsync(fulfil));
                var first = masked.Items.Single(f => f.SerialNo == 1);
                Assert.True(first.IsMasked);
                Assert.Contains('○', first.MemberName!);
                Assert.Equal("09******21", first.RecipientPhone);
                Assert.EndsWith("***", first.RecipientAddress);
                Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PutJsonAsync(service, one, new { recipientName = "改名" })).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await C1Test.PutJsonAsync(service, one, new { note = "只改備註" })).StatusCode);
                var maskedRoster = await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster"));
                Assert.All(maskedRoster.Items, r => Assert.True(r.IsMasked));
                Assert.Contains('○', maskedRoster.Items[0].Name!);
                Assert.Equal(0, (await BizTest.ReadAsync<PagedResult<AdminRosterEntryDto>>(await service.GetAsync($"{Draws}/{draw.Id}/roster?keyword=" + Uri.EscapeDataString("會員甲")))).TotalCount);
            }
            finally
            {
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO role_permissions (admin_role_id, permission_id, scope_type) SELECT r.id, p.id, 'all' FROM admin_roles r, permissions p WHERE r.code = 'customer_service_admin' AND p.code = 'member.pii.reveal'");
            }

            // 已結案的活動不能再改發放
            await BizTest.ExecuteSqlAsync("UPDATE member_draws SET status = 'closed' WHERE id = @D", ("@D", draw.Id));
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(service, one, new { status = "pending" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{fulfil}/batch/status", new { serialNos = new[] { 1 }, status = "pending" })).StatusCode);
        }
        finally
        {
            service.Dispose();
            admin.Dispose();
            await restore();
            await CleanupAsync(code);
        }
    }

    [Fact]
    public async Task 匯出_兩種名單CSV權限不同_檔名規則_受限版只含中獎人_都須填用途_公布稿只給遮罩名單()
    {
        var code = NewCode();
        var (draw, service, admin, restore) = await LockedDrawAsync(code);
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        try
        {
            var export = $"{Draws}/{draw.Id}/export";
            // 還沒有中獎人時：公開版可匯出（全體、遮罩姓名）；受限版只有中獎人 → 0 列
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{export}/public")).StatusCode); // 必填用途
            var publicResponse = await service.GetAsync($"{export}/public?purpose={Uri.EscapeDataString("現場投影")}");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
            Assert.Equal($"draw-{code}-v1-public.csv", publicResponse.Content.Headers.ContentDisposition?.FileName);
            var publicCsv = Encoding.UTF8.GetString(await publicResponse.Content.ReadAsByteArrayAsync());
            Assert.StartsWith("﻿抽獎序號,會員編號,姓名,活動代碼,名單版本,基準時間", publicCsv);
            Assert.Contains("M900001", publicCsv);
            Assert.Contains("M900002", publicCsv);
            Assert.DoesNotContain("會員甲", publicCsv); // 姓名遮罩
            Assert.DoesNotContain("0900-000-001", publicCsv);
            Assert.DoesNotContain("@example.com", publicCsv);

            await C1Test.PutJsonAsync(service, $"{Draws}/{draw.Id}/winners", new { winners = new[] { new { serialNo = 2, prizeName = "【測試】簽名球衣" } } });
            await C1Test.PutJsonAsync(service, $"{Draws}/{draw.Id}/fulfilment/2", new { claimMethod = "ship", recipientName = "【測試】收件人", recipientPhone = "0900-000-654", recipientAddress = "【測試】台中市測試路 6 號" });

            // 受限版：需要 member.draw.export（公關／媒體沒有），只匯出已回填的中獎人，含手機與 Email
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync($"{export}/winners?purpose=x")).StatusCode);
            // 公開遮罩版只需要「檢視」：公關／媒體撰寫公布稿本來就只取得遮罩版名單（規劃書 §6），可匯出（仍須填用途並寫敏感操作日誌）
            Assert.Equal(HttpStatusCode.OK, (await pr.GetAsync($"{export}/public?purpose=x")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await pr.GetAsync($"{export}/public")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await service.GetAsync($"{export}/winners")).StatusCode);
            var winnersResponse = await service.GetAsync($"{export}/winners?purpose={Uri.EscapeDataString("聯絡中獎人")}");
            Assert.Equal($"draw-{code}-v1-winners.csv", winnersResponse.Content.Headers.ContentDisposition?.FileName);
            var winnersCsv = Encoding.UTF8.GetString(await winnersResponse.Content.ReadAsByteArrayAsync());
            Assert.Contains("M900002", winnersCsv);
            Assert.DoesNotContain("M900001", winnersCsv); // 沒中獎的不在受限版
            Assert.Contains("member-b@example.com", winnersCsv);
            Assert.Contains("0900-000-002", winnersCsv);
            Assert.Contains("【測試】簽名球衣", winnersCsv);
            Assert.Contains("寄送", winnersCsv);
            var shippingCsv = Encoding.UTF8.GetString(await (await service.GetAsync($"{export}/shipping?purpose=出貨")).Content.ReadAsByteArrayAsync());
            Assert.Contains("【測試】台中市測試路 6 號", shippingCsv);
            Assert.Contains("待處理", shippingCsv);

            // 尚未產生名單的活動沒有可匯出的資料
            var draft = await NewDrawAsync(service, NewCode());
            try
            {
                Assert.Equal(HttpStatusCode.Conflict, (await service.GetAsync($"{Draws}/{draft.Id}/export/public?purpose=x")).StatusCode);
            }
            finally
            {
                await CleanupAsync(draft.DrawCode);
            }

            // 公布稿預覽：公關／媒體只拿到遮罩名單（抽獎序號＋會員編號＋姓名遮罩）
            var preview = await BizTest.ReadAsync<AdminAnnouncementPreviewDto>(await pr.GetAsync($"{Draws}/{draw.Id}/announcement-preview"));
            var winner = Assert.Single(preview.Winners);
            Assert.Equal((2, "M900002"), (winner.SerialNo, winner.MemberNo));
            Assert.Contains('○', winner.MaskedName!);
            Assert.DoesNotContain("會員乙", await C1Test.BodyAsync(await pr.GetAsync($"{Draws}/{draw.Id}/announcement-preview")));
            Assert.Equal(2 + DevAcceptanceEligible, preview.EligibleCount);
            // 公關／媒體有「檢視（遮罩）」：名單看得到，但姓名一律遮罩、完整值需要 member.pii.reveal（沒有）；不因此取得任何會員模組權限
            var maskedRoster = await pr.GetAsync($"{Draws}/{draw.Id}/roster");
            Assert.Equal(HttpStatusCode.OK, maskedRoster.StatusCode);
            var rosterText = await maskedRoster.Content.ReadAsStringAsync();
            Assert.DoesNotContain("會員甲", rosterText);
            Assert.DoesNotContain("會員乙", rosterText);
            Assert.Equal(HttpStatusCode.Forbidden, (await pr.GetAsync("/api/v1/admin/tcrfc/members")).StatusCode);
        }
        finally
        {
            service.Dispose();
            admin.Dispose();
            await restore();
            await CleanupAsync(code);
        }
    }

    [Fact]
    public async Task 刪除公布稿文章_作業中的活動先解除關聯可刪_已公布的活動回409而不是500()
    {
        var code = NewCode();
        var (draw, service, admin, restore) = await LockedDrawAsync(code);
        try
        {
            var url = $"{Draws}/{draw.Id}";
            await C1Test.PutJsonAsync(service, $"{url}/winners", new { winners = new[] { new { serialNo = 1, prizeName = "【測試】簽名球衣" } } });
            var draft = await BizTest.ReadAsync<AdminAnnouncementDraftDto>(await C1Test.PostEmptyAsync(service, $"{url}/announcement-draft"));
            async Task<HttpResponseMessage> DeleteArticleAsync()
            {
                var article = await BizTest.ReadAsync<AdminArticleDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/news/{draft.ArticleId}"));
                return await admin.DeleteAsync($"/api/v1/admin/tcrfc/news/{draft.ArticleId}?expectedUpdatedAt={Uri.EscapeDataString(article.UpdatedAt.ToString("o"))}");
            }

            // 已抽出、公布稿還只是草稿：解除關聯後刪除成功（原本撞外鍵回 500）。
            Assert.Equal(HttpStatusCode.NoContent, (await DeleteArticleAsync()).StatusCode);
            Assert.Null((await BizTest.ReadAsync<AdminDrawDetailDto>(await service.GetAsync(url))).AnnouncementArticleId);

            // 可以重新產生公布稿；活動標為已公布後，這篇是對外紀錄，刪除回 409 並說明原因。
            draft = await BizTest.ReadAsync<AdminAnnouncementDraftDto>(await C1Test.PostEmptyAsync(service, $"{url}/announcement-draft"));
            await BizTest.ExecuteSqlAsync("UPDATE articles SET status = 'published', published_at = SYSUTCDATETIME() WHERE id = @A", ("@A", draft.ArticleId));
            Assert.Equal(HttpStatusCode.OK, (await C1Test.PostEmptyAsync(service, $"{url}/mark-announced")).StatusCode);
            var blocked = await DeleteArticleAsync();
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Contains(code, await blocked.Content.ReadAsStringAsync());
        }
        finally
        {
            service.Dispose();
            admin.Dispose();
            await restore();
            await CleanupAsync(code);
        }
    }

    [Fact]
    public async Task 公布交接B2_產生草稿文章_分類Club_News加標籤_遮罩_已發布才能標記已公布_結案作廢規則()
    {
        var code = NewCode();
        var (draw, service, admin, restore) = await LockedDrawAsync(code);
        using var pr = await BizTest.ClientAsync(fixture, "pr.media@tcrfc.test");
        try
        {
            var url = $"{Draws}/{draw.Id}";
            // 沒有中獎人不能產生公布稿；標記已公布需要先有已發布的文章
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(pr, $"{url}/announcement-draft")).StatusCode);
            await C1Test.PutJsonAsync(service, $"{url}/winners", new { winners = new[] { new { serialNo = 1, prizeName = "【測試】簽名球衣" } } });
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(service, $"{url}/mark-announced")).StatusCode);

            // 公關／媒體產生公布稿：一律草稿、Club News、加掛標籤、內容只有遮罩名單
            Assert.Equal(HttpStatusCode.OK, (await pr.GetAsync(url)).StatusCode); // 公關／媒體看得到活動詳情（不含完整個資）
            Assert.Equal(HttpStatusCode.Forbidden, (await C1Test.PutJsonAsync(pr, $"{url}/winners", new { winners = new[] { new { serialNo = 1, prizeName = "x" } } })).StatusCode);
            var draft = await BizTest.ReadAsync<AdminAnnouncementDraftDto>(await C1Test.PostEmptyAsync(pr, $"{url}/announcement-draft"));
            Assert.Equal($"member-draw-{code.ToLowerInvariant()}", draft.ArticleSlug);
            Assert.Equal(draft.ArticleId, draft.Draw.AnnouncementArticleId);
            Assert.Equal("草稿", draft.Draw.AnnouncementStatusLabel);
            var article = await BizTest.ReadAsync<AdminArticleDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/news/{draft.ArticleId}"));
            Assert.Equal("club", article.CategoryCode);
            Assert.Equal("draft", article.Status);
            Assert.Contains(article.Tags, t => t.Slug == "member-draw" && t.NameZh == "球迷會員抽獎" && t.NameEn == "Member Draw");
            Assert.Contains("中獎名單公布", article.Zh.Title);
            Assert.Contains("M900001", article.Zh.Body);
            Assert.DoesNotContain("會員甲", article.Zh.Body);
            Assert.DoesNotContain("0900-000-001", article.Zh.Body);
            Assert.DoesNotContain("@example.com", article.Zh.Body);
            Assert.Contains("○", article.Zh.Body);
            // 已經連結過 → 再產生 409
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(pr, $"{url}/announcement-draft")).StatusCode);
            // 文章還是草稿 → 不能標記已公布；發布後可以，列表顯示公布狀態與連結
            var notPublished = await C1Test.PostEmptyAsync(service, $"{url}/mark-announced");
            Assert.Equal(HttpStatusCode.Conflict, notPublished.StatusCode);
            Assert.Contains("還沒發布", await C1Test.BodyAsync(notPublished));
            // 公關潤稿後發布（這裡直接把文章標成已發布，發布流程本身由 AdminNews 測試涵蓋）
            await BizTest.ExecuteSqlAsync("UPDATE articles SET status = 'published', published_at = SYSUTCDATETIME() WHERE id = @A", ("@A", draft.ArticleId));
            var announced = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostEmptyAsync(service, $"{url}/mark-announced"));
            Assert.Equal("已公布", announced.StatusLabel);
            Assert.Equal("已發布", announced.AnnouncementStatusLabel);
            var row = (await BizTest.ReadAsync<PagedResult<AdminDrawListItemDto>>(await service.GetAsync($"{Draws}?keyword={code}"))).Items.Single();
            Assert.Equal(draft.ArticleId, row.AnnouncementArticleId);
            Assert.Equal("已發布", row.AnnouncementStatusLabel);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(service, $"{url}/mark-announced")).StatusCode);

            // 結案；結案後不能作廢、不能再編輯；沒有連結別的俱樂部文章
            Assert.Equal("已結案", (await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostEmptyAsync(service, $"{url}/close"))).StatusLabel);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostEmptyAsync(service, $"{url}/close")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{url}/void", new { reason = "x" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await service.PutAsync(url, BizTest.Multipart(DrawPayload(code, snapshot: draw.SnapshotAt)))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PutJsonAsync(pr, $"{url}/announcement-article", new { articleId = draft.ArticleId })).StatusCode);
        }
        finally
        {
            service.Dispose();
            admin.Dispose();
            await restore();
            await CleanupAsync(code);
        }
    }

    [Fact]
    public async Task 連結既有文章_只能連本俱樂部_作廢活動_作廢原因必填()
    {
        var code = NewCode();
        var (draw, service, admin, restore) = await LockedDrawAsync(code);
        try
        {
            var url = $"{Draws}/{draw.Id}";
            var own = await C1Test.ArticleIdAsync("tcrfc");
            var bwArticle = await C1Test.ArticleIdAsync("bw");
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, $"{url}/announcement-article", new { articleId = bwArticle })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PutJsonAsync(service, $"{url}/announcement-article", new { articleId = Guid.NewGuid() })).StatusCode);
            var linked = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PutJsonAsync(service, $"{url}/announcement-article", new { articleId = own }));
            Assert.Equal(own, linked.AnnouncementArticleId);

            Assert.Equal(HttpStatusCode.BadRequest, (await C1Test.PostJsonAsync(service, $"{url}/void", new { reason = " " })).StatusCode);
            var voided = await BizTest.ReadAsync<AdminDrawDetailDto>(await C1Test.PostJsonAsync(service, $"{url}/void", new { reason = "【測試】主辦方取消活動" }));
            Assert.Equal("作廢", voided.StatusLabel);
            Assert.Contains("作廢：【測試】主辦方取消活動", voided.InternalNote);
            Assert.Empty(voided.AvailableActions);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{url}/void", new { reason = "再作廢" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await C1Test.PostJsonAsync(service, $"{url}/roster", new { voidReason = "x" })).StatusCode);
            // 作廢版本的資料保留，不能刪除活動
            Assert.Equal(HttpStatusCode.Conflict, (await service.DeleteAsync(url)).StatusCode);
            Assert.Equal(2 + DevAcceptanceEligible, await ShopTest.CountAsync("SELECT COUNT(*) FROM draw_rosters WHERE member_draw_id = @D", ("@D", draw.Id)));
        }
        finally
        {
            service.Dispose();
            admin.Dispose();
            await restore();
            await CleanupAsync(code);
        }
    }
}
