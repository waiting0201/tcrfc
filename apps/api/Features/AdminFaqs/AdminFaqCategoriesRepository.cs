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
/// 🔴 **「停用分類」的實作是刪除，不是新增一個 <c>is_enabled</c> 欄位**：<c>faq_categories</c>
/// 沒有任何啟用／停用狀態欄位（<c>docs/12b-database-tables.md</c>／<c>db/club-schema.sql</c>
/// 都沒有），依任務指示「需要新欄位就停下回報，不自己加」，本輪判斷「刪除分類」可以達成規劃書
/// 「停用」字面上要的效果（分類從導覽清單消失），代價是這個動作不可逆（沒有「重新啟用」這回事，
/// 要恢復只能重新建立一個同樣內容的分類，且原本掛在這個分類底下的常見問題不會被連坐刪除，
/// 只是 <c>faq_category_links</c> 的關聯列被 <c>ON DELETE CASCADE</c> 移除，問題本身仍然存在，
/// 只是失去這個分類標籤——若某一題因此變成零分類，該題仍然可由關鍵字搜尋找到，不會憑空消失）。
/// 這是需要業務確認的判斷，見 apps/api/README.md「我的判斷」。
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
            Zh = new AdminFaqCategoryLocaleContent { Name = zh?.Name ?? category.Slug },
            En = en is null ? null : new AdminFaqCategoryLocaleContent { Name = en.Name },
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
        };
    }
}
