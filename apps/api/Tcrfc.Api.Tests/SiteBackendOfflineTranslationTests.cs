using System.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminDashboard;
using Tcrfc.Api.Features.AdminEnquiries;
using Tcrfc.Api.Features.AdminNewsletter;
using Tcrfc.Api.Features.AdminSiteSettings;
using Tcrfc.Api.Features.AdminVenues;
using Tcrfc.Api.Features.Geocoding;
using Tcrfc.Api.Features.Newsletter;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Features.Trials;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 2026-10-02（儀表板、試訓公開端點、電子報、I 網站設定各子模組）：<b>不需要資料庫</b>的 EF 查詢翻譯冒煙測試（工具見 <see cref="OfflineQueryTranslation"/>）。
/// 這批 repository 的 LINQ 有大量巢狀導覽屬性投影、<c>Any</c>／<c>Count</c> 子查詢、多對多（<c>Staff</c>／<c>Teams</c>）——
/// 翻譯失敗只會在執行期爆，工作樹沒有資料庫時這是唯一能自動抓到它的檢查。通過只代表「翻得成 SQL」，
/// 資料行為（權限過濾、計數、語系回退、冪等）由需要資料庫的整合測試驗證（<c>DashboardApiTests</c> 等，合併後在有庫的環境執行）。
/// </summary>
public sealed class SiteBackendOfflineTranslationTests
{
    private static readonly Guid ClubId = Guid.NewGuid();

    private sealed class AllowAllPermissions : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(Guid adminUserId, bool isSuperAdmin, string permissionCode, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<IReadOnlySet<string>> GetHeldPermissionCodesAsync(
            Guid adminUserId, bool isSuperAdmin, IReadOnlyList<string> candidateCodes, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(candidateCodes, StringComparer.Ordinal));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAllHeldPermissionsAsync(
            Guid adminUserId, bool isSuperAdmin, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>());
    }

    private sealed class OfflineConnectionFactory : IClubSqlConnectionFactory
    {
        public IDbConnection CreateConnection()
            => new SqlConnection("Server=127.0.0.1,1;Database=offline_translation_only;Integrated Security=true;Encrypt=false;Connect Timeout=2");
    }

    private static AdminClubScope Admin() => ClubScopeTestFactory.CreateAdmin(ClubId, "tcrfc");

    private static ClubScope Public() => ClubScopeTestFactory.Create(ClubId, "tcrfc");

    private static readonly IQueryCache NoCache = new NoOpQueryCache();

    private static readonly IImagePublicUrlResolver Urls = new UnavailableImagePublicUrlResolver();

    private static AdminDashboardRepository Dashboard(ClubDbContext db)
    {
        var permissions = new AllowAllPermissions();
        return new AdminDashboardRepository(
            db, permissions, new AdminEnquiriesRepository(db, permissions),
            new TranslationStatusReader(new OfflineConnectionFactory()), new NotConfiguredAnalyticsSource());
    }

    private static bool Untranslatable(string slug) => slug.GetHashCode() == 3;

    /// <summary>工具自我驗證：確認這組測試不是「永遠通過」——真的不可翻譯的查詢會讓 <see cref="OfflineQueryTranslation"/> 判失敗，
    /// 可翻譯的查詢則一定會走到「送出被拒」（SqlException）而不是默默成功。</summary>
    [Fact]
    public async Task 翻譯冒煙工具自我驗證_不可翻譯會失敗_可翻譯會走到連線被拒()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => OfflineQueryTranslation.AssertTranslatesAsync(
            () => db.Articles.Where(a => Untranslatable(a.Slug)).ToListAsync()));
        await Assert.ThrowsAsync<SqlException>(() => db.Articles.Where(a => a.Slug == "x").ToListAsync());
    }

    [Fact]
    public async Task 儀表板首頁_全部區塊的查詢都能翻譯()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        await OfflineQueryTranslation.AssertTranslatesAsync(() => Dashboard(db).GetAsync(Admin(), CancellationToken.None));
    }

    [Theory]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData(null)]
    public async Task 儀表板轉換概況_週月都能翻譯(string? period)
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        await OfflineQueryTranslation.AssertTranslatesAsync(() => Dashboard(db).GetConversionAsync(Admin(), period, CancellationToken.None));
    }

    [Fact]
    public async Task 儀表板轉換概況_不合法的週期400()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        await Assert.ThrowsAsync<AdminValidationException>(() => Dashboard(db).GetConversionAsync(Admin(), "year", CancellationToken.None));
    }

    [Fact]
    public async Task 流量概況_未串接回尚未串接_不丟例外()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var traffic = await Dashboard(db).GetTrafficAsync(Admin(), CancellationToken.None);
        Assert.False(traffic.Configured);
        Assert.Null(traffic.Overview);
        Assert.Contains("尚未串接", traffic.Message);
        Assert.Equal(traffic.To.AddDays(-6), traffic.From);
    }

    [Fact]
    public void 儀表板放行權限碼集合_涵蓋各區塊與翻譯目錄與快速入口()
    {
        var codes = AdminDashboardRepository.AllCandidateCodes;
        Assert.Equal(codes.Count, codes.Distinct().Count());
        foreach (var expected in new[]
                 {
                     "enquiry.inbox.view", "program.registration.view", "program.session.view", "business.sponsor.view", "content.article.view",
                     "content.faq.view", "team.match.view", "member.membership.view", "content.article.create", "calendar.custom_event.create",
                 })
        {
            Assert.Contains(expected, codes);
        }

        foreach (var entity in TranslationStatusReader.Catalog)
        {
            Assert.Contains(entity.ViewPermission, codes);
        }
    }

    [Fact]
    public async Task 試訓公開清單_能翻譯()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new TrialsRepository(db);
        await OfflineQueryTranslation.AssertTranslatesAsync(() => repository.ListAsync(Public(), "D1", "en", CancellationToken.None));
    }

    [Fact]
    public async Task 試訓報名_驗證失敗不碰資料庫()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new TrialsRepository(db);
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubmitRegistrationAsync(
            Public(), Guid.NewGuid(), new SubmitTrialRegistrationRequest { ApplicantName = "  ", Phone = "0912" }, null, CancellationToken.None));
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubmitRegistrationAsync(
            Public(), Guid.NewGuid(), new SubmitTrialRegistrationRequest { ApplicantName = "王小明" }, null, CancellationToken.None));
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubmitRegistrationAsync(
            Public(), Guid.NewGuid(), new SubmitTrialRegistrationRequest { ApplicantName = "王小明", Email = "壞信箱" }, null, CancellationToken.None));
        // 未滿 18 歲沒有家長聯絡方式
        var minorBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14));
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubmitRegistrationAsync(
            Public(), Guid.NewGuid(), new SubmitTrialRegistrationRequest { ApplicantName = "王小明", Phone = "0912345678", BirthOn = minorBirth },
            null, CancellationToken.None));
        // 未來的出生日期
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubmitRegistrationAsync(
            Public(), Guid.NewGuid(), new SubmitTrialRegistrationRequest
            {
                ApplicantName = "王小明", Phone = "0912345678", BirthOn = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            }, null, CancellationToken.None));
    }

    [Fact]
    public async Task 前台站台設定_選單_站台設定_政策_介面字串_場地_都能翻譯()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new SiteSettingsRepository(db, NoCache, Urls);
        await OfflineQueryTranslation.AssertTranslatesAsync(() => repository.GetMenusAsync(Public(), "en", CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => repository.GetSiteSettingsAsync(Public(), "en", CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => repository.GetPolicyAsync(Public(), "privacy", "en", CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => repository.GetUiStringsAsync("en", "button", CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => repository.ListVenuesAsync(Public(), "en", CancellationToken.None));
    }

    [Fact]
    public async Task 未知的政策代碼_直接回null_不碰資料庫()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new SiteSettingsRepository(db, NoCache, Urls);
        Assert.Null(await repository.GetPolicyAsync(Public(), "unknown", "zh", CancellationToken.None));
    }

    [Fact]
    public async Task 後台選單_全域設定_多語系_字串翻譯_EDM_場地_都能翻譯()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var editor = new ClubSettingsEditor(db);
        var permissions = new AllowAllPermissions();

        await OfflineQueryTranslation.AssertTranslatesAsync(() => new AdminMenusRepository(db, NoCache).GetAsync(Admin(), CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => new AdminGlobalSettingsRepository(
            db, editor, NoCache, Urls, new SensitiveActionLogger(NullLogger<SensitiveActionLogger>.Instance)).GetAsync(Admin(), CancellationToken.None));

        var i18n = new AdminI18nRepository(db, editor, new TranslationStatusReader(new OfflineConnectionFactory()), NoCache);
        await OfflineQueryTranslation.AssertTranslatesAsync(() => i18n.ListLocalesAsync(CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => i18n.GetSettingsAsync(Admin(), CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => i18n.OverviewAsync(Admin(), "article", "en", "新聞", 1, 20, CancellationToken.None));

        var strings = new AdminUiStringsRepository(db, permissions, NoCache);
        await OfflineQueryTranslation.AssertTranslatesAsync(() => strings.ListAsync(
            new AdminUiStringListQuery { Group = "button", Keyword = "送出", Missing = "en", Page = 1, PageSize = 20 }, CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => strings.ListGroupsAsync(CancellationToken.None));

        var edm = new AdminEdmSettingsRepository(
            db, editor, new EphemeralDataProtectionProvider(), new NotConfiguredEdmSync(), new SensitiveActionLogger(NullLogger<SensitiveActionLogger>.Instance));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => edm.GetAsync(Admin(), CancellationToken.None));

        var venues = new AdminVenuesRepository(db, Urls, new NotConfiguredGeocoder(), NoCache);
        await OfflineQueryTranslation.AssertTranslatesAsync(() => venues.ListAsync(CancellationToken.None));
        await OfflineQueryTranslation.AssertTranslatesAsync(() => venues.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task 電子報訂閱_驗證失敗不碰資料庫_蜜罐直接回成功()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new NewsletterRepository(db, new NewsletterUnsubscribeTokens(new EphemeralDataProtectionProvider()));
        // 蜜罐有值：靜默丟棄，連信箱格式都不檢查、不碰資料庫
        await repository.SubscribeAsync(Public(), new SubscribeNewsletterRequest { Email = "bad", Consent = false, Website = "http://spam" }, CancellationToken.None);
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubscribeAsync(
            Public(), new SubscribeNewsletterRequest { Email = "壞信箱", Consent = true }, CancellationToken.None));
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubscribeAsync(
            Public(), new SubscribeNewsletterRequest { Email = "a@example.test", Consent = false }, CancellationToken.None));
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.SubscribeAsync(
            Public(), new SubscribeNewsletterRequest { Email = "a@example.test", Consent = true, Source = "不存在的入口" }, CancellationToken.None));
        // 無效憑證
        await Assert.ThrowsAsync<PublicValidationException>(() => repository.UnsubscribeAsync(Public(), "亂碼", CancellationToken.None));
    }

    [Fact]
    public void 退訂憑證_往返一致_竄改與跨環境失敗()
    {
        var tokens = new NewsletterUnsubscribeTokens(new EphemeralDataProtectionProvider());
        var clubId = Guid.NewGuid();
        var token = tokens.Create(clubId, "  Fan@Example.TEST ");
        var parsed = tokens.TryParse(token);
        Assert.NotNull(parsed);
        Assert.Equal(clubId, parsed!.Value.ClubId);
        Assert.Equal("fan@example.test", parsed.Value.Email);
        Assert.Null(tokens.TryParse(token + "x"));
        Assert.Null(tokens.TryParse(null));
        Assert.Null(tokens.TryParse(new string('a', 2000)));
        // 另一把金鑰環產生的憑證不能被這邊接受
        Assert.Null(new NewsletterUnsubscribeTokens(new EphemeralDataProtectionProvider()).TryParse(token));
    }

    [Theory]
    [InlineData("1,234.56", ",", ".")]
    [InlineData("1.234,56", ".", ",")]
    [InlineData("1 234,56", " ", ",")]
    [InlineData("1234.56", "", ".")]
    public void 數字格式範例_解析出千分位與小數點(string sample, string thousands, string dec)
    {
        var (t, d) = SiteSettingsRepository.ParseNumberFormat(sample);
        Assert.Equal(thousands, t);
        Assert.Equal(dec, d);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12,345.67")]
    [InlineData("abc")]
    public void 數字格式範例_不合法回null(string? sample)
        => Assert.Equal((null, null), SiteSettingsRepository.ParseNumberFormat(sample));
}
