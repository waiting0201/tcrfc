using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// B4「主題分類管理」（規劃書 §4.2 B4「新增／排序／停用分類」）。<c>faq_categories</c> 沒有
/// <c>club_id</c>（同 <c>article_categories</c> 的共用主檔設計，見 db/club-schema.sql 註解
/// 「刻意不帶 club_id」），因此不是俱樂部範圍操作，收 <see cref="AdminSystemScope"/> 而不是
/// <see cref="AdminClubScope"/>（比照 <c>Features/AdminRoles</c>／<c>Features/AdminClubs</c>
/// 的 J 模組寫法）。
///
/// ✅ **S1-7a：「停用分類」已改為 <c>IsEnabled</c> 軟停用**（原本用 DELETE 湊停用，見
/// db/club-schema.sql 該表註解與 apps/api/README.md S1-6 段的既有記錄）——停用後分類從公開
/// 導覽消失（<c>Features/Faqs/FaqsRepository.ListCategoriesAsync</c> 只回 <c>is_enabled=1</c>
/// 的分類），但既有題目與 <c>faq_category_links</c> 關聯不受影響，可隨時改回啟用。
/// <see cref="DeleteAsync"/> 仍然保留，但現在是**真正的刪除**（不可逆，經
/// <c>ON DELETE CASCADE</c> 解除關聯），不再是「停用」的替代做法——需要停用一律用
/// <see cref="UpdateAsync"/> 把 <c>IsEnabled</c> 設為 <c>false</c>，需要真的移除這個分類（不留存）
/// 才用 DELETE。
/// </summary>
public sealed class AdminFaqCategoriesRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminFaqCategoryListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.FaqCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Slug)
            .Select(c => new
            {
                c.Id,
                c.Slug,
                c.SortOrder,
                c.IsEnabled,
                c.UpdatedAt,
                NameZh = c.FaqCategoriesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = c.FaqCategoriesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                FaqCount = c.Faqs.Count,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminFaqCategoryListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            SortOrder = r.SortOrder,
            IsEnabled = r.IsEnabled,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            FaqCount = r.FaqCount,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminFaqCategoryDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await dbContext.FaqCategories.AsNoTracking()
            .Include(c => c.FaqCategoriesI18ns)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return category is null ? null : ToDetailDto(category);
    }

    public async Task<AdminFaqCategoryDetailDto> CreateAsync(
        CreateAdminFaqCategoryRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        FaqSlugPolicy.Validate(request.Slug, "分類網址名稱");
        ValidateContent(request.Content);

        if (await dbContext.FaqCategories.AsNoTracking().AnyAsync(c => c.Slug == request.Slug, cancellationToken))
        {
            throw new FaqCategorySlugConflictException(request.Slug);
        }

        var now = DateTime.UtcNow;
        var category = new FaqCategory
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug,
            SortOrder = request.SortOrder,
            IsEnabled = request.IsEnabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.FaqCategories.Add(category);
        AddOrReplaceI18n(category, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(category, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(category.Id, cancellationToken))!;
    }

    public async Task<AdminFaqCategoryDetailDto?> UpdateAsync(
        Guid id, UpdateAdminFaqCategoryRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        FaqSlugPolicy.Validate(request.Slug, "分類網址名稱");
        ValidateContent(request.Content);

        var category = await dbContext.FaqCategories
            .Include(c => c.FaqCategoriesI18ns)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        if (!string.Equals(category.Slug, request.Slug, StringComparison.Ordinal)
            && await dbContext.FaqCategories.AsNoTracking().AnyAsync(c => c.Slug == request.Slug && c.Id != id, cancellationToken))
        {
            throw new FaqCategorySlugConflictException(request.Slug);
        }

        category.Slug = request.Slug;
        category.SortOrder = request.SortOrder;
        category.IsEnabled = request.IsEnabled;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = operatorId;

        AddOrReplaceI18n(category, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = category.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(category, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>刪除＝規劃書「停用分類」的實作方式，見本類別檔頭說明。回傳 <c>false</c>＝找不到這個分類。</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await dbContext.FaqCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return false;
        }

        // faq_category_links 的兩個外鍵皆為 ON DELETE CASCADE（db/club-schema.sql），
        // 刪除分類本身即可連帶清掉關聯列，不需要另外手動清子表。
        dbContext.FaqCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void AddOrReplaceI18n(FaqCategory category, string locale, AdminFaqCategoryLocaleContent content)
    {
        var existing = category.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new FaqCategoriesI18n { FaqCategoryId = category.Id, Locale = locale };
            category.FaqCategoriesI18ns.Add(existing);
            dbContext.FaqCategoriesI18ns.Add(existing);
        }

        existing.Name = content.Name;
    }

    private static void ValidateContent(AdminFaqCategoryContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminFaqValidationException("分類中文名稱為必填欄位。");
        }
    }

    private static AdminFaqCategoryDetailDto ToDetailDto(FaqCategory category)
    {
        var zh = category.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = category.FaqCategoriesI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminFaqCategoryDetailDto
        {
            Id = category.Id,
            Slug = category.Slug,
            SortOrder = category.SortOrder,
            IsEnabled = category.IsEnabled,
            Zh = new AdminFaqCategoryLocaleContent { Name = zh?.Name ?? category.Slug },
            En = en is null ? null : new AdminFaqCategoryLocaleContent { Name = en.Name },
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
        };
    }
}
