using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// E5 廣告素材（App 規劃書 §8.8）：依語系分別上傳（<c>zh</c>／<c>en</c>）、圖或影片（影片仍須附海報圖）、A/B 標記、alt 文字、點擊目的地，
/// 以及審核與緊急暫停。素材是否符合版位規格（長寬比、最小像素、檔案大小、是否允許影片）在上傳時就檢查。
/// 🔴 <b>素材內容被修改（含換圖）後一律回到「待審」</b>——否則核可過的素材可以被偷換成沒審過的內容而直接上線（規劃書 §7.9 第 3 點）。
/// </summary>
public sealed class AdminAdCreativesRepository(ClubDbContext dbContext, AdCreativeMapper mapper, SensitiveActionLogger audit)
{
    private static readonly IReadOnlySet<string> Locales = new HashSet<string>(["zh", "en"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> Variants = new HashSet<string>(["A", "B"], StringComparer.Ordinal);

    public async Task<IReadOnlyList<AdminAdCreativeDto>?> ListAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!await dbContext.AdCampaigns.AnyAsync(c => c.Id == campaignId, cancellationToken))
        {
            return null;
        }

        var rows = await dbContext.AdCreatives.AsNoTracking().Where(c => c.CampaignId == campaignId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken);
        return rows.Select(mapper.ToDto).ToList();
    }

    /// <summary>取得檔期與其版位規格；檔期不存在回 <c>null</c>；已結束／結案／作廢的檔期不能再加素材（拋 409）。</summary>
    public async Task<AdSlot?> GetSlotForCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.AdCampaigns.AsNoTracking().Include(c => c.Slot).FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        if (campaign.Status is AdCampaignLifecycle.Ended or AdCampaignLifecycle.Closed or AdCampaignLifecycle.Voided)
        {
            throw new AdminConflictException("檔期已結束", "已結束、已結案或已作廢的檔期不能再新增或修改素材。");
        }

        return campaign.Slot;
    }

    public async Task<AdminAdCreativeDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCreatives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return c is null ? null : mapper.ToDto(c);
    }

    public async Task<Guid?> GetCampaignIdAsync(Guid creativeId, CancellationToken cancellationToken)
        => await dbContext.AdCreatives.AsNoTracking().Where(c => c.Id == creativeId).Select(c => (Guid?)c.CampaignId).FirstOrDefaultAsync(cancellationToken);

    /// <summary>檢查上傳的圖是否符合版位的素材規格（處理後的尺寸與原始檔案大小）。</summary>
    public static void ValidateImageAgainstSlot(AdSlot slot, UploadedImageInfo image, long originalBytes)
    {
        if (slot.MaxFileKb is { } maxKb && originalBytes > maxKb * 1024L)
        {
            throw new AdminValidationException($"這個版位的素材檔案大小上限是 {maxKb} KB，請壓縮後再上傳。", "image");
        }

        if (slot.MinWidth is { } minW && image.Width < minW || slot.MinHeight is { } minH && image.Height < minH)
        {
            throw new AdminValidationException($"這個版位的素材最小尺寸是 {slot.MinWidth ?? 0}×{slot.MinHeight ?? 0} 像素，目前上傳的是 {image.Width}×{image.Height}。", "image");
        }

        if (slot.AspectRatio is { } ratio)
        {
            var parts = ratio.Split(':');
            var target = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) / double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            var actual = (double)image.Width / image.Height;
            if (Math.Abs(actual - target) / target > 0.02)
            {
                throw new AdminValidationException($"這個版位的素材長寬比必須是 {ratio}，目前上傳的圖是 {image.Width}×{image.Height}。", "image");
            }
        }
    }

    public async Task<AdminAdCreativeDto> CreateAsync(
        Guid campaignId, AdSlot slot, UpsertAdminAdCreativeRequest request, UploadedImageInfo image, string? videoKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request, slot, hasVideo: videoKey is not null);
        var now = DateTime.UtcNow;
        var c = new AdCreative
        {
            Id = Guid.NewGuid(), CampaignId = campaignId, Locale = AdLabels.ToDbLocale(request.Locale), ReviewStatus = "pending",
            ImageKey = image.Key, ImageWidth = image.Width, ImageHeight = image.Height, VideoKey = videoKey,
            SortOrder = await dbContext.AdCreatives.CountAsync(x => x.CampaignId == campaignId, cancellationToken),
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(c, request);
        dbContext.AdCreatives.Add(c);
        await dbContext.SaveChangesAsync(cancellationToken);
        return mapper.ToDto(c);
    }

    public async Task<AdminAdCreativeDto?> UpdateAsync(
        Guid id, AdSlot slot, UpsertAdminAdCreativeRequest request, ImageFieldUpdate image, string? newVideoKey,
        OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCreatives.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        var hasVideoAfter = newVideoKey is not null || (c.VideoKey is not null && !request.RemoveVideo);
        Validate(request, slot, hasVideoAfter);
        var changed = image.Change || newVideoKey is not null || request.RemoveVideo
                      || c.Locale != AdLabels.ToDbLocale(request.Locale) || Changed(c, request);
        if (image.Change)
        {
            if (image.Key is null)
            {
                throw new AdminValidationException("素材一定要有圖片（影片素材的圖片是海報），不能移除。", "image");
            }

            orphans.Image(c.ImageKey);
            c.ImageKey = image.Key;
            c.ImageWidth = image.Width;
            c.ImageHeight = image.Height;
        }

        if (newVideoKey is not null || request.RemoveVideo)
        {
            orphans.Video(c.VideoKey);
            c.VideoKey = request.RemoveVideo ? null : newVideoKey;
        }

        c.Locale = AdLabels.ToDbLocale(request.Locale);
        Apply(c, request);
        if (changed)
        {
            c.ReviewStatus = "pending";
            c.RejectReason = null;
            c.ReviewedBy = null;
            c.ReviewedAt = null;
        }

        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return mapper.ToDto(c);
    }

    public async Task<AdminAdCreativeDto?> ApproveAsync(Guid id, AdminIdentity actor, CancellationToken cancellationToken)
        => await ReviewAsync(id, actor, approve: true, reason: null, cancellationToken);

    public async Task<AdminAdCreativeDto?> RejectAsync(Guid id, string? reason, AdminIdentity actor, CancellationToken cancellationToken)
        => await ReviewAsync(id, actor, approve: false, AdminInput.RequireText(reason, "退回原因", 255, "reason"), cancellationToken);

    private async Task<AdminAdCreativeDto?> ReviewAsync(Guid id, AdminIdentity actor, bool approve, string? reason, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCreatives.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        if (c.ReviewStatus != "pending")
        {
            throw new AdminConflictException("不是待審素材", "只有「待審」的素材可以核可或退回；已退回的素材請先修改後再送審。");
        }

        c.ReviewStatus = approve ? "approved" : "rejected";
        c.RejectReason = approve ? null : reason;
        c.ReviewedBy = actor.AdminUserId;
        c.ReviewedAt = DateTime.UtcNow;
        c.UpdatedAt = c.ReviewedAt.Value;
        c.UpdatedBy = actor.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(actor, approve ? "廣告素材核可" : "廣告素材退回", $"素材 {c.Id}", 1, reason);
        return mapper.ToDto(c);
    }

    /// <summary>緊急暫停或恢復單一素材（違規或申訴時的即時下架）。</summary>
    public async Task<AdminAdCreativeDto?> SetPausedAsync(Guid id, bool paused, AdminIdentity actor, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCreatives.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        c.IsPaused = paused;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = actor.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(actor, paused ? "廣告素材緊急暫停" : "廣告素材恢復投放", $"素材 {c.Id}", 1, null);
        return mapper.ToDto(c);
    }

    public async Task<bool> DeleteAsync(Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCreatives.Include(x => x.Campaign).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return false;
        }

        if (c.Campaign.Status is not (AdCampaignLifecycle.Draft or AdCampaignLifecycle.PendingReview))
        {
            throw new AdminConflictException("檔期已排程或投放過", "檔期已經排程或投放過，素材不能刪除（成效數字要留著對帳）；要下架請改用「暫停素材」。");
        }

        orphans.Image(c.ImageKey);
        orphans.Video(c.VideoKey);
        dbContext.AdCreatives.Remove(c);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Validate(UpsertAdminAdCreativeRequest r, AdSlot slot, bool hasVideo)
    {
        AdminInput.OneOf(r.Locale, Locales, "素材語系", "「繁體中文」或「英文」", "locale");
        AdminInput.RequireText(r.AltText, "替代文字（給讀屏軟體與圖片載入失敗時顯示）", 200, "altText");
        AdminInput.OptionalText(r.Title, "標題", 160, "title");
        AdminInput.OptionalText(r.CtaText, "按鈕文字", 60, "ctaText");
        var url = AdminInput.OptionalText(r.ClickUrl, "點擊目的地", 500, "clickUrl");
        if (url is not null && !url.StartsWith("tcrfc://", StringComparison.Ordinal))
        {
            AdminInput.OptionalHttpUrl(url, "點擊目的地", 500, "clickUrl");
        }

        AdminInput.OneOf(r.Theme ?? "both", AdLabels.Theme.Keys.ToHashSet(StringComparer.Ordinal), "底色版本", "「淺色底」「深色底」或「深淺底通用」", "theme");
        if (r.VariantTag is not null)
        {
            AdminInput.OneOf(r.VariantTag, Variants, "A/B 標記", "A 或 B", "variantTag");
        }

        if (hasVideo && !slot.AllowVideo)
        {
            throw new AdminValidationException("這個版位不允許影片素材。", "video");
        }
    }

    private static bool Changed(AdCreative c, UpsertAdminAdCreativeRequest r)
        => (c.AltText ?? "") != (r.AltText?.Trim() ?? "") || (c.Title ?? "") != (r.Title?.Trim() ?? "") || (c.CtaText ?? "") != (r.CtaText?.Trim() ?? "")
           || (c.ClickUrl ?? "") != (r.ClickUrl?.Trim() ?? "") || c.Theme != (r.Theme ?? "both");

    private static void Apply(AdCreative c, UpsertAdminAdCreativeRequest r)
    {
        c.AltText = AdminInput.OptionalText(r.AltText, "替代文字", 200);
        c.Title = AdminInput.OptionalText(r.Title, "標題", 160, "title");
        c.CtaText = AdminInput.OptionalText(r.CtaText, "按鈕文字", 60, "ctaText");
        c.ClickUrl = AdminInput.OptionalText(r.ClickUrl, "點擊目的地", 500);
        c.Theme = r.Theme ?? "both";
        c.VariantTag = r.VariantTag;
    }
}
