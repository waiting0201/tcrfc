using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Shop;

/// <summary>購物車的擁有者：會員（權杖）或訪客（<c>X-Cart-Token</c> 的雜湊）。<b>只有 <see cref="ShopCartService.ResolveOwnerAsync"/> 建得出來</b>——條件永遠來自權杖，不來自路由或請求本文。</summary>
public sealed class CartOwner
{
    public Guid? MemberId { get; }
    public string? TokenHash { get; }

    internal CartOwner(Guid? memberId, string? tokenHash)
    {
        MemberId = memberId;
        TokenHash = tokenHash;
    }

    /// <summary>用於結帳請求指紋與冪等：誰在下單。</summary>
    public string Fingerprint => MemberId is Guid m ? $"m:{m:N}" : $"g:{TokenHash}";
}

/// <summary>
/// 站內商店購物車（規劃書 §3.8 8.3「購物車」：跨頁保留、登入者存帳號、未登入存瀏覽器端 token、登入後合併、修改數量、刪除、小計與運費試算）。
/// 🔴 <b>購物車不得跨俱樂部混買</b>（§4.13、docs/14）：<c>carts.club_id</c> 必填，擁有者的購物車以 <c>(club_id, 擁有者)</c> 定位，
/// 規格一律以 <c>variant.club_id = 目前俱樂部</c> 驗證——拿另一隊的規格 id 加入購物車回 404，切換站台即切換購物車。
/// 🔴 <b>購物車不得讀快取</b>（docs/14 五類之一）：本類別不注入快取服務；每次顯示都重新查庫計算價格與可售量，
/// 價格與庫存以<b>當下</b>為準（購物車只存「規格＋數量」，不存價格快照）。
/// 訪客權杖：伺服器發出 32 位元組亂數、<b>只存 SHA-256 雜湊</b>（<c>carts.anonymous_token</c>），遺失無法補發。
/// </summary>
public sealed class ShopCartService(ClubDbContext db, MemberAuthenticator members, ShopSettingsReader settings, IImagePublicUrlResolver imageUrls)
{
    public const string CartTokenHeader = "X-Cart-Token";
    public const int MaxLines = 50;
    public const int MaxQuantityPerLine = 99;
    private const int MaxTokenLength = 128;

    // ═══════════════════════════ 擁有者 ═══════════════════════════

    /// <summary>解出購物車擁有者：有效會員權杖 → 會員；否則 <c>X-Cart-Token</c> → 訪客；兩者皆無回 null。</summary>
    public async Task<CartOwner?> ResolveOwnerAsync(HttpContext http, CancellationToken cancellationToken)
    {
        var me = await members.TryAsync(http, cancellationToken);
        if (me is not null)
        {
            return new CartOwner(me.MemberId, null);
        }

        var token = http.Request.Headers[CartTokenHeader].ToString();
        return string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength ? null : new CartOwner(null, Hash(token.Trim()));
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private Task<Cart?> FindCartAsync(ClubScope scope, CartOwner owner, CancellationToken cancellationToken)
    {
        var query = db.Carts.Include(c => c.CartItems).Where(c => c.ClubId == scope.ClubId);
        query = owner.MemberId is Guid m ? query.Where(c => c.MemberId == m) : query.Where(c => c.AnonymousToken == owner.TokenHash);
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>取得或建立購物車。回傳 (購物車, 新發出的訪客權杖)；權杖只在「沒帶權杖或權杖查無購物車」而新建訪客購物車時有值。</summary>
    private async Task<(Cart Cart, string? NewToken)> GetOrCreateAsync(ClubScope scope, CartOwner? owner, CancellationToken cancellationToken)
    {
        if (owner is not null && await FindCartAsync(scope, owner, cancellationToken) is { } existing)
        {
            return (existing, null);
        }

        string? newToken = null;
        var memberId = owner?.MemberId;
        string? hash = owner?.TokenHash;
        if (memberId is null)
        {
            // 訪客：沒帶權杖或權杖查無購物車（過期、已清理、換了俱樂部）→ 發一組新權杖，不沿用用戶端自帶的字串（避免被指定權杖）。
            newToken = SecureToken.Generate();
            hash = Hash(newToken);
        }

        var now = DateTime.UtcNow;
        var cart = new Cart { Id = Guid.NewGuid(), ClubId = scope.ClubId, MemberId = memberId, AnonymousToken = hash, CreatedAt = now, UpdatedAt = now };
        db.Carts.Add(cart);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (cart, newToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex) && memberId is not null)
        {
            // 同一會員並行建第一台購物車：用先成立的那台。
            db.Entry(cart).State = EntityState.Detached;
            var winner = await FindCartAsync(scope, owner!, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return (winner, null);
        }
    }

    internal static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };

    // ═══════════════════════════ 讀 ═══════════════════════════

    public async Task<ShopCartDto> GetAsync(ClubScope scope, CartOwner? owner, string dbLocale, CancellationToken cancellationToken)
    {
        var cart = owner is null ? null : await FindCartAsync(scope, owner, cancellationToken);
        return await BuildAsync(scope, cart, newToken: null, dbLocale, cancellationToken);
    }

    // ═══════════════════════════ 寫 ═══════════════════════════

    public async Task<ShopCartDto> AddAsync(ClubScope scope, CartOwner? owner, AddCartItemRequest request, string dbLocale, CancellationToken cancellationToken)
    {
        if (request.Quantity is < 1 or > MaxQuantityPerLine)
        {
            throw new MemberValidationException($"數量請填 1 到 {MaxQuantityPerLine}。", "invalid_quantity");
        }

        var variant = await LoadPurchasableVariantAsync(scope, request.VariantId, cancellationToken);
        var (cart, newToken) = await GetOrCreateAsync(scope, owner, cancellationToken);
        var line = cart.CartItems.FirstOrDefault(i => i.ProductVariantId == variant.Id);
        var desired = (line?.Quantity ?? 0) + request.Quantity;
        await EnsureQuantityAllowedAsync(variant, desired, cancellationToken);
        if (line is null && cart.CartItems.Count >= MaxLines)
        {
            throw new MemberConflictException("購物車已滿", $"購物車最多放 {MaxLines} 種商品規格。", "cart_full");
        }

        await UpsertLineAsync(cart, variant.Id, desired, cancellationToken);
        return await ReloadAndBuildAsync(scope, cart.Id, newToken, dbLocale, cancellationToken);
    }

    public async Task<ShopCartDto> SetQuantityAsync(
        ClubScope scope, CartOwner? owner, Guid variantId, int quantity, string dbLocale, CancellationToken cancellationToken)
    {
        if (quantity is < 0 or > MaxQuantityPerLine)
        {
            throw new MemberValidationException($"數量請填 0 到 {MaxQuantityPerLine}（0 代表移除）。", "invalid_quantity");
        }

        var cart = owner is null ? null : await FindCartAsync(scope, owner, cancellationToken);
        var line = cart?.CartItems.FirstOrDefault(i => i.ProductVariantId == variantId)
                   ?? throw new MemberNotFoundException("購物車裡沒有這項商品。", "cart_item_not_found");
        if (quantity == 0)
        {
            await RemoveLineAsync(cart!, variantId, cancellationToken);
        }
        else
        {
            var variant = await LoadPurchasableVariantAsync(scope, variantId, cancellationToken);
            await EnsureQuantityAllowedAsync(variant, quantity, cancellationToken);
            await UpsertLineAsync(cart!, line.ProductVariantId, quantity, cancellationToken);
        }

        return await ReloadAndBuildAsync(scope, cart!.Id, null, dbLocale, cancellationToken);
    }

    public async Task<ShopCartDto> RemoveAsync(ClubScope scope, CartOwner? owner, Guid variantId, string dbLocale, CancellationToken cancellationToken)
    {
        var cart = owner is null ? null : await FindCartAsync(scope, owner, cancellationToken);
        if (cart is null || cart.CartItems.All(i => i.ProductVariantId != variantId))
        {
            throw new MemberNotFoundException("購物車裡沒有這項商品。", "cart_item_not_found");
        }

        await RemoveLineAsync(cart, variantId, cancellationToken);
        return await ReloadAndBuildAsync(scope, cart.Id, null, dbLocale, cancellationToken);
    }

    public async Task<ShopCartDto> ClearAsync(ClubScope scope, CartOwner? owner, string dbLocale, CancellationToken cancellationToken)
    {
        var cart = owner is null ? null : await FindCartAsync(scope, owner, cancellationToken);
        if (cart is not null)
        {
            await db.CartItems.Where(i => i.CartId == cart.Id).ExecuteDeleteAsync(cancellationToken);
            await Touch(cart.Id, cancellationToken);
            db.ChangeTracker.Clear();
        }

        return await GetAsync(scope, owner, dbLocale, cancellationToken);
    }

    /// <summary>登入後合併：把訪客購物車（<c>X-Cart-Token</c>）併入會員購物車，數量相加（單項上限 <see cref="MaxQuantityPerLine"/>），再刪除訪客購物車。重複呼叫安全（訪客購物車已不存在就只回會員購物車）。</summary>
    public async Task<ShopCartDto> MergeGuestIntoMemberAsync(ClubScope scope, HttpContext http, MemberIdentity me, string dbLocale, CancellationToken cancellationToken)
    {
        var memberOwner = new CartOwner(me.MemberId, null);
        var token = http.Request.Headers[CartTokenHeader].ToString();
        if (!string.IsNullOrWhiteSpace(token) && token.Length <= MaxTokenLength)
        {
            var guest = await FindCartAsync(scope, new CartOwner(null, Hash(token.Trim())), cancellationToken);
            if (guest is not null)
            {
                var (target, _) = await GetOrCreateAsync(scope, memberOwner, cancellationToken);
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                foreach (var item in guest.CartItems.ToList())
                {
                    var existing = target.CartItems.FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);
                    if (existing is null && target.CartItems.Count >= MaxLines)
                    {
                        continue;
                    }

                    await UpsertLineAsync(target, item.ProductVariantId, Math.Min(MaxQuantityPerLine, (existing?.Quantity ?? 0) + item.Quantity), cancellationToken);
                }

                await db.CartItems.Where(i => i.CartId == guest.Id).ExecuteDeleteAsync(cancellationToken);
                await db.Carts.Where(c => c.Id == guest.Id).ExecuteDeleteAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }

        return await GetAsync(scope, memberOwner, dbLocale, cancellationToken);
    }

    // ═══════════════════════════ 內部 ═══════════════════════════

    private async Task<ProductVariant> LoadPurchasableVariantAsync(ClubScope scope, Guid variantId, CancellationToken cancellationToken)
    {
        // 🔴 club_id 硬過濾：別隊的規格 id 一律 404（不混買、不洩漏存在與否）。
        var variant = await db.ProductVariants.AsNoTracking().Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ClubId == scope.ClubId && v.Product.ClubId == scope.ClubId, cancellationToken);
        if (variant is null || variant.Product.Status != "published" || variant.Status != "active")
        {
            throw new MemberNotFoundException("找不到這項商品，或它目前已下架。", "variant_not_found");
        }

        return variant;
    }

    private async Task EnsureQuantityAllowedAsync(ProductVariant variant, int desired, CancellationToken cancellationToken)
    {
        if (desired > MaxQuantityPerLine)
        {
            throw new MemberConflictException("數量超過上限", $"同一項商品一次最多購買 {MaxQuantityPerLine} 件。", "quantity_limit");
        }

        // 直接查庫的最新值（不用先前載入的可能陳舊的欄位）。
        var available = await db.ProductVariants.AsNoTracking().Where(v => v.Id == variant.Id).Select(v => v.StockQty - v.ReservedQty).FirstAsync(cancellationToken);
        if (desired > available)
        {
            throw new MemberConflictException("庫存不足", available <= 0 ? "這項商品目前已售完。" : $"這項商品目前只剩 {available} 件。", "insufficient_stock");
        }
    }

    private async Task UpsertLineAsync(Cart cart, Guid variantId, int quantity, CancellationToken cancellationToken)
    {
        var affected = await db.CartItems.Where(i => i.CartId == cart.Id && i.ProductVariantId == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, quantity), cancellationToken);
        if (affected == 0)
        {
            try
            {
                db.CartItems.Add(new CartItem { CartId = cart.Id, ProductVariantId = variantId, Quantity = quantity });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // 並行加入同一規格：改成更新數量（後到者的數量為準）。
                db.ChangeTracker.Clear();
                await db.CartItems.Where(i => i.CartId == cart.Id && i.ProductVariantId == variantId)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, quantity), cancellationToken);
            }
        }

        await Touch(cart.Id, cancellationToken);
    }

    private async Task RemoveLineAsync(Cart cart, Guid variantId, CancellationToken cancellationToken)
    {
        await db.CartItems.Where(i => i.CartId == cart.Id && i.ProductVariantId == variantId).ExecuteDeleteAsync(cancellationToken);
        await Touch(cart.Id, cancellationToken);
    }

    private Task Touch(Guid cartId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return db.Carts.Where(c => c.Id == cartId).ExecuteUpdateAsync(s => s.SetProperty(c => c.UpdatedAt, now), cancellationToken);
    }

    private async Task<ShopCartDto> ReloadAndBuildAsync(ClubScope scope, Guid cartId, string? newToken, string dbLocale, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var cart = await db.Carts.Include(c => c.CartItems).FirstAsync(c => c.Id == cartId, cancellationToken);
        return await BuildAsync(scope, cart, newToken, dbLocale, cancellationToken);
    }

    /// <summary>結帳用：載入購物車並逐列算出「現在的」價格與可購買性。</summary>
    public async Task<(Cart? Cart, IReadOnlyList<CartLine> Lines)> LoadLinesAsync(ClubScope scope, CartOwner? owner, string dbLocale, CancellationToken cancellationToken)
    {
        var cart = owner is null ? null : await FindCartAsync(scope, owner, cancellationToken);
        if (cart is null)
        {
            return (null, []);
        }

        return (cart, await BuildLinesAsync(scope, cart, dbLocale, cancellationToken));
    }

    public sealed record CartLine(
        Guid VariantId, string ProductSlug, string? ProductName, string VariantLabel, string Sku, string? ImageKey, int? ImageWidth, int? ImageHeight, string? ImageAlt, int ListPrice, int UnitPrice, bool OnSale,
        int Quantity, int Available, bool Purchasable, string? Issue, string? IssueMessage);

    private async Task<IReadOnlyList<CartLine>> BuildLinesAsync(ClubScope scope, Cart cart, string dbLocale, CancellationToken cancellationToken)
    {
        var ids = cart.CartItems.Select(i => i.ProductVariantId).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.ProductVariants.AsNoTracking().Include(v => v.Product).ThenInclude(p => p.ProductsI18ns)
            .Where(v => ids.Contains(v.Id) && v.ClubId == scope.ClubId).AsSplitQuery().ToListAsync(cancellationToken);
        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var covers = await db.ProductImages.AsNoTracking().Where(i => productIds.Contains(i.ProductId))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new { i.ProductId, Cover = new ShopCatalogRepository.ProductCover(i.ImageKey, i.Width, i.Height, i.ImageAltZh, i.ImageAltEn) }).ToListAsync(cancellationToken);
        var coverBy = covers.GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g => g.First().Cover);
        var lines = new List<CartLine>();
        foreach (var item in cart.CartItems.OrderBy(i => i.ProductVariantId))
        {
            var v = rows.FirstOrDefault(r => r.Id == item.ProductVariantId);
            if (v is null)
            {
                continue; // 規格已被刪除或不屬於這個俱樂部：從顯示中略過（結帳時自然不含）
            }

            var requested = v.Product.ProductsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = v.Product.ProductsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var onSale = v.SalePrice is int sale && sale < v.Price;
            var unit = onSale ? v.SalePrice!.Value : v.Price;
            var available = Math.Max(0, v.StockQty - v.ReservedQty);
            string? issue = null;
            string? message = null;
            if (v.Product.Status != "published" || v.Status != "active")
            {
                issue = "unavailable";
                message = "這項商品已下架或停售，請從購物車移除。";
            }
            else if (item.Quantity > available)
            {
                issue = "insufficient_stock";
                message = available <= 0 ? "這項商品目前已售完。" : $"這項商品目前只剩 {available} 件，請調整數量。";
            }

            lines.Add(new CartLine(
                v.Id, v.Product.Slug, RequestLocale.Pick(requested?.Name, fallback?.Name), AdminShopProductsRepository.VariantLabel(v.Size, v.Colour), v.Sku,
                coverBy.GetValueOrDefault(v.ProductId)?.Key, coverBy.GetValueOrDefault(v.ProductId)?.Width, coverBy.GetValueOrDefault(v.ProductId)?.Height,
                coverBy.TryGetValue(v.ProductId, out var cv) ? GalleryImageAlt.Pick(dbLocale, cv.AltZh, cv.AltEn) : null, v.Price, unit, onSale, item.Quantity, available, issue is null, issue, message));
        }

        return lines;
    }

    private async Task<ShopCartDto> BuildAsync(ClubScope scope, Cart? cart, string? newToken, string dbLocale, CancellationToken cancellationToken)
    {
        var lines = cart is null ? [] : await BuildLinesAsync(scope, cart, dbLocale, cancellationToken);
        var shippingRule = await settings.GetShippingAsync(scope.ClubId, cancellationToken);
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var fee = lines.Count == 0 ? 0 : ShopSettingsReader.ComputeShippingFee(shippingRule, "home_delivery", subtotal);
        int? toFree = lines.Count > 0 && fee > 0 && shippingRule.FreeThreshold is int t ? Math.Max(0, t - subtotal) : null;
        return new ShopCartDto
        {
            CartToken = newToken, ClubCode = scope.ClubCode, ItemCount = lines.Sum(l => l.Quantity), Subtotal = subtotal,
            Items = lines.Select(l => new ShopCartItemDto
            {
                VariantId = l.VariantId, ProductSlug = l.ProductSlug, ProductName = l.ProductName, VariantLabel = l.VariantLabel, Sku = l.Sku,
                ImageThumbUrl = l.ImageKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(l.ImageKey)),
                ImageWidth = l.ImageWidth, ImageHeight = l.ImageHeight, ImageAlt = l.ImageAlt, ListPrice = l.ListPrice, UnitPrice = l.UnitPrice,
                OnSale = l.OnSale, Quantity = l.Quantity, LineTotal = l.UnitPrice * l.Quantity, AvailableQty = Math.Min(l.Available, ShopCatalogRepository.MaxReportedQty),
                Purchasable = l.Purchasable, Issue = l.Issue, IssueMessage = l.IssueMessage,
            }).ToList(),
            Shipping = new ShopCartShippingDto { Fee = fee, FreeThreshold = shippingRule.FreeThreshold, AmountToFree = toFree },
            CanCheckout = lines.Count > 0 && lines.All(l => l.Purchasable),
        };
    }
}
