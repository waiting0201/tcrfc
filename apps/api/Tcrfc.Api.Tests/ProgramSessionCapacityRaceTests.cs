using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 課程梯次名額的並行壓測（E-172／E-173 同類）。語意（規劃書 P2「名額控管：額滿自動關閉、候補遞補提醒」）：名額是<b>硬上限</b>——
/// 搶到最後一個名額的轉「額滿」，其餘一律進「候補」（仍成立報名、不是拒絕）；已報名數絕不超過上限。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ProgramSessionCapacityRaceTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 梯次剩1名額_16個不同報名者並行_16並行乘10輪_恰好一個待確認_其餘候補_已報名數恰為上限_狀態額滿()
    {
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        var clubScope = ClubScopeTestFactory.Create(clubId, "tcrfc");
        var ids = new List<Guid>();
        try
        {
            for (var round = 0; round < 10; round++)
            {
                var sessionId = Guid.NewGuid();
                ids.Add(sessionId);
                await BizTest.ExecuteSqlAsync(
                    """
                    DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                    DECLARE @prog uniqueidentifier = (SELECT TOP 1 id FROM programs WHERE club_id = @club);
                    INSERT INTO sessions (id, club_id, program_id, capacity, enrolled_count, status) VALUES (@I, @club, @prog, 1, 0, N'開放');
                    """, ("@I", sessionId));
                // 公開端點掛了「20 次／5 分鐘／IP」的濫用防護（PublicRateLimitPolicies.Submission），16×10 輪的 HTTP 壓測會被它擋下（429）；
                // 名額競爭發生在儲存層，所以直接並行呼叫 ProgramsRepository（各自獨立的 DI scope＝各自的連線）。
                var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
                {
                    using var scope = fixture.Services.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<ProgramsRepository>();
                    return await repository.SubmitRegistrationAsync(clubScope, sessionId,
                        new SubmitProgramRegistrationRequest { ApplicantName = $"ZZ名額{round}-{i}", Phone = $"0933{round:00}{i:0000}" }, CancellationToken.None);
                })));
                Assert.Equal(1, results.Count(r => r.Status == "待確認"));
                Assert.Equal(15, results.Count(r => r.Status == "候補"));
                Assert.Equal(1, await C1Test.ScalarAsync<int>("SELECT enrolled_count FROM sessions WHERE id = @I", ("@I", sessionId)));
                Assert.Equal("額滿", await C1Test.ScalarAsync<string>("SELECT status FROM sessions WHERE id = @I", ("@I", sessionId)));
                Assert.Equal(16, await C1Test.ScalarAsync<int>("SELECT COUNT(*) FROM registrations WHERE session_id = @I", ("@I", sessionId)));
            }
        }
        finally
        {
            foreach (var id in ids)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE session_id = @I; DELETE FROM sessions WHERE id = @I;", ("@I", id));
            }
        }
    }
}
