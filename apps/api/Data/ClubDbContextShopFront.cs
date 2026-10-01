using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Data;

/// <summary>
/// F 批（2026-10-01，S3-5 前台結帳）新增的 EF 對映，與 scaffold 產生的 <c>ClubDbContext.cs</c> 分開放（重新 scaffold 不會覆寫）。
/// 綱要的真實來源是 db/club-schema.sql（docs/12b §6.7／§6.9）；必須與它逐欄一致，
/// 由 <c>EfModelMatchesDatabaseTests</c> 與 <c>dotnet ef migrations has-pending-model-changes</c> 把關。
/// </summary>
public partial class ClubDbContext
{
    private static void ConfigureShopFront(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.BuyerEmail).HasMaxLength(255).HasColumnName("buyer_email");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(64).HasColumnName("idempotency_key");
            entity.Property(e => e.RequestFingerprint)
                .HasMaxLength(64)
                .IsFixedLength()
                .IsUnicode(false)
                .HasColumnName("request_fingerprint");
            entity.Property(e => e.PaymentUrl).HasMaxLength(500).HasColumnName("payment_url");
            entity.HasIndex(e => new { e.ClubId, e.IdempotencyKey }, "UQ_orders_club_idempotency")
                .IsUnique()
                .HasFilter("[idempotency_key] IS NOT NULL");
        });

        // 載具號碼密文（Data Protection）遠超過 64 字元，欄寬放寬為 500。
        modelBuilder.Entity<StoreInvoice>()
            .Property(e => e.CarrierIdEncrypted)
            .HasMaxLength(500)
            .HasColumnName("carrier_id_encrypted");

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasIndex(e => new { e.ClubId, e.MemberId }, "UQ_carts_club_member")
                .IsUnique()
                .HasFilter("[member_id] IS NOT NULL");
            entity.HasIndex(e => e.AnonymousToken, "UQ_carts_anonymous_token")
                .IsUnique()
                .HasFilter("[anonymous_token] IS NOT NULL");
        });
    }
}
