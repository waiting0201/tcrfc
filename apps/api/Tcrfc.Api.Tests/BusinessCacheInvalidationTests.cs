using System.Net;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminSponsors;
using Tcrfc.Api.Features.CharityImpact;
using Tcrfc.Api.Features.Partners;
using Tcrfc.Api.Features.Sponsors;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// E1a 各模組寫入後公開快取真的失效（真實 <c>redis-server</c>，理由同 <c>AdminNewsCacheInvalidationTests</c>：no-op 快取驗不出
/// 「有沒有失效」）。每個案例先打公開端點暖快取（並確認 Redis 真的有 key），再經後台改值，最後斷言公開端點立刻回新值。
/// </summary>
[Collection(AdminWriteRedisEnabledCollection.Name)]
public sealed class BusinessCacheInvalidationTests(AdminWriteRedisEnabledApiFixture fixture)
{
    [Fact]
    public async Task 夥伴與贊助方案與捐款導流設定_寫入後公開端點立刻看到新值()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var server = fixture.RedisInspector.GetServer(fixture.RedisInspector.GetEndPoints()[0]);

        var partner = await BizTest.ReadAsync<AdminPartnerDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(
            new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】快取夥伴舊名") } })));
        var package = await BizTest.ReadAsync<AdminSponsorPackageDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsor-packages", BizTest.Json(new
        {
            status = "published", content = new { zh = new { name = "【測試】快取方案舊名" } },
        })));
        var originalCta = await BizTest.ReadAsync<Tcrfc.Api.Features.AdminCharity.AdminCharitySettingsDto>(await editor.GetAsync("/api/v1/admin/tcrfc/charity/settings"));
        try
        {
            // 夥伴
            var warm = await BizTest.ReadAsync<List<PartnerDto>>(await anonymous.GetAsync("/api/v1/tcrfc/partners"));
            Assert.Equal("【測試】快取夥伴舊名", warm.Single(p => p.Id == partner.Id).Name);
            Assert.NotEmpty(server.Keys(pattern: "*partners*").ToArray());
            var renamed = await business.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}", BizTest.Multipart(
                new { partnerType = "策略夥伴", content = new { zh = BizTest.Zh("【測試】快取夥伴新名") } }));
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
            var after = await BizTest.ReadAsync<List<PartnerDto>>(await anonymous.GetAsync("/api/v1/tcrfc/partners"));
            Assert.Equal("【測試】快取夥伴新名", after.Single(p => p.Id == partner.Id).Name);

            // 贊助方案
            var warmPackages = await BizTest.ReadAsync<List<SponsorPackageDto>>(await anonymous.GetAsync("/api/v1/tcrfc/sponsor-packages"));
            Assert.Equal("【測試】快取方案舊名", warmPackages.Single(p => p.Id == package.Id).Name);
            await business.PutAsync($"/api/v1/admin/tcrfc/sponsor-packages/{package.Id}", BizTest.Json(new
            {
                status = "published", content = new { zh = new { name = "【測試】快取方案新名" } },
            }));
            var afterPackages = await BizTest.ReadAsync<List<SponsorPackageDto>>(await anonymous.GetAsync("/api/v1/tcrfc/sponsor-packages"));
            Assert.Equal("【測試】快取方案新名", afterPackages.Single(p => p.Id == package.Id).Name);

            // 捐款導流設定
            var warmCta = await BizTest.ReadAsync<CharityCtaDto>(await anonymous.GetAsync("/api/v1/tcrfc/charity/cta?lang=zh"));
            Assert.NotNull(warmCta.DonationUrl);
            var changed = await editor.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(new
            {
                donationUrl = "https://charity.example.com/changed", donationCta = new { zh = "捐款（由台灣足球策略發展協會收受）" },
            }));
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var afterCta = await BizTest.ReadAsync<CharityCtaDto>(await anonymous.GetAsync("/api/v1/tcrfc/charity/cta?lang=zh"));
            Assert.Equal("https://charity.example.com/changed", afterCta.DonationUrl);
        }
        finally
        {
            await editor.PutAsync("/api/v1/admin/tcrfc/charity/settings", BizTest.Json(originalCta));
            await business.DeleteAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}");
            await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsor-packages/{package.Id}");
        }
    }
}
