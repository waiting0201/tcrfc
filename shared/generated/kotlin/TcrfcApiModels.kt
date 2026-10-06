// 自動產生，請勿手改。來源：shared/openapi.json；產生器：shared/scripts/gen-dto.mjs（docs/19 §2）。
// 只有資料型別（DTO），沒有 client：權杖續期、冪等鍵、離線佇列、退避重試由 App 的網路層自己負責。
// 時間欄位一律是 String（伺服器的 UTC ISO 8601 字串），解析交給 App 的時間工具。
// 可為空的欄位預設 null；Json 設定請用 ignoreUnknownKeys = true，讓後端新增欄位時舊版 App 不會解析失敗。

package tw.tcrfc.app.api.dto

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement

@Serializable
data class AchievementDto(
    val id: String,
    val year: Int? = null,
    val seasonCode: String? = null,
    val teamCode: String,
    val teamName: String? = null,
    val competitionName: String? = null,
    val placing: String? = null,
)

@Serializable
data class AddCartItemRequest(
    val variantId: String? = null,
    val quantity: Int? = null,
)

@Serializable
data class AppAdEventBatchRequest(
    val batchId: String? = null,
    val deviceInstallId: String,
    val platform: String,
    val appVersion: String? = null,
    val locale: String? = null,
    val events: List<AppAdEventInput>,
)

@Serializable
data class AppAdEventBatchResult(
    val accepted: Int,
    val duplicates: Int,
    val rejected: List<AppAdEventRejectionDto>,
)

@Serializable
data class AppAdEventInput(
    val type: String,
    val creativeId: String,
    val occurredAt: String,
    val presentationId: String? = null,
)

@Serializable
data class AppAdEventRejectionDto(
    val index: Int,
    val reason: String,
)

@Serializable
data class AppAdImageVariantsDto(
    val url320: String,
    val url640: String,
    val url1280: String,
)

@Serializable
data class AppAdItemDto(
    val creativeId: String? = null,
    val campaignId: String? = null,
    val isFallback: Boolean,
    val imageUrl: String? = null,
    val imageWidth: Int? = null,
    val imageHeight: Int? = null,
    val imageVariants: AppAdImageVariantsDto? = null,
    val videoUrl: String? = null,
    val altText: String? = null,
    val title: String? = null,
    val ctaText: String? = null,
    val clickUrl: String? = null,
    val theme: String? = null,
)

@Serializable
data class AppAdPrefetchItemDto(
    val item: AppAdItemDto,
    val startsAt: JsonElement,
    val endsAt: JsonElement,
    val weight: Int,
)

@Serializable
data class AppAdPrefetchResponse(
    val generatedAt: JsonElement,
    val validUntil: JsonElement,
    val disclosureLabel: String,
    val slots: List<AppAdPrefetchSlotDto>,
)

@Serializable
data class AppAdPrefetchSlotDto(
    val slotCode: String,
    val rotationCap: Int? = null,
    val sessionImpressionCap: Int? = null,
    val fallback: AppAdItemDto? = null,
    val items: List<AppAdPrefetchItemDto>,
)

@Serializable
data class AppAdResponse(
    val slotCode: String,
    val isFallback: Boolean,
    val disclosureLabel: String,
    val sessionImpressionCap: Int? = null,
    val items: List<AppAdItemDto>,
)

@Serializable
data class AppAnnouncementDto(
    val id: String,
    val message: String? = null,
    val linkUrl: String? = null,
    val endsAt: JsonElement? = null,
)

@Serializable
data class AppBilingualText(
    val zh: String? = null,
    val en: String? = null,
)

@Serializable
data class AppConfigEvaluation(
    val maintenance: Boolean,
    val updateRequired: Boolean,
    val updateRecommended: Boolean,
)

@Serializable
data class AppConfigResponse(
    val generatedAt: JsonElement,
    val ios: AppPlatformConfig,
    val android: AppPlatformConfig,
    val evaluation: AppConfigEvaluation? = null,
)

@Serializable
data class AppDeepLinkDto(
    val code: String,
    val label: String? = null,
    val appLink: String,
    val webUrl: String? = null,
    val requiresLogin: Boolean,
)

@Serializable
data class AppDeviceRegisteredDto(
    val deviceInstallId: String,
    val isNew: Boolean,
    val pushTokenStatus: String,
)

@Serializable
data class AppDiagnosticBatchRequest(
    val reports: List<AppDiagnosticInput>,
)

@Serializable
data class AppDiagnosticInput(
    val deviceInstallId: String? = null,
    val platform: String,
    val appVersion: String,
    val buildNumber: String? = null,
    val osVersion: String? = null,
    val occurredAt: String,
    val type: String,
    val metricValue: Int? = null,
    val summary: String? = null,
    val detail: String? = null,
)

@Serializable
data class AppLayoutItemDto(
    val code: String,
    val label: String? = null,
    val icon: String? = null,
    val deepLink: String? = null,
    val webUrl: String? = null,
    val isExternal: Boolean? = null,
)

@Serializable
data class AppLayoutResponse(
    val generatedAt: JsonElement,
    val homeSections: List<AppLayoutItemDto>,
    val quickEntries: List<AppLayoutItemDto>,
    val moreItems: List<AppLayoutItemDto>,
    val announcements: List<AppAnnouncementDto>,
    val deepLinks: List<AppDeepLinkDto>,
    val sponsorshipInquiryWebUrl: String? = null,
)

@Serializable
data class AppMaintenanceNode(
    val enabled: Boolean,
    val message: AppBilingualText? = null,
)

@Serializable
data class AppNotificationDto(
    val id: String,
    val title: String? = null,
    val body: String? = null,
    val imageUrl: String? = null,
    val deepLink: String? = null,
    val sentAt: JsonElement,
)

@Serializable
data class AppPlatformConfig(
    val minSupportedVersion: String? = null,
    val recommendedVersion: String? = null,
    val forceUpdateMessage: AppBilingualText? = null,
    val recommendUpdateMessage: AppBilingualText? = null,
    val whatsNew: AppBilingualText? = null,
    val maintenance: AppMaintenanceNode,
    val featureFlags: Map<String, Boolean>,
)

@Serializable
data class AppPushOpenedRequest(
    val deviceInstallId: String,
)

@Serializable
data class AppSubscriptionDto(
    val topicType: String,
    val topicValue: String,
    val isFollowing: Boolean,
    val isPushEnabled: Boolean,
)

@Serializable
data class AppSubscriptionInput(
    val topicType: String,
    val topicValue: String,
    val isFollowing: Boolean? = null,
    val isPushEnabled: Boolean? = null,
)

@Serializable
data class ArticleDetailDto(
    val id: String,
    val isFallbackLocale: Boolean,
    val slug: String,
    val categoryCode: String,
    val categoryName: String? = null,
    val coverKey: String? = null,
    val coverUrl: String? = null,
    val coverWidth: Int? = null,
    val coverHeight: Int? = null,
    val coverAlt: String? = null,
    val isFeatured: Boolean,
    val viewCount: Int? = null,
    val publishedAt: JsonElement? = null,
    val title: String? = null,
    val summary: String? = null,
    val bodyJson: String? = null,
    val seoTitle: String? = null,
    val seoDescription: String? = null,
    val seoKeywords: String? = null,
    val ogImageUrl: String? = null,
    val ogImageWidth: Int? = null,
    val ogImageHeight: Int? = null,
    val ogImageAlt: String? = null,
    val canonicalPath: String? = null,
    val isNoindex: Boolean,
    val isShared: Boolean,
    val breadcrumbSchemaEligible: Boolean,
    val tags: List<ArticleTagDto>,
    val coreValueTags: List<String>,
    val relations: List<ArticleRelationDto>,
    val schemaEligible: Boolean,
)

@Serializable
data class ArticleListItemDto(
    val id: String,
    val isFallbackLocale: Boolean,
    val slug: String,
    val categoryCode: String,
    val categoryName: String? = null,
    val coverKey: String? = null,
    val coverUrl: String? = null,
    val coverWidth: Int? = null,
    val coverHeight: Int? = null,
    val coverAlt: String? = null,
    val isFeatured: Boolean,
    val publishedAt: JsonElement? = null,
    val title: String? = null,
    val summary: String? = null,
    val isShared: Boolean,
    val tags: List<ArticleTagDto>,
)

@Serializable
data class ArticleRelationDto(
    val targetType: String,
    val targetId: String,
)

@Serializable
data class ArticleTagDto(
    val slug: String,
    val name: String? = null,
)

@Serializable
data class BannerDto(
    val id: String,
    val mediaType: String,
    val imageKey: String,
    val imageWidth: Int? = null,
    val imageHeight: Int? = null,
    val videoKey: String? = null,
    val imageUrl: String? = null,
    val videoUrl: String? = null,
    val sortOrder: Int,
    val title: String? = null,
    val subtitle: String? = null,
    val imageAlt: String? = null,
    val cta1Label: String? = null,
    val cta1Url: String? = null,
    val cta2Label: String? = null,
    val cta2Url: String? = null,
)

@Serializable
data class BenefitGroupPublicDto(
    val group: String,
    val groupLabel: String,
    val items: List<BenefitItemPublicDto>,
)

@Serializable
data class BenefitItemPublicDto(
    val name: String? = null,
    val isFallbackLocale: Boolean,
    val description: String? = null,
    val freeValue: String? = null,
    val paidValue: String? = null,
)

@Serializable
data class BenefitTablePublicDto(
    val planCode: String? = null,
    val planName: String? = null,
    val isFallbackLocale: Boolean,
    val groups: List<BenefitGroupPublicDto>,
)

@Serializable
data class CardVerificationDto(
    val nameInitial: String,
    val memberNo: String,
    val tier: String,
    val tierLabel: String,
    val status: String,
    val statusLabel: String,
)

@Serializable
data class CharityArticleLinkDto(
    val slug: String,
    val title: String? = null,
)

@Serializable
data class CharityCtaDto(
    val donationUrl: String? = null,
    val donationCta: String? = null,
    val fanCta: String? = null,
    val corporateCta: String? = null,
    val corporateUrl: String? = null,
)

@Serializable
data class CharityLinkedItemDto(
    val slug: String,
    val name: String? = null,
    val logoDarkUrl: String? = null,
    val logoLightUrl: String? = null,
)

@Serializable
data class CharityProgramDetailDto(
    val id: String,
    val slug: String,
    val name: String? = null,
    val targetAudience: String? = null,
    val startOn: String? = null,
    val endOn: String? = null,
    val progress: String,
    val coverUrl: String? = null,
    val content: String? = null,
    val donationContent: String? = null,
    val charity: PublicCharityOrgDto? = null,
    val images: List<PublicCharityImageDto>,
    val partners: List<CharityLinkedItemDto>,
    val sponsors: List<CharityLinkedItemDto>,
    val articles: List<CharityArticleLinkDto>,
)

@Serializable
data class CharityProgramListItemDto(
    val id: String,
    val slug: String,
    val name: String? = null,
    val targetAudience: String? = null,
    val startOn: String? = null,
    val endOn: String? = null,
    val progress: String,
    val isPinned: Boolean,
    val coverUrl: String? = null,
    val charityName: String? = null,
)

@Serializable
data class CheckoutInvoiceRequest(
    val type: String? = null,
    val carrierId: String? = null,
    val taxId: String? = null,
    val donationCode: String? = null,
)

@Serializable
data class CheckoutRequest(
    val email: String? = null,
    val recipientName: String? = null,
    val recipientPhone: String? = null,
    val deliveryMethod: String? = null,
    val recipientAddress: String? = null,
    val pickupStore: String? = null,
    val customerNote: String? = null,
    val invoice: CheckoutInvoiceRequest? = null,
    val lang: String? = null,
)

@Serializable
data class ClubDto(
    val code: String,
    val name: String,
    val shortName: String? = null,
    val isFallbackLocale: Boolean,
    val description: String? = null,
    val domain: String,
    val ogImageKey: String? = null,
    val defaultLocale: String,
    val ogImageUrl: String? = null,
    val schemaEligible: Boolean,
)

@Serializable
data class ComicAboutPublicDto(
    val title: String? = null,
    val body: String? = null,
)

@Serializable
data class ComicCharacterPublicDto(
    val id: String,
    val name: String? = null,
    val description: String? = null,
    val imageUrl: String? = null,
    val imageThumbUrl: String? = null,
    val playerId: String? = null,
)

@Serializable
data class ComicEpisodeDetailDto(
    val episodeNo: Int,
    val title: String? = null,
    val coverUrl: String? = null,
    val publishedOn: String? = null,
    val isLatest: Boolean,
    val pages: List<ComicPagePublicDto>,
    val previousEpisodeNo: Int? = null,
    val nextEpisodeNo: Int? = null,
)

@Serializable
data class ComicEpisodeListItemDto(
    val episodeNo: Int,
    val title: String? = null,
    val coverUrl: String? = null,
    val coverThumbUrl: String? = null,
    val publishedOn: String? = null,
    val isLatest: Boolean,
    val pageCount: Int,
)

@Serializable
data class ComicPagePublicDto(
    val pageNo: Int,
    val imageUrl: String? = null,
    val imageThumbUrl: String? = null,
    val width: Int? = null,
    val height: Int? = null,
)

@Serializable
data class CompetitionDto(
    val id: String,
    val code: String,
    val clubCode: String,
    val seasonCode: String,
    val compType: String? = null,
    val name: String? = null,
    val isFallbackLocale: Boolean,
    val organizer: String? = null,
    val sortOrder: Int,
)

@Serializable
data class ConfirmMembershipOrderRequest(
    val transactionId: String,
)

@Serializable
data class ConfirmShopOrderRequest(
    val transactionId: String? = null,
)

@Serializable
data class CoreValueDto(
    val code: String,
    val nameZh: String,
    val nameEn: String,
    val sortOrder: Int,
    val learnMorePageSlug: String? = null,
)

@Serializable
data class CrawlerAgentDto(
    val userAgent: String,
    val allowed: Boolean,
)

@Serializable
data class CreateMembershipOrderRequest(
    val planCode: String,
)

@Serializable
data class FanEventArticlePublicDto(
    val slug: String,
    val title: String? = null,
)

@Serializable
data class FanEventDetailDto(
    val event: FanEventListItemDto,
    val description: String? = null,
    val venueName: String? = null,
    val images: List<FanEventImagePublicDto>,
    val articles: List<FanEventArticlePublicDto>,
    val myRegistration: FanEventMyRegistrationDto? = null,
)

@Serializable
data class FanEventImagePublicDto(
    val imageUrl: String? = null,
    val imageThumbUrl: String? = null,
    val width: Int? = null,
    val height: Int? = null,
)

@Serializable
data class FanEventListItemDto(
    val slug: String,
    val name: String? = null,
    val location: String? = null,
    val startsAt: JsonElement? = null,
    val endsAt: JsonElement? = null,
    val registrationDeadlineAt: JsonElement? = null,
    val capacity: Int? = null,
    val spotsLeft: Int? = null,
    val isPaidMembersOnly: Boolean,
    val isRegistrationOpen: Boolean,
    val isFull: Boolean,
    val phase: String,
    val coverUrl: String? = null,
    val coverThumbUrl: String? = null,
)

@Serializable
data class FanEventMyRegistrationDto(
    val status: String,
    val statusLabel: String,
)

@Serializable
data class FanEventRegisterRequest(
    val applicantName: String? = null,
    val phone: String? = null,
    val email: String? = null,
    val note: String? = null,
)

@Serializable
data class FanEventRegistrationResultDto(
    val status: String,
    val statusLabel: String,
    val isWaitlisted: Boolean,
)

@Serializable
data class FaqCategoryDto(
    val id: String,
    val slug: String,
    val sortOrder: Int,
    val name: String? = null,
)

@Serializable
data class FaqFeedbackRequest(
    val helpful: Boolean,
)

@Serializable
data class FaqListItemDto(
    val id: String,
    val isFallbackLocale: Boolean,
    val slug: String,
    val isShared: Boolean,
    val sortOrder: Int,
    val question: String? = null,
    val answer: String? = null,
    val categorySlugs: List<String>,
)

@Serializable
data class FaqSearchMissRequest(
    val keyword: String,
)

@Serializable
data class HomeSectionDto(
    val sectionCode: String,
    val isEnabled: Boolean,
    val sortOrder: Int,
    val featuredBannerId: String? = null,
)

@Serializable
data class ImpactMetricDto(
    val name: String? = null,
    val unit: String? = null,
    val value: Int? = null,
    val programSlug: String? = null,
)

@Serializable
data class ImpactRecordDto(
    val id: String,
    val happenedOn: String? = null,
    val charityName: String? = null,
    val charityLogoUrl: String? = null,
    val donationContent: String? = null,
    val location: String? = null,
    val briefDescription: String? = null,
    val imageUrl: String? = null,
    val imageWidth: Int? = null,
    val imageHeight: Int? = null,
    val images: List<PublicCharityImageDto>,
    val programSlug: String? = null,
    val programName: String? = null,
)

@Serializable
data class ImpactSummaryDto(
    val metrics: List<ImpactMetricDto>,
    val charityCount: Int,
    val donationItemCount: Int,
    val regions: List<String>,
    val charities: List<PublicCharityOrgDto>,
)

@Serializable
data class LookupShopOrderRequest(
    val orderNo: String? = null,
    val email: String? = null,
    val token: String? = null,
)

@Serializable
data class MatchDto(
    val id: String,
    val seasonCode: String,
    val teamCode: String,
    val clubCode: String,
    val competitionCode: String? = null,
    val matchOn: String,
    val kickoff: String? = null,
    val kickoffAt: JsonElement? = null,
    val homeAway: String? = null,
    val opponent: String? = null,
    val isFallbackLocale: Boolean,
    val venue: String? = null,
    val competitionTag: String? = null,
    val competitionName: String? = null,
    val status: String? = null,
    val scoreHome: Int? = null,
    val scoreAway: Int? = null,
    val roundNo: Int? = null,
    val matchNo: Int? = null,
    val originalMatchOn: String? = null,
    val originalKickoff: String? = null,
    val schemaEligible: Boolean,
)

@Serializable
data class MemberCardDto(
    val id: String,
    val membershipId: String,
    val club: MemberClubBrandDto,
    val memberNo: String,
    val holderName: String,
    val tier: String,
    val tierLabel: String,
    val validUntil: String? = null,
    val status: String,
    val statusLabel: String,
    val isValid: Boolean,
    val token: String,
    val reissueCount: Int,
    val issuedAt: JsonElement? = null,
    val serverTime: JsonElement,
)

@Serializable
data class MemberChangePasswordRequest(
    val currentPassword: String? = null,
    val newPassword: String,
    val tokenDelivery: String? = null,
    val deviceInstallId: String? = null,
)

@Serializable
data class MemberClubBrandDto(
    val code: String,
    val name: String,
)

@Serializable
data class MemberDeleteAccountRequest(
    val password: String? = null,
    val confirm: String? = null,
)

@Serializable
data class MemberDeviceDto(
    val deviceId: String,
    val platform: String,
    val osVersion: String? = null,
    val appVersion: String? = null,
    val lastActiveAt: JsonElement,
    val hasActiveSession: Boolean,
)

@Serializable
data class MemberDrawAnnouncementDto(
    val slug: String,
    val categoryCode: String,
)

@Serializable
data class MemberDrawClubDto(
    val code: String,
    val name: String? = null,
)

@Serializable
data class MemberDrawDto(
    val id: String,
    val drawCode: String,
    val club: MemberDrawClubDto,
    val name: String? = null,
    val prizeDescription: String? = null,
    val rules: String? = null,
    val notes: String? = null,
    val isFallbackLocale: Boolean,
    val coverUrl: String? = null,
    val occasion: String? = null,
    val occasionLabel: String? = null,
    val snapshotAt: JsonElement? = null,
    val drawnAt: JsonElement? = null,
    val claimDeadlineOn: String? = null,
    val status: String,
    val statusLabel: String,
    val isEligible: Boolean,
    val announcement: MemberDrawAnnouncementDto? = null,
)

@Serializable
data class MemberForgotPasswordRequest(
    val email: String,
    val club: String,
    val lang: String? = null,
)

@Serializable
data class MemberGuardianConsentRequest(
    val consented: Boolean,
    val guardianName: String? = null,
    val relationship: String? = null,
    val consentTextVersion: String? = null,
)

@Serializable
data class MemberJerseyDto(
    val id: String,
    val clubCode: String,
    val membershipId: String,
    val recipientName: String,
    val phone: String? = null,
    val size: String? = null,
    val deliveryMethod: String? = null,
    val deliveryMethodLabel: String? = null,
    val address: String? = null,
    val status: String,
    val statusLabel: String,
    val shippedOn: String? = null,
    val receivedOn: String? = null,
    val editable: Boolean,
)

@Serializable
data class MemberJerseyGroupDto(
    val membershipId: String,
    val clubCode: String,
    val clubName: String,
    val seasonCode: String,
    val quota: Int,
    val used: Int,
    val canRegister: Boolean,
    val items: List<MemberJerseyDto>,
)

@Serializable
data class MemberJerseyRequest(
    val membershipId: String,
    val recipientName: String,
    val size: String,
    val deliveryMethod: String,
    val phone: String? = null,
    val address: String? = null,
)

@Serializable
data class MemberJerseyUpdateRequest(
    val recipientName: String,
    val size: String,
    val deliveryMethod: String,
    val phone: String? = null,
    val address: String? = null,
)

@Serializable
data class MemberJoinableClubDto(
    val code: String,
    val name: String,
)

@Serializable
data class MemberLineAuthorizeDto(
    val authorizeUrl: String,
    val state: String,
)

@Serializable
data class MemberLineAuthorizeRequest(
    val club: String,
    val mode: String,
    val redirectUri: String? = null,
)

@Serializable
data class MemberLineCallbackDto(
    val status: String,
    val session: MemberSessionDto? = null,
    val ticket: String? = null,
    val displayName: String? = null,
    val suggestedEmail: String? = null,
)

@Serializable
data class MemberLineCallbackRequest(
    val code: String,
    val state: String,
    val tokenDelivery: String? = null,
    val deviceInstallId: String? = null,
)

@Serializable
data class MemberLineCompleteRequest(
    val club: String,
    val ticket: String,
    val email: String,
    val name: String? = null,
    val phone: String? = null,
    val birthOn: String? = null,
    val lang: String? = null,
    val tokenDelivery: String? = null,
    val deviceInstallId: String? = null,
    val guardianConsent: MemberGuardianConsentRequest? = null,
)

@Serializable
data class MemberLoginRequest(
    val email: String,
    val password: String,
    val rememberMe: Boolean? = null,
    val tokenDelivery: String? = null,
    val deviceInstallId: String? = null,
)

@Serializable
data class MemberPendingOrderDto(
    val orderNo: String,
    val status: String,
    val statusLabel: String,
)

@Serializable
data class MemberProfileDto(
    val memberNo: String,
    val name: String,
    val email: String,
    val phone: String? = null,
    val birthOn: String? = null,
    val locale: String? = null,
    val emailVerified: Boolean,
    val hasPassword: Boolean,
    val lineBound: Boolean,
    val signupSource: String,
    val signupSourceLabel: String,
    val createdAt: JsonElement,
)

@Serializable
data class MemberRefreshRequest(
    val refreshToken: String? = null,
    val tokenDelivery: String? = null,
)

@Serializable
data class MemberRegisterRequest(
    val club: String,
    val email: String,
    val password: String,
    val name: String,
    val phone: String? = null,
    val birthOn: String? = null,
    val lang: String? = null,
    val guardianConsent: MemberGuardianConsentRequest? = null,
)

@Serializable
data class MemberRegisteredDto(
    val memberNo: String,
    val emailVerificationRequired: Boolean,
    val emailSent: Boolean,
)

@Serializable
data class MemberRegistrationCourseDto(
    val programSlug: String,
    val programName: String? = null,
    val startOn: String? = null,
    val endOn: String? = null,
    val weeklySchedule: String? = null,
    val venueName: String? = null,
    val venueAddress: String? = null,
    val price: Int? = null,
    val earlyBirdPrice: Int? = null,
    val earlyBirdUntil: String? = null,
    val sessionStatusCode: String,
    val sessionStatusLabelZh: String,
    val sessionStatusLabelEn: String,
)

@Serializable
data class MemberRegistrationDto(
    val id: String,
    val registrationNo: String,
    val clubCode: String,
    val status: String,
    val statusCode: String,
    val statusLabelZh: String,
    val statusLabelEn: String,
    val applicantName: String,
    val sessionId: String? = null,
    val trialId: String? = null,
    val createdAt: JsonElement,
    val kind: String,
    val course: MemberRegistrationCourseDto? = null,
    val trial: MemberRegistrationTrialDto? = null,
    val isFallbackLocale: Boolean,
)

@Serializable
data class MemberRegistrationTrialDto(
    val trialOn: String,
    val teamName: String? = null,
    val venueName: String? = null,
    val venueAddress: String? = null,
    val trialStatusCode: String,
    val trialStatusLabelZh: String,
    val trialStatusLabelEn: String,
)

@Serializable
data class MemberResendVerificationRequest(
    val email: String,
    val club: String,
    val lang: String? = null,
)

@Serializable
data class MemberResetPasswordRequest(
    val token: String,
    val newPassword: String,
)

@Serializable
data class MemberSessionDto(
    val accessToken: String,
    val accessTokenExpiresAt: JsonElement,
    val refreshToken: String? = null,
    val refreshTokenExpiresAt: JsonElement? = null,
    val member: MemberSummaryDto,
)

@Serializable
data class MemberSummaryDto(
    val memberNo: String,
    val name: String,
    val emailVerified: Boolean,
    val hasPassword: Boolean,
    val lineBound: Boolean,
)

@Serializable
data class MemberUpdateProfileRequest(
    val name: String,
    val phone: String? = null,
    val birthOn: String? = null,
    val locale: String? = null,
)

@Serializable
data class MemberVerifyEmailRequest(
    val token: String,
)

@Serializable
data class MembershipOrderDto(
    val orderNo: String,
    val clubCode: String,
    val planCode: String,
    val planName: String? = null,
    val seasonCode: String,
    val amount: Int,
    val status: String,
    val statusLabel: String,
    val paymentMethod: String? = null,
    val paymentUrl: String? = null,
    val expiresAt: JsonElement? = null,
    val paidAt: JsonElement? = null,
    val activatedAt: JsonElement? = null,
    val membershipId: String? = null,
    val createdAt: JsonElement,
    val canPayOnline: Boolean,
    val canCancel: Boolean,
)

@Serializable
data class MembershipPlanPublicDto(
    val code: String,
    val name: String? = null,
    val isFallbackLocale: Boolean,
    val benefitNote: String? = null,
    val fee: Int,
    val cardQuota: Int,
    val jerseyQuota: Int,
    val midSeasonRule: String? = null,
    val seasonCode: String,
    val startsOn: String,
    val endsOn: String,
)

@Serializable
data class MilestoneDto(
    val id: String,
    val happenedOn: String,
    val title: String? = null,
    val description: String? = null,
    val imageUrl: String? = null,
    val imageAlt: String? = null,
    val imageWidth: Int? = null,
    val imageHeight: Int? = null,
)

@Serializable
data class MyMembershipDto(
    val id: String,
    val club: MemberClubBrandDto,
    val seasonCode: String,
    val tier: String,
    val tierLabel: String,
    val status: String,
    val statusLabel: String,
    val startOn: String? = null,
    val endOn: String? = null,
    val planCode: String? = null,
    val planName: String? = null,
    val cardQuota: Int,
    val jerseyQuota: Int,
    val isCurrentSeason: Boolean,
    val renewalDue: Boolean,
    val pendingOrder: MemberPendingOrderDto? = null,
    val cards: List<MemberCardDto>,
)

@Serializable
data class MyMembershipsDto(
    val memberships: List<MyMembershipDto>,
    val joinableClubs: List<MemberJoinableClubDto>,
)

@Serializable
data class NewsletterSubscribeResultDto(
    val status: String? = null,
)

@Serializable
data class NewsletterUnsubscribeResultDto(
    val changed: Boolean,
)

@Serializable
data class PageBlockPublicDto(
    val blockType: String,
    val content: JsonElement,
    val sortOrder: Int,
)

@Serializable
data class PageDetailDto(
    val id: String,
    val slug: String,
    val seoTitle: String? = null,
    val seoDescription: String? = null,
    val seoKeywords: String? = null,
    val canonicalPath: String? = null,
    val isNoindex: Boolean? = null,
    val ogImageUrl: String? = null,
    val ogImageWidth: Int? = null,
    val ogImageHeight: Int? = null,
    val ogImageAlt: String? = null,
    val publishedAt: JsonElement? = null,
    val blocks: List<PageBlockPublicDto>,
)

@Serializable
data class PagePreviewDto(
    val pageId: String,
    val versionNo: Int,
    val status: String,
    val slug: String,
    val seoTitle: String? = null,
    val seoDescription: String? = null,
    val blocks: List<PageBlockPublicDto>,
)

@Serializable
data class PagedResultOfArticleListItemDto(
    val items: List<ArticleListItemDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfCharityProgramListItemDto(
    val items: List<CharityProgramListItemDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfFaqListItemDto(
    val items: List<FaqListItemDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfImpactRecordDto(
    val items: List<ImpactRecordDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfMatchDto(
    val items: List<MatchDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfPlayerDto(
    val items: List<PlayerDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfPressResourceDto(
    val items: List<PressResourceDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfProgramListItemDto(
    val items: List<ProgramListItemDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfPublicCalendarEventDto(
    val items: List<PublicCalendarEventDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfShopProductListItemDto(
    val items: List<ShopProductListItemDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PagedResultOfStaffDto(
    val items: List<StaffDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val totalPages: Int? = null,
)

@Serializable
data class PartnerCharityProgramDto(
    val slug: String,
    val name: String? = null,
)

@Serializable
data class PartnerDto(
    val id: String,
    val slug: String,
    val isFallbackLocale: Boolean,
    val partnerType: String? = null,
    val country: String? = null,
    val startOn: String? = null,
    val endOn: String? = null,
    val websiteUrl: String? = null,
    val showInFooter: Boolean,
    val showOnHome: Boolean,
    val sortOrder: Int,
    val name: String? = null,
    val content: String? = null,
    val logoDarkUrl: String? = null,
    val logoLightUrl: String? = null,
    val charityPrograms: List<PartnerCharityProgramDto>,
)

@Serializable
data class PartnerStoreFiltersDto(
    val categories: List<String>,
    val regions: List<String>,
)

@Serializable
data class PartnerStorePublicDto(
    val slug: String,
    val name: String? = null,
    val isFallbackLocale: Boolean,
    val category: String? = null,
    val region: String? = null,
    val address: String? = null,
    val lat: Double? = null,
    val lng: Double? = null,
    val phone: String? = null,
    val businessHours: String? = null,
    val offerContent: String? = null,
    val applicableTier: String,
    val applicableTierLabel: String,
    val mapUrl: String? = null,
    val websiteUrl: String? = null,
    val imageUrl: String? = null,
    val isShared: Boolean,
)

@Serializable
data class PlayerCareerStatDto(
    val seasonCode: String,
    val appearances: Int,
    val goals: Int,
    val assists: Int? = null,
    val yellowCards: Int,
    val redCards: Int,
    val source: String,
)

@Serializable
data class PlayerCareerStatsDto(
    val playerId: String,
    val seasons: List<PlayerCareerStatDto>,
)

@Serializable
data class PlayerDto(
    val id: String,
    val status: String? = null,
    val slug: String,
    val isFallbackLocale: Boolean,
    val teamCode: String,
    val shirtNo: Int? = null,
    val position: String? = null,
    val birthOn: String? = null,
    val heightCm: Int? = null,
    val weightKg: Int? = null,
    val nationality: String? = null,
    val preferredFoot: String? = null,
    val photoKey: String? = null,
    val portraitConsented: Boolean,
    val name: String? = null,
    val bio: String? = null,
    val photoUrl: String? = null,
    val schemaEligible: Boolean,
)

@Serializable
data class PlayerSeasonStatDto(
    val playerId: String,
    val name: String? = null,
    val teamCode: String,
    val shirtNo: Int? = null,
    val position: String? = null,
    val photoUrl: String? = null,
    val appearances: Int,
    val goals: Int,
    val assists: Int? = null,
    val yellowCards: Int,
    val redCards: Int,
    val source: String,
)

@Serializable
data class PlayerStatsDto(
    val season: SeasonRefDto? = null,
    val seasons: List<String>,
    val items: List<PlayerSeasonStatDto>,
)

@Serializable
data class PressResourceDto(
    val id: String,
    val slug: String,
    val resourceType: String,
    val title: String? = null,
    val description: String? = null,
    val publishedOn: String? = null,
    val fileBytes: Int? = null,
    val fileExtension: String? = null,
    val coverUrl: String? = null,
    val downloadPath: String,
)

@Serializable
data class ProgramDetailDto(
    val id: String,
    val slug: String,
    val isFallbackLocale: Boolean,
    val programType: String? = null,
    val audience: String? = null,
    val ageMin: Int? = null,
    val ageMax: Int? = null,
    val coverKey: String? = null,
    val coverUrl: String? = null,
    val name: String? = null,
    val intro: String? = null,
    val content: String? = null,
    val staff: List<ProgramStaffSummaryDto>,
    val partners: List<ProgramPartnerSummaryDto>,
    val sessions: List<ProgramSessionDto>,
)

@Serializable
data class ProgramListItemDto(
    val id: String,
    val slug: String,
    val isFallbackLocale: Boolean,
    val programType: String? = null,
    val audience: String? = null,
    val ageMin: Int? = null,
    val ageMax: Int? = null,
    val coverKey: String? = null,
    val coverUrl: String? = null,
    val name: String? = null,
    val intro: String? = null,
    val hasOpenSession: Boolean,
)

@Serializable
data class ProgramPartnerSummaryDto(
    val id: String,
    val slug: String,
    val name: String? = null,
    val logoDarkKey: String? = null,
    val logoLightKey: String? = null,
    val logoDarkUrl: String? = null,
    val logoLightUrl: String? = null,
    val websiteUrl: String? = null,
)

@Serializable
data class ProgramRegistrationSubmittedDto(
    val registrationNo: String,
    val status: String,
    val statusCode: String,
    val statusLabelZh: String,
    val statusLabelEn: String,
)

@Serializable
data class ProgramSessionDto(
    val id: String,
    val startOn: String? = null,
    val endOn: String? = null,
    val weeklySchedule: String? = null,
    val capacity: Int? = null,
    val enrolledCount: Int,
    val price: Int? = null,
    val earlyBirdPrice: Int? = null,
    val earlyBirdUntil: String? = null,
    val signupOpensAt: JsonElement? = null,
    val signupClosesAt: JsonElement? = null,
    val status: String,
    val statusCode: String,
    val statusLabelZh: String,
    val statusLabelEn: String,
    val venueId: String? = null,
    val venueName: String? = null,
    val venueAddress: String? = null,
    val venueLat: Double? = null,
    val venueLng: Double? = null,
)

@Serializable
data class ProgramStaffSummaryDto(
    val id: String,
    val name: String? = null,
)

@Serializable
data class ProposalDownloadRequest(
    val company: String? = null,
    val name: String? = null,
    val email: String? = null,
    val consent: Boolean? = null,
    val lang: String? = null,
    val sourcePath: String? = null,
    val utmSource: String? = null,
    val utmCampaign: String? = null,
    val website: String? = null,
)

@Serializable
data class ProposalDownloadResultDto(
    val downloadPath: String? = null,
    val expiresAt: JsonElement? = null,
)

@Serializable
data class PublicCalendarEventDto(
    val sourceType: String,
    val id: String,
    val startsAt: JsonElement,
    val endsAt: JsonElement? = null,
    val isAllDay: Boolean,
    val title: String,
    val teamCodes: List<String>,
    val venueName: String? = null,
    val seasonCode: String? = null,
    val competitionTag: String? = null,
    val competitionName: String? = null,
    val status: String? = null,
    val homeAway: String? = null,
    val scoreHome: Int? = null,
    val scoreAway: Int? = null,
    val roundNo: Int? = null,
    val matchNo: Int? = null,
    val originalMatchOn: String? = null,
    val originalKickoff: String? = null,
    val eventTypeCode: String? = null,
    val eventTypeName: String? = null,
    val eventTypeColour: String? = null,
    val eventTypeIcon: String? = null,
    val isRecurring: Boolean? = null,
    val occurrenceId: String? = null,
    val description: String? = null,
    val ctaUrl: String? = null,
    val coverKey: String? = null,
    val coverUrl: String? = null,
)

@Serializable
data class PublicCalendarEventTypeDto(
    val code: String,
    val name: String,
    val colour: String? = null,
    val icon: String? = null,
)

@Serializable
data class PublicCalendarSettingsDto(
    val defaultView: String,
    val defaultRange: String,
    val defaultTeamCode: String,
    val homeTeamCodes: List<String>,
    val firstTeamCode: String? = null,
    val teams: List<PublicCalendarTeamDto>,
    val eventTypes: List<PublicCalendarEventTypeDto>,
)

@Serializable
data class PublicCalendarTeamDto(
    val code: String,
    val displayName: String,
    val colour: String? = null,
    val sortOrder: Int,
)

@Serializable
data class PublicCharityImageDto(
    val imageUrl: String,
    val thumbUrl: String? = null,
)

@Serializable
data class PublicCharityOrgDto(
    val slug: String,
    val name: String? = null,
    val intro: String? = null,
    val logoUrl: String? = null,
    val websiteUrl: String? = null,
)

@Serializable
data class PublicCrawlerSettingsDto(
    val userAgents: List<CrawlerAgentDto>,
    val excludePaths: List<String>,
)

@Serializable
data class PublicFormDto(
    val formCode: String,
    val formNameZh: String,
    val formNameEn: String,
    val captchaEnabled: Boolean,
    val redirectPath: String? = null,
    val fields: List<PublicFormFieldDto>,
)

@Serializable
data class PublicFormFieldDto(
    val fieldKey: String,
    val fieldType: String,
    val label: String,
    val isRequired: Boolean,
    val validationRule: String? = null,
    val options: List<String>? = null,
    val optionLabels: List<String>? = null,
    val sortOrder: Int,
)

@Serializable
data class PublicFormatsDto(
    val dateFormat: String? = null,
    val numberFormat: String? = null,
    val thousandsSeparator: String? = null,
    val decimalSeparator: String? = null,
)

@Serializable
data class PublicLanguageDto(
    val code: String,
    val name: String,
    val isDefault: Boolean,
    val fallbackCode: String? = null,
)

@Serializable
data class PublicLlmsContentDto(
    val positioningZh: String? = null,
    val positioningEn: String? = null,
    val keyPagesZh: String? = null,
    val keyPagesEn: String? = null,
    val factsSummaryZh: String? = null,
    val factsSummaryEn: String? = null,
    val licenseZh: String? = null,
    val licenseEn: String? = null,
    val contactZh: String? = null,
    val contactEn: String? = null,
)

@Serializable
data class PublicMaintenanceDto(
    val enabled: Boolean,
    val message: String? = null,
)

@Serializable
data class PublicMenuItemDto(
    val id: String,
    val label: String,
    val url: String? = null,
    val isExternal: Boolean,
    val children: List<PublicMenuItemDto>,
)

@Serializable
data class PublicMenusDto(
    val main: List<PublicMenuItemDto>,
    val mega: List<PublicMenuItemDto>,
    val footer: List<PublicMenuItemDto>,
)

@Serializable
data class PublicPolicyDto(
    val code: String,
    val title: String,
    val body: String,
    val updatedAt: JsonElement,
    val isFallbackLocale: Boolean,
)

@Serializable
data class PublicPolicyIndexDto(
    val code: String,
    val title: String,
    val hasContent: Boolean,
)

@Serializable
data class PublicProposalDto(
    val id: String,
    val title: String,
    val versionNo: Int,
    val locales: List<String>,
)

@Serializable
data class PublicRedirectDto(
    val fromPath: String,
    val toPath: String,
)

@Serializable
data class PublicSeoSettingsDto(
    val titleTemplateZh: String? = null,
    val titleTemplateEn: String? = null,
    val defaultDescriptionZh: String? = null,
    val defaultDescriptionEn: String? = null,
    val robotsCustomRules: String? = null,
    val ga4MeasurementId: String? = null,
    val gtmContainerId: String? = null,
    val metaPixelId: String? = null,
    val lineTagId: String? = null,
    val ogImageUrl: String? = null,
    val ogImageWidth: Int? = null,
    val ogImageHeight: Int? = null,
)

@Serializable
data class PublicSiteFactContactDto(
    val address: String? = null,
    val phone: String? = null,
    val hours: String? = null,
    val email: String? = null,
    val departments: List<PublicSiteFactDepartmentDto>,
)

@Serializable
data class PublicSiteFactDepartmentDto(
    val name: String,
    val email: String? = null,
    val phoneExtension: String? = null,
)

@Serializable
data class PublicSiteFactLeagueDto(
    val name: String? = null,
    val shortName: String? = null,
)

@Serializable
data class PublicSiteFactSocialDto(
    val facebook: String? = null,
    val instagram: String? = null,
    val youtube: String? = null,
    val line: String? = null,
)

@Serializable
data class PublicSiteFactVenueDto(
    val name: String,
    val address: String? = null,
    val isHomeGround: Boolean,
)

@Serializable
data class PublicSiteFactsDto(
    val foundedYear: String? = null,
    val foundingDateIso: String? = null,
    val foundedDisplay: String? = null,
    val foundingTitle: String? = null,
    val league: PublicSiteFactLeagueDto,
    val venues: List<PublicSiteFactVenueDto>,
    val squadStructureSummary: String? = null,
    val squadCodes: List<String>,
    val contact: PublicSiteFactContactDto,
    val blueWhaleSiteUrl: String? = null,
    val social: PublicSiteFactSocialDto,
    val footerBlurb: String? = null,
)

@Serializable
data class PublicSiteSettingsDto(
    val maintenance: PublicMaintenanceDto,
    val languages: List<PublicLanguageDto>,
    val fallbackMode: String,
    val formats: PublicFormatsDto,
    val policies: List<PublicPolicyIndexDto>,
)

@Serializable
data class PublicTrialDto(
    val id: String,
    val trialOn: String,
    val teamCode: String? = null,
    val teamName: String? = null,
    val audience: String? = null,
    val venueId: String? = null,
    val venueName: String? = null,
    val venueAddress: String? = null,
    val venueLat: Double? = null,
    val venueLng: Double? = null,
    val capacity: Int? = null,
    val enrolledCount: Int,
    val deadlineOn: String? = null,
    val status: String,
    val statusCode: String,
    val statusLabelZh: String,
    val statusLabelEn: String,
    val isSignupOpen: Boolean,
    val acceptsWaitlist: Boolean,
)

@Serializable
data class PublicUiStringsDto(
    val locale: String,
    val strings: Map<String, String>,
)

@Serializable
data class PublicVenueDto(
    val id: String,
    val name: String? = null,
    val address: String? = null,
    val directions: String? = null,
    val lat: Double? = null,
    val lng: Double? = null,
    val photoUrl: String? = null,
    val photoWidth: Int? = null,
    val photoHeight: Int? = null,
    val photoAlt: String? = null,
    val isHome: Boolean,
)

@Serializable
data class RegisterAppDeviceRequest(
    val platform: String,
    val osVersion: String? = null,
    val appVersion: String? = null,
    val locale: String? = null,
    val pushToken: String? = null,
    val pushPermission: String? = null,
)

@Serializable
data class SearchFacetDto(
    val type: String,
    val label: String,
    val count: Int,
)

@Serializable
data class SearchResponseDto(
    val query: String,
    val tokens: List<String>,
    val items: List<SearchResultItemDto>,
    val page: Int,
    val pageSize: Int,
    val totalCount: Int,
    val facets: List<SearchFacetDto>,
    val truncated: Boolean,
    val isEmpty: Boolean,
)

@Serializable
data class SearchResultItemDto(
    val type: String,
    val subType: String? = null,
    val id: String,
    val slug: String? = null,
    val title: String,
    val snippet: String? = null,
    val date: JsonElement? = null,
    val categoryCode: String? = null,
    val teamCode: String? = null,
    val imageUrl: String? = null,
    val isFallbackLocale: Boolean,
)

@Serializable
data class SeasonRefDto(
    val code: String,
    val startOn: String,
    val endOn: String,
)

@Serializable
data class SetCartItemRequest(
    val quantity: Int? = null,
)

@Serializable
data class ShopCartDto(
    val cartToken: String? = null,
    val clubCode: String,
    val items: List<ShopCartItemDto>,
    val itemCount: Int,
    val subtotal: Int,
    val shipping: ShopCartShippingDto,
    val canCheckout: Boolean,
)

@Serializable
data class ShopCartItemDto(
    val variantId: String,
    val productSlug: String,
    val productName: String? = null,
    val variantLabel: String,
    val sku: String,
    val imageThumbUrl: String? = null,
    val listPrice: Int,
    val unitPrice: Int,
    val onSale: Boolean,
    val quantity: Int,
    val lineTotal: Int,
    val availableQty: Int,
    val purchasable: Boolean,
    val issue: String? = null,
    val issueMessage: String? = null,
)

@Serializable
data class ShopCartShippingDto(
    val fee: Int,
    val freeThreshold: Int? = null,
    val amountToFree: Int? = null,
)

@Serializable
data class ShopCollectionDto(
    val slug: String,
    val name: String? = null,
    val narrative: String? = null,
    val productCount: Int,
)

@Serializable
data class ShopDeliveryMethodDto(
    val code: String,
    val label: String,
    val baseFee: Int,
)

@Serializable
data class ShopDonationCodeDto(
    val code: String,
    val orgName: String,
)

@Serializable
data class ShopImageDto(
    val url: String,
    val thumbUrl: String,
    val width: Int? = null,
    val height: Int? = null,
)

@Serializable
data class ShopInfoDto(
    val entryTitle: String? = null,
    val entryIntro: String? = null,
    val policyNotice: String? = null,
    val policyShipping: String? = null,
    val policyReturns: String? = null,
    val policyTerms: String? = null,
    val shippingFee: Int,
    val freeShippingThreshold: Int? = null,
    val excludedRegions: List<String>,
    val deliveryMethods: List<ShopDeliveryMethodDto>,
    val donationCodes: List<ShopDonationCodeDto>,
    val collectingSubjectName: String? = null,
    val paymentAvailable: Boolean,
    val collections: List<ShopCollectionDto>,
)

@Serializable
data class ShopOrderDto(
    val orderNo: String,
    val clubCode: String,
    val accessToken: String? = null,
    val status: String,
    val paymentStatus: String,
    val paymentStatusLabel: String,
    val paymentMethod: String,
    val paymentMethodLabel: String,
    val deliveryMethod: String,
    val deliveryMethodLabel: String,
    val subtotal: Int,
    val shippingFee: Int,
    val total: Int,
    val items: List<ShopOrderItemDto>,
    val recipientName: String? = null,
    val recipientPhone: String? = null,
    val recipientAddress: String? = null,
    val buyerEmail: String? = null,
    val customerNote: String? = null,
    val isMasked: Boolean,
    val invoice: ShopOrderInvoiceDto? = null,
    val shipment: ShopOrderShipmentDto? = null,
    val paymentUrl: String? = null,
    val expiresAt: JsonElement? = null,
    val canPay: Boolean,
    val canCancel: Boolean,
    val createdAt: JsonElement,
    val paidAt: JsonElement? = null,
)

@Serializable
data class ShopOrderInvoiceDto(
    val type: String,
    val typeLabel: String,
    val status: String,
    val statusLabel: String,
    val invoiceNo: String? = null,
    val issuedAt: JsonElement? = null,
    val taxId: String? = null,
    val donationCode: String? = null,
)

@Serializable
data class ShopOrderItemDto(
    val productName: String,
    val variantLabel: String? = null,
    val sku: String,
    val unitPrice: Int,
    val quantity: Int,
    val lineTotal: Int,
)

@Serializable
data class ShopOrderListItemDto(
    val orderNo: String,
    val status: String,
    val paymentStatusLabel: String,
    val total: Int,
    val itemCount: Int,
    val firstItemName: String? = null,
    val invoiceNo: String? = null,
    val trackingNo: String? = null,
    val createdAt: JsonElement,
)

@Serializable
data class ShopOrderShipmentDto(
    val carrier: String? = null,
    val trackingNo: String? = null,
    val shippedAt: JsonElement? = null,
    val deliveredAt: JsonElement? = null,
    val pickupStatus: String? = null,
    val pickupStatusLabel: String? = null,
    val pickupDeadlineOn: String? = null,
)

@Serializable
data class ShopProductDetailDto(
    val slug: String,
    val name: String? = null,
    val narrative: String? = null,
    val seoTitle: String? = null,
    val seoDescription: String? = null,
    val collectionSlug: String? = null,
    val collectionName: String? = null,
    val tags: List<String>,
    val isNewArrival: Boolean,
    val sizeChart: JsonElement? = null,
    val images: List<ShopImageDto>,
    val variants: List<ShopVariantDto>,
    val priceMin: Int? = null,
    val priceMax: Int? = null,
    val listPriceMin: Int? = null,
    val onSale: Boolean,
    val stockStatus: String,
    val stockStatusLabel: String,
)

@Serializable
data class ShopProductListItemDto(
    val slug: String,
    val name: String? = null,
    val collectionSlug: String? = null,
    val collectionName: String? = null,
    val tags: List<String>,
    val isNewArrival: Boolean,
    val imageUrl: String? = null,
    val imageThumbUrl: String? = null,
    val priceMin: Int? = null,
    val priceMax: Int? = null,
    val listPriceMin: Int? = null,
    val onSale: Boolean,
    val stockStatus: String,
    val stockStatusLabel: String,
    val sizes: List<String>,
    val colours: List<String>,
)

@Serializable
data class ShopVariantDto(
    val id: String,
    val sku: String,
    val size: String? = null,
    val colour: String? = null,
    val label: String,
    val listPrice: Int,
    val price: Int,
    val onSale: Boolean,
    val availableQty: Int,
    val purchasable: Boolean,
)

@Serializable
data class SitemapEntryDto(
    val path: String,
    val lastModifiedAt: JsonElement? = null,
)

@Serializable
data class SponsorActivationDto(
    val id: String,
    val title: String? = null,
    val happenedOn: String? = null,
    val resultSummary: String? = null,
    val images: List<SponsorActivationImageDto>,
)

@Serializable
data class SponsorActivationImageDto(
    val imageUrl: String,
    val thumbUrl: String? = null,
    val imageWidth: Int? = null,
    val imageHeight: Int? = null,
)

@Serializable
data class SponsorDto(
    val id: String,
    val slug: String,
    val isFallbackLocale: Boolean,
    val tier: String? = null,
    val sortOrder: Int,
    val name: String? = null,
    val content: String? = null,
    val logoDarkUrl: String? = null,
    val logoLightUrl: String? = null,
    val stories: List<SponsorStoryDto>,
    val activations: List<SponsorActivationDto>,
    val charityPrograms: List<PartnerCharityProgramDto>,
)

@Serializable
data class SponsorPackageDto(
    val id: String,
    val slug: String,
    val isFallbackLocale: Boolean,
    val sortOrder: Int,
    val name: String? = null,
    val content: String? = null,
    val benefitList: String? = null,
    val audience: String? = null,
    val priceMin: Int? = null,
    val priceMax: Int? = null,
)

@Serializable
data class SponsorStoryDto(
    val slug: String,
    val title: String? = null,
    val summary: String? = null,
)

@Serializable
data class StaffDto(
    val id: String,
    val isFallbackLocale: Boolean,
    val staffGroup: String? = null,
    val licence: String? = null,
    val photoKey: String? = null,
    val portraitConsented: Boolean,
    val name: String? = null,
    val title: String? = null,
    val bio: String? = null,
    val teamCodes: List<String>,
    val isShared: Boolean,
    val photoUrl: String? = null,
    val schemaEligible: Boolean,
)

@Serializable
data class StandingRowDto(
    val rank: Int? = null,
    val teamName: String,
    val played: Int? = null,
    val points: Int? = null,
)

@Serializable
data class StandingsDto(
    val season: SeasonRefDto? = null,
    val seasons: List<String>,
    val items: List<StandingRowDto>,
    val updatedAt: JsonElement? = null,
)

@Serializable
data class SubmitFormRequest(
    val answers: Map<String, String>,
    val sourcePath: String? = null,
    val utmSource: String? = null,
    val utmCampaign: String? = null,
    val website: String? = null,
    val lang: String? = null,
    val turnstileToken: String? = null,
)

@Serializable
data class SubmitFormResultDto(
    val success: Boolean,
)

@Serializable
data class SubmitProgramRegistrationRequest(
    val applicantName: String,
    val phone: String? = null,
    val email: String? = null,
    val birthOn: String? = null,
    val guardianName: String? = null,
    val guardianPhone: String? = null,
    val healthDeclaration: String? = null,
    val note: String? = null,
)

@Serializable
data class SubmitTrialRegistrationRequest(
    val applicantName: String? = null,
    val phone: String? = null,
    val email: String? = null,
    val birthOn: String? = null,
    val guardianName: String? = null,
    val guardianPhone: String? = null,
    val healthDeclaration: String? = null,
    val note: String? = null,
)

@Serializable
data class SubscribeNewsletterRequest(
    val email: String? = null,
    val consent: Boolean? = null,
    val source: String? = null,
    val website: String? = null,
)

@Serializable
data class TeamDto(
    val id: String,
    val code: String,
    val clubCode: String,
    val type: String,
    val gender: String,
    val ageBand: String? = null,
    val teamColor: String? = null,
    val heroKey: String? = null,
    val name: String? = null,
    val isFallbackLocale: Boolean,
    val intro: String? = null,
    val heroUrl: String? = null,
    val schemaEligible: Boolean,
)

@Serializable
data class TrialRegistrationSubmittedDto(
    val registrationNo: String,
    val status: String,
    val statusCode: String,
    val statusLabelZh: String,
    val statusLabelEn: String,
)

@Serializable
data class UnsubscribeNewsletterRequest(
    val token: String? = null,
)

@Serializable
data class UpdateAppSubscriptionsRequest(
    val items: List<AppSubscriptionInput>,
    val replaceAll: Boolean? = null,
)
