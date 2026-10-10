using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCalendar;
using Tcrfc.Api.Features.AdminCharity;
using Tcrfc.Api.Features.AdminComics;
using Tcrfc.Api.Features.AdminDraws;
using Tcrfc.Api.Features.AdminFanEvents;
using Tcrfc.Api.Features.AdminPartners;
using Tcrfc.Api.Features.AdminPartnerStores;
using Tcrfc.Api.Features.AdminPlayers;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.AdminSponsors;
using Tcrfc.Api.Features.AdminStaff;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 圖片欄位組補齊（主站規劃書 §4.0「物件鍵、寬、高、雙語 Alt」；S0-7h 收尾，migration <c>ClubImageFieldGroupExpand</c>）：
/// 上傳後主檔縮小的寬高寫回（<c>TestImages.SmallPng</c> 是 500 寬）、逐語系 Alt 讀寫與過長 400、公開 DTO 輸出寬高與
/// 當前語系 Alt（英文空白回退繁中）、移除圖片後寬高清空；多圖子表用並排 <c>altZh／altEn</c> 與
/// <c>PUT .../images/{imageId}</c>（漫畫為 <c>.../pages/{pageId}</c>）。圖片對真實 Azurite，沒有 Azurite 時略過。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class ImageFieldGroupTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private static readonly (string Field, byte[] Bytes, string FileName, string ContentType) Png = ("file", TestImages.SmallPng(), "a.png", "image/png");

    private static string Long() => new('長', 201);

    private static async Task AddGalleryAsync(HttpClient client, string url, string field = "file")
    {
        var response = await client.PostAsync(url, BizTest.Multipart(new { }, (field, TestImages.SmallPng(), "g.png", "image/png")));
        Assert.True(response.IsSuccessStatusCode, $"{url} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    // ───────────────────────── 球隊／球員／教練 ─────────────────────────

    [AzuriteFact]
    public async Task 球隊主視覺_寬高寫回_Alt逐語系_公開輸出_英文空白回退_移除後清空()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var code = "Z" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        Guid? id = null;
        try
        {
            var tooLong = await admin.PostAsync("/api/v1/admin/tcrfc/teams", BizTest.Multipart(new
            {
                code, type = "academy", gender = "men", content = new { zh = new { name = "測試梯隊", heroAlt = Long() } },
            }));
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

            var created = await BizTest.ReadAsync<AdminTeamDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/teams", BizTest.Multipart(new
            {
                code, type = "academy", gender = "men",
                content = new { zh = new { name = "測試梯隊", heroAlt = "梯隊大合照" }, en = new { name = "Test squad" } },
            }, Png)));
            id = created.Id;
            Assert.Equal(500, created.HeroWidth);
            Assert.True(created.HeroHeight > 0);
            Assert.Equal("梯隊大合照", created.Zh.HeroAlt);
            Assert.Null(created.En!.HeroAlt);

            var zh = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Teams.TeamDto>>(await anonymous.GetAsync("/api/v1/tcrfc/teams?lang=zh"));
            var pubZh = zh.Single(t => t.Code == code);
            Assert.Equal(500, pubZh.HeroWidth);
            Assert.True(pubZh.HeroHeight > 0);
            Assert.Equal("梯隊大合照", pubZh.HeroAlt);
            var en = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Teams.TeamDto>>(await anonymous.GetAsync("/api/v1/tcrfc/teams?lang=en"));
            Assert.Equal("梯隊大合照", en.Single(t => t.Code == code).HeroAlt); // 英文沒填 → 回退繁中

            var removed = await BizTest.ReadAsync<AdminTeamDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/teams/{id}", BizTest.Multipart(new
            {
                code, type = "academy", gender = "men", removeHero = true, content = new { zh = new { name = "測試梯隊", heroAlt = "梯隊大合照" } },
            })));
            Assert.Null(removed.HeroKey);
            Assert.Null(removed.HeroWidth);
            Assert.Null(removed.HeroHeight);
            var after = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Teams.TeamDto>>(await anonymous.GetAsync("/api/v1/tcrfc/teams?lang=zh"));
            Assert.Null(after.Single(t => t.Code == code).HeroAlt);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM teams_i18n WHERE team_id IN (SELECT id FROM teams WHERE code = @C); DELETE FROM teams WHERE code = @C;", ("@C", code));
        }
    }

    [AzuriteFact]
    public async Task 球員與教練照片_寬高Alt_公開依肖像同意輸出_未同意時三者皆null()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var teamId = await BizTest.ScalarGuidAsync("SELECT t.id FROM teams t JOIN clubs c ON c.id = t.club_id WHERE c.code = 'tcrfc' AND t.code = 'D1'");
        var tag = BizTest.Unique("ifg");
        Guid? playerId = null, staffId = null;
        try
        {
            var player = await BizTest.ReadAsync<AdminPlayerDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/players", BizTest.Multipart(new
            {
                teamId, shirtNo = 87, status = "active", slug = tag, portraitConsentStatus = "consented",
                content = new { zh = new { name = "【測試】照片球員", photoAlt = "球員肖像" }, en = new { name = "Photo Player", photoAlt = "Player portrait" } },
            }, Png)));
            playerId = player.Id;
            Assert.Equal(500, player.PhotoWidth);
            Assert.True(player.PhotoHeight > 0);
            Assert.Equal("Player portrait", player.En!.PhotoAlt);

            var staff = await BizTest.ReadAsync<AdminStaffDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/staff", BizTest.Multipart(new
            {
                portraitConsentStatus = "consented",
                content = new { zh = new { name = "【測試】照片教練" + tag, photoAlt = "教練肖像" } },
            }, Png)));
            staffId = staff.Id;
            Assert.Equal(500, staff.PhotoWidth);
            Assert.Equal("教練肖像", staff.Zh.PhotoAlt);

            var enPlayer = await BizTest.ReadAsync<Tcrfc.Api.Features.Players.PlayerDto>(await anonymous.GetAsync($"/api/v1/tcrfc/players/{tag}?lang=en"));
            Assert.Equal("Player portrait", enPlayer.PhotoAlt);
            Assert.Equal(500, enPlayer.PhotoWidth);
            Assert.True(enPlayer.PhotoHeight > 0);
            var staffList = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Staff.StaffDto>>(await anonymous.GetAsync("/api/v1/tcrfc/staff?lang=zh&pageSize=100"));
            var pubStaff = staffList.Items.Single(s => s.Name == "【測試】照片教練" + tag);
            Assert.Equal("教練肖像", pubStaff.PhotoAlt);
            Assert.Equal(500, pubStaff.PhotoWidth);

            // 撤回肖像同意 → 公開端照片、寬高、Alt 全部為 null（fail-closed）
            await BizTest.ExecuteSqlAsync("UPDATE players SET portrait_consent_status = 'not_consented' WHERE id = @Id", ("@Id", playerId));
            var cached = await admin.PutAsync($"/api/v1/admin/tcrfc/players/{playerId}", BizTest.Multipart(new
            {
                teamId, shirtNo = 87, status = "active", slug = tag, portraitConsentStatus = "not_consented",
                content = new { zh = new { name = "【測試】照片球員", photoAlt = "球員肖像" } },
            }));
            Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
            var closed = await BizTest.ReadAsync<Tcrfc.Api.Features.Players.PlayerDto>(await anonymous.GetAsync($"/api/v1/tcrfc/players/{tag}?lang=zh"));
            Assert.Null(closed.PhotoUrl);
            Assert.Null(closed.PhotoWidth);
            Assert.Null(closed.PhotoHeight);
            Assert.Null(closed.PhotoAlt);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/players/{playerId}", BizTest.Multipart(new
            {
                teamId, shirtNo = 87, content = new { zh = new { name = "x", photoAlt = Long() } },
            }))).StatusCode);
        }
        finally
        {
            if (playerId is Guid p)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM players_i18n WHERE player_id = @Id; DELETE FROM players WHERE id = @Id;", ("@Id", p));
            }

            if (staffId is Guid s)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM staff_teams WHERE staff_id = @Id; DELETE FROM staff_i18n WHERE staff_id = @Id; DELETE FROM staff WHERE id = @Id;", ("@Id", s));
            }
        }
    }

    // ───────────────────────── 課程／店家／抽獎／行事曆 ─────────────────────────

    [AzuriteFact]
    public async Task 課程封面_寬高Alt_公開清單與詳情輸出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var slug = BizTest.Unique("ifgp");
        Guid? id = null;
        try
        {
            var created = await BizTest.ReadAsync<AdminProgramDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/programs", BizTest.Multipart(new
            {
                slug, status = "published", content = new { zh = new { name = "【測試】封面課程", coverAlt = "訓練情景" }, en = new { name = "Cover program", coverAlt = "Training" } },
            }, Png)));
            id = created.Id;
            Assert.Equal(500, created.CoverWidth);
            Assert.Equal("Training", created.En!.CoverAlt);

            var detail = await BizTest.ReadAsync<Tcrfc.Api.Features.Programs.ProgramDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/programs/{slug}?lang=en"));
            Assert.Equal(500, detail.CoverWidth);
            Assert.True(detail.CoverHeight > 0);
            Assert.Equal("Training", detail.CoverAlt);
            var list = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Programs.ProgramListItemDto>>(await anonymous.GetAsync("/api/v1/tcrfc/programs?lang=zh&pageSize=100"));
            Assert.Equal("訓練情景", list.Items.Single(p => p.Slug == slug).CoverAlt);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/programs/{id}", BizTest.Multipart(new
            {
                slug, status = "published", content = new { zh = new { name = "x", coverAlt = Long() } },
            }))).StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM programs_i18n WHERE program_id IN (SELECT id FROM programs WHERE slug = @S); DELETE FROM programs WHERE slug = @S;", ("@S", slug));
        }
    }

    [AzuriteFact]
    public async Task 特約店家圖片_寬高Alt_公開輸出_移除後清空()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? id = null;
        var slug = BizTest.Unique("ifgs");
        try
        {
            var payload = new
            {
                slug, status = "published", content = new { zh = new { name = "【測試】圖片店家", imageAlt = "店門口" }, en = new { name = "Image store", imageAlt = "Storefront" } },
            };
            var created = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/partner-stores", BizTest.Multipart(payload, ("image", TestImages.SmallPng(), "s.png", "image/png"))));
            id = created.Id;
            Assert.Equal(500, created.ImageWidth);
            Assert.True(created.ImageHeight > 0);
            Assert.Equal("Storefront", created.En!.ImageAlt);

            var pubEn = await BizTest.ReadAsync<Tcrfc.Api.Features.MembershipPublic.PartnerStorePublicDto>(await anonymous.GetAsync($"/api/v1/tcrfc/partner-stores/{slug}?lang=en"));
            Assert.Equal("Storefront", pubEn.ImageAlt);
            Assert.Equal(500, pubEn.ImageWidth);
            var pubZh = await BizTest.ReadAsync<Tcrfc.Api.Features.MembershipPublic.PartnerStorePublicDto>(await anonymous.GetAsync($"/api/v1/tcrfc/partner-stores/{slug}?lang=zh"));
            Assert.Equal("店門口", pubZh.ImageAlt);

            var removed = await BizTest.ReadAsync<AdminPartnerStoreDetailDto>(await admin.PutAsync($"/api/v1/admin/tcrfc/partner-stores/{id}", BizTest.Multipart(new
            {
                slug, status = "published", removeImage = true, content = new { zh = new { name = "【測試】圖片店家", imageAlt = "店門口" } },
            })));
            Assert.Null(removed.ImageWidth);
            Assert.Null(removed.ImageHeight);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/partner-stores/{id}", BizTest.Multipart(new
            {
                slug, status = "published", content = new { zh = new { name = "x", imageAlt = Long() } },
            }))).StatusCode);
        }
        finally
        {
            if (id is Guid storeId)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/partner-stores/{storeId}");
            }
        }
    }

    [AzuriteFact]
    public async Task 抽獎封面與行事曆自建活動封面_寬高Alt_後台讀回_公開行事曆輸出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? drawId = null, eventId = null;
        try
        {
            var draw = await BizTest.ReadAsync<AdminDrawDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/draws", BizTest.Multipart(new
            {
                content = new { zh = new { name = "【測試】封面抽獎", rules = "辦法", coverAlt = "抽獎主視覺" }, en = new { name = "Cover draw", coverAlt = "Draw key visual" } },
            }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            drawId = draw.Id;
            Assert.Equal(500, draw.CoverWidth);
            Assert.True(draw.CoverHeight > 0);
            Assert.Equal("Draw key visual", draw.En!.CoverAlt);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/tcrfc/draws", BizTest.Multipart(new
            {
                content = new { zh = new { name = "x", coverAlt = Long() } },
            }))).StatusCode);

            var startsAt = DateTime.UtcNow.AddDays(3);
            var ev = await BizTest.ReadAsync<AdminCalendarCustomEventDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/calendar/custom-events", BizTest.Multipart(new
            {
                startsAt, isPublic = true,
                content = new { zh = new { title = "【測試】封面活動", coverAlt = "活動海報" }, en = new { title = "Cover event", coverAlt = "Event poster" } },
            }, Png)));
            eventId = ev.Id;
            Assert.Equal(500, ev.CoverWidth);
            Assert.Equal("活動海報", ev.Zh.CoverAlt);

            var from = DateOnly.FromDateTime(startsAt.AddDays(-1)).ToString("yyyy-MM-dd");
            var to = DateOnly.FromDateTime(startsAt.AddDays(2)).ToString("yyyy-MM-dd");
            var events = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Calendar.PublicCalendarEventDto>>(
                await anonymous.GetAsync($"/api/v1/tcrfc/calendar/events?lang=en&from={from}&to={to}&pageSize=100"));
            var pub = events.Items.Single(e => e.Id == ev.Id);
            Assert.Equal(500, pub.CoverWidth);
            Assert.True(pub.CoverHeight > 0);
            Assert.Equal("Event poster", pub.CoverAlt);
        }
        finally
        {
            if (eventId is Guid e)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{e}");
            }

            if (drawId is Guid d)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/draws/{d}");
            }
        }
    }

    // ───────────────────────── 漫畫／球迷活動 ─────────────────────────

    [AzuriteFact]
    public async Task 漫畫角色與集數封面與內頁Alt_寬高Alt_公開輸出_內頁PUT更新Alt()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var no = 7000 + Random.Shared.Next(1, 900);
        Guid? characterId = null, episodeId = null;
        try
        {
            var character = await BizTest.ReadAsync<AdminComicCharacterDto>(await admin.PostAsync("/api/v1/admin/tcrfc/comic/characters", BizTest.Multipart(new
            {
                content = new { zh = new { name = "【測試】漫畫角色", imageAlt = "角色立繪" }, en = new { name = "Comic char", imageAlt = "Character art" } },
            }, ("image", TestImages.SmallPng(), "c.png", "image/png"))));
            characterId = character.Id;
            Assert.Equal(500, character.ImageWidth);
            Assert.Equal("Character art", character.En!.ImageAlt);

            var episode = await BizTest.ReadAsync<AdminComicEpisodeDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/comic/episodes", BizTest.Multipart(new
            {
                episodeNo = no, status = "draft", content = new { zh = new { title = "【測試】封面集", coverAlt = "集數封面" } },
            }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            episodeId = episode.Id;
            Assert.Equal(500, episode.CoverWidth);
            Assert.Equal("集數封面", episode.Zh.CoverAlt);

            var pageForm = BizTest.Multipart(new { }, ("files", TestImages.SmallPng(), "p1.png", "image/png"));
            var withPage = await BizTest.ReadAsync<AdminComicEpisodeDetailDto>(await admin.PostAsync($"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}/pages", pageForm));
            var page = Assert.Single(withPage.Pages);
            Assert.Equal(500, page.ImageWidth);
            Assert.Null(page.AltZh);

            var tooLong = await admin.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}/pages/{page.Id}", BizTest.Json(new { altZh = Long() }));
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}/pages/{Guid.NewGuid()}", BizTest.Json(new { altZh = "x" }))).StatusCode);
            var updated = await BizTest.ReadAsync<AdminComicEpisodeDetailDto>(await admin.PutAsync(
                $"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}/pages/{page.Id}", BizTest.Json(new { altZh = "第一頁", altEn = "Page one" })));
            Assert.Equal("第一頁", updated.Pages[0].AltZh);
            Assert.Equal("Page one", updated.Pages[0].AltEn);

            // 發布後公開輸出
            var published = await admin.PutAsync($"/api/v1/admin/tcrfc/comic/episodes/{episode.Id}", BizTest.Multipart(new
            {
                episodeNo = no, status = "published", publishedOn = "2020-01-01", content = new { zh = new { title = "【測試】封面集", coverAlt = "集數封面" }, en = new { title = "Cover episode", coverAlt = "Episode cover" } },
            }));
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);
            var pubEn = await BizTest.ReadAsync<Tcrfc.Api.Features.Comics.ComicEpisodeDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/comic/episodes/{no}?lang=en"));
            Assert.Equal(500, pubEn.CoverWidth);
            Assert.Equal("Episode cover", pubEn.CoverAlt);
            Assert.Equal("Page one", pubEn.Pages.Single().Alt);
            var pubZh = await BizTest.ReadAsync<Tcrfc.Api.Features.Comics.ComicEpisodeDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/comic/episodes/{no}?lang=zh"));
            Assert.Equal("第一頁", pubZh.Pages.Single().Alt);
            var characters = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Comics.ComicCharacterPublicDto>>(await anonymous.GetAsync("/api/v1/tcrfc/comic/characters?lang=en"));
            var pubChar = characters.Single(c => c.Id == character.Id);
            Assert.Equal("Character art", pubChar.ImageAlt);
            Assert.Equal(500, pubChar.ImageWidth);
        }
        finally
        {
            if (episodeId is Guid e)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/comic/episodes/{e}");
            }

            if (characterId is Guid c)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/comic/characters/{c}");
            }
        }
    }

    [AzuriteFact]
    public async Task 球迷活動封面寬高與圖集Alt_PUT更新_公開輸出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var slug = BizTest.Unique("ifgf");
        Guid? id = null;
        try
        {
            var created = await BizTest.ReadAsync<AdminFanEventDetailDto>(await admin.PostAsync("/api/v1/admin/tcrfc/fan-events", BizTest.Multipart(new
            {
                slug, status = "published", startsAt = DateTime.UtcNow.AddDays(10).ToString("o"),
                content = new { zh = new { name = "【測試】圖集活動" } },
            }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            id = created.Id;
            Assert.Equal(500, created.CoverWidth);
            Assert.True(created.CoverHeight > 0);

            await AddGalleryAsync(admin, $"/api/v1/admin/tcrfc/fan-events/{id}/images", "files");
            var detail = await BizTest.ReadAsync<AdminFanEventDetailDto>(await admin.GetAsync($"/api/v1/admin/tcrfc/fan-events/{id}"));
            var image = Assert.Single(detail.Images);
            Assert.Null(image.AltZh);
            var updated = await BizTest.ReadAsync<AdminFanEventDetailDto>(await admin.PutAsync(
                $"/api/v1/admin/tcrfc/fan-events/{id}/images/{image.Id}", BizTest.Json(new { altZh = "活動現場", altEn = "On site" })));
            Assert.Equal("活動現場", updated.Images[0].AltZh);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"/api/v1/admin/tcrfc/fan-events/{id}/images/{image.Id}", BizTest.Json(new { altEn = Long() }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/api/v1/admin/tcrfc/fan-events/{id}/images/{Guid.NewGuid()}", BizTest.Json(new { altZh = "x" }))).StatusCode);

            var pubEn = await BizTest.ReadAsync<Tcrfc.Api.Features.FanEvents.FanEventDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=en"));
            Assert.Equal(500, pubEn.Event.CoverWidth);
            Assert.Equal("On site", pubEn.Images.Single().Alt);
            var pubZh = await BizTest.ReadAsync<Tcrfc.Api.Features.FanEvents.FanEventDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/fan-events/{slug}?lang=zh"));
            Assert.Equal("活動現場", pubZh.Images.Single().Alt);
        }
        finally
        {
            if (id is Guid eventId)
            {
                await admin.DeleteAsync($"/api/v1/admin/tcrfc/fan-events/{eventId}");
            }
        }
    }

    // ───────────────────────── 夥伴／贊助商／商品 ─────────────────────────

    [AzuriteFact]
    public async Task 夥伴與贊助商標誌_深淺各自寬高_Alt共用_公開輸出_贊助活動圖集Alt()
    {
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? partnerId = null, sponsorId = null;
        try
        {
            var partner = await BizTest.ReadAsync<AdminPartnerDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/partners", BizTest.Multipart(new
            {
                partnerType = "策略夥伴", showOnHome = true,
                content = new { zh = new { name = "【測試】標誌夥伴", logoAlt = "夥伴標誌" }, en = new { name = "Logo partner", logoAlt = "Partner logo" } },
            }, ("logoDark", TestImages.SmallPng(), "d.png", "image/png"))));
            partnerId = partner.Id;
            Assert.Equal(500, partner.LogoDarkWidth);
            Assert.True(partner.LogoDarkHeight > 0);
            Assert.Null(partner.LogoLightWidth);
            Assert.Equal("Partner logo", partner.En!.LogoAlt);
            var pubPartners = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Partners.PartnerDto>>(await anonymous.GetAsync("/api/v1/tcrfc/partners?lang=en"));
            var pubPartner = pubPartners.Single(p => p.Id == partner.Id);
            Assert.Equal(500, pubPartner.LogoDarkWidth);
            Assert.Null(pubPartner.LogoLightWidth);
            Assert.Equal("Partner logo", pubPartner.LogoAlt);
            Assert.Equal(HttpStatusCode.BadRequest, (await business.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}", BizTest.Multipart(new
            {
                partnerType = "策略夥伴", content = new { zh = new { name = "x", logoAlt = Long() } },
            }))).StatusCode);
            var cleared = await BizTest.ReadAsync<AdminPartnerDetailDto>(await business.PutAsync($"/api/v1/admin/tcrfc/partners/{partner.Id}", BizTest.Multipart(new
            {
                partnerType = "策略夥伴", removeLogoDark = true, content = new { zh = new { name = "【測試】標誌夥伴", logoAlt = "夥伴標誌" } },
            })));
            Assert.Null(cleared.LogoDarkWidth);
            Assert.Null(cleared.LogoDarkHeight);

            var sponsor = await BizTest.ReadAsync<AdminSponsorDetailDto>(await business.PostAsync("/api/v1/admin/tcrfc/sponsors", BizTest.Multipart(new
            {
                tier = "支持夥伴", content = new { zh = new { name = "【測試】標誌贊助商", logoAlt = "贊助商標誌" } },
            }, ("logoLight", TestImages.SmallPng(), "l.png", "image/png"))));
            sponsorId = sponsor.Id;
            Assert.Equal(500, sponsor.LogoLightWidth);
            Assert.Null(sponsor.LogoDarkWidth);

            var activation = await BizTest.ReadAsync<AdminActivationDto>(await business.PostAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations",
                BizTest.Json(new { content = new { zh = new { title = "【測試】有圖活動" } } })));
            await AddGalleryAsync(business, $"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{activation.Id}/images");
            var withImage = await BizTest.ReadAsync<AdminActivationDto>(await business.GetAsync($"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{activation.Id}"));
            var image = Assert.Single(withImage.Images);
            var updated = await BizTest.ReadAsync<AdminActivationDto>(await business.PutAsync(
                $"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{activation.Id}/images/{image.Id}", BizTest.Json(new { altZh = "活動照", altEn = "Activation photo" })));
            Assert.Equal("Activation photo", updated.Images[0].AltEn);
            Assert.Equal(HttpStatusCode.NotFound, (await business.PutAsync(
                $"/api/v1/admin/tcrfc/sponsors/{sponsor.Id}/activations/{activation.Id}/images/{Guid.NewGuid()}", BizTest.Json(new { altZh = "x" }))).StatusCode);

            var sponsors = await BizTest.ReadAsync<List<Tcrfc.Api.Features.Sponsors.SponsorDto>>(await anonymous.GetAsync("/api/v1/tcrfc/sponsors?lang=en"));
            var pubSponsor = sponsors.Single(s => s.Id == sponsor.Id);
            Assert.Equal(500, pubSponsor.LogoLightWidth);
            Assert.Equal("贊助商標誌", pubSponsor.LogoAlt); // 英文沒填 → 回退繁中
            var pubImage = Assert.Single(Assert.Single(pubSponsor.Activations).Images);
            Assert.Equal("Activation photo", pubImage.Alt);
            Assert.Equal(500, pubImage.ImageWidth);
        }
        finally
        {
            if (partnerId is Guid p)
            {
                await business.DeleteAsync($"/api/v1/admin/tcrfc/partners/{p}");
            }

            if (sponsorId is Guid s)
            {
                await business.DeleteAsync($"/api/v1/admin/tcrfc/sponsors/{s}");
            }
        }
    }

    [AzuriteFact]
    public async Task 商品圖片Alt_PUT更新_公開商品詳情與列表輸出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "alt", ("M", 100, 1));
        try
        {
            var url = $"/api/v1/admin/tcrfc/shop/products/{made.ProductId}/images";
            var uploaded = await BizTest.ReadAsync<AdminProductDetailDto>(await admin.PostAsync(url, BizTest.Multipart(new { }, ("files", TestImages.SmallPng(), "1.png", "image/png"))));
            var image = Assert.Single(uploaded.Images);
            Assert.Equal(500, image.Width);
            Assert.Null(image.AltZh);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync($"{url}/{image.Id}", BizTest.Json(new { altZh = Long() }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"{url}/{Guid.NewGuid()}", BizTest.Json(new { altZh = "x" }))).StatusCode);
            var updated = await BizTest.ReadAsync<AdminProductDetailDto>(await admin.PutAsync($"{url}/{image.Id}", BizTest.Json(new { altZh = "球衣正面", altEn = "Jersey front" })));
            Assert.Equal("球衣正面", updated.Images[0].AltZh);

            var en = await BizTest.ReadAsync<Tcrfc.Api.Features.Shop.ShopProductDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/shop/products/{made.Slug}?lang=en"));
            Assert.Equal("Jersey front", en.Images.Single().Alt);
            Assert.Equal(500, en.Images.Single().Width);
            var zhList = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.Shop.ShopProductListItemDto>>(await anonymous.GetAsync("/api/v1/tcrfc/shop/products?lang=zh&pageSize=100"));
            var item = zhList.Items.Single(p => p.Slug == made.Slug);
            Assert.Equal("球衣正面", item.ImageAlt);
            Assert.Equal(500, item.ImageWidth);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    // ───────────────────────── 公益 ─────────────────────────

    [AzuriteFact]
    public async Task 公益團體標誌_計畫封面_事蹟圖片與圖集_寬高Alt_公開輸出()
    {
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Guid? orgId = null, programId = null, recordId = null;
        try
        {
            var org = await BizTest.ReadAsync<AdminCharityOrgDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/organizations", BizTest.Multipart(new
            {
                content = new { zh = new { name = "【測試】圖片團體", logoAlt = "團體標誌" }, en = new { name = "Image org", logoAlt = "Org logo" } },
            }, ("logo", TestImages.SmallPng(), "l.png", "image/png"))));
            orgId = org.Id;
            Assert.Equal(500, org.LogoWidth);
            Assert.Equal("Org logo", org.En!.LogoAlt);

            var program = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/programs", BizTest.Multipart(new
            {
                charityId = org.Id, status = "published",
                content = new { zh = new { name = "【測試】封面計畫", donationContent = "足球", coverAlt = "計畫封面" }, en = new { name = "Cover program", coverAlt = "Program cover" } },
            }, ("cover", TestImages.SmallPng(), "c.png", "image/png"))));
            programId = program.Id;
            Assert.Equal(500, program.CoverWidth);
            Assert.Equal("計畫封面", program.Zh.CoverAlt);

            await AddGalleryAsync(editor, $"/api/v1/admin/tcrfc/charity/programs/{program.Id}/images");
            var withGallery = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.GetAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}"));
            var gImage = Assert.Single(withGallery.Images);
            Assert.Equal(500, gImage.ImageWidth);
            Assert.True(gImage.ImageHeight > 0);
            var galleryUpdated = await BizTest.ReadAsync<AdminCharityProgramDetailDto>(await editor.PutAsync(
                $"/api/v1/admin/tcrfc/charity/programs/{program.Id}/images/{gImage.Id}", BizTest.Json(new { altZh = "活動照片", altEn = "Event photo" })));
            Assert.Equal("Event photo", galleryUpdated.Images[0].AltEn);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsync($"/api/v1/admin/tcrfc/charity/programs/{program.Id}/images/{gImage.Id}", BizTest.Json(new { altZh = Long() }))).StatusCode);

            var pubProgram = await BizTest.ReadAsync<Tcrfc.Api.Features.CharityImpact.CharityProgramDetailDto>(await anonymous.GetAsync($"/api/v1/tcrfc/charity/programs/{program.Slug}?lang=en"));
            Assert.Equal(500, pubProgram.CoverWidth);
            Assert.Equal("Program cover", pubProgram.CoverAlt);
            Assert.Equal(500, pubProgram.Charity!.LogoWidth);
            Assert.Equal("Org logo", pubProgram.Charity.LogoAlt);
            var pubImage = Assert.Single(pubProgram.Images);
            Assert.Equal("Event photo", pubImage.Alt);
            Assert.Equal(500, pubImage.ImageWidth);
            var list = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.CharityImpact.CharityProgramListItemDto>>(await anonymous.GetAsync("/api/v1/tcrfc/charity/programs?lang=zh&pageSize=100"));
            Assert.Equal("計畫封面", list.Items.Single(p => p.Id == program.Id).CoverAlt);

            var record = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(await editor.PostAsync("/api/v1/admin/tcrfc/charity/records", BizTest.Multipart(new
            {
                charityId = org.Id, happenedOn = "2026-05-01",
                content = new { zh = new { donationContent = "【測試】足球 50 顆", imageAlt = "捐贈現場" }, en = new { donationContent = "50 footballs", imageAlt = "Donation scene" } },
            }, ("image", TestImages.SmallPng(), "a.png", "image/png"))));
            recordId = record.Id;
            Assert.Equal("Donation scene", record.En!.ImageAlt);
            await AddGalleryAsync(editor, $"/api/v1/admin/tcrfc/charity/records/{record.Id}/images");
            var recordWithGallery = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(await editor.GetAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}"));
            var rImage = Assert.Single(recordWithGallery.Images);
            Assert.Equal(500, rImage.ImageWidth);
            var rUpdated = await BizTest.ReadAsync<AdminImpactRecordDetailDto>(await editor.PutAsync(
                $"/api/v1/admin/tcrfc/charity/records/{record.Id}/images/{rImage.Id}", BizTest.Json(new { altZh = "現場二", altEn = "Scene two" })));
            Assert.Equal("現場二", rUpdated.Images[0].AltZh);
            Assert.Equal(HttpStatusCode.NotFound, (await editor.PutAsync($"/api/v1/admin/tcrfc/charity/records/{record.Id}/images/{Guid.NewGuid()}", BizTest.Json(new { altZh = "x" }))).StatusCode);

            var records = await BizTest.ReadAsync<PagedResult<Tcrfc.Api.Features.CharityImpact.ImpactRecordDto>>(await anonymous.GetAsync("/api/v1/tcrfc/charity/records?year=2026&lang=en&pageSize=50"));
            var pub = records.Items.Single(r => r.Id == record.Id);
            Assert.Equal("Donation scene", pub.ImageAlt);
            Assert.Equal(500, pub.CharityLogoWidth);
            Assert.Equal("Org logo", pub.CharityLogoAlt);
            Assert.Equal("Scene two", Assert.Single(pub.Images).Alt);
        }
        finally
        {
            if (recordId is Guid r) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/records/{r}"); }
            if (programId is Guid p) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/programs/{p}"); }
            if (orgId is Guid o) { await editor.DeleteAsync($"/api/v1/admin/tcrfc/charity/organizations/{o}"); }
        }
    }

    // ───────────────────────── 全站預設 OG 圖片 Alt ─────────────────────────

    [AzuriteFact]
    public async Task 全站預設OG圖片Alt_後台設定與清除_公開SEO設定與俱樂部輸出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        var current = await BizTest.ReadAsync<AdminSeoSettingsDto>(await admin.GetAsync("/api/v1/admin/tcrfc/seo/settings"));
        var restore = BizTest.Multipart(new
        {
            titleTemplateZh = current.TitleTemplateZh, titleTemplateEn = current.TitleTemplateEn,
            defaultDescriptionZh = current.DefaultDescriptionZh, defaultDescriptionEn = current.DefaultDescriptionEn,
            robotsCustomRules = current.RobotsCustomRules, ga4MeasurementId = current.Ga4MeasurementId, gtmContainerId = current.GtmContainerId,
            metaPixelId = current.MetaPixelId, lineTagId = current.LineTagId, ogImageAltZh = current.OgImageAltZh, ogImageAltEn = current.OgImageAltEn,
            removeOgImage = current.OgImageUrl is null,
        });
        object Payload(string? altZh, string? altEn) => new
        {
            titleTemplateZh = current.TitleTemplateZh ?? "%s｜台中磐石", defaultDescriptionZh = current.DefaultDescriptionZh ?? "台中磐石足球俱樂部",
            titleTemplateEn = current.TitleTemplateEn, defaultDescriptionEn = current.DefaultDescriptionEn, robotsCustomRules = current.RobotsCustomRules,
            ga4MeasurementId = current.Ga4MeasurementId, gtmContainerId = current.GtmContainerId, metaPixelId = current.MetaPixelId, lineTagId = current.LineTagId,
            ogImageAltZh = altZh, ogImageAltEn = altEn,
        };
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync("/api/v1/admin/tcrfc/seo/settings", BizTest.Multipart(Payload(Long(), null)))).StatusCode);
            var saved = await BizTest.ReadAsync<AdminSeoSettingsDto>(await admin.PutAsync("/api/v1/admin/tcrfc/seo/settings",
                BizTest.Multipart(Payload("社群分享圖", "Social share image"), ("ogImage", TestImages.SmallPng(), "og.png", "image/png"))));
            Assert.Equal(500, saved.OgImageWidth);
            Assert.True(saved.OgImageHeight > 0);
            Assert.Equal("社群分享圖", saved.OgImageAltZh);
            Assert.Equal("Social share image", saved.OgImageAltEn);

            var pubSeo = await BizTest.ReadAsync<PublicSeoSettingsDto>(await anonymous.GetAsync("/api/v1/tcrfc/seo/settings"));
            Assert.Equal("社群分享圖", pubSeo.OgImageAltZh);
            Assert.Equal("Social share image", pubSeo.OgImageAltEn);
            var club = await BizTest.ReadAsync<Tcrfc.Api.Features.Clubs.ClubDto>(await anonymous.GetAsync("/api/v1/clubs/tcrfc?lang=en"));
            Assert.Equal(500, club.OgImageWidth);
            Assert.Equal("Social share image", club.OgImageAlt);

            // 整份取代：沒送 Alt ＝清空
            var cleared = await BizTest.ReadAsync<AdminSeoSettingsDto>(await admin.PutAsync("/api/v1/admin/tcrfc/seo/settings", BizTest.Multipart(Payload(null, null))));
            Assert.Null(cleared.OgImageAltZh);
            Assert.Null(cleared.OgImageAltEn);
            Assert.Equal(500, cleared.OgImageWidth); // 沒換圖，寬高維持
        }
        finally
        {
            await admin.PutAsync("/api/v1/admin/tcrfc/seo/settings", restore);
        }
    }
}
