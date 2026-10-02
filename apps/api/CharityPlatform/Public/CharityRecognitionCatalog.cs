using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Storage;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Public;

/// <summary>
/// CH-5 公開唯讀：捐款徵信名單與成果回顧頁。同 <see cref="CharityPublicCatalog"/>：不經過任何快取（慈善平台完全不接 Redis）、全部 <c>AsNoTracking</c> 投影。
/// </summary>
public sealed class CharityRecognitionCatalog(CharityDbContext db, ICharityImageStorage imageUrls)
{
    public const string ClubSiteUrlKey = "donation.club_site_url";
    private const int MaxPageSize = 200;

    /// <summary>
    /// 徵信名單（規劃書 §3.6、§11.1）。🔴 公開條件缺一不可：<c>paid</c>、捐款人明示具名（<c>is_anonymous = 0</c>）、後台沒有逐筆隱藏、
    /// 站台沒有整站關閉（<c>donation.credit_list_display_rule ≠ off</c>）。只輸出姓名：同名去重（不洩漏捐款次數）、依姓名排序（不洩漏捐款時間先後）。
    /// </summary>
    public async Task<PublicCreditListDto> GetCreditListAsync(
        string? projectSlug, DateOnly? from, DateOnly? to, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 100, maxPageSize: MaxPageSize);

        var rule = await db.Settings.AsNoTracking().Where(s => s.SettingKey == CharityPublicCatalog.CreditListRuleKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        if (string.Equals(rule, "off", StringComparison.Ordinal))
        {
            return new PublicCreditListDto { Enabled = false, Names = [], Page = p, PageSize = size, TotalCount = 0 };
        }

        if (from is { } f && to is { } t && f > t)
        {
            throw new AdminValidationException("期間的開始日不可晚於結束日。");
        }

        var query = db.Donations.AsNoTracking().Where(d => d.Status == DonationStatus.Paid && !d.IsAnonymous && !d.IsCreditHidden);

        if (!string.IsNullOrWhiteSpace(projectSlug))
        {
            var slug = projectSlug.Trim();
            query = query.Where(d => d.DonationProject.ProjectSlug == slug);
        }

        if (from is { } fromDate)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(fromDate);
            query = query.Where(d => d.PaidAt >= fromUtc);
        }

        if (to is { } toDate)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(d => d.PaidAt < toUtc);
        }

        var names = query.Select(d => d.DonorName.Trim()).Where(n => n != string.Empty).Distinct();
        var total = await names.CountAsync(cancellationToken);
        var page1 = await names.OrderBy(n => n).Skip((p - 1) * size).Take(size).ToListAsync(cancellationToken);

        return new PublicCreditListDto { Enabled = true, Names = page1, Page = p, PageSize = size, TotalCount = total };
    }

    /// <summary>
    /// 成果回顧：已上架項目依「關聯的慈善計畫」分組（沒有指定計畫、只指定公益團體的另成一組）。名稱取自項目建立當下的快照，
    /// 不即時查主站（規劃書 §9.3 第 2 條：不得即時 join、本平台不得持有主站資料庫連線）。
    /// </summary>
    public async Task<PublicImpactDto> GetImpactAsync(string dbLocale, CancellationToken cancellationToken)
    {
        var rows = await db.DonationProjects.AsNoTracking()
            .Where(p => p.Status == "published" && (p.CharityProgramRefCode != null || p.CharityRefCode != null))
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Seq)
            .Select(p => new
            {
                p.ProjectSlug, p.CoverKey, p.CharityRefCode, p.CharityProgramRefCode, p.CharityNameSnapshot, p.CharityProgramNameSnapshot,
                Requested = p.DonationProjectsI18ns.Where(i => i.Locale == dbLocale).Select(i => new { i.Name, i.OneLiner, i.CoverAlt }).FirstOrDefault(),
                Default = p.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => new { i.Name, i.OneLiner, i.CoverAlt }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var fallback = false;
        var groups = rows
            .Select(r => new { Row = r, Name = RequestLocale.Pick(r.Requested?.Name, r.Default?.Name) })
            .Where(x => x.Name is not null)
            .GroupBy(x => x.Row.CharityProgramRefCode ?? $"charity:{x.Row.CharityRefCode}")
            .Select(g =>
            {
                var first = g.First().Row;
                return new PublicImpactProgramDto
                {
                    ProgramRefCode = first.CharityProgramRefCode,
                    ProgramName = first.CharityProgramNameSnapshot,
                    CharityName = first.CharityNameSnapshot,
                    Projects = g.Select(x =>
                    {
                        if (dbLocale != RequestLocale.DefaultDbLocale && string.IsNullOrWhiteSpace(x.Row.Requested?.Name))
                        {
                            fallback = true;
                        }

                        return new PublicImpactProjectDto
                        {
                            Slug = x.Row.ProjectSlug,
                            Name = x.Name!,
                            OneLiner = RequestLocale.Pick(x.Row.Requested?.OneLiner, x.Row.Default?.OneLiner),
                            CoverUrl = imageUrls.Resolve(x.Row.CoverKey),
                            CoverAlt = RequestLocale.Pick(x.Row.Requested?.CoverAlt, x.Row.Default?.CoverAlt),
                        };
                    }).ToList(),
                };
            })
            .ToList();

        var clubUrl = await db.Settings.AsNoTracking().Where(s => s.SettingKey == ClubSiteUrlKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        return new PublicImpactDto
        {
            ClubSiteUrl = string.IsNullOrWhiteSpace(clubUrl) ? null : clubUrl,
            Programs = groups,
            IsFallback = fallback,
        };
    }
}
