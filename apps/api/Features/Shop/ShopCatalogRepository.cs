using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.MembershipPayments;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Shop;

/// <summary>
/// 站內商店 8.3 的公開目錄讀取（主站規劃書 §3.8 8.3）：系列、商品列表（篩選與排序比照舊站：分類、價格、尺寸、顏色）、商品詳情、商店入口與政策。
/// 🔴 <b>庫存不得讀快取</b>（docs/14「五類資料不得讀快取」）：本類別與整個 <c>Features/Shop</c> <b>刻意不注入快取服務</b>
/// （<c>AdminC1MiscTests</c> 的反射掃描鎖定），可售量每次直接查庫。
/// 可見條件：商品 <c>status = published</c>；規格只輸出 <c>active</c> 的；「缺貨」由庫存自動判定（所有販售中規格的可售量都 ≤ 0），
/// 缺貨時依 <c>out_of_stock_behavior</c>：<c>show_unavailable</c> 顯示但不可購買、<c>hide</c> 從<b>列表</b>隱藏（詳情網址仍可開啟並標示缺貨，避免已流通的連結變 404）。
/// 商品與系列都以 <c>club_id</c> 硬過濾：兩隊的商品互不可見。
/// </summary>
public sealed class ShopCatalogRepository(
    ClubDbContext db, IImagePublicUrlResolver imageUrls, ClubTextSettings texts, ShopSettingsReader settings, IPaymentGateway gateway)
{
    public const int MaxReportedQty = 99;

    private static readonly string[] InfoKeys =
    [
        ShopSettingKeys.EntryTitle, ShopSettingKeys.EntryIntro, ShopSettingKeys.PolicyNotice, ShopSettingKeys.PolicyShipping,
        ShopSettingKeys.PolicyReturns, ShopSettingKeys.PolicyTerms,
    ];

    public sealed record ListFilter(string? Collection, string? Tag, int? MinPrice, int? MaxPrice, string? Size, string? Colour, string? Sort, bool NewOnly);

    // ═══════════════════════════ 入口與政策 ═══════════════════════════

    public async Task<ShopInfoDto> GetInfoAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        var map = await texts.LoadAsync(scope.ClubId, [.. InfoKeys, ShopSettingKeys.ExcludedRegions], cancellationToken);
        var en = dbLocale == "en";
        string? Text(string key)
        {
            var (zh, enText) = ClubTextSettings.Get(map, key);
            return RequestLocale.Pick(en ? enText : zh, zh);
        }

        var shipping = await settings.GetShippingAsync(scope.ClubId, cancellationToken);
        IReadOnlyList<string> regions = [];
        if (ClubTextSettings.GetValue(map, ShopSettingKeys.ExcludedRegions) is { Length: > 0 } raw)
        {
            try
            {
                regions = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            }
            catch (JsonException)
            {
                regions = [];
            }
        }

        var subject = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder)
            .Select(c => c.ClubsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault()
                         ?? c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);
        var donation = await db.InvoiceDonationCodes.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.SortOrder).ThenBy(d => d.RowSeq)
            .Select(d => new ShopDonationCodeDto { Code = d.Code, OrgName = d.OrgName }).ToListAsync(cancellationToken);
        return new ShopInfoDto
        {
            EntryTitle = Text(ShopSettingKeys.EntryTitle), EntryIntro = Text(ShopSettingKeys.EntryIntro), PolicyNotice = Text(ShopSettingKeys.PolicyNotice),
            PolicyShipping = Text(ShopSettingKeys.PolicyShipping), PolicyReturns = Text(ShopSettingKeys.PolicyReturns), PolicyTerms = Text(ShopSettingKeys.PolicyTerms),
            ShippingFee = shipping.Fee, FreeShippingThreshold = shipping.FreeThreshold, ExcludedRegions = regions,
            DeliveryMethods = ShopLabelsPublic.DeliveryMethods(shipping.Fee, en), DonationCodes = donation,
            CollectingSubjectName = subject, PaymentAvailable = gateway.IsConfigured,
            Collections = await ListCollectionsAsync(scope, dbLocale, cancellationToken),
        };
    }

    public async Task<IReadOnlyList<ShopCollectionDto>> ListCollectionsAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        var rows = await db.Collections.AsNoTracking().Include(c => c.CollectionsI18ns)
            .Where(c => c.ClubId == scope.ClubId && c.Status == "published").OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken);
        var counts = await db.Products.AsNoTracking().Where(p => p.ClubId == scope.ClubId && p.Status == "published" && p.CollectionId != null)
            .GroupBy(p => p.CollectionId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);
        return rows.Select(c =>
        {
            var requested = c.CollectionsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = c.CollectionsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            return new ShopCollectionDto
            {
                Slug = c.Slug, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), Narrative = RequestLocale.Pick(requested?.Narrative, fallback?.Narrative),
                ProductCount = counts.GetValueOrDefault(c.Id),
            };
        }).ToList();
    }

    // ═══════════════════════════ 商品列表 ═══════════════════════════

    public async Task<PagedResult<ShopProductListItemDto>> ListProductsAsync(
        ClubScope scope, ListFilter filter, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking().Where(p => p.ClubId == scope.ClubId && p.Status == "published");

        // 缺貨且設定為「自動隱藏」者不進列表。
        query = query.Where(p => !(p.OutOfStockBehavior == "hide"
                                   && !p.ProductVariants.Any(v => v.Status == "active" && v.StockQty - v.ReservedQty > 0)));

        if (!string.IsNullOrWhiteSpace(filter.Collection))
        {
            var slug = filter.Collection.Trim();
            query = query.Where(p => p.Collection != null && p.Collection.Slug == slug && p.Collection.Status == "published");
        }

        if (filter.NewOnly)
        {
            query = query.Where(p => p.IsNewArrival);
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag.Trim();
            query = query.Where(p => p.ProductsI18ns.Any(i => i.Tags != null && i.Tags.Contains(tag)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Size))
        {
            var size = filter.Size.Trim();
            query = query.Where(p => p.ProductVariants.Any(v => v.Status == "active" && v.Size == size));
        }

        if (!string.IsNullOrWhiteSpace(filter.Colour))
        {
            var colour = filter.Colour.Trim();
            query = query.Where(p => p.ProductVariants.Any(v => v.Status == "active" && v.Colour == colour));
        }

        if (filter.MinPrice is int min)
        {
            query = query.Where(p => p.ProductVariants.Any(v => v.Status == "active" && (v.SalePrice ?? v.Price) >= min));
        }

        if (filter.MaxPrice is int max)
        {
            query = query.Where(p => p.ProductVariants.Any(v => v.Status == "active" && (v.SalePrice ?? v.Price) <= max));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = filter.Sort switch
        {
            "price_asc" => query.OrderBy(p => p.ProductVariants.Where(v => v.Status == "active").Min(v => (int?)(v.SalePrice ?? v.Price)) ?? int.MaxValue).ThenBy(p => p.SortOrder),
            "price_desc" => query.OrderByDescending(p => p.ProductVariants.Where(v => v.Status == "active").Max(v => (int?)(v.SalePrice ?? v.Price)) ?? 0).ThenBy(p => p.SortOrder),
            "newest" => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.SortOrder),
            _ => query.OrderBy(p => p.SortOrder).ThenByDescending(p => p.RowSeq),
        };
        var products = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Include(p => p.ProductsI18ns).Include(p => p.Collection).ThenInclude(c => c!.CollectionsI18ns)
            .AsSplitQuery().ToListAsync(cancellationToken);
        var ids = products.Select(p => p.Id).ToList();
        var variants = await db.ProductVariants.AsNoTracking().Where(v => ids.Contains(v.ProductId) && v.Status == "active")
            .OrderBy(v => v.SortOrder).ThenBy(v => v.RowSeq).ToListAsync(cancellationToken);
        var covers = await db.ProductImages.AsNoTracking().Where(i => ids.Contains(i.ProductId))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new { i.ProductId, i.ImageKey }).ToListAsync(cancellationToken);
        var coverByProduct = covers.GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.First().ImageKey);
        var threshold = await settings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        var en = dbLocale == "en";
        var items = products.Select(p => ToListItem(p, variants.Where(v => v.ProductId == p.Id).ToList(), coverByProduct.GetValueOrDefault(p.Id), threshold, dbLocale, en)).ToList();
        return new PagedResult<ShopProductListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    // ═══════════════════════════ 商品詳情 ═══════════════════════════

    public async Task<ShopProductDetailDto?> GetProductAsync(ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.ProductsI18ns).Include(p => p.ProductImages)
            .Include(p => p.Collection).ThenInclude(c => c!.CollectionsI18ns).AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ClubId == scope.ClubId && p.Slug == slug && p.Status == "published", cancellationToken);
        if (product is null)
        {
            return null;
        }

        var variants = await db.ProductVariants.AsNoTracking().Where(v => v.ProductId == product.Id && v.Status == "active")
            .OrderBy(v => v.SortOrder).ThenBy(v => v.RowSeq).ToListAsync(cancellationToken);
        var threshold = await settings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        var en = dbLocale == "en";
        var requested = product.ProductsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = product.ProductsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var summary = Summarize(variants, threshold, en);
        return new ShopProductDetailDto
        {
            Slug = product.Slug, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), Narrative = RequestLocale.Pick(requested?.Narrative, fallback?.Narrative),
            SeoTitle = RequestLocale.Pick(requested?.SeoTitle, fallback?.SeoTitle), SeoDescription = RequestLocale.Pick(requested?.SeoDescription, fallback?.SeoDescription),
            CollectionSlug = product.Collection?.Slug, CollectionName = CollectionName(product.Collection, dbLocale),
            Tags = ParseTags(RequestLocale.Pick(requested?.Tags, fallback?.Tags)), IsNewArrival = product.IsNewArrival, SizeChart = ParseJson(product.SizeChart),
            Images = product.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new ShopImageDto
            {
                Url = imageUrls.Resolve(i.ImageKey) ?? string.Empty, ThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)) ?? string.Empty, Width = i.Width, Height = i.Height,
            }).ToList(),
            Variants = variants.Select(ToVariant).ToList(), PriceMin = summary.PriceMin, PriceMax = summary.PriceMax, ListPriceMin = summary.ListPriceMin,
            OnSale = summary.OnSale, StockStatus = summary.StockStatus, StockStatusLabel = summary.StockStatusLabel,
        };
    }

    // ═══════════════════════════ 共用 ═══════════════════════════

    public static ShopVariantDto ToVariant(ProductVariant v)
    {
        var available = Math.Max(0, v.StockQty - v.ReservedQty);
        var onSale = v.SalePrice is int sale && sale < v.Price;
        return new ShopVariantDto
        {
            Id = v.Id, Sku = v.Sku, Size = v.Size, Colour = v.Colour, Label = AdminShopProductsRepository.VariantLabel(v.Size, v.Colour),
            ListPrice = v.Price, Price = onSale ? v.SalePrice!.Value : v.Price, OnSale = onSale, AvailableQty = Math.Min(available, MaxReportedQty), Purchasable = available > 0,
        };
    }

    public readonly record struct StockSummary(int? PriceMin, int? PriceMax, int? ListPriceMin, bool OnSale, string StockStatus, string StockStatusLabel);

    public static StockSummary Summarize(IReadOnlyList<ProductVariant> activeVariants, int lowStockThreshold, bool en)
    {
        if (activeVariants.Count == 0)
        {
            return new StockSummary(null, null, null, false, "sold_out", ShopLabelsPublic.StockLabel("sold_out", en));
        }

        int Effective(ProductVariant v) => v.SalePrice is int s && s < v.Price ? s : v.Price;
        var onSaleVariants = activeVariants.Where(v => v.SalePrice is int s && s < v.Price).ToList();
        var totalAvailable = activeVariants.Sum(v => Math.Max(0, v.StockQty - v.ReservedQty));
        var status = totalAvailable <= 0 ? "sold_out"
            : activeVariants.Where(v => v.StockQty - v.ReservedQty > 0).All(v => v.StockQty - v.ReservedQty <= (v.LowStockThreshold ?? lowStockThreshold)) ? "low_stock"
            : "in_stock";
        return new StockSummary(
            activeVariants.Min(Effective), activeVariants.Max(Effective), onSaleVariants.Count == 0 ? null : onSaleVariants.Min(v => v.Price),
            onSaleVariants.Count > 0, status, ShopLabelsPublic.StockLabel(status, en));
    }

    private ShopProductListItemDto ToListItem(
        Product p, IReadOnlyList<ProductVariant> variants, string? coverKey, int threshold, string dbLocale, bool en)
    {
        var requested = p.ProductsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = p.ProductsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var summary = Summarize(variants, threshold, en);
        return new ShopProductListItemDto
        {
            Slug = p.Slug, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), CollectionSlug = p.Collection?.Slug, CollectionName = CollectionName(p.Collection, dbLocale),
            Tags = ParseTags(RequestLocale.Pick(requested?.Tags, fallback?.Tags)), IsNewArrival = p.IsNewArrival,
            ImageUrl = coverKey is null ? null : imageUrls.Resolve(coverKey), ImageThumbUrl = coverKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(coverKey)),
            PriceMin = summary.PriceMin, PriceMax = summary.PriceMax, ListPriceMin = summary.ListPriceMin, OnSale = summary.OnSale,
            StockStatus = summary.StockStatus, StockStatusLabel = summary.StockStatusLabel,
            Sizes = variants.Select(v => v.Size).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToList(),
            Colours = variants.Select(v => v.Colour).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().ToList(),
        };
    }

    private static string? CollectionName(Collection? collection, string dbLocale)
    {
        if (collection is null)
        {
            return null;
        }

        return RequestLocale.Pick(collection.CollectionsI18ns.FirstOrDefault(i => i.Locale == dbLocale)?.Name,
            collection.CollectionsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name);
    }

    internal static IReadOnlyList<string> ParseTags(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', '，', '、', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    private static JsonElement? ParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>公開商店的日常中文／英文標籤。</summary>
public static class ShopLabelsPublic
{
    public static string StockLabel(string status, bool en) => status switch
    {
        "sold_out" => en ? "Sold out" : "缺貨",
        "low_stock" => en ? "Low stock" : "庫存緊張",
        _ => en ? "In stock" : "供貨中",
    };

    public static IReadOnlyList<ShopDeliveryMethodDto> DeliveryMethods(int fee, bool en)
        =>
        [
            new() { Code = "home_delivery", Label = en ? "Home delivery" : "宅配到府", BaseFee = fee },
            new() { Code = "cvs_pickup", Label = en ? "Convenience-store pickup (no payment at the store)" : "超商取貨（僅取貨，不在門市付款）", BaseFee = fee },
            new() { Code = "onsite_pickup", Label = en ? "Pick up at a home match day or the club" : "主場賽事日或俱樂部現場自取", BaseFee = 0 },
        ];

    public static string DeliveryLabel(string code, bool en) => code switch
    {
        "home_delivery" => en ? "Home delivery" : "宅配到府",
        "cvs_pickup" => en ? "Convenience-store pickup" : "超商取貨",
        "onsite_pickup" => en ? "On-site pickup" : "現場自取",
        _ => code,
    };

    public static string InvoiceTypeLabel(string type, bool en) => type switch
    {
        "mobile_barcode" => en ? "Mobile barcode carrier" : "手機條碼載具",
        "citizen_cert" => en ? "Citizen digital certificate carrier" : "自然人憑證載具",
        "tax_id" => en ? "Company tax ID" : "統一編號",
        "donation" => en ? "Donate the invoice" : "捐贈發票",
        _ => type,
    };

    public static string InvoiceStatusLabel(string status, bool en) => status switch
    {
        "issued" => en ? "Issued" : "已開立",
        _ => en ? "Processing" : "處理中",
    };

    public static string PaymentStatusLabel(string status, bool en) => status switch
    {
        "paid" => en ? "Paid" : "已付款",
        "failed" => en ? "Payment failed" : "付款失敗",
        "expired" => en ? "Expired" : "已逾時",
        "refunded" => en ? "Refunded" : "已退款",
        _ => en ? "Awaiting payment" : "待付款",
    };

    public static string PaymentMethodLabel(string method, bool en) => method switch
    {
        "onsite" => en ? "Paid on site" : "現場收款",
        _ => "LINE Pay",
    };

    public static string PickupLabel(string status, bool en) => status switch
    {
        "picked_up" => en ? "Picked up" : "已領取",
        "overdue" => en ? "Overdue" : "逾期",
        _ => en ? "Ready for pickup" : "待領取",
    };
}
