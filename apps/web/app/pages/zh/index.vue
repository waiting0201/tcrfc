<script setup lang="ts">
// app/pages/zh/index.vue — 由 site/src/pages/zh/index.html 轉來（首頁，S0-9 切片驗證頁之一）
//
// 🔴 main 內容（下方樣板區塊）與 mockup 逐段一致，DOM 結構、class、文字內容不動；
// {{ROOT}} 在 mockup 是相對路徑 token，Nuxt 掛載於站根，一律省略（等同空字串）。
// ⛔ 原頁本身沒有頁內 style／script 標籤（唯一的行為邏輯來自共用的 site/src/assets/js/site.js），
// 這裡把該檔「賽事切換 tabs」與「主視覺輪播」兩段頁面專屬邏輯移入 script setup
// （sticky header／行動選單／mega menu 屬於版型層級，已移到 app/components/SiteHeader.vue）。
definePageMeta({ nav: 'home', unit: '01' })

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

useSeoMeta({
  title: computed(() => HOME_SEO[clubKey.value].title),
  description: computed(() => HOME_SEO[clubKey.value].description),
})

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
  <section ref="heroSectionEl" class="hero" id="top" aria-label="首頁主視覺">
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
              <a class="btn btn--primary" :href="heroCopy.ctaPrimaryHref">加入球隊</a>
              <a class="btn btn--light" :href="heroCopy.ctaSecondaryHref">{{ heroCopy.ctaSecondaryLabelZh }}</a>
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
            <a class="hero-card clip-card clip-card--on-dark" href="/zh/news/">
              <div class="hero-card__media">
                <img src="/assets/img/news-trencin.jpg" alt="台中磐石青訓球員與斯洛伐克 AS Trenčín 球員合影交流" loading="lazy" width="1280" height="853">
              </div>
              <div class="hero-card__body">
                <span class="hero-card__tag">消息 News</span>
                <span class="hero-card__title">台中磐石與 AS Trenčín 深化青訓合作</span>
              </div>
            </a>
            <a class="hero-card clip-card clip-card--on-dark" href="/zh/news/">
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

  <!-- SPEC 3.1 / 3.13 — Match band
       藍鯨未來 12 個月賽程完全沒有（docs/13-blue-whale-site.md §5 擋開發第 4 項），
       本區塊不沿用磐石賽事資料頂替，直接不顯示。 -->
  <section v-if="isTcrfc" class="band grain match-band" id="schedule" aria-labelledby="schedule-title">
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

      <div class="match-grid" id="match-grid-d1" data-team-panel="D1" :hidden="teamPanel !== 'D1'">
        <article class="match-card">
          <div class="match-card__label"><span>最新戰績 LATEST RESULT</span></div>
          <p class="match-card__meta">企甲聯賽 · 2026/05/24</p>
          <div class="match-card__fixture">
            <span class="match-card__team">台中磐石</span>
            <span class="match-card__score">3<span class="sep">:</span>0</span>
            <span class="match-card__team match-card__team--away">銘傳大學</span>
          </div>
        </article>

        <article class="match-card">
          <div class="match-card__label"><span>上一場 PREVIOUS</span></div>
          <p class="match-card__meta">企甲聯賽 · 2026/05/17</p>
          <div class="match-card__fixture">
            <span class="match-card__team">台中磐石</span>
            <span class="match-card__score">1<span class="sep">:</span>2</span>
            <span class="match-card__team match-card__team--away">陽信北競</span>
          </div>
          <p class="match-card__scorers">2026/05/10 主場 2:4 不敵南市台鋼</p>
        </article>

        <article class="match-card match-card--next">
          <div class="match-card__label"><span>下一場 NEXT FIXTURE</span></div>
          <p class="match-card__meta">2026/27 企甲聯賽 第 1 週 · 9/13（日）19:00 · 客場</p>
          <div class="match-card__fixture">
            <span class="match-card__team">台中磐石</span>
            <span class="match-card__vs">VS</span>
            <span class="match-card__team match-card__team--away">高雄先鋒</span>
          </div>
          <p class="match-card__scorers">楠梓足球場　賽程以官方公告為準</p>
        </article>
      </div>

      <div class="match-grid" id="match-grid-other" data-team-panel="other" :hidden="teamPanel !== 'other'">
        <div class="match-card match-card--placeholder">
          <p>青訓梯隊賽程尚未公開發布，敬請關注後續公告。</p>
        </div>
      </div>

      <div class="match-band__actions">
        <a class="btn btn--light" href="/zh/schedule/">查看完整行事曆</a>
        <a class="btn btn--light" href="/zh/schedule/">訂閱一線隊賽程 (.ics)</a>
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
          <a href="/zh/club/first-team/">查看完整名單 →</a>
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
  <section v-if="isTcrfc" class="band values-band" id="values" aria-labelledby="values-title">
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
  <section class="band grain pillars-band" id="club" aria-labelledby="pillars-title">
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
        <a v-for="(pillar, i) in pillars" :id="pillar.id" :key="pillar.enLabel" class="pillar-card clip-card clip-card--on-dark" :href="pillar.href">
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

  <!-- SPEC 3.7 — News mosaic
       藍鯨新聞 07 單元自有全文 0 篇（gap-analysis.md §4 #3），本區塊不顯示。
       🔴 S0-9e：本區塊內容逐字沿用 mockup（本頁檔頭註解「main 內容不動」），不是資料驅動——
       5 張卡片的 href 原本寫死 /zh/news/article/，改為依各卡片自己 <img> 的檔名
       （已對應 /assets/img/news/{slug}.jpg 的既有命名慣例）反推出真實 slug，
       組成逐篇網址；沒有改成打 API，因為這個區塊本身就是精選 5 篇的靜態展示，
       跟 news/index.vue 的資料驅動清單是兩回事。 -->
  <section v-if="isTcrfc" class="band news-band" id="news" aria-labelledby="news-title">
    <div class="band-inner container">
      <div class="eyebrow-row">
        <div>
          <p class="kicker">LATEST STORIES</p>
          <h2 class="section-title" id="news-title">最新消息</h2>
        </div>
        <a class="btn btn--dark btn--sm" href="/zh/news/">所有新聞</a>
      </div>

      <div class="news-mosaic">
        <a class="news-card clip-card news-card--feature" href="/zh/news/2026-05-17-match-002/">
          <div class="news-card__media">
            <span class="news-card__tag">消息 News</span>
            <img src="/assets/img/news/2026-05-17-match-002.jpg" alt="企甲聯賽 台中磐石 1-2 陽信北競" loading="lazy" width="1280" height="853">
          </div>
          <div class="news-card__body">
            <p class="news-card__meta">2026/05/17</p>
            <p class="news-card__title">企甲聯賽 台中磐石 1-2 陽信北競</p>
          </div>
        </a>

        <a class="news-card clip-card news-card--sml" href="/zh/news/2026-05-10-match-003/">
          <div class="news-card__media">
            <span class="news-card__tag">比賽 Matches</span>
            <img src="/assets/img/news/2026-05-10-match-003.jpg" alt="企甲聯賽 台中磐石 2-4 南市台鋼" loading="lazy" width="1280" height="855">
          </div>
          <div class="news-card__body">
            <p class="news-card__meta">2026/05/10</p>
            <p class="news-card__title">企甲聯賽 台中磐石 2-4 南市台鋼</p>
          </div>
        </a>

        <a class="news-card clip-card news-card--sml" href="/zh/news/2026-05-03-match-005/">
          <div class="news-card__media">
            <span class="news-card__tag">比賽 Matches</span>
            <img src="/assets/img/news/2026-05-03-match-005.jpg" alt="乙級聯賽 台中磐石預備隊 0-2 銘傳Desafio" loading="lazy" width="1280" height="855">
          </div>
          <div class="news-card__body">
            <p class="news-card__meta">2026/05/03</p>
            <p class="news-card__title">乙級聯賽 台中磐石預備隊 0-2 銘傳Desafio</p>
          </div>
        </a>

        <a class="news-card clip-card news-card--wide" href="/zh/news/2026-08-10-international-000/">
          <div class="news-card__inner" style="display:flex;width:100%;">
            <div class="news-card__media">
              <span class="news-card__tag">國際動態 International</span>
              <img src="/assets/img/news/2026-08-10-international-000.jpg" alt="台中磐石與AS Trenčín深化青訓合作　共創台斯足球交流新篇章" loading="lazy" width="1600" height="1067">
            </div>
            <div class="news-card__body">
              <p class="news-card__meta">2026/08/10</p>
              <p class="news-card__title">台中磐石與AS Trenčín深化青訓合作　共創台斯足球交流新篇章</p>
            </div>
          </div>
        </a>

        <a class="news-card clip-card news-card--wide" href="/zh/news/2026-05-24-match-001/">
          <div class="news-card__inner" style="display:flex;width:100%;">
            <div class="news-card__media">
              <span class="news-card__tag">比賽 Matches</span>
              <img src="/assets/img/news/2026-05-24-match-001.jpg" alt="企甲聯賽 台中磐石 3-0 銘傳大學" loading="lazy" width="1600" height="1067">
            </div>
            <div class="news-card__body">
              <p class="news-card__meta">2026/05/24</p>
              <p class="news-card__title">企甲聯賽 台中磐石 3-0 銘傳大學</p>
            </div>
          </div>
        </a>
      </div>
    </div>
  </section>

  <!-- SPEC 3.8 — Official store band
       藍鯨商店 0 商品、無物流與價格資訊（gap-analysis.md §4 #1），本區塊不顯示。 -->
  <section v-if="isTcrfc" class="band grain grain--2 store-band" aria-labelledby="store-title">
    <span class="ghost-num ghost-num--dark" aria-hidden="true">08</span>
    <div class="band-inner container">
      <div class="store-band__grid">
        <div>
          <p class="kicker kicker--on-dark">TEAM UP IN STYLE</p>
          <h2 class="section-title" id="store-title">官方商店</h2>
          <p>主客場球衣、周邊配件與訓練服飾，穿上台中磐石桃紅，與球隊一起在場邊、場上同進退。</p>
          <a class="btn btn--primary" href="/zh/culture/merchandise/">官方商品 MERCHANDISE</a>
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
  <section class="band sponsor-band" id="partners" aria-labelledby="partners-title">
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
  <section class="band grain cta-band" id="charity" aria-labelledby="cta-title">
    <div class="band-inner container">
      <h2 class="visually-hidden" id="cta-title">加入{{ assets.shortNameZh }}</h2>
      <div class="cta-grid">
        <div v-for="card in ctaTrio" :key="card.num" class="cta-card">
          <p class="cta-card__num">{{ card.num }}</p>
          <p class="cta-card__title">{{ card.titleZh }}</p>
          <p class="cta-card__desc">{{ card.descZh }}</p>
          <a class="btn btn--primary" :href="card.href">{{ card.ctaLabelZh }}</a>
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
