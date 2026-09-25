<script setup lang="ts">
// app/components/SiteHeader.vue — 由 site/src/partials/header.html 轉來
//
// 🔴 DOM 結構與 class 一律不動（docs/14-invariants.md）；{{ROOT}} 在 mockup 是相對路徑，
// Nuxt 掛載在站根，一律改成絕對路徑（即直接省略，等同空字串）。
// ⛔ style／script 標籤不得留在樣板區塊裡（紀律 9）；本檔沒有頁面專屬樣式可搬，
// 行為邏輯（sticky header 陰影、行動選單開闔、mega menu hover/focus/Esc）從
// site/src/assets/js/site.js 移植到下面的 script setup，改綁 template ref 而非
// document.getElementById，其餘行為（含 220ms 死區延遲的理由）逐字保留原註解。
//
// 單元開關呼叫點 2／4：女子足球（06）與慈善（11）兩個 mega-menu 以外的單項連結，
// 依 isUnitEnabledForClub 決定要不要出現在導覽（桌機／行動版共用同一份過濾結果）。
//
// 文案依俱樂部切換（docs/13-blue-whale-site.md §6 紀律 11）：導覽本身的「關於＿＿」
// 「＿＿文化」與社群連結網址含俱樂部名稱／官方帳號，屬於「俱樂部自己的事實」，
// 從 shared/utils/club-copy.ts 的單一真實來源取值，不得在這裡另外硬編碼一份。
//
// S0-9n（2026-09-23）：04 學院 mega menu 主標籤本來就正確用 identity.academyLabelZh
// （04 單元的全名），但底下 7 個子項目與 1 個 CTA 按鈕字面寫死「學院」，藍鯨站因此
// 出現「標題說青年隊、子項目說學院」的自相矛盾；07 新聞 mega menu 的「7.3 學院新聞」
// 同一個形狀，順手一併修。改用 identity.academyShortLabelZh（04 單元的**短名**，
// club-copy.ts 既有欄位，磐石值＝「學院」逐字對應 mockup、藍鯨值＝「青年隊」）逐字
// 替換這些位置裡的「學院」二字，磐石端因此渲染結果與 mockup 完全不變。
// ⚠️ 這個欄位原本叫 academyJoinLabelZh、只給 SiteFooter「加入＿＿」一句用；這次把它
// 用到本檔 8 處非 join 的複合句時，同一次交付內把欄位改名並改寫 JSDoc（見
// club-copy.ts `ClubIdentity.academyShortLabelZh` 的說明），不是留著錯的名字加註解——
// 名字錯了本身就是 E-42 那個根因（一個欄位身兼兩種語境）的同一種現形。
// ⛔ 只是換詞消除矛盾，不是內容架構決策——這七個子項目該不該對藍鯨保留、藍鯨青年隊
// 自己的課程架構怎麼寫，等 B-6 藍鯨素材到位後再決定（docs/13 §3）。
const config = useRuntimeConfig()
const club = computed(() => config.public.club)
const assets = computed(() => getClubAssets(club.value))
const identity = computed(() => getClubIdentity(club.value))
const showWomens = computed(() => isUnitEnabledForClub('06', club.value))
const showCharity = computed(() => isUnitEnabledForClub('11', club.value))

const route = useRoute()
const activeNav = computed(() => route.meta.nav as string | undefined)

// 所有導覽連結（本檔上面 78 處 href、1 處 NuxtLink to）一律用 lp() 換算成目前語系版本
// （S1-13，shared/utils/locale.ts 的單一真實來源），不得改回寫死 /zh/——語系切換器
// 本身另外用 switchTo()（見下方樣板），因為它要「切去另一個語系」，不是「留在目前語系」。
const { locale, lp, switchTo } = useLocale()

// ---- Sticky header shadow ----
const headerEl = ref<HTMLElement | null>(null)
function onScroll() {
  if (!headerEl.value) return
  headerEl.value.classList.toggle('is-scrolled', window.scrollY > 8)
}

// ---- Mobile nav toggle ----
const mobileNavEl = ref<HTMLElement | null>(null)
const openBtnEl = ref<HTMLElement | null>(null)
const closeBtnEl = ref<HTMLElement | null>(null)
function openMobileNav() {
  mobileNavEl.value?.classList.add('is-open')
  openBtnEl.value?.setAttribute('aria-expanded', 'true')
  document.body.style.overflow = 'hidden'
  closeBtnEl.value?.focus()
}
function closeMobileNav() {
  mobileNavEl.value?.classList.remove('is-open')
  openBtnEl.value?.setAttribute('aria-expanded', 'false')
  document.body.style.overflow = ''
  openBtnEl.value?.focus()
}
function onMobileNavClick(e: MouseEvent) {
  if ((e.target as HTMLElement).tagName === 'A') closeMobileNav()
}
function onKeydownEsc(e: KeyboardEvent) {
  if (e.key === 'Escape' && mobileNavEl.value?.classList.contains('is-open')) closeMobileNav()
}

/* Mega Menu — hover 與鍵盤皆可開啟，Esc 關閉。
   .mega 是 position:absolute; top:100%，定位基準是 .site-header（.main-nav li
   刻意設為 position:static，讓面板能相對整個 header 全寬展開）。這代表 <li>
   的版面高度只等於連結本身，連結底緣與 .mega 頂緣之間，隔著 header 置中對齊
   留下的一段「死區」——游標往下移動經過這段死區時會先離開 <li> 的命中範圍，
   觸發 mouseleave，選單才還沒到就關閉了。
   兩段式修法：
   1) 關閉延遲（CLOSE_DELAY）：mouseleave 先不關，等一小段時間；只要游標在
      期限內抵達 .mega，選單就不會消失。
   2) CSS 死區橋接（.has-mega > a::before，見 tcrfc.css）：在觸發連結正下方
      補一塊不可見的可命中區域，讓 hover 範圍實際上連續，delay 只是保險。 */
const CLOSE_DELAY = 220
let closeTimer: ReturnType<typeof setTimeout> | null = null
let openItem: HTMLElement | null = null

function cancelClose() {
  if (closeTimer) {
    clearTimeout(closeTimer)
    closeTimer = null
  }
}
function hideMega(li: HTMLElement | null) {
  if (!li) return
  const m = li.querySelector<HTMLElement>('.mega')
  if (m) m.hidden = true
  li.classList.remove('is-open')
  if (openItem === li) openItem = null
}
function openMega(li: HTMLElement) {
  cancelClose()
  if (openItem && openItem !== li) hideMega(openItem)
  const m = li.querySelector<HTMLElement>('.mega')
  if (m) m.hidden = false
  li.classList.add('is-open')
  openItem = li
}
function scheduleCloseMega(li: HTMLElement) {
  cancelClose()
  closeTimer = setTimeout(() => {
    closeTimer = null
    hideMega(li)
  }, CLOSE_DELAY)
}
function onMegaKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && openItem) {
    cancelClose()
    hideMega(openItem)
  }
}

const mainNavEl = ref<HTMLElement | null>(null)
let megaItems: HTMLElement[] = []
function bindMegaMenu() {
  megaItems = Array.from(mainNavEl.value?.querySelectorAll<HTMLElement>('.has-mega') ?? [])
  for (const li of megaItems) {
    li.addEventListener('mouseenter', () => openMega(li))
    li.addEventListener('mouseleave', () => scheduleCloseMega(li))
    li.addEventListener('focusin', () => openMega(li))
    li.addEventListener('focusout', (e: FocusEvent) => {
      if (!li.contains(e.relatedTarget as Node)) hideMega(li)
    })
  }
}

onMounted(() => {
  document.addEventListener('scroll', onScroll, { passive: true })
  onScroll()
  document.addEventListener('keydown', onKeydownEsc)
  document.addEventListener('keydown', onMegaKeydown)
  bindMegaMenu()
})
onBeforeUnmount(() => {
  document.removeEventListener('scroll', onScroll)
  document.removeEventListener('keydown', onKeydownEsc)
  document.removeEventListener('keydown', onMegaKeydown)
  cancelClose()
})
</script>

<template>
  <div class="utility-bar">
    <div class="container">
      <div class="utility-bar__left">
        <div class="lang-switch" role="group" aria-label="網站語言切換">
          <button type="button" :aria-current="locale === 'zh' ? 'true' : undefined" lang="zh-Hant" @click="switchTo('zh')">繁中</button>
          <span aria-hidden="true">|</span>
          <button type="button" :aria-current="locale === 'en' ? 'true' : undefined" lang="en" @click="switchTo('en')">EN</button>
        </div>
        <div class="utility-bar__member">
          <a :href="lp('/zh/member/')">會員登入</a><span class="divider">/</span><a :href="lp('/zh/member/#tab-register')">註冊</a>
        </div>
      </div>
      <div class="utility-bar__right">
        <nav class="social-row" aria-label="社群媒體">
          <a v-if="identity.social.facebook" :href="identity.social.facebook" aria-label="前往 Facebook 粉絲專頁" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z" /></svg>
          </a>
          <a v-if="identity.social.instagram" :href="identity.social.instagram" aria-label="前往 Instagram" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.2" cy="6.8" r="1" /></svg>
          </a>
          <a v-if="identity.social.youtube" :href="identity.social.youtube" aria-label="前往 YouTube 頻道" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8" /><path d="M10 9.5l6 2.5-6 2.5z" /></svg>
          </a>
        </nav>
      </div>
    </div>
  </div>

  <header ref="headerEl" class="site-header" id="site-header">
    <div class="container">
      <NuxtLink class="brand-lockup" :to="lp('/zh/')" :aria-label="`${assets.nameZh} 首頁`">
        <img :src="assets.headerMark.src" alt="" aria-hidden="true" :width="assets.headerMark.width" :height="assets.headerMark.height">
      </NuxtLink>

      <nav ref="mainNavEl" class="main-nav" aria-label="主要導覽">
        <ul>
          <li class="has-mega">
            <a :href="lp('/zh/about/')" data-nav="about" :aria-current="activeNav === 'about' ? 'page' : undefined">{{ identity.aboutLabelZh }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/about/our-story/')">2.1 我們的故事</a></li>
                  <li><a :href="lp('/zh/about/vision-mission/')">2.2 願景與使命</a></li>
                  <li><a :href="lp('/zh/about/philosophy/')">2.3 足球理念</a></li>
                  <li><a :href="lp('/zh/about/our-people/')">2.4 團隊成員</a></li>
                  <li><a :href="lp('/zh/about/governance/')">2.5 治理與管理</a></li>
                  <li><a :href="lp('/zh/about/ecosystem/')">2.6 生態系</a></li>
                  <li><a :href="lp('/zh/about/history/')">2.7 俱樂部歷程</a></li>
                  <li><a :href="lp('/zh/about/milestones/')">2.8 重要里程碑</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-about.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/about/our-story/')">認識{{ assets.shortNameZh }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/club/')" data-nav="club" :aria-current="activeNav === 'club' ? 'page' : undefined">俱樂部</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/club/first-team/')">3.1 一線隊</a></li>
                  <li><a :href="lp('/zh/club/player-development/')">3.2 球員發展系統</a></li>
                  <li><a :href="lp('/zh/club/opportunities/')">3.3 球員機會</a></li>
                  <li><a :href="lp('/zh/club/international-pathways/')">3.4 國際發展通道</a></li>
                  <li><a :href="lp('/zh/club/player-stories/')">3.5 球員故事</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-club.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/player/')">加入球隊</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/academy/')" data-nav="academy" :aria-current="activeNav === 'academy' ? 'page' : undefined">{{ identity.academyLabelZh }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/academy/overview/')">4.1 {{ identity.academyShortLabelZh }}總覽</a></li>
                  <li><a :href="lp('/zh/academy/teams/')">4.2 {{ identity.academyShortLabelZh }}隊伍</a></li>
                  <li><a :href="lp('/zh/academy/pathway/')">4.3 {{ identity.academyShortLabelZh }}發展路徑</a></li>
                  <li><a :href="lp('/zh/academy/curriculum/')">4.4 訓練課程與課綱</a></li>
                  <li><a :href="lp('/zh/academy/coaches/')">4.5 {{ identity.academyShortLabelZh }}教練團</a></li>
                  <li><a :href="lp('/zh/academy/life/')">4.6 {{ identity.academyShortLabelZh }}生活</a></li>
                  <li><a :href="lp('/zh/academy/join/')">4.7 加入{{ identity.academyShortLabelZh }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-academy.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/academy/')">加入{{ identity.academyShortLabelZh }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/programs/')" data-nav="programs" :aria-current="activeNav === 'programs' ? 'page' : undefined">課程</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/programs/childrens-training/')">5.1 兒童足球訓練</a></li>
                  <li><a :href="lp('/zh/programs/summer-camp/')">5.2 夏令營</a></li>
                  <li><a :href="lp('/zh/programs/winter-camp/')">5.3 冬令營</a></li>
                  <li><a :href="lp('/zh/programs/specialist/')">5.4 專項訓練</a></li>
                  <li><a :href="lp('/zh/programs/school-community/')">5.5 校園與社區計畫</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-programs.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/academy/')">報名課程</a>
                </div>
              </div>
            </div>
          </li>
          <li v-if="showWomens"><a :href="lp('/zh/womens/')" data-nav="womens" :aria-current="activeNav === 'womens' ? 'page' : undefined">女子足球</a></li>
          <li><a :href="lp('/zh/schedule/')" data-nav="schedule" :aria-current="activeNav === 'schedule' ? 'page' : undefined">賽事</a></li>
          <li class="has-mega">
            <a :href="lp('/zh/news/')" data-nav="news" :aria-current="activeNav === 'news' ? 'page' : undefined">新聞</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/news/club/')">7.1 俱樂部新聞</a></li>
                  <li><a :href="lp('/zh/news/match/')">7.2 比賽報導</a></li>
                  <li><a :href="lp('/zh/news/academy/')">7.3 {{ identity.academyShortLabelZh }}新聞</a></li>
                  <li><a :href="lp('/zh/news/player-stories/')">7.4 球員故事</a></li>
                  <li><a :href="lp('/zh/news/international/')">7.5 國際動態</a></li>
                  <li><a :href="lp('/zh/news/camps-events/')">7.6 營隊與活動</a></li>
                  <li><a :href="lp('/zh/news/community/')">7.7 社區活動</a></li>
                  <li><a :href="lp('/zh/news/media/')">7.8 媒體專區</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-news.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/news/')">所有消息</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/culture/')" data-nav="culture" :aria-current="activeNav === 'culture' ? 'page' : undefined">{{ identity.cultureLabelZh }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/culture/manga/')">8.1 {{ assets.shortNameZh }}漫畫</a></li>
                  <li><a :href="lp('/zh/culture/fan-club/')">8.2 {{ assets.shortNameZh }}球迷會</a></li>
                  <li><a :href="lp('/zh/culture/merchandise/')">8.3 官方商品</a></li>
                  <li><a :href="lp('/zh/shop/')">8.3 官方商店 SHOP</a></li>
                  <li><a :href="lp('/zh/perks/')">8.4 特約店家</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-culture.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/culture/fan-club/')">加入球迷會</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/partners/')" data-nav="partners" :aria-current="activeNav === 'partners' ? 'page' : undefined">夥伴</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/partners/our-partners/')">9.1 合作夥伴</a></li>
                  <li><a :href="lp('/zh/partners/our-sponsors/')">9.2 贊助商</a></li>
                  <li><a :href="lp('/zh/partners/become-a-partner/')">9.3 成為夥伴</a></li>
                  <li><a :href="lp('/zh/partners/opportunities/')">9.4 贊助方案</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-partners.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/partnership/')">洽談贊助</a>
                </div>
              </div>
            </div>
          </li>
          <li v-if="showCharity"><a :href="lp('/zh/charity/')" data-nav="charity" :aria-current="activeNav === 'charity' ? 'page' : undefined">慈善</a></li>
        </ul>
      </nav>

      <div class="header-actions">
        <button class="icon-btn" type="button" aria-label="搜尋">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" stroke-linecap="round" /></svg>
        </button>
        <a class="icon-btn" :href="lp('/zh/cart/')" aria-label="購物車（2 件商品）">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 4h2l2.4 10.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.55L21 8H6" /><circle cx="10" cy="20" r="1.4" /><circle cx="18" cy="20" r="1.4" /></svg>
          <span class="cart-count" aria-hidden="true">2</span>
        </a>
        <a class="btn btn--primary btn--sm" :href="lp('/zh/join/')">加入我們 JOIN</a>
        <button ref="openBtnEl" class="icon-btn hamburger" type="button" id="menu-open-btn" aria-haspopup="true" aria-controls="mobile-nav" aria-expanded="false" aria-label="開啟選單" @click="openMobileNav">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 6h16M4 12h16M4 18h16" /></svg>
        </button>
      </div>
    </div>
  </header>

  <div ref="mobileNavEl" class="mobile-nav" id="mobile-nav" role="dialog" aria-modal="true" aria-label="行動選單" @click="onMobileNavClick">
    <div class="mobile-nav__top">
      <img :src="assets.headerMark.src" :alt="`${assets.nameZh}隊徽`" width="33" height="34">
      <button ref="closeBtnEl" class="mobile-nav__close" type="button" id="menu-close-btn" aria-label="關閉選單" @click="closeMobileNav">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18" /></svg>
      </button>
    </div>
    <nav aria-label="行動主要導覽">
      <ul>
        <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }} ABOUT</a></li>
        <li><a :href="lp('/zh/club/')">俱樂部 CLUB</a></li>
        <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }} {{ identity.academyLabelEn }}</a></li>
        <li><a :href="lp('/zh/programs/')">課程 PROGRAMS</a></li>
        <li v-if="showWomens"><a :href="lp('/zh/womens/')">女子足球 WOMEN'S</a></li>
        <li><a :href="lp('/zh/schedule/')">賽事 SCHEDULE</a></li>
        <li><a :href="lp('/zh/news/')">新聞 NEWS</a></li>
        <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }} CULTURE</a></li>
        <li><a :href="lp('/zh/shop/')">官方商店 SHOP</a></li>
        <li><a :href="lp('/zh/partners/')">夥伴 PARTNERS</a></li>
        <li v-if="showCharity"><a :href="lp('/zh/charity/')">慈善 CHARITY</a></li>
        <li><a :href="lp('/zh/faq/')">常見問題 FAQ</a></li>
      </ul>
    </nav>
    <div class="mobile-nav__cta">
      <a class="btn btn--primary btn--block" :href="lp('/zh/join/')">加入我們 JOIN</a>
      <div class="lang-switch" role="group" aria-label="網站語言切換" style="color:#fff;justify-content:center;">
        <button type="button" :aria-current="locale === 'zh' ? 'true' : undefined" :style="locale === 'zh' ? 'color:#fff;' : 'color:var(--muted-dark);'" @click="switchTo('zh')">繁中</button>
        <span aria-hidden="true" style="color:rgba(255,255,255,.3);">|</span>
        <button type="button" :aria-current="locale === 'en' ? 'true' : undefined" :style="locale === 'en' ? 'color:#fff;' : 'color:var(--muted-dark);'" @click="switchTo('en')">EN</button>
      </div>
    </div>
  </div>

  <div class="mobile-cta-bar" aria-label="快速行動">
    <a class="btn btn--primary btn--sm" :href="lp('/zh/join/player/')">加入球隊</a>
    <a class="btn btn--dark btn--sm" :href="lp('/zh/join/general/')">聯絡我們</a>
  </div>
</template>
