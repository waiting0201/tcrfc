using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Images;
using Tcrfc.Api.Videos;

namespace Tcrfc.Api.Features.AdminAds;

public sealed class AdCreativeMapper(IImagePublicUrlResolver imageUrls, IVideoPublicUrlResolver videoUrls)
{
    public AdminAdCreativeDto ToDto(AdCreative c) => new()
    {
        Id = c.Id, CampaignId = c.CampaignId, Locale = AdLabels.ToExternalLocale(c.Locale), ImageKey = c.ImageKey,
        ImageUrl = imageUrls.Resolve(c.ImageKey),
        ImageThumbUrl = c.ImageKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(c.ImageKey)),
        ImageWidth = c.ImageWidth, ImageHeight = c.ImageHeight, VideoKey = c.VideoKey, VideoUrl = videoUrls.Resolve(c.VideoKey),
        AltText = c.AltText, Title = c.Title, CtaText = c.CtaText, ClickUrl = c.ClickUrl, Theme = c.Theme,
        ThemeLabel = AdLabels.Of(AdLabels.Theme, c.Theme), VariantTag = c.VariantTag, ReviewStatus = c.ReviewStatus,
        ReviewStatusLabel = AdLabels.Of(AdLabels.ReviewStatus, c.ReviewStatus), RejectReason = c.RejectReason, ReviewedAt = c.ReviewedAt,
        IsPaused = c.IsPaused, UpdatedAt = c.UpdatedAt,
    };
}
