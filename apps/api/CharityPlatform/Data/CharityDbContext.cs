using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data.Entities;

namespace Tcrfc.Api.CharityPlatform.Data;

public partial class CharityDbContext : DbContext
{
    public CharityDbContext(DbContextOptions<CharityDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminRefreshToken> AdminRefreshTokens { get; set; }

    public virtual DbSet<AdminRole> AdminRoles { get; set; }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<CharityProgramRef> CharityProgramRefs { get; set; }

    public virtual DbSet<CharityRef> CharityRefs { get; set; }

    public virtual DbSet<Donation> Donations { get; set; }

    public virtual DbSet<DonationAmountOption> DonationAmountOptions { get; set; }

    public virtual DbSet<DonationInvoice> DonationInvoices { get; set; }

    public virtual DbSet<DonationPayment> DonationPayments { get; set; }

    public virtual DbSet<DonationProject> DonationProjects { get; set; }

    public virtual DbSet<DonationProjectsI18n> DonationProjectsI18ns { get; set; }

    public virtual DbSet<DonationStore> DonationStores { get; set; }

    public virtual DbSet<DonationStoresI18n> DonationStoresI18ns { get; set; }

    public virtual DbSet<EmailLog> EmailLogs { get; set; }

    public virtual DbSet<EmailTemplate> EmailTemplates { get; set; }

    public virtual DbSet<EmailTemplatesI18n> EmailTemplatesI18ns { get; set; }

    public virtual DbSet<Locale> Locales { get; set; }

    public virtual DbSet<PaymentChannel> PaymentChannels { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<ReconciliationDiscrepancy> ReconciliationDiscrepancies { get; set; }

    public virtual DbSet<ReconciliationRun> ReconciliationRuns { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<SettingsI18n> SettingsI18ns { get; set; }

    public virtual DbSet<Settlement> Settlements { get; set; }

    public virtual DbSet<SettlementLine> SettlementLines { get; set; }

    public virtual DbSet<UiString> UiStrings { get; set; }

    public virtual DbSet<UiStringTranslation> UiStringTranslations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("admin_refresh_tokens");

            entity.HasIndex(e => e.AdminUserId, "IX_admin_refresh_tokens_user");

            entity.HasIndex(e => e.Seq, "UQ_admin_refresh_tokens_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.TokenHash, "UQ_admin_refresh_tokens_token_hash").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(3)
                .HasColumnName("expires_at");
            entity.Property(e => e.IssuedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("issued_at");
            entity.Property(e => e.ReplacedById).HasColumnName("replaced_by_id");
            entity.Property(e => e.RevokedAt)
                .HasPrecision(3)
                .HasColumnName("revoked_at");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.AdminRefreshTokens)
                .HasForeignKey(d => d.AdminUserId)
                .HasConstraintName("FK_admin_refresh_tokens_user");

            entity.HasOne(d => d.ReplacedBy).WithMany(p => p.InverseReplacedBy)
                .HasForeignKey(d => d.ReplacedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_admin_refresh_tokens_replaced");
        });

        modelBuilder.Entity<AdminRole>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("admin_roles");

            entity.HasIndex(e => e.Code, "UQ_admin_roles_code").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_admin_roles_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(64)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsSystem).HasColumnName("is_system");
            entity.Property(e => e.NameEn)
                .HasMaxLength(50)
                .HasColumnName("name_en");
            entity.Property(e => e.NameZh)
                .HasMaxLength(50)
                .HasColumnName("name_zh");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AdminRoleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_admin_roles_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AdminRoleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_admin_roles_updated_by");
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("admin_users");

            entity.HasIndex(e => e.Seq, "UQ_admin_users_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Username, "UQ_admin_users_username").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .HasColumnName("display_name");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FailedAttemptCount).HasColumnName("failed_attempt_count");
            entity.Property(e => e.IsSuperAdmin).HasColumnName("is_super_admin");
            entity.Property(e => e.LastLoginAt)
                .HasPrecision(3)
                .HasColumnName("last_login_at");
            entity.Property(e => e.LockedUntil)
                .HasPrecision(3)
                .HasColumnName("locked_until");
            entity.Property(e => e.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(e => e.PasswordChangedAt)
                .HasPrecision(3)
                .HasColumnName("password_changed_at");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.TwoFactorConfirmedAt)
                .HasPrecision(3)
                .HasColumnName("two_factor_confirmed_at");
            entity.Property(e => e.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(e => e.TwoFactorSecretEncrypted)
                .HasMaxLength(255)
                .HasColumnName("two_factor_secret_encrypted");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Username)
                .HasMaxLength(191)
                .HasColumnName("username");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_admin_users_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InverseUpdatedByNavigation)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_admin_users_updated_by");

            entity.HasMany(d => d.AdminRoles).WithMany(p => p.AdminUsers)
                .UsingEntity<Dictionary<string, object>>(
                    "AdminUserRole",
                    r => r.HasOne<AdminRole>().WithMany()
                        .HasForeignKey("AdminRoleId")
                        .HasConstraintName("FK_admin_user_roles_role"),
                    l => l.HasOne<AdminUser>().WithMany()
                        .HasForeignKey("AdminUserId")
                        .HasConstraintName("FK_admin_user_roles_user"),
                    j =>
                    {
                        j.HasKey("AdminUserId", "AdminRoleId");
                        j.ToTable("admin_user_roles");
                        j.IndexerProperty<Guid>("AdminUserId").HasColumnName("admin_user_id");
                        j.IndexerProperty<Guid>("AdminRoleId").HasColumnName("admin_role_id");
                    });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("audit_logs");

            entity.HasIndex(e => new { e.AdminUserId, e.OccurredAt }, "IX_audit_logs_admin_user_occurred").IsDescending(false, true);

            entity.HasIndex(e => e.OccurredAt, "IX_audit_logs_occurred_at_desc").IsDescending();

            entity.HasIndex(e => new { e.TargetType, e.TargetId }, "IX_audit_logs_target");

            entity.HasIndex(e => e.Seq, "UQ_audit_logs_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(32)
                .HasColumnName("action");
            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.ChangeSummary)
                .HasMaxLength(500)
                .HasColumnName("change_summary");
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.PurposeNote)
                .HasMaxLength(255)
                .HasColumnName("purpose_note");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.SourceIp)
                .HasMaxLength(45)
                .HasColumnName("source_ip");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(32)
                .HasColumnName("target_type");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.AdminUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_audit_logs_admin_user");
        });

        modelBuilder.Entity<CharityProgramRef>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("charity_program_refs");

            entity.HasIndex(e => e.CharityRefId, "IX_charity_program_refs_charity_ref");

            entity.HasIndex(e => e.RefCode, "UQ_charity_program_refs_code").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_charity_program_refs_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CharityRefId).HasColumnName("charity_ref_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ImportedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("imported_at");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.RefCode)
                .HasMaxLength(32)
                .HasColumnName("ref_code");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CharityRef).WithMany(p => p.CharityProgramRefs)
                .HasForeignKey(d => d.CharityRefId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_charity_program_refs_charity_ref");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CharityProgramRefCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_charity_program_refs_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CharityProgramRefUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_charity_program_refs_updated_by");
        });

        modelBuilder.Entity<CharityRef>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("charity_refs");

            entity.HasIndex(e => e.RefCode, "UQ_charity_refs_code").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_charity_refs_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ImportedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("imported_at");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.RefCode)
                .HasMaxLength(32)
                .HasColumnName("ref_code");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.Source)
                .HasMaxLength(32)
                .HasColumnName("source");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CharityRefCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_charity_refs_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CharityRefUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_charity_refs_updated_by");
        });

        modelBuilder.Entity<Donation>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("donations");

            entity.HasIndex(e => new { e.DonationProjectId, e.PaidAt }, "IX_donations_project_paid_at");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_donations_status_created_at");

            entity.HasIndex(e => new { e.Status, e.PaidAt }, "IX_donations_status_paid_at");

            entity.HasIndex(e => new { e.DonationStoreId, e.PaidAt }, "IX_donations_store_paid_at");

            entity.HasIndex(e => e.OrderNo, "UQ_donations_order_no").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_donations_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.AssociationAmount).HasColumnName("association_amount");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationProjectId).HasColumnName("donation_project_id");
            entity.Property(e => e.DonationStoreId).HasColumnName("donation_store_id");
            entity.Property(e => e.DonorEmail)
                .HasMaxLength(255)
                .HasColumnName("donor_email");
            entity.Property(e => e.DonorName)
                .HasMaxLength(64)
                .HasColumnName("donor_name");
            entity.Property(e => e.InvoiceMode)
                .HasMaxLength(20)
                .HasColumnName("invoice_mode");
            entity.Property(e => e.IsAnonymous).HasColumnName("is_anonymous");
            entity.Property(e => e.IsCreditHidden).HasColumnName("is_credit_hidden");
            entity.Property(e => e.OrderNo)
                .HasMaxLength(32)
                .HasColumnName("order_no");
            entity.Property(e => e.PaidAt)
                .HasPrecision(3)
                .HasColumnName("paid_at");
            entity.Property(e => e.ProjectAmount).HasColumnName("project_amount");
            entity.Property(e => e.ProjectSharePctSnapshot)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("project_share_pct_snapshot");
            entity.Property(e => e.RefundReason)
                .HasMaxLength(255)
                .HasColumnName("refund_reason");
            entity.Property(e => e.RefundedBy).HasColumnName("refunded_by");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("created")
                .HasColumnName("status");
            entity.Property(e => e.StoreAmount).HasColumnName("store_amount");
            entity.Property(e => e.StoreSharePctSnapshot)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("store_share_pct_snapshot");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DonationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_donations_created_by");

            entity.HasOne(d => d.DonationProject).WithMany(p => p.Donations)
                .HasForeignKey(d => d.DonationProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_donations_project");

            entity.HasOne(d => d.DonationStore).WithMany(p => p.Donations)
                .HasForeignKey(d => d.DonationStoreId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_donations_store");

            entity.HasOne(d => d.RefundedByNavigation).WithMany(p => p.DonationRefundedByNavigations)
                .HasForeignKey(d => d.RefundedBy)
                .HasConstraintName("FK_donations_refunded_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DonationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_donations_updated_by");
        });

        modelBuilder.Entity<DonationAmountOption>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("donation_amount_options");

            entity.HasIndex(e => e.DonationProjectId, "IX_donation_amount_options_project");

            entity.HasIndex(e => e.Seq, "UQ_donation_amount_options_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationProjectId).HasColumnName("donation_project_id");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DonationAmountOptionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_donation_amount_options_created_by");

            entity.HasOne(d => d.DonationProject).WithMany(p => p.DonationAmountOptions)
                .HasForeignKey(d => d.DonationProjectId)
                .HasConstraintName("FK_donation_amount_options_project");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DonationAmountOptionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_donation_amount_options_updated_by");
        });

        modelBuilder.Entity<DonationInvoice>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("donation_invoices");

            entity.HasIndex(e => e.DonationId, "IX_donation_invoices_donation_id");

            entity.HasIndex(e => new { e.IssueStatus, e.IssuedAt }, "IX_donation_invoices_issue_status");

            entity.HasIndex(e => e.Seq, "UQ_donation_invoices_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.InvoiceNo, "UX_donation_invoices_invoice_no")
                .IsUnique()
                .HasFilter("([invoice_no] IS NOT NULL)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CarrierIdEncrypted)
                .HasMaxLength(255)
                .HasColumnName("carrier_id_encrypted");
            entity.Property(e => e.CarrierType)
                .HasMaxLength(16)
                .HasColumnName("carrier_type");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationId).HasColumnName("donation_id");
            entity.Property(e => e.InvoiceNo)
                .HasMaxLength(32)
                .HasColumnName("invoice_no");
            entity.Property(e => e.InvoiceTitle)
                .HasMaxLength(128)
                .HasColumnName("invoice_title");
            entity.Property(e => e.InvoiceType)
                .HasMaxLength(20)
                .HasColumnName("invoice_type");
            entity.Property(e => e.IsAnnualSummary).HasColumnName("is_annual_summary");
            entity.Property(e => e.IssueStatus)
                .HasMaxLength(16)
                .HasDefaultValue("pending")
                .HasColumnName("issue_status");
            entity.Property(e => e.IssuedAt)
                .HasPrecision(3)
                .HasColumnName("issued_at");
            entity.Property(e => e.NationalIdEncrypted)
                .HasMaxLength(255)
                .HasColumnName("national_id_encrypted");
            entity.Property(e => e.ReceiptAddress)
                .HasMaxLength(500)
                .HasColumnName("receipt_address");
            entity.Property(e => e.ReceiptTitle)
                .HasMaxLength(128)
                .HasColumnName("receipt_title");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.TaxId)
                .HasMaxLength(16)
                .HasColumnName("tax_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VoidReason)
                .HasMaxLength(255)
                .HasColumnName("void_reason");
            entity.Property(e => e.VoidStatus)
                .HasMaxLength(16)
                .HasDefaultValue("none")
                .HasColumnName("void_status");
            entity.Property(e => e.VoidedBy).HasColumnName("voided_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DonationInvoiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_donation_invoices_created_by");

            entity.HasOne(d => d.Donation).WithMany(p => p.DonationInvoices)
                .HasForeignKey(d => d.DonationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_donation_invoices_donation");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DonationInvoiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_donation_invoices_updated_by");

            entity.HasOne(d => d.VoidedByNavigation).WithMany(p => p.DonationInvoiceVoidedByNavigations)
                .HasForeignKey(d => d.VoidedBy)
                .HasConstraintName("FK_donation_invoices_voided_by");
        });

        modelBuilder.Entity<DonationPayment>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("donation_payments");

            entity.HasIndex(e => e.DonationId, "IX_donation_payments_donation_id");

            entity.HasIndex(e => e.Seq, "UQ_donation_payments_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ConfirmedAt)
                .HasPrecision(3)
                .HasColumnName("confirmed_at");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationId).HasColumnName("donation_id");
            entity.Property(e => e.RawResponse).HasColumnName("raw_response");
            entity.Property(e => e.RequestedAt)
                .HasPrecision(3)
                .HasColumnName("requested_at");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.TransactionId)
                .HasMaxLength(64)
                .HasColumnName("transaction_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DonationPaymentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_donation_payments_created_by");

            entity.HasOne(d => d.Donation).WithMany(p => p.DonationPayments)
                .HasForeignKey(d => d.DonationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_donation_payments_donation");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DonationPaymentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_donation_payments_updated_by");
        });

        modelBuilder.Entity<DonationProject>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("donation_projects");

            entity.HasIndex(e => e.Seq, "UQ_donation_projects_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.ProjectSlug, "UQ_donation_projects_slug").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CharityNameSnapshot)
                .HasMaxLength(128)
                .HasColumnName("charity_name_snapshot");
            entity.Property(e => e.CharityProgramNameSnapshot)
                .HasMaxLength(128)
                .HasColumnName("charity_program_name_snapshot");
            entity.Property(e => e.CharityProgramRefCode)
                .HasMaxLength(32)
                .HasColumnName("charity_program_ref_code");
            entity.Property(e => e.CharityRefCode)
                .HasMaxLength(32)
                .HasColumnName("charity_ref_code");
            entity.Property(e => e.CoverHeight).HasColumnName("cover_height");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CoverWidth).HasColumnName("cover_width");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.InvoiceMode)
                .HasMaxLength(20)
                .HasColumnName("invoice_mode");
            entity.Property(e => e.MaxAmount).HasColumnName("max_amount");
            entity.Property(e => e.MinAmount).HasColumnName("min_amount");
            entity.Property(e => e.ProjectSharePct)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("project_share_pct");
            entity.Property(e => e.ProjectSlug)
                .HasMaxLength(160)
                .HasColumnName("project_slug");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DonationProjectCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_donation_projects_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DonationProjectUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_donation_projects_updated_by");
        });

        modelBuilder.Entity<DonationProjectsI18n>(entity =>
        {
            entity.HasKey(e => new { e.DonationProjectId, e.Locale });

            entity.ToTable("donation_projects_i18n");

            entity.HasIndex(e => e.Locale, "IX_donation_projects_i18n_locale");

            entity.Property(e => e.DonationProjectId).HasColumnName("donation_project_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.CoverAlt)
                .HasMaxLength(255)
                .HasColumnName("cover_alt");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FundUsage).HasColumnName("fund_usage");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.OneLiner)
                .HasMaxLength(255)
                .HasColumnName("one_liner");

            entity.HasOne(d => d.DonationProject).WithMany(p => p.DonationProjectsI18ns)
                .HasForeignKey(d => d.DonationProjectId)
                .HasConstraintName("FK_donation_projects_i18n_project");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.DonationProjectsI18ns)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_donation_projects_i18n_locale");
        });

        modelBuilder.Entity<DonationStore>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("donation_stores");

            entity.HasIndex(e => e.Seq, "UQ_donation_stores_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.StoreSlug, "UQ_donation_stores_slug").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.Category)
                .HasMaxLength(32)
                .HasColumnName("category");
            entity.Property(e => e.ContactName)
                .HasMaxLength(64)
                .HasColumnName("contact_name");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(32)
                .HasColumnName("contact_phone");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndOn).HasColumnName("end_on");
            entity.Property(e => e.LogoHeight).HasColumnName("logo_height");
            entity.Property(e => e.LogoKey)
                .HasMaxLength(500)
                .HasColumnName("logo_key");
            entity.Property(e => e.LogoWidth).HasColumnName("logo_width");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.StartOn).HasColumnName("start_on");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.StoreSharePct)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("store_share_pct");
            entity.Property(e => e.StoreSlug)
                .HasMaxLength(160)
                .HasColumnName("store_slug");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DonationStoreCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_donation_stores_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DonationStoreUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_donation_stores_updated_by");
        });

        modelBuilder.Entity<DonationStoresI18n>(entity =>
        {
            entity.HasKey(e => new { e.DonationStoreId, e.Locale });

            entity.ToTable("donation_stores_i18n");

            entity.HasIndex(e => e.Locale, "IX_donation_stores_i18n_locale");

            entity.Property(e => e.DonationStoreId).HasColumnName("donation_store_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.LogoAlt)
                .HasMaxLength(255)
                .HasColumnName("logo_alt");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.DonationStore).WithMany(p => p.DonationStoresI18ns)
                .HasForeignKey(d => d.DonationStoreId)
                .HasConstraintName("FK_donation_stores_i18n_store");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.DonationStoresI18ns)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_donation_stores_i18n_locale");
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("email_logs");

            entity.HasIndex(e => e.EmailTemplateId, "IX_email_logs_email_template_id");

            entity.HasIndex(e => e.Seq, "UQ_email_logs_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmailTemplateId).HasColumnName("email_template_id");
            entity.Property(e => e.RecipientEmail)
                .HasMaxLength(255)
                .HasColumnName("recipient_email");
            entity.Property(e => e.SentAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("sent_at");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("sent")
                .HasColumnName("status");
            entity.Property(e => e.Type)
                .HasMaxLength(32)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.EmailLogCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_email_logs_created_by");

            entity.HasOne(d => d.EmailTemplate).WithMany(p => p.EmailLogs)
                .HasForeignKey(d => d.EmailTemplateId)
                .HasConstraintName("FK_email_logs_template");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.EmailLogUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_email_logs_updated_by");
        });

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("email_templates");

            entity.HasIndex(e => e.Code, "UQ_email_templates_code").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_email_templates_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(64)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.EmailTemplateCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_email_templates_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.EmailTemplateUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_email_templates_updated_by");
        });

        modelBuilder.Entity<EmailTemplatesI18n>(entity =>
        {
            entity.HasKey(e => new { e.EmailTemplateId, e.Locale });

            entity.ToTable("email_templates_i18n");

            entity.HasIndex(e => e.Locale, "IX_email_templates_i18n_locale");

            entity.Property(e => e.EmailTemplateId).HasColumnName("email_template_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.Subject)
                .HasMaxLength(255)
                .HasColumnName("subject");

            entity.HasOne(d => d.EmailTemplate).WithMany(p => p.EmailTemplatesI18ns)
                .HasForeignKey(d => d.EmailTemplateId)
                .HasConstraintName("FK_email_templates_i18n_template");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.EmailTemplatesI18ns)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_email_templates_i18n_locale");
        });

        modelBuilder.Entity<Locale>(entity =>
        {
            entity.HasKey(e => e.Code);

            entity.ToTable("locales");

            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasColumnName("code");
            entity.Property(e => e.FallbackLocale)
                .HasMaxLength(10)
                .HasColumnName("fallback_locale");
            entity.Property(e => e.IsDefault).HasColumnName("is_default");
            entity.Property(e => e.NameEn)
                .HasMaxLength(50)
                .HasColumnName("name_en");
            entity.Property(e => e.NameZh)
                .HasMaxLength(50)
                .HasColumnName("name_zh");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");

            entity.HasOne(d => d.FallbackLocaleNavigation).WithMany(p => p.InverseFallbackLocaleNavigation)
                .HasForeignKey(d => d.FallbackLocale)
                .HasConstraintName("FK_locales_fallback");
        });

        modelBuilder.Entity<PaymentChannel>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("payment_channels");

            entity.HasIndex(e => e.Seq, "UQ_payment_channels_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => new { e.ChannelType, e.Environment }, "UQ_payment_channels_type_env").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ChannelType)
                .HasMaxLength(20)
                .HasColumnName("channel_type");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CredentialEncrypted)
                .HasMaxLength(500)
                .HasColumnName("credential_encrypted");
            entity.Property(e => e.Environment)
                .HasMaxLength(16)
                .HasColumnName("environment");
            entity.Property(e => e.InvoicePrefix)
                .HasMaxLength(16)
                .HasColumnName("invoice_prefix");
            entity.Property(e => e.RotatedAt)
                .HasPrecision(3)
                .HasColumnName("rotated_at");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PaymentChannelCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_payment_channels_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentChannelUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_payment_channels_updated_by");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("permissions");

            entity.HasIndex(e => e.Code, "UQ_permissions_code").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_permissions_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(16)
                .HasColumnName("action");
            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Domain)
                .HasMaxLength(32)
                .HasColumnName("domain");
            entity.Property(e => e.IsRestricted).HasColumnName("is_restricted");
            entity.Property(e => e.ModuleCode)
                .HasMaxLength(8)
                .HasDefaultValue("N")
                .HasColumnName("module_code");
            entity.Property(e => e.NameEn)
                .HasMaxLength(100)
                .HasColumnName("name_en");
            entity.Property(e => e.NameZh)
                .HasMaxLength(100)
                .HasColumnName("name_zh");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.SubmoduleCode)
                .HasMaxLength(8)
                .HasColumnName("submodule_code");
            entity.Property(e => e.SysadminOnly).HasColumnName("sysadmin_only");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PermissionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_permissions_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PermissionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_permissions_updated_by");
        });

        modelBuilder.Entity<ReconciliationDiscrepancy>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("reconciliation_discrepancies");

            entity.HasIndex(e => new { e.ResolutionStatus, e.ReconciliationRunId }, "IX_reconciliation_discrepancies_status");

            entity.HasIndex(e => e.RowSeq, "UQ_reconciliation_discrepancies_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DiscrepancyType)
                .HasMaxLength(24)
                .HasColumnName("discrepancy_type");
            entity.Property(e => e.DonationId).HasColumnName("donation_id");
            entity.Property(e => e.GatewayAmount).HasColumnName("gateway_amount");
            entity.Property(e => e.GatewayTransactionId)
                .HasMaxLength(64)
                .HasColumnName("gateway_transaction_id");
            entity.Property(e => e.ReconciliationRunId).HasColumnName("reconciliation_run_id");
            entity.Property(e => e.ResolutionStatus)
                .HasMaxLength(16)
                .HasDefaultValue("pending")
                .HasColumnName("resolution_status");
            entity.Property(e => e.ResolveNote)
                .HasMaxLength(255)
                .HasColumnName("resolve_note");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SiteAmount).HasColumnName("site_amount");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Donation).WithMany(p => p.ReconciliationDiscrepancies)
                .HasForeignKey(d => d.DonationId)
                .HasConstraintName("FK_reconciliation_discrepancies_donation");

            entity.HasOne(d => d.ReconciliationRun).WithMany(p => p.ReconciliationDiscrepancies)
                .HasForeignKey(d => d.ReconciliationRunId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reconciliation_discrepancies_run");
        });

        modelBuilder.Entity<ReconciliationRun>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("reconciliation_runs");

            entity.HasIndex(e => e.RunOn, "IX_reconciliation_runs_run_on").IsDescending();

            entity.HasIndex(e => new { e.RunOn, e.Source }, "UQ_reconciliation_runs_run_on").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_reconciliation_runs_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ComparedCount).HasColumnName("compared_count");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DiscrepancyCount).HasColumnName("discrepancy_count");
            entity.Property(e => e.MatchedCount).HasColumnName("matched_count");
            entity.Property(e => e.RanAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("ran_at");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.RunOn).HasColumnName("run_on");
            entity.Property(e => e.Source)
                .HasMaxLength(32)
                .HasDefaultValue("linepay")
                .HasColumnName("source");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.AdminRoleId, e.PermissionId });

            entity.ToTable("role_permissions");

            entity.Property(e => e.AdminRoleId).HasColumnName("admin_role_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.ScopeType)
                .HasMaxLength(32)
                .HasColumnName("scope_type");

            entity.HasOne(d => d.AdminRole).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.AdminRoleId)
                .HasConstraintName("FK_role_permissions_role");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("FK_role_permissions_permission");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("settings");

            entity.HasIndex(e => e.SettingKey, "UQ_settings_key").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_settings_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.SettingKey)
                .HasMaxLength(191)
                .HasColumnName("setting_key");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SettingCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_settings_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SettingUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_settings_updated_by");
        });

        modelBuilder.Entity<SettingsI18n>(entity =>
        {
            entity.HasKey(e => new { e.SettingId, e.Locale });

            entity.ToTable("settings_i18n");

            entity.HasIndex(e => e.Locale, "IX_settings_i18n_locale");

            entity.Property(e => e.SettingId).HasColumnName("setting_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.SettingsI18ns)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_settings_i18n_locale");

            entity.HasOne(d => d.Setting).WithMany(p => p.SettingsI18ns)
                .HasForeignKey(d => d.SettingId)
                .HasConstraintName("FK_settings_i18n_setting");
        });

        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("settlements");

            entity.HasIndex(e => e.Seq, "UQ_settlements_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationCount).HasColumnName("donation_count");
            entity.Property(e => e.DonationTotal).HasColumnName("donation_total");
            entity.Property(e => e.PayableAmount).HasColumnName("payable_amount");
            entity.Property(e => e.PayeeId).HasColumnName("payee_id");
            entity.Property(e => e.PayeeType)
                .HasMaxLength(16)
                .HasColumnName("payee_type");
            entity.Property(e => e.PeriodEnd).HasColumnName("period_end");
            entity.Property(e => e.PeriodStart).HasColumnName("period_start");
            entity.Property(e => e.RemitMethod)
                .HasMaxLength(32)
                .HasColumnName("remit_method");
            entity.Property(e => e.RemitNote)
                .HasMaxLength(255)
                .HasColumnName("remit_note");
            entity.Property(e => e.RemittedOn).HasColumnName("remitted_on");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SettlementCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_settlements_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SettlementUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_settlements_updated_by");
        });

        modelBuilder.Entity<SettlementLine>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("settlement_lines");

            entity.HasIndex(e => e.DonationId, "IX_settlement_lines_donation_id");

            entity.HasIndex(e => e.SettlementId, "IX_settlement_lines_settlement_id");

            entity.HasIndex(e => new { e.SettlementId, e.DonationId, e.IsClawback }, "UQ_settlement_lines_settlement_donation_kind").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_settlement_lines_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClawbackReason)
                .HasMaxLength(255)
                .HasColumnName("clawback_reason");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationId).HasColumnName("donation_id");
            entity.Property(e => e.IsClawback).HasColumnName("is_clawback");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.SettlementId).HasColumnName("settlement_id");
            entity.Property(e => e.ShareAmount).HasColumnName("share_amount");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SettlementLineCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_settlement_lines_created_by");

            entity.HasOne(d => d.Donation).WithMany(p => p.SettlementLines)
                .HasForeignKey(d => d.DonationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_settlement_lines_donation");

            entity.HasOne(d => d.Settlement).WithMany(p => p.SettlementLines)
                .HasForeignKey(d => d.SettlementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_settlement_lines_settlement");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SettlementLineUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_settlement_lines_updated_by");
        });

        modelBuilder.Entity<UiString>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("ui_strings");

            entity.HasIndex(e => e.StringKey, "UQ_ui_strings_key").IsUnique();

            entity.HasIndex(e => e.Seq, "UQ_ui_strings_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.GroupName)
                .HasMaxLength(64)
                .HasColumnName("group_name");
            entity.Property(e => e.Seq)
                .ValueGeneratedOnAdd()
                .HasColumnName("seq");
            entity.Property(e => e.StringKey)
                .HasMaxLength(191)
                .HasColumnName("string_key");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.UiStringCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_ui_strings_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.UiStringUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_ui_strings_updated_by");
        });

        modelBuilder.Entity<UiStringTranslation>(entity =>
        {
            entity.HasKey(e => new { e.UiStringId, e.Locale });

            entity.ToTable("ui_string_translations");

            entity.HasIndex(e => e.Locale, "IX_ui_string_translations_locale");

            entity.Property(e => e.UiStringId).HasColumnName("ui_string_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.UiStringTranslations)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ui_string_translations_locale");

            entity.HasOne(d => d.UiString).WithMany(p => p.UiStringTranslations)
                .HasForeignKey(d => d.UiStringId)
                .HasConstraintName("FK_ui_string_translations_string");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
