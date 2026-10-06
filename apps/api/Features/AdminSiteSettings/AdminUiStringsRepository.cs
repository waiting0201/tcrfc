using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>
/// I4 字串翻譯表（規劃書 §4.9「按鈕、表單標籤、提示訊息、錯誤訊息等介面文案的雙語對照維護」）。資料在 <c>ui_strings</c>＋<c>ui_string_translations</c>，
/// 全站共用（沒有 <c>club_id</c>，與內容 i18n 側表是兩套形狀相同的機制）。
///
/// 🔴 <b>翻譯人員限制在伺服器端強制</b>（規劃書 §6 補充規則 ※：翻譯人員僅能編輯 <c>en</c> 語系欄位，不得修改繁中原文）：
/// 持有 <c>site.string.update</c> 才能新增／刪除字串、改分組、改預設語系（繁中）文字；只有 <c>site.string.translate</c> 的人只能新增／修改／清除
/// 「非預設語系」的翻譯，請求裡只要有任何改動繁中文字或分組的內容就整個請求 403（不做部分套用）。
/// </summary>
public sealed partial class AdminUiStringsRepository(ClubDbContext db, IPermissionChecker permissions, IQueryCache cache)
{
    public const string PermissionUpdate = "site.string.update";
    public const string PermissionTranslate = "site.string.translate";
    private const int MaxValueLength = 2000;

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]*(\.[a-z0-9][a-z0-9_-]*)*$")]
    private static partial Regex KeyFormat();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex GroupFormat();

    public async Task<PagedResult<AdminUiStringDto>> ListAsync(AdminUiStringListQuery query, CancellationToken cancellationToken)
    {
        var (page, pageSize) = PagingQuery.Normalize(query.Page, query.PageSize, defaultPageSize: 50, maxPageSize: 100);
        var q = db.UiStrings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Group))
        {
            var group = query.Group.Trim();
            q = q.Where(s => s.StringGroup == group);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var k = query.Keyword.Trim();
            q = q.Where(s => s.StringKey.Contains(k) || s.UiStringTranslations.Any(t => t.Value.Contains(k)));
        }

        if (!string.IsNullOrWhiteSpace(query.Missing))
        {
            var locale = query.Missing.Trim();
            q = q.Where(s => !s.UiStringTranslations.Any(t => t.Locale == locale && t.Value != ""));
        }

        var total = await q.CountAsync(cancellationToken);
        var rows = await q.OrderBy(s => s.StringGroup).ThenBy(s => s.StringKey)
            .Skip((page - 1) * pageSize).Take(pageSize).Include(s => s.UiStringTranslations).ToListAsync(cancellationToken);
        return new PagedResult<AdminUiStringDto> { Items = rows.Select(ToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<IReadOnlyList<string>> ListGroupsAsync(CancellationToken cancellationToken)
        => await db.UiStrings.AsNoTracking().Where(s => s.StringGroup != null).Select(s => s.StringGroup!).Distinct().OrderBy(g => g).ToListAsync(cancellationToken);

    public async Task<AdminUiStringDto> CreateAsync(
        AdminClubScope scope, CreateAdminUiStringRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        await RequireFullAsync(scope, cancellationToken);
        var key = request.Key?.Trim() ?? string.Empty;
        if (key.Length is 0 or > 128 || !KeyFormat().IsMatch(key))
        {
            throw new AdminValidationException("字串鍵只能用小寫英文、數字、底線、連字號與句點（例如 form.submit），最長 128 字。", "key");
        }

        var group = ValidateGroup(request.Group);
        var values = await ValidateValuesAsync(request.Values, requireDefault: true, cancellationToken);
        if (await db.UiStrings.AnyAsync(s => s.StringKey == key, cancellationToken))
        {
            throw new AdminConflictException("字串鍵重複", "這個字串鍵已經存在，請直接編輯既有的字串。", "key");
        }

        var now = DateTime.UtcNow;
        var row = new UiString
        {
            Id = Guid.NewGuid(), StringKey = key, StringGroup = group, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.UiStrings.Add(row);
        foreach (var (locale, text) in values.Where(v => !string.IsNullOrEmpty(v.Value)))
        {
            var t = new UiStringTranslation { UiStringId = row.Id, Locale = locale, Value = text! };
            row.UiStringTranslations.Add(t);
            db.UiStringTranslations.Add(t);
        }

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<AdminUiStringDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminUiStringRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var held = await permissions.GetHeldPermissionCodesAsync(
            scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, [PermissionUpdate, PermissionTranslate], cancellationToken);
        var canFull = held.Contains(PermissionUpdate);

        var row = await db.UiStrings.Include(s => s.UiStringTranslations).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var values = await ValidateValuesAsync(request.Values, requireDefault: false, cancellationToken);
        var defaultLocale = RequestLocale.DefaultDbLocale;

        if (!canFull)
        {
            // 翻譯人員：不得改分組、不得改繁中原文（內容相同的重送視為沒改）。
            var currentGroup = row.StringGroup;
            var requestedGroup = request.Group is null ? currentGroup : ValidateGroup(request.Group);
            var currentDefault = row.UiStringTranslations.FirstOrDefault(t => t.Locale == defaultLocale)?.Value;
            var touchesDefault = values.TryGetValue(defaultLocale, out var newDefault) && !string.Equals(newDefault ?? string.Empty, currentDefault ?? string.Empty, StringComparison.Ordinal);
            if (requestedGroup != currentGroup || touchesDefault)
            {
                throw new AdminForbiddenException("翻譯人員只能編輯非繁中語系的翻譯，不能修改繁中原文或分組。");
            }
        }
        else if (request.Group is not null)
        {
            row.StringGroup = ValidateGroup(request.Group);
        }

        foreach (var (locale, text) in values)
        {
            if (locale == defaultLocale && !canFull)
            {
                continue; // 上面已確認內容相同
            }

            var existing = row.UiStringTranslations.FirstOrDefault(t => t.Locale == locale);
            if (string.IsNullOrEmpty(text))
            {
                if (locale == defaultLocale)
                {
                    throw new AdminValidationException("繁中原文不能清空。", "defaultValue");
                }

                if (existing is not null)
                {
                    db.UiStringTranslations.Remove(existing);
                    row.UiStringTranslations.Remove(existing);
                }

                continue;
            }

            if (existing is null)
            {
                existing = new UiStringTranslation { UiStringId = row.Id, Locale = locale, Value = text };
                row.UiStringTranslations.Add(existing);
                db.UiStringTranslations.Add(existing);
            }
            else
            {
                existing.Value = text;
            }
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = operatorId;
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        await RequireFullAsync(scope, cancellationToken);
        var row = await db.UiStrings.Include(s => s.UiStringTranslations).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.UiStringTranslations.RemoveRange(row.UiStringTranslations.ToList());
        db.UiStrings.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(cancellationToken);
        return true;
    }

    private async Task RequireFullAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        if (!await permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, PermissionUpdate, cancellationToken))
        {
            throw new AdminForbiddenException("翻譯人員不能新增或刪除字串，只能編輯非繁中語系的翻譯。");
        }
    }

    private Task InvalidateAsync(CancellationToken cancellationToken)
        => cache.InvalidateAsync(SiteSettingsRepository.CacheEntities.UiStrings, CacheDimensions.SharedClub, cancellationToken);

    private static string? ValidateGroup(string? group)
    {
        var text = group?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return GroupFormat().IsMatch(text) ? text : throw new AdminValidationException("分組只能用小寫英文、數字、底線與連字號，最長 64 字。", "group");
    }

    /// <summary>檢查語系代碼存在、文字長度；回傳「語系 → 文字（可為空字串，代表清除）」。</summary>
    private async Task<Dictionary<string, string?>> ValidateValuesAsync(
        IReadOnlyDictionary<string, string?>? values, bool requireDefault, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (values is not null)
        {
            var known = await db.Locales.AsNoTracking().Select(l => l.Code).ToListAsync(cancellationToken);
            foreach (var (locale, text) in values)
            {
                if (!known.Contains(locale))
                {
                    throw new AdminValidationException("語系不正確。", "values");
                }

                var trimmed = text?.Trim();
                if (trimmed is { Length: > MaxValueLength })
                {
                    throw new AdminValidationException($"每則文字不可超過 {MaxValueLength} 個字。", "values");
                }

                result[locale] = trimmed;
            }
        }

        if (requireDefault && string.IsNullOrEmpty(result.GetValueOrDefault(RequestLocale.DefaultDbLocale)))
        {
            throw new AdminValidationException("繁中原文為必填欄位。", "defaultValue");
        }

        return result;
    }

    private static AdminUiStringDto ToDto(UiString s) => new()
    {
        Id = s.Id,
        Key = s.StringKey,
        Group = s.StringGroup,
        Values = s.UiStringTranslations.ToDictionary(t => t.Locale, t => t.Value, StringComparer.Ordinal),
        UpdatedAt = s.UpdatedAt,
    };
}
