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
const config = useRuntimeConfig()
const club = computed(() => config.public.club)
const assets = computed(() => getClubAssets(club.value))
const showWomens = computed(() => isUnitEnabledForClub('06', club.value))
const showCharity = computed(() => isUnitEnabledForClub('11', club.value))

const route = useRoute()
const activeNav = computed(() => route.meta.nav as string | undefined)

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
          <button type="button" aria-current="true" lang="zh-Hant">繁中</button>
          <span aria-hidden="true">|</span>
          <button type="button" lang="en">EN</button>
        </div>
        <div class="utility-bar__member">
          <a href="/zh/member/">會員登入</a><span class="divider">/</span><a href="/zh/member/#tab-register">註冊</a>
        </div>
      </div>
      <div class="utility-bar__right">
        <nav class="social-row" aria-label="社群媒體">
          <a href="https://www.facebook.com/TCRFC2024" aria-label="前往 Facebook 粉絲專頁" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z" /></svg>
          </a>
          <a href="https://www.instagram.com/tcr_fc_2024" aria-label="前往 Instagram" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.2" cy="6.8" r="1" /></svg>
          </a>
          <a href="https://www.youtube.com/@TCRFC-2024" aria-label="前往 YouTube 頻道" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8" /><path d="M10 9.5l6 2.5-6 2.5z" /></svg>
          </a>
        </nav>
      </div>
    </div>
  </div>

  <header ref="headerEl" class="site-header" id="site-header">
    <div class="container">
      <NuxtLink class="brand-lockup" to="/zh/" :aria-label="`${assets.nameZh} 首頁`">
        <img :src="assets.headerMark.src" alt="" aria-hidden="true" :width="assets.headerMark.width" :height="assets.headerMark.height">
      </NuxtLink>

      <nav ref="mainNavEl" class="main-nav" aria-label="主要導覽">
        <ul>
          <li class="has-mega">
            <a href="/zh/about/" data-nav="about" :aria-current="activeNav === 'about' ? 'page' : undefined">關於台中磐石</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/about/our-story/">2.1 我們的故事</a></li>
                  <li><a href="/zh/about/vision-mission/">2.2 願景與使命</a></li>
                  <li><a href="/zh/about/philosophy/">2.3 足球理念</a></li>
                  <li><a href="/zh/about/our-people/">2.4 團隊成員</a></li>
                  <li><a href="/zh/about/governance/">2.5 治理與管理</a></li>
                  <li><a href="/zh/about/ecosystem/">2.6 生態系</a></li>
                  <li><a href="/zh/about/history/">2.7 俱樂部歷程</a></li>
                  <li><a href="/zh/about/milestones/">2.8 重要里程碑</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-about.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/about/our-story/">認識台中磐石</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a href="/zh/club/" data-nav="club" :aria-current="activeNav === 'club' ? 'page' : undefined">俱樂部</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/club/first-team/">3.1 一線隊</a></li>
                  <li><a href="/zh/club/player-development/">3.2 球員發展系統</a></li>
                  <li><a href="/zh/club/opportunities/">3.3 球員機會</a></li>
                  <li><a href="/zh/club/international-pathways/">3.4 國際發展通道</a></li>
                  <li><a href="/zh/club/player-stories/">3.5 球員故事</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-club.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/join/player/">加入球隊</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a href="/zh/academy/" data-nav="academy" :aria-current="activeNav === 'academy' ? 'page' : undefined">足球學院</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/academy/overview/">4.1 學院總覽</a></li>
                  <li><a href="/zh/academy/teams/">4.2 學院隊伍</a></li>
                  <li><a href="/zh/academy/pathway/">4.3 學院發展路徑</a></li>
                  <li><a href="/zh/academy/curriculum/">4.4 訓練課程與課綱</a></li>
                  <li><a href="/zh/academy/coaches/">4.5 學院教練團</a></li>
                  <li><a href="/zh/academy/life/">4.6 學院生活</a></li>
                  <li><a href="/zh/academy/join/">4.7 加入學院</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-academy.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/join/academy/">加入學院</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a href="/zh/programs/" data-nav="programs" :aria-current="activeNav === 'programs' ? 'page' : undefined">課程</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/programs/childrens-training/">5.1 兒童足球訓練</a></li>
                  <li><a href="/zh/programs/summer-camp/">5.2 夏令營</a></li>
                  <li><a href="/zh/programs/winter-camp/">5.3 冬令營</a></li>
                  <li><a href="/zh/programs/specialist/">5.4 專項訓練</a></li>
                  <li><a href="/zh/programs/school-community/">5.5 校園與社區計畫</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-programs.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/join/academy/">報名課程</a>
                </div>
              </div>
            </div>
          </li>
          <li v-if="showWomens"><a href="/zh/womens/" data-nav="womens" :aria-current="activeNav === 'womens' ? 'page' : undefined">女子足球</a></li>
          <li><a href="/zh/schedule/" data-nav="schedule" :aria-current="activeNav === 'schedule' ? 'page' : undefined">賽事</a></li>
          <li class="has-mega">
            <a href="/zh/news/" data-nav="news" :aria-current="activeNav === 'news' ? 'page' : undefined">新聞</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/news/club/">7.1 俱樂部新聞</a></li>
                  <li><a href="/zh/news/match/">7.2 比賽報導</a></li>
                  <li><a href="/zh/news/academy/">7.3 學院新聞</a></li>
                  <li><a href="/zh/news/player-stories/">7.4 球員故事</a></li>
                  <li><a href="/zh/news/international/">7.5 國際動態</a></li>
                  <li><a href="/zh/news/camps-events/">7.6 營隊與活動</a></li>
                  <li><a href="/zh/news/community/">7.7 社區活動</a></li>
                  <li><a href="/zh/news/media/">7.8 媒體專區</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-news.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/news/">所有消息</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a href="/zh/culture/" data-nav="culture" :aria-current="activeNav === 'culture' ? 'page' : undefined">台中磐石文化</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/culture/manga/">8.1 台中磐石漫畫</a></li>
                  <li><a href="/zh/culture/fan-club/">8.2 台中磐石球迷會</a></li>
                  <li><a href="/zh/culture/merchandise/">8.3 官方商品</a></li>
                  <li><a href="/zh/shop/">8.3 官方商店 SHOP</a></li>
                  <li><a href="/zh/perks/">8.4 特約店家</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-culture.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/culture/fan-club/">加入球迷會</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a href="/zh/partners/" data-nav="partners" :aria-current="activeNav === 'partners' ? 'page' : undefined">夥伴</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a href="/zh/partners/our-partners/">9.1 合作夥伴</a></li>
                  <li><a href="/zh/partners/our-sponsors/">9.2 贊助商</a></li>
                  <li><a href="/zh/partners/become-a-partner/">9.3 成為夥伴</a></li>
                  <li><a href="/zh/partners/opportunities/">9.4 贊助方案</a></li>
                </ul>
                <div class="mega__feature">
                  <img src="/assets/img/nav-partners.jpg" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" href="/zh/join/partnership/">洽談贊助</a>
                </div>
              </div>
            </div>
          </li>
          <li v-if="showCharity"><a href="/zh/charity/" data-nav="charity" :aria-current="activeNav === 'charity' ? 'page' : undefined">慈善</a></li>
        </ul>
      </nav>

      <div class="header-actions">
        <button class="icon-btn" type="button" aria-label="搜尋">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" stroke-linecap="round" /></svg>
        </button>
        <a class="icon-btn" href="/zh/cart/" aria-label="購物車（2 件商品）">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 4h2l2.4 10.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.55L21 8H6" /><circle cx="10" cy="20" r="1.4" /><circle cx="18" cy="20" r="1.4" /></svg>
          <span class="cart-count" aria-hidden="true">2</span>
        </a>
        <a class="btn btn--primary btn--sm" href="/zh/join/">加入我們 JOIN</a>
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
        <li><a href="/zh/about/">關於台中磐石 ABOUT</a></li>
        <li><a href="/zh/club/">俱樂部 CLUB</a></li>
        <li><a href="/zh/academy/">足球學院 ACADEMY</a></li>
        <li><a href="/zh/programs/">課程 PROGRAMS</a></li>
        <li v-if="showWomens"><a href="/zh/womens/">女子足球 WOMEN'S</a></li>
        <li><a href="/zh/schedule/">賽事 SCHEDULE</a></li>
        <li><a href="/zh/news/">新聞 NEWS</a></li>
        <li><a href="/zh/culture/">台中磐石文化 CULTURE</a></li>
        <li><a href="/zh/shop/">官方商店 SHOP</a></li>
        <li><a href="/zh/partners/">夥伴 PARTNERS</a></li>
        <li v-if="showCharity"><a href="/zh/charity/">慈善 CHARITY</a></li>
        <li><a href="/zh/faq/">常見問題 FAQ</a></li>
      </ul>
    </nav>
    <div class="mobile-nav__cta">
      <a class="btn btn--primary btn--block" href="/zh/join/">加入我們 JOIN</a>
      <div class="lang-switch" role="group" aria-label="網站語言切換" style="color:#fff;justify-content:center;">
        <button type="button" aria-current="true" style="color:#fff;">繁中</button>
        <span aria-hidden="true" style="color:rgba(255,255,255,.3);">|</span>
        <button type="button" style="color:var(--muted-dark);">EN</button>
      </div>
    </div>
  </div>

  <div class="mobile-cta-bar" aria-label="快速行動">
    <a class="btn btn--primary btn--sm" href="/zh/join/player/">加入球隊</a>
    <a class="btn btn--dark btn--sm" href="/zh/join/general/">聯絡我們</a>
  </div>
</template>
