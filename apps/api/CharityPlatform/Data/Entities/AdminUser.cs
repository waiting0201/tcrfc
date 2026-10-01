using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class AdminUser
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string DisplayName { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public string? TwoFactorSecretEncrypted { get; set; }

    public DateTime? TwoFactorConfirmedAt { get; set; }

    public int FailedAttemptCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsSuperAdmin { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AdminRefreshToken> AdminRefreshTokens { get; set; } = new List<AdminRefreshToken>();

    public virtual ICollection<AdminRole> AdminRoleCreatedByNavigations { get; set; } = new List<AdminRole>();

    public virtual ICollection<AdminRole> AdminRoleUpdatedByNavigations { get; set; } = new List<AdminRole>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<CharityProgramRef> CharityProgramRefCreatedByNavigations { get; set; } = new List<CharityProgramRef>();

    public virtual ICollection<CharityProgramRef> CharityProgramRefUpdatedByNavigations { get; set; } = new List<CharityProgramRef>();

    public virtual ICollection<CharityRef> CharityRefCreatedByNavigations { get; set; } = new List<CharityRef>();

    public virtual ICollection<CharityRef> CharityRefUpdatedByNavigations { get; set; } = new List<CharityRef>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DonationAmountOption> DonationAmountOptionCreatedByNavigations { get; set; } = new List<DonationAmountOption>();

    public virtual ICollection<DonationAmountOption> DonationAmountOptionUpdatedByNavigations { get; set; } = new List<DonationAmountOption>();

    public virtual ICollection<Donation> DonationCreatedByNavigations { get; set; } = new List<Donation>();

    public virtual ICollection<DonationInvoice> DonationInvoiceCreatedByNavigations { get; set; } = new List<DonationInvoice>();

    public virtual ICollection<DonationInvoice> DonationInvoiceUpdatedByNavigations { get; set; } = new List<DonationInvoice>();

    public virtual ICollection<DonationInvoice> DonationInvoiceVoidedByNavigations { get; set; } = new List<DonationInvoice>();

    public virtual ICollection<DonationPayment> DonationPaymentCreatedByNavigations { get; set; } = new List<DonationPayment>();

    public virtual ICollection<DonationPayment> DonationPaymentUpdatedByNavigations { get; set; } = new List<DonationPayment>();

    public virtual ICollection<DonationProject> DonationProjectCreatedByNavigations { get; set; } = new List<DonationProject>();

    public virtual ICollection<DonationProject> DonationProjectUpdatedByNavigations { get; set; } = new List<DonationProject>();

    public virtual ICollection<Donation> DonationRefundedByNavigations { get; set; } = new List<Donation>();

    public virtual ICollection<DonationStore> DonationStoreCreatedByNavigations { get; set; } = new List<DonationStore>();

    public virtual ICollection<DonationStore> DonationStoreUpdatedByNavigations { get; set; } = new List<DonationStore>();

    public virtual ICollection<Donation> DonationUpdatedByNavigations { get; set; } = new List<Donation>();

    public virtual ICollection<EmailLog> EmailLogCreatedByNavigations { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailLog> EmailLogUpdatedByNavigations { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailTemplate> EmailTemplateCreatedByNavigations { get; set; } = new List<EmailTemplate>();

    public virtual ICollection<EmailTemplate> EmailTemplateUpdatedByNavigations { get; set; } = new List<EmailTemplate>();

    public virtual ICollection<AdminUser> InverseCreatedByNavigation { get; set; } = new List<AdminUser>();

    public virtual ICollection<AdminUser> InverseUpdatedByNavigation { get; set; } = new List<AdminUser>();

    public virtual ICollection<PaymentChannel> PaymentChannelCreatedByNavigations { get; set; } = new List<PaymentChannel>();

    public virtual ICollection<PaymentChannel> PaymentChannelUpdatedByNavigations { get; set; } = new List<PaymentChannel>();

    public virtual ICollection<Permission> PermissionCreatedByNavigations { get; set; } = new List<Permission>();

    public virtual ICollection<Permission> PermissionUpdatedByNavigations { get; set; } = new List<Permission>();

    public virtual ICollection<Setting> SettingCreatedByNavigations { get; set; } = new List<Setting>();

    public virtual ICollection<Setting> SettingUpdatedByNavigations { get; set; } = new List<Setting>();

    public virtual ICollection<Settlement> SettlementCreatedByNavigations { get; set; } = new List<Settlement>();

    public virtual ICollection<SettlementLine> SettlementLineCreatedByNavigations { get; set; } = new List<SettlementLine>();

    public virtual ICollection<SettlementLine> SettlementLineUpdatedByNavigations { get; set; } = new List<SettlementLine>();

    public virtual ICollection<Settlement> SettlementUpdatedByNavigations { get; set; } = new List<Settlement>();

    public virtual ICollection<UiString> UiStringCreatedByNavigations { get; set; } = new List<UiString>();

    public virtual ICollection<UiString> UiStringUpdatedByNavigations { get; set; } = new List<UiString>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<AdminRole> AdminRoles { get; set; } = new List<AdminRole>();
}
