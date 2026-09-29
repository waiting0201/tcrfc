<script setup lang="ts">
// app/pages/zh/index.vue — 由 site/src/pages/zh/index.html 轉來（首頁，S0-9 切片驗證頁之一）
//
// 🔴 main 內容（下方樣板區塊）與 mockup 逐段一致，DOM 結構、class、文字內容不動；
// {{ROOT}} 在 mockup 是相對路徑 token，Nuxt 掛載於站根，一律省略（等同空字串）。
// ⛔ 原頁本身沒有頁內 style／script 標籤（唯一的行為邏輯來自共用的 site/src/assets/js/site.js），
// 這裡把該檔「賽事切換 tabs」與「主視覺輪播」兩段頁面專屬邏輯移入 script setup
// （sticky header／行動選單／mega menu 屬於版型層級，已移到 app/components/SiteHeader.vue）。
definePageMeta({ nav: 'home', unit: '01', bodyClass: 'page-home' })

// 文案依俱樂部切換（docs/13-blue-whale-site.md §6 紀律 11）：SEO、Hero 標語與
// 底下幾個「真人真事」區塊（賽事戰績、球員名單、新聞、商店實拍照）分屬 shared/
// utils/club-copy.ts 的資料層，或屬於動態內容（球員／新聞／賽程，見該檔檔頭
// 說明 (c)）——藍鯨目前這些區塊 0 素材，一律不顯示，不沿用磐石的真人真事資料
// 頂替（docs/13 踩雷點 8 同一道理：缺素材不放假的，直接不顯示這個區塊）。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const assets = computed(() => getClubAssets(clubKey.value))
const heroCopy = computed(() => HOME_HERO[clubKey.value])
const pillars = computed(() => HOME_PILLARS[clubKey.value])
const ctaTrio = computed(() => HOME_CTA_TRIO[clubKey.value])

// S1-13：club-copy.ts 裡的 ctaPrimaryHref／pillars[].href／ctaTrio[].href 三組欄位存的是
// 「裸的 /zh/... 路徑」（該檔案的資料格式一律如此，不隨語系變化），樣板消費這些欄位時要
// 套一層 lp() 換算成目前路由語系——這裡是本輪 curl 實測時抓到的真實回歸（英文首頁的三個
// CTA 連結原本會把讀者導回 /zh/...），修法見下方樣板三處呼叫點。
const { locale, lp } = useLocale()

useSeoMeta({
  title: computed(() => HOME_SEO[clubKey.value].title),
  description: computed(() => HOME_SEO[clubKey.value].description),
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
const { isSectionEnabled } = useHomeSections(config.public.club)

// Banner（B3 首頁輪播）：本輪已接上真實 API，但 apps/api 的 `HomeRepository.ListBannersAsync`
// 目前只回傳 `imageKey`（Blob 物件鍵），沒有像 StaffDto／PlayerDto 一樣經由
// `IImagePublicUrlResolver` 解成可直接用的完整網址（見 apps/web/README.md「S1-14」節、
// docs/18-work-errors.md 對應記錄）——前端沒有 Blob 容器網址可以自己兜，用猜的網址規則
// 會顯示壞圖，比不顯示更糟。兩俱樂部 `banners` 資料表目前也都是 0 筆（db/seed 沒有種子
// 資料），所以這裡先只用來源 API 讀「第一則輪播的主要 CTA」文字／連結（純文字欄位，
// 不涉及圖片網址解析），有值才覆蓋 club-copy.ts 的預設 CTA；輪播圖片本身維持現有素材
// 直到後端補上 `ImageUrl` 欄位。
const { data: bannersData } = await useFetch<Array<{
  cta1Label: string | null
  cta1Url: string | null
}>>(`/api/backend/${config.public.club}/banners`, { query: { lang: locale.value } })
const primaryCta = computed(() => {
  const first = bannersData.value?.[0]
  if (first?.cta1Label && first?.cta1Url) return { label: first.cta1Label, href: first.cta1Url }
  return null
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

const d1Sorted = computed(() =>
  allMatches.value.filter((m) => m.teamCode === 'D1').slice().sort((a, b) => a.matchOn.localeCompare(b.matchOn)),
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
  const haLabel = m.homeAway === 'AWAY' ? '客場' : m.homeAway === 'HOME' ? '主場' : ''
  return [m.competitionName ?? '', `${Number(mo)}/${Number(d)}（${wd}）`, haLabel].filter(Boolean).join(' · ')
}

const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000
const cutoffStr = computed(() => new Date(Date.now() + THIRTY_DAYS_MS).toISOString().slice(0, 10))
/** 近期賽事（未來 30 天）：非一線隊（U15／U14／U12）——目前種子資料只有一線隊，
 * 這裡先接上真實邏輯，真的有梯隊賽程時會自然出現，不用再改程式碼。 */
const otherTeamUpcoming = computed(() =>
  allMatches.value
    .filter((m) => m.teamCode !== 'D1' && m.status === 'scheduled' && m.matchOn >= todayStr && m.matchOn <= cutoffStr.value)
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
const slideEls = ref<HTMLElement[]>([])

const COLS = 6
const ROWS = 4
const TILE_DURATION = 520 // ms — 對應 tcrfc.css 裡 .hero__tile 的 transition
const STAGGER_SPAN = 420 // ms — 網格內 transition-delay 的分布範圍
const AUTOPLAY_MS = 4400

const total = 3
let current = 0
let isAnimating = false
let timer: ReturnType<typeof setInterval> | null = null
let reduceMQ: MediaQueryList | null = null

function reduced() {
  return reduceMQ?.matches ?? false
}

function updateControls() {
  if (statusEl.value) {
    statusEl.value.textContent = `目前顯示第 ${current + 1} 張，共 ${total} 張`
  }
}

function swapInstant(index: number) {
  slideEls.value[current]?.classList.remove('is-active')
  slideEls.value[current]?.setAttribute('aria-hidden', 'true')
  slideEls.value[index]?.classList.add('is-active')
  slideEls.value[index]?.removeAttribute('aria-hidden')
  current = index
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
  const next = ((index % total) + total) % total
  if (next === current || isAnimating) return
  if (reduced()) swapInstant(next)
  else animateSwap(next)
}

function startAutoplay() {
  if (!isTcrfc.value || reduced() || total < 2) return
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
function onHeroFocusout(e: FocusEvent) {
  if (!heroSectionEl.value?.contains(e.relatedTarget as Node)) resumeAutoplay()
}

onMounted(() => {
  if (!isTcrfc.value) return // 藍鯨無 hero 輪播素材（首頁 hero 圖未下載、無授權狀態），本頁不掛載輪播行為
  slideEls.value = Array.from(sliderEl.value?.querySelectorAll<HTMLElement>('.hero__slide') ?? [])
  reduceMQ = window.matchMedia('(prefers-reduced-motion: reduce)')

  heroSectionEl.value?.addEventListener('mouseenter', pauseForInteraction)
  heroSectionEl.value?.addEventListener('mouseleave', resumeAutoplay)
  heroSectionEl.value?.addEventListener('focusin', pauseForInteraction)
  heroSectionEl.value?.addEventListener('focusout', onHeroFocusout)
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
})
</script>

<template>
  <section v-if="isSectionEnabled('hero')" ref="heroSectionEl" class="hero" id="top" aria-label="首頁主視覺">
    <div v-if="isTcrfc" ref="sliderEl" class="hero__media" id="hero-slider" role="group" aria-roledescription="carousel" aria-label="首頁主視覺輪播，共 3 張">
      <ul class="hero__slides">
        <li class="hero__slide is-active" role="group" aria-roledescription="slide" aria-label="第 1 張，共 3 張">
          <img src="/assets/img/hero-01.jpg" alt="台中磐石球員於夜間賽事中振臂吶喊慶祝，場邊看板可見桃紅色 TCRFC 字樣" width="2400" height="1600" loading="eager" fetchpriority="high" style="object-position:58% 35%">
        </li>
        <li class="hero__slide" aria-hidden="true" role="group" aria-roledescription="slide" aria-label="第 2 張，共 3 張">
          <img src="/assets/img/hero-02.jpg" alt="台中磐石5號球員於夜間賽事中揮腳觸球，身後可見場邊看台的球員與觀眾" width="2400" height="1600" loading="lazy" style="object-position:56% 30%">
        </li>
        <li class="hero__slide" aria-hidden="true" role="group" aria-roledescription="slide" aria-label="第 3 張，共 3 張">
          <img src="/assets/img/hero-03.jpg" alt="台中磐石一線隊球員賽前肩併肩圍成一圈，互相激勵士氣" width="2400" height="1600" loading="lazy" style="object-position:55% 42%">
        </li>
      </ul>
      <p ref="statusEl" class="visually-hidden" id="hero-slide-status" aria-live="off" aria-atomic="true">目前顯示第 1 張，共 3 張</p>
    </div>
    <!-- 藍鯨首頁 hero 圖未下載、無授權狀態（content/blue-whale/gap-analysis.md §2），
         不得沿用磐石的照片頂替，改用純色塊（docs/13 踩雷點 8 同一道理：缺素材不放假圖）。 -->
    <div v-else class="hero__media hero__media--pending" aria-hidden="true"></div>
    <div class="hero__scrim" aria-hidden="true"></div>
    <span class="ghost-num" aria-hidden="true">01</span>
    <div class="hero__inner">
      <div class="container">
        <div class="hero__grid">
          <div class="hero__copy">
            <p v-if="heroCopy.kickerEn" class="kicker kicker--on-dark">{{ heroCopy.kickerEn }}</p>
            <h1 class="hero__headline" v-html="heroCopy.headlineZh"></h1>
            <p class="hero__sub" v-html="heroCopy.factLineZh"></p>
            <div class="hero__ctas">
              <a class="btn btn--primary" :href="primaryCta ? primaryCta.href : lp(heroCopy.ctaPrimaryHref)">{{ primaryCta ? primaryCta.label : '加入球隊' }}</a>
              <a class="btn btn--light" :href="lp(heroCopy.ctaSecondaryHref)">{{ heroCopy.ctaSecondaryLabelZh }}</a>
            </div>
            <div v-if="isTcrfc" class="hero__slider-nav">
              <button type="button" class="hero__arrow hero__arrow--prev" data-hero-prev aria-controls="hero-slider" aria-label="上一張主視覺圖片" @click="goTo(current - 1)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 5l-7 7 7 7" /></svg>
              </button>
              <div class="hero__dots" role="tablist" aria-label="選擇主視覺圖片">
                <button type="button" class="hero__dot is-active" role="tab" aria-selected="true" aria-controls="hero-slider" aria-label="第 1 張，共 3 張" data-hero-goto="0" @click="goTo(0)"></button>
                <button type="button" class="hero__dot" role="tab" aria-selected="false" aria-controls="hero-slider" aria-label="第 2 張，共 3 張" data-hero-goto="1" @click="goTo(1)"></button>
                <button type="button" class="hero__dot" role="tab" aria-selected="false" aria-controls="hero-slider" aria-label="第 3 張，共 3 張" data-hero-goto="2" @click="goTo(2)"></button>
              </div>
              <button type="button" class="hero__arrow hero__arrow--next" data-hero-next aria-controls="hero-slider" aria-label="下一張主視覺圖片" @click="goTo(current + 1)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 5l7 7-7 7" /></svg>
              </button>
            </div>
          </div>
          <!-- 藍鯨新聞 07 單元自有全文 0 篇（gap-analysis.md §4 #3），不沿用磐石新聞頂替，本區塊不顯示。 -->
          <div v-if="isTcrfc" class="hero__news">
            <a class="hero-card clip-card clip-card--on-dark" :href="lp('/zh/news/')">
              <div class="hero-card__media">
                <img src="/assets/img/news-trencin.jpg" alt="台中磐石青訓球員與斯洛伐克 AS Trenčín 球員合影交流" loading="lazy" width="1280" height="853">
              </div>
              <div class="hero-card__body">
                <span class="hero-card__tag">消息 News</span>
                <span class="hero-card__title">台中磐石與 AS Trenčín 深化青訓合作</span>
              </div>
            </a>
            <a class="hero-card clip-card clip-card--on-dark" :href="lp('/zh/news/')">
              <div class="hero-card__media">
                <img src="/assets/img/news-mcu.jpg" alt="台中磐石 7 號球員於夜間比賽中盤球突破銘傳大學白色球衣防線" loading="lazy" width="1280" height="855">
              </div>
              <div class="hero-card__body">
                <span class="hero-card__tag">比賽 Matches</span>
                <span class="hero-card__title">企甲聯賽：台中磐石 3-0 銘傳大學</span>
              </div>
            </a>
          </div>
        </div>
      </div>
    </div>
  </section>

  <!-- SPEC 3.1（最新賽事區／近期賽事）／3.13 — Match band
       資料來源：賽事管理模組（GET /api/backend/{club}/schedule，S1-14 起接上真實資料，
       見上方 script setup「賽事資料」段與 apps/web/README.md「S1-14」節）。
       藍鯨未來 12 個月賽程完全沒有（docs/13-blue-whale-site.md §5 擋開發第 4 項），
       本區塊不沿用磐石賽事資料頂替，直接不顯示（維持既有 isTcrfc 閘門不變——是否要
       改為「藍鯨已有真實歷史賽果就顯示戰績卡」留給主 session 裁決，見任務回報）。 -->
  <section v-if="isTcrfc && (isSectionEnabled('upcoming_match') || isSectionEnabled('recent_fixtures'))" class="band grain match-band" id="schedule" aria-labelledby="schedule-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">21</span>
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker kicker--on-dark">MATCHDAY</p>
          <h2 class="section-title" id="schedule-title">賽事行事曆</h2>
        </div>
      </div>

      <div class="team-chips" role="group" aria-label="選擇隊伍等級">
        <button class="team-chip" type="button" data-team="D1" :aria-pressed="teamPanel === 'D1'" @click="teamPanel = 'D1'">一線隊 First Team</button>
        <button class="team-chip" type="button" data-team="U15" :aria-pressed="teamPanel === 'other'" @click="teamPanel = 'other'">U15</button>
        <button class="team-chip" type="button" data-team="U14" :aria-pressed="false" @click="teamPanel = 'other'">U14</button>
        <button class="team-chip" type="button" data-team="U12" :aria-pressed="false" @click="teamPanel = 'other'">U12</button>
      </div>

      <div v-if="isSectionEnabled('upcoming_match')" class="match-grid" id="match-grid-d1" data-team-panel="D1" :hidden="teamPanel !== 'D1'">
        <article v-if="latestResult" class="match-card">
          <div class="match-card__label"><span>最新戰績 LATEST RESULT</span></div>
          <p class="match-card__meta">{{ resultMetaLine(latestResult) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ assets.nameZh }}</span>
            <span class="match-card__score">{{ clubScore(latestResult) }}<span class="sep">:</span>{{ opponentScore(latestResult) }}</span>
            <span class="match-card__team match-card__team--away">{{ latestResult.opponent }}</span>
          </div>
        </article>
        <article v-else class="match-card match-card--placeholder">
          <p>尚無已完賽數據，敬請鎖定近期賽事。</p>
        </article>

        <article v-if="previousResult" class="match-card">
          <div class="match-card__label"><span>上一場 PREVIOUS</span></div>
          <p class="match-card__meta">{{ resultMetaLine(previousResult) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ assets.nameZh }}</span>
            <span class="match-card__score">{{ clubScore(previousResult) }}<span class="sep">:</span>{{ opponentScore(previousResult) }}</span>
            <span class="match-card__team match-card__team--away">{{ previousResult.opponent }}</span>
          </div>
        </article>

        <article v-if="nextFixture" class="match-card match-card--next">
          <div class="match-card__label"><span>下一場 NEXT FIXTURE</span></div>
          <p class="match-card__meta">{{ fixtureMetaLine(nextFixture) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ assets.nameZh }}</span>
            <span class="match-card__vs">VS</span>
            <span class="match-card__team match-card__team--away">{{ nextFixture.opponent }}</span>
          </div>
          <p class="match-card__scorers">{{ nextFixture.venue }}　賽程以官方公告為準</p>
        </article>
        <article v-else class="match-card match-card--placeholder">
          <p>下一場賽程尚未公告，敬請關注後續公告。</p>
        </article>
      </div>

      <div v-if="isSectionEnabled('recent_fixtures')" class="match-grid" id="match-grid-other" data-team-panel="other" :hidden="teamPanel !== 'other'">
        <article v-for="m in otherTeamUpcoming" :key="`${m.teamCode}-${m.matchOn}-${m.opponent}`" class="match-card">
          <div class="match-card__label"><span>{{ m.teamCode }}</span></div>
          <p class="match-card__meta">{{ fixtureMetaLine(m) }}</p>
          <div class="match-card__fixture">
            <span class="match-card__team">{{ assets.nameZh }}</span>
            <span class="match-card__vs">VS</span>
            <span class="match-card__team match-card__team--away">{{ m.opponent }}</span>
          </div>
        </article>
        <div v-if="otherTeamUpcoming.length === 0" class="match-card match-card--placeholder">
          <p>青訓梯隊賽程尚未公開發布，敬請關注後續公告。</p>
        </div>
      </div>

      <div class="match-band__actions">
        <a class="btn btn--light" :href="lp('/zh/schedule/')">查看完整行事曆</a>
        <a class="btn btn--light" :href="lp('/zh/schedule/')">訂閱一線隊賽程 (.ics)</a>
      </div>
    </div>
  </section>

  <!-- 一線隊球員橫幅（沿用 .stats-band 的深色帶樣式；數據區塊已移除）
       球員名單屬動態內容（不進 club-copy.ts），藍鯨目前無已核可肖像可用的一線隊球員照片，
       本區塊不顯示，不沿用磐石球員資料頂替。 -->
  <section v-if="isTcrfc" class="band grain grain--2 stats-band" aria-labelledby="roster-strip-title">
    <div class="band-inner container">
      <div class="roster-strip">
        <div class="roster-strip__head">
          <h3 id="roster-strip-title">一線隊球員 FIRST TEAM</h3>
          <a :href="lp('/zh/club/first-team/')">查看完整名單 →</a>
        </div>
        <div class="roster-row">
          <div class="roster-card">
            <div class="roster-card__photo"><span class="roster-card__num">9</span><img src="/assets/img/player-09-liu.jpg" alt="9 號球員 劉選手" loading="lazy" width="620" height="620"></div>
            <p class="roster-card__name">#9 劉建緯　FW</p>
          </div>
          <div class="roster-card">
            <div class="roster-card__photo"><span class="roster-card__num">11</span><img src="/assets/img/player-11-yang.jpg" alt="11 號球員 楊朝景，現效力香港九龍城" loading="lazy" width="620" height="620"></div>
            <p class="roster-card__name">#11 楊朝景　旅外</p>
          </div>
          <div class="roster-card">
            <div class="roster-card__photo"><span class="roster-card__num">27</span><img src="/assets/img/player-27-shi.jpg" alt="27 號球員 施靖堂" loading="lazy" width="620" height="620"></div>
            <p class="roster-card__name">#27 施靖堂　FW</p>
          </div>
          <div class="roster-card">
            <div class="roster-card__photo"><span class="roster-card__num">44</span><img src="/assets/img/player-44-yamauchi.jpg" alt="44 號球員 山內大空" loading="lazy" width="465" height="620"></div>
            <p class="roster-card__name">#44 山內大空　FW</p>
          </div>
          <div class="roster-card">
            <div class="roster-card__photo"><span class="roster-card__num">77</span><img src="/assets/img/player-77-lin.jpg" alt="77 號球員 林偉傑" loading="lazy" width="465" height="620"></div>
            <p class="roster-card__name">#77 林偉傑　FW</p>
          </div>
        </div>
      </div>
    </div>
  </section>

  <!-- SPEC 1.2 — Five core values
       五大核心價值是磐石自訂的品牌框架，舊站沒有陳述對等的架構，依內容紀律
       不得自行創作藍鯨版的「五大核心價值」，本區塊不顯示。 -->
  <section v-if="isTcrfc && isSectionEnabled('core_values')" class="band values-band" id="values" aria-labelledby="values-title">
    <span class="ghost-num ghost-num--light" aria-hidden="true">05</span>
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker">OUR MISSION</p>
          <h2 class="section-title" id="values-title">透過專業模式<br>培育選手追求卓越</h2>
        </div>
        <p class="section-lede">從台中出發：培育本土選手邁向職業、成為在地榮耀的來源，並以足球讓世界看見台灣。</p>
      </div>
      <div class="values-grid">
        <div class="value-card">
          <p class="value-card__num">01</p>
          <p class="value-card__en">Players First</p>
          <p class="value-card__zh">以球員為本</p>
          <p class="value-card__desc">所有訓練規劃與資源配置，皆以球員的長期發展與福祉為核心考量。</p>
        </div>
        <div class="value-card">
          <p class="value-card__num">02</p>
          <p class="value-card__en">Excellence</p>
          <p class="value-card__zh">追求卓越</p>
          <p class="value-card__desc">建立專業化訓練與教練體系，協助選手邁向職業舞台所需的實力與態度。</p>
        </div>
        <div class="value-card">
          <p class="value-card__num">03</p>
          <p class="value-card__en">Global Pathways</p>
          <p class="value-card__zh">國際發展</p>
          <p class="value-card__desc">從台中出發、放眼世界，透過海外交流建立選手與職業舞台接軌的路徑。</p>
        </div>
        <div class="value-card">
          <p class="value-card__num">04</p>
          <p class="value-card__en">Community</p>
          <p class="value-card__zh">社區共好</p>
          <p class="value-card__desc">紮根台中在地，成為社區認同與榮耀的來源，與球迷共同成長。</p>
        </div>
        <div class="value-card">
          <p class="value-card__num">05</p>
          <p class="value-card__en">Integrity</p>
          <p class="value-card__zh">誠信專業</p>
          <p class="value-card__desc">以誠信治理與專業制度，支撐俱樂部長期穩健發展。</p>
        </div>
      </div>
    </div>
  </section>

  <!-- SPEC 3.1 — Four pillars -->
  <section v-if="isSectionEnabled('ecosystem_nav')" class="band grain pillars-band" id="club" aria-labelledby="pillars-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">04</span>
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker kicker--on-dark">WHAT WE DO</p>
          <h2 class="section-title" id="pillars-title">四大支柱</h2>
        </div>
      </div>
      <!-- 四大支柱／三大體系圖卡沿用既有 mockup 圖片（人物照為磐石既有素材，藍鯨
           無對應照片，兩站共用同一組通用足球場景照，不涉及任何俱樂部辨識內容）。 -->
      <div class="pillars-grid">
        <a v-for="(pillar, i) in pillars" :id="pillar.id" :key="pillar.enLabel" class="pillar-card clip-card clip-card--on-dark" :href="lp(pillar.href)">
          <img :src="['/assets/img/news-mcu.jpg', '/assets/img/trencin-04.jpg', '/assets/img/trencin-05.jpg', '/assets/img/news-w20.jpg'][i]" :alt="pillar.imgAlt" loading="lazy" :width="pillar.imgWidth" :height="pillar.imgHeight">
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
          <h2 class="section-title" id="news-title">最新消息</h2>
        </div>
        <a class="btn btn--dark btn--sm" :href="lp('/zh/news/')">所有新聞</a>
      </div>

      <div class="news-mosaic">
        <a
          v-for="(article, i) in homeNews"
          :key="article.slug"
          :class="['news-card', 'clip-card', `news-card--${NEWS_VARIANTS[i]}`]"
          :href="lp(`/zh/news/${article.slug}/`)"
        >
          <div v-if="NEWS_VARIANTS[i] === 'wide'" class="news-card__inner" style="display:flex;width:100%;">
            <div :class="['news-card__media', { 'news-card__media--noimg': !hasNewsCover(article.slug) }]">
              <span class="news-card__tag">{{ article.categoryName }}</span>
              <img v-if="hasNewsCover(article.slug)" :src="newsCoverSrc(article.slug)" :alt="article.title ?? ''" loading="lazy" width="1600" height="1067">
              <img v-else class="news-card__media-mark" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" loading="lazy" width="64" height="67">
            </div>
            <div class="news-card__body">
              <p class="news-card__meta">{{ newsSlashDate(article.publishedAt) }}</p>
              <p class="news-card__title">{{ article.title }}</p>
            </div>
          </div>
          <template v-else>
            <div :class="['news-card__media', { 'news-card__media--noimg': !hasNewsCover(article.slug) }]">
              <span class="news-card__tag">{{ article.categoryName }}</span>
              <img v-if="hasNewsCover(article.slug)" :src="newsCoverSrc(article.slug)" :alt="article.title ?? ''" loading="lazy" width="1280" height="853">
              <img v-else class="news-card__media-mark" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" loading="lazy" width="64" height="67">
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

  <!-- SPEC 3.8 — Official store band
       藍鯨商店 0 商品、無物流與價格資訊（gap-analysis.md §4 #1），本區塊不顯示。 -->
  <section v-if="isTcrfc && isSectionEnabled('shop_entry')" class="band grain grain--2 store-band" aria-labelledby="store-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">08</span>
    <div class="band-inner container">
      <div class="store-band__grid">
        <div>
          <p class="kicker kicker--on-dark">TEAM UP IN STYLE</p>
          <h2 class="section-title" id="store-title">官方商店</h2>
          <p>主客場球衣、周邊配件與訓練服飾，穿上台中磐石桃紅，與球隊一起在場邊、場上同進退。</p>
          <a class="btn btn--primary" :href="lp('/zh/culture/merchandise/')">官方商品 MERCHANDISE</a>
          <p class="store-band__fine">詳細商品與購買方式請至官方商品頁面查看。</p>
        </div>
        <div class="store-visual clip-card clip-card--on-dark">
          <img src="/assets/img/player-09-liu.jpg" alt="球員身著台中磐石主場球衣" loading="lazy" width="620" height="620">
          <span class="store-visual__badge">台中磐石主場球衣</span>
        </div>
      </div>
    </div>
  </section>

  <!-- SPEC 3.9 — Sponsor wall -->
  <section v-if="isSectionEnabled('partner_logos')" class="band sponsor-band" id="partners" aria-labelledby="partners-title">
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker">WITH THANKS TO</p>
          <h2 class="section-title" id="partners-title">合作夥伴</h2>
        </div>
        <p class="section-lede">感謝以下夥伴支持{{ assets.nameZh }}的每一步成長。</p>
      </div>

      <div class="sponsor-grid" aria-hidden="true">
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
        <div class="sponsor-tile sponsor-tile--empty"></div>
      </div>
    </div>
  </section>

  <!-- SPEC 3.1 — Bottom CTA trio (10.1 / 10.2 / 10.5) -->
  <section v-if="isSectionEnabled('bottom_cta')" class="band grain cta-band" id="charity" aria-labelledby="cta-title">
    <div class="band-inner container">
      <h2 class="visually-hidden" id="cta-title">加入{{ assets.shortNameZh }}</h2>
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

<style>
/* 藍鯨首頁 hero 無授權照片可用時的純色回退（見 script setup 開頭說明）——
   只用既有 --brand 系列 token，不引入新色碼，遵守「顏色只能是 CSS custom
   properties」（docs/13-blue-whale-site.md §6 紀律 1）。 */
.hero__media--pending {
  background: linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%);
}
</style>
