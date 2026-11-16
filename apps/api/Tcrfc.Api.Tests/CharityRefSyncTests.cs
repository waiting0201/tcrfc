using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// N2「從官網更新清單」（K-2）：慈善平台以 HTTP 呼叫主站公開清單，整份對齊公益團體與慈善計畫的唯讀複本。
/// 主站以 <see cref="ScriptedMainSite"/> 假處理器替代（不打網路）。
///
/// 🔴 <c>charity_refs</c>／<c>charity_program_refs</c> 是<b>共用本機庫上的單一份資料</b>：同步會對「所有 <c>external_id</c> 非空的列」做整份對齊，
/// 使用者自己同步過的真實列也會被假清單停用。所以每個測試前拍下兩張表的整份內容，測試後原樣還原（比照 <see cref="CharityAdminSettingsTests"/>），
/// 絕不依標記清除——否則會把使用者真實同步來的列一起刪掉。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityRefSyncTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Projects = AdminBase + "/projects";
    private const string Sync = Projects + "/charity-refs/sync";
    private const string Refs = Projects + "/charity-refs";

    private RefTables _snapshot = null!;

    public async Task InitializeAsync()
    {
        fx.ResetDoubles();
        _snapshot = await RefTables.TakeAsync(fx.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _snapshot.RestoreAsync(fx.ConnectionString);
        await fx.CleanupAsync();
    }

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    /// <summary>建立項目的請求內容（slug 用 <c>ct-</c> 前綴讓 <see cref="CharityApiFixture.CleanupAsync"/> 清得掉）。</summary>
    private static Dictionary<string, object?> ProjectBody(string name, Action<Dictionary<string, object?>>? tweak = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["slug"] = $"ct-{NewKey()}"[..14],
            ["nameZh"] = name,
            ["nameEn"] = "CT Project Name",
            ["oneLinerZh"] = "一句話說明",
            ["fundUsageZh"] = "款項用途說明",
            ["minAmount"] = 100,
            ["maxAmount"] = 10000,
            ["amountOptions"] = new[] { 500, 100, 300 },
            ["invoiceMode"] = "b2c_invoice",
        };
        tweak?.Invoke(body);
        return body;
    }

    private sealed class Env(string name) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static MainSiteCharity Charity(Guid id, string zh, string? en = "Charity") => new(id, $"slug-{id:N}"[..12], zh, en);

    private static MainSiteProgram Program(Guid id, Guid charityId, string zh, string? en = "Program") => new(id, $"prog-{id:N}"[..12], charityId, zh, en);

    private async Task<(string? Name, string? NameEn, bool Active, string Source, string Code)> CharityRow(Guid externalId)
    {
        await using var c = new SqlConnection(fx.ConnectionString);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name, name_en, is_active, source, ref_code FROM charity_refs WHERE external_id = @e";
        cmd.Parameters.AddWithValue("@e", externalId);
        await using var r = await cmd.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync(), "複本找不到這個 external_id");
        return (r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetBoolean(2), r.GetString(3), r.GetString(4));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 對齊行為
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 同步_新增_改名_下架標停用不刪列_重新公開恢復_ref_code由主站id決定且不變()
    {
        using var client = await SysAsync();
        var charityId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var otherCharityId = Guid.NewGuid();

        // 1. 第一次：全新增
        fx.MainSite.SetDirectory(
            [Charity(charityId, "CT公益團體甲", "CT Charity A"), Charity(otherCharityId, "CT公益團體乙", null)],
            [Program(programId, charityId, "CT計畫一", "CT Program One")]);
        var first = await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        Assert.Equal(2, first.Charities.Created);
        Assert.Equal(1, first.Programs.Created);
        Assert.Equal(0, first.Charities.Deactivated);

        var row = await CharityRow(charityId);
        Assert.Equal("CT公益團體甲", row.Name);
        Assert.Equal("CT Charity A", row.NameEn);
        Assert.True(row.Active);
        Assert.Equal("main_site_api", row.Source);
        Assert.Equal(charityId.ToString("N"), row.Code);                       // ref_code ＝ 主站 id 的 32 碼十六進位
        Assert.Null((await CharityRow(otherCharityId)).NameEn);                // 主站沒填英文 → 空，不以中文頂替
        Assert.Contains("/api/v1/tcrfc/charity/directory", fx.MainSite.RequestedUris.Single());

        // 2. 沒變：全部 unchanged
        var same = await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        Assert.Equal(2, same.Charities.Unchanged);
        Assert.Equal(1, same.Programs.Unchanged);
        Assert.Equal(0, same.Charities.Created + same.Charities.Updated + same.Charities.Deactivated + same.Charities.Reactivated);

        // 3. 改名（中、英）＋乙下架：甲更新、乙停用但列還在
        fx.MainSite.SetDirectory([Charity(charityId, "CT公益團體甲（新名）", "CT Charity A2")], [Program(programId, charityId, "CT計畫一", "CT Program One")]);
        var changed = await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        Assert.Equal(1, changed.Charities.Updated);
        Assert.Equal(1, changed.Charities.Deactivated);
        var renamed = await CharityRow(charityId);
        Assert.Equal("CT公益團體甲（新名）", renamed.Name);
        Assert.Equal("CT Charity A2", renamed.NameEn);
        Assert.Equal(row.Code, renamed.Code);                                  // ref_code 不因改名而變
        var disabled = await CharityRow(otherCharityId);
        Assert.False(disabled.Active);                                         // 標停用，列還在
        Assert.Equal("CT公益團體乙", disabled.Name);
        Assert.NotNull(await fx.ScalarAsync<DateTime?>("SELECT deactivated_at FROM charity_refs WHERE external_id = @e", ("@e", otherCharityId)));

        // 4. 乙重新公開（含計畫整個消失再回來）：恢復啟用、清空停用時間
        fx.MainSite.SetDirectory([Charity(charityId, "CT公益團體甲（新名）", "CT Charity A2"), Charity(otherCharityId, "CT公益團體乙", "CT Charity B")], []);
        var back = await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        Assert.Equal(1, back.Charities.Reactivated);
        Assert.Equal(1, back.Programs.Deactivated);                            // 計畫一這次不在清單 → 停用
        Assert.True((await CharityRow(otherCharityId)).Active);
        Assert.Null(await fx.ScalarAsync<DateTime?>("SELECT deactivated_at FROM charity_refs WHERE external_id = @e", ("@e", otherCharityId)));
        Assert.False(await fx.ScalarAsync<bool>("SELECT is_active FROM charity_program_refs WHERE external_id = @e", ("@e", programId)));
    }

    [Fact]
    public async Task 同步_不碰external_id為空的既有列_清單為空時只停用同步來的列()
    {
        using var client = await SysAsync();
        var legacy = await fx.ScalarAsync<int>("SELECT COUNT(*) FROM charity_refs WHERE external_id IS NULL AND is_active = 1");
        var id = Guid.NewGuid();
        fx.MainSite.SetDirectory([Charity(id, "CT公益團體")], []);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        fx.MainSite.SetDirectory([], []);                                      // 主站全部下架：合法，整份對齊
        var result = await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        Assert.False((await CharityRow(id)).Active);
        Assert.True(result.Charities.Deactivated >= 1);
        Assert.Equal(legacy, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM charity_refs WHERE external_id IS NULL AND is_active = 1")); // 既有列不受影響
    }

    [Fact]
    public async Task 同步_計畫改掛到另一個公益團體_跟著主站_所屬公益團體不公開的計畫略過()
    {
        using var client = await SysAsync();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        var moving = Guid.NewGuid();
        var orphanLike = Guid.NewGuid();

        fx.MainSite.SetDirectory([Charity(a, "CT團體A"), Charity(b, "CT團體B")], [Program(moving, a, "CT移動的計畫")]);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        Assert.Equal(a.ToString("N"), await fx.ScalarAsync<string>(
            "SELECT c.ref_code FROM charity_program_refs p JOIN charity_refs c ON c.id = p.charity_ref_id WHERE p.external_id = @e", ("@e", moving)));

        fx.MainSite.SetDirectory(
            [Charity(a, "CT團體A"), Charity(b, "CT團體B"), new MainSiteCharity(hidden, "hidden-x", "CT未公開團體", null, "draft")],
            [Program(moving, b, "CT移動的計畫"), Program(orphanLike, hidden, "CT掛在未公開團體下")]);
        var result = await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        Assert.Equal(1, result.Programs.Updated);
        Assert.Equal(0, result.Programs.Created);
        Assert.Equal(b.ToString("N"), await fx.ScalarAsync<string>(
            "SELECT c.ref_code FROM charity_program_refs p JOIN charity_refs c ON c.id = p.charity_ref_id WHERE p.external_id = @e", ("@e", moving)));
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM charity_program_refs WHERE external_id = @e", ("@e", orphanLike)));
        Assert.Equal(0, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM charity_refs WHERE external_id = @e", ("@e", hidden)));
    }

    [Fact]
    public async Task 同步_寫稽核紀錄_摘要不含個資()
    {
        var admin = await fx.CreateAdminAsync(true);
        using var client = fx.CreateClientFor(admin);
        fx.MainSite.SetDirectory([Charity(Guid.NewGuid(), "CT公益團體")], []);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        var summary = await fx.ScalarAsync<string>(
            "SELECT change_summary FROM audit_logs WHERE admin_user_id = @a AND action = @x AND target_type = @t",
            ("@a", admin.Id), ("@x", CharityAuditActions.CharityRefSync), ("@t", CharityAuditTargets.CharityRef));
        Assert.Contains("新增 1", summary);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 主站回錯：複本一個欄位都不動
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("status500")]
    [InlineData("notJson")]
    [InlineData("missingPrograms")]
    [InlineData("missingZhName")]
    [InlineData("duplicateId")]
    [InlineData("programWithoutCharity")]
    [InlineData("nameTooLong")]
    [InlineData("unreachable")]
    [InlineData("timeout")]
    public async Task 主站回錯或回壞資料_回502_複本完全不動(string scenario)
    {
        using var client = await SysAsync();
        var existing = Guid.NewGuid();
        fx.MainSite.SetDirectory([Charity(existing, "CT既有團體", "CT Existing")], []);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        var before = await _snapshot.FingerprintAsync(fx.ConnectionString);

        var dupe = Guid.NewGuid();
        switch (scenario)
        {
            case "status500": fx.MainSite.SetStatus(HttpStatusCode.InternalServerError); break;
            case "notJson": fx.MainSite.SetRaw("<html>oops</html>"); break;
            case "missingPrograms": fx.MainSite.SetRaw("{\"charities\":[]}"); break;
            case "missingZhName": fx.MainSite.SetRaw($"{{\"charities\":[{{\"id\":\"{Guid.NewGuid()}\",\"slug\":\"x\",\"status\":\"published\",\"nameZh\":\"  \"}}],\"programs\":[]}}"); break;
            case "duplicateId":
                fx.MainSite.SetDirectory([Charity(dupe, "CT重複一"), Charity(dupe, "CT重複二")], []);
                break;
            case "programWithoutCharity":
                fx.MainSite.SetDirectory([Charity(existing, "CT既有團體")], [Program(Guid.NewGuid(), Guid.NewGuid(), "CT孤兒計畫")]);
                break;
            case "nameTooLong": fx.MainSite.SetDirectory([Charity(Guid.NewGuid(), new string('字', 129))], []); break;
            case "unreachable": fx.MainSite.ThrowOnSend(new HttpRequestException("connection refused")); break;
            case "timeout": fx.MainSite.ThrowOnSend(new TaskCanceledException("timeout")); break;
        }

        var response = await client.PostAsync(Sync, null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("複本沒有任何變動", body);
        Assert.DoesNotContain("main-site.test", body);                          // 不洩漏內部網址
        Assert.Equal(before, await _snapshot.FingerprintAsync(fx.ConnectionString));
    }

    [Fact]
    public async Task 同步_需要N2項目管理的寫入權限_只有檢視權限回403_未登入回401()
    {
        var viewOnly = fx.CreateClientFor(await fx.CreateAdminAsync(false, await fx.CreateRoleAsync("n2.donation_project.view")));
        var forbidden = await viewOnly.PostAsync(Sync, null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, fx.MainSite.Calls);                                     // 沒權限不會去打主站

        using var anonymous = fx.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Sync, null)).StatusCode);

        var manager = fx.CreateClientFor(await fx.CreateAdminAsync(false, await fx.CreateRoleAsync("n2.donation_project.manage")));
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync(Sync, null)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 並發
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 並發同步_被應用程式鎖串行化_不重複建立_恰好一次新增()
    {
        var id = Guid.NewGuid();
        var programId = Guid.NewGuid();
        fx.MainSite.SetDirectory([Charity(id, "CT並發團體")], [Program(programId, id, "CT並發計畫")]);
        fx.MainSite.Delay = TimeSpan.FromMilliseconds(250);                    // 讓四個請求確實重疊在鎖的視窗內

        var clients = new List<HttpClient>();
        for (var i = 0; i < 4; i++)
        {
            clients.Add(await SysAsync());
        }

        var responses = await Task.WhenAll(clients.Select(c => c.PostAsync(Sync, null)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var results = new List<AdminCharityRefSyncResultDto>();
        foreach (var r in responses)
        {
            results.Add((await r.Content.ReadFromJsonAsync<AdminCharityRefSyncResultDto>(TestJson.Options))!);
        }

        Assert.Equal(1, results.Sum(r => r.Charities.Created));                 // 只有第一個拿到鎖的建立；其餘看到已存在
        Assert.Equal(1, results.Sum(r => r.Programs.Created));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM charity_refs WHERE external_id = @e", ("@e", id)));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM charity_program_refs WHERE external_id = @e", ("@e", programId)));
        foreach (var c in clients)
        {
            c.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 下拉選單與項目選用
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 下拉只列啟用中者_帶projectId時保留該項目已選用的停用項目_新選或改選停用項目被擋_原樣保留可存()
    {
        using var client = await SysAsync();
        var keep = Guid.NewGuid();
        var other = Guid.NewGuid();
        var programKeep = Guid.NewGuid();
        fx.MainSite.SetDirectory([Charity(keep, "CT保留團體", "CT Keep"), Charity(other, "CT其他團體", "CT Other")], [Program(programKeep, keep, "CT保留計畫", "CT Keep Program")]);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        var keepCode = keep.ToString("N");
        var otherCode = other.ToString("N");
        var programCode = programKeep.ToString("N");

        // 項目選用「保留團體＋計畫」
        var created = await ReadAsync<AdminProjectDetailDto>(await client.PostAsJsonAsync(Projects, ProjectBody("CT選用項目", b => { b["charityRefCode"] = keepCode; b["charityProgramRefCode"] = programCode; }), TestJson.WriteOptions));
        Assert.Equal("CT保留團體", created.CharityName);

        // 主站把「保留團體」與其計畫都下架
        fx.MainSite.SetDirectory([Charity(other, "CT其他團體", "CT Other")], []);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        // 一般下拉：看不到停用者
        var plain = await ReadAsync<AdminCharityRefOptionsDto>(await client.GetAsync(Refs));
        Assert.DoesNotContain(plain.Charities, c => c.RefCode == keepCode);
        Assert.DoesNotContain(plain.Programs, p => p.RefCode == programCode);
        Assert.Contains(plain.Charities, c => c.RefCode == otherCode && c.IsActive && c.NameEn == "CT Other");
        Assert.NotNull(plain.LastSyncedAt);

        // 帶 projectId：保留該項目已選用的（標示停用），其餘停用者仍不出現
        var withProject = await ReadAsync<AdminCharityRefOptionsDto>(await client.GetAsync($"{Refs}?projectId={created.Id}"));
        Assert.Contains(withProject.Charities, c => c.RefCode == keepCode && !c.IsActive);
        Assert.Contains(withProject.Programs, p => p.RefCode == programCode && !p.IsActive && p.CharityRefCode == keepCode);

        // 新增項目選用停用者 → 400；改選停用者 → 400
        var newPick = await client.PostAsJsonAsync(Projects, ProjectBody("CT新選停用", b => b["charityRefCode"] = keepCode), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, newPick.StatusCode);
        Assert.Contains("不再公開", await newPick.Content.ReadAsStringAsync());

        // 已選用它的既有項目：原樣保留、照常顯示、可以儲存（快照沿用複本中的最後名稱）
        var reread = await ReadAsync<AdminProjectDetailDto>(await client.GetAsync($"{Projects}/{created.Id}"));
        Assert.Equal("CT保留團體", reread.CharityName);
        Assert.Equal("CT保留計畫", reread.CharityProgramName);
        var resaved = await client.PutAsJsonAsync($"{Projects}/{created.Id}", ProjectBody("CT選用項目（改）", b => { b["slug"] = created.Slug; b["charityRefCode"] = keepCode; b["charityProgramRefCode"] = programCode; }), TestJson.WriteOptions);
        Assert.True(resaved.IsSuccessStatusCode, await resaved.Content.ReadAsStringAsync());

        // 另一個項目（沒選過它）去改選停用者 → 400
        var other2 = await ReadAsync<AdminProjectDetailDto>(await client.PostAsJsonAsync(Projects, ProjectBody("CT另一項目"), TestJson.WriteOptions));
        var switchTo = await client.PutAsJsonAsync($"{Projects}/{other2.Id}", ProjectBody("CT另一項目", b => { b["slug"] = other2.Slug; b["charityProgramRefCode"] = programCode; }), TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, switchTo.StatusCode);
    }

    [Fact]
    public async Task 改名與下架不影響已存項目的名稱快照_憑證用的快照維持中文()
    {
        using var client = await SysAsync();
        var id = Guid.NewGuid();
        fx.MainSite.SetDirectory([Charity(id, "CT改名前", "CT Before")], []);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));
        var project = await ReadAsync<AdminProjectDetailDto>(await client.PostAsJsonAsync(Projects, ProjectBody("CT快照項目", b => b["charityRefCode"] = id.ToString("N")), TestJson.WriteOptions));

        fx.MainSite.SetDirectory([Charity(id, "CT改名後", "CT After")], []);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        var reread = await ReadAsync<AdminProjectDetailDto>(await client.GetAsync($"{Projects}/{project.Id}"));
        Assert.Equal("CT改名前", reread.CharityName);                           // 快照不隨主站改名
        Assert.Equal("CT改名前", await fx.ScalarAsync<string>("SELECT charity_name_snapshot FROM donation_projects WHERE id = @i", ("@i", project.Id)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 公開前台：英文頁輸出英文名稱（K-3）、缺英文回退並標示（K-10）
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 英文頁輸出複本的英文名稱_複本沒有英文時回退中文快照並標示語系提示_中文頁用快照()
    {
        using var client = await SysAsync();
        var withEn = Guid.NewGuid();
        var noEn = Guid.NewGuid();
        var programEn = Guid.NewGuid();
        fx.MainSite.SetDirectory(
            [Charity(withEn, "CT有英文團體", "CT Charity With English"), Charity(noEn, "CT無英文團體", null)],
            [Program(programEn, withEn, "CT有英文計畫", "CT Program With English")]);
        await ReadAsync<AdminCharityRefSyncResultDto>(await client.PostAsync(Sync, null));

        var (idA, slugA) = await CreateProjectAsync(fx, nameEn: "CT Project A");
        var (idB, slugB) = await CreateProjectAsync(fx, nameEn: "CT Project B");
        await fx.ExecuteAsync(
            """
            UPDATE donation_projects SET charity_ref_code = @c1, charity_name_snapshot = N'CT有英文團體', charity_program_ref_code = @p1, charity_program_name_snapshot = N'CT有英文計畫' WHERE id = @a;
            UPDATE donation_projects SET charity_ref_code = @c2, charity_name_snapshot = N'CT無英文團體' WHERE id = @b;
            """,
            ("@c1", withEn.ToString("N")), ("@p1", programEn.ToString("N")), ("@c2", noEn.ToString("N")), ("@a", idA), ("@b", idB));
        using var anonymous = fx.CreateClient();

        var enA = (await anonymous.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{slugA}?lang=en", TestJson.Options))!;
        Assert.Equal("CT Charity With English", enA.CharityName);
        Assert.Equal("CT Program With English", enA.CharityProgramName);
        Assert.False(enA.IsFallback);

        var enB = (await anonymous.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{slugB}?lang=en", TestJson.Options))!;
        Assert.Equal("CT無英文團體", enB.CharityName);                            // 複本沒有英文 → 回退快照
        Assert.True(enB.IsFallback);                                            // 並出現語系提示

        var zhA = (await anonymous.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{slugA}?lang=zh", TestJson.Options))!;
        Assert.Equal("CT有英文團體", zhA.CharityName);                            // 中文頁永遠用快照
        Assert.Equal("CT有英文計畫", zhA.CharityProgramName);
        Assert.False(zhA.IsFallback);

        // 成果回顧同理
        var impactEn = (await anonymous.GetFromJsonAsync<PublicImpactDto>($"{Base}/impact?lang=en", TestJson.Options))!;
        var group = impactEn.Programs.Single(p => p.ProgramRefCode == programEn.ToString("N"));
        Assert.Equal("CT Program With English", group.ProgramName);
        Assert.Equal("CT Charity With English", group.CharityName);
        var groupB = impactEn.Programs.Single(p => p.ProgramRefCode is null && p.CharityName == "CT無英文團體");
        Assert.Contains(groupB.Projects, p => p.Slug == slugB);
        Assert.True(impactEn.IsFallback);
        var impactZh = (await anonymous.GetFromJsonAsync<PublicImpactDto>($"{Base}/impact?lang=zh", TestJson.Options))!;
        Assert.Equal("CT有英文團體", impactZh.Programs.Single(p => p.ProgramRefCode == programEn.ToString("N")).CharityName);
    }

    [Fact]
    public async Task 英文名稱以中文頂替時英文頁也出現語系提示_K10()
    {
        // 後台只填英文說明、沒填英文名稱時，系統以中文名稱頂替英文名稱列（name 必填）。
        var (id, slug) = await CreateProjectAsync(fx);                          // 只有繁中列
        await fx.ExecuteAsync(
            "INSERT INTO donation_projects_i18n (donation_project_id, locale, name, one_liner) SELECT donation_project_id, N'en', name, N'English one liner' FROM donation_projects_i18n WHERE donation_project_id = @i AND locale = N'zh-Hant'",
            ("@i", id));
        var (_, realEnSlug) = await CreateProjectAsync(fx, nameEn: "CT Real English Name");
        using var anonymous = fx.CreateClient();

        var detail = (await anonymous.GetFromJsonAsync<PublicProjectDetailDto>($"{Base}/projects/{slug}?lang=en", TestJson.Options))!;
        Assert.True(detail.IsFallback);

        var cards = (await anonymous.GetFromJsonAsync<List<PublicProjectCardDto>>($"{Base}/projects?lang=en", TestJson.Options))!;
        Assert.True(cards.Single(c => c.Slug == slug).IsFallback);
        Assert.False(cards.Single(c => c.Slug == realEnSlug).IsFallback);       // 真的有英文名稱的不受影響
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 設定解析（純函式）
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null, "Development", "http://127.0.0.1:5299")]
    [InlineData(null, "Production", null)]
    [InlineData("  ", "Production", null)]
    [InlineData("http://127.0.0.1:8080", "Production", "http://127.0.0.1:8080")]
    [InlineData("http://127.0.0.1:8080/", "Production", "http://127.0.0.1:8080")]
    [InlineData("https://api.example.org/", "Production", "https://api.example.org")]
    [InlineData("not a url", "Development", null)]
    [InlineData("ftp://x.example.org", "Production", null)]
    public void 主站API基底網址_本機有預設_非本機未設定回null_不合法回null(string? configured, string environmentName, string? expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CharityOptions.MainSiteApiBaseUrlConfigKey] = configured })
            .Build();
        var environment = new Env(environmentName);

        Assert.Equal(expected, CharityOptions.ResolveMainSiteApiBaseUrl(configuration, environment));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 兩張複本表的整份快照與還原
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class RefTables
    {
        private readonly List<Dictionary<string, object?>> _charities = [];
        private readonly List<Dictionary<string, object?>> _programs = [];

        private static async Task<List<Dictionary<string, object?>>> ReadAsync(SqlConnection c, string table)
        {
            var rows = new List<Dictionary<string, object?>>();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table}";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            return rows;
        }

        public static async Task<RefTables> TakeAsync(string connectionString)
        {
            var s = new RefTables();
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            s._charities.AddRange(await ReadAsync(c, "charity_refs"));
            s._programs.AddRange(await ReadAsync(c, "charity_program_refs"));
            return s;
        }

        /// <summary>目前兩張表的內容指紋（用來斷言「主站回錯時複本一個欄位都沒動」）。</summary>
        public async Task<string> FingerprintAsync(string connectionString)
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            var sb = new System.Text.StringBuilder();
            foreach (var table in new[] { "charity_refs", "charity_program_refs" })
            {
                foreach (var row in (await ReadAsync(c, table)).OrderBy(r => (long)r["seq"]!))
                {
                    sb.Append(table).Append('|').AppendJoin('|', row.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}")).Append('\n');
                }
            }

            return sb.ToString();
        }

        public async Task RestoreAsync(string connectionString)
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            await using var t = (SqlTransaction)await c.BeginTransactionAsync();

            async Task Exec(string sql, params (string, object?)[] ps)
            {
                await using var cmd = c.CreateCommand();
                cmd.Transaction = t;
                cmd.CommandText = sql;
                foreach (var (n, v) in ps)
                {
                    cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
                }

                await cmd.ExecuteNonQueryAsync();
            }

            // 先刪快照之外新增的列（計畫先於團體，外鍵），再把快照內的列逐欄還原。同步只新增與更新、從不刪列，所以不需要重新插入。
            var programIds = _programs.Select(r => (Guid)r["id"]!).ToList();
            var charityIds = _charities.Select(r => (Guid)r["id"]!).ToList();
            await Exec($"DELETE FROM charity_program_refs{(programIds.Count > 0 ? $" WHERE id NOT IN ({string.Join(",", programIds.Select(i => $"'{i}'"))})" : string.Empty)}");
            await Exec($"DELETE FROM charity_refs{(charityIds.Count > 0 ? $" WHERE id NOT IN ({string.Join(",", charityIds.Select(i => $"'{i}'"))})" : string.Empty)}");

            foreach (var row in _charities)
            {
                await Exec(
                    "UPDATE charity_refs SET ref_code=@ref_code, name=@name, name_en=@name_en, external_id=@external_id, is_active=@is_active, deactivated_at=@deactivated_at, last_synced_at=@last_synced_at, imported_at=@imported_at, source=@source, created_at=@created_at, updated_at=@updated_at, created_by=@created_by, updated_by=@updated_by WHERE id=@id",
                    Params(row, "ref_code", "name", "name_en", "external_id", "is_active", "deactivated_at", "last_synced_at", "imported_at", "source", "created_at", "updated_at", "created_by", "updated_by", "id"));
            }

            foreach (var row in _programs)
            {
                await Exec(
                    "UPDATE charity_program_refs SET ref_code=@ref_code, charity_ref_id=@charity_ref_id, name=@name, name_en=@name_en, external_id=@external_id, is_active=@is_active, deactivated_at=@deactivated_at, last_synced_at=@last_synced_at, imported_at=@imported_at, created_at=@created_at, updated_at=@updated_at, created_by=@created_by, updated_by=@updated_by WHERE id=@id",
                    Params(row, "ref_code", "charity_ref_id", "name", "name_en", "external_id", "is_active", "deactivated_at", "last_synced_at", "imported_at", "created_at", "updated_at", "created_by", "updated_by", "id"));
            }

            await t.CommitAsync();
        }

        private static (string, object?)[] Params(Dictionary<string, object?> row, params string[] columns)
            => columns.Select(col => ("@" + col, row[col])).ToArray();
    }
}
