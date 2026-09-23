using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class AdminUser
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Username { get; set; } = null!;

    public Guid? PrimaryClubId { get; set; }

    public string PasswordHash { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Email { get; set; }

    public string Status { get; set; } = null!;

    public bool IsSuperAdmin { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public string? TwoFactorSecretEncrypted { get; set; }

    public DateTime? TwoFactorConfirmedAt { get; set; }

    public int FailedAttemptCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? Locale { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Achievement> AchievementCreatedByNavigations { get; set; } = new List<Achievement>();

    public virtual ICollection<Achievement> AchievementUpdatedByNavigations { get; set; } = new List<Achievement>();

    public virtual ICollection<AdminRefreshToken> AdminRefreshTokens { get; set; } = new List<AdminRefreshToken>();

    public virtual ICollection<AdminRole> AdminRoleCreatedByNavigations { get; set; } = new List<AdminRole>();

    public virtual ICollection<AdminRole> AdminRoleUpdatedByNavigations { get; set; } = new List<AdminRole>();

    public virtual ICollection<AdminUserClub> AdminUserClubAdminUsers { get; set; } = new List<AdminUserClub>();

    public virtual ICollection<AdminUserClub> AdminUserClubGrantedByNavigations { get; set; } = new List<AdminUserClub>();

    public virtual ICollection<AdminUserTeam> AdminUserTeams { get; set; } = new List<AdminUserTeam>();

    public virtual ICollection<ArticleCategory> ArticleCategoryCreatedByNavigations { get; set; } = new List<ArticleCategory>();

    public virtual ICollection<ArticleCategory> ArticleCategoryUpdatedByNavigations { get; set; } = new List<ArticleCategory>();

    public virtual ICollection<Article> ArticleCreatedByNavigations { get; set; } = new List<Article>();

    public virtual ICollection<Article> ArticleUpdatedByNavigations { get; set; } = new List<Article>();

    public virtual ICollection<Banner> BannerCreatedByNavigations { get; set; } = new List<Banner>();

    public virtual ICollection<Banner> BannerUpdatedByNavigations { get; set; } = new List<Banner>();

    public virtual ICollection<CalendarCustomEvent> CalendarCustomEventCreatedByNavigations { get; set; } = new List<CalendarCustomEvent>();

    public virtual ICollection<CalendarCustomEvent> CalendarCustomEventUpdatedByNavigations { get; set; } = new List<CalendarCustomEvent>();

    public virtual ICollection<Cart> CartCreatedByNavigations { get; set; } = new List<Cart>();

    public virtual ICollection<Cart> CartUpdatedByNavigations { get; set; } = new List<Cart>();

    public virtual ICollection<Charity> CharityCreatedByNavigations { get; set; } = new List<Charity>();

    public virtual ICollection<CharityProgram> CharityProgramCreatedByNavigations { get; set; } = new List<CharityProgram>();

    public virtual ICollection<CharityProgramImage> CharityProgramImageCreatedByNavigations { get; set; } = new List<CharityProgramImage>();

    public virtual ICollection<CharityProgramImage> CharityProgramImageUpdatedByNavigations { get; set; } = new List<CharityProgramImage>();

    public virtual ICollection<CharityProgram> CharityProgramUpdatedByNavigations { get; set; } = new List<CharityProgram>();

    public virtual ICollection<Charity> CharityUpdatedByNavigations { get; set; } = new List<Charity>();

    public virtual ICollection<Club> ClubCreatedByNavigations { get; set; } = new List<Club>();

    public virtual ICollection<Club> ClubUpdatedByNavigations { get; set; } = new List<Club>();

    public virtual ICollection<Collection> CollectionCreatedByNavigations { get; set; } = new List<Collection>();

    public virtual ICollection<Collection> CollectionUpdatedByNavigations { get; set; } = new List<Collection>();

    public virtual ICollection<ComicCharacter> ComicCharacterCreatedByNavigations { get; set; } = new List<ComicCharacter>();

    public virtual ICollection<ComicCharacter> ComicCharacterUpdatedByNavigations { get; set; } = new List<ComicCharacter>();

    public virtual ICollection<ComicEpisode> ComicEpisodeCreatedByNavigations { get; set; } = new List<ComicEpisode>();

    public virtual ICollection<ComicEpisode> ComicEpisodeUpdatedByNavigations { get; set; } = new List<ComicEpisode>();

    public virtual ICollection<ComicPage> ComicPageCreatedByNavigations { get; set; } = new List<ComicPage>();

    public virtual ICollection<ComicPage> ComicPageUpdatedByNavigations { get; set; } = new List<ComicPage>();

    public virtual ICollection<Competition> CompetitionCreatedByNavigations { get; set; } = new List<Competition>();

    public virtual ICollection<Competition> CompetitionUpdatedByNavigations { get; set; } = new List<Competition>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DrawRoster> DrawRosterCreatedByNavigations { get; set; } = new List<DrawRoster>();

    public virtual ICollection<DrawRoster> DrawRosterUpdatedByNavigations { get; set; } = new List<DrawRoster>();

    public virtual ICollection<EmailLog> EmailLogCreatedByNavigations { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailLog> EmailLogUpdatedByNavigations { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailTemplate> EmailTemplateCreatedByNavigations { get; set; } = new List<EmailTemplate>();

    public virtual ICollection<EmailTemplate> EmailTemplateUpdatedByNavigations { get; set; } = new List<EmailTemplate>();

    public virtual ICollection<Enquiry> EnquiryAssigneeAdminUsers { get; set; } = new List<Enquiry>();

    public virtual ICollection<Enquiry> EnquiryCreatedByNavigations { get; set; } = new List<Enquiry>();

    public virtual ICollection<Enquiry> EnquiryUpdatedByNavigations { get; set; } = new List<Enquiry>();

    public virtual ICollection<EventType> EventTypeCreatedByNavigations { get; set; } = new List<EventType>();

    public virtual ICollection<EventType> EventTypeUpdatedByNavigations { get; set; } = new List<EventType>();

    public virtual ICollection<FanEvent> FanEventCreatedByNavigations { get; set; } = new List<FanEvent>();

    public virtual ICollection<FanEventRegistration> FanEventRegistrationCreatedByNavigations { get; set; } = new List<FanEventRegistration>();

    public virtual ICollection<FanEventRegistration> FanEventRegistrationUpdatedByNavigations { get; set; } = new List<FanEventRegistration>();

    public virtual ICollection<FanEvent> FanEventUpdatedByNavigations { get; set; } = new List<FanEvent>();

    public virtual ICollection<FaqCategory> FaqCategoryCreatedByNavigations { get; set; } = new List<FaqCategory>();

    public virtual ICollection<FaqCategory> FaqCategoryUpdatedByNavigations { get; set; } = new List<FaqCategory>();

    public virtual ICollection<Faq> FaqCreatedByNavigations { get; set; } = new List<Faq>();

    public virtual ICollection<FaqSearchMiss> FaqSearchMissCreatedByNavigations { get; set; } = new List<FaqSearchMiss>();

    public virtual ICollection<FaqSearchMiss> FaqSearchMissUpdatedByNavigations { get; set; } = new List<FaqSearchMiss>();

    public virtual ICollection<Faq> FaqUpdatedByNavigations { get; set; } = new List<Faq>();

    public virtual ICollection<Form> FormCreatedByNavigations { get; set; } = new List<Form>();

    public virtual ICollection<FormField> FormFieldCreatedByNavigations { get; set; } = new List<FormField>();

    public virtual ICollection<FormField> FormFieldUpdatedByNavigations { get; set; } = new List<FormField>();

    public virtual ICollection<Form> FormUpdatedByNavigations { get; set; } = new List<Form>();

    public virtual ICollection<HomeSection> HomeSectionCreatedByNavigations { get; set; } = new List<HomeSection>();

    public virtual ICollection<HomeSection> HomeSectionUpdatedByNavigations { get; set; } = new List<HomeSection>();

    public virtual ICollection<ImpactMetric> ImpactMetricCreatedByNavigations { get; set; } = new List<ImpactMetric>();

    public virtual ICollection<ImpactMetric> ImpactMetricUpdatedByNavigations { get; set; } = new List<ImpactMetric>();

    public virtual ICollection<ImpactRecord> ImpactRecordCreatedByNavigations { get; set; } = new List<ImpactRecord>();

    public virtual ICollection<ImpactRecordImage> ImpactRecordImageCreatedByNavigations { get; set; } = new List<ImpactRecordImage>();

    public virtual ICollection<ImpactRecordImage> ImpactRecordImageUpdatedByNavigations { get; set; } = new List<ImpactRecordImage>();

    public virtual ICollection<ImpactRecord> ImpactRecordUpdatedByNavigations { get; set; } = new List<ImpactRecord>();

    public virtual ICollection<InventoryMovement> InventoryMovementCreatedByNavigations { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<InventoryMovement> InventoryMovementUpdatedByNavigations { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<AdminUser> InverseCreatedByNavigation { get; set; } = new List<AdminUser>();

    public virtual ICollection<AdminUser> InverseUpdatedByNavigation { get; set; } = new List<AdminUser>();

    public virtual ICollection<InvoiceDonationCode> InvoiceDonationCodeCreatedByNavigations { get; set; } = new List<InvoiceDonationCode>();

    public virtual ICollection<InvoiceDonationCode> InvoiceDonationCodeUpdatedByNavigations { get; set; } = new List<InvoiceDonationCode>();

    public virtual ICollection<JerseyIssue> JerseyIssueCreatedByNavigations { get; set; } = new List<JerseyIssue>();

    public virtual ICollection<JerseyIssue> JerseyIssueUpdatedByNavigations { get; set; } = new List<JerseyIssue>();

    public virtual ICollection<MatchCard> MatchCardCreatedByNavigations { get; set; } = new List<MatchCard>();

    public virtual ICollection<MatchCard> MatchCardUpdatedByNavigations { get; set; } = new List<MatchCard>();

    public virtual ICollection<Match> MatchCreatedByNavigations { get; set; } = new List<Match>();

    public virtual ICollection<MatchGoal> MatchGoalCreatedByNavigations { get; set; } = new List<MatchGoal>();

    public virtual ICollection<MatchGoal> MatchGoalUpdatedByNavigations { get; set; } = new List<MatchGoal>();

    public virtual ICollection<MatchLineup> MatchLineupCreatedByNavigations { get; set; } = new List<MatchLineup>();

    public virtual ICollection<MatchLineup> MatchLineupUpdatedByNavigations { get; set; } = new List<MatchLineup>();

    public virtual ICollection<Match> MatchUpdatedByNavigations { get; set; } = new List<Match>();

    public virtual ICollection<MemberCard> MemberCardCreatedByNavigations { get; set; } = new List<MemberCard>();

    public virtual ICollection<MemberCard> MemberCardUpdatedByNavigations { get; set; } = new List<MemberCard>();

    public virtual ICollection<Member> MemberCreatedByNavigations { get; set; } = new List<Member>();

    public virtual ICollection<MemberDraw> MemberDrawCreatedByNavigations { get; set; } = new List<MemberDraw>();

    public virtual ICollection<MemberDraw> MemberDrawLockedByNavigations { get; set; } = new List<MemberDraw>();

    public virtual ICollection<MemberDraw> MemberDrawUpdatedByNavigations { get; set; } = new List<MemberDraw>();

    public virtual ICollection<Member> MemberUpdatedByNavigations { get; set; } = new List<Member>();

    public virtual ICollection<MembershipBenefit> MembershipBenefitCreatedByNavigations { get; set; } = new List<MembershipBenefit>();

    public virtual ICollection<MembershipBenefit> MembershipBenefitUpdatedByNavigations { get; set; } = new List<MembershipBenefit>();

    public virtual ICollection<Membership> MembershipCreatedByNavigations { get; set; } = new List<Membership>();

    public virtual ICollection<MembershipPayment> MembershipPaymentCreatedByNavigations { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPayment> MembershipPaymentHandledByNavigations { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPayment> MembershipPaymentUpdatedByNavigations { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPlan> MembershipPlanCreatedByNavigations { get; set; } = new List<MembershipPlan>();

    public virtual ICollection<MembershipPlan> MembershipPlanUpdatedByNavigations { get; set; } = new List<MembershipPlan>();

    public virtual ICollection<Membership> MembershipUpdatedByNavigations { get; set; } = new List<Membership>();

    public virtual ICollection<MenuItem> MenuItemCreatedByNavigations { get; set; } = new List<MenuItem>();

    public virtual ICollection<MenuItem> MenuItemUpdatedByNavigations { get; set; } = new List<MenuItem>();

    public virtual ICollection<Milestone> MilestoneCreatedByNavigations { get; set; } = new List<Milestone>();

    public virtual ICollection<Milestone> MilestoneUpdatedByNavigations { get; set; } = new List<Milestone>();

    public virtual ICollection<NewsletterSubscriber> NewsletterSubscriberCreatedByNavigations { get; set; } = new List<NewsletterSubscriber>();

    public virtual ICollection<NewsletterSubscriber> NewsletterSubscriberUpdatedByNavigations { get; set; } = new List<NewsletterSubscriber>();

    public virtual ICollection<Order> OrderCreatedByNavigations { get; set; } = new List<Order>();

    public virtual ICollection<OrderItem> OrderItemCreatedByNavigations { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderItem> OrderItemUpdatedByNavigations { get; set; } = new List<OrderItem>();

    public virtual ICollection<Order> OrderUpdatedByNavigations { get; set; } = new List<Order>();

    public virtual ICollection<PageBlock> PageBlockCreatedByNavigations { get; set; } = new List<PageBlock>();

    public virtual ICollection<PageBlock> PageBlockUpdatedByNavigations { get; set; } = new List<PageBlock>();

    public virtual ICollection<Page> PageCreatedByNavigations { get; set; } = new List<Page>();

    public virtual ICollection<Page> PageUpdatedByNavigations { get; set; } = new List<Page>();

    public virtual ICollection<PageVersion> PageVersionCreatedByNavigations { get; set; } = new List<PageVersion>();

    public virtual ICollection<PageVersion> PageVersionUpdatedByNavigations { get; set; } = new List<PageVersion>();

    public virtual ICollection<Partner> PartnerCreatedByNavigations { get; set; } = new List<Partner>();

    public virtual ICollection<PartnerStore> PartnerStoreCreatedByNavigations { get; set; } = new List<PartnerStore>();

    public virtual ICollection<PartnerStore> PartnerStoreUpdatedByNavigations { get; set; } = new List<PartnerStore>();

    public virtual ICollection<Partner> PartnerUpdatedByNavigations { get; set; } = new List<Partner>();

    public virtual ICollection<PaymentChannel> PaymentChannelCreatedByNavigations { get; set; } = new List<PaymentChannel>();

    public virtual ICollection<PaymentChannel> PaymentChannelUpdatedByNavigations { get; set; } = new List<PaymentChannel>();

    public virtual ICollection<Permission> PermissionCreatedByNavigations { get; set; } = new List<Permission>();

    public virtual ICollection<Permission> PermissionUpdatedByNavigations { get; set; } = new List<Permission>();

    public virtual ICollection<Player> PlayerCreatedByNavigations { get; set; } = new List<Player>();

    public virtual ICollection<PlayerSeasonStat> PlayerSeasonStatCreatedByNavigations { get; set; } = new List<PlayerSeasonStat>();

    public virtual ICollection<PlayerSeasonStat> PlayerSeasonStatUpdatedByNavigations { get; set; } = new List<PlayerSeasonStat>();

    public virtual ICollection<Player> PlayerUpdatedByNavigations { get; set; } = new List<Player>();

    public virtual ICollection<PressResource> PressResourceCreatedByNavigations { get; set; } = new List<PressResource>();

    public virtual ICollection<PressResource> PressResourceUpdatedByNavigations { get; set; } = new List<PressResource>();

    public virtual Club? PrimaryClub { get; set; }

    public virtual ICollection<Product> ProductCreatedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<ProductImage> ProductImageCreatedByNavigations { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductImage> ProductImageUpdatedByNavigations { get; set; } = new List<ProductImage>();

    public virtual ICollection<Product> ProductUpdatedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<ProductVariant> ProductVariantCreatedByNavigations { get; set; } = new List<ProductVariant>();

    public virtual ICollection<ProductVariant> ProductVariantUpdatedByNavigations { get; set; } = new List<ProductVariant>();

    public virtual ICollection<TrainingProgram> ProgramCreatedByNavigations { get; set; } = new List<TrainingProgram>();

    public virtual ICollection<TrainingProgram> ProgramUpdatedByNavigations { get; set; } = new List<TrainingProgram>();

    public virtual ICollection<Proposal> ProposalCreatedByNavigations { get; set; } = new List<Proposal>();

    public virtual ICollection<ProposalFile> ProposalFileCreatedByNavigations { get; set; } = new List<ProposalFile>();

    public virtual ICollection<ProposalFile> ProposalFileUpdatedByNavigations { get; set; } = new List<ProposalFile>();

    public virtual ICollection<Proposal> ProposalUpdatedByNavigations { get; set; } = new List<Proposal>();

    public virtual ICollection<Redirect> RedirectCreatedByNavigations { get; set; } = new List<Redirect>();

    public virtual ICollection<Redirect> RedirectUpdatedByNavigations { get; set; } = new List<Redirect>();

    public virtual ICollection<RefundRequest> RefundRequestApprovedByNavigations { get; set; } = new List<RefundRequest>();

    public virtual ICollection<RefundRequest> RefundRequestCreatedByNavigations { get; set; } = new List<RefundRequest>();

    public virtual ICollection<RefundRequest> RefundRequestUpdatedByNavigations { get; set; } = new List<RefundRequest>();

    public virtual ICollection<Registration> RegistrationCreatedByNavigations { get; set; } = new List<Registration>();

    public virtual ICollection<Registration> RegistrationUpdatedByNavigations { get; set; } = new List<Registration>();

    public virtual ICollection<Season> SeasonCreatedByNavigations { get; set; } = new List<Season>();

    public virtual ICollection<Season> SeasonUpdatedByNavigations { get; set; } = new List<Season>();

    public virtual ICollection<Session> SessionCreatedByNavigations { get; set; } = new List<Session>();

    public virtual ICollection<Session> SessionUpdatedByNavigations { get; set; } = new List<Session>();

    public virtual ICollection<Setting> SettingCreatedByNavigations { get; set; } = new List<Setting>();

    public virtual ICollection<Setting> SettingUpdatedByNavigations { get; set; } = new List<Setting>();

    public virtual ICollection<Shipment> ShipmentCreatedByNavigations { get; set; } = new List<Shipment>();

    public virtual ICollection<Shipment> ShipmentUpdatedByNavigations { get; set; } = new List<Shipment>();

    public virtual ICollection<Sponsor> SponsorCreatedByNavigations { get; set; } = new List<Sponsor>();

    public virtual ICollection<SponsorPackage> SponsorPackageCreatedByNavigations { get; set; } = new List<SponsorPackage>();

    public virtual ICollection<SponsorPackage> SponsorPackageUpdatedByNavigations { get; set; } = new List<SponsorPackage>();

    public virtual ICollection<Sponsor> SponsorUpdatedByNavigations { get; set; } = new List<Sponsor>();

    public virtual ICollection<Staff> StaffCreatedByNavigations { get; set; } = new List<Staff>();

    public virtual ICollection<Staff> StaffUpdatedByNavigations { get; set; } = new List<Staff>();

    public virtual ICollection<Standing> StandingCreatedByNavigations { get; set; } = new List<Standing>();

    public virtual ICollection<Standing> StandingUpdatedByNavigations { get; set; } = new List<Standing>();

    public virtual ICollection<StoreInvoice> StoreInvoiceCreatedByNavigations { get; set; } = new List<StoreInvoice>();

    public virtual ICollection<StoreInvoice> StoreInvoiceUpdatedByNavigations { get; set; } = new List<StoreInvoice>();

    public virtual ICollection<Tag> TagCreatedByNavigations { get; set; } = new List<Tag>();

    public virtual ICollection<Tag> TagUpdatedByNavigations { get; set; } = new List<Tag>();

    public virtual ICollection<Team> TeamCreatedByNavigations { get; set; } = new List<Team>();

    public virtual ICollection<Team> TeamUpdatedByNavigations { get; set; } = new List<Team>();

    public virtual ICollection<Trial> TrialCreatedByNavigations { get; set; } = new List<Trial>();

    public virtual ICollection<Trial> TrialUpdatedByNavigations { get; set; } = new List<Trial>();

    public virtual ICollection<UiString> UiStringCreatedByNavigations { get; set; } = new List<UiString>();

    public virtual ICollection<UiString> UiStringUpdatedByNavigations { get; set; } = new List<UiString>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Venue> VenueCreatedByNavigations { get; set; } = new List<Venue>();

    public virtual ICollection<Venue> VenueUpdatedByNavigations { get; set; } = new List<Venue>();

    public virtual ICollection<AdminRole> AdminRoles { get; set; } = new List<AdminRole>();
}
