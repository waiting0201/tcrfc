namespace Tcrfc.Api.Features.AdminNews;

/// <summary>新聞封面焦點驗證（主站規劃書 v3.28）：對應資料庫 <c>CK_articles_cover_focal</c> 與 decimal(5,2)。</summary>
public static class CoverFocalPolicy
{
    /// <summary>兩者同為 null（＝置中），或同為 0–100 且最多 2 位小數；否則丟 <see cref="AdminArticleValidationException"/>（400，欄位鍵 <c>coverFocalX</c>）。</summary>
    public static void Validate(decimal? x, decimal? y)
    {
        if (x is null != y is null)
        {
            throw new AdminArticleValidationException("封面焦點的水平與垂直位置需要同時設定，或同時不設定。", "coverFocalX");
        }

        if (x is { } fx && y is { } fy)
        {
            if (fx is < 0 or > 100 || fy is < 0 or > 100)
            {
                throw new AdminArticleValidationException("封面焦點的位置必須介於 0 到 100 之間。", "coverFocalX");
            }

            if (decimal.Round(fx, 2) != fx || decimal.Round(fy, 2) != fy)
            {
                throw new AdminArticleValidationException("封面焦點的位置最多只能到小數點後 2 位。", "coverFocalX");
            }
        }
    }
}
