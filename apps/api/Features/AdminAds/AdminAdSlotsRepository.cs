using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// E4 廣告版位（App 規劃書 §7.2、§8.7）。版位是長期資產：建立後代號不能改（App 端以代號取廣告）、有檔期時不能刪。
/// 兩條法遵規則在這裡直接擋：<b>兒童向畫面（學院、課程：S15／S16／S17）不設版位</b>；<b>不做慈善相關版位</b>（§7.1）。
/// 「版位永不空白」：備援素材（自家內容）由公開投放端點在沒有可投放檔期時回傳。
/// </summary>
public sealed partial class AdminAdSlotsRepository(ClubDbContext dbContext, IImagePublicUrlResolver imageUrls)
{
    /// <summary>兒童向畫面（App 規劃書 §2.2：S15 課程列表、S16 課程報名表、S17 我的報名；§3.12）。</summary>
    internal static readonly HashSet<string> ChildFacingScreens = new(StringComparer.OrdinalIgnoreCase) { "S15", "S16", "S17" };

    [GeneratedRegex(@"^[a-z0-9]+(_[a-z0-9]+)+$")]
    private static partial Regex SlotCodeFormat();

    [GeneratedRegex(@"^\d{1,2}:\d{1,2}$")]
    private static partial Regex RatioFormat();

    public async Task<IReadOnlyList<AdminAdSlotDto>> ListAsync(CancellationToken cancellationToken)
    {
        var slots = await dbContext.AdSlots.AsNoTracking().Include(s => s.AdSlotsI18ns).AsSplitQuery()
            .OrderBy(s => s.ScreenCode).ThenBy(s => s.BlockOrder).ThenBy(s => s.RowSeq).ToListAsync(cancellationToken);
        var counts = await dbContext.AdCampaigns.AsNoTracking().GroupBy(c => c.SlotId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return slots.Select(s => ToDto(s, counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<AdminAdSlotDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var slot = await dbContext.AdSlots.AsNoTracking().Include(s => s.AdSlotsI18ns).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (slot is null)
        {
            return null;
        }

        var count = await dbContext.AdCampaigns.CountAsync(c => c.SlotId == id, cancellationToken);
        return ToDto(slot, count);
    }

    public async Task<AdminAdSlotDto> CreateAsync(
        Guid id, UpsertAdminAdSlotRequest request, UploadedImageInfo? fallbackImage, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request, out var code);
        if (await dbContext.AdSlots.AnyAsync(s => s.SlotCode == code, cancellationToken))
        {
            throw new AdminConflictException("版位代號重複", $"版位代號「{code}」已經有人使用，請換一個。");
        }

        var now = DateTime.UtcNow;
        var slot = new AdSlot { Id = id, SlotCode = code, Surface = "app", CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        Apply(slot, request);
        if (fallbackImage is not null)
        {
            slot.FallbackImageKey = fallbackImage.Key;
            slot.FallbackImageWidth = fallbackImage.Width;
            slot.FallbackImageHeight = fallbackImage.Height;
        }

        SetI18n(slot, request.Content);
        dbContext.AdSlots.Add(slot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(id, cancellationToken))!;
    }

    public async Task<AdminAdSlotDto?> UpdateAsync(
        Guid id, UpsertAdminAdSlotRequest request, ImageFieldUpdate fallbackImage, OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request, out var code);
        var slot = await dbContext.AdSlots.Include(s => s.AdSlotsI18ns).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (slot is null)
        {
            return null;
        }

        if (!string.Equals(slot.SlotCode, code, StringComparison.Ordinal))
        {
            throw new AdminValidationException("版位代號建立後不能修改（App 端是用代號取得廣告的）。");
        }

        Apply(slot, request);
        if (fallbackImage.Change)
        {
            orphans.Image(slot.FallbackImageKey);
            slot.FallbackImageKey = fallbackImage.Key;
            slot.FallbackImageWidth = fallbackImage.Width;
            slot.FallbackImageHeight = fallbackImage.Height;
        }

        slot.UpdatedAt = DateTime.UtcNow;
        slot.UpdatedBy = operatorId;
        SetI18n(slot, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var slot = await dbContext.AdSlots.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (slot is null)
        {
            return false;
        }

        if (await dbContext.AdCampaigns.AnyAsync(c => c.SlotId == id, cancellationToken))
        {
            throw new AdminConflictException("版位已有檔期", "這個版位已經有投放檔期，不能刪除；請改成「停用」。");
        }

        orphans.Image(slot.FallbackImageKey);
        dbContext.AdSlots.Remove(slot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Validate(UpsertAdminAdSlotRequest request, out string code)
    {
        var raw = AdminInput.RequireText(request.SlotCode, "版位代號", 64);
        if (!SlotCodeFormat().IsMatch(raw))
        {
            throw new AdminValidationException("版位代號的格式是「畫面_位置」，只能用小寫英文字母、數字與底線，例如 home_top。");
        }

        if (raw.Contains("charity", StringComparison.Ordinal) || raw.Contains("donation", StringComparison.Ordinal))
        {
            throw new AdminValidationException("不設慈善相關的廣告版位：慈善捐款的主辦與收款主體是協會，俱樂部的 App 不販售慈善版位。");
        }

        code = raw;
        var screen = AdminInput.OptionalText(request.ScreenCode, "畫面代碼", 16);
        if (screen is not null && ChildFacingScreens.Contains(screen))
        {
            throw new AdminValidationException("學院與課程相關畫面是兒童向畫面，不設廣告版位。");
        }

        if (request.AspectRatio is { } ratio && !RatioFormat().IsMatch(ratio))
        {
            throw new AdminValidationException("長寬比的格式是「寬:高」，例如 16:9。");
        }

        AdminInput.OptionalNonNegative(request.MinWidth, "最小寬度");
        AdminInput.OptionalNonNegative(request.MinHeight, "最小高度");
        AdminInput.OptionalNonNegative(request.MaxFileKb, "檔案大小上限");
        AdminInput.OptionalNonNegative(request.SessionImpressionCap, "單次使用的曝光上限");
        if (request.RotationCap is < 1 or > 10)
        {
            throw new AdminValidationException("輪播張數上限只能是 1 到 10。");
        }

        AdminInput.OptionalHttpUrl(request.FallbackLink, "備援連結");
        AdminInput.RequireText(request.Content.Zh.Name, "版位名稱（繁中）", 128);
        AdminInput.OptionalText(request.Content.En?.Name, "版位名稱（英文）", 128);
    }

    private static void Apply(AdSlot slot, UpsertAdminAdSlotRequest r)
    {
        slot.ScreenCode = AdminInput.OptionalText(r.ScreenCode, "畫面代碼", 16)?.ToUpperInvariant();
        slot.BlockOrder = r.BlockOrder;
        slot.AspectRatio = AdminInput.OptionalText(r.AspectRatio, "長寬比", 16);
        slot.MinWidth = r.MinWidth;
        slot.MinHeight = r.MinHeight;
        slot.MaxFileKb = r.MaxFileKb;
        slot.AllowedFormats = AdminInput.OptionalText(r.AllowedFormats, "允許格式", 64);
        slot.AllowVideo = r.AllowVideo;
        slot.SessionImpressionCap = r.SessionImpressionCap;
        slot.RotationCap = r.RotationCap ?? 1;
        slot.FallbackLink = AdminInput.OptionalText(r.FallbackLink, "備援連結", 500);
        slot.IsActive = r.IsActive;
    }

    private static void SetI18n(AdSlot slot, AdSlotContentInput content)
    {
        Upsert(slot, RequestLocale.DefaultDbLocale, content.Zh);
        if (content.En is not null)
        {
            Upsert(slot, "en", content.En);
        }
    }

    private static void Upsert(AdSlot slot, string locale, AdSlotLocaleContent c)
    {
        var row = slot.AdSlotsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new AdSlotsI18n { AdSlotId = slot.Id, Locale = locale };
            slot.AdSlotsI18ns.Add(row);
        }

        row.Name = AdminInput.OptionalText(c.Name, "版位名稱", 128);
        row.FallbackAlt = AdminInput.OptionalText(c.FallbackAlt, "備援素材說明文字", 200);
    }

    private AdminAdSlotDto ToDto(AdSlot s, int campaignCount)
    {
        string? Name(string locale) => s.AdSlotsI18ns.FirstOrDefault(i => i.Locale == locale)?.Name;
        string? Alt(string locale) => s.AdSlotsI18ns.FirstOrDefault(i => i.Locale == locale)?.FallbackAlt;
        return new AdminAdSlotDto
        {
            Id = s.Id, SlotCode = s.SlotCode, Surface = s.Surface, ScreenCode = s.ScreenCode, BlockOrder = s.BlockOrder,
            AspectRatio = s.AspectRatio, MinWidth = s.MinWidth, MinHeight = s.MinHeight, MaxFileKb = s.MaxFileKb,
            AllowedFormats = s.AllowedFormats, AllowVideo = s.AllowVideo, SessionImpressionCap = s.SessionImpressionCap,
            RotationCap = s.RotationCap, FallbackImageKey = s.FallbackImageKey, FallbackImageUrl = imageUrls.Resolve(s.FallbackImageKey),
            FallbackImageThumbUrl = s.FallbackImageKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(s.FallbackImageKey)),
            FallbackLink = s.FallbackLink, IsActive = s.IsActive, NameZh = Name(RequestLocale.DefaultDbLocale), NameEn = Name("en"),
            FallbackAltZh = Alt(RequestLocale.DefaultDbLocale), FallbackAltEn = Alt("en"), CampaignCount = campaignCount, UpdatedAt = s.UpdatedAt,
        };
    }
}
