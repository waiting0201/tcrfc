using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Club
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Code { get; set; } = null!;

    public string Domain { get; set; } = null!;

    public string? LogoLightKey { get; set; }

    public string? LogoDarkKey { get; set; }

    public string? FaviconKey { get; set; }

    public string? OgImageKey { get; set; }

    public string? BrandColor { get; set; }

    public string? BrandSecondaryColor { get; set; }

    public string? InvoiceTitle { get; set; }

    public string? TaxId { get; set; }

    public bool IsCollectingSubject { get; set; }

    public string DefaultLocale { get; set; } = null!;

    public int SortOrder { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();

    public virtual ICollection<AdminUserClub> AdminUserClubs { get; set; } = new List<AdminUserClub>();

    public virtual ICollection<AdminUser> AdminUsers { get; set; } = new List<AdminUser>();

    public virtual ICollection<Article> Articles { get; set; } = new List<Article>();

    public virtual ICollection<Banner> Banners { get; set; } = new List<Banner>();

    public virtual ICollection<CalendarCustomEvent> CalendarCustomEvents { get; set; } = new List<CalendarCustomEvent>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<Charity> Charities { get; set; } = new List<Charity>();

    public virtual ICollection<CharityProgram> CharityPrograms { get; set; } = new List<CharityProgram>();

    public virtual ICollection<ClubsI18n> ClubsI18ns { get; set; } = new List<ClubsI18n>();

    public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();

    public virtual ICollection<ComicCharacter> ComicCharacters { get; set; } = new List<ComicCharacter>();

    public virtual ICollection<ComicEpisode> ComicEpisodes { get; set; } = new List<ComicEpisode>();

    public virtual ICollection<Competition> Competitions { get; set; } = new List<Competition>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<DrawRoster> DrawRosters { get; set; } = new List<DrawRoster>();

    public virtual ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();

    public virtual ICollection<EmailTemplate> EmailTemplates { get; set; } = new List<EmailTemplate>();

    public virtual ICollection<Enquiry> Enquiries { get; set; } = new List<Enquiry>();

    public virtual ICollection<FanEventRegistration> FanEventRegistrations { get; set; } = new List<FanEventRegistration>();

    public virtual ICollection<FanEvent> FanEvents { get; set; } = new List<FanEvent>();

    public virtual ICollection<FaqSearchMiss> FaqSearchMisses { get; set; } = new List<FaqSearchMiss>();

    public virtual ICollection<Faq> Faqs { get; set; } = new List<Faq>();

    public virtual ICollection<Form> Forms { get; set; } = new List<Form>();

    public virtual ICollection<HomeSection> HomeSections { get; set; } = new List<HomeSection>();

    public virtual ICollection<ImpactMetric> ImpactMetrics { get; set; } = new List<ImpactMetric>();

    public virtual ICollection<ImpactRecord> ImpactRecords { get; set; } = new List<ImpactRecord>();

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<JerseyIssue> JerseyIssues { get; set; } = new List<JerseyIssue>();

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual ICollection<MemberCard> MemberCards { get; set; } = new List<MemberCard>();

    public virtual ICollection<MemberDraw> MemberDraws { get; set; } = new List<MemberDraw>();

    public virtual ICollection<MembershipPayment> MembershipPaymentClubs { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPayment> MembershipPaymentCollectingClubs { get; set; } = new List<MembershipPayment>();

    public virtual ICollection<MembershipPlan> MembershipPlans { get; set; } = new List<MembershipPlan>();

    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();

    public virtual ICollection<NewsletterSubscriber> NewsletterSubscribers { get; set; } = new List<NewsletterSubscriber>();

    public virtual ICollection<Order> OrderClubs { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderCollectingClubs { get; set; } = new List<Order>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Order> OrderSellingClubs { get; set; } = new List<Order>();

    public virtual ICollection<Page> Pages { get; set; } = new List<Page>();

    public virtual ICollection<PartnerStore> PartnerStores { get; set; } = new List<PartnerStore>();

    public virtual ICollection<Partner> Partners { get; set; } = new List<Partner>();

    public virtual ICollection<PaymentChannel> PaymentChannels { get; set; } = new List<PaymentChannel>();

    public virtual ICollection<Player> Players { get; set; } = new List<Player>();

    public virtual ICollection<PressResource> PressResources { get; set; } = new List<PressResource>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<TrainingProgram> Programs { get; set; } = new List<TrainingProgram>();

    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();

    public virtual ICollection<Redirect> Redirects { get; set; } = new List<Redirect>();

    public virtual ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<Season> Seasons { get; set; } = new List<Season>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual ICollection<Setting> Settings { get; set; } = new List<Setting>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public virtual ICollection<SponsorPackage> SponsorPackages { get; set; } = new List<SponsorPackage>();

    public virtual ICollection<Sponsor> Sponsors { get; set; } = new List<Sponsor>();

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();

    public virtual ICollection<Standing> Standings { get; set; } = new List<Standing>();

    public virtual ICollection<StoreInvoice> StoreInvoices { get; set; } = new List<StoreInvoice>();

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual ICollection<Trial> Trials { get; set; } = new List<Trial>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
