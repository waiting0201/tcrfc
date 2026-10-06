<script setup lang="ts">
// app/pages/zh/index.vue — 由 site/src/pages/zh/index.html 轉來（首頁，S0-9 切片驗證頁之一）
//
// 🔴 main 內容（下方樣板區塊）與 mockup 逐段一致，DOM 結構、class、文字內容不動；
// {{ROOT}} 在 mockup 是相對路徑 token，Nuxt 掛載於站根，一律省略（等同空字串）。
// ⛔ 原頁本身沒有頁內 style／script 標籤（唯一的行為邏輯來自共用的 site/src/assets/js/site.js），
// 這裡把該檔「賽事切換 tabs」與「主視覺輪播」兩段頁面專屬邏輯移入 script setup
// （sticky header／行動選單／mega menu 屬於版型層級，已移到 app/components/SiteHeader.vue）。
import type { PagedResponse } from '#shared/utils/api-types'
import type { CoreValueDto } from '#shared/utils/core-values'
import type { ShopProductListItem } from '#shared/utils/shop'

definePageMeta({ nav: 'home', unit: '01', bodyClass: 'page-home', enReady: true, enReadyBw: true })

// 文案依俱樂部切換（docs/13-blue-whale-site.md §6 紀律 11）：SEO、Hero 標語與
// 底下幾個「真人真事」區塊（賽事戰績、球員名單、新聞、商店實拍照）分屬 shared/
// utils/club-copy.ts 的資料層，或屬於動態內容（球員／新聞／賽程，見該檔檔頭
// 說明 (c)）——藍鯨目前這些區塊 0 素材，一律不顯示，不沿用磐石的真人真事資料
// 頂替（docs/13 踩雷點 8 同一道理：缺素材不放假的，直接不顯示這個區塊）。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const assets = computed(() => getClubAssets(clubKey.value))
// S1-12d 收尾第二輪：HOME_HERO／HOME_SEO／HOME_CTA_TRIO 三者含成立年份／聯賽／梯隊代碼
// 事實，club-copy.ts 已改為工廠函式，改讀 useSiteFacts(club) 取得的 facts（單一來源，
// 見 shared/utils/club-copy.ts 檔頭說明）。
const { facts: siteFacts } = useSiteFacts(clubKey.value)
// 英文版（主站 /en/）：文案取自 shared/utils/club-copy-en-core.ts（只回 tcrfc 英文值）。
const heroCopy = computed(() => (isEn.value ? (clubKey.value === 'bw' ? getHomeHeroEnBw(siteFacts.value) : getHomeHeroEn(siteFacts.value)) : getHomeHero(clubKey.value, siteFacts.value)))
const pillars = computed(() => (isEn.value ? (clubKey.value === 'bw' ? HOME_PILLARS_EN_BW : HOME_PILLARS_EN) : HOME_PILLARS[clubKey.value]))
const ctaTrio = computed(() => (isEn.value ? (clubKey.value === 'bw' ? getHomeCtaTrioEnBw(siteFacts.value) : getHomeCtaTrioEn()) : getHomeCtaTrio(clubKey.value, siteFacts.value)))
// 對陣雙方與球員橫幅上的「我方」名稱：英文版用英文俱樂部名稱。
const clubName = computed(() => (isEn.value ? (isTcrfc.value ? CLUB_NAME_EN : BW_NAME_EN) : assets.value.nameZh))

// S1-13：club-copy.ts 裡的 ctaPrimaryHref／pillars[].href／ctaTrio[].href 三組欄位存的是
// 「裸的 /zh/... 路徑」（該檔案的資料格式一律如此，不隨語系變化），樣板消費這些欄位時要
// 套一層 lp() 換算成目前路由語系——這裡是本輪 curl 實測時抓到的真實回歸（英文首頁的三個
// CTA 連結原本會把讀者導回 /zh/...），修法見下方樣板三處呼叫點。
const { locale, lp, isEn, tx } = useLocale()

const homeSeo = computed(() => (isEn.value ? (clubKey.value === 'bw' ? getHomeSeoEnBw(siteFacts.value) : getHomeSeoEn(siteFacts.value)) : getHomeSeo(clubKey.value, siteFacts.value)))

useSeoMeta({
  title: computed(() => homeSeo.value.title),
  description: computed(() => homeSeo.value.description),
})

// Organization JSON-LD（GEO-05／S1-12f）。首頁是最適合放站台層級 Organization 結構化資料的
// 頁面之一（規劃書只列型別清單，位置由本輪判斷——見 apps/web/README.md「S1-12f」節）。
// 資料是否合格（名稱／網址／隊徽齊全）由 apps/api 的 SchemaCompleteness 算好，這裡不重判一次
// （E-39）；現況見 useSchemaOrgClub.ts 檔頭說明。
useOrganizationSchema()

// ---- S1-14：首頁九大區塊資料源 ----
// 規劃書 §3.1「資料來源」欄逐區塊對應的既有公開 API：B3 首頁編排（區塊開關）、
// 賽事管理模組（最新賽事／近期賽事）、新聞模組（最新消息）。夥伴模組／商店模組
// 目前沒有對應的公開讀取端點（見 apps/web/README.md「S1-14」節「哪些是真資料」表），
// 這兩區塊維持既有靜態呈現，不臆造 API。
const { isSectionEnabled, isSectionExplicitlyEnabled, featuredBannerId, orderBlocks } = useHomeSections(config.public.club)

// B-14：頁面區塊由上到下的預設順序＋各自對應的後台區塊代碼（一線隊球員橫幅沒有自己的代碼，跟著前一塊走）。
// 後台「排序」改了之後 blockOrder 跟著動；不在排序清單的區塊維持這裡的相對位置，API 失敗＝這個預設順序。
const HOME_BLOCK_DEFAULTS = [
  { key: 'hero', codes: ['hero'] },
  { key: 'match', codes: ['upcoming_match', 'recent_fixtures'] },
  { key: 'roster', codes: [] },
  { key: 'core_values', codes: ['core_values'] },
  { key: 'ecosystem_nav', codes: ['ecosystem_nav'] },
  { key: 'latest_news', codes: ['latest_news'] },
  { key: 'shop_entry', codes: ['shop_entry'] },
  { key: 'partner_logos', codes: ['partner_logos'] },
  { key: 'bottom_cta', codes: ['bottom_cta'] },
] as const
const blockOrder = computed(() => orderBlocks(HOME_BLOCK_DEFAULTS))

// S3-9：五大核心價值改接 `GET /api/backend/{club}/home/core-values`（固定五項：code／中英名稱／排序／「了解更多」頁面）。
// 說明文字與圖示由前台依 `code` 對應（shared/utils/core-values.ts）；後端打不到或回空時退回同順序的備援。
// 區塊本身仍只對磐石顯示（藍鯨版標籤文字尚待客戶確認，藍鯨規劃書 §10 第 13 點）；藍鯨不發這個請求。
// B-14：藍鯨也發這個請求（拿掉寫死的 isTcrfc）。磐石維持「後端回空就用備援」；藍鯨沒有備援——
// 後台明確開啟 core_values 區塊且 API 真的回了資料才顯示，否則整塊不出現。
const { data: coreValuesData } = await useFetch<CoreValueDto[]>(`/api/backend/${config.public.club}/home/core-values`, {
  default: () => [],
})
const showCoreValues = computed(() => isTcrfc.value
  ? isSectionEnabled('core_values')
  : isSectionExplicitlyEnabled('core_values') && (coreValuesData.value?.length ?? 0) > 0)
const coreValues = computed(() => buildCoreValueViews(coreValuesData.value))
const coreValuesMore = computed(() => coreValueLearnMorePath(coreValuesData.value))

// S3-5：官方商店入口（shop_entry）改接商店資料——「精選商品與 Collection 入口」。精選＝後台排序的前 3 件上架商品
// （商品列表端點的預設排序）。只顯示名稱／圖片／價格，**不顯示庫存狀態**（首頁沒有 no-store，庫存不得出現在可被快取的畫面）。
// 磐石：沒有任何上架商品時維持既有的靜態入口（真實球衣照片）；藍鯨：沒有商品整個區塊不顯示。
const { data: shopEntryData } = await useFetch<PagedResponse<ShopProductListItem> | null>('/api/shop/products', {
  query: computed(() => ({ pageSize: 3, lang: locale.value })),
  key: `home-shop-entry-${config.public.club}-${locale.value}`,
  default: () => null,
})
const shopEntryProducts = computed(() => shopEntryData.value?.items ?? [])
const showShopEntry = computed(() => isSectionEnabled('shop_entry') && (isTcrfc.value || shopEntryProducts.value.length > 0))

// 贊助夥伴 Logo 牆（partner_logos）：S2-7 起接上 `GET /api/backend/{club}/partners?home=true`（後台 E1 勾選
// 「首頁曝光」的夥伴；只回合作期間涵蓋今天者）。規劃書 §3.1「依等級排序輪播，可點擊至 9.1」——
// 排序：依 9.1 五類型順序（策略→國際→訓練→教育→品牌，後端 `sortOrder` 在同類型內維持），自訂類型殿後；
// 🔴 「輪播」目前不做動態輪播（>15 家時只顯示前 15 家並附「查看全部夥伴」連結），理由見 README「S2-7」節。
// 藍鯨與磐石由端點的 club 分區，不混列。
const { partners: homePartners } = await usePartnerList({ home: true })
const HOME_PARTNER_LIMIT = 15
const homePartnerTiles = computed(() => {
  const order = new Map<string, number>(PARTNER_TYPE_SECTIONS.map((t, i) => [t.type, i]))
  return homePartners.value
    .map((p, idx) => ({ p, idx, rank: order.get(p.partnerType ?? '') ?? PARTNER_TYPE_SECTIONS.length }))
    .sort((a, b) => a.rank - b.rank || a.idx - b.idx)
    .map((x) => x.p)
    .slice(0, HOME_PARTNER_LIMIT)
})

// Banner（B3 首頁輪播）：apps/api 已於 E-64（2026-09-29）修正 `HomeRepository.ListBannersAsync`，
// 新增 `imageUrl`／`videoUrl`（由 `IImagePublicUrlResolver`／`IVideoPublicUrlResolver` 解析
// 出的完整可公開網址），比照 StaffDto／PlayerDto 的既有慣例，本輪接上。
const { data: bannersData } = await useFetch<Array<{
  id: string
  title: string | null
  subtitle: string | null
  cta2Label: string | null
  cta2Url: string | null
  mediaType: string
  imageUrl: string | null
  videoUrl: string | null
  imageAlt: string | null
  imageWidth: number | null
  imageHeight: number | null
  cta1Label: string | null
  cta1Url: string | null
}>>(`/api/backend/${config.public.club}/banners`, { query: { lang: locale.value } })
type HomeBanner = NonNullable<typeof bannersData.value>[number]
/** B-14：後台「精選輪播」指定的那則排第一；其餘維持 API 順序。 */
const orderedBanners = computed<HomeBanner[]>(() => {
  const list = bannersData.value ?? []
  const fid = featuredBannerId.value
  if (!fid) return list
  const hit = list.find((b) => b.id === fid)
  return hit ? [hit, ...list.filter((b) => b !== hit)] : list
})
const isDisplayableBanner = (b: HomeBanner) => (b.mediaType === 'video' ? Boolean(b.imageUrl && b.videoUrl) : Boolean(b.imageUrl))
/** Hero 文案跟隨「第一則」輪播：優先取第一則真的會顯示的，沒有就取排序後第一則。 */
const leadBanner = computed<HomeBanner | null>(() => orderedBanners.value.find(isDisplayableBanner) ?? orderedBanners.value[0] ?? null)
/** 英文頁不採用後端回退的繁中文字（含 CJK 字元），改走 heroCopy 的英文版。 */
function bannerText(raw: string | null | undefined): string | null {
  const t = raw?.trim()
  if (!t) return null
  return isEn.value && /[\u3400-\u9fff]/.test(t) ? null : t
}
/** A-10：站內連結（`/zh/...`）在英文頁要換成 `/en/...`；外部（http(s)）原樣；其他協定（javascript: 等）不輸出。 */
function bannerCta(label: string | null | undefined, rawUrl: string | null | undefined) {
  const l = bannerText(label)
  if (!l || !rawUrl) return null
  const url = safeUrl(rawUrl)
  return url ? { label: l, href: url.startsWith('/') ? lp(url) : url } : null
}
const primaryCta = computed(() => bannerCta(leadBanner.value?.cta1Label, leadBanner.value?.cta1Url))
const secondaryCta = computed(() => bannerCta(leadBanner.value?.cta2Label, leadBanner.value?.cta2Url))
// Banner 的標題／副標是後台純文字（不是 HTML），樣板一律用文字插值，不走 v-html（heroCopy 才是自家 HTML）。
const heroTitle = computed(() => bannerText(leadBanner.value?.title))
const heroSubtitle = computed(() => bannerText(leadBanner.value?.subtitle))

// ---- Hero 輪播素材：banners 有值就用真資料，沒有（兩俱樂部 `banners` 資料表目前皆 0 筆
// 種子資料，db/seed）就退回既有靜態素材，不得因為欄位缺值顯示壞圖 ----
interface HeroSlide {
  kind: 'image' | 'video'
  /** image 模式是輪播圖本身；video 模式是 `<video poster>` 海報格（docs/17 §6）。 */
  imageUrl: string
  /** 只有 kind==='video' 時有值。 */
  videoUrl: string
  alt: string
  width: number
  height: number
  objectPosition?: string
}
/** 既有 3 張真實照片（tcrfc 既有素材，S0-9 搬遷保留），banners 沒有可用資料時的回退。 */
const STATIC_TCRFC_HERO_SLIDES: HeroSlide[] = [
  { kind: 'image', imageUrl: siteImg('/assets/img/hero-01.jpg'), videoUrl: '', alt: tx('台中磐石球員於夜間賽事中振臂吶喊慶祝，場邊看板可見桃紅色 TCRFC 字樣', 'A Taichung Rock FC player celebrates with raised arms in a night match, with a pink TCRFC sign visible on the advertising boards'), width: 2400, height: 1600, objectPosition: '58% 35%' },
  { kind: 'image', imageUrl: siteImg('/assets/img/hero-02.jpg'), videoUrl: '', alt: tx('台中磐石5號球員於夜間賽事中揮腳觸球，身後可見場邊看台的球員與觀眾', 'Taichung Rock FC number 5 strikes the ball in a night match, with players and spectators in the stand behind'), width: 2400, height: 1600, objectPosition: '56% 30%' },
  { kind: 'image', imageUrl: siteImg('/assets/img/hero-03.jpg'), videoUrl: '', alt: tx('台中磐石一線隊球員賽前肩併肩圍成一圈，互相激勵士氣', 'Taichung Rock FC first team players stand shoulder to shoulder in a circle before kick-off, lifting each other\'s spirits'), width: 2400, height: 1600, objectPosition: '55% 42%' },
]
/** 只收「真的有完整網址可用」的輪播——image 模式要有 imageUrl；video 模式要海報圖與影片
 * 網址皆有，缺一律整則跳過，不得對缺欄位的資料猜網址（比不顯示更糟的是顯示壞圖）。 */
const heroBanners = computed<HeroSlide[]>(() =>
  orderedBanners.value
    .filter(isDisplayableBanner)
    .map((b) => ({
      kind: b.mediaType === 'video' ? ('video' as const) : ('image' as const),
      imageUrl: b.imageUrl ?? '',
      videoUrl: b.videoUrl ?? '',
      alt: b.imageAlt ?? '',
      width: b.imageWidth ?? 2400,
      height: b.imageHeight ?? 1600,
    })),
)
const heroSlides = computed<HeroSlide[]>(() => {
  if (heroBanners.value.length > 0) return heroBanners.value
  return isTcrfc.value ? STATIC_TCRFC_HERO_SLIDES : []
})

// ---- S1-14：賽事資料（最新賽事區／近期賽事，兩個規劃書區塊共用同一支 schedule API）----
const { data: scheduleData } = await useFetch(`/api/backend/${config.public.club}/schedule`, {
  query: { pageSize: 200, lang: locale.value },
})

interface HomeMatch {
  teamCode: string
  matchOn: string
  opponent: string | null
  venue: string | null
  competitionName: string | null
  status: string | null
  homeAway: string | null
  scoreHome: number | null
  scoreAway: number | null
}

const allMatches = computed<HomeMatch[]>(() => scheduleData.value?.items ?? [])
// SSR 渲染當下的日期字串（純顯示用的分界，不是安全判斷，兩端各自算一次即可）。
const todayStr = new Date().toISOString().slice(0, 10)

// 一線隊代號依俱樂部而定：磐石 D1、藍鯨 BW1（docs/14 踩雷點「BW1 不是第二個 D1」）——
// 這裡原本寫死 'D1'，對 bw 資料一定比對不到任何一筆，是 S1-14 發現並修正的既有落差。
// S1-19 收斂進 shared/utils/club.ts 的 getFirstTeamCode()，不再各頁各自寫一份三元運算式。
const firstTeamCode = computed(() => getFirstTeamCode(clubKey.value))

// ---- 一線隊球員橫幅（`roster-strip`）：接 `GET /{club}/players?team=<一線隊代號>` ----
// 取 5 位：有肖像（`photoUrl`，後端已套肖像同意 fail-closed，未同意者一律 null）的球員優先，
// 不足 5 位再用其餘球員（以隊徽卡呈現，與一線隊頁同一做法）補滿，各自依背號排序。
// 沒有任何球員資料（例：藍鯨名單尚未建立）整個橫幅不顯示，不沿用磐石寫死球員頂替。
interface RosterPlayer {
  id: string
  shirtNo: number | null
  position: string | null
  name: string | null
  photoUrl: string | null
}
const { data: rosterData } = await useFetch<PagedResponse<RosterPlayer>>(`/api/backend/${config.public.club}/players`, {
  query: { team: firstTeamCode.value, pageSize: 100, lang: locale.value },
})
const ROSTER_STRIP_SIZE = 5
const rosterStrip = computed<RosterPlayer[]>(() => {
  const byNo = (a: RosterPlayer, b: RosterPlayer) => (a.shirtNo ?? 999) - (b.shirtNo ?? 999)
  const all = (rosterData.value?.items ?? []).filter((p) => p.name)
  const withPhoto = all.filter((p) => p.photoUrl).sort(byNo)
  const without = all.filter((p) => !p.photoUrl).sort(byNo)
  return [...withPhoto, ...without].slice(0, ROSTER_STRIP_SIZE).sort(byNo)
})

const d1Sorted = computed(() =>
  allMatches.value.filter((m) => m.teamCode === firstTeamCode.value).slice().sort((a, b) => a.matchOn.localeCompare(b.matchOn)),
)
const d1Played = computed(() => d1Sorted.value.filter((m) => m.status === 'played' && m.matchOn <= todayStr))
const d1Upcoming = computed(() => d1Sorted.value.filter((m) => m.status === 'scheduled' && m.matchOn >= todayStr))
const latestResult = computed(() => d1Played.value.at(-1) ?? null)
const previousResult = computed(() => d1Played.value.at(-2) ?? null)
const nextFixture = computed(() => d1Upcoming.value[0] ?? null)

/** 主場視角換算：homeAway 是「本俱樂部」的主客場，不是聯賽官方主隊。 */
function clubScore(m: HomeMatch): number | null {
  return m.homeAway === 'AWAY' ? m.scoreAway : m.scoreHome
}
function opponentScore(m: HomeMatch): number | null {
  return m.homeAway === 'AWAY' ? m.scoreHome : m.scoreAway
}
function resultMetaLine(m: HomeMatch): string {
  return [m.competitionName ?? '', m.matchOn.replaceAll('-', '/')].filter(Boolean).join(' · ')
}
function fixtureMetaLine(m: HomeMatch): string {
  const [, mo, d] = m.matchOn.split('-')
  const wd = matchWeekday(m.matchOn).zh
  const haLabel = m.homeAway === 'AWAY' ? tx('客場', 'Away') : m.homeAway === 'HOME' ? tx('主場', 'Home') : ''
  const dateLabel = isEn.value ? `${Number(mo)}/${Number(d)} (${matchWeekday(m.matchOn).en})` : `${Number(mo)}/${Number(d)}（${wd}）`
  return [m.competitionName ?? '', dateLabel, haLabel].filter(Boolean).join(' · ')
}

const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000
const cutoffStr = computed(() => new Date(Date.now() + THIRTY_DAYS_MS).toISOString().slice(0, 10))
/** 近期賽事（未來 30 天）：非一線隊（U15／U14／U12）——目前種子資料只有一線隊，
 * 這裡先接上真實邏輯，真的有梯隊賽程時會自然出現，不用再改程式碼。 */
const otherTeamUpcoming = computed(() =>
  allMatches.value
    .filter((m) => m.teamCode !== firstTeamCode.value && m.status === 'scheduled' && m.matchOn >= todayStr && m.matchOn <= cutoffStr.value)
    .sort((a, b) => a.matchOn.localeCompare(b.matchOn)),
)

// ---- S1-14：最新消息（latest_news）----
// 規劃書：「抓取 7.x 最新 3–6 則，可指定精選」——精選（IsFeatured）優先，不足再用最新日期補滿。
const { data: newsData } = await useFetch(`/api/backend/${config.public.club}/news`, {
  query: { pageSize: 20, lang: locale.value },
})
const homeNews = computed(() => {
  const items = newsData.value?.items ?? []
  return items
    .slice()
    .sort((a, b) => {
      if (a.isFeatured !== b.isFeatured) return a.isFeatured ? -1 : 1
      return (b.publishedAt ?? '').localeCompare(a.publishedAt ?? '')
    })
    .slice(0, 5)
})
/** Hero 內的兩張迷你新聞卡（`hero__news`）：有真實新聞就取最前面兩篇（與下方「最新消息」同一份排序），
 * 沒有才退回既有靜態兩張（磐石）。藍鯨 0 篇時維持不顯示。 */
const heroNews = computed(() => homeNews.value.slice(0, 2))
/** 首頁 mosaic 版位固定 5 格（feature／sml×2／wide×2），資料不足 5 篇時依序省略後面的格子。 */
const NEWS_VARIANTS = ['feature', 'sml', 'sml', 'wide', 'wide'] as const

// ---- Team chips（賽事行事曆的隊伍切換）----
const teamPanel = ref<'D1' | 'other'>('D1')

// ---- Hero slider — "方塊拆解"（block-dismantle）轉場 ----
// 出場那張照片會暫時被一格一格的磚塊覆蓋（每次轉場即時建立、用完即丟），
// 每塊磚透過 background-position 顯示同一張照片的裁切局部（共用一個圖片網址，
// 不會多發圖片請求）；磚塊的 background-size / position 依出場 <img> 實際的
// object-fit:cover 幾何（容器尺寸、原始尺寸、object-position）換算，讓馬賽克
// 在任何裝置寬度下都能與底下的照片像素對齊。轉場時磚塊由網格中心向外淡出縮小，
// 顯示已經換到底層的下一張——節制、約 0.9 秒、無旋轉無變色。
const heroSectionEl = ref<HTMLElement | null>(null)
const sliderEl = ref<HTMLElement | null>(null)
const statusEl = ref<HTMLElement | null>(null)
// 這三個元素在 blockOrder 的 v-for 裡，一般 `ref="x"` 會被編譯成陣列，所以一律用函式 ref。
const setHeroSectionEl = (el: unknown) => { heroSectionEl.value = (el as HTMLElement | null) ?? null }
const setSliderEl = (el: unknown) => { sliderEl.value = (el as HTMLElement | null) ?? null }
const setStatusEl = (el: unknown) => { statusEl.value = (el as HTMLElement | null) ?? null }
const slideEls = ref<HTMLElement[]>([])

const COLS = 6
const ROWS = 4
const TILE_DURATION = 520 // ms — 對應 tcrfc.css 裡 .hero__tile 的 transition
const STAGGER_SPAN = 420 // ms — 網格內 transition-delay 的分布範圍
const AUTOPLAY_MS = 4400

const total = computed(() => heroSlides.value.length)
let current = 0
// 🔴 指示器（dots）的「目前張」必須是響應式狀態：`current` 是供動畫用的一般變數、不會觸發重繪，
// 原本 dots 的 `is-active` 在樣板裡寫死 `i === 0`，輪播切換後粉紅指示器永遠停在第一顆。
// 所有換張路徑（自動播放、箭頭、點指示器、滑動）都經 `goTo()`／`swapInstant()`，在那裡同步 `activeIndex`。
const activeIndex = ref(0)
let isAnimating = false
let timer: ReturnType<typeof setInterval> | null = null
let reduceMQ: MediaQueryList | null = null

function reduced() {
  return reduceMQ?.matches ?? false
}

function updateControls() {
  if (statusEl.value) {
    statusEl.value.textContent = tx(`目前顯示第 ${current + 1} 張，共 ${total.value} 張`, `Showing slide ${current + 1} of ${total.value}`)
  }
}

function swapInstant(index: number) {
  slideEls.value[current]?.classList.remove('is-active')
  slideEls.value[current]?.setAttribute('aria-hidden', 'true')
  slideEls.value[index]?.classList.add('is-active')
  slideEls.value[index]?.removeAttribute('aria-hidden')
  current = index
  activeIndex.value = index
  updateControls()
}

function animateSwap(index: number) {
  const outgoing = slideEls.value[current]
  const incoming = slideEls.value[index]
  const slider = sliderEl.value
  const img = outgoing?.querySelector<HTMLImageElement>('img')
  if (!outgoing || !incoming || !slider || !img || !img.naturalWidth) {
    // 出場那張還沒解碼完成（理論上不該發生，它正顯示在畫面上）——
    // 與其用不可信的幾何算馬賽克，不如直接切換。
    swapInstant(index)
    return
  }
  isAnimating = true

  const rect = slider.getBoundingClientRect()
  const cw = rect.width
  const ch = rect.height
  const naturalW = img.naturalWidth
  const naturalH = img.naturalHeight
  const imgAspect = naturalW / naturalH
  const boxAspect = cw / ch
  let scaledW: number
  let scaledH: number
  if (boxAspect > imgAspect) {
    scaledW = cw
    scaledH = cw / imgAspect
  } else {
    scaledH = ch
    scaledW = ch * imgAspect
  }

  const posParts = getComputedStyle(img).objectPosition.split(' ')
  const posX = (Number.parseFloat(posParts[0] ?? '') || 50) / 100
  const posY = (Number.parseFloat(posParts[1] ?? '') || 50) / 100
  const offsetX = (cw - scaledW) * posX
  const offsetY = (ch - scaledH) * posY
  const srcUrl = img.currentSrc || img.src

  // 磚塊顯示的是「出場」照片的裁切，所以要把「入場」那張抬到出場（仍完全不透明）
  // 之上——否則淡出的磚塊背後只會露出更多同一張出場照片，畫面在最後一刻突然
  // 硬切之前看起來什麼都沒發生。
  incoming.classList.add('is-active', 'hero__slide--front')
  incoming.removeAttribute('aria-hidden')

  const tiles = document.createElement('div')
  tiles.className = 'hero__tiles'
  tiles.style.setProperty('--tile-cols', String(COLS))
  tiles.style.setProperty('--tile-rows', String(ROWS))

  const tileW = cw / COLS
  const tileH = ch / ROWS
  const cx = (COLS - 1) / 2
  const cy = (ROWS - 1) / 2
  const maxDist = Math.sqrt(cx * cx + cy * cy) || 1
  const frag = document.createDocumentFragment()

  for (let r = 0; r < ROWS; r++) {
    for (let c = 0; c < COLS; c++) {
      const tile = document.createElement('span')
      tile.className = 'hero__tile'
      tile.style.backgroundImage = `url("${srcUrl}")`
      tile.style.backgroundSize = `${scaledW.toFixed(1)}px ${scaledH.toFixed(1)}px`
      tile.style.backgroundPosition = `${(offsetX - c * tileW).toFixed(1)}px ${(offsetY - r * tileH).toFixed(1)}px`

      const dx = c - cx
      const dy = r - cy
      const dist = Math.sqrt(dx * dx + dy * dy)
      const len = dist || 1
      tile.style.transitionDelay = `${((dist / maxDist) * STAGGER_SPAN).toFixed(0)}ms`
      tile.style.setProperty('--tx', `${((dx / len) * 12).toFixed(1)}px`)
      tile.style.setProperty('--ty', `${((dy / len) * 12).toFixed(1)}px`)
      frag.appendChild(tile)
    }
  }
  tiles.appendChild(frag)
  slider.appendChild(tiles)

  // 強制觸發 layout，讓「磚塊剛拼好」的初始狀態先繪製一次，
  // 下一個 frame 才加上觸發 transition 的 class。
  void tiles.offsetWidth
  window.requestAnimationFrame(() => {
    tiles.classList.add('is-out')
  })

  window.setTimeout(() => {
    outgoing.classList.remove('is-active')
    outgoing.setAttribute('aria-hidden', 'true')
    incoming.classList.remove('hero__slide--front')
    slider.removeChild(tiles)
    current = index
    isAnimating = false
    updateControls()
  }, STAGGER_SPAN + TILE_DURATION + 60)
}

function goTo(index: number) {
  const t = total.value
  if (t === 0) return
  const next = ((index % t) + t) % t
  if (next === current || isAnimating) return
  activeIndex.value = next // 指示器在轉場開始時就跟著走，不等動畫結束
  if (reduced()) swapInstant(next)
  else animateSwap(next)
}

function startAutoplay() {
  if (reduced() || total.value < 2) return
  stopAutoplay()
  timer = setInterval(() => goTo(current + 1), AUTOPLAY_MS)
  statusEl.value?.setAttribute('aria-live', 'off')
}
function stopAutoplay() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}
function pauseForInteraction() {
  stopAutoplay()
  statusEl.value?.setAttribute('aria-live', 'polite')
}
function resumeAutoplay() {
  startAutoplay()
}
function onReduceMotionChange() {
  if (reduced()) stopAutoplay()
  else resumeAutoplay()
}
// 觸控滑動：水平位移 ≥ 50px 且大於垂直位移才換張（不吃掉頁面的垂直捲動）；左滑＝下一張。
let touchX = 0
let touchY = 0
function onTouchStart(e: TouchEvent) {
  const t = e.touches[0]
  if (!t) return
  touchX = t.clientX
  touchY = t.clientY
  pauseForInteraction()
}
function onTouchEnd(e: TouchEvent) {
  const t = e.changedTouches[0]
  if (t) {
    const dx = t.clientX - touchX
    const dy = t.clientY - touchY
    if (Math.abs(dx) >= 50 && Math.abs(dx) > Math.abs(dy)) goTo(current + (dx < 0 ? 1 : -1))
  }
  resumeAutoplay()
}
function onHeroFocusout(e: FocusEvent) {
  if (!heroSectionEl.value?.contains(e.relatedTarget as Node)) resumeAutoplay()
}

onMounted(() => {
  if (heroSlides.value.length === 0) return // 沒有可顯示的輪播素材（見 heroSlides 計算邏輯），本頁不掛載輪播行為
  slideEls.value = Array.from(sliderEl.value?.querySelectorAll<HTMLElement>('.hero__slide') ?? [])
  reduceMQ = window.matchMedia('(prefers-reduced-motion: reduce)')

  heroSectionEl.value?.addEventListener('mouseenter', pauseForInteraction)
  heroSectionEl.value?.addEventListener('mouseleave', resumeAutoplay)
  heroSectionEl.value?.addEventListener('focusin', pauseForInteraction)
  heroSectionEl.value?.addEventListener('focusout', onHeroFocusout)
  heroSectionEl.value?.addEventListener('touchstart', onTouchStart, { passive: true })
  heroSectionEl.value?.addEventListener('touchend', onTouchEnd, { passive: true })
  reduceMQ.addEventListener('change', onReduceMotionChange)

  updateControls()
  startAutoplay()
})
onBeforeUnmount(() => {
  stopAutoplay()
  reduceMQ?.removeEventListener('change', onReduceMotionChange)
  heroSectionEl.value?.removeEventListener('mouseenter', pauseForInteraction)
  heroSectionEl.value?.removeEventListener('mouseleave', resumeAutoplay)
  heroSectionEl.value?.removeEventListener('focusin', pauseForInteraction)
  heroSectionEl.value?.removeEventListener('focusout', onHeroFocusout)
  heroSectionEl.value?.removeEventListener('touchstart', onTouchStart)
  heroSectionEl.value?.removeEventListener('touchend', onTouchEnd)
})
</script>

<template>
  <!-- 英文版：新聞、球員、夥伴名稱等 API 內容若有後端回的繁中備援，於頁面頂端提示（版面文字已全數英文）。 -->
  <LocaleFallbackNotice v-if="isEn && (hasFallbackLocale(newsData) || hasFallbackLocale(rosterData) || hasFallbackLocale(homePartners))" partial />
  <!-- B-14：區塊依後台「排序」渲染（blockOrder，見 useHomeSections.orderBlocks）；每塊的內容與 DOM 結構不變。 -->
  <template v-for="blk in blockOrder" :key="blk">
  <template v-if="blk === 'hero'">
  <section v-if="isSectionEnabled('hero')" :ref="setHeroSectionEl" class="hero" id="top" :aria-label="tx('首頁主視覺', 'Homepage hero')">
    <div v-if="heroSlides.length > 0" :ref="setSliderEl" class="hero__media" id="hero-slider" role="group" aria-roledescription="carousel" :aria-label="tx(`首頁主視覺輪播，共 ${heroSlides.length} 張`, `Homepage hero carousel, ${heroSlides.length} slides`)">
      <ul class="hero__slides">
        <li
          v-for="(slide, i) in heroSlides"
          :key="`${slide.kind}-${slide.imageUrl}-${i}`"
          class="hero__slide"
          :class="{ 'is-active': i === activeIndex }"
          :aria-hidden="i === 0 ? undefined : 'true'"
          role="group"
          aria-roledescription="slide"
          :aria-label="tx(`第 ${i + 1} 張，共 ${heroSlides.length} 張`, `Slide ${i + 1} of ${heroSlides.length}`)"
        >
          <video v-if="slide.kind === 'video'" :poster="slide.imageUrl" :width="slide.width" :height="slide.height" muted loop playsinline autoplay preload="metadata">
            <source :src="slide.videoUrl" type="video/mp4">
          </video>
          <img
            v-else
            :src="slide.imageUrl"
            :alt="slide.alt"
            :width="slide.width"
            :height="slide.height"
            :loading="i === 0 ? 'eager' : 'lazy'"
            :fetchpriority="i === 0 ? 'high' : undefined"
            :style="slide.objectPosition ? `object-position:${slide.objectPosition}` : undefined"
          >
        </li>
      </ul>
      <p :ref="setStatusEl" class="visually-hidden" id="hero-slide-status" aria-live="off" aria-atomic="true">{{ tx(`目前顯示第 1 張，共 ${heroSlides.length} 張`, `Showing slide 1 of ${heroSlides.length}`) }}</p>
    </div>
    <!-- 沒有可用的輪播素材時（藍鯨首頁 hero 圖未下載、無授權狀態，content/blue-whale/
         gap-analysis.md §2；或 banners 資料表暫無資料）改用純色塊，不沿用磐石的照片頂替
         （docs/13 踩雷點 8：缺素材不放假圖）。 -->
    <div v-else class="hero__media hero__media--pending" aria-hidden="true"></div>
    <div class="hero__scrim" aria-hidden="true"></div>
    <span class="ghost-num" aria-hidden="true">01</span>
    <div class="hero__inner">
      <div class="container">
        <div class="hero__grid">
          <div class="hero__copy">
            <p v-if="heroCopy.kickerEn" class="kicker kicker--on-dark">{{ heroCopy.kickerEn }}</p>
            <h1 v-if="heroTitle" class="hero__headline">{{ heroTitle }}</h1>
            <h1 v-else class="hero__headline" v-html="heroCopy.headlineZh"></h1>
            <p v-if="heroSubtitle" class="hero__sub">{{ heroSubtitle }}</p>
            <p v-else class="hero__sub" v-html="heroCopy.factLineZh"></p>
            <div class="hero__ctas">
              <a class="btn btn--primary" :href="primaryCta ? primaryCta.href : lp(heroCopy.ctaPrimaryHref)">{{ primaryCta ? primaryCta.label : tx('加入球隊', HOME_JOIN_LABEL_EN) }}</a>
              <a class="btn btn--light" :href="secondaryCta ? secondaryCta.href : lp(heroCopy.ctaSecondaryHref)">{{ secondaryCta ? secondaryCta.label : heroCopy.ctaSecondaryLabelZh }}</a>
            </div>
            <div v-if="heroSlides.length > 1" class="hero__slider-nav">
              <button type="button" class="hero__arrow hero__arrow--prev" data-hero-prev aria-controls="hero-slider" :aria-label="tx('上一張主視覺圖片', 'Previous hero image')" @click="goTo(current - 1)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 5l-7 7 7 7" /></svg>
              </button>
              <div class="hero__dots" role="tablist" :aria-label="tx('選擇主視覺圖片', 'Choose hero image')">
                <button
                  v-for="(slide, i) in heroSlides"
                  :key="`dot-${slide.imageUrl}-${i}`"
                  type="button"
                  class="hero__dot"
                  :class="{ 'is-active': i === activeIndex }"
                  role="tab"
                  :aria-selected="i === activeIndex ? 'true' : 'false'"
                  aria-controls="hero-slider"
                  :aria-label="tx(`第 ${i + 1} 張，共 ${heroSlides.length} 張`, `Slide ${i + 1} of ${heroSlides.length}`)"
                  :data-hero-goto="i"
                  @click="goTo(i)"
                ></button>
              </div>
              <button type="button" class="hero__arrow hero__arrow--next" data-hero-next aria-controls="hero-slider" :aria-label="tx('下一張主視覺圖片', 'Next hero image')" @click="goTo(current + 1)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 5l7 7-7 7" /></svg>
              </button>
            </div>
          </div>
          <!-- 藍鯨新聞 07 單元自有全文 0 篇（gap-analysis.md §4 #3），不沿用磐石新聞頂替，本區塊不顯示。 -->
          <div v-if="heroNews.length || isTcrfc" class="hero__news">
            <template v-if="heroNews.length">
              <a v-for="a in heroNews" :key="a.slug" class="hero-card clip-card clip-card--on-dark" :href="lp(`/zh/news/${a.slug}/`)">
                <div class="hero-card__media">
                  <img v-if="newsCoverImg(a, clubKey)" :src="newsCoverImg(a, clubKey)!.src" alt="" loading="lazy" :width="newsCoverImg(a, clubKey)!.width ?? undefined" :height="newsCoverImg(a, clubKey)!.height ?? undefined">
                  <img v-else class="news-card__media-mark" :src="newsFallbackMarkSrc(clubKey)" alt="" loading="lazy" width="64" height="67">
                </div>
                <div class="hero-card__body">
                  <span class="hero-card__tag">{{ a.categoryName }}</span>
                  <span class="hero-card__title">{{ a.title }}</span>
                </div>
              </a>
            </template>
            <template v-else>
            <a class="hero-card clip-card clip-card--on-dark" :href="lp('/zh/news/')">
              <div class="hero-card__media">
                <img :src="siteImg('/assets/img/news-trencin.jpg')" :alt="tx('台中磐石青訓球員與斯洛伐克 AS Trenčín 球員合影交流', 'Taichung Rock FC youth players pose for a group photo with players from AS Trenčín of Slovakia')" loading="lazy" width="1280" height="853">
              </div>
              <div class="hero-card__body">
                <span class="hero-card__tag">{{ tx('消息 News', 'News') }}</span>
                <span class="hero-card__title">{{ tx('台中磐石與 AS Trenčín 深化青訓合作', 'Taichung Rock FC and AS Trenčín deepen youth development cooperation') }}</span>
              </div>
            </a>
            <a class="hero-card clip-card clip-card--on-dark" :href="lp('/zh/news/')">
              <div class="hero-card__media">
                <img :src="siteImg('/assets/img/news-mcu.jpg')" :alt="tx('台中磐石 7 號球員於夜間比賽中盤球突破銘傳大學白色球衣防線', 'Taichung Rock FC number 7 dribbles past the white-shirted Ming Chuan University defence in a night match')" loading="lazy" width="1280" height="855">
              </div>
              <div class="hero-card__body">
                <span class="hero-card__tag">{{ tx('比賽 Matches', 'Matches') }}</span>
                <span class="hero-card__title">{{ tx('企甲聯賽：台中磐石 3-0 銘傳大學', 'League match: Taichung Rock FC 3-0 Ming Chuan University') }}</span>
              </div>
            </a>
            </template>
          </div>
        </div>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'match'">
  <!-- SPEC 3.1（最新賽事區／近期賽事）／3.13 — Match band
       資料來源：賽事管理模組（GET /api/backend/{club}/schedule，S1-14 起接上真實資料，
       見上方 script setup「賽事資料」段與 apps/web/README.md「S1-14」節）。
       藍鯨規劃書 §1.3 定「與主站同一套網站，只有配色不同」，本區塊本輪起兩俱樂部皆顯示
       （原本用 isTcrfc 整段隱藏，是還沒核對藍鯨規劃書就沿用的過度保守判斷，本輪修正）。
       藍鯨一線隊（BW1）目前只有 21 筆歷史賽果、沒有任何未來賽程（docs/13 §5 擋開發第 4
       項），下方「最新戰績／上一場」兩張卡會顯示真實比分，「下一場」卡會自然落到既有的
       「賽程尚未公告」占位文案——不是新造的假分支，是既有 v-else 邏輯本來就會產生的結果。 -->
  <section v-if="isSectionEnabled('upcoming_match') || isSectionEnabled('recent_fixtures')" class="band grain match-band" id="schedule" aria-labelledby="schedule-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">21</span>
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker kicker--on-dark">MATCHDAY</p>
          <h2 class="section-title" id="schedule-title">{{ tx('賽事行事曆', 'Schedule') }}</h2>
        </div>
      </div>

      <!-- 隊伍等級 chips：藍鯨青年隊只有 U15／U12（無 U14，見 db/seed 藍鯨隊伍名單），
           U14 chip 只在磐石顯示，不對藍鯨顯示不存在的隊別。 -->
      <div class="team-chips" role="group" :aria-label="tx('選擇隊伍等級', 'Select a team')">
        <button class="team-chip" type="button" :data-team="firstTeamCode" :aria-pressed="teamPanel === 'D1'" @click="teamPanel = 'D1'">{{ tx('一線隊 First Team', 'First Team') }}</button>
        <button class="team-chip" type="button" data-team="U15" :aria-pressed="teamPanel === 'other'" @click="teamPanel = 'other'">U15</button>
        <button v-if="isTcrfc" class="team-chip" type="button" data-team="U14" :aria-pressed="false" @click="teamPanel = 'other'">U14</button>
        <button class="team-chip" type="button" data-team="U12" :aria-pressed="false" @click="teamPanel = 'other'">U12</button>
      </div>

      <div v-if="isSectionEnabled('upcoming_match')" class="match-grid" id="match-grid-d1" data-team-panel="D1" :hidden="teamPanel !== 'D1'">
        <article v-if="latestResult" class="match-card">
          <div class="match-card__label"><span>{{ tx('最新戰績 LATEST RESULT', 'LATEST RESULT') }}</span></div>
          <p class="match-card__meta">{{ resultMetaLine(latestResult) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ clubName }}</span>
            <span class="match-card__score">{{ clubScore(latestResult) }}<span class="sep">:</span>{{ opponentScore(latestResult) }}</span>
            <span class="match-card__team match-card__team--away">{{ latestResult.opponent }}</span>
          </div>
        </article>
        <article v-else class="match-card match-card--placeholder">
          <p>{{ tx('尚無已完賽數據，敬請鎖定近期賽事。', 'No completed matches yet. Stay tuned for upcoming fixtures.') }}</p>
        </article>

        <article v-if="previousResult" class="match-card">
          <div class="match-card__label"><span>{{ tx('上一場 PREVIOUS', 'PREVIOUS') }}</span></div>
          <p class="match-card__meta">{{ resultMetaLine(previousResult) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ clubName }}</span>
            <span class="match-card__score">{{ clubScore(previousResult) }}<span class="sep">:</span>{{ opponentScore(previousResult) }}</span>
            <span class="match-card__team match-card__team--away">{{ previousResult.opponent }}</span>
          </div>
        </article>

        <article v-if="nextFixture" class="match-card match-card--next">
          <div class="match-card__label"><span>{{ tx('下一場 NEXT FIXTURE', 'NEXT FIXTURE') }}</span></div>
          <p class="match-card__meta">{{ fixtureMetaLine(nextFixture) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ clubName }}</span>
            <span class="match-card__vs">VS</span>
            <span class="match-card__team match-card__team--away">{{ nextFixture.opponent }}</span>
          </div>
          <p class="match-card__scorers">{{ nextFixture.venue }}{{ tx('　賽程以官方公告為準', ' · Fixtures are subject to official announcements') }}</p>
        </article>
        <article v-else class="match-card match-card--placeholder">
          <p>{{ tx('下一場賽程尚未公告，敬請關注後續公告。', 'The next fixture has not been announced yet. Please check back for updates.') }}</p>
        </article>
      </div>

      <div v-if="isSectionEnabled('recent_fixtures')" class="match-grid" id="match-grid-other" data-team-panel="other" :hidden="teamPanel !== 'other'">
        <article v-for="m in otherTeamUpcoming" :key="`${m.teamCode}-${m.matchOn}-${m.opponent}`" class="match-card">
          <div class="match-card__label"><span>{{ m.teamCode }}</span></div>
          <p class="match-card__meta">{{ fixtureMetaLine(m) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ clubName }}</span>
            <span class="match-card__vs">VS</span>
            <span class="match-card__team match-card__team--away">{{ m.opponent }}</span>
          </div>
        </article>
        <div v-if="otherTeamUpcoming.length === 0" class="match-card match-card--placeholder">
          <p>{{ tx('青訓梯隊賽程尚未公開發布，敬請關注後續公告。', `${isTcrfc ? 'Academy' : 'Youth'} age-group fixtures have not been published yet. Please check back for updates.`) }}</p>
        </div>
      </div>

      <div class="match-band__actions">
        <a class="btn btn--light" :href="lp('/zh/schedule/')">{{ tx('查看完整行事曆', 'View Full Schedule') }}</a>
        <a class="btn btn--light" :href="lp('/zh/schedule/')">{{ tx('訂閱一線隊賽程 (.ics)', 'Subscribe to First Team Fixtures (.ics)') }}</a>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'roster'">
  <!-- 一線隊球員橫幅（沿用 .stats-band 的深色帶樣式；數據區塊已移除）
       資料來源：GET /api/backend/{club}/players?team=<一線隊>（見上方 script setup「一線隊球員橫幅」段）。
       沒有球員資料時整個區塊不顯示；未取得肖像同意的球員 photoUrl 為 null，以隊徽卡呈現。 -->
  <section v-if="rosterStrip.length > 0" class="band grain grain--2 stats-band" aria-labelledby="roster-strip-title">
    <div class="band-inner container">
      <div class="roster-strip">
        <div class="roster-strip__head">
          <h3 id="roster-strip-title">{{ tx('一線隊球員 FIRST TEAM', 'FIRST TEAM') }}</h3>
          <a :href="lp('/zh/club/first-team/')">{{ tx('查看完整名單 →', 'View the full squad →') }}</a>
        </div>
        <div class="roster-row">
          <div v-for="p in rosterStrip" :key="p.id" class="roster-card">
            <div class="roster-card__photo">
              <span v-if="p.shirtNo != null" class="roster-card__num">{{ p.shirtNo }}</span>
              <img v-if="p.photoUrl" :src="p.photoUrl" :alt="`${p.shirtNo != null ? tx(`${p.shirtNo} 號球員 `, `Player ${p.shirtNo}: `) : ''}${p.name}`" loading="lazy" width="620" height="620">
              <img v-else class="roster-card__crest" :src="assets.headerMark.src" alt="" loading="lazy" width="64" height="67">
            </div>
            <p class="roster-card__name">{{ p.shirtNo != null ? `#${p.shirtNo} ` : '' }}{{ p.name }}{{ p.position ? `${tx('　', ' · ')}${p.position}` : '' }}</p>
          </div>
        </div>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'core_values'">
  <!-- SPEC 1.2 — Five core values
       五大核心價值是磐石自訂的品牌框架，舊站沒有陳述對等的架構，依內容紀律
       不得自行創作藍鯨版的「五大核心價值」，本區塊不顯示。 -->
  <section v-if="showCoreValues" class="band values-band" id="values" aria-labelledby="values-title">
    <span class="ghost-num ghost-num--light" aria-hidden="true">05</span>
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker">OUR MISSION</p>
          <!-- 藍鯨的標題與說明文字尚待客戶確認（藍鯨規劃書 §10 第 13 點），不沿用磐石的品牌主張文案：只顯示中性標題與 API 的五項名稱。 -->
          <h2 v-if="!isTcrfc" class="section-title" id="values-title">{{ tx('核心價值', 'Core Values') }}</h2>
          <h2 v-else-if="isEn" class="section-title" id="values-title">Developing players who pursue excellence<br>through a professional model</h2>
          <h2 v-else class="section-title" id="values-title">透過專業模式<br>培育選手追求卓越</h2>
        </div>
        <p v-if="isTcrfc" class="section-lede">{{ tx('從台中出發：培育本土選手邁向職業、成為在地榮耀的來源，並以足球讓世界看見台灣。', 'Starting from Taichung: developing local players for the professional game, becoming a source of local pride, and letting the world see Taiwan football through the sport.') }}</p>
      </div>
      <div class="values-grid">
        <div v-for="v in coreValues" :key="v.code" class="value-card">
          <p class="value-card__num">{{ v.num }}</p>
          <p class="value-card__en">{{ v.nameEn }}</p>
          <p v-if="!isEn" class="value-card__zh">{{ v.nameZh }}</p>
          <p v-if="isTcrfc && (isEn ? CORE_VALUE_DESCRIPTIONS_EN[v.code] : v.desc)" class="value-card__desc">{{ isEn ? CORE_VALUE_DESCRIPTIONS_EN[v.code] : v.desc }}</p>
        </div>
      </div>
      <p v-if="isTcrfc && coreValuesMore" class="sponsor-more"><a :href="lp(coreValuesMore)">{{ tx('了解足球理念與五大核心價值 →', 'Learn about our football philosophy and five core values →') }}</a></p>
    </div>
  </section>
  </template>
  <template v-if="blk === 'ecosystem_nav'">
  <!-- SPEC 3.1 — Four pillars -->
  <section v-if="isSectionEnabled('ecosystem_nav')" class="band grain pillars-band" id="club" aria-labelledby="pillars-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">04</span>
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker kicker--on-dark">WHAT WE DO</p>
          <h2 class="section-title" id="pillars-title">{{ tx('四大支柱', 'Four Pillars') }}</h2>
        </div>
      </div>
      <!-- 四大支柱圖卡的四張照片全是磐石素材（news-mcu／trencin-04／trencin-05／news-w20，含磐石球員、
           未成年青訓球員與贊助字樣），不是「通用足球場景照」（E-83 更正原註解）。藍鯨站不輸出照片，
           圖卡退為深色底＋scrim（.pillar-card 本身的底色），待藍鯨素材到位再換。 -->
      <div class="pillars-grid">
        <a v-for="(pillar, i) in pillars" :id="pillar.id" :key="pillar.enLabel" class="pillar-card clip-card clip-card--on-dark" :href="lp(pillar.href)">
          <img v-if="isTcrfc" :src="[siteImg('/assets/img/news-mcu.jpg'), siteImg('/assets/img/trencin-04.jpg'), siteImg('/assets/img/trencin-05.jpg'), siteImg('/assets/img/news-w20.jpg')][i]" :alt="pillar.imgAlt" loading="lazy" :width="pillar.imgWidth" :height="pillar.imgHeight">
          <div class="pillar-card__scrim" aria-hidden="true"></div>
          <div class="pillar-card__body">
            <p class="pillar-card__en">{{ pillar.enLabel }}</p>
            <p class="pillar-card__zh">{{ pillar.zhLabel }}</p>
            <span class="pillar-card__link">{{ pillar.linkLabelZh }} <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6" /></svg></span>
          </div>
        </a>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'latest_news'">
  <!-- SPEC 3.1（最新消息）／3.7 — News mosaic
       資料來源：新聞模組（GET /api/backend/{club}/news，S1-14 起接上真實資料，精選優先、
       不足再用最新日期補滿，見上方 script setup「最新消息」段）。藍鯨新聞 07 單元自有
       全文 0 篇（gap-analysis.md §4 #3），homeNews 會自然是空陣列，本區塊不顯示——
       不再用 isTcrfc 硬判斷，改成看真實資料有沒有內容。
       版位固定 5 格（feature／sml×2／wide×2），資料不足 5 篇時依序省略後面的格子。 -->
  <section v-if="isSectionEnabled('latest_news') && homeNews.length > 0" class="band news-band" id="news" aria-labelledby="news-title">
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker">LATEST STORIES</p>
          <h2 class="section-title" id="news-title">{{ tx('最新消息', 'Latest News') }}</h2>
        </div>
        <a class="btn btn--dark btn--sm" :href="lp('/zh/news/')">{{ tx('所有新聞', 'All News') }}</a>
      </div>

      <div class="news-mosaic">
        <a
          v-for="(article, i) in homeNews"
          :key="article.slug"
          :class="['news-card', 'clip-card', `news-card--${NEWS_VARIANTS[i]}`]"
          :href="lp(`/zh/news/${article.slug}/`)"
        >
          <div v-if="NEWS_VARIANTS[i] === 'wide'" class="news-card__inner" style="display:flex;width:100%;">
            <div :class="['news-card__media', { 'news-card__media--noimg': !newsCoverImg(article, clubKey) }]">
              <span class="news-card__tag">{{ article.categoryName }}</span>
              <img v-if="newsCoverImg(article, clubKey)" :src="newsCoverImg(article, clubKey)!.src" :alt="newsCoverImg(article, clubKey)!.alt" loading="lazy" :width="newsCoverImg(article, clubKey)!.width ?? undefined" :height="newsCoverImg(article, clubKey)!.height ?? undefined">
              <img v-else class="news-card__media-mark" :src="newsFallbackMarkSrc(clubKey)" alt="" loading="lazy" width="64" height="67">
            </div>
            <div class="news-card__body">
              <p class="news-card__meta">{{ newsSlashDate(article.publishedAt) }}</p>
              <p class="news-card__title">{{ article.title }}</p>
            </div>
          </div>
          <template v-else>
            <div :class="['news-card__media', { 'news-card__media--noimg': !newsCoverImg(article, clubKey) }]">
              <span class="news-card__tag">{{ article.categoryName }}</span>
              <img v-if="newsCoverImg(article, clubKey)" :src="newsCoverImg(article, clubKey)!.src" :alt="newsCoverImg(article, clubKey)!.alt" loading="lazy" :width="newsCoverImg(article, clubKey)!.width ?? undefined" :height="newsCoverImg(article, clubKey)!.height ?? undefined">
              <img v-else class="news-card__media-mark" :src="newsFallbackMarkSrc(clubKey)" alt="" loading="lazy" width="64" height="67">
            </div>
            <div class="news-card__body">
              <p class="news-card__meta">{{ newsSlashDate(article.publishedAt) }}</p>
              <p class="news-card__title">{{ article.title }}</p>
            </div>
          </template>
        </a>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'shop_entry'">
  <!-- SPEC 3.1「官方商店入口」＋ 3.8 — Official store band（S3-5 接商店資料）
       有上架商品：顯示精選商品卡（名稱／圖片／價格）與站內商店入口（站內頁面，不另開新分頁）。
       磐石沒有任何上架商品：維持既有的靜態入口（真實球衣照片）；藍鯨沒有商品：整個區塊不顯示（showShopEntry）。 -->
  <section v-if="showShopEntry" class="band grain grain--2 store-band" aria-labelledby="store-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">08</span>
    <div class="band-inner container">
      <div class="store-band__grid">
        <div>
          <p class="kicker kicker--on-dark">TEAM UP IN STYLE</p>
          <h2 class="section-title" id="store-title">{{ tx('官方商店', 'Official Store') }}</h2>
          <p>{{ isTcrfc ? tx('主客場球衣、周邊配件與訓練服飾，穿上台中磐石桃紅，與球隊一起在場邊、場上同進退。', 'Home and away shirts, accessories and training wear. Put on Taichung Rock FC pink and stand with the team, on the touchline and on the pitch.') : tx(`${assets.nameZh}官方商店，選購球隊商品，與球隊一起同進退。`, `The official ${BW_NAME_EN} store. Pick up team merchandise and stand with the team.`) }}</p>
          <a class="btn btn--primary" :href="lp('/zh/shop/')">{{ tx('前往官方商店 SHOP', 'Visit the Official Store') }}</a>
          <p class="store-band__fine">{{ tx('結帳以 LINE Pay 付款並開立電子發票。', 'Pay with LINE Pay at checkout; an electronic invoice is issued.') }}</p>
        </div>
        <ul v-if="shopEntryProducts.length" class="sh-entry-grid">
          <li v-for="p in shopEntryProducts" :key="p.slug" class="sh-entry-card">
            <a :href="lp(`/zh/shop/${p.slug}/`)">
              <img v-if="p.imageThumbUrl || p.imageUrl" :src="(p.imageThumbUrl || p.imageUrl) ?? ''" :alt="p.name || ''" loading="lazy" width="320" height="320">
              <span class="sh-entry-card__name">{{ p.name || p.slug }}</span>
              <span class="sh-entry-card__price">{{ formatPriceRange(p.priceMin, p.priceMax) ?? tx('暫無販售', 'Currently unavailable') }}</span>
            </a>
          </li>
        </ul>
        <div v-else class="store-visual clip-card clip-card--on-dark">
          <img :src="siteImg('/assets/img/player-09-liu.jpg')" :alt="tx('球員身著台中磐石主場球衣', 'A player wearing the Taichung Rock FC home shirt')" loading="lazy" width="620" height="620">
          <span class="store-visual__badge">{{ tx('台中磐石主場球衣', 'Taichung Rock FC home shirt') }}</span>
        </div>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'partner_logos'">
  <!-- SPEC 3.9 — Sponsor wall -->
  <section v-if="isSectionEnabled('partner_logos')" class="band sponsor-band" id="partners" aria-labelledby="partners-title">
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker">WITH THANKS TO</p>
          <h2 class="section-title" id="partners-title">{{ tx('合作夥伴', 'Our Partners') }}</h2>
        </div>
        <p class="section-lede">{{ tx(`感謝以下夥伴支持${assets.nameZh}的每一步成長。`, `Thank you to the partners below for supporting every step of ${clubName}'s growth.`) }}</p>
      </div>

      <div v-if="homePartnerTiles.length" class="sponsor-grid">
        <PartnerLogoTile
          v-for="p in homePartnerTiles"
          :key="p.id"
          :name="p.name"
          :logo-url="pickLogoUrl(p)"
          :href="lp('/zh/partners/our-partners/')"
        />
      </div>
      <p v-if="homePartnerTiles.length" class="sponsor-more"><a :href="lp('/zh/partners/our-partners/')">{{ tx('查看全部合作夥伴 →', 'View all partners →') }}</a></p>
      <div v-else class="sponsor-grid" aria-hidden="true">
        <div v-for="n in 10" :key="n" class="sponsor-tile sponsor-tile--empty"></div>
      </div>
    </div>
  </section>
  </template>
  <template v-if="blk === 'bottom_cta'">
  <!-- SPEC 3.1 — Bottom CTA trio (10.1 / 10.2 / 10.5) -->
  <section v-if="isSectionEnabled('bottom_cta')" class="band grain cta-band" id="charity" aria-labelledby="cta-title">
    <div class="band-inner container">
      <h2 class="visually-hidden" id="cta-title">{{ tx(`加入${assets.shortNameZh}`, `Join ${isTcrfc ? 'TCRFC' : BW_NAME_EN}`) }}</h2>
      <div class="cta-grid">
        <div v-for="card in ctaTrio" :key="card.num" class="cta-card">
          <p class="cta-card__num">{{ card.num }}</p>
          <p class="cta-card__title">{{ card.titleZh }}</p>
          <p class="cta-card__desc">{{ card.descZh }}</p>
          <a class="btn btn--primary" :href="lp(card.href)">{{ card.ctaLabelZh }}</a>
        </div>
      </div>
    </div>
  </section>
  </template>
  </template>
</template>

<style>
.sponsor-more{ margin-top:1.25rem; font-size:.88rem; font-weight:700; }
.sponsor-more a{ color:var(--brand-aa); text-decoration:underline; }
/* 藍鯨首頁 hero 無授權照片可用時的純色回退（見 script setup 開頭說明）——
   只用既有 --brand 系列 token，不引入新色碼，遵守「顏色只能是 CSS custom
   properties」（docs/13-blue-whale-site.md §6 紀律 1）。 */
.hero__media--pending {
  background: linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%);
}
/* 一線隊球員橫幅：未取得肖像同意的球員以隊徽置中顯示（不拉伸成滿版照片） */
.roster-card__photo .roster-card__crest {
  position: absolute; inset: 0; margin: auto; width: 40%; height: auto; object-fit: contain; opacity: .55;
}
</style>
