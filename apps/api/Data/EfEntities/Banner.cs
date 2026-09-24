using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Banner
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    /// <summary>素材種類（S1-7a）：<c>image</c>／<c>video</c>，預設 <c>image</c>。<c>video</c> 時
    /// <see cref="ImageKey"/> 作為影片的海報格（poster），<see cref="VideoKey"/> 必填
    /// （db/club-schema.sql <c>CK_banners_video_key</c>）。⚠️ 本輪 API 只接受 <c>image</c>，
    /// <c>video</c> 上傳規則（格式、大小、轉碼）尚待使用者裁決，見 apps/api/README.md。</summary>
    public string MediaType { get; set; } = null!;

    public string ImageKey { get; set; } = null!;

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    /// <summary>僅 <see cref="MediaType"/>＝<c>video</c> 時有值。本輪未開放寫入。</summary>
    public string? VideoKey { get; set; }

    /// <summary>草稿／發布，預設 <c>draft</c>（v3.14，主站規劃書 §4.2 B3；發布後依既有
    /// <see cref="StartAt"/>／<see cref="EndAt"/>（上架期間）自動顯示與下架，不是排程轉態，
    /// 見 db/club-schema.sql 該表註解與 docs/12 §12 第 35 點）。</summary>
    public string Status { get; set; } = null!;

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<BannersI18n> BannersI18ns { get; set; } = new List<BannersI18n>();

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<HomeSection> HomeSections { get; set; } = new List<HomeSection>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
