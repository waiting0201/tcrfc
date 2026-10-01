using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Data;

/// <summary>
/// E 批（2026-10-01，S2-11 會員前台）新增的 EF 對映，與 scaffold 產生的 <c>ClubDbContext.cs</c> 分開放（重新 scaffold 不會覆寫）。
/// 綱要的真實來源是 db/club-schema.sql（docs/12b §6.5c）；這裡必須與它逐欄一致，
/// 由 <c>EfModelMatchesDatabaseTests</c> 與 <c>dotnet ef migrations has-pending-model-changes</c> 把關。
/// </summary>
public partial class ClubDbContext
{
    public virtual DbSet<MemberRefreshToken> MemberRefreshTokens { get; set; }

    public virtual DbSet<MembershipOrder> MembershipOrders { get; set; }

    private static void ConfigureMemberFront(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>(entity =>
        {
            entity.Property(e => e.FailedAttemptCount)
                .HasDefaultValue(0)
                .HasColumnName("failed_attempt_count");
            entity.Property(e => e.LockedUntil)
                .HasPrecision(3)
                .HasColumnName("locked_until");
            entity.Property(e => e.LineUserIdHash)
                .HasMaxLength(64)
                .IsFixedLength()
                .IsUnicode(false)
                .HasColumnName("line_user_id_hash");
            entity.HasIndex(e => e.LineUserIdHash, "UQ_members_line_user_id_hash")
                .IsUnique()
                .HasFilter("[line_user_id_hash] IS NOT NULL");
        });

        modelBuilder.Entity<MembershipPayment>(entity =>
        {
            entity.Property(e => e.MembershipOrderId).HasColumnName("membership_order_id");
            entity.HasIndex(e => e.MembershipOrderId, "UQ_membership_payments_order")
                .IsUnique()
                .HasFilter("[membership_order_id] IS NOT NULL");
            entity.HasOne<MembershipOrder>().WithMany()
                .HasForeignKey(d => d.MembershipOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_payments_order");
        });

        modelBuilder.Entity<MemberRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("member_refresh_tokens");

            entity.HasIndex(e => e.MemberId, "IX_member_refresh_tokens_member");

            entity.HasIndex(e => e.RowSeq, "UQ_member_refresh_tokens_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.TokenHash, "UQ_member_refresh_tokens_token_hash").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(3)
                .HasColumnName("expires_at");
            entity.Property(e => e.IsPersistent).HasColumnName("is_persistent");
            entity.Property(e => e.IssuedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("issued_at");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");
            entity.Property(e => e.RevokedAt)
                .HasPrecision(3)
                .HasColumnName("revoked_at");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");

            entity.HasOne(d => d.Member).WithMany()
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_member_refresh_tokens_member");

            entity.HasOne(d => d.ReplacedBy).WithMany()
                .HasForeignKey(d => d.ReplacedById)
                .HasConstraintName("FK_member_refresh_tokens_replaced");
        });

        modelBuilder.Entity<MembershipOrder>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("membership_orders", t =>
            {
                t.HasCheckConstraint("CK_membership_orders_status",
                    "[status] IN ('created','pending_payment','paid','activated','expired','activation_failed','cancelled','refunded')");
                t.HasCheckConstraint("CK_membership_orders_payment_method", "[payment_method] IS NULL OR [payment_method] IN ('linepay')");
                t.HasCheckConstraint("CK_membership_orders_activation_source", "[activation_source] IS NULL OR [activation_source] IN ('payment','internal','admin')");
            });

            entity.HasIndex(e => new { e.ClubId, e.Status }, "IX_membership_orders_club_status");

            entity.HasIndex(e => new { e.MemberId, e.CreatedAt }, "IX_membership_orders_member_created")
                .IsDescending(false, true);

            entity.HasIndex(e => e.OrderNo, "UQ_membership_orders_order_no").IsUnique();

            entity.HasIndex(e => new { e.MemberId, e.IdempotencyKey }, "UQ_membership_orders_member_idempotency").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_membership_orders_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ActivatedAt)
                .HasPrecision(3)
                .HasColumnName("activated_at");
            entity.Property(e => e.ActivationSource)
                .HasMaxLength(16)
                .HasColumnName("activation_source");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CollectingClubId).HasColumnName("collecting_club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(3)
                .HasColumnName("expires_at");
            entity.Property(e => e.FailureReason)
                .HasMaxLength(255)
                .HasColumnName("failure_reason");
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(64)
                .HasColumnName("idempotency_key");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.MembershipId).HasColumnName("membership_id");
            entity.Property(e => e.MembershipPlanId).HasColumnName("membership_plan_id");
            entity.Property(e => e.OrderNo)
                .HasMaxLength(32)
                .HasColumnName("order_no");
            entity.Property(e => e.PaidAt)
                .HasPrecision(3)
                .HasColumnName("paid_at");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(16)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentTransactionId)
                .HasMaxLength(64)
                .HasColumnName("payment_transaction_id");
            entity.Property(e => e.PaymentUrl)
                .HasMaxLength(500)
                .HasColumnName("payment_url");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Status)
                .HasMaxLength(24)
                .HasDefaultValue("created")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Member).WithMany()
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_orders_member");
            entity.HasOne(d => d.Club).WithMany()
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_orders_club");
            entity.HasOne(d => d.CollectingClub).WithMany()
                .HasForeignKey(d => d.CollectingClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_orders_collecting_club");
            entity.HasOne(d => d.MembershipPlan).WithMany()
                .HasForeignKey(d => d.MembershipPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_orders_plan");
            entity.HasOne(d => d.Membership).WithMany()
                .HasForeignKey(d => d.MembershipId)
                .HasConstraintName("FK_membership_orders_membership");
        });
    }
}
