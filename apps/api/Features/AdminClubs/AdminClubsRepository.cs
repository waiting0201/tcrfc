using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminClubs;

/// <summary>J4「俱樂部品牌與法人資料」——<c>Club</c> 型別的後台維護端點。全域（不分「目前站在哪個
/// 俱樂部」，管的正是「有哪些俱樂部」這件事本身），比照 <c>ClubsRepository</c>（公開唯讀端點）的
/// i18n 讀寫方式，但寫入走 EF Core（跟 <c>AdminArticlesRepository</c> 同一個既有慣例）。</summary>
public sealed class AdminClubsRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminClubListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Clubs.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Domain,
                c.DefaultLocale,
                c.IsCollectingSubject,
                c.SortOrder,
                c.Status,
                NameZh = c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = c.ClubsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminClubListItemDto
        {
            Id = r.Id,
            Code = r.Code,
            Domain = r.Domain,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            DefaultLocale = r.DefaultLocale,
            IsCollectingSubject = r.IsCollectingSubject,
            SortOrder = r.SortOrder,
            Status = r.Status,
        }).ToList();
    }

    public async Task<AdminClubDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var club = await dbContext.Clubs.AsNoTracking()
            .Include(c => c.ClubsI18ns)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return club is null ? null : ToDetailDto(club);
    }

    public async Task<AdminClubDetailDto> CreateAsync(CreateAdminClubRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateDomain(request.Domain);
        ValidateContent(request.Content);

        if (await dbContext.Clubs.AsNoTracking().AnyAsync(c => c.Code == request.Code, cancellationToken))
        {
            throw new AdminClubCodeConflictException(request.Code);
        }
        if (await dbContext.Clubs.AsNoTracking().AnyAsync(c => c.Domain == request.Domain, cancellationToken))
        {
            throw new AdminClubDomainConflictException(request.Domain);
        }

        var now = DateTime.UtcNow;
        var club = new Club
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Domain = request.Domain,
            BrandColor = request.BrandColor,
            BrandSecondaryColor = request.BrandSecondaryColor,
            InvoiceTitle = request.InvoiceTitle,
            TaxId = request.TaxId,
            IsCollectingSubject = request.IsCollectingSubject,
            DefaultLocale = request.DefaultLocale,
            SortOrder = request.SortOrder,
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Clubs.Add(club);
        AddOrReplaceI18n(club, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(club, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(club.Id, cancellationToken))!;
    }

    public async Task<AdminClubDetailDto?> UpdateAsync(Guid id, UpdateAdminClubRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateDomain(request.Domain);
        ValidateContent(request.Content);

        var club = await dbContext.Clubs
            .Include(c => c.ClubsI18ns)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (club is null)
        {
            return null;
        }

        if (!string.Equals(club.Domain, request.Domain, StringComparison.Ordinal)
            && await dbContext.Clubs.AsNoTracking().AnyAsync(c => c.Domain == request.Domain && c.Id != id, cancellationToken))
        {
            throw new AdminClubDomainConflictException(request.Domain);
        }

        club.Domain = request.Domain;
        club.BrandColor = request.BrandColor;
        club.BrandSecondaryColor = request.BrandSecondaryColor;
        club.InvoiceTitle = request.InvoiceTitle;
        club.TaxId = request.TaxId;
        club.IsCollectingSubject = request.IsCollectingSubject;
        club.DefaultLocale = request.DefaultLocale;
        club.SortOrder = request.SortOrder;
        club.Status = request.Status;
        club.UpdatedAt = DateTime.UtcNow;
        club.UpdatedBy = operatorId;

        AddOrReplaceI18n(club, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = club.ClubsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(club, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private void AddOrReplaceI18n(Club club, string locale, AdminClubLocaleContent content)
    {
        var existing = club.ClubsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new ClubsI18n { ClubId = club.Id, Locale = locale };
            club.ClubsI18ns.Add(existing);
            dbContext.ClubsI18ns.Add(existing);
        }

        existing.Name = content.Name;
        existing.Description = content.Description;
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AdminClubValidationException("俱樂部代碼為必填欄位。");
        }
        if (code.Length > 16)
        {
            throw new AdminClubValidationException("俱樂部代碼長度不能超過 16 個字元。");
        }
        if (!code.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)))
        {
            throw new AdminClubValidationException("俱樂部代碼只能使用小寫英文字母與數字組成。");
        }
    }

    private static void ValidateDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new AdminClubValidationException("網域為必填欄位。");
        }
        if (domain.Length > 128)
        {
            throw new AdminClubValidationException("網域長度不能超過 128 個字元。");
        }
    }

    private static void ValidateContent(AdminClubContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminClubValidationException("中文名稱為必填欄位。");
        }
    }

    private static AdminClubDetailDto ToDetailDto(Club club)
    {
        var zh = club.ClubsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = club.ClubsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminClubDetailDto
        {
            Id = club.Id,
            Code = club.Code,
            Domain = club.Domain,
            LogoLightKey = club.LogoLightKey,
            LogoDarkKey = club.LogoDarkKey,
            FaviconKey = club.FaviconKey,
            OgImageKey = club.OgImageKey,
            BrandColor = club.BrandColor,
            BrandSecondaryColor = club.BrandSecondaryColor,
            InvoiceTitle = club.InvoiceTitle,
            TaxId = club.TaxId,
            IsCollectingSubject = club.IsCollectingSubject,
            DefaultLocale = club.DefaultLocale,
            SortOrder = club.SortOrder,
            Status = club.Status,
            Zh = new AdminClubLocaleContent { Name = zh?.Name ?? club.Code, Description = zh?.Description },
            En = en is null ? null : new AdminClubLocaleContent { Name = en.Name, Description = en.Description },
            CreatedAt = club.CreatedAt,
            UpdatedAt = club.UpdatedAt,
        };
    }
}
