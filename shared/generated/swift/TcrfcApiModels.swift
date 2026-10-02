// 自動產生，請勿手改。來源：shared/openapi.json；產生器：shared/scripts/gen-dto.mjs（docs/19 §2）。
// 只有資料型別（DTO），沒有 client：權杖續期、冪等鍵、離線佇列、退避重試由 App 的網路層自己負責。
// 時間欄位一律是 String（伺服器的 UTC ISO 8601 字串），解析交給 App 的時間工具。

import Foundation

/// 任意 JSON 值（OpenAPI schema 為 {} 的欄位）。
public enum JSONValue: Codable, Equatable, Sendable {
    case null
    case bool(Bool)
    case number(Double)
    case string(String)
    case array([JSONValue])
    case object([String: JSONValue])

    public init(from decoder: Decoder) throws {
        let c = try decoder.singleValueContainer()
        if c.decodeNil() { self = .null }
        else if let v = try? c.decode(Bool.self) { self = .bool(v) }
        else if let v = try? c.decode(Double.self) { self = .number(v) }
        else if let v = try? c.decode(String.self) { self = .string(v) }
        else if let v = try? c.decode([JSONValue].self) { self = .array(v) }
        else { self = .object(try c.decode([String: JSONValue].self)) }
    }

    public func encode(to encoder: Encoder) throws {
        var c = encoder.singleValueContainer()
        switch self {
        case .null: try c.encodeNil()
        case .bool(let v): try c.encode(v)
        case .number(let v): try c.encode(v)
        case .string(let v): try c.encode(v)
        case .array(let v): try c.encode(v)
        case .object(let v): try c.encode(v)
        }
    }
}

public struct AchievementDto: Codable, Equatable, Sendable {
    public var id: String
    public var year: Int?
    public var seasonCode: String?
    public var teamCode: String
    public var teamName: String?
    public var competitionName: String?
    public var placing: String?

    public init(
        id: String,
        year: Int? = nil,
        seasonCode: String? = nil,
        teamCode: String,
        teamName: String? = nil,
        competitionName: String? = nil,
        placing: String? = nil
    ) {
        self.id = id
        self.year = year
        self.seasonCode = seasonCode
        self.teamCode = teamCode
        self.teamName = teamName
        self.competitionName = competitionName
        self.placing = placing
    }
}

public struct AddCartItemRequest: Codable, Equatable, Sendable {
    public var variantId: String?
    public var quantity: Int?

    public init(
        variantId: String? = nil,
        quantity: Int? = nil
    ) {
        self.variantId = variantId
        self.quantity = quantity
    }
}

public struct AppAdEventBatchRequest: Codable, Equatable, Sendable {
    public var batchId: String?
    public var deviceInstallId: String
    public var platform: String
    public var appVersion: String?
    public var locale: String?
    public var events: [AppAdEventInput]

    public init(
        batchId: String? = nil,
        deviceInstallId: String,
        platform: String,
        appVersion: String? = nil,
        locale: String? = nil,
        events: [AppAdEventInput]
    ) {
        self.batchId = batchId
        self.deviceInstallId = deviceInstallId
        self.platform = platform
        self.appVersion = appVersion
        self.locale = locale
        self.events = events
    }
}

public struct AppAdEventBatchResult: Codable, Equatable, Sendable {
    public var accepted: Int
    public var duplicates: Int
    public var rejected: [AppAdEventRejectionDto]

    public init(
        accepted: Int,
        duplicates: Int,
        rejected: [AppAdEventRejectionDto]
    ) {
        self.accepted = accepted
        self.duplicates = duplicates
        self.rejected = rejected
    }
}

public struct AppAdEventInput: Codable, Equatable, Sendable {
    public var type: String
    public var creativeId: String
    public var occurredAt: String
    public var presentationId: String?

    public init(
        type: String,
        creativeId: String,
        occurredAt: String,
        presentationId: String? = nil
    ) {
        self.type = type
        self.creativeId = creativeId
        self.occurredAt = occurredAt
        self.presentationId = presentationId
    }
}

public struct AppAdEventRejectionDto: Codable, Equatable, Sendable {
    public var index: Int
    public var reason: String

    public init(
        index: Int,
        reason: String
    ) {
        self.index = index
        self.reason = reason
    }
}

public struct AppAdItemDto: Codable, Equatable, Sendable {
    public var creativeId: String?
    public var campaignId: String?
    public var isFallback: Bool
    public var imageUrl: String?
    public var imageWidth: Int?
    public var imageHeight: Int?
    public var videoUrl: String?
    public var altText: String?
    public var title: String?
    public var ctaText: String?
    public var clickUrl: String?
    public var theme: String?

    public init(
        creativeId: String? = nil,
        campaignId: String? = nil,
        isFallback: Bool,
        imageUrl: String? = nil,
        imageWidth: Int? = nil,
        imageHeight: Int? = nil,
        videoUrl: String? = nil,
        altText: String? = nil,
        title: String? = nil,
        ctaText: String? = nil,
        clickUrl: String? = nil,
        theme: String? = nil
    ) {
        self.creativeId = creativeId
        self.campaignId = campaignId
        self.isFallback = isFallback
        self.imageUrl = imageUrl
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
        self.videoUrl = videoUrl
        self.altText = altText
        self.title = title
        self.ctaText = ctaText
        self.clickUrl = clickUrl
        self.theme = theme
    }
}

public struct AppAdResponse: Codable, Equatable, Sendable {
    public var slotCode: String
    public var isFallback: Bool
    public var disclosureLabel: String
    public var sessionImpressionCap: Int?
    public var items: [AppAdItemDto]

    public init(
        slotCode: String,
        isFallback: Bool,
        disclosureLabel: String,
        sessionImpressionCap: Int? = nil,
        items: [AppAdItemDto]
    ) {
        self.slotCode = slotCode
        self.isFallback = isFallback
        self.disclosureLabel = disclosureLabel
        self.sessionImpressionCap = sessionImpressionCap
        self.items = items
    }
}

public struct AppAnnouncementDto: Codable, Equatable, Sendable {
    public var id: String
    public var message: String?
    public var linkUrl: String?
    public var endsAt: JSONValue?

    public init(
        id: String,
        message: String? = nil,
        linkUrl: String? = nil,
        endsAt: JSONValue? = nil
    ) {
        self.id = id
        self.message = message
        self.linkUrl = linkUrl
        self.endsAt = endsAt
    }
}

public struct AppBilingualText: Codable, Equatable, Sendable {
    public var zh: String?
    public var en: String?

    public init(
        zh: String? = nil,
        en: String? = nil
    ) {
        self.zh = zh
        self.en = en
    }
}

public struct AppConfigEvaluation: Codable, Equatable, Sendable {
    public var maintenance: Bool
    public var updateRequired: Bool
    public var updateRecommended: Bool

    public init(
        maintenance: Bool,
        updateRequired: Bool,
        updateRecommended: Bool
    ) {
        self.maintenance = maintenance
        self.updateRequired = updateRequired
        self.updateRecommended = updateRecommended
    }
}

public struct AppConfigResponse: Codable, Equatable, Sendable {
    public var generatedAt: JSONValue
    public var ios: AppPlatformConfig
    public var android: AppPlatformConfig
    public var evaluation: AppConfigEvaluation?

    public init(
        generatedAt: JSONValue,
        ios: AppPlatformConfig,
        android: AppPlatformConfig,
        evaluation: AppConfigEvaluation? = nil
    ) {
        self.generatedAt = generatedAt
        self.ios = ios
        self.android = android
        self.evaluation = evaluation
    }
}

public struct AppDeepLinkDto: Codable, Equatable, Sendable {
    public var code: String
    public var label: String?
    public var appLink: String
    public var webUrl: String?
    public var requiresLogin: Bool

    public init(
        code: String,
        label: String? = nil,
        appLink: String,
        webUrl: String? = nil,
        requiresLogin: Bool
    ) {
        self.code = code
        self.label = label
        self.appLink = appLink
        self.webUrl = webUrl
        self.requiresLogin = requiresLogin
    }
}

public struct AppDeviceRegisteredDto: Codable, Equatable, Sendable {
    public var deviceInstallId: String
    public var isNew: Bool
    public var pushTokenStatus: String

    public init(
        deviceInstallId: String,
        isNew: Bool,
        pushTokenStatus: String
    ) {
        self.deviceInstallId = deviceInstallId
        self.isNew = isNew
        self.pushTokenStatus = pushTokenStatus
    }
}

public struct AppDiagnosticBatchRequest: Codable, Equatable, Sendable {
    public var reports: [AppDiagnosticInput]

    public init(
        reports: [AppDiagnosticInput]
    ) {
        self.reports = reports
    }
}

public struct AppDiagnosticInput: Codable, Equatable, Sendable {
    public var deviceInstallId: String?
    public var platform: String
    public var appVersion: String
    public var buildNumber: String?
    public var osVersion: String?
    public var occurredAt: String
    public var type: String
    public var metricValue: Int?
    public var summary: String?
    public var detail: String?

    public init(
        deviceInstallId: String? = nil,
        platform: String,
        appVersion: String,
        buildNumber: String? = nil,
        osVersion: String? = nil,
        occurredAt: String,
        type: String,
        metricValue: Int? = nil,
        summary: String? = nil,
        detail: String? = nil
    ) {
        self.deviceInstallId = deviceInstallId
        self.platform = platform
        self.appVersion = appVersion
        self.buildNumber = buildNumber
        self.osVersion = osVersion
        self.occurredAt = occurredAt
        self.type = type
        self.metricValue = metricValue
        self.summary = summary
        self.detail = detail
    }
}

public struct AppLayoutItemDto: Codable, Equatable, Sendable {
    public var code: String
    public var label: String?
    public var icon: String?
    public var deepLink: String?
    public var webUrl: String?

    public init(
        code: String,
        label: String? = nil,
        icon: String? = nil,
        deepLink: String? = nil,
        webUrl: String? = nil
    ) {
        self.code = code
        self.label = label
        self.icon = icon
        self.deepLink = deepLink
        self.webUrl = webUrl
    }
}

public struct AppLayoutResponse: Codable, Equatable, Sendable {
    public var generatedAt: JSONValue
    public var homeSections: [AppLayoutItemDto]
    public var quickEntries: [AppLayoutItemDto]
    public var moreItems: [AppLayoutItemDto]
    public var announcements: [AppAnnouncementDto]
    public var deepLinks: [AppDeepLinkDto]

    public init(
        generatedAt: JSONValue,
        homeSections: [AppLayoutItemDto],
        quickEntries: [AppLayoutItemDto],
        moreItems: [AppLayoutItemDto],
        announcements: [AppAnnouncementDto],
        deepLinks: [AppDeepLinkDto]
    ) {
        self.generatedAt = generatedAt
        self.homeSections = homeSections
        self.quickEntries = quickEntries
        self.moreItems = moreItems
        self.announcements = announcements
        self.deepLinks = deepLinks
    }
}

public struct AppMaintenanceNode: Codable, Equatable, Sendable {
    public var enabled: Bool
    public var message: AppBilingualText?

    public init(
        enabled: Bool,
        message: AppBilingualText? = nil
    ) {
        self.enabled = enabled
        self.message = message
    }
}

public struct AppNotificationDto: Codable, Equatable, Sendable {
    public var id: String
    public var title: String?
    public var body: String?
    public var imageUrl: String?
    public var deepLink: String?
    public var sentAt: JSONValue

    public init(
        id: String,
        title: String? = nil,
        body: String? = nil,
        imageUrl: String? = nil,
        deepLink: String? = nil,
        sentAt: JSONValue
    ) {
        self.id = id
        self.title = title
        self.body = body
        self.imageUrl = imageUrl
        self.deepLink = deepLink
        self.sentAt = sentAt
    }
}

public struct AppPlatformConfig: Codable, Equatable, Sendable {
    public var minSupportedVersion: String?
    public var recommendedVersion: String?
    public var forceUpdateMessage: AppBilingualText?
    public var recommendUpdateMessage: AppBilingualText?
    public var whatsNew: AppBilingualText?
    public var maintenance: AppMaintenanceNode
    public var featureFlags: [String: Bool]

    public init(
        minSupportedVersion: String? = nil,
        recommendedVersion: String? = nil,
        forceUpdateMessage: AppBilingualText? = nil,
        recommendUpdateMessage: AppBilingualText? = nil,
        whatsNew: AppBilingualText? = nil,
        maintenance: AppMaintenanceNode,
        featureFlags: [String: Bool]
    ) {
        self.minSupportedVersion = minSupportedVersion
        self.recommendedVersion = recommendedVersion
        self.forceUpdateMessage = forceUpdateMessage
        self.recommendUpdateMessage = recommendUpdateMessage
        self.whatsNew = whatsNew
        self.maintenance = maintenance
        self.featureFlags = featureFlags
    }
}

public struct AppPushOpenedRequest: Codable, Equatable, Sendable {
    public var deviceInstallId: String

    public init(
        deviceInstallId: String
    ) {
        self.deviceInstallId = deviceInstallId
    }
}

public struct AppSubscriptionDto: Codable, Equatable, Sendable {
    public var topicType: String
    public var topicValue: String
    public var isFollowing: Bool
    public var isPushEnabled: Bool

    public init(
        topicType: String,
        topicValue: String,
        isFollowing: Bool,
        isPushEnabled: Bool
    ) {
        self.topicType = topicType
        self.topicValue = topicValue
        self.isFollowing = isFollowing
        self.isPushEnabled = isPushEnabled
    }
}

public struct AppSubscriptionInput: Codable, Equatable, Sendable {
    public var topicType: String
    public var topicValue: String
    public var isFollowing: Bool?
    public var isPushEnabled: Bool?

    public init(
        topicType: String,
        topicValue: String,
        isFollowing: Bool? = nil,
        isPushEnabled: Bool? = nil
    ) {
        self.topicType = topicType
        self.topicValue = topicValue
        self.isFollowing = isFollowing
        self.isPushEnabled = isPushEnabled
    }
}

public struct ArticleDetailDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var categoryCode: String
    public var categoryName: String?
    public var coverKey: String?
    public var coverUrl: String?
    public var coverWidth: Int?
    public var coverHeight: Int?
    public var coverAlt: String?
    public var isFeatured: Bool
    public var viewCount: Int?
    public var publishedAt: JSONValue?
    public var title: String?
    public var summary: String?
    public var bodyJson: String?
    public var seoTitle: String?
    public var seoDescription: String?
    public var seoKeywords: String?
    public var ogImageUrl: String?
    public var ogImageWidth: Int?
    public var ogImageHeight: Int?
    public var ogImageAlt: String?
    public var canonicalPath: String?
    public var isNoindex: Bool
    public var isShared: Bool
    public var breadcrumbSchemaEligible: Bool
    public var tags: [ArticleTagDto]
    public var coreValueTags: [String]
    public var relations: [ArticleRelationDto]
    public var schemaEligible: Bool

    public init(
        id: String,
        slug: String,
        categoryCode: String,
        categoryName: String? = nil,
        coverKey: String? = nil,
        coverUrl: String? = nil,
        coverWidth: Int? = nil,
        coverHeight: Int? = nil,
        coverAlt: String? = nil,
        isFeatured: Bool,
        viewCount: Int? = nil,
        publishedAt: JSONValue? = nil,
        title: String? = nil,
        summary: String? = nil,
        bodyJson: String? = nil,
        seoTitle: String? = nil,
        seoDescription: String? = nil,
        seoKeywords: String? = nil,
        ogImageUrl: String? = nil,
        ogImageWidth: Int? = nil,
        ogImageHeight: Int? = nil,
        ogImageAlt: String? = nil,
        canonicalPath: String? = nil,
        isNoindex: Bool,
        isShared: Bool,
        breadcrumbSchemaEligible: Bool,
        tags: [ArticleTagDto],
        coreValueTags: [String],
        relations: [ArticleRelationDto],
        schemaEligible: Bool
    ) {
        self.id = id
        self.slug = slug
        self.categoryCode = categoryCode
        self.categoryName = categoryName
        self.coverKey = coverKey
        self.coverUrl = coverUrl
        self.coverWidth = coverWidth
        self.coverHeight = coverHeight
        self.coverAlt = coverAlt
        self.isFeatured = isFeatured
        self.viewCount = viewCount
        self.publishedAt = publishedAt
        self.title = title
        self.summary = summary
        self.bodyJson = bodyJson
        self.seoTitle = seoTitle
        self.seoDescription = seoDescription
        self.seoKeywords = seoKeywords
        self.ogImageUrl = ogImageUrl
        self.ogImageWidth = ogImageWidth
        self.ogImageHeight = ogImageHeight
        self.ogImageAlt = ogImageAlt
        self.canonicalPath = canonicalPath
        self.isNoindex = isNoindex
        self.isShared = isShared
        self.breadcrumbSchemaEligible = breadcrumbSchemaEligible
        self.tags = tags
        self.coreValueTags = coreValueTags
        self.relations = relations
        self.schemaEligible = schemaEligible
    }
}

public struct ArticleListItemDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var categoryCode: String
    public var categoryName: String?
    public var coverKey: String?
    public var coverUrl: String?
    public var coverWidth: Int?
    public var coverHeight: Int?
    public var coverAlt: String?
    public var isFeatured: Bool
    public var publishedAt: JSONValue?
    public var title: String?
    public var summary: String?
    public var isShared: Bool
    public var tags: [ArticleTagDto]

    public init(
        id: String,
        slug: String,
        categoryCode: String,
        categoryName: String? = nil,
        coverKey: String? = nil,
        coverUrl: String? = nil,
        coverWidth: Int? = nil,
        coverHeight: Int? = nil,
        coverAlt: String? = nil,
        isFeatured: Bool,
        publishedAt: JSONValue? = nil,
        title: String? = nil,
        summary: String? = nil,
        isShared: Bool,
        tags: [ArticleTagDto]
    ) {
        self.id = id
        self.slug = slug
        self.categoryCode = categoryCode
        self.categoryName = categoryName
        self.coverKey = coverKey
        self.coverUrl = coverUrl
        self.coverWidth = coverWidth
        self.coverHeight = coverHeight
        self.coverAlt = coverAlt
        self.isFeatured = isFeatured
        self.publishedAt = publishedAt
        self.title = title
        self.summary = summary
        self.isShared = isShared
        self.tags = tags
    }
}

public struct ArticleRelationDto: Codable, Equatable, Sendable {
    public var targetType: String
    public var targetId: String

    public init(
        targetType: String,
        targetId: String
    ) {
        self.targetType = targetType
        self.targetId = targetId
    }
}

public struct ArticleTagDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?

    public init(
        slug: String,
        name: String? = nil
    ) {
        self.slug = slug
        self.name = name
    }
}

public struct BannerDto: Codable, Equatable, Sendable {
    public var id: String
    public var mediaType: String
    public var imageKey: String
    public var imageWidth: Int?
    public var imageHeight: Int?
    public var videoKey: String?
    public var imageUrl: String?
    public var videoUrl: String?
    public var sortOrder: Int
    public var title: String?
    public var subtitle: String?
    public var imageAlt: String?
    public var cta1Label: String?
    public var cta1Url: String?
    public var cta2Label: String?
    public var cta2Url: String?

    public init(
        id: String,
        mediaType: String,
        imageKey: String,
        imageWidth: Int? = nil,
        imageHeight: Int? = nil,
        videoKey: String? = nil,
        imageUrl: String? = nil,
        videoUrl: String? = nil,
        sortOrder: Int,
        title: String? = nil,
        subtitle: String? = nil,
        imageAlt: String? = nil,
        cta1Label: String? = nil,
        cta1Url: String? = nil,
        cta2Label: String? = nil,
        cta2Url: String? = nil
    ) {
        self.id = id
        self.mediaType = mediaType
        self.imageKey = imageKey
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
        self.videoKey = videoKey
        self.imageUrl = imageUrl
        self.videoUrl = videoUrl
        self.sortOrder = sortOrder
        self.title = title
        self.subtitle = subtitle
        self.imageAlt = imageAlt
        self.cta1Label = cta1Label
        self.cta1Url = cta1Url
        self.cta2Label = cta2Label
        self.cta2Url = cta2Url
    }
}

public struct BenefitGroupPublicDto: Codable, Equatable, Sendable {
    public var group: String
    public var groupLabel: String
    public var items: [BenefitItemPublicDto]

    public init(
        group: String,
        groupLabel: String,
        items: [BenefitItemPublicDto]
    ) {
        self.group = group
        self.groupLabel = groupLabel
        self.items = items
    }
}

public struct BenefitItemPublicDto: Codable, Equatable, Sendable {
    public var name: String?
    public var description: String?
    public var freeValue: String?
    public var paidValue: String?

    public init(
        name: String? = nil,
        description: String? = nil,
        freeValue: String? = nil,
        paidValue: String? = nil
    ) {
        self.name = name
        self.description = description
        self.freeValue = freeValue
        self.paidValue = paidValue
    }
}

public struct BenefitTablePublicDto: Codable, Equatable, Sendable {
    public var planCode: String?
    public var planName: String?
    public var groups: [BenefitGroupPublicDto]

    public init(
        planCode: String? = nil,
        planName: String? = nil,
        groups: [BenefitGroupPublicDto]
    ) {
        self.planCode = planCode
        self.planName = planName
        self.groups = groups
    }
}

public struct CardVerificationDto: Codable, Equatable, Sendable {
    public var nameInitial: String
    public var memberNo: String
    public var tier: String
    public var tierLabel: String
    public var status: String
    public var statusLabel: String

    public init(
        nameInitial: String,
        memberNo: String,
        tier: String,
        tierLabel: String,
        status: String,
        statusLabel: String
    ) {
        self.nameInitial = nameInitial
        self.memberNo = memberNo
        self.tier = tier
        self.tierLabel = tierLabel
        self.status = status
        self.statusLabel = statusLabel
    }
}

public struct CharityArticleLinkDto: Codable, Equatable, Sendable {
    public var slug: String
    public var title: String?

    public init(
        slug: String,
        title: String? = nil
    ) {
        self.slug = slug
        self.title = title
    }
}

public struct CharityCtaDto: Codable, Equatable, Sendable {
    public var donationUrl: String?
    public var donationCta: String?
    public var fanCta: String?
    public var corporateCta: String?
    public var corporateUrl: String?

    public init(
        donationUrl: String? = nil,
        donationCta: String? = nil,
        fanCta: String? = nil,
        corporateCta: String? = nil,
        corporateUrl: String? = nil
    ) {
        self.donationUrl = donationUrl
        self.donationCta = donationCta
        self.fanCta = fanCta
        self.corporateCta = corporateCta
        self.corporateUrl = corporateUrl
    }
}

public struct CharityLinkedItemDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var logoDarkUrl: String?
    public var logoLightUrl: String?

    public init(
        slug: String,
        name: String? = nil,
        logoDarkUrl: String? = nil,
        logoLightUrl: String? = nil
    ) {
        self.slug = slug
        self.name = name
        self.logoDarkUrl = logoDarkUrl
        self.logoLightUrl = logoLightUrl
    }
}

public struct CharityProgramDetailDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var name: String?
    public var targetAudience: String?
    public var startOn: String?
    public var endOn: String?
    public var progress: String
    public var coverUrl: String?
    public var content: String?
    public var donationContent: String?
    public var charity: PublicCharityOrgDto?
    public var images: [PublicCharityImageDto]
    public var partners: [CharityLinkedItemDto]
    public var sponsors: [CharityLinkedItemDto]
    public var articles: [CharityArticleLinkDto]

    public init(
        id: String,
        slug: String,
        name: String? = nil,
        targetAudience: String? = nil,
        startOn: String? = nil,
        endOn: String? = nil,
        progress: String,
        coverUrl: String? = nil,
        content: String? = nil,
        donationContent: String? = nil,
        charity: PublicCharityOrgDto? = nil,
        images: [PublicCharityImageDto],
        partners: [CharityLinkedItemDto],
        sponsors: [CharityLinkedItemDto],
        articles: [CharityArticleLinkDto]
    ) {
        self.id = id
        self.slug = slug
        self.name = name
        self.targetAudience = targetAudience
        self.startOn = startOn
        self.endOn = endOn
        self.progress = progress
        self.coverUrl = coverUrl
        self.content = content
        self.donationContent = donationContent
        self.charity = charity
        self.images = images
        self.partners = partners
        self.sponsors = sponsors
        self.articles = articles
    }
}

public struct CharityProgramListItemDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var name: String?
    public var targetAudience: String?
    public var startOn: String?
    public var endOn: String?
    public var progress: String
    public var isPinned: Bool
    public var coverUrl: String?
    public var charityName: String?

    public init(
        id: String,
        slug: String,
        name: String? = nil,
        targetAudience: String? = nil,
        startOn: String? = nil,
        endOn: String? = nil,
        progress: String,
        isPinned: Bool,
        coverUrl: String? = nil,
        charityName: String? = nil
    ) {
        self.id = id
        self.slug = slug
        self.name = name
        self.targetAudience = targetAudience
        self.startOn = startOn
        self.endOn = endOn
        self.progress = progress
        self.isPinned = isPinned
        self.coverUrl = coverUrl
        self.charityName = charityName
    }
}

public struct CheckoutInvoiceRequest: Codable, Equatable, Sendable {
    public var type: String?
    public var carrierId: String?
    public var taxId: String?
    public var donationCode: String?

    public init(
        type: String? = nil,
        carrierId: String? = nil,
        taxId: String? = nil,
        donationCode: String? = nil
    ) {
        self.type = type
        self.carrierId = carrierId
        self.taxId = taxId
        self.donationCode = donationCode
    }
}

public struct CheckoutRequest: Codable, Equatable, Sendable {
    public var email: String?
    public var recipientName: String?
    public var recipientPhone: String?
    public var deliveryMethod: String?
    public var recipientAddress: String?
    public var pickupStore: String?
    public var customerNote: String?
    public var invoice: CheckoutInvoiceRequest?
    public var lang: String?

    public init(
        email: String? = nil,
        recipientName: String? = nil,
        recipientPhone: String? = nil,
        deliveryMethod: String? = nil,
        recipientAddress: String? = nil,
        pickupStore: String? = nil,
        customerNote: String? = nil,
        invoice: CheckoutInvoiceRequest? = nil,
        lang: String? = nil
    ) {
        self.email = email
        self.recipientName = recipientName
        self.recipientPhone = recipientPhone
        self.deliveryMethod = deliveryMethod
        self.recipientAddress = recipientAddress
        self.pickupStore = pickupStore
        self.customerNote = customerNote
        self.invoice = invoice
        self.lang = lang
    }
}

public struct ClubDto: Codable, Equatable, Sendable {
    public var code: String
    public var name: String
    public var description: String?
    public var domain: String
    public var logoLightKey: String?
    public var logoDarkKey: String?
    public var faviconKey: String?
    public var ogImageKey: String?
    public var brandColor: String?
    public var brandSecondaryColor: String?
    public var defaultLocale: String
    public var logoUrl: String?
    public var logoDarkUrl: String?
    public var faviconUrl: String?
    public var ogImageUrl: String?
    public var schemaEligible: Bool

    public init(
        code: String,
        name: String,
        description: String? = nil,
        domain: String,
        logoLightKey: String? = nil,
        logoDarkKey: String? = nil,
        faviconKey: String? = nil,
        ogImageKey: String? = nil,
        brandColor: String? = nil,
        brandSecondaryColor: String? = nil,
        defaultLocale: String,
        logoUrl: String? = nil,
        logoDarkUrl: String? = nil,
        faviconUrl: String? = nil,
        ogImageUrl: String? = nil,
        schemaEligible: Bool
    ) {
        self.code = code
        self.name = name
        self.description = description
        self.domain = domain
        self.logoLightKey = logoLightKey
        self.logoDarkKey = logoDarkKey
        self.faviconKey = faviconKey
        self.ogImageKey = ogImageKey
        self.brandColor = brandColor
        self.brandSecondaryColor = brandSecondaryColor
        self.defaultLocale = defaultLocale
        self.logoUrl = logoUrl
        self.logoDarkUrl = logoDarkUrl
        self.faviconUrl = faviconUrl
        self.ogImageUrl = ogImageUrl
        self.schemaEligible = schemaEligible
    }
}

public struct ComicAboutPublicDto: Codable, Equatable, Sendable {
    public var title: String?
    public var body: String?

    public init(
        title: String? = nil,
        body: String? = nil
    ) {
        self.title = title
        self.body = body
    }
}

public struct ComicCharacterPublicDto: Codable, Equatable, Sendable {
    public var id: String
    public var name: String?
    public var description: String?
    public var imageUrl: String?
    public var imageThumbUrl: String?
    public var playerId: String?

    public init(
        id: String,
        name: String? = nil,
        description: String? = nil,
        imageUrl: String? = nil,
        imageThumbUrl: String? = nil,
        playerId: String? = nil
    ) {
        self.id = id
        self.name = name
        self.description = description
        self.imageUrl = imageUrl
        self.imageThumbUrl = imageThumbUrl
        self.playerId = playerId
    }
}

public struct ComicEpisodeDetailDto: Codable, Equatable, Sendable {
    public var episodeNo: Int
    public var title: String?
    public var coverUrl: String?
    public var publishedOn: String?
    public var isLatest: Bool
    public var pages: [ComicPagePublicDto]
    public var previousEpisodeNo: Int?
    public var nextEpisodeNo: Int?

    public init(
        episodeNo: Int,
        title: String? = nil,
        coverUrl: String? = nil,
        publishedOn: String? = nil,
        isLatest: Bool,
        pages: [ComicPagePublicDto],
        previousEpisodeNo: Int? = nil,
        nextEpisodeNo: Int? = nil
    ) {
        self.episodeNo = episodeNo
        self.title = title
        self.coverUrl = coverUrl
        self.publishedOn = publishedOn
        self.isLatest = isLatest
        self.pages = pages
        self.previousEpisodeNo = previousEpisodeNo
        self.nextEpisodeNo = nextEpisodeNo
    }
}

public struct ComicEpisodeListItemDto: Codable, Equatable, Sendable {
    public var episodeNo: Int
    public var title: String?
    public var coverUrl: String?
    public var coverThumbUrl: String?
    public var publishedOn: String?
    public var isLatest: Bool
    public var pageCount: Int

    public init(
        episodeNo: Int,
        title: String? = nil,
        coverUrl: String? = nil,
        coverThumbUrl: String? = nil,
        publishedOn: String? = nil,
        isLatest: Bool,
        pageCount: Int
    ) {
        self.episodeNo = episodeNo
        self.title = title
        self.coverUrl = coverUrl
        self.coverThumbUrl = coverThumbUrl
        self.publishedOn = publishedOn
        self.isLatest = isLatest
        self.pageCount = pageCount
    }
}

public struct ComicPagePublicDto: Codable, Equatable, Sendable {
    public var pageNo: Int
    public var imageUrl: String?
    public var imageThumbUrl: String?
    public var width: Int?
    public var height: Int?

    public init(
        pageNo: Int,
        imageUrl: String? = nil,
        imageThumbUrl: String? = nil,
        width: Int? = nil,
        height: Int? = nil
    ) {
        self.pageNo = pageNo
        self.imageUrl = imageUrl
        self.imageThumbUrl = imageThumbUrl
        self.width = width
        self.height = height
    }
}

public struct ConfirmMembershipOrderRequest: Codable, Equatable, Sendable {
    public var transactionId: String

    public init(
        transactionId: String
    ) {
        self.transactionId = transactionId
    }
}

public struct ConfirmShopOrderRequest: Codable, Equatable, Sendable {
    public var transactionId: String?

    public init(
        transactionId: String? = nil
    ) {
        self.transactionId = transactionId
    }
}

public struct CoreValueDto: Codable, Equatable, Sendable {
    public var code: String
    public var nameZh: String
    public var nameEn: String
    public var sortOrder: Int
    public var learnMorePageSlug: String?

    public init(
        code: String,
        nameZh: String,
        nameEn: String,
        sortOrder: Int,
        learnMorePageSlug: String? = nil
    ) {
        self.code = code
        self.nameZh = nameZh
        self.nameEn = nameEn
        self.sortOrder = sortOrder
        self.learnMorePageSlug = learnMorePageSlug
    }
}

public struct CrawlerAgentDto: Codable, Equatable, Sendable {
    public var userAgent: String
    public var allowed: Bool

    public init(
        userAgent: String,
        allowed: Bool
    ) {
        self.userAgent = userAgent
        self.allowed = allowed
    }
}

public struct CreateMembershipOrderRequest: Codable, Equatable, Sendable {
    public var planCode: String

    public init(
        planCode: String
    ) {
        self.planCode = planCode
    }
}

public struct FanEventArticlePublicDto: Codable, Equatable, Sendable {
    public var slug: String
    public var title: String?

    public init(
        slug: String,
        title: String? = nil
    ) {
        self.slug = slug
        self.title = title
    }
}

public struct FanEventDetailDto: Codable, Equatable, Sendable {
    public var event: FanEventListItemDto
    public var description: String?
    public var venueName: String?
    public var images: [FanEventImagePublicDto]
    public var articles: [FanEventArticlePublicDto]
    public var myRegistration: FanEventMyRegistrationDto?

    public init(
        event: FanEventListItemDto,
        description: String? = nil,
        venueName: String? = nil,
        images: [FanEventImagePublicDto],
        articles: [FanEventArticlePublicDto],
        myRegistration: FanEventMyRegistrationDto? = nil
    ) {
        self.event = event
        self.description = description
        self.venueName = venueName
        self.images = images
        self.articles = articles
        self.myRegistration = myRegistration
    }
}

public struct FanEventImagePublicDto: Codable, Equatable, Sendable {
    public var imageUrl: String?
    public var imageThumbUrl: String?
    public var width: Int?
    public var height: Int?

    public init(
        imageUrl: String? = nil,
        imageThumbUrl: String? = nil,
        width: Int? = nil,
        height: Int? = nil
    ) {
        self.imageUrl = imageUrl
        self.imageThumbUrl = imageThumbUrl
        self.width = width
        self.height = height
    }
}

public struct FanEventListItemDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var location: String?
    public var startsAt: JSONValue?
    public var endsAt: JSONValue?
    public var registrationDeadlineAt: JSONValue?
    public var capacity: Int?
    public var spotsLeft: Int?
    public var isPaidMembersOnly: Bool
    public var isRegistrationOpen: Bool
    public var isFull: Bool
    public var phase: String
    public var coverUrl: String?
    public var coverThumbUrl: String?

    public init(
        slug: String,
        name: String? = nil,
        location: String? = nil,
        startsAt: JSONValue? = nil,
        endsAt: JSONValue? = nil,
        registrationDeadlineAt: JSONValue? = nil,
        capacity: Int? = nil,
        spotsLeft: Int? = nil,
        isPaidMembersOnly: Bool,
        isRegistrationOpen: Bool,
        isFull: Bool,
        phase: String,
        coverUrl: String? = nil,
        coverThumbUrl: String? = nil
    ) {
        self.slug = slug
        self.name = name
        self.location = location
        self.startsAt = startsAt
        self.endsAt = endsAt
        self.registrationDeadlineAt = registrationDeadlineAt
        self.capacity = capacity
        self.spotsLeft = spotsLeft
        self.isPaidMembersOnly = isPaidMembersOnly
        self.isRegistrationOpen = isRegistrationOpen
        self.isFull = isFull
        self.phase = phase
        self.coverUrl = coverUrl
        self.coverThumbUrl = coverThumbUrl
    }
}

public struct FanEventMyRegistrationDto: Codable, Equatable, Sendable {
    public var status: String
    public var statusLabel: String

    public init(
        status: String,
        statusLabel: String
    ) {
        self.status = status
        self.statusLabel = statusLabel
    }
}

public struct FanEventRegisterRequest: Codable, Equatable, Sendable {
    public var applicantName: String?
    public var phone: String?
    public var email: String?
    public var note: String?

    public init(
        applicantName: String? = nil,
        phone: String? = nil,
        email: String? = nil,
        note: String? = nil
    ) {
        self.applicantName = applicantName
        self.phone = phone
        self.email = email
        self.note = note
    }
}

public struct FanEventRegistrationResultDto: Codable, Equatable, Sendable {
    public var status: String
    public var statusLabel: String
    public var isWaitlisted: Bool

    public init(
        status: String,
        statusLabel: String,
        isWaitlisted: Bool
    ) {
        self.status = status
        self.statusLabel = statusLabel
        self.isWaitlisted = isWaitlisted
    }
}

public struct FaqCategoryDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var sortOrder: Int
    public var name: String?

    public init(
        id: String,
        slug: String,
        sortOrder: Int,
        name: String? = nil
    ) {
        self.id = id
        self.slug = slug
        self.sortOrder = sortOrder
        self.name = name
    }
}

public struct FaqFeedbackRequest: Codable, Equatable, Sendable {
    public var helpful: Bool

    public init(
        helpful: Bool
    ) {
        self.helpful = helpful
    }
}

public struct FaqListItemDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var isShared: Bool
    public var sortOrder: Int
    public var question: String?
    public var answer: String?
    public var categorySlugs: [String]

    public init(
        id: String,
        slug: String,
        isShared: Bool,
        sortOrder: Int,
        question: String? = nil,
        answer: String? = nil,
        categorySlugs: [String]
    ) {
        self.id = id
        self.slug = slug
        self.isShared = isShared
        self.sortOrder = sortOrder
        self.question = question
        self.answer = answer
        self.categorySlugs = categorySlugs
    }
}

public struct FaqSearchMissRequest: Codable, Equatable, Sendable {
    public var keyword: String

    public init(
        keyword: String
    ) {
        self.keyword = keyword
    }
}

public struct HomeSectionDto: Codable, Equatable, Sendable {
    public var sectionCode: String
    public var isEnabled: Bool
    public var sortOrder: Int
    public var featuredBannerId: String?

    public init(
        sectionCode: String,
        isEnabled: Bool,
        sortOrder: Int,
        featuredBannerId: String? = nil
    ) {
        self.sectionCode = sectionCode
        self.isEnabled = isEnabled
        self.sortOrder = sortOrder
        self.featuredBannerId = featuredBannerId
    }
}

public struct ImpactMetricDto: Codable, Equatable, Sendable {
    public var name: String?
    public var unit: String?
    public var value: Int?
    public var programSlug: String?

    public init(
        name: String? = nil,
        unit: String? = nil,
        value: Int? = nil,
        programSlug: String? = nil
    ) {
        self.name = name
        self.unit = unit
        self.value = value
        self.programSlug = programSlug
    }
}

public struct ImpactRecordDto: Codable, Equatable, Sendable {
    public var id: String
    public var happenedOn: String?
    public var charityName: String?
    public var charityLogoUrl: String?
    public var donationContent: String?
    public var location: String?
    public var briefDescription: String?
    public var imageUrl: String?
    public var imageWidth: Int?
    public var imageHeight: Int?
    public var images: [PublicCharityImageDto]
    public var programSlug: String?
    public var programName: String?

    public init(
        id: String,
        happenedOn: String? = nil,
        charityName: String? = nil,
        charityLogoUrl: String? = nil,
        donationContent: String? = nil,
        location: String? = nil,
        briefDescription: String? = nil,
        imageUrl: String? = nil,
        imageWidth: Int? = nil,
        imageHeight: Int? = nil,
        images: [PublicCharityImageDto],
        programSlug: String? = nil,
        programName: String? = nil
    ) {
        self.id = id
        self.happenedOn = happenedOn
        self.charityName = charityName
        self.charityLogoUrl = charityLogoUrl
        self.donationContent = donationContent
        self.location = location
        self.briefDescription = briefDescription
        self.imageUrl = imageUrl
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
        self.images = images
        self.programSlug = programSlug
        self.programName = programName
    }
}

public struct ImpactSummaryDto: Codable, Equatable, Sendable {
    public var metrics: [ImpactMetricDto]
    public var charityCount: Int
    public var donationItemCount: Int
    public var regions: [String]
    public var charities: [PublicCharityOrgDto]

    public init(
        metrics: [ImpactMetricDto],
        charityCount: Int,
        donationItemCount: Int,
        regions: [String],
        charities: [PublicCharityOrgDto]
    ) {
        self.metrics = metrics
        self.charityCount = charityCount
        self.donationItemCount = donationItemCount
        self.regions = regions
        self.charities = charities
    }
}

public struct LookupShopOrderRequest: Codable, Equatable, Sendable {
    public var orderNo: String?
    public var email: String?
    public var token: String?

    public init(
        orderNo: String? = nil,
        email: String? = nil,
        token: String? = nil
    ) {
        self.orderNo = orderNo
        self.email = email
        self.token = token
    }
}

public struct MatchDto: Codable, Equatable, Sendable {
    public var id: String
    public var seasonCode: String
    public var teamCode: String
    public var matchOn: String
    public var kickoff: String?
    public var homeAway: String?
    public var opponent: String?
    public var venue: String?
    public var competitionTag: String?
    public var competitionName: String?
    public var status: String?
    public var scoreHome: Int?
    public var scoreAway: Int?
    public var roundNo: Int?
    public var matchNo: Int?
    public var originalMatchOn: String?
    public var originalKickoff: String?
    public var schemaEligible: Bool

    public init(
        id: String,
        seasonCode: String,
        teamCode: String,
        matchOn: String,
        kickoff: String? = nil,
        homeAway: String? = nil,
        opponent: String? = nil,
        venue: String? = nil,
        competitionTag: String? = nil,
        competitionName: String? = nil,
        status: String? = nil,
        scoreHome: Int? = nil,
        scoreAway: Int? = nil,
        roundNo: Int? = nil,
        matchNo: Int? = nil,
        originalMatchOn: String? = nil,
        originalKickoff: String? = nil,
        schemaEligible: Bool
    ) {
        self.id = id
        self.seasonCode = seasonCode
        self.teamCode = teamCode
        self.matchOn = matchOn
        self.kickoff = kickoff
        self.homeAway = homeAway
        self.opponent = opponent
        self.venue = venue
        self.competitionTag = competitionTag
        self.competitionName = competitionName
        self.status = status
        self.scoreHome = scoreHome
        self.scoreAway = scoreAway
        self.roundNo = roundNo
        self.matchNo = matchNo
        self.originalMatchOn = originalMatchOn
        self.originalKickoff = originalKickoff
        self.schemaEligible = schemaEligible
    }
}

public struct MemberCardDto: Codable, Equatable, Sendable {
    public var id: String
    public var membershipId: String
    public var club: MemberClubBrandDto
    public var memberNo: String
    public var holderName: String
    public var tier: String
    public var tierLabel: String
    public var validUntil: String?
    public var status: String
    public var statusLabel: String
    public var isValid: Bool
    public var token: String
    public var reissueCount: Int
    public var issuedAt: JSONValue?

    public init(
        id: String,
        membershipId: String,
        club: MemberClubBrandDto,
        memberNo: String,
        holderName: String,
        tier: String,
        tierLabel: String,
        validUntil: String? = nil,
        status: String,
        statusLabel: String,
        isValid: Bool,
        token: String,
        reissueCount: Int,
        issuedAt: JSONValue? = nil
    ) {
        self.id = id
        self.membershipId = membershipId
        self.club = club
        self.memberNo = memberNo
        self.holderName = holderName
        self.tier = tier
        self.tierLabel = tierLabel
        self.validUntil = validUntil
        self.status = status
        self.statusLabel = statusLabel
        self.isValid = isValid
        self.token = token
        self.reissueCount = reissueCount
        self.issuedAt = issuedAt
    }
}

public struct MemberChangePasswordRequest: Codable, Equatable, Sendable {
    public var currentPassword: String?
    public var newPassword: String
    public var tokenDelivery: String?
    public var deviceInstallId: String?

    public init(
        currentPassword: String? = nil,
        newPassword: String,
        tokenDelivery: String? = nil,
        deviceInstallId: String? = nil
    ) {
        self.currentPassword = currentPassword
        self.newPassword = newPassword
        self.tokenDelivery = tokenDelivery
        self.deviceInstallId = deviceInstallId
    }
}

public struct MemberClubBrandDto: Codable, Equatable, Sendable {
    public var code: String
    public var name: String
    public var logoLightUrl: String?
    public var logoDarkUrl: String?
    public var brandColor: String?
    public var brandSecondaryColor: String?

    public init(
        code: String,
        name: String,
        logoLightUrl: String? = nil,
        logoDarkUrl: String? = nil,
        brandColor: String? = nil,
        brandSecondaryColor: String? = nil
    ) {
        self.code = code
        self.name = name
        self.logoLightUrl = logoLightUrl
        self.logoDarkUrl = logoDarkUrl
        self.brandColor = brandColor
        self.brandSecondaryColor = brandSecondaryColor
    }
}

public struct MemberDeleteAccountRequest: Codable, Equatable, Sendable {
    public var password: String?
    public var confirm: String?

    public init(
        password: String? = nil,
        confirm: String? = nil
    ) {
        self.password = password
        self.confirm = confirm
    }
}

public struct MemberDeviceDto: Codable, Equatable, Sendable {
    public var deviceId: String
    public var platform: String
    public var osVersion: String?
    public var appVersion: String?
    public var lastActiveAt: JSONValue
    public var hasActiveSession: Bool

    public init(
        deviceId: String,
        platform: String,
        osVersion: String? = nil,
        appVersion: String? = nil,
        lastActiveAt: JSONValue,
        hasActiveSession: Bool
    ) {
        self.deviceId = deviceId
        self.platform = platform
        self.osVersion = osVersion
        self.appVersion = appVersion
        self.lastActiveAt = lastActiveAt
        self.hasActiveSession = hasActiveSession
    }
}

public struct MemberForgotPasswordRequest: Codable, Equatable, Sendable {
    public var email: String
    public var club: String
    public var lang: String?

    public init(
        email: String,
        club: String,
        lang: String? = nil
    ) {
        self.email = email
        self.club = club
        self.lang = lang
    }
}

public struct MemberJerseyDto: Codable, Equatable, Sendable {
    public var id: String
    public var clubCode: String
    public var membershipId: String
    public var recipientName: String
    public var phone: String?
    public var size: String?
    public var deliveryMethod: String?
    public var deliveryMethodLabel: String?
    public var address: String?
    public var status: String
    public var statusLabel: String
    public var shippedOn: String?
    public var receivedOn: String?
    public var editable: Bool

    public init(
        id: String,
        clubCode: String,
        membershipId: String,
        recipientName: String,
        phone: String? = nil,
        size: String? = nil,
        deliveryMethod: String? = nil,
        deliveryMethodLabel: String? = nil,
        address: String? = nil,
        status: String,
        statusLabel: String,
        shippedOn: String? = nil,
        receivedOn: String? = nil,
        editable: Bool
    ) {
        self.id = id
        self.clubCode = clubCode
        self.membershipId = membershipId
        self.recipientName = recipientName
        self.phone = phone
        self.size = size
        self.deliveryMethod = deliveryMethod
        self.deliveryMethodLabel = deliveryMethodLabel
        self.address = address
        self.status = status
        self.statusLabel = statusLabel
        self.shippedOn = shippedOn
        self.receivedOn = receivedOn
        self.editable = editable
    }
}

public struct MemberJerseyGroupDto: Codable, Equatable, Sendable {
    public var membershipId: String
    public var clubCode: String
    public var clubName: String
    public var seasonCode: String
    public var quota: Int
    public var used: Int
    public var canRegister: Bool
    public var items: [MemberJerseyDto]

    public init(
        membershipId: String,
        clubCode: String,
        clubName: String,
        seasonCode: String,
        quota: Int,
        used: Int,
        canRegister: Bool,
        items: [MemberJerseyDto]
    ) {
        self.membershipId = membershipId
        self.clubCode = clubCode
        self.clubName = clubName
        self.seasonCode = seasonCode
        self.quota = quota
        self.used = used
        self.canRegister = canRegister
        self.items = items
    }
}

public struct MemberJerseyRequest: Codable, Equatable, Sendable {
    public var membershipId: String
    public var recipientName: String
    public var size: String
    public var deliveryMethod: String
    public var phone: String?
    public var address: String?

    public init(
        membershipId: String,
        recipientName: String,
        size: String,
        deliveryMethod: String,
        phone: String? = nil,
        address: String? = nil
    ) {
        self.membershipId = membershipId
        self.recipientName = recipientName
        self.size = size
        self.deliveryMethod = deliveryMethod
        self.phone = phone
        self.address = address
    }
}

public struct MemberJerseyUpdateRequest: Codable, Equatable, Sendable {
    public var recipientName: String
    public var size: String
    public var deliveryMethod: String
    public var phone: String?
    public var address: String?

    public init(
        recipientName: String,
        size: String,
        deliveryMethod: String,
        phone: String? = nil,
        address: String? = nil
    ) {
        self.recipientName = recipientName
        self.size = size
        self.deliveryMethod = deliveryMethod
        self.phone = phone
        self.address = address
    }
}

public struct MemberJoinableClubDto: Codable, Equatable, Sendable {
    public var code: String
    public var name: String

    public init(
        code: String,
        name: String
    ) {
        self.code = code
        self.name = name
    }
}

public struct MemberLineAuthorizeDto: Codable, Equatable, Sendable {
    public var authorizeUrl: String
    public var state: String

    public init(
        authorizeUrl: String,
        state: String
    ) {
        self.authorizeUrl = authorizeUrl
        self.state = state
    }
}

public struct MemberLineAuthorizeRequest: Codable, Equatable, Sendable {
    public var club: String
    public var mode: String
    public var redirectUri: String?

    public init(
        club: String,
        mode: String,
        redirectUri: String? = nil
    ) {
        self.club = club
        self.mode = mode
        self.redirectUri = redirectUri
    }
}

public struct MemberLineCallbackDto: Codable, Equatable, Sendable {
    public var status: String
    public var session: MemberSessionDto?
    public var ticket: String?
    public var displayName: String?
    public var suggestedEmail: String?

    public init(
        status: String,
        session: MemberSessionDto? = nil,
        ticket: String? = nil,
        displayName: String? = nil,
        suggestedEmail: String? = nil
    ) {
        self.status = status
        self.session = session
        self.ticket = ticket
        self.displayName = displayName
        self.suggestedEmail = suggestedEmail
    }
}

public struct MemberLineCallbackRequest: Codable, Equatable, Sendable {
    public var code: String
    public var state: String
    public var tokenDelivery: String?
    public var deviceInstallId: String?

    public init(
        code: String,
        state: String,
        tokenDelivery: String? = nil,
        deviceInstallId: String? = nil
    ) {
        self.code = code
        self.state = state
        self.tokenDelivery = tokenDelivery
        self.deviceInstallId = deviceInstallId
    }
}

public struct MemberLineCompleteRequest: Codable, Equatable, Sendable {
    public var club: String
    public var ticket: String
    public var email: String
    public var name: String?
    public var phone: String?
    public var birthOn: String?
    public var lang: String?
    public var tokenDelivery: String?
    public var deviceInstallId: String?

    public init(
        club: String,
        ticket: String,
        email: String,
        name: String? = nil,
        phone: String? = nil,
        birthOn: String? = nil,
        lang: String? = nil,
        tokenDelivery: String? = nil,
        deviceInstallId: String? = nil
    ) {
        self.club = club
        self.ticket = ticket
        self.email = email
        self.name = name
        self.phone = phone
        self.birthOn = birthOn
        self.lang = lang
        self.tokenDelivery = tokenDelivery
        self.deviceInstallId = deviceInstallId
    }
}

public struct MemberLoginRequest: Codable, Equatable, Sendable {
    public var email: String
    public var password: String
    public var rememberMe: Bool?
    public var tokenDelivery: String?
    public var deviceInstallId: String?

    public init(
        email: String,
        password: String,
        rememberMe: Bool? = nil,
        tokenDelivery: String? = nil,
        deviceInstallId: String? = nil
    ) {
        self.email = email
        self.password = password
        self.rememberMe = rememberMe
        self.tokenDelivery = tokenDelivery
        self.deviceInstallId = deviceInstallId
    }
}

public struct MemberPendingOrderDto: Codable, Equatable, Sendable {
    public var orderNo: String
    public var status: String
    public var statusLabel: String

    public init(
        orderNo: String,
        status: String,
        statusLabel: String
    ) {
        self.orderNo = orderNo
        self.status = status
        self.statusLabel = statusLabel
    }
}

public struct MemberProfileDto: Codable, Equatable, Sendable {
    public var memberNo: String
    public var name: String
    public var email: String
    public var phone: String?
    public var birthOn: String?
    public var locale: String?
    public var emailVerified: Bool
    public var hasPassword: Bool
    public var lineBound: Bool
    public var signupSource: String
    public var signupSourceLabel: String
    public var createdAt: JSONValue

    public init(
        memberNo: String,
        name: String,
        email: String,
        phone: String? = nil,
        birthOn: String? = nil,
        locale: String? = nil,
        emailVerified: Bool,
        hasPassword: Bool,
        lineBound: Bool,
        signupSource: String,
        signupSourceLabel: String,
        createdAt: JSONValue
    ) {
        self.memberNo = memberNo
        self.name = name
        self.email = email
        self.phone = phone
        self.birthOn = birthOn
        self.locale = locale
        self.emailVerified = emailVerified
        self.hasPassword = hasPassword
        self.lineBound = lineBound
        self.signupSource = signupSource
        self.signupSourceLabel = signupSourceLabel
        self.createdAt = createdAt
    }
}

public struct MemberRefreshRequest: Codable, Equatable, Sendable {
    public var refreshToken: String?
    public var tokenDelivery: String?

    public init(
        refreshToken: String? = nil,
        tokenDelivery: String? = nil
    ) {
        self.refreshToken = refreshToken
        self.tokenDelivery = tokenDelivery
    }
}

public struct MemberRegisterRequest: Codable, Equatable, Sendable {
    public var club: String
    public var email: String
    public var password: String
    public var name: String
    public var phone: String?
    public var birthOn: String?
    public var lang: String?

    public init(
        club: String,
        email: String,
        password: String,
        name: String,
        phone: String? = nil,
        birthOn: String? = nil,
        lang: String? = nil
    ) {
        self.club = club
        self.email = email
        self.password = password
        self.name = name
        self.phone = phone
        self.birthOn = birthOn
        self.lang = lang
    }
}

public struct MemberRegisteredDto: Codable, Equatable, Sendable {
    public var memberNo: String
    public var emailVerificationRequired: Bool
    public var emailSent: Bool

    public init(
        memberNo: String,
        emailVerificationRequired: Bool,
        emailSent: Bool
    ) {
        self.memberNo = memberNo
        self.emailVerificationRequired = emailVerificationRequired
        self.emailSent = emailSent
    }
}

public struct MemberRegistrationDto: Codable, Equatable, Sendable {
    public var id: String
    public var registrationNo: String
    public var clubCode: String
    public var status: String
    public var applicantName: String
    public var sessionId: String?
    public var trialId: String?
    public var createdAt: JSONValue

    public init(
        id: String,
        registrationNo: String,
        clubCode: String,
        status: String,
        applicantName: String,
        sessionId: String? = nil,
        trialId: String? = nil,
        createdAt: JSONValue
    ) {
        self.id = id
        self.registrationNo = registrationNo
        self.clubCode = clubCode
        self.status = status
        self.applicantName = applicantName
        self.sessionId = sessionId
        self.trialId = trialId
        self.createdAt = createdAt
    }
}

public struct MemberResendVerificationRequest: Codable, Equatable, Sendable {
    public var email: String
    public var club: String
    public var lang: String?

    public init(
        email: String,
        club: String,
        lang: String? = nil
    ) {
        self.email = email
        self.club = club
        self.lang = lang
    }
}

public struct MemberResetPasswordRequest: Codable, Equatable, Sendable {
    public var token: String
    public var newPassword: String

    public init(
        token: String,
        newPassword: String
    ) {
        self.token = token
        self.newPassword = newPassword
    }
}

public struct MemberSessionDto: Codable, Equatable, Sendable {
    public var accessToken: String
    public var accessTokenExpiresAt: JSONValue
    public var refreshToken: String?
    public var refreshTokenExpiresAt: JSONValue?
    public var member: MemberSummaryDto

    public init(
        accessToken: String,
        accessTokenExpiresAt: JSONValue,
        refreshToken: String? = nil,
        refreshTokenExpiresAt: JSONValue? = nil,
        member: MemberSummaryDto
    ) {
        self.accessToken = accessToken
        self.accessTokenExpiresAt = accessTokenExpiresAt
        self.refreshToken = refreshToken
        self.refreshTokenExpiresAt = refreshTokenExpiresAt
        self.member = member
    }
}

public struct MemberSummaryDto: Codable, Equatable, Sendable {
    public var memberNo: String
    public var name: String
    public var emailVerified: Bool
    public var hasPassword: Bool
    public var lineBound: Bool

    public init(
        memberNo: String,
        name: String,
        emailVerified: Bool,
        hasPassword: Bool,
        lineBound: Bool
    ) {
        self.memberNo = memberNo
        self.name = name
        self.emailVerified = emailVerified
        self.hasPassword = hasPassword
        self.lineBound = lineBound
    }
}

public struct MemberUpdateProfileRequest: Codable, Equatable, Sendable {
    public var name: String
    public var phone: String?
    public var birthOn: String?
    public var locale: String?

    public init(
        name: String,
        phone: String? = nil,
        birthOn: String? = nil,
        locale: String? = nil
    ) {
        self.name = name
        self.phone = phone
        self.birthOn = birthOn
        self.locale = locale
    }
}

public struct MemberVerifyEmailRequest: Codable, Equatable, Sendable {
    public var token: String

    public init(
        token: String
    ) {
        self.token = token
    }
}

public struct MembershipOrderDto: Codable, Equatable, Sendable {
    public var orderNo: String
    public var clubCode: String
    public var planCode: String
    public var planName: String?
    public var seasonCode: String
    public var amount: Int
    public var status: String
    public var statusLabel: String
    public var paymentMethod: String?
    public var paymentUrl: String?
    public var expiresAt: JSONValue?
    public var paidAt: JSONValue?
    public var activatedAt: JSONValue?
    public var membershipId: String?
    public var createdAt: JSONValue
    public var canPayOnline: Bool
    public var canCancel: Bool

    public init(
        orderNo: String,
        clubCode: String,
        planCode: String,
        planName: String? = nil,
        seasonCode: String,
        amount: Int,
        status: String,
        statusLabel: String,
        paymentMethod: String? = nil,
        paymentUrl: String? = nil,
        expiresAt: JSONValue? = nil,
        paidAt: JSONValue? = nil,
        activatedAt: JSONValue? = nil,
        membershipId: String? = nil,
        createdAt: JSONValue,
        canPayOnline: Bool,
        canCancel: Bool
    ) {
        self.orderNo = orderNo
        self.clubCode = clubCode
        self.planCode = planCode
        self.planName = planName
        self.seasonCode = seasonCode
        self.amount = amount
        self.status = status
        self.statusLabel = statusLabel
        self.paymentMethod = paymentMethod
        self.paymentUrl = paymentUrl
        self.expiresAt = expiresAt
        self.paidAt = paidAt
        self.activatedAt = activatedAt
        self.membershipId = membershipId
        self.createdAt = createdAt
        self.canPayOnline = canPayOnline
        self.canCancel = canCancel
    }
}

public struct MembershipPlanPublicDto: Codable, Equatable, Sendable {
    public var code: String
    public var name: String?
    public var benefitNote: String?
    public var fee: Int
    public var cardQuota: Int
    public var jerseyQuota: Int
    public var midSeasonRule: String?
    public var seasonCode: String
    public var startsOn: String
    public var endsOn: String

    public init(
        code: String,
        name: String? = nil,
        benefitNote: String? = nil,
        fee: Int,
        cardQuota: Int,
        jerseyQuota: Int,
        midSeasonRule: String? = nil,
        seasonCode: String,
        startsOn: String,
        endsOn: String
    ) {
        self.code = code
        self.name = name
        self.benefitNote = benefitNote
        self.fee = fee
        self.cardQuota = cardQuota
        self.jerseyQuota = jerseyQuota
        self.midSeasonRule = midSeasonRule
        self.seasonCode = seasonCode
        self.startsOn = startsOn
        self.endsOn = endsOn
    }
}

public struct MilestoneDto: Codable, Equatable, Sendable {
    public var id: String
    public var happenedOn: String
    public var title: String?
    public var description: String?
    public var imageUrl: String?
    public var imageAlt: String?
    public var imageWidth: Int?
    public var imageHeight: Int?

    public init(
        id: String,
        happenedOn: String,
        title: String? = nil,
        description: String? = nil,
        imageUrl: String? = nil,
        imageAlt: String? = nil,
        imageWidth: Int? = nil,
        imageHeight: Int? = nil
    ) {
        self.id = id
        self.happenedOn = happenedOn
        self.title = title
        self.description = description
        self.imageUrl = imageUrl
        self.imageAlt = imageAlt
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
    }
}

public struct MyMembershipDto: Codable, Equatable, Sendable {
    public var id: String
    public var club: MemberClubBrandDto
    public var seasonCode: String
    public var tier: String
    public var tierLabel: String
    public var status: String
    public var statusLabel: String
    public var startOn: String?
    public var endOn: String?
    public var planCode: String?
    public var planName: String?
    public var cardQuota: Int
    public var jerseyQuota: Int
    public var isCurrentSeason: Bool
    public var renewalDue: Bool
    public var pendingOrder: MemberPendingOrderDto?
    public var cards: [MemberCardDto]

    public init(
        id: String,
        club: MemberClubBrandDto,
        seasonCode: String,
        tier: String,
        tierLabel: String,
        status: String,
        statusLabel: String,
        startOn: String? = nil,
        endOn: String? = nil,
        planCode: String? = nil,
        planName: String? = nil,
        cardQuota: Int,
        jerseyQuota: Int,
        isCurrentSeason: Bool,
        renewalDue: Bool,
        pendingOrder: MemberPendingOrderDto? = nil,
        cards: [MemberCardDto]
    ) {
        self.id = id
        self.club = club
        self.seasonCode = seasonCode
        self.tier = tier
        self.tierLabel = tierLabel
        self.status = status
        self.statusLabel = statusLabel
        self.startOn = startOn
        self.endOn = endOn
        self.planCode = planCode
        self.planName = planName
        self.cardQuota = cardQuota
        self.jerseyQuota = jerseyQuota
        self.isCurrentSeason = isCurrentSeason
        self.renewalDue = renewalDue
        self.pendingOrder = pendingOrder
        self.cards = cards
    }
}

public struct MyMembershipsDto: Codable, Equatable, Sendable {
    public var memberships: [MyMembershipDto]
    public var joinableClubs: [MemberJoinableClubDto]

    public init(
        memberships: [MyMembershipDto],
        joinableClubs: [MemberJoinableClubDto]
    ) {
        self.memberships = memberships
        self.joinableClubs = joinableClubs
    }
}

public struct PageBlockPublicDto: Codable, Equatable, Sendable {
    public var blockType: String
    public var content: JSONValue
    public var sortOrder: Int

    public init(
        blockType: String,
        content: JSONValue,
        sortOrder: Int
    ) {
        self.blockType = blockType
        self.content = content
        self.sortOrder = sortOrder
    }
}

public struct PageDetailDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var seoTitle: String?
    public var seoDescription: String?
    public var seoKeywords: String?
    public var canonicalPath: String?
    public var isNoindex: Bool?
    public var ogImageUrl: String?
    public var ogImageWidth: Int?
    public var ogImageHeight: Int?
    public var ogImageAlt: String?
    public var publishedAt: JSONValue?
    public var blocks: [PageBlockPublicDto]

    public init(
        id: String,
        slug: String,
        seoTitle: String? = nil,
        seoDescription: String? = nil,
        seoKeywords: String? = nil,
        canonicalPath: String? = nil,
        isNoindex: Bool? = nil,
        ogImageUrl: String? = nil,
        ogImageWidth: Int? = nil,
        ogImageHeight: Int? = nil,
        ogImageAlt: String? = nil,
        publishedAt: JSONValue? = nil,
        blocks: [PageBlockPublicDto]
    ) {
        self.id = id
        self.slug = slug
        self.seoTitle = seoTitle
        self.seoDescription = seoDescription
        self.seoKeywords = seoKeywords
        self.canonicalPath = canonicalPath
        self.isNoindex = isNoindex
        self.ogImageUrl = ogImageUrl
        self.ogImageWidth = ogImageWidth
        self.ogImageHeight = ogImageHeight
        self.ogImageAlt = ogImageAlt
        self.publishedAt = publishedAt
        self.blocks = blocks
    }
}

public struct PagePreviewDto: Codable, Equatable, Sendable {
    public var pageId: String
    public var versionNo: Int
    public var status: String
    public var slug: String
    public var seoTitle: String?
    public var seoDescription: String?
    public var blocks: [PageBlockPublicDto]

    public init(
        pageId: String,
        versionNo: Int,
        status: String,
        slug: String,
        seoTitle: String? = nil,
        seoDescription: String? = nil,
        blocks: [PageBlockPublicDto]
    ) {
        self.pageId = pageId
        self.versionNo = versionNo
        self.status = status
        self.slug = slug
        self.seoTitle = seoTitle
        self.seoDescription = seoDescription
        self.blocks = blocks
    }
}

public struct PagedResultOfArticleListItemDto: Codable, Equatable, Sendable {
    public var items: [ArticleListItemDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [ArticleListItemDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfCharityProgramListItemDto: Codable, Equatable, Sendable {
    public var items: [CharityProgramListItemDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [CharityProgramListItemDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfFaqListItemDto: Codable, Equatable, Sendable {
    public var items: [FaqListItemDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [FaqListItemDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfImpactRecordDto: Codable, Equatable, Sendable {
    public var items: [ImpactRecordDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [ImpactRecordDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfMatchDto: Codable, Equatable, Sendable {
    public var items: [MatchDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [MatchDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfPlayerDto: Codable, Equatable, Sendable {
    public var items: [PlayerDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [PlayerDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfPressResourceDto: Codable, Equatable, Sendable {
    public var items: [PressResourceDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [PressResourceDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfProgramListItemDto: Codable, Equatable, Sendable {
    public var items: [ProgramListItemDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [ProgramListItemDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfPublicCalendarEventDto: Codable, Equatable, Sendable {
    public var items: [PublicCalendarEventDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [PublicCalendarEventDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfShopProductListItemDto: Codable, Equatable, Sendable {
    public var items: [ShopProductListItemDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [ShopProductListItemDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PagedResultOfStaffDto: Codable, Equatable, Sendable {
    public var items: [StaffDto]
    public var page: Int
    public var pageSize: Int
    public var totalCount: Int
    public var totalPages: Int?

    public init(
        items: [StaffDto],
        page: Int,
        pageSize: Int,
        totalCount: Int,
        totalPages: Int? = nil
    ) {
        self.items = items
        self.page = page
        self.pageSize = pageSize
        self.totalCount = totalCount
        self.totalPages = totalPages
    }
}

public struct PartnerCharityProgramDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?

    public init(
        slug: String,
        name: String? = nil
    ) {
        self.slug = slug
        self.name = name
    }
}

public struct PartnerDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var partnerType: String?
    public var country: String?
    public var startOn: String?
    public var endOn: String?
    public var websiteUrl: String?
    public var showInFooter: Bool
    public var showOnHome: Bool
    public var sortOrder: Int
    public var name: String?
    public var content: String?
    public var logoDarkUrl: String?
    public var logoLightUrl: String?
    public var charityPrograms: [PartnerCharityProgramDto]

    public init(
        id: String,
        slug: String,
        partnerType: String? = nil,
        country: String? = nil,
        startOn: String? = nil,
        endOn: String? = nil,
        websiteUrl: String? = nil,
        showInFooter: Bool,
        showOnHome: Bool,
        sortOrder: Int,
        name: String? = nil,
        content: String? = nil,
        logoDarkUrl: String? = nil,
        logoLightUrl: String? = nil,
        charityPrograms: [PartnerCharityProgramDto]
    ) {
        self.id = id
        self.slug = slug
        self.partnerType = partnerType
        self.country = country
        self.startOn = startOn
        self.endOn = endOn
        self.websiteUrl = websiteUrl
        self.showInFooter = showInFooter
        self.showOnHome = showOnHome
        self.sortOrder = sortOrder
        self.name = name
        self.content = content
        self.logoDarkUrl = logoDarkUrl
        self.logoLightUrl = logoLightUrl
        self.charityPrograms = charityPrograms
    }
}

public struct PartnerStoreFiltersDto: Codable, Equatable, Sendable {
    public var categories: [String]
    public var regions: [String]

    public init(
        categories: [String],
        regions: [String]
    ) {
        self.categories = categories
        self.regions = regions
    }
}

public struct PartnerStorePublicDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var category: String?
    public var region: String?
    public var address: String?
    public var lat: Double?
    public var lng: Double?
    public var phone: String?
    public var businessHours: String?
    public var offerContent: String?
    public var applicableTier: String
    public var applicableTierLabel: String
    public var mapUrl: String?
    public var websiteUrl: String?
    public var imageUrl: String?
    public var isShared: Bool

    public init(
        slug: String,
        name: String? = nil,
        category: String? = nil,
        region: String? = nil,
        address: String? = nil,
        lat: Double? = nil,
        lng: Double? = nil,
        phone: String? = nil,
        businessHours: String? = nil,
        offerContent: String? = nil,
        applicableTier: String,
        applicableTierLabel: String,
        mapUrl: String? = nil,
        websiteUrl: String? = nil,
        imageUrl: String? = nil,
        isShared: Bool
    ) {
        self.slug = slug
        self.name = name
        self.category = category
        self.region = region
        self.address = address
        self.lat = lat
        self.lng = lng
        self.phone = phone
        self.businessHours = businessHours
        self.offerContent = offerContent
        self.applicableTier = applicableTier
        self.applicableTierLabel = applicableTierLabel
        self.mapUrl = mapUrl
        self.websiteUrl = websiteUrl
        self.imageUrl = imageUrl
        self.isShared = isShared
    }
}

public struct PlayerCareerStatDto: Codable, Equatable, Sendable {
    public var seasonCode: String
    public var appearances: Int
    public var goals: Int
    public var assists: Int?
    public var yellowCards: Int
    public var redCards: Int
    public var source: String

    public init(
        seasonCode: String,
        appearances: Int,
        goals: Int,
        assists: Int? = nil,
        yellowCards: Int,
        redCards: Int,
        source: String
    ) {
        self.seasonCode = seasonCode
        self.appearances = appearances
        self.goals = goals
        self.assists = assists
        self.yellowCards = yellowCards
        self.redCards = redCards
        self.source = source
    }
}

public struct PlayerCareerStatsDto: Codable, Equatable, Sendable {
    public var playerId: String
    public var seasons: [PlayerCareerStatDto]

    public init(
        playerId: String,
        seasons: [PlayerCareerStatDto]
    ) {
        self.playerId = playerId
        self.seasons = seasons
    }
}

public struct PlayerDto: Codable, Equatable, Sendable {
    public var id: String
    public var teamCode: String
    public var shirtNo: Int?
    public var position: String?
    public var birthOn: String?
    public var heightCm: Int?
    public var weightKg: Int?
    public var nationality: String?
    public var preferredFoot: String?
    public var photoKey: String?
    public var name: String?
    public var bio: String?
    public var photoUrl: String?
    public var schemaEligible: Bool

    public init(
        id: String,
        teamCode: String,
        shirtNo: Int? = nil,
        position: String? = nil,
        birthOn: String? = nil,
        heightCm: Int? = nil,
        weightKg: Int? = nil,
        nationality: String? = nil,
        preferredFoot: String? = nil,
        photoKey: String? = nil,
        name: String? = nil,
        bio: String? = nil,
        photoUrl: String? = nil,
        schemaEligible: Bool
    ) {
        self.id = id
        self.teamCode = teamCode
        self.shirtNo = shirtNo
        self.position = position
        self.birthOn = birthOn
        self.heightCm = heightCm
        self.weightKg = weightKg
        self.nationality = nationality
        self.preferredFoot = preferredFoot
        self.photoKey = photoKey
        self.name = name
        self.bio = bio
        self.photoUrl = photoUrl
        self.schemaEligible = schemaEligible
    }
}

public struct PlayerSeasonStatDto: Codable, Equatable, Sendable {
    public var playerId: String
    public var name: String?
    public var teamCode: String
    public var shirtNo: Int?
    public var position: String?
    public var photoUrl: String?
    public var appearances: Int
    public var goals: Int
    public var assists: Int?
    public var yellowCards: Int
    public var redCards: Int
    public var source: String

    public init(
        playerId: String,
        name: String? = nil,
        teamCode: String,
        shirtNo: Int? = nil,
        position: String? = nil,
        photoUrl: String? = nil,
        appearances: Int,
        goals: Int,
        assists: Int? = nil,
        yellowCards: Int,
        redCards: Int,
        source: String
    ) {
        self.playerId = playerId
        self.name = name
        self.teamCode = teamCode
        self.shirtNo = shirtNo
        self.position = position
        self.photoUrl = photoUrl
        self.appearances = appearances
        self.goals = goals
        self.assists = assists
        self.yellowCards = yellowCards
        self.redCards = redCards
        self.source = source
    }
}

public struct PlayerStatsDto: Codable, Equatable, Sendable {
    public var season: SeasonRefDto?
    public var seasons: [String]
    public var items: [PlayerSeasonStatDto]

    public init(
        season: SeasonRefDto? = nil,
        seasons: [String],
        items: [PlayerSeasonStatDto]
    ) {
        self.season = season
        self.seasons = seasons
        self.items = items
    }
}

public struct PressResourceDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var resourceType: String
    public var title: String?
    public var description: String?
    public var publishedOn: String?
    public var fileBytes: Int?
    public var fileExtension: String?
    public var coverUrl: String?
    public var downloadPath: String

    public init(
        id: String,
        slug: String,
        resourceType: String,
        title: String? = nil,
        description: String? = nil,
        publishedOn: String? = nil,
        fileBytes: Int? = nil,
        fileExtension: String? = nil,
        coverUrl: String? = nil,
        downloadPath: String
    ) {
        self.id = id
        self.slug = slug
        self.resourceType = resourceType
        self.title = title
        self.description = description
        self.publishedOn = publishedOn
        self.fileBytes = fileBytes
        self.fileExtension = fileExtension
        self.coverUrl = coverUrl
        self.downloadPath = downloadPath
    }
}

public struct ProgramDetailDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var programType: String?
    public var audience: String?
    public var ageMin: Int?
    public var ageMax: Int?
    public var coverKey: String?
    public var coverUrl: String?
    public var name: String?
    public var intro: String?
    public var content: String?
    public var staff: [ProgramStaffSummaryDto]
    public var partners: [ProgramPartnerSummaryDto]
    public var sessions: [ProgramSessionDto]

    public init(
        id: String,
        slug: String,
        programType: String? = nil,
        audience: String? = nil,
        ageMin: Int? = nil,
        ageMax: Int? = nil,
        coverKey: String? = nil,
        coverUrl: String? = nil,
        name: String? = nil,
        intro: String? = nil,
        content: String? = nil,
        staff: [ProgramStaffSummaryDto],
        partners: [ProgramPartnerSummaryDto],
        sessions: [ProgramSessionDto]
    ) {
        self.id = id
        self.slug = slug
        self.programType = programType
        self.audience = audience
        self.ageMin = ageMin
        self.ageMax = ageMax
        self.coverKey = coverKey
        self.coverUrl = coverUrl
        self.name = name
        self.intro = intro
        self.content = content
        self.staff = staff
        self.partners = partners
        self.sessions = sessions
    }
}

public struct ProgramListItemDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var programType: String?
    public var audience: String?
    public var ageMin: Int?
    public var ageMax: Int?
    public var coverKey: String?
    public var coverUrl: String?
    public var name: String?
    public var intro: String?
    public var hasOpenSession: Bool

    public init(
        id: String,
        slug: String,
        programType: String? = nil,
        audience: String? = nil,
        ageMin: Int? = nil,
        ageMax: Int? = nil,
        coverKey: String? = nil,
        coverUrl: String? = nil,
        name: String? = nil,
        intro: String? = nil,
        hasOpenSession: Bool
    ) {
        self.id = id
        self.slug = slug
        self.programType = programType
        self.audience = audience
        self.ageMin = ageMin
        self.ageMax = ageMax
        self.coverKey = coverKey
        self.coverUrl = coverUrl
        self.name = name
        self.intro = intro
        self.hasOpenSession = hasOpenSession
    }
}

public struct ProgramPartnerSummaryDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var name: String?
    public var logoDarkKey: String?
    public var logoLightKey: String?
    public var logoDarkUrl: String?
    public var logoLightUrl: String?
    public var websiteUrl: String?

    public init(
        id: String,
        slug: String,
        name: String? = nil,
        logoDarkKey: String? = nil,
        logoLightKey: String? = nil,
        logoDarkUrl: String? = nil,
        logoLightUrl: String? = nil,
        websiteUrl: String? = nil
    ) {
        self.id = id
        self.slug = slug
        self.name = name
        self.logoDarkKey = logoDarkKey
        self.logoLightKey = logoLightKey
        self.logoDarkUrl = logoDarkUrl
        self.logoLightUrl = logoLightUrl
        self.websiteUrl = websiteUrl
    }
}

public struct ProgramRegistrationSubmittedDto: Codable, Equatable, Sendable {
    public var registrationNo: String
    public var status: String

    public init(
        registrationNo: String,
        status: String
    ) {
        self.registrationNo = registrationNo
        self.status = status
    }
}

public struct ProgramSessionDto: Codable, Equatable, Sendable {
    public var id: String
    public var startOn: String?
    public var endOn: String?
    public var weeklySchedule: String?
    public var capacity: Int?
    public var enrolledCount: Int
    public var price: Int?
    public var earlyBirdPrice: Int?
    public var earlyBirdUntil: String?
    public var signupOpensAt: JSONValue?
    public var signupClosesAt: JSONValue?
    public var status: String
    public var venueId: String?
    public var venueName: String?
    public var venueAddress: String?
    public var venueLat: Double?
    public var venueLng: Double?

    public init(
        id: String,
        startOn: String? = nil,
        endOn: String? = nil,
        weeklySchedule: String? = nil,
        capacity: Int? = nil,
        enrolledCount: Int,
        price: Int? = nil,
        earlyBirdPrice: Int? = nil,
        earlyBirdUntil: String? = nil,
        signupOpensAt: JSONValue? = nil,
        signupClosesAt: JSONValue? = nil,
        status: String,
        venueId: String? = nil,
        venueName: String? = nil,
        venueAddress: String? = nil,
        venueLat: Double? = nil,
        venueLng: Double? = nil
    ) {
        self.id = id
        self.startOn = startOn
        self.endOn = endOn
        self.weeklySchedule = weeklySchedule
        self.capacity = capacity
        self.enrolledCount = enrolledCount
        self.price = price
        self.earlyBirdPrice = earlyBirdPrice
        self.earlyBirdUntil = earlyBirdUntil
        self.signupOpensAt = signupOpensAt
        self.signupClosesAt = signupClosesAt
        self.status = status
        self.venueId = venueId
        self.venueName = venueName
        self.venueAddress = venueAddress
        self.venueLat = venueLat
        self.venueLng = venueLng
    }
}

public struct ProgramStaffSummaryDto: Codable, Equatable, Sendable {
    public var id: String
    public var name: String?

    public init(
        id: String,
        name: String? = nil
    ) {
        self.id = id
        self.name = name
    }
}

public struct ProposalDownloadRequest: Codable, Equatable, Sendable {
    public var company: String?
    public var name: String?
    public var email: String?
    public var consent: Bool?
    public var lang: String?
    public var sourcePath: String?
    public var utmSource: String?
    public var utmCampaign: String?
    public var website: String?

    public init(
        company: String? = nil,
        name: String? = nil,
        email: String? = nil,
        consent: Bool? = nil,
        lang: String? = nil,
        sourcePath: String? = nil,
        utmSource: String? = nil,
        utmCampaign: String? = nil,
        website: String? = nil
    ) {
        self.company = company
        self.name = name
        self.email = email
        self.consent = consent
        self.lang = lang
        self.sourcePath = sourcePath
        self.utmSource = utmSource
        self.utmCampaign = utmCampaign
        self.website = website
    }
}

public struct ProposalDownloadResultDto: Codable, Equatable, Sendable {
    public var downloadPath: String?
    public var expiresAt: JSONValue?

    public init(
        downloadPath: String? = nil,
        expiresAt: JSONValue? = nil
    ) {
        self.downloadPath = downloadPath
        self.expiresAt = expiresAt
    }
}

public struct PublicCalendarEventDto: Codable, Equatable, Sendable {
    public var sourceType: String
    public var id: String
    public var startsAt: JSONValue
    public var endsAt: JSONValue?
    public var isAllDay: Bool
    public var title: String
    public var teamCodes: [String]
    public var venueName: String?
    public var seasonCode: String?
    public var competitionTag: String?
    public var competitionName: String?
    public var status: String?
    public var homeAway: String?
    public var scoreHome: Int?
    public var scoreAway: Int?
    public var roundNo: Int?
    public var matchNo: Int?
    public var originalMatchOn: String?
    public var originalKickoff: String?
    public var eventTypeCode: String?
    public var description: String?
    public var ctaUrl: String?
    public var coverKey: String?
    public var coverUrl: String?

    public init(
        sourceType: String,
        id: String,
        startsAt: JSONValue,
        endsAt: JSONValue? = nil,
        isAllDay: Bool,
        title: String,
        teamCodes: [String],
        venueName: String? = nil,
        seasonCode: String? = nil,
        competitionTag: String? = nil,
        competitionName: String? = nil,
        status: String? = nil,
        homeAway: String? = nil,
        scoreHome: Int? = nil,
        scoreAway: Int? = nil,
        roundNo: Int? = nil,
        matchNo: Int? = nil,
        originalMatchOn: String? = nil,
        originalKickoff: String? = nil,
        eventTypeCode: String? = nil,
        description: String? = nil,
        ctaUrl: String? = nil,
        coverKey: String? = nil,
        coverUrl: String? = nil
    ) {
        self.sourceType = sourceType
        self.id = id
        self.startsAt = startsAt
        self.endsAt = endsAt
        self.isAllDay = isAllDay
        self.title = title
        self.teamCodes = teamCodes
        self.venueName = venueName
        self.seasonCode = seasonCode
        self.competitionTag = competitionTag
        self.competitionName = competitionName
        self.status = status
        self.homeAway = homeAway
        self.scoreHome = scoreHome
        self.scoreAway = scoreAway
        self.roundNo = roundNo
        self.matchNo = matchNo
        self.originalMatchOn = originalMatchOn
        self.originalKickoff = originalKickoff
        self.eventTypeCode = eventTypeCode
        self.description = description
        self.ctaUrl = ctaUrl
        self.coverKey = coverKey
        self.coverUrl = coverUrl
    }
}

public struct PublicCalendarEventTypeDto: Codable, Equatable, Sendable {
    public var code: String
    public var name: String
    public var colour: String?
    public var icon: String?

    public init(
        code: String,
        name: String,
        colour: String? = nil,
        icon: String? = nil
    ) {
        self.code = code
        self.name = name
        self.colour = colour
        self.icon = icon
    }
}

public struct PublicCalendarSettingsDto: Codable, Equatable, Sendable {
    public var defaultView: String
    public var defaultRange: String
    public var defaultTeamCode: String
    public var homeTeamCodes: [String]
    public var firstTeamCode: String?
    public var teams: [PublicCalendarTeamDto]
    public var eventTypes: [PublicCalendarEventTypeDto]

    public init(
        defaultView: String,
        defaultRange: String,
        defaultTeamCode: String,
        homeTeamCodes: [String],
        firstTeamCode: String? = nil,
        teams: [PublicCalendarTeamDto],
        eventTypes: [PublicCalendarEventTypeDto]
    ) {
        self.defaultView = defaultView
        self.defaultRange = defaultRange
        self.defaultTeamCode = defaultTeamCode
        self.homeTeamCodes = homeTeamCodes
        self.firstTeamCode = firstTeamCode
        self.teams = teams
        self.eventTypes = eventTypes
    }
}

public struct PublicCalendarTeamDto: Codable, Equatable, Sendable {
    public var code: String
    public var displayName: String
    public var colour: String?
    public var sortOrder: Int

    public init(
        code: String,
        displayName: String,
        colour: String? = nil,
        sortOrder: Int
    ) {
        self.code = code
        self.displayName = displayName
        self.colour = colour
        self.sortOrder = sortOrder
    }
}

public struct PublicCharityImageDto: Codable, Equatable, Sendable {
    public var imageUrl: String
    public var thumbUrl: String?

    public init(
        imageUrl: String,
        thumbUrl: String? = nil
    ) {
        self.imageUrl = imageUrl
        self.thumbUrl = thumbUrl
    }
}

public struct PublicCharityOrgDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var intro: String?
    public var logoUrl: String?
    public var websiteUrl: String?

    public init(
        slug: String,
        name: String? = nil,
        intro: String? = nil,
        logoUrl: String? = nil,
        websiteUrl: String? = nil
    ) {
        self.slug = slug
        self.name = name
        self.intro = intro
        self.logoUrl = logoUrl
        self.websiteUrl = websiteUrl
    }
}

public struct PublicCrawlerSettingsDto: Codable, Equatable, Sendable {
    public var userAgents: [CrawlerAgentDto]
    public var excludePaths: [String]

    public init(
        userAgents: [CrawlerAgentDto],
        excludePaths: [String]
    ) {
        self.userAgents = userAgents
        self.excludePaths = excludePaths
    }
}

public struct PublicFormDto: Codable, Equatable, Sendable {
    public var formCode: String
    public var formNameZh: String
    public var formNameEn: String
    public var captchaEnabled: Bool
    public var fields: [PublicFormFieldDto]

    public init(
        formCode: String,
        formNameZh: String,
        formNameEn: String,
        captchaEnabled: Bool,
        fields: [PublicFormFieldDto]
    ) {
        self.formCode = formCode
        self.formNameZh = formNameZh
        self.formNameEn = formNameEn
        self.captchaEnabled = captchaEnabled
        self.fields = fields
    }
}

public struct PublicFormFieldDto: Codable, Equatable, Sendable {
    public var fieldKey: String
    public var fieldType: String
    public var label: String
    public var isRequired: Bool
    public var validationRule: String?
    public var options: [String]?
    public var optionLabels: [String]?
    public var sortOrder: Int

    public init(
        fieldKey: String,
        fieldType: String,
        label: String,
        isRequired: Bool,
        validationRule: String? = nil,
        options: [String]? = nil,
        optionLabels: [String]? = nil,
        sortOrder: Int
    ) {
        self.fieldKey = fieldKey
        self.fieldType = fieldType
        self.label = label
        self.isRequired = isRequired
        self.validationRule = validationRule
        self.options = options
        self.optionLabels = optionLabels
        self.sortOrder = sortOrder
    }
}

public struct PublicLlmsContentDto: Codable, Equatable, Sendable {
    public var positioningZh: String?
    public var positioningEn: String?
    public var keyPagesZh: String?
    public var keyPagesEn: String?
    public var factsSummaryZh: String?
    public var factsSummaryEn: String?
    public var licenseZh: String?
    public var licenseEn: String?
    public var contactZh: String?
    public var contactEn: String?

    public init(
        positioningZh: String? = nil,
        positioningEn: String? = nil,
        keyPagesZh: String? = nil,
        keyPagesEn: String? = nil,
        factsSummaryZh: String? = nil,
        factsSummaryEn: String? = nil,
        licenseZh: String? = nil,
        licenseEn: String? = nil,
        contactZh: String? = nil,
        contactEn: String? = nil
    ) {
        self.positioningZh = positioningZh
        self.positioningEn = positioningEn
        self.keyPagesZh = keyPagesZh
        self.keyPagesEn = keyPagesEn
        self.factsSummaryZh = factsSummaryZh
        self.factsSummaryEn = factsSummaryEn
        self.licenseZh = licenseZh
        self.licenseEn = licenseEn
        self.contactZh = contactZh
        self.contactEn = contactEn
    }
}

public struct PublicProposalDto: Codable, Equatable, Sendable {
    public var id: String
    public var title: String
    public var versionNo: Int
    public var locales: [String]

    public init(
        id: String,
        title: String,
        versionNo: Int,
        locales: [String]
    ) {
        self.id = id
        self.title = title
        self.versionNo = versionNo
        self.locales = locales
    }
}

public struct PublicRedirectDto: Codable, Equatable, Sendable {
    public var fromPath: String
    public var toPath: String

    public init(
        fromPath: String,
        toPath: String
    ) {
        self.fromPath = fromPath
        self.toPath = toPath
    }
}

public struct PublicSeoSettingsDto: Codable, Equatable, Sendable {
    public var titleTemplateZh: String?
    public var titleTemplateEn: String?
    public var defaultDescriptionZh: String?
    public var defaultDescriptionEn: String?
    public var robotsCustomRules: String?
    public var ga4MeasurementId: String?
    public var gtmContainerId: String?
    public var metaPixelId: String?
    public var lineTagId: String?
    public var ogImageUrl: String?
    public var ogImageWidth: Int?
    public var ogImageHeight: Int?

    public init(
        titleTemplateZh: String? = nil,
        titleTemplateEn: String? = nil,
        defaultDescriptionZh: String? = nil,
        defaultDescriptionEn: String? = nil,
        robotsCustomRules: String? = nil,
        ga4MeasurementId: String? = nil,
        gtmContainerId: String? = nil,
        metaPixelId: String? = nil,
        lineTagId: String? = nil,
        ogImageUrl: String? = nil,
        ogImageWidth: Int? = nil,
        ogImageHeight: Int? = nil
    ) {
        self.titleTemplateZh = titleTemplateZh
        self.titleTemplateEn = titleTemplateEn
        self.defaultDescriptionZh = defaultDescriptionZh
        self.defaultDescriptionEn = defaultDescriptionEn
        self.robotsCustomRules = robotsCustomRules
        self.ga4MeasurementId = ga4MeasurementId
        self.gtmContainerId = gtmContainerId
        self.metaPixelId = metaPixelId
        self.lineTagId = lineTagId
        self.ogImageUrl = ogImageUrl
        self.ogImageWidth = ogImageWidth
        self.ogImageHeight = ogImageHeight
    }
}

public struct PublicSiteFactContactDto: Codable, Equatable, Sendable {
    public var address: String?
    public var phone: String?
    public var hours: String?

    public init(
        address: String? = nil,
        phone: String? = nil,
        hours: String? = nil
    ) {
        self.address = address
        self.phone = phone
        self.hours = hours
    }
}

public struct PublicSiteFactLeagueDto: Codable, Equatable, Sendable {
    public var name: String?
    public var shortName: String?

    public init(
        name: String? = nil,
        shortName: String? = nil
    ) {
        self.name = name
        self.shortName = shortName
    }
}

public struct PublicSiteFactVenueDto: Codable, Equatable, Sendable {
    public var name: String
    public var address: String?
    public var isHomeGround: Bool

    public init(
        name: String,
        address: String? = nil,
        isHomeGround: Bool
    ) {
        self.name = name
        self.address = address
        self.isHomeGround = isHomeGround
    }
}

public struct PublicSiteFactsDto: Codable, Equatable, Sendable {
    public var foundedYear: String?
    public var foundingDateIso: String?
    public var foundedDisplay: String?
    public var foundingTitle: String?
    public var league: PublicSiteFactLeagueDto
    public var venues: [PublicSiteFactVenueDto]
    public var squadStructureSummary: String?
    public var squadCodes: [String]
    public var contact: PublicSiteFactContactDto
    public var blueWhaleSiteUrl: String?

    public init(
        foundedYear: String? = nil,
        foundingDateIso: String? = nil,
        foundedDisplay: String? = nil,
        foundingTitle: String? = nil,
        league: PublicSiteFactLeagueDto,
        venues: [PublicSiteFactVenueDto],
        squadStructureSummary: String? = nil,
        squadCodes: [String],
        contact: PublicSiteFactContactDto,
        blueWhaleSiteUrl: String? = nil
    ) {
        self.foundedYear = foundedYear
        self.foundingDateIso = foundingDateIso
        self.foundedDisplay = foundedDisplay
        self.foundingTitle = foundingTitle
        self.league = league
        self.venues = venues
        self.squadStructureSummary = squadStructureSummary
        self.squadCodes = squadCodes
        self.contact = contact
        self.blueWhaleSiteUrl = blueWhaleSiteUrl
    }
}

public struct RegisterAppDeviceRequest: Codable, Equatable, Sendable {
    public var platform: String
    public var osVersion: String?
    public var appVersion: String?
    public var locale: String?
    public var pushToken: String?
    public var pushPermission: String?

    public init(
        platform: String,
        osVersion: String? = nil,
        appVersion: String? = nil,
        locale: String? = nil,
        pushToken: String? = nil,
        pushPermission: String? = nil
    ) {
        self.platform = platform
        self.osVersion = osVersion
        self.appVersion = appVersion
        self.locale = locale
        self.pushToken = pushToken
        self.pushPermission = pushPermission
    }
}

public struct SeasonRefDto: Codable, Equatable, Sendable {
    public var code: String
    public var startOn: String
    public var endOn: String

    public init(
        code: String,
        startOn: String,
        endOn: String
    ) {
        self.code = code
        self.startOn = startOn
        self.endOn = endOn
    }
}

public struct SetCartItemRequest: Codable, Equatable, Sendable {
    public var quantity: Int?

    public init(
        quantity: Int? = nil
    ) {
        self.quantity = quantity
    }
}

public struct ShopCartDto: Codable, Equatable, Sendable {
    public var cartToken: String?
    public var clubCode: String
    public var items: [ShopCartItemDto]
    public var itemCount: Int
    public var subtotal: Int
    public var shipping: ShopCartShippingDto
    public var canCheckout: Bool

    public init(
        cartToken: String? = nil,
        clubCode: String,
        items: [ShopCartItemDto],
        itemCount: Int,
        subtotal: Int,
        shipping: ShopCartShippingDto,
        canCheckout: Bool
    ) {
        self.cartToken = cartToken
        self.clubCode = clubCode
        self.items = items
        self.itemCount = itemCount
        self.subtotal = subtotal
        self.shipping = shipping
        self.canCheckout = canCheckout
    }
}

public struct ShopCartItemDto: Codable, Equatable, Sendable {
    public var variantId: String
    public var productSlug: String
    public var productName: String?
    public var variantLabel: String
    public var sku: String
    public var imageThumbUrl: String?
    public var listPrice: Int
    public var unitPrice: Int
    public var onSale: Bool
    public var quantity: Int
    public var lineTotal: Int
    public var availableQty: Int
    public var purchasable: Bool
    public var issue: String?
    public var issueMessage: String?

    public init(
        variantId: String,
        productSlug: String,
        productName: String? = nil,
        variantLabel: String,
        sku: String,
        imageThumbUrl: String? = nil,
        listPrice: Int,
        unitPrice: Int,
        onSale: Bool,
        quantity: Int,
        lineTotal: Int,
        availableQty: Int,
        purchasable: Bool,
        issue: String? = nil,
        issueMessage: String? = nil
    ) {
        self.variantId = variantId
        self.productSlug = productSlug
        self.productName = productName
        self.variantLabel = variantLabel
        self.sku = sku
        self.imageThumbUrl = imageThumbUrl
        self.listPrice = listPrice
        self.unitPrice = unitPrice
        self.onSale = onSale
        self.quantity = quantity
        self.lineTotal = lineTotal
        self.availableQty = availableQty
        self.purchasable = purchasable
        self.issue = issue
        self.issueMessage = issueMessage
    }
}

public struct ShopCartShippingDto: Codable, Equatable, Sendable {
    public var fee: Int
    public var freeThreshold: Int?
    public var amountToFree: Int?

    public init(
        fee: Int,
        freeThreshold: Int? = nil,
        amountToFree: Int? = nil
    ) {
        self.fee = fee
        self.freeThreshold = freeThreshold
        self.amountToFree = amountToFree
    }
}

public struct ShopCollectionDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var narrative: String?
    public var productCount: Int

    public init(
        slug: String,
        name: String? = nil,
        narrative: String? = nil,
        productCount: Int
    ) {
        self.slug = slug
        self.name = name
        self.narrative = narrative
        self.productCount = productCount
    }
}

public struct ShopDeliveryMethodDto: Codable, Equatable, Sendable {
    public var code: String
    public var label: String
    public var baseFee: Int

    public init(
        code: String,
        label: String,
        baseFee: Int
    ) {
        self.code = code
        self.label = label
        self.baseFee = baseFee
    }
}

public struct ShopDonationCodeDto: Codable, Equatable, Sendable {
    public var code: String
    public var orgName: String

    public init(
        code: String,
        orgName: String
    ) {
        self.code = code
        self.orgName = orgName
    }
}

public struct ShopImageDto: Codable, Equatable, Sendable {
    public var url: String
    public var thumbUrl: String
    public var width: Int?
    public var height: Int?

    public init(
        url: String,
        thumbUrl: String,
        width: Int? = nil,
        height: Int? = nil
    ) {
        self.url = url
        self.thumbUrl = thumbUrl
        self.width = width
        self.height = height
    }
}

public struct ShopInfoDto: Codable, Equatable, Sendable {
    public var entryTitle: String?
    public var entryIntro: String?
    public var policyNotice: String?
    public var policyShipping: String?
    public var policyReturns: String?
    public var policyTerms: String?
    public var shippingFee: Int
    public var freeShippingThreshold: Int?
    public var excludedRegions: [String]
    public var deliveryMethods: [ShopDeliveryMethodDto]
    public var donationCodes: [ShopDonationCodeDto]
    public var collectingSubjectName: String?
    public var paymentAvailable: Bool
    public var collections: [ShopCollectionDto]

    public init(
        entryTitle: String? = nil,
        entryIntro: String? = nil,
        policyNotice: String? = nil,
        policyShipping: String? = nil,
        policyReturns: String? = nil,
        policyTerms: String? = nil,
        shippingFee: Int,
        freeShippingThreshold: Int? = nil,
        excludedRegions: [String],
        deliveryMethods: [ShopDeliveryMethodDto],
        donationCodes: [ShopDonationCodeDto],
        collectingSubjectName: String? = nil,
        paymentAvailable: Bool,
        collections: [ShopCollectionDto]
    ) {
        self.entryTitle = entryTitle
        self.entryIntro = entryIntro
        self.policyNotice = policyNotice
        self.policyShipping = policyShipping
        self.policyReturns = policyReturns
        self.policyTerms = policyTerms
        self.shippingFee = shippingFee
        self.freeShippingThreshold = freeShippingThreshold
        self.excludedRegions = excludedRegions
        self.deliveryMethods = deliveryMethods
        self.donationCodes = donationCodes
        self.collectingSubjectName = collectingSubjectName
        self.paymentAvailable = paymentAvailable
        self.collections = collections
    }
}

public struct ShopOrderDto: Codable, Equatable, Sendable {
    public var orderNo: String
    public var clubCode: String
    public var accessToken: String?
    public var status: String
    public var paymentStatus: String
    public var paymentStatusLabel: String
    public var paymentMethod: String
    public var paymentMethodLabel: String
    public var deliveryMethod: String
    public var deliveryMethodLabel: String
    public var subtotal: Int
    public var shippingFee: Int
    public var total: Int
    public var items: [ShopOrderItemDto]
    public var recipientName: String?
    public var recipientPhone: String?
    public var recipientAddress: String?
    public var buyerEmail: String?
    public var customerNote: String?
    public var isMasked: Bool
    public var invoice: ShopOrderInvoiceDto?
    public var shipment: ShopOrderShipmentDto?
    public var paymentUrl: String?
    public var expiresAt: JSONValue?
    public var canPay: Bool
    public var canCancel: Bool
    public var createdAt: JSONValue
    public var paidAt: JSONValue?

    public init(
        orderNo: String,
        clubCode: String,
        accessToken: String? = nil,
        status: String,
        paymentStatus: String,
        paymentStatusLabel: String,
        paymentMethod: String,
        paymentMethodLabel: String,
        deliveryMethod: String,
        deliveryMethodLabel: String,
        subtotal: Int,
        shippingFee: Int,
        total: Int,
        items: [ShopOrderItemDto],
        recipientName: String? = nil,
        recipientPhone: String? = nil,
        recipientAddress: String? = nil,
        buyerEmail: String? = nil,
        customerNote: String? = nil,
        isMasked: Bool,
        invoice: ShopOrderInvoiceDto? = nil,
        shipment: ShopOrderShipmentDto? = nil,
        paymentUrl: String? = nil,
        expiresAt: JSONValue? = nil,
        canPay: Bool,
        canCancel: Bool,
        createdAt: JSONValue,
        paidAt: JSONValue? = nil
    ) {
        self.orderNo = orderNo
        self.clubCode = clubCode
        self.accessToken = accessToken
        self.status = status
        self.paymentStatus = paymentStatus
        self.paymentStatusLabel = paymentStatusLabel
        self.paymentMethod = paymentMethod
        self.paymentMethodLabel = paymentMethodLabel
        self.deliveryMethod = deliveryMethod
        self.deliveryMethodLabel = deliveryMethodLabel
        self.subtotal = subtotal
        self.shippingFee = shippingFee
        self.total = total
        self.items = items
        self.recipientName = recipientName
        self.recipientPhone = recipientPhone
        self.recipientAddress = recipientAddress
        self.buyerEmail = buyerEmail
        self.customerNote = customerNote
        self.isMasked = isMasked
        self.invoice = invoice
        self.shipment = shipment
        self.paymentUrl = paymentUrl
        self.expiresAt = expiresAt
        self.canPay = canPay
        self.canCancel = canCancel
        self.createdAt = createdAt
        self.paidAt = paidAt
    }
}

public struct ShopOrderInvoiceDto: Codable, Equatable, Sendable {
    public var type: String
    public var typeLabel: String
    public var status: String
    public var statusLabel: String
    public var invoiceNo: String?
    public var issuedAt: JSONValue?
    public var taxId: String?
    public var donationCode: String?

    public init(
        type: String,
        typeLabel: String,
        status: String,
        statusLabel: String,
        invoiceNo: String? = nil,
        issuedAt: JSONValue? = nil,
        taxId: String? = nil,
        donationCode: String? = nil
    ) {
        self.type = type
        self.typeLabel = typeLabel
        self.status = status
        self.statusLabel = statusLabel
        self.invoiceNo = invoiceNo
        self.issuedAt = issuedAt
        self.taxId = taxId
        self.donationCode = donationCode
    }
}

public struct ShopOrderItemDto: Codable, Equatable, Sendable {
    public var productName: String
    public var variantLabel: String?
    public var sku: String
    public var unitPrice: Int
    public var quantity: Int
    public var lineTotal: Int

    public init(
        productName: String,
        variantLabel: String? = nil,
        sku: String,
        unitPrice: Int,
        quantity: Int,
        lineTotal: Int
    ) {
        self.productName = productName
        self.variantLabel = variantLabel
        self.sku = sku
        self.unitPrice = unitPrice
        self.quantity = quantity
        self.lineTotal = lineTotal
    }
}

public struct ShopOrderListItemDto: Codable, Equatable, Sendable {
    public var orderNo: String
    public var status: String
    public var paymentStatusLabel: String
    public var total: Int
    public var itemCount: Int
    public var firstItemName: String?
    public var invoiceNo: String?
    public var trackingNo: String?
    public var createdAt: JSONValue

    public init(
        orderNo: String,
        status: String,
        paymentStatusLabel: String,
        total: Int,
        itemCount: Int,
        firstItemName: String? = nil,
        invoiceNo: String? = nil,
        trackingNo: String? = nil,
        createdAt: JSONValue
    ) {
        self.orderNo = orderNo
        self.status = status
        self.paymentStatusLabel = paymentStatusLabel
        self.total = total
        self.itemCount = itemCount
        self.firstItemName = firstItemName
        self.invoiceNo = invoiceNo
        self.trackingNo = trackingNo
        self.createdAt = createdAt
    }
}

public struct ShopOrderShipmentDto: Codable, Equatable, Sendable {
    public var carrier: String?
    public var trackingNo: String?
    public var shippedAt: JSONValue?
    public var deliveredAt: JSONValue?
    public var pickupStatus: String?
    public var pickupStatusLabel: String?
    public var pickupDeadlineOn: String?

    public init(
        carrier: String? = nil,
        trackingNo: String? = nil,
        shippedAt: JSONValue? = nil,
        deliveredAt: JSONValue? = nil,
        pickupStatus: String? = nil,
        pickupStatusLabel: String? = nil,
        pickupDeadlineOn: String? = nil
    ) {
        self.carrier = carrier
        self.trackingNo = trackingNo
        self.shippedAt = shippedAt
        self.deliveredAt = deliveredAt
        self.pickupStatus = pickupStatus
        self.pickupStatusLabel = pickupStatusLabel
        self.pickupDeadlineOn = pickupDeadlineOn
    }
}

public struct ShopProductDetailDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var narrative: String?
    public var seoTitle: String?
    public var seoDescription: String?
    public var collectionSlug: String?
    public var collectionName: String?
    public var tags: [String]
    public var isNewArrival: Bool
    public var sizeChart: JSONValue?
    public var images: [ShopImageDto]
    public var variants: [ShopVariantDto]
    public var priceMin: Int?
    public var priceMax: Int?
    public var listPriceMin: Int?
    public var onSale: Bool
    public var stockStatus: String
    public var stockStatusLabel: String

    public init(
        slug: String,
        name: String? = nil,
        narrative: String? = nil,
        seoTitle: String? = nil,
        seoDescription: String? = nil,
        collectionSlug: String? = nil,
        collectionName: String? = nil,
        tags: [String],
        isNewArrival: Bool,
        sizeChart: JSONValue? = nil,
        images: [ShopImageDto],
        variants: [ShopVariantDto],
        priceMin: Int? = nil,
        priceMax: Int? = nil,
        listPriceMin: Int? = nil,
        onSale: Bool,
        stockStatus: String,
        stockStatusLabel: String
    ) {
        self.slug = slug
        self.name = name
        self.narrative = narrative
        self.seoTitle = seoTitle
        self.seoDescription = seoDescription
        self.collectionSlug = collectionSlug
        self.collectionName = collectionName
        self.tags = tags
        self.isNewArrival = isNewArrival
        self.sizeChart = sizeChart
        self.images = images
        self.variants = variants
        self.priceMin = priceMin
        self.priceMax = priceMax
        self.listPriceMin = listPriceMin
        self.onSale = onSale
        self.stockStatus = stockStatus
        self.stockStatusLabel = stockStatusLabel
    }
}

public struct ShopProductListItemDto: Codable, Equatable, Sendable {
    public var slug: String
    public var name: String?
    public var collectionSlug: String?
    public var collectionName: String?
    public var tags: [String]
    public var isNewArrival: Bool
    public var imageUrl: String?
    public var imageThumbUrl: String?
    public var priceMin: Int?
    public var priceMax: Int?
    public var listPriceMin: Int?
    public var onSale: Bool
    public var stockStatus: String
    public var stockStatusLabel: String
    public var sizes: [String]
    public var colours: [String]

    public init(
        slug: String,
        name: String? = nil,
        collectionSlug: String? = nil,
        collectionName: String? = nil,
        tags: [String],
        isNewArrival: Bool,
        imageUrl: String? = nil,
        imageThumbUrl: String? = nil,
        priceMin: Int? = nil,
        priceMax: Int? = nil,
        listPriceMin: Int? = nil,
        onSale: Bool,
        stockStatus: String,
        stockStatusLabel: String,
        sizes: [String],
        colours: [String]
    ) {
        self.slug = slug
        self.name = name
        self.collectionSlug = collectionSlug
        self.collectionName = collectionName
        self.tags = tags
        self.isNewArrival = isNewArrival
        self.imageUrl = imageUrl
        self.imageThumbUrl = imageThumbUrl
        self.priceMin = priceMin
        self.priceMax = priceMax
        self.listPriceMin = listPriceMin
        self.onSale = onSale
        self.stockStatus = stockStatus
        self.stockStatusLabel = stockStatusLabel
        self.sizes = sizes
        self.colours = colours
    }
}

public struct ShopVariantDto: Codable, Equatable, Sendable {
    public var id: String
    public var sku: String
    public var size: String?
    public var colour: String?
    public var label: String
    public var listPrice: Int
    public var price: Int
    public var onSale: Bool
    public var availableQty: Int
    public var purchasable: Bool

    public init(
        id: String,
        sku: String,
        size: String? = nil,
        colour: String? = nil,
        label: String,
        listPrice: Int,
        price: Int,
        onSale: Bool,
        availableQty: Int,
        purchasable: Bool
    ) {
        self.id = id
        self.sku = sku
        self.size = size
        self.colour = colour
        self.label = label
        self.listPrice = listPrice
        self.price = price
        self.onSale = onSale
        self.availableQty = availableQty
        self.purchasable = purchasable
    }
}

public struct SitemapEntryDto: Codable, Equatable, Sendable {
    public var path: String
    public var lastModifiedAt: JSONValue?

    public init(
        path: String,
        lastModifiedAt: JSONValue? = nil
    ) {
        self.path = path
        self.lastModifiedAt = lastModifiedAt
    }
}

public struct SponsorActivationDto: Codable, Equatable, Sendable {
    public var id: String
    public var title: String?
    public var happenedOn: String?
    public var resultSummary: String?
    public var images: [SponsorActivationImageDto]

    public init(
        id: String,
        title: String? = nil,
        happenedOn: String? = nil,
        resultSummary: String? = nil,
        images: [SponsorActivationImageDto]
    ) {
        self.id = id
        self.title = title
        self.happenedOn = happenedOn
        self.resultSummary = resultSummary
        self.images = images
    }
}

public struct SponsorActivationImageDto: Codable, Equatable, Sendable {
    public var imageUrl: String
    public var thumbUrl: String?
    public var imageWidth: Int?
    public var imageHeight: Int?

    public init(
        imageUrl: String,
        thumbUrl: String? = nil,
        imageWidth: Int? = nil,
        imageHeight: Int? = nil
    ) {
        self.imageUrl = imageUrl
        self.thumbUrl = thumbUrl
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
    }
}

public struct SponsorDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var tier: String?
    public var sortOrder: Int
    public var name: String?
    public var content: String?
    public var logoDarkUrl: String?
    public var logoLightUrl: String?
    public var stories: [SponsorStoryDto]
    public var activations: [SponsorActivationDto]
    public var charityPrograms: [PartnerCharityProgramDto]

    public init(
        id: String,
        slug: String,
        tier: String? = nil,
        sortOrder: Int,
        name: String? = nil,
        content: String? = nil,
        logoDarkUrl: String? = nil,
        logoLightUrl: String? = nil,
        stories: [SponsorStoryDto],
        activations: [SponsorActivationDto],
        charityPrograms: [PartnerCharityProgramDto]
    ) {
        self.id = id
        self.slug = slug
        self.tier = tier
        self.sortOrder = sortOrder
        self.name = name
        self.content = content
        self.logoDarkUrl = logoDarkUrl
        self.logoLightUrl = logoLightUrl
        self.stories = stories
        self.activations = activations
        self.charityPrograms = charityPrograms
    }
}

public struct SponsorPackageDto: Codable, Equatable, Sendable {
    public var id: String
    public var slug: String
    public var sortOrder: Int
    public var name: String?
    public var content: String?
    public var benefitList: String?
    public var audience: String?
    public var priceMin: Int?
    public var priceMax: Int?

    public init(
        id: String,
        slug: String,
        sortOrder: Int,
        name: String? = nil,
        content: String? = nil,
        benefitList: String? = nil,
        audience: String? = nil,
        priceMin: Int? = nil,
        priceMax: Int? = nil
    ) {
        self.id = id
        self.slug = slug
        self.sortOrder = sortOrder
        self.name = name
        self.content = content
        self.benefitList = benefitList
        self.audience = audience
        self.priceMin = priceMin
        self.priceMax = priceMax
    }
}

public struct SponsorStoryDto: Codable, Equatable, Sendable {
    public var slug: String
    public var title: String?
    public var summary: String?

    public init(
        slug: String,
        title: String? = nil,
        summary: String? = nil
    ) {
        self.slug = slug
        self.title = title
        self.summary = summary
    }
}

public struct StaffDto: Codable, Equatable, Sendable {
    public var id: String
    public var staffGroup: String?
    public var licence: String?
    public var photoKey: String?
    public var name: String?
    public var title: String?
    public var bio: String?
    public var teamCodes: [String]
    public var isShared: Bool
    public var photoUrl: String?
    public var schemaEligible: Bool

    public init(
        id: String,
        staffGroup: String? = nil,
        licence: String? = nil,
        photoKey: String? = nil,
        name: String? = nil,
        title: String? = nil,
        bio: String? = nil,
        teamCodes: [String],
        isShared: Bool,
        photoUrl: String? = nil,
        schemaEligible: Bool
    ) {
        self.id = id
        self.staffGroup = staffGroup
        self.licence = licence
        self.photoKey = photoKey
        self.name = name
        self.title = title
        self.bio = bio
        self.teamCodes = teamCodes
        self.isShared = isShared
        self.photoUrl = photoUrl
        self.schemaEligible = schemaEligible
    }
}

public struct StandingRowDto: Codable, Equatable, Sendable {
    public var rank: Int?
    public var teamName: String
    public var played: Int?
    public var points: Int?

    public init(
        rank: Int? = nil,
        teamName: String,
        played: Int? = nil,
        points: Int? = nil
    ) {
        self.rank = rank
        self.teamName = teamName
        self.played = played
        self.points = points
    }
}

public struct StandingsDto: Codable, Equatable, Sendable {
    public var season: SeasonRefDto?
    public var seasons: [String]
    public var items: [StandingRowDto]
    public var updatedAt: JSONValue?

    public init(
        season: SeasonRefDto? = nil,
        seasons: [String],
        items: [StandingRowDto],
        updatedAt: JSONValue? = nil
    ) {
        self.season = season
        self.seasons = seasons
        self.items = items
        self.updatedAt = updatedAt
    }
}

public struct SubmitFormRequest: Codable, Equatable, Sendable {
    public var answers: [String: String]
    public var sourcePath: String?
    public var utmSource: String?
    public var utmCampaign: String?
    public var website: String?

    public init(
        answers: [String: String],
        sourcePath: String? = nil,
        utmSource: String? = nil,
        utmCampaign: String? = nil,
        website: String? = nil
    ) {
        self.answers = answers
        self.sourcePath = sourcePath
        self.utmSource = utmSource
        self.utmCampaign = utmCampaign
        self.website = website
    }
}

public struct SubmitFormResultDto: Codable, Equatable, Sendable {
    public var success: Bool

    public init(
        success: Bool
    ) {
        self.success = success
    }
}

public struct SubmitProgramRegistrationRequest: Codable, Equatable, Sendable {
    public var applicantName: String
    public var phone: String?
    public var email: String?
    public var birthOn: String?
    public var guardianName: String?
    public var guardianPhone: String?
    public var healthDeclaration: String?
    public var note: String?

    public init(
        applicantName: String,
        phone: String? = nil,
        email: String? = nil,
        birthOn: String? = nil,
        guardianName: String? = nil,
        guardianPhone: String? = nil,
        healthDeclaration: String? = nil,
        note: String? = nil
    ) {
        self.applicantName = applicantName
        self.phone = phone
        self.email = email
        self.birthOn = birthOn
        self.guardianName = guardianName
        self.guardianPhone = guardianPhone
        self.healthDeclaration = healthDeclaration
        self.note = note
    }
}

public struct TeamDto: Codable, Equatable, Sendable {
    public var id: String
    public var code: String
    public var type: String
    public var gender: String
    public var ageBand: String?
    public var teamColor: String?
    public var heroKey: String?
    public var name: String?
    public var intro: String?
    public var heroUrl: String?
    public var logoUrl: String?
    public var schemaEligible: Bool

    public init(
        id: String,
        code: String,
        type: String,
        gender: String,
        ageBand: String? = nil,
        teamColor: String? = nil,
        heroKey: String? = nil,
        name: String? = nil,
        intro: String? = nil,
        heroUrl: String? = nil,
        logoUrl: String? = nil,
        schemaEligible: Bool
    ) {
        self.id = id
        self.code = code
        self.type = type
        self.gender = gender
        self.ageBand = ageBand
        self.teamColor = teamColor
        self.heroKey = heroKey
        self.name = name
        self.intro = intro
        self.heroUrl = heroUrl
        self.logoUrl = logoUrl
        self.schemaEligible = schemaEligible
    }
}

public struct UpdateAppSubscriptionsRequest: Codable, Equatable, Sendable {
    public var items: [AppSubscriptionInput]
    public var replaceAll: Bool?

    public init(
        items: [AppSubscriptionInput],
        replaceAll: Bool? = nil
    ) {
        self.items = items
        self.replaceAll = replaceAll
    }
}
