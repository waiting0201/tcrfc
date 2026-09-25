using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Data;

public partial class ClubDbContext : DbContext
{
    public ClubDbContext(DbContextOptions<ClubDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<AdminRefreshToken> AdminRefreshTokens { get; set; }

    public virtual DbSet<AdminRole> AdminRoles { get; set; }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }

    public virtual DbSet<AdminUserClub> AdminUserClubs { get; set; }

    public virtual DbSet<AdminUserTeam> AdminUserTeams { get; set; }

    public virtual DbSet<Article> Articles { get; set; }

    public virtual DbSet<ArticleCategoriesI18n> ArticleCategoriesI18ns { get; set; }

    public virtual DbSet<ArticleCategory> ArticleCategories { get; set; }

    public virtual DbSet<ArticleRelation> ArticleRelations { get; set; }

    public virtual DbSet<ArticlesI18n> ArticlesI18ns { get; set; }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<BannersI18n> BannersI18ns { get; set; }

    public virtual DbSet<CalendarCustomEvent> CalendarCustomEvents { get; set; }

    public virtual DbSet<CalendarCustomEventsI18n> CalendarCustomEventsI18ns { get; set; }

    public virtual DbSet<CalendarEvent> CalendarEvents { get; set; }

    public virtual DbSet<CalendarEventException> CalendarEventExceptions { get; set; }

    public virtual DbSet<CalendarEventTeam> CalendarEventTeams { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<CharitiesI18n> CharitiesI18ns { get; set; }

    public virtual DbSet<Charity> Charities { get; set; }

    public virtual DbSet<CharityProgram> CharityPrograms { get; set; }

    public virtual DbSet<CharityProgramImage> CharityProgramImages { get; set; }

    public virtual DbSet<CharityProgramsI18n> CharityProgramsI18ns { get; set; }

    public virtual DbSet<Club> Clubs { get; set; }

    public virtual DbSet<ClubsI18n> ClubsI18ns { get; set; }

    public virtual DbSet<Collection> Collections { get; set; }

    public virtual DbSet<CollectionsI18n> CollectionsI18ns { get; set; }

    public virtual DbSet<ComicCharacter> ComicCharacters { get; set; }

    public virtual DbSet<ComicCharactersI18n> ComicCharactersI18ns { get; set; }

    public virtual DbSet<ComicEpisode> ComicEpisodes { get; set; }

    public virtual DbSet<ComicEpisodesI18n> ComicEpisodesI18ns { get; set; }

    public virtual DbSet<ComicPage> ComicPages { get; set; }

    public virtual DbSet<Competition> Competitions { get; set; }

    public virtual DbSet<CompetitionsI18n> CompetitionsI18ns { get; set; }

    public virtual DbSet<DrawRoster> DrawRosters { get; set; }

    public virtual DbSet<EmailLog> EmailLogs { get; set; }

    public virtual DbSet<EmailTemplate> EmailTemplates { get; set; }

    public virtual DbSet<EmailTemplatesI18n> EmailTemplatesI18ns { get; set; }

    public virtual DbSet<Enquiry> Enquiries { get; set; }

    public virtual DbSet<EnquiryAnswer> EnquiryAnswers { get; set; }

    public virtual DbSet<EventType> EventTypes { get; set; }

    public virtual DbSet<EventTypesI18n> EventTypesI18ns { get; set; }

    public virtual DbSet<FanEvent> FanEvents { get; set; }

    public virtual DbSet<FanEventRegistration> FanEventRegistrations { get; set; }

    public virtual DbSet<FanEventsI18n> FanEventsI18ns { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<FaqCategoriesI18n> FaqCategoriesI18ns { get; set; }

    public virtual DbSet<FaqCategory> FaqCategories { get; set; }

    public virtual DbSet<FaqEmbedSlot> FaqEmbedSlots { get; set; }

    public virtual DbSet<FaqEmbedSlotLink> FaqEmbedSlotLinks { get; set; }

    public virtual DbSet<FaqSearchMiss> FaqSearchMisses { get; set; }

    public virtual DbSet<FaqsI18n> FaqsI18ns { get; set; }

    public virtual DbSet<Form> Forms { get; set; }

    public virtual DbSet<FormField> FormFields { get; set; }

    public virtual DbSet<FormFieldsI18n> FormFieldsI18ns { get; set; }

    public virtual DbSet<FormsI18n> FormsI18ns { get; set; }

    public virtual DbSet<HomeSection> HomeSections { get; set; }

    public virtual DbSet<ImpactMetric> ImpactMetrics { get; set; }

    public virtual DbSet<ImpactMetricsI18n> ImpactMetricsI18ns { get; set; }

    public virtual DbSet<ImpactRecord> ImpactRecords { get; set; }

    public virtual DbSet<ImpactRecordImage> ImpactRecordImages { get; set; }

    public virtual DbSet<ImpactRecordsI18n> ImpactRecordsI18ns { get; set; }

    public virtual DbSet<InventoryMovement> InventoryMovements { get; set; }

    public virtual DbSet<InvoiceDonationCode> InvoiceDonationCodes { get; set; }

    public virtual DbSet<JerseyIssue> JerseyIssues { get; set; }

    public virtual DbSet<Locale> Locales { get; set; }

    public virtual DbSet<Match> Matches { get; set; }

    public virtual DbSet<MatchCard> MatchCards { get; set; }

    public virtual DbSet<MatchGoal> MatchGoals { get; set; }

    public virtual DbSet<MatchLineup> MatchLineups { get; set; }

    public virtual DbSet<MatchesI18n> MatchesI18ns { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<MemberCard> MemberCards { get; set; }

    public virtual DbSet<MemberDraw> MemberDraws { get; set; }

    public virtual DbSet<MemberDrawsI18n> MemberDrawsI18ns { get; set; }

    public virtual DbSet<Membership> Memberships { get; set; }

    public virtual DbSet<MembershipBenefit> MembershipBenefits { get; set; }

    public virtual DbSet<MembershipBenefitsI18n> MembershipBenefitsI18ns { get; set; }

    public virtual DbSet<MembershipPayment> MembershipPayments { get; set; }

    public virtual DbSet<MembershipPlan> MembershipPlans { get; set; }

    public virtual DbSet<MembershipPlansI18n> MembershipPlansI18ns { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }

    public virtual DbSet<MenuItemsI18n> MenuItemsI18ns { get; set; }

    public virtual DbSet<Milestone> Milestones { get; set; }

    public virtual DbSet<MilestonesI18n> MilestonesI18ns { get; set; }

    public virtual DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Page> Pages { get; set; }

    public virtual DbSet<PageBlock> PageBlocks { get; set; }

    public virtual DbSet<PageVersion> PageVersions { get; set; }

    public virtual DbSet<PagesI18n> PagesI18ns { get; set; }

    public virtual DbSet<Partner> Partners { get; set; }

    public virtual DbSet<PartnerStore> PartnerStores { get; set; }

    public virtual DbSet<PartnerStoresI18n> PartnerStoresI18ns { get; set; }

    public virtual DbSet<PartnersI18n> PartnersI18ns { get; set; }

    public virtual DbSet<PaymentChannel> PaymentChannels { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Player> Players { get; set; }

    public virtual DbSet<PlayerSeasonStat> PlayerSeasonStats { get; set; }

    public virtual DbSet<PlayersI18n> PlayersI18ns { get; set; }

    public virtual DbSet<PressResource> PressResources { get; set; }

    public virtual DbSet<PressResourcesI18n> PressResourcesI18ns { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<ProductsI18n> ProductsI18ns { get; set; }

    public virtual DbSet<TrainingProgram> Programs { get; set; }

    public virtual DbSet<ProgramsI18n> ProgramsI18ns { get; set; }

    public virtual DbSet<Proposal> Proposals { get; set; }

    public virtual DbSet<ProposalFile> ProposalFiles { get; set; }

    public virtual DbSet<Redirect> Redirects { get; set; }

    public virtual DbSet<RefundRequest> RefundRequests { get; set; }

    public virtual DbSet<RefundRequestItem> RefundRequestItems { get; set; }

    public virtual DbSet<Registration> Registrations { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<Season> Seasons { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<SettingsI18n> SettingsI18ns { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<Sponsor> Sponsors { get; set; }

    public virtual DbSet<SponsorPackage> SponsorPackages { get; set; }

    public virtual DbSet<SponsorPackagesI18n> SponsorPackagesI18ns { get; set; }

    public virtual DbSet<SponsorsI18n> SponsorsI18ns { get; set; }

    public virtual DbSet<Staff> Staff { get; set; }

    public virtual DbSet<StaffI18n> StaffI18ns { get; set; }

    public virtual DbSet<StaffTeam> StaffTeams { get; set; }

    public virtual DbSet<Standing> Standings { get; set; }

    public virtual DbSet<StoreInvoice> StoreInvoices { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TagsI18n> TagsI18ns { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamsI18n> TeamsI18ns { get; set; }

    public virtual DbSet<Trial> Trials { get; set; }

    public virtual DbSet<UiString> UiStrings { get; set; }

    public virtual DbSet<UiStringTranslation> UiStringTranslations { get; set; }

    public virtual DbSet<ValueTagLink> ValueTagLinks { get; set; }

    public virtual DbSet<Venue> Venues { get; set; }

    public virtual DbSet<VenuesI18n> VenuesI18ns { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("achievements");

            entity.HasIndex(e => e.RowSeq, "UQ_achievements_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CompetitionName)
                .HasMaxLength(128)
                .HasColumnName("competition_name");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Placing)
                .HasMaxLength(32)
                .HasColumnName("placing");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Year).HasColumnName("year");

            entity.HasOne(d => d.Club).WithMany(p => p.Achievements)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_achievements_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AchievementCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_achievements_created_by");

            entity.HasOne(d => d.Season).WithMany(p => p.Achievements)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_achievements_season");

            entity.HasOne(d => d.Team).WithMany(p => p.Achievements)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_achievements_team");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.AchievementUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_achievements_updated_by");
        });

        modelBuilder.Entity<AdminRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("admin_refresh_tokens");

            entity.HasIndex(e => e.AdminUserId, "IX_admin_refresh_tokens_user");

            entity.HasIndex(e => e.RowSeq, "UQ_admin_refresh_tokens_row_seq")
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
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(128)
                .HasColumnName("token_hash");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.AdminRefreshTokens)
                .HasForeignKey(d => d.AdminUserId)
                .HasConstraintName("FK_admin_refresh_tokens_user");

            entity.HasOne(d => d.ReplacedBy).WithMany(p => p.InverseReplacedBy)
                .HasForeignKey(d => d.ReplacedById)
                .HasConstraintName("FK_admin_refresh_tokens_replaced");
        });

        modelBuilder.Entity<AdminRole>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("admin_roles");

            entity.HasIndex(e => e.Code, "UQ_admin_roles_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_admin_roles_row_seq")
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
                .HasMaxLength(64)
                .HasColumnName("name_en");
            entity.Property(e => e.NameZh)
                .HasMaxLength(64)
                .HasColumnName("name_zh");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ScopeMode)
                .HasMaxLength(16)
                .HasDefaultValue("all_clubs")
                .HasColumnName("scope_mode");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
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

            entity.HasIndex(e => e.RowSeq, "UQ_admin_users_row_seq")
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
                .HasMaxLength(64)
                .HasColumnName("display_name");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.FailedAttemptCount).HasColumnName("failed_attempt_count");
            entity.Property(e => e.IsSuperAdmin).HasColumnName("is_super_admin");
            entity.Property(e => e.LastLoginAt)
                .HasPrecision(3)
                .HasColumnName("last_login_at");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.LockedUntil)
                .HasPrecision(3)
                .HasColumnName("locked_until");
            entity.Property(e => e.MustChangePassword)
                .HasDefaultValue(true)
                .HasColumnName("must_change_password");
            entity.Property(e => e.PasswordChangedAt)
                .HasPrecision(3)
                .HasColumnName("password_changed_at");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.PrimaryClubId).HasColumnName("primary_club_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
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
                .HasMaxLength(64)
                .HasColumnName("username");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_admin_users_created_by");

            entity.HasOne(d => d.PrimaryClub).WithMany(p => p.AdminUsers)
                .HasForeignKey(d => d.PrimaryClubId)
                .HasConstraintName("FK_admin_users_primary_club");

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

        modelBuilder.Entity<AdminUserClub>(entity =>
        {
            entity.HasKey(e => new { e.AdminUserId, e.ClubId });

            entity.ToTable("admin_user_clubs");

            entity.HasIndex(e => new { e.AdminUserId, e.IsActive }, "IX_admin_user_clubs_user_active");

            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.ExpiresOn).HasColumnName("expires_on");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.GrantedOn).HasColumnName("granted_on");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.AdminUserClubAdminUsers)
                .HasForeignKey(d => d.AdminUserId)
                .HasConstraintName("FK_admin_user_clubs_user");

            entity.HasOne(d => d.Club).WithMany(p => p.AdminUserClubs)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_admin_user_clubs_club");

            entity.HasOne(d => d.GrantedByNavigation).WithMany(p => p.AdminUserClubGrantedByNavigations)
                .HasForeignKey(d => d.GrantedBy)
                .HasConstraintName("FK_admin_user_clubs_granted_by");
        });

        modelBuilder.Entity<AdminUserTeam>(entity =>
        {
            entity.HasKey(e => new { e.AdminUserId, e.TeamId });

            entity.ToTable("admin_user_teams");

            entity.Property(e => e.AdminUserId).HasColumnName("admin_user_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.ExpiresOn).HasColumnName("expires_on");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.HasOne(d => d.AdminUser).WithMany(p => p.AdminUserTeams)
                .HasForeignKey(d => d.AdminUserId)
                .HasConstraintName("FK_admin_user_teams_user");

            entity.HasOne(d => d.Team).WithMany(p => p.AdminUserTeams)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_admin_user_teams_team");
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("articles");

            entity.HasIndex(e => new { e.ArticleCategoryId, e.PublishedAt }, "IX_articles_category_published").IsDescending(false, true);

            entity.HasIndex(e => new { e.ClubId, e.Status, e.PublishedAt }, "IX_articles_club_status_published").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.Status, e.PublishedAt }, "IX_articles_status_published").IsDescending(false, true);

            entity.HasIndex(e => e.RowSeq, "UQ_articles_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Slug, "UQ_articles_slug").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ArticleCategoryId).HasColumnName("article_category_id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsFeatured).HasColumnName("is_featured");
            entity.Property(e => e.PublishedAt)
                .HasPrecision(3)
                .HasColumnName("published_at");
            // S1-12（H 單頁 SEO）：canonical_path／is_noindex／is_excluded_from_sitemap。
            entity.Property(e => e.CanonicalPath)
                .HasMaxLength(500)
                .HasColumnName("canonical_path");
            entity.Property(e => e.IsNoindex)
                .HasDefaultValue(false)
                .HasColumnName("is_noindex");
            entity.Property(e => e.IsExcludedFromSitemap)
                .HasDefaultValue(false)
                .HasColumnName("is_excluded_from_sitemap");
            entity.Property(e => e.OgImageKey)
                .HasMaxLength(500)
                .HasColumnName("og_image_key");
            entity.Property(e => e.OgImageWidth).HasColumnName("og_image_width");
            entity.Property(e => e.OgImageHeight).HasColumnName("og_image_height");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.ViewCount).HasColumnName("view_count");

            entity.HasOne(d => d.ArticleCategory).WithMany(p => p.Articles)
                .HasForeignKey(d => d.ArticleCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_articles_category");

            entity.HasOne(d => d.Club).WithMany(p => p.Articles)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_articles_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ArticleCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_articles_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ArticleUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_articles_updated_by");

            entity.HasMany(d => d.Tags).WithMany(p => p.Articles)
                .UsingEntity<Dictionary<string, object>>(
                    "ArticleTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("FK_article_tags_tag"),
                    l => l.HasOne<Article>().WithMany()
                        .HasForeignKey("ArticleId")
                        .HasConstraintName("FK_article_tags_article"),
                    j =>
                    {
                        j.HasKey("ArticleId", "TagId");
                        j.ToTable("article_tags");
                        j.IndexerProperty<Guid>("ArticleId").HasColumnName("article_id");
                        j.IndexerProperty<Guid>("TagId").HasColumnName("tag_id");
                    });
        });

        modelBuilder.Entity<ArticleCategoriesI18n>(entity =>
        {
            entity.HasKey(e => new { e.ArticleCategoryId, e.Locale });

            entity.ToTable("article_categories_i18n");

            entity.HasIndex(e => e.Locale, "IX_article_categories_i18n_locale");

            entity.Property(e => e.ArticleCategoryId).HasColumnName("article_category_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.ArticleCategory).WithMany(p => p.ArticleCategoriesI18ns)
                .HasForeignKey(d => d.ArticleCategoryId)
                .HasConstraintName("FK_article_categories_i18n_cat");
        });

        modelBuilder.Entity<ArticleCategory>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("article_categories");

            entity.HasIndex(e => e.Code, "UQ_article_categories_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_article_categories_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(32)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ArticleCategoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_article_categories_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ArticleCategoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_article_categories_updated_by");
        });

        modelBuilder.Entity<ArticleRelation>(entity =>
        {
            entity.HasKey(e => new { e.ArticleId, e.TargetType, e.TargetId });

            entity.ToTable("article_relations");

            entity.Property(e => e.ArticleId).HasColumnName("article_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(32)
                .HasColumnName("target_type");
            entity.Property(e => e.TargetId).HasColumnName("target_id");

            entity.HasOne(d => d.Article).WithMany(p => p.ArticleRelations)
                .HasForeignKey(d => d.ArticleId)
                .HasConstraintName("FK_article_relations_article");
        });

        modelBuilder.Entity<ArticlesI18n>(entity =>
        {
            entity.HasKey(e => new { e.ArticleId, e.Locale });

            entity.ToTable("articles_i18n");

            entity.HasIndex(e => e.Locale, "IX_articles_i18n_locale");

            entity.Property(e => e.ArticleId).HasColumnName("article_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.SeoDescription)
                .HasMaxLength(300)
                .HasColumnName("seo_description");
            entity.Property(e => e.SeoTitle)
                .HasMaxLength(200)
                .HasColumnName("seo_title");
            entity.Property(e => e.SeoKeywords)
                .HasMaxLength(200)
                .HasColumnName("seo_keywords");
            entity.Property(e => e.OgImageAlt)
                .HasMaxLength(200)
                .HasColumnName("og_image_alt");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Article).WithMany(p => p.ArticlesI18ns)
                .HasForeignKey(d => d.ArticleId)
                .HasConstraintName("FK_articles_i18n_article");
        });

        modelBuilder.Entity<Banner>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("banners");

            entity.HasIndex(e => e.RowSeq, "UQ_banners_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndAt)
                .HasPrecision(3)
                .HasColumnName("end_at");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.ImageWidth).HasColumnName("image_width");
            entity.Property(e => e.ImageHeight).HasColumnName("image_height");
            entity.Property(e => e.MediaType)
                .HasMaxLength(10)
                .HasDefaultValue("image")
                .HasColumnName("media_type");
            entity.Property(e => e.VideoKey)
                .HasMaxLength(500)
                .HasColumnName("video_key");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.StartAt)
                .HasPrecision(3)
                .HasColumnName("start_at");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Banners)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_banners_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BannerCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_banners_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BannerUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_banners_updated_by");
        });

        modelBuilder.Entity<BannersI18n>(entity =>
        {
            entity.HasKey(e => new { e.BannerId, e.Locale });

            entity.ToTable("banners_i18n");

            entity.HasIndex(e => e.Locale, "IX_banners_i18n_locale");

            entity.Property(e => e.BannerId).HasColumnName("banner_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Cta1Label)
                .HasMaxLength(64)
                .HasColumnName("cta_1_label");
            entity.Property(e => e.Cta1Url)
                .HasMaxLength(500)
                .HasColumnName("cta_1_url");
            entity.Property(e => e.Cta2Label)
                .HasMaxLength(64)
                .HasColumnName("cta_2_label");
            entity.Property(e => e.Cta2Url)
                .HasMaxLength(500)
                .HasColumnName("cta_2_url");
            entity.Property(e => e.ImageAlt)
                .HasMaxLength(200)
                .HasColumnName("image_alt");
            entity.Property(e => e.Subtitle)
                .HasMaxLength(300)
                .HasColumnName("subtitle");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Banner).WithMany(p => p.BannersI18ns)
                .HasForeignKey(d => d.BannerId)
                .HasConstraintName("FK_banners_i18n_banner");
        });

        modelBuilder.Entity<CalendarCustomEvent>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("calendar_custom_events");

            entity.HasIndex(e => e.RowSeq, "UQ_calendar_custom_events_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CtaUrl)
                .HasMaxLength(500)
                .HasColumnName("cta_url");
            entity.Property(e => e.EndsAt)
                .HasPrecision(3)
                .HasColumnName("ends_at");
            entity.Property(e => e.EventTypeId).HasColumnName("event_type_id");
            entity.Property(e => e.IsAllDay).HasColumnName("is_all_day");
            entity.Property(e => e.IsPublic)
                .HasDefaultValue(true)
                .HasColumnName("is_public");
            entity.Property(e => e.RepeatRule)
                .HasMaxLength(32)
                .HasColumnName("repeat_rule");
            entity.Property(e => e.RepeatUntil).HasColumnName("repeat_until");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.StartsAt)
                .HasPrecision(3)
                .HasColumnName("starts_at");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VenueId).HasColumnName("venue_id");

            entity.HasOne(d => d.Club).WithMany(p => p.CalendarCustomEvents)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_calendar_custom_events_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CalendarCustomEventCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_calendar_custom_events_created_by");

            entity.HasOne(d => d.EventType).WithMany(p => p.CalendarCustomEvents)
                .HasForeignKey(d => d.EventTypeId)
                .HasConstraintName("FK_calendar_custom_events_event_type");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CalendarCustomEventUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_calendar_custom_events_updated_by");

            entity.HasOne(d => d.Venue).WithMany(p => p.CalendarCustomEvents)
                .HasForeignKey(d => d.VenueId)
                .HasConstraintName("FK_calendar_custom_events_venue");
        });

        modelBuilder.Entity<CalendarCustomEventsI18n>(entity =>
        {
            entity.HasKey(e => new { e.CalendarCustomEventId, e.Locale });

            entity.ToTable("calendar_custom_events_i18n");

            entity.HasIndex(e => e.Locale, "IX_calendar_custom_events_i18n_locale");

            entity.Property(e => e.CalendarCustomEventId).HasColumnName("calendar_custom_event_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Title)
                .HasMaxLength(128)
                .HasColumnName("title");

            entity.HasOne(d => d.CalendarCustomEvent).WithMany(p => p.CalendarCustomEventsI18ns)
                .HasForeignKey(d => d.CalendarCustomEventId)
                .HasConstraintName("FK_calendar_custom_events_i18n_event");
        });

        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("calendar_events");

            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.EventTypeId).HasColumnName("event_type_id");
            entity.Property(e => e.IsAllDay).HasColumnName("is_all_day");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.SourceType)
                .HasMaxLength(6)
                .IsUnicode(false)
                .HasColumnName("source_type");
            entity.Property(e => e.StartsAt)
                .HasPrecision(3)
                .HasColumnName("starts_at");
            entity.Property(e => e.VenueId).HasColumnName("venue_id");
        });

        modelBuilder.Entity<CalendarEventException>(entity =>
        {
            entity.HasKey(e => new { e.CalendarCustomEventId, e.ExcludedOn });

            entity.ToTable("calendar_event_exceptions");

            entity.Property(e => e.CalendarCustomEventId).HasColumnName("calendar_custom_event_id");
            entity.Property(e => e.ExcludedOn).HasColumnName("excluded_on");

            entity.HasOne(d => d.CalendarCustomEvent).WithMany(p => p.CalendarEventExceptions)
                .HasForeignKey(d => d.CalendarCustomEventId)
                .HasConstraintName("FK_calendar_event_exceptions_event");
        });

        modelBuilder.Entity<CalendarEventTeam>(entity =>
        {
            entity.HasKey(e => new { e.SourceType, e.SourceId, e.TeamId });

            entity.ToTable("calendar_event_teams");

            entity.HasIndex(e => new { e.TeamId, e.SourceType }, "IX_calendar_event_teams_team_source");

            entity.Property(e => e.SourceType)
                .HasMaxLength(16)
                .HasColumnName("source_type");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");

            entity.HasOne(d => d.Team).WithMany(p => p.CalendarEventTeams)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_calendar_event_teams_team");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("carts");

            entity.HasIndex(e => e.RowSeq, "UQ_carts_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AnonymousToken)
                .HasMaxLength(64)
                .HasColumnName("anonymous_token");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Carts)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_carts_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CartCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_carts_created_by");

            entity.HasOne(d => d.Member).WithMany(p => p.Carts)
                .HasForeignKey(d => d.MemberId)
                .HasConstraintName("FK_carts_member");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CartUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_carts_updated_by");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => new { e.CartId, e.ProductVariantId });

            entity.ToTable("cart_items");

            entity.Property(e => e.CartId).HasColumnName("cart_id");
            entity.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("FK_cart_items_cart");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductVariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_cart_items_variant");
        });

        modelBuilder.Entity<CharitiesI18n>(entity =>
        {
            entity.HasKey(e => new { e.CharityId, e.Locale });

            entity.ToTable("charities_i18n");

            entity.HasIndex(e => e.Locale, "IX_charities_i18n_locale");

            entity.Property(e => e.CharityId).HasColumnName("charity_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Intro).HasColumnName("intro");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.Charity).WithMany(p => p.CharitiesI18ns)
                .HasForeignKey(d => d.CharityId)
                .HasConstraintName("FK_charities_i18n_charity");
        });

        modelBuilder.Entity<Charity>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("charities");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_charities_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_charities_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_charities_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
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
            entity.Property(e => e.LogoKey)
                .HasMaxLength(500)
                .HasColumnName("logo_key");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(500)
                .HasColumnName("website_url");

            entity.HasOne(d => d.Club).WithMany(p => p.Charities)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_charities_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CharityCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_charities_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CharityUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_charities_updated_by");
        });

        modelBuilder.Entity<CharityProgram>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("charity_programs");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_charity_programs_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_charity_programs_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_charity_programs_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CharityId).HasColumnName("charity_id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndOn).HasColumnName("end_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.StartOn).HasColumnName("start_on");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Charity).WithMany(p => p.CharityPrograms)
                .HasForeignKey(d => d.CharityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_charity_programs_charity");

            entity.HasOne(d => d.Club).WithMany(p => p.CharityPrograms)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_charity_programs_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CharityProgramCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_charity_programs_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CharityProgramUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_charity_programs_updated_by");
        });

        modelBuilder.Entity<CharityProgramImage>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("charity_program_images");

            entity.HasIndex(e => e.RowSeq, "UQ_charity_program_images_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CharityProgramId).HasColumnName("charity_program_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CharityProgram).WithMany(p => p.CharityProgramImages)
                .HasForeignKey(d => d.CharityProgramId)
                .HasConstraintName("FK_charity_program_images_program");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CharityProgramImageCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_charity_program_images_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CharityProgramImageUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_charity_program_images_updated_by");
        });

        modelBuilder.Entity<CharityProgramsI18n>(entity =>
        {
            entity.HasKey(e => new { e.CharityProgramId, e.Locale });

            entity.ToTable("charity_programs_i18n");

            entity.HasIndex(e => e.Locale, "IX_charity_programs_i18n_locale");

            entity.Property(e => e.CharityProgramId).HasColumnName("charity_program_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.DonationContent).HasColumnName("donation_content");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.TargetAudience)
                .HasMaxLength(200)
                .HasColumnName("target_audience");

            entity.HasOne(d => d.CharityProgram).WithMany(p => p.CharityProgramsI18ns)
                .HasForeignKey(d => d.CharityProgramId)
                .HasConstraintName("FK_charity_programs_i18n_program");
        });

        modelBuilder.Entity<Club>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("clubs");

            entity.HasIndex(e => e.Code, "UQ_clubs_code").IsUnique();

            entity.HasIndex(e => e.Domain, "UQ_clubs_domain").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_clubs_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.BrandColor)
                .HasMaxLength(16)
                .HasColumnName("brand_color");
            entity.Property(e => e.BrandSecondaryColor)
                .HasMaxLength(16)
                .HasColumnName("brand_secondary_color");
            entity.Property(e => e.Code)
                .HasMaxLength(16)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DefaultLocale)
                .HasMaxLength(10)
                .HasDefaultValue("zh-Hant")
                .HasColumnName("default_locale");
            entity.Property(e => e.Domain)
                .HasMaxLength(128)
                .HasColumnName("domain");
            entity.Property(e => e.FaviconKey)
                .HasMaxLength(255)
                .HasColumnName("favicon_key");
            entity.Property(e => e.InvoiceTitle)
                .HasMaxLength(64)
                .HasColumnName("invoice_title");
            entity.Property(e => e.IsCollectingSubject)
                .HasDefaultValue(true)
                .HasColumnName("is_collecting_subject");
            entity.Property(e => e.LogoDarkKey)
                .HasMaxLength(255)
                .HasColumnName("logo_dark_key");
            entity.Property(e => e.LogoLightKey)
                .HasMaxLength(255)
                .HasColumnName("logo_light_key");
            entity.Property(e => e.OgImageKey)
                .HasMaxLength(255)
                .HasColumnName("og_image_key");
            entity.Property(e => e.OgImageWidth).HasColumnName("og_image_width");
            entity.Property(e => e.OgImageHeight).HasColumnName("og_image_height");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.TaxId)
                .HasMaxLength(16)
                .HasColumnName("tax_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ClubCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_clubs_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ClubUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_clubs_updated_by");
        });

        modelBuilder.Entity<ClubsI18n>(entity =>
        {
            entity.HasKey(e => new { e.ClubId, e.Locale });

            entity.ToTable("clubs_i18n");

            entity.HasIndex(e => e.Locale, "IX_clubs_i18n_locale");

            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.Club).WithMany(p => p.ClubsI18ns)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_clubs_i18n_club");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.ClubsI18ns)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_clubs_i18n_locale");
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("collections");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_collections_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_collections_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_collections_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
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

            entity.HasOne(d => d.Club).WithMany(p => p.Collections)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_collections_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CollectionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_collections_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CollectionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_collections_updated_by");
        });

        modelBuilder.Entity<CollectionsI18n>(entity =>
        {
            entity.HasKey(e => new { e.CollectionId, e.Locale });

            entity.ToTable("collections_i18n");

            entity.HasIndex(e => e.Locale, "IX_collections_i18n_locale");

            entity.Property(e => e.CollectionId).HasColumnName("collection_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.Narrative).HasColumnName("narrative");

            entity.HasOne(d => d.Collection).WithMany(p => p.CollectionsI18ns)
                .HasForeignKey(d => d.CollectionId)
                .HasConstraintName("FK_collections_i18n_collection");
        });

        modelBuilder.Entity<ComicCharacter>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("comic_characters");

            entity.HasIndex(e => e.RowSeq, "UQ_comic_characters_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.ComicCharacters)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_comic_characters_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ComicCharacterCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_comic_characters_created_by");

            entity.HasOne(d => d.Player).WithMany(p => p.ComicCharacters)
                .HasForeignKey(d => d.PlayerId)
                .HasConstraintName("FK_comic_characters_player");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ComicCharacterUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_comic_characters_updated_by");
        });

        modelBuilder.Entity<ComicCharactersI18n>(entity =>
        {
            entity.HasKey(e => new { e.ComicCharacterId, e.Locale });

            entity.ToTable("comic_characters_i18n");

            entity.HasIndex(e => e.Locale, "IX_comic_characters_i18n_locale");

            entity.Property(e => e.ComicCharacterId).HasColumnName("comic_character_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.ComicCharacter).WithMany(p => p.ComicCharactersI18ns)
                .HasForeignKey(d => d.ComicCharacterId)
                .HasConstraintName("FK_comic_characters_i18n_cc");
        });

        modelBuilder.Entity<ComicEpisode>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("comic_episodes");

            entity.HasIndex(e => e.RowSeq, "UQ_comic_episodes_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EpisodeNo).HasColumnName("episode_no");
            entity.Property(e => e.IsLatest).HasColumnName("is_latest");
            entity.Property(e => e.PublishedOn).HasColumnName("published_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.ViewCount).HasColumnName("view_count");

            entity.HasOne(d => d.Club).WithMany(p => p.ComicEpisodes)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_comic_episodes_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ComicEpisodeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_comic_episodes_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ComicEpisodeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_comic_episodes_updated_by");
        });

        modelBuilder.Entity<ComicEpisodesI18n>(entity =>
        {
            entity.HasKey(e => new { e.ComicEpisodeId, e.Locale });

            entity.ToTable("comic_episodes_i18n");

            entity.HasIndex(e => e.Locale, "IX_comic_episodes_i18n_locale");

            entity.Property(e => e.ComicEpisodeId).HasColumnName("comic_episode_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Title)
                .HasMaxLength(128)
                .HasColumnName("title");

            entity.HasOne(d => d.ComicEpisode).WithMany(p => p.ComicEpisodesI18ns)
                .HasForeignKey(d => d.ComicEpisodeId)
                .HasConstraintName("FK_comic_episodes_i18n_ep");
        });

        modelBuilder.Entity<ComicPage>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("comic_pages");

            entity.HasIndex(e => e.RowSeq, "UQ_comic_pages_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ComicEpisodeId).HasColumnName("comic_episode_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.ComicEpisode).WithMany(p => p.ComicPages)
                .HasForeignKey(d => d.ComicEpisodeId)
                .HasConstraintName("FK_comic_pages_episode");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ComicPageCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_comic_pages_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ComicPageUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_comic_pages_updated_by");
        });

        modelBuilder.Entity<Competition>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("competitions");

            entity.HasIndex(e => new { e.ClubId, e.Code }, "UQ_competitions_club_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_competitions_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Code)
                .HasMaxLength(16)
                .HasColumnName("code");
            entity.Property(e => e.CompType)
                .HasMaxLength(32)
                .HasColumnName("comp_type");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
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

            entity.HasOne(d => d.Club).WithMany(p => p.Competitions)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_competitions_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CompetitionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_competitions_created_by");

            entity.HasOne(d => d.Season).WithMany(p => p.Competitions)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_competitions_season");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CompetitionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_competitions_updated_by");
        });

        modelBuilder.Entity<CompetitionsI18n>(entity =>
        {
            entity.HasKey(e => new { e.CompetitionId, e.Locale });

            entity.ToTable("competitions_i18n");

            entity.HasIndex(e => e.Locale, "IX_competitions_i18n_locale");

            entity.Property(e => e.CompetitionId).HasColumnName("competition_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.Organizer)
                .HasMaxLength(128)
                .HasColumnName("organizer");

            entity.HasOne(d => d.Competition).WithMany(p => p.CompetitionsI18ns)
                .HasForeignKey(d => d.CompetitionId)
                .HasConstraintName("FK_competitions_i18n_comp");

            entity.HasOne(d => d.LocaleNavigation).WithMany(p => p.CompetitionsI18ns)
                .HasForeignKey(d => d.Locale)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_competitions_i18n_locale");
        });

        modelBuilder.Entity<DrawRoster>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("draw_rosters");

            entity.HasIndex(e => new { e.MemberDrawId, e.MemberNoSnapshot }, "UQ_draw_rosters_draw_member_no").IsUnique();

            entity.HasIndex(e => new { e.MemberDrawId, e.SerialNo }, "UQ_draw_rosters_draw_serial").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_draw_rosters_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClaimMethod)
                .HasMaxLength(32)
                .HasColumnName("claim_method");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FulfilmentStatus)
                .HasMaxLength(16)
                .HasColumnName("fulfilment_status");
            entity.Property(e => e.IsWinner).HasColumnName("is_winner");
            entity.Property(e => e.MemberDrawId).HasColumnName("member_draw_id");
            entity.Property(e => e.MemberNoSnapshot)
                .HasMaxLength(32)
                .HasColumnName("member_no_snapshot");
            entity.Property(e => e.MembershipEndOnSnapshot).HasColumnName("membership_end_on_snapshot");
            entity.Property(e => e.NameSnapshot)
                .HasMaxLength(64)
                .HasColumnName("name_snapshot");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.PrizeName)
                .HasMaxLength(128)
                .HasColumnName("prize_name");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SerialNo).HasColumnName("serial_no");
            entity.Property(e => e.TierSnapshot)
                .HasMaxLength(16)
                .HasColumnName("tier_snapshot");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WithholdingDataEncrypted)
                .HasMaxLength(255)
                .HasColumnName("withholding_data_encrypted");

            entity.HasOne(d => d.Club).WithMany(p => p.DrawRosters)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_draw_rosters_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DrawRosterCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_draw_rosters_created_by");

            entity.HasOne(d => d.MemberDraw).WithMany(p => p.DrawRosters)
                .HasForeignKey(d => d.MemberDrawId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_draw_rosters_draw");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DrawRosterUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_draw_rosters_updated_by");
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("email_logs");

            entity.HasIndex(e => new { e.MemberId, e.SentAt }, "IX_email_logs_member_sent").IsDescending(false, true);

            entity.HasIndex(e => new { e.Type, e.SentAt }, "IX_email_logs_type_sent");

            entity.HasIndex(e => e.RowSeq, "UQ_email_logs_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EmailTemplateId).HasColumnName("email_template_id");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SendStatus)
                .HasMaxLength(20)
                .HasColumnName("send_status");
            entity.Property(e => e.SentAt)
                .HasPrecision(3)
                .HasColumnName("sent_at");
            entity.Property(e => e.ToEmail)
                .HasMaxLength(255)
                .HasColumnName("to_email");
            entity.Property(e => e.Type)
                .HasMaxLength(32)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.EmailLogs)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_email_logs_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.EmailLogCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_email_logs_created_by");

            entity.HasOne(d => d.EmailTemplate).WithMany(p => p.EmailLogs)
                .HasForeignKey(d => d.EmailTemplateId)
                .HasConstraintName("FK_email_logs_template");

            entity.HasOne(d => d.Member).WithMany(p => p.EmailLogs)
                .HasForeignKey(d => d.MemberId)
                .HasConstraintName("FK_email_logs_member");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.EmailLogUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_email_logs_updated_by");
        });

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("email_templates");

            entity.HasIndex(e => new { e.ClubId, e.TemplateCode }, "UQ_email_templates_club_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_email_templates_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.TemplateCode)
                .HasMaxLength(32)
                .HasColumnName("template_code");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.EmailTemplates)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_email_templates_club");

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
                .HasMaxLength(200)
                .HasColumnName("subject");

            entity.HasOne(d => d.EmailTemplate).WithMany(p => p.EmailTemplatesI18ns)
                .HasForeignKey(d => d.EmailTemplateId)
                .HasConstraintName("FK_email_templates_i18n_template");
        });

        modelBuilder.Entity<Enquiry>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("enquiries");

            entity.HasIndex(e => e.AssigneeAdminUserId, "IX_enquiries_assignee");

            entity.HasIndex(e => new { e.FormId, e.Status, e.CreatedAt }, "IX_enquiries_form_status_created").IsDescending(false, false, true);

            entity.HasIndex(e => e.RowSeq, "UQ_enquiries_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AssigneeAdminUserId).HasColumnName("assignee_admin_user_id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FormId).HasColumnName("form_id");
            entity.Property(e => e.InternalNote).HasColumnName("internal_note");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SourcePath)
                .HasMaxLength(500)
                .HasColumnName("source_path");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.Tags)
                .HasMaxLength(255)
                .HasColumnName("tags");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UtmCampaign)
                .HasMaxLength(255)
                .HasColumnName("utm_campaign");
            entity.Property(e => e.UtmSource)
                .HasMaxLength(255)
                .HasColumnName("utm_source");

            entity.HasOne(d => d.AssigneeAdminUser).WithMany(p => p.EnquiryAssigneeAdminUsers)
                .HasForeignKey(d => d.AssigneeAdminUserId)
                .HasConstraintName("FK_enquiries_assignee");

            entity.HasOne(d => d.Club).WithMany(p => p.Enquiries)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_enquiries_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.EnquiryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_enquiries_created_by");

            entity.HasOne(d => d.Form).WithMany(p => p.Enquiries)
                .HasForeignKey(d => d.FormId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_enquiries_form");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.EnquiryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_enquiries_updated_by");
        });

        modelBuilder.Entity<EnquiryAnswer>(entity =>
        {
            entity.HasKey(e => new { e.EnquiryId, e.FormFieldId });

            entity.ToTable("enquiry_answers");

            entity.Property(e => e.EnquiryId).HasColumnName("enquiry_id");
            entity.Property(e => e.FormFieldId).HasColumnName("form_field_id");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.Enquiry).WithMany(p => p.EnquiryAnswers)
                .HasForeignKey(d => d.EnquiryId)
                .HasConstraintName("FK_enquiry_answers_enquiry");

            entity.HasOne(d => d.FormField).WithMany(p => p.EnquiryAnswers)
                .HasForeignKey(d => d.FormFieldId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_enquiry_answers_field");
        });

        modelBuilder.Entity<EventType>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("event_types");

            entity.HasIndex(e => e.Code, "UQ_event_types_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_event_types_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(32)
                .HasColumnName("code");
            entity.Property(e => e.Colour)
                .HasMaxLength(16)
                .HasColumnName("colour");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Icon)
                .HasMaxLength(64)
                .HasColumnName("icon");
            entity.Property(e => e.IsPublic)
                .HasDefaultValue(true)
                .HasColumnName("is_public");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.EventTypeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_event_types_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.EventTypeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_event_types_updated_by");
        });

        modelBuilder.Entity<EventTypesI18n>(entity =>
        {
            entity.HasKey(e => new { e.EventTypeId, e.Locale });

            entity.ToTable("event_types_i18n");

            entity.HasIndex(e => e.Locale, "IX_event_types_i18n_locale");

            entity.Property(e => e.EventTypeId).HasColumnName("event_type_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.EventType).WithMany(p => p.EventTypesI18ns)
                .HasForeignKey(d => d.EventTypeId)
                .HasConstraintName("FK_event_types_i18n_type");
        });

        modelBuilder.Entity<FanEvent>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("fan_events");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_fan_events_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_fan_events_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_fan_events_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsPaidMembersOnly).HasColumnName("is_paid_members_only");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.StartsAt)
                .HasPrecision(3)
                .HasColumnName("starts_at");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.FanEvents)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_fan_events_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FanEventCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_fan_events_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FanEventUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_fan_events_updated_by");
        });

        modelBuilder.Entity<FanEventRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("fan_event_registrations");

            entity.HasIndex(e => e.RowSeq, "UQ_fan_event_registrations_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FanEventId).HasColumnName("fan_event_id");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.FanEventRegistrations)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_fan_event_registrations_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FanEventRegistrationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_fan_event_registrations_created_by");

            entity.HasOne(d => d.FanEvent).WithMany(p => p.FanEventRegistrations)
                .HasForeignKey(d => d.FanEventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_fan_event_registrations_event");

            entity.HasOne(d => d.Member).WithMany(p => p.FanEventRegistrations)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_fan_event_registrations_member");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FanEventRegistrationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_fan_event_registrations_updated_by");
        });

        modelBuilder.Entity<FanEventsI18n>(entity =>
        {
            entity.HasKey(e => new { e.FanEventId, e.Locale });

            entity.ToTable("fan_events_i18n");

            entity.HasIndex(e => e.Locale, "IX_fan_events_i18n_locale");

            entity.Property(e => e.FanEventId).HasColumnName("fan_event_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.FanEvent).WithMany(p => p.FanEventsI18ns)
                .HasForeignKey(d => d.FanEventId)
                .HasConstraintName("FK_fan_events_i18n_event");
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("faqs");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_faqs_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_faqs_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_faqs_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HelpfulCount).HasColumnName("helpful_count");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.UnhelpfulCount).HasColumnName("unhelpful_count");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.ViewCount).HasColumnName("view_count");

            entity.HasOne(d => d.Club).WithMany(p => p.Faqs)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_faqs_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FaqCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_faqs_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FaqUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_faqs_updated_by");

            entity.HasMany(d => d.FaqCategories).WithMany(p => p.Faqs)
                .UsingEntity<Dictionary<string, object>>(
                    "FaqCategoryLink",
                    r => r.HasOne<FaqCategory>().WithMany()
                        .HasForeignKey("FaqCategoryId")
                        .HasConstraintName("FK_faq_category_links_cat"),
                    l => l.HasOne<Faq>().WithMany()
                        .HasForeignKey("FaqId")
                        .HasConstraintName("FK_faq_category_links_faq"),
                    j =>
                    {
                        j.HasKey("FaqId", "FaqCategoryId");
                        j.ToTable("faq_category_links");
                        j.IndexerProperty<Guid>("FaqId").HasColumnName("faq_id");
                        j.IndexerProperty<Guid>("FaqCategoryId").HasColumnName("faq_category_id");
                    });
        });

        modelBuilder.Entity<FaqCategoriesI18n>(entity =>
        {
            entity.HasKey(e => new { e.FaqCategoryId, e.Locale });

            entity.ToTable("faq_categories_i18n");

            entity.HasIndex(e => e.Locale, "IX_faq_categories_i18n_locale");

            entity.Property(e => e.FaqCategoryId).HasColumnName("faq_category_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.FaqCategory).WithMany(p => p.FaqCategoriesI18ns)
                .HasForeignKey(d => d.FaqCategoryId)
                .HasConstraintName("FK_faq_categories_i18n_cat");
        });

        modelBuilder.Entity<FaqCategory>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("faq_categories");

            entity.HasIndex(e => e.RowSeq, "UQ_faq_categories_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Slug, "UQ_faq_categories_slug").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("is_enabled");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FaqCategoryCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_faq_categories_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FaqCategoryUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_faq_categories_updated_by");
        });

        modelBuilder.Entity<FaqEmbedSlot>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("faq_embed_slots");

            entity.HasIndex(e => e.RowSeq, "UQ_faq_embed_slots_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Code, "UQ_faq_embed_slots_code").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(64)
                .HasColumnName("code");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FaqEmbedSlotCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_faq_embed_slots_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FaqEmbedSlotUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_faq_embed_slots_updated_by");
        });

        modelBuilder.Entity<FaqEmbedSlotLink>(entity =>
        {
            entity.HasKey(e => new { e.FaqId, e.FaqEmbedSlotId });

            entity.ToTable("faq_embed_slot_links");

            entity.HasIndex(e => e.FaqEmbedSlotId, "IX_faq_embed_slot_links_slot");

            entity.Property(e => e.FaqId).HasColumnName("faq_id");
            entity.Property(e => e.FaqEmbedSlotId).HasColumnName("faq_embed_slot_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");

            entity.HasOne(d => d.Faq).WithMany(p => p.FaqEmbedSlotLinks)
                .HasForeignKey(d => d.FaqId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_faq_embed_slot_links_faq");

            entity.HasOne(d => d.FaqEmbedSlot).WithMany(p => p.FaqEmbedSlotLinks)
                .HasForeignKey(d => d.FaqEmbedSlotId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_faq_embed_slot_links_slot");
        });

        modelBuilder.Entity<FaqSearchMiss>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("faq_search_misses");

            entity.HasIndex(e => new { e.ClubId, e.Keyword }, "UQ_faq_search_misses_club_keyword").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_faq_search_misses_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HitCount)
                .HasDefaultValue(1)
                .HasColumnName("hit_count");
            entity.Property(e => e.Keyword)
                .HasMaxLength(200)
                .HasColumnName("keyword");
            entity.Property(e => e.LastSearchedAt)
                .HasPrecision(3)
                .HasColumnName("last_searched_at");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.FaqSearchMisses)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_faq_search_misses_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FaqSearchMissCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_faq_search_misses_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FaqSearchMissUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_faq_search_misses_updated_by");
        });

        modelBuilder.Entity<FaqsI18n>(entity =>
        {
            entity.HasKey(e => new { e.FaqId, e.Locale });

            entity.ToTable("faqs_i18n");

            entity.HasIndex(e => e.Locale, "IX_faqs_i18n_locale");

            entity.Property(e => e.FaqId).HasColumnName("faq_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Answer).HasColumnName("answer");
            entity.Property(e => e.Question)
                .HasMaxLength(500)
                .HasColumnName("question");

            entity.HasOne(d => d.Faq).WithMany(p => p.FaqsI18ns)
                .HasForeignKey(d => d.FaqId)
                .HasConstraintName("FK_faqs_i18n_faq");
        });

        modelBuilder.Entity<Form>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("forms");

            entity.HasIndex(e => new { e.ClubId, e.FormCode }, "UQ_forms_club_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_forms_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CaptchaEnabled)
                .HasDefaultValue(true)
                .HasColumnName("captcha_enabled");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FormCode)
                .HasMaxLength(32)
                .HasColumnName("form_code");
            entity.Property(e => e.NotifyEmails)
                .HasMaxLength(500)
                .HasColumnName("notify_emails");
            entity.Property(e => e.RedirectPath)
                .HasMaxLength(500)
                .HasColumnName("redirect_path");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Forms)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_forms_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FormCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_forms_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FormUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_forms_updated_by");
        });

        modelBuilder.Entity<FormField>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("form_fields");

            entity.HasIndex(e => e.RowSeq, "UQ_form_fields_row_seq")
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
            entity.Property(e => e.FieldKey)
                .HasMaxLength(64)
                .HasColumnName("field_key");
            entity.Property(e => e.FieldType)
                .HasMaxLength(32)
                .HasColumnName("field_type");
            entity.Property(e => e.FormId).HasColumnName("form_id");
            entity.Property(e => e.IsRequired).HasColumnName("is_required");
            entity.Property(e => e.IsSummary).HasColumnName("is_summary");
            entity.Property(e => e.OptionsJson)
                .HasMaxLength(1000)
                .HasColumnName("options_json");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.ValidationRule)
                .HasMaxLength(255)
                .HasColumnName("validation_rule");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FormFieldCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_form_fields_created_by");

            entity.HasOne(d => d.Form).WithMany(p => p.FormFields)
                .HasForeignKey(d => d.FormId)
                .HasConstraintName("FK_form_fields_form");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FormFieldUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_form_fields_updated_by");
        });

        modelBuilder.Entity<FormsI18n>(entity =>
        {
            entity.HasKey(e => new { e.FormId, e.Locale });

            entity.ToTable("forms_i18n");

            entity.HasIndex(e => e.Locale, "IX_forms_i18n_locale");

            entity.Property(e => e.FormId).HasColumnName("form_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.AutoReplyBody).HasColumnName("auto_reply_body");

            entity.HasOne(d => d.Form).WithMany(p => p.FormsI18ns)
                .HasForeignKey(d => d.FormId)
                .HasConstraintName("FK_forms_i18n_form");
        });

        modelBuilder.Entity<FormFieldsI18n>(entity =>
        {
            entity.HasKey(e => new { e.FormFieldId, e.Locale });

            entity.ToTable("form_fields_i18n");

            entity.HasIndex(e => e.Locale, "IX_form_fields_i18n_locale");

            entity.Property(e => e.FormFieldId).HasColumnName("form_field_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Label)
                .HasMaxLength(255)
                .HasColumnName("label");
            entity.Property(e => e.OptionsJson)
                .HasMaxLength(1000)
                .HasColumnName("options_json");

            entity.HasOne(d => d.FormField).WithMany(p => p.FormFieldsI18ns)
                .HasForeignKey(d => d.FormFieldId)
                .HasConstraintName("FK_form_fields_i18n_field");
        });

        modelBuilder.Entity<HomeSection>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("home_sections");

            entity.HasIndex(e => new { e.ClubId, e.SectionCode }, "UQ_home_sections_club_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_home_sections_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FeaturedBannerId).HasColumnName("featured_banner_id");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("is_enabled");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SectionCode)
                .HasMaxLength(32)
                .HasColumnName("section_code");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.HomeSections)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_home_sections_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.HomeSectionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_home_sections_created_by");

            entity.HasOne(d => d.FeaturedBanner).WithMany(p => p.HomeSections)
                .HasForeignKey(d => d.FeaturedBannerId)
                .HasConstraintName("FK_home_sections_banner");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.HomeSectionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_home_sections_updated_by");
        });

        modelBuilder.Entity<ImpactMetric>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("impact_metrics");

            entity.HasIndex(e => e.RowSeq, "UQ_impact_metrics_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CharityProgramId).HasColumnName("charity_program_id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsPublic).HasColumnName("is_public");
            entity.Property(e => e.MetricKey)
                .HasMaxLength(64)
                .HasColumnName("metric_key");
            entity.Property(e => e.MetricValue).HasColumnName("metric_value");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CharityProgram).WithMany(p => p.ImpactMetrics)
                .HasForeignKey(d => d.CharityProgramId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_impact_metrics_program");

            entity.HasOne(d => d.Club).WithMany(p => p.ImpactMetrics)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_impact_metrics_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ImpactMetricCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_impact_metrics_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ImpactMetricUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_impact_metrics_updated_by");
        });

        modelBuilder.Entity<ImpactMetricsI18n>(entity =>
        {
            entity.HasKey(e => new { e.ImpactMetricId, e.Locale });

            entity.ToTable("impact_metrics_i18n");

            entity.HasIndex(e => e.Locale, "IX_impact_metrics_i18n_locale");

            entity.Property(e => e.ImpactMetricId).HasColumnName("impact_metric_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.ImpactMetric).WithMany(p => p.ImpactMetricsI18ns)
                .HasForeignKey(d => d.ImpactMetricId)
                .HasConstraintName("FK_impact_metrics_i18n_metric");
        });

        modelBuilder.Entity<ImpactRecord>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("impact_records");

            entity.HasIndex(e => e.RowSeq, "UQ_impact_records_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CharityId).HasColumnName("charity_id");
            entity.Property(e => e.CharityProgramId).HasColumnName("charity_program_id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HappenedOn).HasColumnName("happened_on");
            entity.Property(e => e.ImageHeight).HasColumnName("image_height");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.ImageWidth).HasColumnName("image_width");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Charity).WithMany(p => p.ImpactRecords)
                .HasForeignKey(d => d.CharityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_impact_records_charity");

            entity.HasOne(d => d.CharityProgram).WithMany(p => p.ImpactRecords)
                .HasForeignKey(d => d.CharityProgramId)
                .HasConstraintName("FK_impact_records_program");

            entity.HasOne(d => d.Club).WithMany(p => p.ImpactRecords)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_impact_records_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ImpactRecordCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_impact_records_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ImpactRecordUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_impact_records_updated_by");
        });

        modelBuilder.Entity<ImpactRecordImage>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("impact_record_images");

            entity.HasIndex(e => e.RowSeq, "UQ_impact_record_images_row_seq")
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
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.ImpactRecordId).HasColumnName("impact_record_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ImpactRecordImageCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_impact_record_images_created_by");

            entity.HasOne(d => d.ImpactRecord).WithMany(p => p.ImpactRecordImages)
                .HasForeignKey(d => d.ImpactRecordId)
                .HasConstraintName("FK_impact_record_images_record");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ImpactRecordImageUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_impact_record_images_updated_by");
        });

        modelBuilder.Entity<ImpactRecordsI18n>(entity =>
        {
            entity.HasKey(e => new { e.ImpactRecordId, e.Locale });

            entity.ToTable("impact_records_i18n");

            entity.HasIndex(e => e.Locale, "IX_impact_records_i18n_locale");

            entity.Property(e => e.ImpactRecordId).HasColumnName("impact_record_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.BriefDescription).HasColumnName("brief_description");
            entity.Property(e => e.DonationContent).HasColumnName("donation_content");
            entity.Property(e => e.Location)
                .HasMaxLength(128)
                .HasColumnName("location");

            entity.HasOne(d => d.ImpactRecord).WithMany(p => p.ImpactRecordsI18ns)
                .HasForeignKey(d => d.ImpactRecordId)
                .HasConstraintName("FK_impact_records_i18n_record");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("inventory_movements");

            entity.HasIndex(e => new { e.ProductVariantId, e.OccurredAt }, "IX_inventory_movements_variant_occurred").IsDescending(false, true);

            entity.HasIndex(e => e.RowSeq, "UQ_inventory_movements_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HandledBy).HasColumnName("handled_by");
            entity.Property(e => e.MovementType)
                .HasMaxLength(32)
                .HasColumnName("movement_type");
            entity.Property(e => e.OccurredAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("occurred_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inventory_movements_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryMovementCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_inventory_movements_created_by");

            entity.HasOne(d => d.Order).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_inventory_movements_order");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.ProductVariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_inventory_movements_variant");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InventoryMovementUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_inventory_movements_updated_by");
        });

        modelBuilder.Entity<InvoiceDonationCode>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("invoice_donation_codes");

            entity.HasIndex(e => e.Code, "UQ_invoice_donation_codes_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_invoice_donation_codes_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(16)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.OrgName)
                .HasMaxLength(128)
                .HasColumnName("org_name");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InvoiceDonationCodeCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_invoice_donation_codes_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.InvoiceDonationCodeUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_invoice_donation_codes_updated_by");
        });

        modelBuilder.Entity<JerseyIssue>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("jersey_issues");

            entity.HasIndex(e => e.RowSeq, "UQ_jersey_issues_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeliveryMethod)
                .HasMaxLength(32)
                .HasColumnName("delivery_method");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.Phone)
                .HasMaxLength(32)
                .HasColumnName("phone");
            entity.Property(e => e.RecipientName)
                .HasMaxLength(64)
                .HasColumnName("recipient_name");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ShippedOn).HasColumnName("shipped_on");
            entity.Property(e => e.Size)
                .HasMaxLength(16)
                .HasColumnName("size");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.JerseyIssues)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jersey_issues_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.JerseyIssueCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_jersey_issues_created_by");

            entity.HasOne(d => d.Member).WithMany(p => p.JerseyIssues)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_jersey_issues_member");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.JerseyIssueUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_jersey_issues_updated_by");
        });

        modelBuilder.Entity<Locale>(entity =>
        {
            entity.HasKey(e => e.Code);

            entity.ToTable("locales");

            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasColumnName("code");
            entity.Property(e => e.FallbackCode)
                .HasMaxLength(10)
                .HasColumnName("fallback_code");
            entity.Property(e => e.IsDefault).HasColumnName("is_default");
            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true)
                .HasColumnName("is_enabled");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("matches");

            entity.HasIndex(e => new { e.SeasonId, e.MatchOn }, "IX_matches_season_matchon");

            entity.HasIndex(e => new { e.Status, e.MatchOn }, "IX_matches_status_matchon");

            entity.HasIndex(e => e.RowSeq, "UQ_matches_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Competition)
                .HasMaxLength(16)
                .HasColumnName("competition");
            entity.Property(e => e.CompetitionId).HasColumnName("competition_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HomeAway)
                .HasMaxLength(16)
                .HasColumnName("home_away");
            entity.Property(e => e.Kickoff)
                .HasMaxLength(8)
                .HasColumnName("kickoff");
            entity.Property(e => e.MatchNo).HasColumnName("match_no");
            entity.Property(e => e.MatchOn).HasColumnName("match_on");
            entity.Property(e => e.Opponent)
                .HasMaxLength(128)
                .HasColumnName("opponent");
            entity.Property(e => e.OriginalKickoff)
                .HasMaxLength(8)
                .HasColumnName("original_kickoff");
            entity.Property(e => e.OriginalMatchOn).HasColumnName("original_match_on");
            entity.Property(e => e.RoundNo).HasColumnName("round_no");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ScoreAway).HasColumnName("score_away");
            entity.Property(e => e.ScoreHome).HasColumnName("score_home");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VenueId).HasColumnName("venue_id");

            entity.HasOne(d => d.Club).WithMany(p => p.Matches)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_matches_club");

            entity.HasOne(d => d.CompetitionNavigation).WithMany(p => p.Matches)
                .HasForeignKey(d => d.CompetitionId)
                .HasConstraintName("FK_matches_competition");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MatchCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_matches_created_by");

            entity.HasOne(d => d.Season).WithMany(p => p.Matches)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_matches_season");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MatchUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_matches_updated_by");

            entity.HasOne(d => d.Venue).WithMany(p => p.Matches)
                .HasForeignKey(d => d.VenueId)
                .HasConstraintName("FK_matches_venue");

            entity.HasMany(d => d.Teams).WithMany(p => p.Matches)
                .UsingEntity<Dictionary<string, object>>(
                    "MatchTeam",
                    r => r.HasOne<Team>().WithMany()
                        .HasForeignKey("TeamId")
                        .HasConstraintName("FK_match_teams_team"),
                    l => l.HasOne<Match>().WithMany()
                        .HasForeignKey("MatchId")
                        .HasConstraintName("FK_match_teams_match"),
                    j =>
                    {
                        j.HasKey("MatchId", "TeamId");
                        j.ToTable("match_teams");
                        j.IndexerProperty<Guid>("MatchId").HasColumnName("match_id");
                        j.IndexerProperty<Guid>("TeamId").HasColumnName("team_id");
                    });
        });

        modelBuilder.Entity<MatchCard>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("match_cards");

            entity.HasIndex(e => e.RowSeq, "UQ_match_cards_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CardType)
                .HasMaxLength(16)
                .HasColumnName("card_type");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.Minute).HasColumnName("minute");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MatchCardCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_match_cards_created_by");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchCards)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("FK_match_cards_match");

            entity.HasOne(d => d.Player).WithMany(p => p.MatchCards)
                .HasForeignKey(d => d.PlayerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_match_cards_player");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MatchCardUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_match_cards_updated_by");
        });

        modelBuilder.Entity<MatchGoal>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("match_goals");

            entity.HasIndex(e => e.RowSeq, "UQ_match_goals_row_seq")
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
            entity.Property(e => e.GoalType)
                .HasMaxLength(32)
                .HasColumnName("goal_type");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.Minute).HasColumnName("minute");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MatchGoalCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_match_goals_created_by");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchGoals)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("FK_match_goals_match");

            entity.HasOne(d => d.Player).WithMany(p => p.MatchGoals)
                .HasForeignKey(d => d.PlayerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_match_goals_player");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MatchGoalUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_match_goals_updated_by");
        });

        modelBuilder.Entity<MatchLineup>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("match_lineups");

            entity.HasIndex(e => e.RowSeq, "UQ_match_lineups_row_seq")
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
            entity.Property(e => e.IsStarter).HasColumnName("is_starter");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MatchLineupCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_match_lineups_created_by");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchLineups)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("FK_match_lineups_match");

            entity.HasOne(d => d.Player).WithMany(p => p.MatchLineups)
                .HasForeignKey(d => d.PlayerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_match_lineups_player");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MatchLineupUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_match_lineups_updated_by");
        });

        modelBuilder.Entity<MatchesI18n>(entity =>
        {
            entity.HasKey(e => new { e.MatchId, e.Locale });

            entity.ToTable("matches_i18n");

            entity.HasIndex(e => e.Locale, "IX_matches_i18n_locale");

            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Opponent)
                .HasMaxLength(128)
                .HasColumnName("opponent");
            entity.Property(e => e.Venue)
                .HasMaxLength(128)
                .HasColumnName("venue");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchesI18ns)
                .HasForeignKey(d => d.MatchId)
                .HasConstraintName("FK_matches_i18n_match");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("members");

            entity.HasIndex(e => e.Email, "UQ_members_email").IsUnique();

            entity.HasIndex(e => e.MemberNo, "UQ_members_member_no").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_members_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.BirthOn).HasColumnName("birth_on");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.LineUserIdEncrypted)
                .HasMaxLength(255)
                .HasColumnName("line_user_id_encrypted");
            entity.Property(e => e.MemberNo)
                .HasMaxLength(32)
                .HasColumnName("member_no");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(32)
                .HasColumnName("phone");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SignupSource)
                .HasMaxLength(16)
                .HasColumnName("signup_source");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MemberCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_members_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MemberUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_members_updated_by");
        });

        modelBuilder.Entity<MemberCard>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("member_cards");

            entity.HasIndex(e => e.RowSeq, "UQ_member_cards_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Token, "UQ_member_cards_token").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HolderName)
                .HasMaxLength(64)
                .HasColumnName("holder_name");
            entity.Property(e => e.IssuedAt)
                .HasPrecision(3)
                .HasColumnName("issued_at");
            entity.Property(e => e.MembershipId).HasColumnName("membership_id");
            entity.Property(e => e.ReissueCount).HasColumnName("reissue_count");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.Token)
                .HasMaxLength(64)
                .HasColumnName("token");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.MemberCards)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_member_cards_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MemberCardCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_member_cards_created_by");

            entity.HasOne(d => d.Membership).WithMany(p => p.MemberCards)
                .HasForeignKey(d => d.MembershipId)
                .HasConstraintName("FK_member_cards_membership");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MemberCardUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_member_cards_updated_by");
        });

        modelBuilder.Entity<MemberDraw>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("member_draws");

            entity.HasIndex(e => new { e.ClubId, e.DrawCode }, "UQ_member_draws_club_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_member_draws_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AnnouncementArticleId).HasColumnName("announcement_article_id");
            entity.Property(e => e.ClaimDeadlineOn).HasColumnName("claim_deadline_on");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DrawCode)
                .HasMaxLength(32)
                .HasColumnName("draw_code");
            entity.Property(e => e.DrawOccasion)
                .HasMaxLength(32)
                .HasColumnName("draw_occasion");
            entity.Property(e => e.DrawnAt)
                .HasPrecision(3)
                .HasColumnName("drawn_at");
            entity.Property(e => e.LockedAt)
                .HasPrecision(3)
                .HasColumnName("locked_at");
            entity.Property(e => e.LockedBy).HasColumnName("locked_by");
            entity.Property(e => e.RosterHash)
                .HasMaxLength(64)
                .HasColumnName("roster_hash");
            entity.Property(e => e.RosterVersion)
                .HasDefaultValue(1)
                .HasColumnName("roster_version");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SnapshotAt)
                .HasPrecision(3)
                .HasColumnName("snapshot_at");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.TotalCount).HasColumnName("total_count");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.AnnouncementArticle).WithMany(p => p.MemberDraws)
                .HasForeignKey(d => d.AnnouncementArticleId)
                .HasConstraintName("FK_member_draws_article");

            entity.HasOne(d => d.Club).WithMany(p => p.MemberDraws)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_member_draws_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MemberDrawCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_member_draws_created_by");

            entity.HasOne(d => d.LockedByNavigation).WithMany(p => p.MemberDrawLockedByNavigations)
                .HasForeignKey(d => d.LockedBy)
                .HasConstraintName("FK_member_draws_locked_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MemberDrawUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_member_draws_updated_by");
        });

        modelBuilder.Entity<MemberDrawsI18n>(entity =>
        {
            entity.HasKey(e => new { e.MemberDrawId, e.Locale });

            entity.ToTable("member_draws_i18n");

            entity.HasIndex(e => e.Locale, "IX_member_draws_i18n_locale");

            entity.Property(e => e.MemberDrawId).HasColumnName("member_draw_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.PrizeDescription).HasColumnName("prize_description");
            entity.Property(e => e.Rules).HasColumnName("rules");

            entity.HasOne(d => d.MemberDraw).WithMany(p => p.MemberDrawsI18ns)
                .HasForeignKey(d => d.MemberDrawId)
                .HasConstraintName("FK_member_draws_i18n_draw");
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("memberships");

            entity.HasIndex(e => new { e.ClubId, e.Status, e.MembershipEndOn }, "IX_memberships_club_status_end");

            entity.HasIndex(e => e.MemberId, "IX_memberships_member");

            entity.HasIndex(e => new { e.MemberId, e.ClubId, e.SeasonId }, "UQ_memberships_member_club_season").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_memberships_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.MembershipEndOn).HasColumnName("membership_end_on");
            entity.Property(e => e.MembershipStartOn).HasColumnName("membership_start_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.Tier)
                .HasMaxLength(16)
                .HasColumnName("tier");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Memberships)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_memberships_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MembershipCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_memberships_created_by");

            entity.HasOne(d => d.Member).WithMany(p => p.Memberships)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_memberships_member");

            entity.HasOne(d => d.Season).WithMany(p => p.Memberships)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_memberships_season");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MembershipUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_memberships_updated_by");
        });

        modelBuilder.Entity<MembershipBenefit>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("membership_benefits");

            entity.HasIndex(e => e.RowSeq, "UQ_membership_benefits_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.BenefitGroup)
                .HasMaxLength(64)
                .HasColumnName("benefit_group");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.MembershipPlanId).HasColumnName("membership_plan_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MembershipBenefitCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_membership_benefits_created_by");

            entity.HasOne(d => d.MembershipPlan).WithMany(p => p.MembershipBenefits)
                .HasForeignKey(d => d.MembershipPlanId)
                .HasConstraintName("FK_membership_benefits_plan");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MembershipBenefitUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_membership_benefits_updated_by");
        });

        modelBuilder.Entity<MembershipBenefitsI18n>(entity =>
        {
            entity.HasKey(e => new { e.MembershipBenefitId, e.Locale });

            entity.ToTable("membership_benefits_i18n");

            entity.HasIndex(e => e.Locale, "IX_membership_benefits_i18n_locale");

            entity.Property(e => e.MembershipBenefitId).HasColumnName("membership_benefit_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.FreeValue)
                .HasMaxLength(255)
                .HasColumnName("free_value");
            entity.Property(e => e.GroupLabel)
                .HasMaxLength(64)
                .HasColumnName("group_label");
            entity.Property(e => e.PaidValue)
                .HasMaxLength(255)
                .HasColumnName("paid_value");

            entity.HasOne(d => d.MembershipBenefit).WithMany(p => p.MembershipBenefitsI18ns)
                .HasForeignKey(d => d.MembershipBenefitId)
                .HasConstraintName("FK_membership_benefits_i18n_benefit");
        });

        modelBuilder.Entity<MembershipPayment>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("membership_payments");

            entity.HasIndex(e => e.RowSeq, "UQ_membership_payments_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ActivatedEndOn).HasColumnName("activated_end_on");
            entity.Property(e => e.ActivatedStartOn).HasColumnName("activated_start_on");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CollectingClubId).HasColumnName("collecting_club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HandledBy).HasColumnName("handled_by");
            entity.Property(e => e.MembershipId).HasColumnName("membership_id");
            entity.Property(e => e.MembershipPlanId).HasColumnName("membership_plan_id");
            entity.Property(e => e.Method)
                .HasMaxLength(32)
                .HasColumnName("method");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasColumnName("note");
            entity.Property(e => e.PaidOn).HasColumnName("paid_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.MembershipPaymentClubs)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_payments_club");

            entity.HasOne(d => d.CollectingClub).WithMany(p => p.MembershipPaymentCollectingClubs)
                .HasForeignKey(d => d.CollectingClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_payments_collecting_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MembershipPaymentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_membership_payments_created_by");

            entity.HasOne(d => d.HandledByNavigation).WithMany(p => p.MembershipPaymentHandledByNavigations)
                .HasForeignKey(d => d.HandledBy)
                .HasConstraintName("FK_membership_payments_handled_by");

            entity.HasOne(d => d.Membership).WithMany(p => p.MembershipPayments)
                .HasForeignKey(d => d.MembershipId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_payments_membership");

            entity.HasOne(d => d.MembershipPlan).WithMany(p => p.MembershipPayments)
                .HasForeignKey(d => d.MembershipPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_payments_plan");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MembershipPaymentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_membership_payments_updated_by");
        });

        modelBuilder.Entity<MembershipPlan>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("membership_plans");

            entity.HasIndex(e => new { e.ClubId, e.SeasonId, e.Code }, "UQ_membership_plans_club_season_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_membership_plans_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CardQuota)
                .HasDefaultValue(1)
                .HasColumnName("card_quota");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Code)
                .HasMaxLength(32)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Fee).HasColumnName("fee");
            entity.Property(e => e.JerseyQuota).HasColumnName("jersey_quota");
            entity.Property(e => e.MidSeasonRule)
                .HasMaxLength(255)
                .HasColumnName("mid_season_rule");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.MembershipPlans)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_plans_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MembershipPlanCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_membership_plans_created_by");

            entity.HasOne(d => d.Season).WithMany(p => p.MembershipPlans)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_membership_plans_season");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MembershipPlanUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_membership_plans_updated_by");
        });

        modelBuilder.Entity<MembershipPlansI18n>(entity =>
        {
            entity.HasKey(e => new { e.MembershipPlanId, e.Locale });

            entity.ToTable("membership_plans_i18n");

            entity.HasIndex(e => e.Locale, "IX_membership_plans_i18n_locale");

            entity.Property(e => e.MembershipPlanId).HasColumnName("membership_plan_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.BenefitNote).HasColumnName("benefit_note");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.MembershipPlan).WithMany(p => p.MembershipPlansI18ns)
                .HasForeignKey(d => d.MembershipPlanId)
                .HasConstraintName("FK_membership_plans_i18n_plan");
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("menu_items");

            entity.HasIndex(e => e.RowSeq, "UQ_menu_items_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsExternal).HasColumnName("is_external");
            entity.Property(e => e.MenuLocation)
                .HasMaxLength(16)
                .HasColumnName("menu_location");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .HasColumnName("url");

            entity.HasOne(d => d.Club).WithMany(p => p.MenuItems)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_menu_items_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MenuItemCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_menu_items_created_by");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK_menu_items_parent");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MenuItemUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_menu_items_updated_by");
        });

        modelBuilder.Entity<MenuItemsI18n>(entity =>
        {
            entity.HasKey(e => new { e.MenuItemId, e.Locale });

            entity.ToTable("menu_items_i18n");

            entity.HasIndex(e => e.Locale, "IX_menu_items_i18n_locale");

            entity.Property(e => e.MenuItemId).HasColumnName("menu_item_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Label)
                .HasMaxLength(64)
                .HasColumnName("label");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.MenuItemsI18ns)
                .HasForeignKey(d => d.MenuItemId)
                .HasConstraintName("FK_menu_items_i18n_item");
        });

        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("milestones");

            entity.HasIndex(e => e.RowSeq, "UQ_milestones_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HappenedOn).HasColumnName("happened_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Milestones)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_milestones_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.MilestoneCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_milestones_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.MilestoneUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_milestones_updated_by");
        });

        modelBuilder.Entity<MilestonesI18n>(entity =>
        {
            entity.HasKey(e => new { e.MilestoneId, e.Locale });

            entity.ToTable("milestones_i18n");

            entity.HasIndex(e => e.Locale, "IX_milestones_i18n_locale");

            entity.Property(e => e.MilestoneId).HasColumnName("milestone_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Milestone).WithMany(p => p.MilestonesI18ns)
                .HasForeignKey(d => d.MilestoneId)
                .HasConstraintName("FK_milestones_i18n_ms");
        });

        modelBuilder.Entity<NewsletterSubscriber>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("newsletter_subscribers");

            entity.HasIndex(e => new { e.ClubId, e.Email }, "UQ_newsletter_subscribers_club_email").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_newsletter_subscribers_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Source)
                .HasMaxLength(64)
                .HasColumnName("source");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.SubscribedAt)
                .HasPrecision(3)
                .HasColumnName("subscribed_at");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.NewsletterSubscribers)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_newsletter_subscribers_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.NewsletterSubscriberCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_newsletter_subscribers_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.NewsletterSubscriberUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_newsletter_subscribers_updated_by");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("orders");

            entity.HasIndex(e => new { e.ClubId, e.CreatedAt }, "IX_orders_club_created").IsDescending(false, true);

            entity.HasIndex(e => new { e.MemberId, e.CreatedAt }, "IX_orders_member_created").IsDescending(false, true);

            entity.HasIndex(e => e.OrderStatus, "IX_orders_order_status");

            entity.HasIndex(e => new { e.PaymentStatus, e.CreatedAt }, "IX_orders_payment_status_created");

            entity.HasIndex(e => e.LookupToken, "UQ_orders_lookup_token").IsUnique();

            entity.HasIndex(e => e.OrderNo, "UQ_orders_order_no").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_orders_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CollectingClubId).HasColumnName("collecting_club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeliveryMethod)
                .HasMaxLength(16)
                .HasColumnName("delivery_method");
            entity.Property(e => e.IsManual).HasColumnName("is_manual");
            entity.Property(e => e.LinepayTransactionId)
                .HasMaxLength(64)
                .HasColumnName("linepay_transaction_id");
            entity.Property(e => e.LookupToken)
                .HasMaxLength(64)
                .HasColumnName("lookup_token");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.OrderNo)
                .HasMaxLength(32)
                .HasColumnName("order_no");
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(16)
                .HasDefaultValue("待付款")
                .HasColumnName("order_status");
            entity.Property(e => e.PaidAt)
                .HasPrecision(3)
                .HasColumnName("paid_at");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(16)
                .HasDefaultValue("pending")
                .HasColumnName("payment_status");
            entity.Property(e => e.RecipientAddress)
                .HasMaxLength(500)
                .HasColumnName("recipient_address");
            entity.Property(e => e.RecipientName)
                .HasMaxLength(64)
                .HasColumnName("recipient_name");
            entity.Property(e => e.RecipientPhone)
                .HasMaxLength(32)
                .HasColumnName("recipient_phone");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SellingClubId).HasColumnName("selling_club_id");
            entity.Property(e => e.ShippingFee).HasColumnName("shipping_fee");
            entity.Property(e => e.Subtotal).HasColumnName("subtotal");
            entity.Property(e => e.Total).HasColumnName("total");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.OrderClubs)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_orders_club");

            entity.HasOne(d => d.CollectingClub).WithMany(p => p.OrderCollectingClubs)
                .HasForeignKey(d => d.CollectingClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_orders_collecting_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.OrderCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_orders_created_by");

            entity.HasOne(d => d.Member).WithMany(p => p.Orders)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_orders_member");

            entity.HasOne(d => d.SellingClub).WithMany(p => p.OrderSellingClubs)
                .HasForeignKey(d => d.SellingClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_orders_selling_club");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.OrderUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_orders_updated_by");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("order_items");

            entity.HasIndex(e => e.OrderId, "IX_order_items_order");

            entity.HasIndex(e => e.RowSeq, "UQ_order_items_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.LineTotal).HasColumnName("line_total");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductNameSnapshot)
                .HasMaxLength(200)
                .HasColumnName("product_name_snapshot");
            entity.Property(e => e.ProductVariantId).HasColumnName("product_variant_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SkuSnapshot)
                .HasMaxLength(64)
                .HasColumnName("sku_snapshot");
            entity.Property(e => e.UnitPriceSnapshot).HasColumnName("unit_price_snapshot");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VariantLabelSnapshot)
                .HasMaxLength(64)
                .HasColumnName("variant_label_snapshot");

            entity.HasOne(d => d.Club).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_order_items_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.OrderItemCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_order_items_created_by");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_order_items_order");

            entity.HasOne(d => d.ProductVariant).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductVariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_order_items_variant");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.OrderItemUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_order_items_updated_by");
        });

        modelBuilder.Entity<Page>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("pages");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_pages_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_pages_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_pages_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PublishedAt)
                .HasPrecision(3)
                .HasColumnName("published_at");
            // S1-12（H 單頁 SEO）：canonical_path／is_noindex／is_excluded_from_sitemap。
            entity.Property(e => e.CanonicalPath)
                .HasMaxLength(500)
                .HasColumnName("canonical_path");
            entity.Property(e => e.IsNoindex)
                .HasDefaultValue(false)
                .HasColumnName("is_noindex");
            entity.Property(e => e.IsExcludedFromSitemap)
                .HasDefaultValue(false)
                .HasColumnName("is_excluded_from_sitemap");
            entity.Property(e => e.OgImageKey)
                .HasMaxLength(500)
                .HasColumnName("og_image_key");
            entity.Property(e => e.OgImageWidth).HasColumnName("og_image_width");
            entity.Property(e => e.OgImageHeight).HasColumnName("og_image_height");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("draft")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Pages)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_pages_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PageCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_pages_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PageUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_pages_updated_by");
        });

        modelBuilder.Entity<PageBlock>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("page_blocks");

            entity.HasIndex(e => e.RowSeq, "UQ_page_blocks_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.BlockType)
                .HasMaxLength(32)
                .HasColumnName("block_type");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PageBlockCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_page_blocks_created_by");

            entity.HasOne(d => d.Page).WithMany(p => p.PageBlocks)
                .HasForeignKey(d => d.PageId)
                .HasConstraintName("FK_page_blocks_page");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PageBlockUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_page_blocks_updated_by");
        });

        modelBuilder.Entity<PageVersion>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("page_versions");

            entity.HasIndex(e => e.RowSeq, "UQ_page_versions_row_seq")
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
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.PreviewToken)
                .HasMaxLength(64)
                .HasColumnName("preview_token");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Snapshot).HasColumnName("snapshot");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VersionNo).HasColumnName("version_no");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PageVersionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_page_versions_created_by");

            entity.HasOne(d => d.Page).WithMany(p => p.PageVersions)
                .HasForeignKey(d => d.PageId)
                .HasConstraintName("FK_page_versions_page");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PageVersionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_page_versions_updated_by");
        });

        modelBuilder.Entity<PagesI18n>(entity =>
        {
            entity.HasKey(e => new { e.PageId, e.Locale });

            entity.ToTable("pages_i18n");

            entity.HasIndex(e => e.Locale, "IX_pages_i18n_locale");

            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.SeoDescription)
                .HasMaxLength(300)
                .HasColumnName("seo_description");
            entity.Property(e => e.SeoTitle)
                .HasMaxLength(200)
                .HasColumnName("seo_title");
            entity.Property(e => e.SeoKeywords)
                .HasMaxLength(200)
                .HasColumnName("seo_keywords");
            entity.Property(e => e.OgImageAlt)
                .HasMaxLength(200)
                .HasColumnName("og_image_alt");

            entity.HasOne(d => d.Page).WithMany(p => p.PagesI18ns)
                .HasForeignKey(d => d.PageId)
                .HasConstraintName("FK_pages_i18n_page");
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("partners");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_partners_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_partners_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_partners_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Country)
                .HasMaxLength(32)
                .HasColumnName("country");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndOn).HasColumnName("end_on");
            entity.Property(e => e.LogoDarkKey)
                .HasMaxLength(500)
                .HasColumnName("logo_dark_key");
            entity.Property(e => e.LogoLightKey)
                .HasMaxLength(500)
                .HasColumnName("logo_light_key");
            entity.Property(e => e.PartnerType)
                .HasMaxLength(32)
                .HasColumnName("partner_type");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ShowInFooter).HasColumnName("show_in_footer");
            entity.Property(e => e.ShowOnHome).HasColumnName("show_on_home");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.StartOn).HasColumnName("start_on");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(500)
                .HasColumnName("website_url");

            entity.HasOne(d => d.Club).WithMany(p => p.Partners)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_partners_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PartnerCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_partners_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PartnerUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_partners_updated_by");
        });

        modelBuilder.Entity<PartnerStore>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("partner_stores");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_partner_stores_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_partner_stores_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_partner_stores_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.ApplicableTier)
                .HasMaxLength(16)
                .HasColumnName("applicable_tier");
            entity.Property(e => e.BusinessHours).HasColumnName("business_hours");
            entity.Property(e => e.Category)
                .HasMaxLength(32)
                .HasColumnName("category");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndOn).HasColumnName("end_on");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.Lat)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("lat");
            entity.Property(e => e.Lng)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("lng");
            entity.Property(e => e.Phone)
                .HasMaxLength(32)
                .HasColumnName("phone");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.StartOn).HasColumnName("start_on");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(500)
                .HasColumnName("website_url");

            entity.HasOne(d => d.Club).WithMany(p => p.PartnerStores)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_partner_stores_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PartnerStoreCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_partner_stores_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PartnerStoreUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_partner_stores_updated_by");
        });

        modelBuilder.Entity<PartnerStoresI18n>(entity =>
        {
            entity.HasKey(e => new { e.PartnerStoreId, e.Locale });

            entity.ToTable("partner_stores_i18n");

            entity.HasIndex(e => e.Locale, "IX_partner_stores_i18n_locale");

            entity.Property(e => e.PartnerStoreId).HasColumnName("partner_store_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.OfferContent).HasColumnName("offer_content");

            entity.HasOne(d => d.PartnerStore).WithMany(p => p.PartnerStoresI18ns)
                .HasForeignKey(d => d.PartnerStoreId)
                .HasConstraintName("FK_partner_stores_i18n_store");
        });

        modelBuilder.Entity<PartnersI18n>(entity =>
        {
            entity.HasKey(e => new { e.PartnerId, e.Locale });

            entity.ToTable("partners_i18n");

            entity.HasIndex(e => e.Locale, "IX_partners_i18n_locale");

            entity.Property(e => e.PartnerId).HasColumnName("partner_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.Partner).WithMany(p => p.PartnersI18ns)
                .HasForeignKey(d => d.PartnerId)
                .HasConstraintName("FK_partners_i18n_partner");
        });

        modelBuilder.Entity<PaymentChannel>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("payment_channels");

            entity.HasIndex(e => new { e.OwnerClubId, e.ChannelType, e.Environment }, "UQ_payment_channels_owner_type_env").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_payment_channels_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ChannelType)
                .HasMaxLength(16)
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
            entity.Property(e => e.OwnerClubId).HasColumnName("owner_club_id");
            entity.Property(e => e.RotatedAt)
                .HasPrecision(3)
                .HasColumnName("rotated_at");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PaymentChannelCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_payment_channels_created_by");

            entity.HasOne(d => d.OwnerClub).WithMany(p => p.PaymentChannels)
                .HasForeignKey(d => d.OwnerClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_payment_channels_owner_club");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentChannelUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_payment_channels_updated_by");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("permissions");

            entity.HasIndex(e => e.Code, "UQ_permissions_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_permissions_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(16)
                .HasColumnName("action");
            entity.Property(e => e.Code)
                .HasMaxLength(96)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Domain)
                .HasMaxLength(32)
                .HasColumnName("domain");
            entity.Property(e => e.IsClubScoped).HasColumnName("is_club_scoped");
            entity.Property(e => e.IsRestricted).HasColumnName("is_restricted");
            entity.Property(e => e.ModuleCode)
                .HasMaxLength(4)
                .HasColumnName("module_code");
            entity.Property(e => e.NameEn)
                .HasMaxLength(64)
                .HasColumnName("name_en");
            entity.Property(e => e.NameZh)
                .HasMaxLength(64)
                .HasColumnName("name_zh");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
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

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("players");

            entity.HasIndex(e => e.RowSeq, "UQ_players_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.BirthOn).HasColumnName("birth_on");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.HeightCm).HasColumnName("height_cm");
            entity.Property(e => e.JoinedOn).HasColumnName("joined_on");
            entity.Property(e => e.Nationality)
                .HasMaxLength(32)
                .HasColumnName("nationality");
            entity.Property(e => e.PhotoKey)
                .HasMaxLength(500)
                .HasColumnName("photo_key");
            entity.Property(e => e.Position)
                .HasMaxLength(32)
                .HasColumnName("position");
            entity.Property(e => e.PreferredFoot)
                .HasMaxLength(16)
                .HasColumnName("preferred_foot");
            entity.Property(e => e.PortraitConsentStatus)
                .HasMaxLength(32)
                .HasDefaultValue("not_consented")
                .HasColumnName("portrait_consent_status");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ShirtNo).HasColumnName("shirt_no");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.WeightKg).HasColumnName("weight_kg");

            entity.HasOne(d => d.Club).WithMany(p => p.Players)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_players_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PlayerCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_players_created_by");

            entity.HasOne(d => d.Team).WithMany(p => p.Players)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_players_team");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PlayerUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_players_updated_by");
        });

        modelBuilder.Entity<PlayerSeasonStat>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("player_season_stats");

            entity.HasIndex(e => new { e.PlayerId, e.SeasonId }, "UQ_player_season_stats_player_season").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_player_season_stats_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Appearances).HasColumnName("appearances");
            entity.Property(e => e.Assists).HasColumnName("assists");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Goals).HasColumnName("goals");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.RedCards).HasColumnName("red_cards");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.YellowCards).HasColumnName("yellow_cards");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PlayerSeasonStatCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_player_season_stats_created_by");

            entity.HasOne(d => d.Player).WithMany(p => p.PlayerSeasonStats)
                .HasForeignKey(d => d.PlayerId)
                .HasConstraintName("FK_player_season_stats_player");

            entity.HasOne(d => d.Season).WithMany(p => p.PlayerSeasonStats)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_player_season_stats_season");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PlayerSeasonStatUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_player_season_stats_updated_by");
        });

        modelBuilder.Entity<PlayersI18n>(entity =>
        {
            entity.HasKey(e => new { e.PlayerId, e.Locale });

            entity.ToTable("players_i18n");

            entity.HasIndex(e => e.Locale, "IX_players_i18n_locale");

            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.Player).WithMany(p => p.PlayersI18ns)
                .HasForeignKey(d => d.PlayerId)
                .HasConstraintName("FK_players_i18n_player");
        });

        modelBuilder.Entity<PressResource>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("press_resources");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_press_resources_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_press_resources_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_press_resources_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
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
            entity.Property(e => e.DownloadCount).HasColumnName("download_count");
            entity.Property(e => e.FileBytes).HasColumnName("file_bytes");
            entity.Property(e => e.FileKey)
                .HasMaxLength(500)
                .HasColumnName("file_key");
            entity.Property(e => e.PublishedOn).HasColumnName("published_on");
            entity.Property(e => e.ResourceType)
                .HasMaxLength(32)
                .HasColumnName("resource_type");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
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

            entity.HasOne(d => d.Club).WithMany(p => p.PressResources)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_press_resources_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PressResourceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_press_resources_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PressResourceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_press_resources_updated_by");
        });

        modelBuilder.Entity<PressResourcesI18n>(entity =>
        {
            entity.HasKey(e => new { e.PressResourceId, e.Locale });

            entity.ToTable("press_resources_i18n");

            entity.HasIndex(e => e.Locale, "IX_press_resources_i18n_locale");

            entity.Property(e => e.PressResourceId).HasColumnName("press_resource_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.PressResource).WithMany(p => p.PressResourcesI18ns)
                .HasForeignKey(d => d.PressResourceId)
                .HasConstraintName("FK_press_resources_i18n_pr");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("products");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_products_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_products_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_products_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CollectionId).HasColumnName("collection_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsNewArrival).HasColumnName("is_new_arrival");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SizeChart).HasColumnName("size_chart");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
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

            entity.HasOne(d => d.Club).WithMany(p => p.Products)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_products_club");

            entity.HasOne(d => d.Collection).WithMany(p => p.Products)
                .HasForeignKey(d => d.CollectionId)
                .HasConstraintName("FK_products_collection");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_products_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProductUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_products_updated_by");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("product_images");

            entity.HasIndex(e => e.RowSeq, "UQ_product_images_row_seq")
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
            entity.Property(e => e.Height).HasColumnName("height");
            entity.Property(e => e.ImageKey)
                .HasMaxLength(500)
                .HasColumnName("image_key");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Width).HasColumnName("width");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductImageCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_product_images_created_by");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_product_images_product");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProductImageUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_product_images_updated_by");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("product_variants");

            entity.HasIndex(e => e.RowSeq, "UQ_product_variants_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Sku, "UQ_product_variants_sku").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Colour)
                .HasMaxLength(32)
                .HasColumnName("colour");
            entity.Property(e => e.Cost).HasColumnName("cost");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ReservedQty).HasColumnName("reserved_qty");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SalePrice).HasColumnName("sale_price");
            entity.Property(e => e.Size)
                .HasMaxLength(32)
                .HasColumnName("size");
            entity.Property(e => e.Sku)
                .HasMaxLength(64)
                .HasColumnName("sku");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.StockQty).HasColumnName("stock_qty");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_product_variants_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductVariantCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_product_variants_created_by");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_product_variants_product");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProductVariantUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_product_variants_updated_by");
        });

        modelBuilder.Entity<ProductsI18n>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.Locale });

            entity.ToTable("products_i18n");

            entity.HasIndex(e => e.Locale, "IX_products_i18n_locale");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");
            entity.Property(e => e.Narrative).HasColumnName("narrative");
            entity.Property(e => e.SeoDescription)
                .HasMaxLength(300)
                .HasColumnName("seo_description");
            entity.Property(e => e.SeoTitle)
                .HasMaxLength(200)
                .HasColumnName("seo_title");
            entity.Property(e => e.Tags)
                .HasMaxLength(255)
                .HasColumnName("tags");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductsI18ns)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK_products_i18n_product");
        });

        modelBuilder.Entity<TrainingProgram>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("programs");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_programs_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_programs_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_programs_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AgeMax).HasColumnName("age_max");
            entity.Property(e => e.AgeMin).HasColumnName("age_min");
            entity.Property(e => e.Audience)
                .HasMaxLength(32)
                .HasColumnName("audience");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CoverKey)
                .HasMaxLength(500)
                .HasColumnName("cover_key");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ProgramType)
                .HasMaxLength(32)
                .HasColumnName("program_type");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Programs)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_programs_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProgramCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_programs_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProgramUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_programs_updated_by");

            entity.HasMany(d => d.Partners).WithMany(p => p.Programs)
                .UsingEntity<Dictionary<string, object>>(
                    "ProgramPartner",
                    r => r.HasOne<Partner>().WithMany()
                        .HasForeignKey("PartnerId")
                        .HasConstraintName("FK_program_partners_partner"),
                    l => l.HasOne<TrainingProgram>().WithMany()
                        .HasForeignKey("ProgramId")
                        .HasConstraintName("FK_program_partners_program"),
                    j =>
                    {
                        j.HasKey("ProgramId", "PartnerId");
                        j.ToTable("program_partners");
                        j.IndexerProperty<Guid>("ProgramId").HasColumnName("program_id");
                        j.IndexerProperty<Guid>("PartnerId").HasColumnName("partner_id");
                    });

            entity.HasMany(d => d.Staff).WithMany(p => p.Programs)
                .UsingEntity<Dictionary<string, object>>(
                    "ProgramStaff",
                    r => r.HasOne<Staff>().WithMany()
                        .HasForeignKey("StaffId")
                        .HasConstraintName("FK_program_staff_staff"),
                    l => l.HasOne<TrainingProgram>().WithMany()
                        .HasForeignKey("ProgramId")
                        .HasConstraintName("FK_program_staff_program"),
                    j =>
                    {
                        j.HasKey("ProgramId", "StaffId");
                        j.ToTable("program_staff");
                        j.IndexerProperty<Guid>("ProgramId").HasColumnName("program_id");
                        j.IndexerProperty<Guid>("StaffId").HasColumnName("staff_id");
                    });
        });

        modelBuilder.Entity<ProgramsI18n>(entity =>
        {
            entity.HasKey(e => new { e.ProgramId, e.Locale });

            entity.ToTable("programs_i18n");

            entity.HasIndex(e => e.Locale, "IX_programs_i18n_locale");

            entity.Property(e => e.ProgramId).HasColumnName("program_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Intro).HasColumnName("intro");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.TrainingProgram).WithMany(p => p.ProgramsI18ns)
                .HasForeignKey(d => d.ProgramId)
                .HasConstraintName("FK_programs_i18n_program");
        });

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("proposals");

            entity.HasIndex(e => e.RowSeq, "UQ_proposals_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(128)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VersionNo)
                .HasDefaultValue(1)
                .HasColumnName("version_no");

            entity.HasOne(d => d.Club).WithMany(p => p.Proposals)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_proposals_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProposalCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_proposals_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProposalUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_proposals_updated_by");
        });

        modelBuilder.Entity<ProposalFile>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("proposal_files");

            entity.HasIndex(e => e.RowSeq, "UQ_proposal_files_row_seq")
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
            entity.Property(e => e.FileBytes).HasColumnName("file_bytes");
            entity.Property(e => e.FileKey)
                .HasMaxLength(500)
                .HasColumnName("file_key");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.ProposalId).HasColumnName("proposal_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VersionNo)
                .HasDefaultValue(1)
                .HasColumnName("version_no");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProposalFileCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_proposal_files_created_by");

            entity.HasOne(d => d.Proposal).WithMany(p => p.ProposalFiles)
                .HasForeignKey(d => d.ProposalId)
                .HasConstraintName("FK_proposal_files_proposal");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProposalFileUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_proposal_files_updated_by");
        });

        modelBuilder.Entity<Redirect>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("redirects");

            entity.HasIndex(e => new { e.ClubId, e.FromPath }, "UQ_redirects_club_path").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_redirects_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.FromPath)
                .HasMaxLength(500)
                .HasColumnName("from_path");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ToPath)
                .HasMaxLength(500)
                .HasColumnName("to_path");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Redirects)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_redirects_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RedirectCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_redirects_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RedirectUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_redirects_updated_by");
        });

        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("refund_requests");

            entity.HasIndex(e => e.RowSeq, "UQ_refund_requests_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.RefundAmount).HasColumnName("refund_amount");
            entity.Property(e => e.RefundMethod)
                .HasMaxLength(32)
                .HasColumnName("refund_method");
            entity.Property(e => e.RefundedAt)
                .HasPrecision(3)
                .HasColumnName("refunded_at");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.RefundRequestApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_refund_requests_approved_by");

            entity.HasOne(d => d.Club).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_refund_requests_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RefundRequestCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_refund_requests_created_by");

            entity.HasOne(d => d.Order).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_refund_requests_order");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RefundRequestUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_refund_requests_updated_by");
        });

        modelBuilder.Entity<RefundRequestItem>(entity =>
        {
            entity.HasKey(e => new { e.RefundRequestId, e.OrderItemId });

            entity.ToTable("refund_request_items");

            entity.Property(e => e.RefundRequestId).HasColumnName("refund_request_id");
            entity.Property(e => e.OrderItemId).HasColumnName("order_item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.RefundRequestItems)
                .HasForeignKey(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_refund_request_items_item");

            entity.HasOne(d => d.RefundRequest).WithMany(p => p.RefundRequestItems)
                .HasForeignKey(d => d.RefundRequestId)
                .HasConstraintName("FK_refund_request_items_request");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("registrations");

            entity.HasIndex(e => e.MemberId, "IX_registrations_member");

            entity.HasIndex(e => new { e.SessionId, e.Status }, "IX_registrations_session_status");

            entity.HasIndex(e => e.RegistrationNo, "UQ_registrations_registration_no").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_registrations_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ApplicantName)
                .HasMaxLength(64)
                .HasColumnName("applicant_name");
            entity.Property(e => e.BirthOn).HasColumnName("birth_on");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.GuardianName)
                .HasMaxLength(64)
                .HasColumnName("guardian_name");
            entity.Property(e => e.GuardianPhone)
                .HasMaxLength(32)
                .HasColumnName("guardian_phone");
            entity.Property(e => e.HealthDeclaration).HasColumnName("health_declaration");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.Phone)
                .HasMaxLength(32)
                .HasColumnName("phone");
            entity.Property(e => e.RegistrationNo)
                .HasMaxLength(32)
                .HasColumnName("registration_no");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasDefaultValue("待確認")
                .HasColumnName("status");
            entity.Property(e => e.TrialId).HasColumnName("trial_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_registrations_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RegistrationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_registrations_created_by");

            entity.HasOne(d => d.Member).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.MemberId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_registrations_member");

            entity.HasOne(d => d.Session).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK_registrations_session");

            entity.HasOne(d => d.Trial).WithMany(p => p.Registrations)
                .HasForeignKey(d => d.TrialId)
                .HasConstraintName("FK_registrations_trial");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RegistrationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_registrations_updated_by");
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.AdminRoleId, e.PermissionId });

            entity.ToTable("role_permissions");

            entity.Property(e => e.AdminRoleId).HasColumnName("admin_role_id");
            entity.Property(e => e.PermissionId).HasColumnName("permission_id");
            entity.Property(e => e.ScopeType)
                .HasMaxLength(32)
                .HasDefaultValue("all")
                .HasColumnName("scope_type");

            entity.HasOne(d => d.AdminRole).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.AdminRoleId)
                .HasConstraintName("FK_role_permissions_role");

            entity.HasOne(d => d.Permission).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.PermissionId)
                .HasConstraintName("FK_role_permissions_permission");
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("seasons");

            entity.HasIndex(e => new { e.ClubId, e.Code }, "UQ_seasons_club_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_seasons_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Code)
                .HasMaxLength(16)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndOn).HasColumnName("end_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.StartOn).HasColumnName("start_on");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Seasons)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_seasons_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SeasonCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_seasons_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SeasonUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_seasons_updated_by");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("sessions");

            entity.HasIndex(e => e.RowSeq, "UQ_sessions_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EarlyBirdPrice).HasColumnName("early_bird_price");
            entity.Property(e => e.EarlyBirdUntil).HasColumnName("early_bird_until");
            entity.Property(e => e.EndOn).HasColumnName("end_on");
            entity.Property(e => e.EnrolledCount).HasColumnName("enrolled_count");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.ProgramId).HasColumnName("program_id");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SignupClosesAt)
                .HasPrecision(3)
                .HasColumnName("signup_closes_at");
            entity.Property(e => e.SignupOpensAt)
                .HasPrecision(3)
                .HasColumnName("signup_opens_at");
            entity.Property(e => e.StartOn).HasColumnName("start_on");
            entity.Property(e => e.Status)
                .HasMaxLength(16)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VenueId).HasColumnName("venue_id");
            entity.Property(e => e.WeeklySchedule).HasColumnName("weekly_schedule");

            entity.HasOne(d => d.Club).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_sessions_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SessionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_sessions_created_by");

            entity.HasOne(d => d.TrainingProgram).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.ProgramId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_sessions_program");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SessionUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_sessions_updated_by");

            entity.HasOne(d => d.Venue).WithMany(p => p.Sessions)
                .HasForeignKey(d => d.VenueId)
                .HasConstraintName("FK_sessions_venue");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("settings");

            entity.HasIndex(e => new { e.ClubId, e.SettingKey }, "UQ_settings_club_key").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_settings_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SettingGroup)
                .HasMaxLength(32)
                .HasColumnName("setting_group");
            entity.Property(e => e.SettingKey)
                .HasMaxLength(128)
                .HasColumnName("setting_key");
            entity.Property(e => e.SettingValue).HasColumnName("setting_value");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Settings)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_settings_club");

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

            entity.HasOne(d => d.Setting).WithMany(p => p.SettingsI18ns)
                .HasForeignKey(d => d.SettingId)
                .HasConstraintName("FK_settings_i18n_setting");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("shipments");

            entity.HasIndex(e => e.RowSeq, "UQ_shipments_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Carrier)
                .HasMaxLength(32)
                .HasColumnName("carrier");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeliveredAt)
                .HasPrecision(3)
                .HasColumnName("delivered_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PickupStatus)
                .HasMaxLength(16)
                .HasColumnName("pickup_status");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.ShippedAt)
                .HasPrecision(3)
                .HasColumnName("shipped_at");
            entity.Property(e => e.StoreBranchCode)
                .HasMaxLength(32)
                .HasColumnName("store_branch_code");
            entity.Property(e => e.TrackingNo)
                .HasMaxLength(64)
                .HasColumnName("tracking_no");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shipments_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ShipmentCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_shipments_created_by");

            entity.HasOne(d => d.Order).WithMany(p => p.Shipments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_shipments_order");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ShipmentUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_shipments_updated_by");
        });

        modelBuilder.Entity<Sponsor>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("sponsors");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_sponsors_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_sponsors_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_sponsors_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(255)
                .HasColumnName("contact_email");
            entity.Property(e => e.ContactName)
                .HasMaxLength(64)
                .HasColumnName("contact_name");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(32)
                .HasColumnName("contact_phone");
            entity.Property(e => e.ContractEndOn).HasColumnName("contract_end_on");
            entity.Property(e => e.ContractStartOn).HasColumnName("contract_start_on");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.ExpiryAlertOn).HasColumnName("expiry_alert_on");
            entity.Property(e => e.LogoDarkKey)
                .HasMaxLength(500)
                .HasColumnName("logo_dark_key");
            entity.Property(e => e.LogoLightKey)
                .HasMaxLength(500)
                .HasColumnName("logo_light_key");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.Tier)
                .HasMaxLength(32)
                .HasColumnName("tier");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Sponsors)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_sponsors_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SponsorCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_sponsors_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SponsorUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_sponsors_updated_by");

            entity.HasMany(d => d.SponsorPackages).WithMany(p => p.Sponsors)
                .UsingEntity<Dictionary<string, object>>(
                    "SponsorPackageLink",
                    r => r.HasOne<SponsorPackage>().WithMany()
                        .HasForeignKey("SponsorPackageId")
                        .HasConstraintName("FK_sponsor_package_links_pkg"),
                    l => l.HasOne<Sponsor>().WithMany()
                        .HasForeignKey("SponsorId")
                        .HasConstraintName("FK_sponsor_package_links_sponsor"),
                    j =>
                    {
                        j.HasKey("SponsorId", "SponsorPackageId");
                        j.ToTable("sponsor_package_links");
                        j.IndexerProperty<Guid>("SponsorId").HasColumnName("sponsor_id");
                        j.IndexerProperty<Guid>("SponsorPackageId").HasColumnName("sponsor_package_id");
                    });
        });

        modelBuilder.Entity<SponsorPackage>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("sponsor_packages");

            entity.HasIndex(e => new { e.Slug, e.ClubId }, "IX_sponsor_packages_slug_club");

            entity.HasIndex(e => new { e.ClubId, e.Slug }, "UQ_sponsor_packages_club_slug").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_sponsor_packages_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsPricePublic)
                .HasDefaultValue(true)
                .HasColumnName("is_price_public");
            entity.Property(e => e.PriceMax).HasColumnName("price_max");
            entity.Property(e => e.PriceMin).HasColumnName("price_min");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
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

            entity.HasOne(d => d.Club).WithMany(p => p.SponsorPackages)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_sponsor_packages_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SponsorPackageCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_sponsor_packages_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SponsorPackageUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_sponsor_packages_updated_by");
        });

        modelBuilder.Entity<SponsorPackagesI18n>(entity =>
        {
            entity.HasKey(e => new { e.SponsorPackageId, e.Locale });

            entity.ToTable("sponsor_packages_i18n");

            entity.HasIndex(e => e.Locale, "IX_sponsor_packages_i18n_locale");

            entity.Property(e => e.SponsorPackageId).HasColumnName("sponsor_package_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Audience)
                .HasMaxLength(128)
                .HasColumnName("audience");
            entity.Property(e => e.BenefitList).HasColumnName("benefit_list");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.SponsorPackage).WithMany(p => p.SponsorPackagesI18ns)
                .HasForeignKey(d => d.SponsorPackageId)
                .HasConstraintName("FK_sponsor_packages_i18n_pkg");
        });

        modelBuilder.Entity<SponsorsI18n>(entity =>
        {
            entity.HasKey(e => new { e.SponsorId, e.Locale });

            entity.ToTable("sponsors_i18n");

            entity.HasIndex(e => e.Locale, "IX_sponsors_i18n_locale");

            entity.Property(e => e.SponsorId).HasColumnName("sponsor_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.Sponsor).WithMany(p => p.SponsorsI18ns)
                .HasForeignKey(d => d.SponsorId)
                .HasConstraintName("FK_sponsors_i18n_sponsor");
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("staff");

            entity.HasIndex(e => e.RowSeq, "UQ_staff_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Licence)
                .HasMaxLength(64)
                .HasColumnName("licence");
            entity.Property(e => e.PhotoKey)
                .HasMaxLength(500)
                .HasColumnName("photo_key");
            entity.Property(e => e.PortraitConsentStatus)
                .HasMaxLength(32)
                .HasDefaultValue("not_consented")
                .HasColumnName("portrait_consent_status");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.StaffGroup)
                .HasMaxLength(32)
                .HasColumnName("staff_group");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Staff)
                .HasForeignKey(d => d.ClubId)
                .HasConstraintName("FK_staff_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_staff_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StaffUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_staff_updated_by");
        });

        modelBuilder.Entity<StaffI18n>(entity =>
        {
            entity.HasKey(e => new { e.StaffId, e.Locale });

            entity.ToTable("staff_i18n");

            entity.HasIndex(e => e.Locale, "IX_staff_i18n_locale");

            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");
            entity.Property(e => e.Title)
                .HasMaxLength(64)
                .HasColumnName("title");

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffI18ns)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK_staff_i18n_staff");
        });

        modelBuilder.Entity<StaffTeam>(entity =>
        {
            entity.HasKey(e => new { e.StaffId, e.TeamId });

            entity.ToTable("staff_teams");

            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.RoleCode)
                .HasMaxLength(64)
                .HasColumnName("role_code");

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffTeams)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK_staff_teams_staff");

            entity.HasOne(d => d.Team).WithMany(p => p.StaffTeams)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_staff_teams_team");
        });

        modelBuilder.Entity<Standing>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("standings");

            entity.HasIndex(e => e.RowSeq, "UQ_standings_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Played).HasColumnName("played");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.Rank).HasColumnName("rank");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SeasonId).HasColumnName("season_id");
            entity.Property(e => e.TeamName)
                .HasMaxLength(128)
                .HasColumnName("team_name");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Standings)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_standings_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StandingCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_standings_created_by");

            entity.HasOne(d => d.Season).WithMany(p => p.Standings)
                .HasForeignKey(d => d.SeasonId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_standings_season");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StandingUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_standings_updated_by");
        });

        modelBuilder.Entity<StoreInvoice>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("store_invoices");

            entity.HasIndex(e => e.RowSeq, "UQ_store_invoices_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CarrierIdEncrypted)
                .HasMaxLength(64)
                .HasColumnName("carrier_id_encrypted");
            entity.Property(e => e.CarrierType)
                .HasMaxLength(16)
                .HasColumnName("carrier_type");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DonationCode)
                .HasMaxLength(16)
                .HasColumnName("donation_code");
            entity.Property(e => e.InvoiceNo)
                .HasMaxLength(32)
                .HasColumnName("invoice_no");
            entity.Property(e => e.IssueStatus)
                .HasMaxLength(16)
                .HasDefaultValue("pending")
                .HasColumnName("issue_status");
            entity.Property(e => e.IssuedAt)
                .HasPrecision(3)
                .HasColumnName("issued_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PaymentChannelId).HasColumnName("payment_channel_id");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.TaxId)
                .HasMaxLength(16)
                .HasColumnName("tax_id");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VoidStatus)
                .HasMaxLength(16)
                .HasDefaultValue("none")
                .HasColumnName("void_status");

            entity.HasOne(d => d.Club).WithMany(p => p.StoreInvoices)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_store_invoices_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StoreInvoiceCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_store_invoices_created_by");

            entity.HasOne(d => d.Order).WithMany(p => p.StoreInvoices)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_store_invoices_order");

            entity.HasOne(d => d.PaymentChannel).WithMany(p => p.StoreInvoices)
                .HasForeignKey(d => d.PaymentChannelId)
                .HasConstraintName("FK_store_invoices_channel");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.StoreInvoiceUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_store_invoices_updated_by");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("tags");

            entity.HasIndex(e => e.RowSeq, "UQ_tags_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.Slug, "UQ_tags_slug").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.Slug)
                .HasMaxLength(160)
                .HasColumnName("slug");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.TagCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_tags_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.TagUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_tags_updated_by");
        });

        modelBuilder.Entity<TagsI18n>(entity =>
        {
            entity.HasKey(e => new { e.TagId, e.Locale });

            entity.ToTable("tags_i18n");

            entity.HasIndex(e => e.Locale, "IX_tags_i18n_locale");

            entity.Property(e => e.TagId).HasColumnName("tag_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.Tag).WithMany(p => p.TagsI18ns)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("FK_tags_i18n_tag");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("teams");

            entity.HasIndex(e => e.Code, "UQ_teams_code").IsUnique();

            entity.HasIndex(e => e.RowSeq, "UQ_teams_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AgeBand)
                .HasMaxLength(16)
                .HasColumnName("age_band");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.Code)
                .HasMaxLength(8)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Gender)
                .HasMaxLength(16)
                .HasColumnName("gender");
            entity.Property(e => e.HeroKey)
                .HasMaxLength(500)
                .HasColumnName("hero_key");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.TeamColor)
                .HasMaxLength(16)
                .HasColumnName("team_color");
            entity.Property(e => e.Type)
                .HasMaxLength(16)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.Club).WithMany(p => p.Teams)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_teams_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.TeamCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_teams_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.TeamUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_teams_updated_by");
        });

        modelBuilder.Entity<TeamsI18n>(entity =>
        {
            entity.HasKey(e => new { e.TeamId, e.Locale });

            entity.ToTable("teams_i18n");

            entity.HasIndex(e => e.Locale, "IX_teams_i18n_locale");

            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Intro).HasColumnName("intro");
            entity.Property(e => e.Name)
                .HasMaxLength(64)
                .HasColumnName("name");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamsI18ns)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_teams_i18n_team");
        });

        modelBuilder.Entity<Trial>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("trials");

            entity.HasIndex(e => e.RowSeq, "UQ_trials_row_seq")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.ClubId).HasColumnName("club_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeadlineOn).HasColumnName("deadline_on");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SyncToCalendar).HasColumnName("sync_to_calendar");
            entity.Property(e => e.TeamId).HasColumnName("team_id");
            entity.Property(e => e.TrialOn).HasColumnName("trial_on");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.VenueId).HasColumnName("venue_id");

            entity.HasOne(d => d.Club).WithMany(p => p.Trials)
                .HasForeignKey(d => d.ClubId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_trials_club");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.TrialCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_trials_created_by");

            entity.HasOne(d => d.Team).WithMany(p => p.Trials)
                .HasForeignKey(d => d.TeamId)
                .HasConstraintName("FK_trials_team");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.TrialUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_trials_updated_by");

            entity.HasOne(d => d.Venue).WithMany(p => p.Trials)
                .HasForeignKey(d => d.VenueId)
                .HasConstraintName("FK_trials_venue");
        });

        modelBuilder.Entity<UiString>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("ui_strings");

            entity.HasIndex(e => e.RowSeq, "UQ_ui_strings_row_seq")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => e.StringKey, "UQ_ui_strings_string_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.StringGroup)
                .HasMaxLength(64)
                .HasColumnName("string_group");
            entity.Property(e => e.StringKey)
                .HasMaxLength(128)
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

            entity.Property(e => e.UiStringId).HasColumnName("ui_string_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.UiString).WithMany(p => p.UiStringTranslations)
                .HasForeignKey(d => d.UiStringId)
                .HasConstraintName("FK_ui_string_translations_ui_string");
        });

        modelBuilder.Entity<ValueTagLink>(entity =>
        {
            entity.HasKey(e => new { e.EntityType, e.EntityId, e.ValueTag });

            entity.ToTable("value_tag_links");

            entity.Property(e => e.EntityType)
                .HasMaxLength(32)
                .HasColumnName("entity_type");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.ValueTag)
                .HasMaxLength(32)
                .HasColumnName("value_tag");
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.ToTable("venues");

            entity.HasIndex(e => e.RowSeq, "UQ_venues_row_seq")
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
            entity.Property(e => e.Lat)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("lat");
            entity.Property(e => e.Lng)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("lng");
            entity.Property(e => e.PhotoKey)
                .HasMaxLength(500)
                .HasColumnName("photo_key");
            entity.Property(e => e.RowSeq)
                .ValueGeneratedOnAdd()
                .HasColumnName("row_seq");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.VenueCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_venues_created_by");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.VenueUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK_venues_updated_by");
        });

        modelBuilder.Entity<VenuesI18n>(entity =>
        {
            entity.HasKey(e => new { e.VenueId, e.Locale });

            entity.ToTable("venues_i18n");

            entity.HasIndex(e => e.Locale, "IX_venues_i18n_locale");

            entity.Property(e => e.VenueId).HasColumnName("venue_id");
            entity.Property(e => e.Locale)
                .HasMaxLength(10)
                .HasColumnName("locale");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.Directions).HasColumnName("directions");
            entity.Property(e => e.Name)
                .HasMaxLength(128)
                .HasColumnName("name");

            entity.HasOne(d => d.Venue).WithMany(p => p.VenuesI18ns)
                .HasForeignKey(d => d.VenueId)
                .HasConstraintName("FK_venues_i18n_venue");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
