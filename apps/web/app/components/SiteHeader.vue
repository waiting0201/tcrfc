<script setup lang="ts">
import { menuItemHref, type PublicMenuItem } from '#shared/utils/site-settings'
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
// 英文版（主站 /en/）改用英文識別；`getClubIdentityEn` 見 shared/utils/club-copy-en-core.ts。
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(club.value)))
// 導覽下拉的特色照片全是磐石（含未成年學員）——藍鯨站不輸出，只留按鈕（E-83、check-club-image-leak.mjs）。
const isTcrfc = computed(() => club.value === 'tcrfc')
const showWomens = computed(() => isUnitEnabledForClub('06', club.value))
const showCharity = computed(() => isUnitEnabledForClub('11', club.value))

const route = useRoute()
const activeNav = computed(() => route.meta.nav as string | undefined)

// 所有導覽連結（本檔上面 78 處 href、1 處 NuxtLink to）一律用 lp() 換算成目前語系版本
// （S1-13，shared/utils/locale.ts 的單一真實來源），不得改回寫死 /zh/——語系切換器
// 本身另外用 switchTo()（見下方樣板），因為它要「切去另一個語系」，不是「留在目前語系」。
const { locale, lp, switchTo, isEn, tx } = useLocale()
// I2 選單管理（H 批）：API 有主選單項目時改由 API 呈現，沒有時沿用下面寫死的既有選單（過渡策略，見 useSiteMenus.ts）。
const { main: apiMain, mega: apiMega } = useSiteMenus()
const useApiMenu = computed(() => apiMain.value.length > 0)
/** 下拉面板內容：項目自己的子項目；沒有時，用「大選單」位置中連結相同的項目的子項目補上。 */
function panelOf(item: PublicMenuItem): PublicMenuItem[] {
  if (item.children.length > 0) return item.children
  return item.url ? (apiMega.value.find((m) => m.url === item.url)?.children ?? []) : []
}
function hrefOf(item: PublicMenuItem): string | null {
  return menuItemHref(item, lp)
}
// 購物車件數（S3-5）：讀 BFF 寫的非 HttpOnly 提示 Cookie，只在瀏覽器端有值（SSR 一律 0，不讓 HTML 帶出個人狀態）。
const cartCount = useCartBadge()

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
// 選單由 API 改版後會重新渲染 .has-mega：用 WeakSet 避免對同一個 <li> 重複綁定事件。
const boundMega = new WeakSet<HTMLElement>()
function bindMegaMenu() {
  megaItems = Array.from(mainNavEl.value?.querySelectorAll<HTMLElement>('.has-mega') ?? [])
  for (const li of megaItems) {
    if (boundMega.has(li)) continue
    boundMega.add(li)
    li.addEventListener('mouseenter', () => openMega(li))
    li.addEventListener('mouseleave', () => scheduleCloseMega(li))
    li.addEventListener('focusin', () => openMega(li))
    li.addEventListener('focusout', (e: FocusEvent) => {
      if (!li.contains(e.relatedTarget as Node)) hideMega(li)
    })
  }
}

// ---- 全站搜尋（G-02，H 批）：按鈕展開搜尋列，送出後前往搜尋結果頁 ----
const searchOpen = ref(false)
const searchQuery = ref('')
const searchInputEl = ref<HTMLInputElement | null>(null)
const searchBtnEl = ref<HTMLElement | null>(null)
async function toggleSearch() {
  searchOpen.value = !searchOpen.value
  if (searchOpen.value) {
    await nextTick()
    searchInputEl.value?.focus()
  }
}
function closeSearch() {
  if (!searchOpen.value) return
  searchOpen.value = false
  searchBtnEl.value?.focus()
}
function submitSearch() {
  const q = searchQuery.value.trim()
  if (!q) {
    searchInputEl.value?.focus()
    return
  }
  searchOpen.value = false
  return navigateTo({ path: lp('/zh/search/'), query: { q } })
}
function onSearchKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') closeSearch()
}
watch(useApiMenu, () => nextTick(bindMegaMenu))

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
        <div class="lang-switch" role="group" :aria-label="tx('網站語言切換', 'Site language')">
          <button type="button" :aria-current="locale === 'zh' ? 'true' : undefined" lang="zh-Hant" @click="switchTo('zh')">繁中</button>
          <span aria-hidden="true">|</span>
          <button type="button" :aria-current="locale === 'en' ? 'true' : undefined" lang="en" @click="switchTo('en')">EN</button>
        </div>
        <div class="utility-bar__member">
          <a :href="lp('/zh/member/')">{{ tx('會員登入', 'Member login') }}</a><span class="divider">/</span><a :href="lp('/zh/member/#tab-register')">{{ tx('註冊', 'Register') }}</a>
        </div>
      </div>
      <div class="utility-bar__right">
        <nav class="social-row" :aria-label="tx('社群媒體', 'Social media')">
          <a v-if="identity.social.facebook" :href="identity.social.facebook" :aria-label="tx('前往 Facebook 粉絲專頁', 'Visit our Facebook page')" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z" /></svg>
          </a>
          <a v-if="identity.social.instagram" :href="identity.social.instagram" :aria-label="tx('前往 Instagram', 'Visit our Instagram')" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.2" cy="6.8" r="1" /></svg>
          </a>
          <a v-if="identity.social.youtube" :href="identity.social.youtube" :aria-label="tx('前往 YouTube 頻道', 'Visit our YouTube channel')" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8" /><path d="M10 9.5l6 2.5-6 2.5z" /></svg>
          </a>
        </nav>
      </div>
    </div>
  </div>

  <header ref="headerEl" class="site-header" id="site-header">
    <div class="container">
      <NuxtLink class="brand-lockup" :to="lp('/zh/')" :aria-label="tx(`${assets.nameZh} 首頁`, `${CLUB_NAME_EN} home`)">
        <img :src="assets.headerMark.src" alt="" aria-hidden="true" :width="assets.headerMark.width" :height="assets.headerMark.height">
      </NuxtLink>

      <nav ref="mainNavEl" class="main-nav" :aria-label="tx('主要導覽', 'Main navigation')">
        <!-- I2 選單管理（H 批）：後台設定了主選單就改讀 API；下拉面板用項目的子項目（最多再一層）。
             API 版沒有特色圖片與按鈕（那些是磐石專屬、不在選單資料內）。 -->
        <ul v-if="useApiMenu">
          <li v-for="item in apiMain" :key="item.id" :class="{ 'has-mega': panelOf(item).length > 0 }">
            <a v-if="hrefOf(item)" :href="hrefOf(item)!" :aria-current="hrefOf(item) === route.path ? 'page' : undefined" v-bind="item.isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {}">{{ item.label }}</a>
            <a v-else href="#" role="button" @click.prevent>{{ item.label }}</a>
            <div v-if="panelOf(item).length > 0" class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li v-for="child in panelOf(item)" :key="child.id">
                    <a v-if="hrefOf(child)" :href="hrefOf(child)!" v-bind="child.isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {}">{{ child.label }}</a>
                    <span v-else>{{ child.label }}</span>
                    <ul v-if="child.children.length > 0" class="mega__sub">
                      <li v-for="leaf in child.children" :key="leaf.id">
                        <a v-if="hrefOf(leaf)" :href="hrefOf(leaf)!" v-bind="leaf.isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {}">{{ leaf.label }}</a>
                        <span v-else>{{ leaf.label }}</span>
                      </li>
                    </ul>
                  </li>
                </ul>
              </div>
            </div>
          </li>
        </ul>
        <ul v-else>
          <li class="has-mega">
            <a :href="lp('/zh/about/')" data-nav="about" :aria-current="activeNav === 'about' ? 'page' : undefined">{{ identity.aboutLabelZh }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/about/our-story/')">2.1 {{ tx('我們的故事', 'Our Story') }}</a></li>
                  <li><a :href="lp('/zh/about/vision-mission/')">2.2 {{ tx('願景與使命', 'Vision & Mission') }}</a></li>
                  <li><a :href="lp('/zh/about/philosophy/')">2.3 {{ tx('足球理念', 'Our Philosophy') }}</a></li>
                  <li><a :href="lp('/zh/about/our-people/')">2.4 {{ tx('團隊成員', 'Our People') }}</a></li>
                  <li><a :href="lp('/zh/about/governance/')">2.5 {{ tx('治理與管理', 'Governance') }}</a></li>
                  <li><a :href="lp('/zh/about/ecosystem/')">2.6 {{ tx('生態系', 'Ecosystem') }}</a></li>
                  <li><a :href="lp('/zh/about/history/')">2.7 {{ tx('俱樂部歷程', 'Club History') }}</a></li>
                  <li><a :href="lp('/zh/about/milestones/')">2.8 {{ tx('重要里程碑', 'Key Milestones') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-about.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/about/our-story/')">{{ tx(`認識${assets.shortNameZh}`, 'Get to know TCRFC') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/club/')" data-nav="club" :aria-current="activeNav === 'club' ? 'page' : undefined">{{ tx('俱樂部', 'Football Club') }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/club/first-team/')">3.1 {{ tx('一線隊', 'First Team') }}</a></li>
                  <!-- BW-C1：3.2／3.4 已重開（見 shared/utils/units.ts 檔頭），isUnitEnabledForClub
                       現在對兩俱樂部都回傳 true，選單自動恢復連結，本身不必再改。 -->
                  <li v-if="isUnitEnabledForClub('3.2', club)"><a :href="lp('/zh/club/player-development/')">3.2 {{ tx('球員發展系統', 'Player Development') }}</a></li>
                  <li><a :href="lp('/zh/club/opportunities/')">3.3 {{ tx('球員機會', 'Player Opportunities') }}</a></li>
                  <li v-if="isUnitEnabledForClub('3.4', club)"><a :href="lp('/zh/club/international-pathways/')">3.4 {{ tx('國際發展通道', 'International Pathways') }}</a></li>
                  <li><a :href="lp('/zh/club/player-stories/')">3.5 {{ tx('球員故事', 'Player Stories') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-club.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/player/')">{{ tx('加入球隊', 'Join as a Player') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/academy/')" data-nav="academy" :aria-current="activeNav === 'academy' ? 'page' : undefined">{{ identity.academyLabelZh }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/academy/overview/')">4.1 {{ tx(`${identity.academyShortLabelZh}總覽`, 'Academy Overview') }}</a></li>
                  <li><a :href="lp('/zh/academy/teams/')">4.2 {{ tx(`${identity.academyShortLabelZh}隊伍`, 'Our Teams') }}</a></li>
                  <li><a :href="lp('/zh/academy/pathway/')">4.3 {{ tx(`${identity.academyShortLabelZh}發展路徑`, 'Academy Pathway') }}</a></li>
                  <li><a :href="lp('/zh/academy/curriculum/')">4.4 {{ tx('訓練課程與課綱', 'Training & Curriculum') }}</a></li>
                  <!-- BW-C1：4.5／4.6 已重開，只有 4.7（招生架構）維持關閉（藍鯨規劃書 §1.3
                       總則例外，見 shared/utils/units.ts 檔頭）。isUnitEnabledForClub
                       已隨 units.ts 更新自動反映，本身不必再改。 -->
                  <li v-if="isUnitEnabledForClub('4.5', club)"><a :href="lp('/zh/academy/coaches/')">4.5 {{ tx(`${identity.academyShortLabelZh}教練團`, 'Coaches') }}</a></li>
                  <li v-if="isUnitEnabledForClub('4.6', club)"><a :href="lp('/zh/academy/life/')">4.6 {{ tx(`${identity.academyShortLabelZh}生活`, 'Academy Life') }}</a></li>
                  <li v-if="isUnitEnabledForClub('4.7', club)"><a :href="lp('/zh/academy/join/')">4.7 {{ tx(`加入${identity.academyShortLabelZh}`, 'Join the Academy') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-academy.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/academy/')">{{ tx(`加入${identity.academyShortLabelZh}`, 'Join the Academy') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/programs/')" data-nav="programs" :aria-current="activeNav === 'programs' ? 'page' : undefined">{{ tx('課程', 'Programs') }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <!-- BW-C1：5.1–5.4 已重開（藍鯨規劃書 §3.5 明文「頁面開放、報名功能
                       待確認」，見 shared/utils/units.ts 檔頭），isUnitEnabledForClub
                       已隨 units.ts 更新自動反映，本身不必再改。5.5 一直對藍鯨開放。 -->
                  <li v-if="isUnitEnabledForClub('5.1', club)"><a :href="lp('/zh/programs/childrens-training/')">5.1 {{ tx('兒童足球訓練', "Children's Training") }}</a></li>
                  <li v-if="isUnitEnabledForClub('5.2', club)"><a :href="lp('/zh/programs/summer-camp/')">5.2 {{ tx('夏令營', 'Summer Camp') }}</a></li>
                  <li v-if="isUnitEnabledForClub('5.3', club)"><a :href="lp('/zh/programs/winter-camp/')">5.3 {{ tx('冬令營', 'Winter Camp') }}</a></li>
                  <li v-if="isUnitEnabledForClub('5.4', club)"><a :href="lp('/zh/programs/specialist/')">5.4 {{ tx('專項訓練', 'Specialist Training') }}</a></li>
                  <li><a :href="lp('/zh/programs/school-community/')">5.5 {{ tx('校園與社區計畫', 'School & Community') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-programs.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/academy/')">{{ tx('報名課程', 'Register for Programs') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li v-if="showWomens"><a :href="lp('/zh/womens/')" data-nav="womens" :aria-current="activeNav === 'womens' ? 'page' : undefined">{{ tx('女子足球', "Women's Football") }}</a></li>
          <li><a :href="lp('/zh/schedule/')" data-nav="schedule" :aria-current="activeNav === 'schedule' ? 'page' : undefined">{{ tx('賽事', 'Schedule') }}</a></li>
          <li class="has-mega">
            <a :href="lp('/zh/news/')" data-nav="news" :aria-current="activeNav === 'news' ? 'page' : undefined">{{ tx('新聞', 'News') }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/news/club/')">7.1 {{ tx('俱樂部新聞', 'Club News') }}</a></li>
                  <li><a :href="lp('/zh/news/match/')">7.2 {{ tx('比賽報導', 'Match Reports') }}</a></li>
                  <li><a :href="lp('/zh/news/academy/')">7.3 {{ tx(`${identity.academyShortLabelZh}新聞`, 'Academy News') }}</a></li>
                  <li><a :href="lp('/zh/news/player-stories/')">7.4 {{ tx('球員故事', 'Player Stories') }}</a></li>
                  <li><a :href="lp('/zh/news/international/')">7.5 {{ tx('國際動態', 'International News') }}</a></li>
                  <li><a :href="lp('/zh/news/camps-events/')">7.6 {{ tx('營隊與活動', 'Camps & Events') }}</a></li>
                  <li><a :href="lp('/zh/news/community/')">7.7 {{ tx('社區活動', 'Community Events') }}</a></li>
                  <li><a :href="lp('/zh/news/media/')">7.8 {{ tx('媒體專區', 'Press & Media') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-news.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/news/')">{{ tx('所有消息', 'All News') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/culture/')" data-nav="culture" :aria-current="activeNav === 'culture' ? 'page' : undefined">{{ identity.cultureLabelZh }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li v-if="isUnitEnabledForClub('8.1', club)"><a :href="lp('/zh/culture/manga/')">8.1 {{ tx(`${assets.shortNameZh}漫畫`, 'TCRFC Manga') }}</a></li>
                  <li><a :href="lp('/zh/culture/fan-club/')">8.2 {{ tx(`${assets.shortNameZh}球迷會`, 'TCRFC Fan Club') }}</a></li>
                  <li><a :href="lp('/zh/culture/merchandise/')">8.3 {{ tx('官方商品', 'Merchandise') }}</a></li>
                  <li><a :href="lp('/zh/shop/')">8.3 {{ tx('官方商店 SHOP', 'Official Store') }}</a></li>
                  <li><a :href="lp('/zh/perks/')">8.4 {{ tx('特約店家', 'Partner Perks') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-culture.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/culture/fan-club/')">{{ tx('加入球迷會', 'Join the Fan Club') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li class="has-mega">
            <a :href="lp('/zh/partners/')" data-nav="partners" :aria-current="activeNav === 'partners' ? 'page' : undefined">{{ tx('夥伴', 'Partners') }}</a>
            <div class="mega" hidden>
              <div class="container mega__inner">
                <ul class="mega__list">
                  <li><a :href="lp('/zh/partners/our-partners/')">9.1 {{ tx('合作夥伴', 'Our Partners') }}</a></li>
                  <li><a :href="lp('/zh/partners/our-sponsors/')">9.2 {{ tx('贊助商', 'Our Sponsors') }}</a></li>
                  <li><a :href="lp('/zh/partners/become-a-partner/')">9.3 {{ tx('成為夥伴', 'Become a Partner') }}</a></li>
                  <li><a :href="lp('/zh/partners/opportunities/')">9.4 {{ tx('贊助方案', 'Sponsorship Opportunities') }}</a></li>
                </ul>
                <div class="mega__feature">
                  <img v-if="isTcrfc" :src="siteImg('/assets/img/nav-partners.jpg')" alt="" width="440" height="280" loading="lazy">
                  <a class="btn btn--primary btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽談贊助', 'Sponsorship Enquiries') }}</a>
                </div>
              </div>
            </div>
          </li>
          <li v-if="showCharity"><a :href="lp('/zh/charity/')" data-nav="charity" :aria-current="activeNav === 'charity' ? 'page' : undefined">{{ tx('慈善', 'Charity') }}</a></li>
        </ul>
      </nav>

      <div class="header-actions">
        <button ref="searchBtnEl" class="icon-btn" type="button" :aria-label="tx('搜尋', 'Search')" :aria-expanded="searchOpen ? 'true' : 'false'" aria-controls="header-search" @click="toggleSearch">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" stroke-linecap="round" /></svg>
        </button>
        <a class="icon-btn" :href="lp('/zh/cart/')" :aria-label="cartCount > 0 ? tx(`購物車（${cartCount} 件商品）`, `Cart (${cartCount} ${cartCount === 1 ? 'item' : 'items'})`) : tx('購物車', 'Cart')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 4h2l2.4 10.4a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.55L21 8H6" /><circle cx="10" cy="20" r="1.4" /><circle cx="18" cy="20" r="1.4" /></svg>
          <span v-if="cartCount > 0" class="cart-count" aria-hidden="true">{{ cartCount > 99 ? '99+' : cartCount }}</span>
        </a>
        <a class="btn btn--primary btn--sm" :href="lp('/zh/join/')">{{ tx('加入我們 JOIN', 'Join Us') }}</a>
        <button ref="openBtnEl" class="icon-btn hamburger" type="button" id="menu-open-btn" aria-haspopup="true" aria-controls="mobile-nav" aria-expanded="false" :aria-label="tx('開啟選單', 'Open menu')" @click="openMobileNav">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 6h16M4 12h16M4 18h16" /></svg>
        </button>
      </div>
    </div>
    <div v-if="searchOpen" id="header-search" class="header-search">
      <form class="header-search__form" role="search" :aria-label="tx('全站搜尋', 'Site search')" @submit.prevent="submitSearch" @keydown="onSearchKeydown">
        <label class="visually-hidden" for="header-search-input">{{ tx('搜尋關鍵字', 'Search keywords') }}</label>
        <input id="header-search-input" ref="searchInputEl" v-model="searchQuery" type="search" name="q" maxlength="100" :placeholder="tx('搜尋新聞、常見問題、課程、球員、教練、慈善…', 'Search news, FAQ, programs, players, coaches, charity…')" autocomplete="off" enterkeyhint="search">
        <button type="submit" class="btn btn--primary btn--sm">{{ tx('搜尋', 'Search') }}</button>
        <button type="button" class="btn btn--sm header-search__close" @click="closeSearch">{{ tx('關閉', 'Close') }}</button>
      </form>
    </div>
  </header>

  <div ref="mobileNavEl" class="mobile-nav" id="mobile-nav" role="dialog" aria-modal="true" :aria-label="tx('行動選單', 'Mobile menu')" @click="onMobileNavClick">
    <div class="mobile-nav__top">
      <img :src="assets.headerMark.src" :alt="tx(`${assets.nameZh}隊徽`, `${CLUB_NAME_EN} crest`)" width="33" height="34">
      <button ref="closeBtnEl" class="mobile-nav__close" type="button" id="menu-close-btn" :aria-label="tx('關閉選單', 'Close menu')" @click="closeMobileNav">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18" /></svg>
      </button>
    </div>
    <nav :aria-label="tx('行動主要導覽', 'Mobile main navigation')">
      <ul v-if="useApiMenu">
        <li v-for="item in apiMain" :key="item.id">
          <a v-if="hrefOf(item)" :href="hrefOf(item)!" v-bind="item.isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {}">{{ item.label }}</a>
          <span v-else>{{ item.label }}</span>
        </li>
      </ul>
      <ul v-else>
        <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}{{ tx(' ABOUT', '') }}</a></li>
        <li><a :href="lp('/zh/club/')">{{ tx('俱樂部 CLUB', 'Football Club') }}</a></li>
        <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}{{ tx(` ${identity.academyLabelEn}`, '') }}</a></li>
        <li><a :href="lp('/zh/programs/')">{{ tx('課程 PROGRAMS', 'Programs') }}</a></li>
        <li v-if="showWomens"><a :href="lp('/zh/womens/')">{{ tx('女子足球 WOMEN\'S', "Women's Football") }}</a></li>
        <li><a :href="lp('/zh/schedule/')">{{ tx('賽事 SCHEDULE', 'Schedule') }}</a></li>
        <li><a :href="lp('/zh/news/')">{{ tx('新聞 NEWS', 'News') }}</a></li>
        <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}{{ tx(' CULTURE', '') }}</a></li>
        <li><a :href="lp('/zh/shop/')">{{ tx('官方商店 SHOP', 'Official Store') }}</a></li>
        <li><a :href="lp('/zh/partners/')">{{ tx('夥伴 PARTNERS', 'Partners') }}</a></li>
        <li v-if="showCharity"><a :href="lp('/zh/charity/')">{{ tx('慈善 CHARITY', 'Charity') }}</a></li>
        <li><a :href="lp('/zh/faq/')">{{ tx('常見問題 FAQ', 'FAQ') }}</a></li>
      </ul>
    </nav>
    <div class="mobile-nav__cta">
      <a class="btn btn--primary btn--block" :href="lp('/zh/join/')">{{ tx('加入我們 JOIN', 'Join Us') }}</a>
      <div class="lang-switch" role="group" :aria-label="tx('網站語言切換', 'Site language')" style="color:#fff;justify-content:center;">
        <button type="button" :aria-current="locale === 'zh' ? 'true' : undefined" :style="locale === 'zh' ? 'color:#fff;' : 'color:var(--muted-dark);'" @click="switchTo('zh')">繁中</button>
        <span aria-hidden="true" style="color:rgba(255,255,255,.3);">|</span>
        <button type="button" :aria-current="locale === 'en' ? 'true' : undefined" :style="locale === 'en' ? 'color:#fff;' : 'color:var(--muted-dark);'" @click="switchTo('en')">EN</button>
      </div>
    </div>
  </div>

  <div class="mobile-cta-bar" :aria-label="tx('快速行動', 'Quick actions')">
    <a class="btn btn--primary btn--sm" :href="lp('/zh/join/player/')">{{ tx('加入球隊', 'Join as a Player') }}</a>
    <a class="btn btn--dark btn--sm" :href="lp('/zh/join/general/')">{{ tx('聯絡我們', 'Contact Us') }}</a>
  </div>
</template>

<style>
/* 全站搜尋列（G-02，H 批）：貼在 sticky header 下緣，與 header 同寬同底色。 */
.header-search{ border-top:1px solid var(--rule); background:rgba(255,255,255,.98); padding:.75rem 0; }
.header-search__form{ display:flex; align-items:center; gap:.6rem; width:calc(100% - 3rem); max-width:1200px; margin-inline:auto; min-width:0; }
.header-search__form input[type="search"]{ flex:1 1 auto; min-width:0; min-height:44px; padding:.6rem .9rem; border:1px solid var(--rule); background:#fff; font:inherit; font-size:.92rem; }
.header-search__form .btn{ flex:0 0 auto; min-height:44px; }
.header-search__close{ background:transparent; border:1px solid var(--rule); color:var(--text); }
.mega__sub{ margin:.25rem 0 .5rem 1rem; display:flex; flex-direction:column; gap:.15rem; }
.mega__sub a{ font-size:.85em; font-weight:500; }
@media (max-width:640px){
  .header-search__form{ width:calc(100% - 2rem); flex-wrap:wrap; }
  .header-search__form input[type="search"]{ flex-basis:100%; }
}
</style>
