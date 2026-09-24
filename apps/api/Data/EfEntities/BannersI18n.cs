using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class BannersI18n
{
    public Guid BannerId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    /// <summary>圖片替代文字（S1-7a，docs/14 圖片欄位組通則）。單一語系——輪播圖是視覺內容，
    /// 中英文 alt 文字各自獨立，不像 <c>Title</c> 需要回退。</summary>
    public string? ImageAlt { get; set; }

    public string? Cta1Label { get; set; }

    public string? Cta1Url { get; set; }

    public string? Cta2Label { get; set; }

    public string? Cta2Url { get; set; }

    public virtual Banner Banner { get; set; } = null!;
}
