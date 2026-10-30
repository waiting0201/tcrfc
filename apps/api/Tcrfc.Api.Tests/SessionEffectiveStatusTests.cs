using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 梯次對外狀態必須與報名閘門同一套時窗規則（2026-10-30 Android 實機驗收：已過截止的梯次仍顯示「開放」）。
/// 純函式部分不需資料庫；整合部分驗證公開詳情輸出與閘門結論一致。
/// </summary>
public sealed class SessionEffectiveStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 已過截止_開放變已結束()
        => Assert.Equal("ended", EnrollmentStatus.OfSlotAt("開放", null, Now.AddSeconds(-1), Now).Code);

    [Fact]
    public void 額滿與已截止並存_以已結束為準()
        => Assert.Equal("ended", EnrollmentStatus.OfSlotAt("額滿", null, Now.AddDays(-1), Now).Code);

    [Fact]
    public void 候補與已截止並存_以已結束為準()
        => Assert.Equal("ended", EnrollmentStatus.OfSlotAt("候補", null, Now.AddDays(-1), Now).Code);

    [Fact]
    public void 時窗內_額滿仍為額滿()
        => Assert.Equal("full", EnrollmentStatus.OfSlotAt("額滿", Now.AddDays(-5), Now.AddDays(5), Now).Code);

    [Fact]
    public void 時窗內_開放仍為開放()
        => Assert.Equal("open", EnrollmentStatus.OfSlotAt("開放", Now.AddDays(-5), Now.AddDays(5), Now).Code);

    [Fact]
    public void 恰等於截止時刻_視為仍在時窗內_與閘門一致()
    {
        Assert.Equal("open", EnrollmentStatus.OfSlotAt("開放", null, Now, Now).Code);
        Assert.Equal(SignupWindowState.Within, SignupWindow.Evaluate(null, Now, Now));
    }

    [Fact]
    public void 尚未開放_儲存為開放_輸出not_yet_open_且閘門判定為尚未開放()
    {
        var s = EnrollmentStatus.OfSlotAt("開放", Now.AddDays(1), null, Now);
        Assert.Equal(("not_yet_open", "尚未開放", "Not yet open"), s);
        Assert.Equal(SignupWindowState.NotYetOpen, SignupWindow.Evaluate(Now.AddDays(1), null, Now));
    }

    [Fact]
    public void 尚未開放加額滿_以尚未開放為準()
        => Assert.Equal("not_yet_open", EnrollmentStatus.OfSlotAt("額滿", Now.AddDays(1), Now.AddDays(9), Now).Code);

    [Fact]
    public void 尚未開放加候補_以尚未開放為準_不接受候補()
        => Assert.Equal("not_yet_open", EnrollmentStatus.OfSlotAt("候補", Now.AddDays(1), null, Now).Code);

    [Fact]
    public void 恰等於開始時刻_仍可報名_輸出儲存狀態()
    {
        Assert.Equal("open", EnrollmentStatus.OfSlotAt("開放", Now, null, Now).Code);
        Assert.Equal("full", EnrollmentStatus.OfSlotAt("額滿", Now, null, Now).Code);
        Assert.Equal(SignupWindowState.Within, SignupWindow.Evaluate(Now, null, Now));
    }

    [Fact]
    public void 開始時刻前一秒_尚未開放()
        => Assert.Equal("not_yet_open", EnrollmentStatus.OfSlotAt("開放", Now.AddSeconds(1), null, Now).Code);

    [Fact]
    public void 儲存已結束加尚未開放_以已結束為準()
        => Assert.Equal("ended", EnrollmentStatus.OfSlotAt("已結束", Now.AddDays(1), null, Now).Code);

    [Fact]
    public void 已過截止加尚未開放_資料矛盾時以已結束為準()
        => Assert.Equal("ended", EnrollmentStatus.OfSlotAt("開放", Now.AddDays(2), Now.AddDays(-1), Now).Code);

    [Fact]
    public void 尚未開放不是儲存值_不在Slot字典()
        => Assert.DoesNotContain("尚未開放", EnrollmentStatus.Slot.Keys);

    [Fact]
    public void 兩端皆未設定_維持儲存狀態()
        => Assert.Equal("waitlist", EnrollmentStatus.OfSlotAt("候補", null, null, Now).Code);

    [Fact]
    public void 儲存狀態已結束_無論時窗皆為已結束()
        => Assert.Equal("ended", EnrollmentStatus.OfSlotAt("已結束", Now.AddDays(-5), Now.AddDays(5), Now).Code);
}

[Collection(AdminWriteCollection.Name)]
public sealed class SessionEffectiveStatusIntegrationTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 公開詳情_已截止梯次為ended_且送出報名被拒_時窗內梯次為open()
    {
        var clubId = await BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = N'tcrfc'");
        var scope = ClubScopeTestFactory.Create(clubId, "tcrfc");
        var closedId = Guid.NewGuid();
        var openId = Guid.NewGuid();
        var notYetId = Guid.NewGuid();
        try
        {
            var slug = await C1Test.ScalarAsync<string>("SELECT TOP 1 slug FROM programs WHERE club_id = (SELECT id FROM clubs WHERE code = N'tcrfc') AND status = 'published'");
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                DECLARE @prog uniqueidentifier = (SELECT TOP 1 id FROM programs WHERE club_id = @club AND status = 'published');
                INSERT INTO sessions (id, club_id, program_id, capacity, enrolled_count, status, signup_closes_at)
                    VALUES (@C, @club, @prog, 10, 0, N'開放', DATEADD(day, -1, SYSUTCDATETIME()));
                INSERT INTO sessions (id, club_id, program_id, capacity, enrolled_count, status, signup_closes_at)
                    VALUES (@O, @club, @prog, 10, 0, N'開放', DATEADD(day, 5, SYSUTCDATETIME()));
                INSERT INTO sessions (id, club_id, program_id, capacity, enrolled_count, status, signup_opens_at)
                    VALUES (@N, @club, @prog, 10, 10, N'額滿', DATEADD(day, 3, SYSUTCDATETIME()));
                """, ("@C", closedId), ("@O", openId), ("@N", notYetId));

            using var di = fixture.Services.CreateScope();
            var repository = di.ServiceProvider.GetRequiredService<ProgramsRepository>();
            var detail = await repository.GetBySlugAsync(scope, slug, "zh-Hant", CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Equal("ended", detail!.Sessions.Single(s => s.Id == closedId).StatusCode);
            Assert.Equal("open", detail.Sessions.Single(s => s.Id == openId).StatusCode);
            var notYet = detail.Sessions.Single(s => s.Id == notYetId);
            Assert.Equal("not_yet_open", notYet.StatusCode);
            Assert.Equal("尚未開放", notYet.StatusLabelZh);
            Assert.Equal("Not yet open", notYet.StatusLabelEn);
            Assert.Equal("額滿", notYet.Status); // 儲存值不動
            Assert.NotNull(notYet.SignupOpensAt);
            await Assert.ThrowsAsync<ProgramRegistrationValidationException>(() => repository.SubmitRegistrationAsync(
                scope, notYetId, new SubmitProgramRegistrationRequest { PrivacyConsent = true, ApplicantName = "ZZ未開放", Phone = "0933000002" }, CancellationToken.None));

            await Assert.ThrowsAsync<ProgramRegistrationValidationException>(() => repository.SubmitRegistrationAsync(
                scope, closedId, new SubmitProgramRegistrationRequest { PrivacyConsent = true, ApplicantName = "ZZ截止", Phone = "0933000001" }, CancellationToken.None));
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE session_id IN (@C, @O, @N); DELETE FROM sessions WHERE id IN (@C, @O, @N);", ("@C", closedId), ("@O", openId), ("@N", notYetId));
        }
    }
}
