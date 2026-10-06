<script setup lang="ts">
// app/pages/zh/join/contact/index.vue — 由 site/src/pages/zh/join/contact/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
//
// S1-17 收尾修正（2026-09-29）：電話／營業時間改綁 useSiteFacts('tcrfc').contact.phone／
// .hours（PublicSiteFactsDto.contact 既有欄位，S1-17 主輪交付時漏綁，見 apps/web/README.md
// 「S1-17」節「規格疑點」第 5 點）。兩俱樂部這兩個值目前在後台都還是 null，綁定後畫面仍只顯示
// 標籤、不顯示值——這是資料現況，不是本次修正的缺陷。
definePageMeta({ nav: '', unit: '10-contact', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()

// 文案依俱樂部切換：hero／SEO 與社群連結取自 club-copy.ts。藍鯨無實體地址、
// 電話與各部門分機（舊站盤點：content/blue-whale/gap-analysis.md §2 單元 10，
// 「沒有任何實體地址、電話或聯絡表單」），這幾格本站一律不顯示。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
// C-2：社群／Email 以後台值優先，未維護時沿用 club-copy 過渡值（useClubIdentity）。
const identity = useClubIdentity()
const hero = computed(() => (isEn.value ? (isTcrfc.value ? JOIN_CONTACT_HERO_EN : JOIN_CONTACT_HERO_EN_BW) : JOIN_CONTACT_HERO[clubKey.value]))
const seoEn = computed(() => (isTcrfc.value ? JOIN_CONTACT_SEO_EN : JOIN_CONTACT_SEO_EN_BW))
// S1-13 缺口①：hero.lede（兩個俱樂部版本皆有）含內嵌連結標記，v-html 渲染前
// 用 localizeHtmlLinks() 把裡面的 /zh/join/ 換成目前語系版本。
const ledeHtml = computed(() => localizeHtmlLinks(hero.value.lede, locale.value))
// S1-12d 收尾：地址／主場名稱改讀 useSiteFacts（後端公開端點）。
// 🔴 F2（2026-10-03）：原本固定 useSiteFacts('tcrfc')，藍鯨站也會抓磐石事實並整份序列化進
// SSR payload（磐石的場地、地址與梯隊代碼外洩，頁面可見文字雖不顯示）。
// 改依當前站台取事實；地址區塊仍只在 isTcrfc 時渲染。
const { facts: tcrfcFacts, primaryVenue: tcrfcVenue } = useSiteFacts(clubKey.value)

const departments = computed(() => tcrfcFacts.value.contact.departments ?? [])

useSeoMeta({
  title: computed(() => (isEn.value ? seoEn.value : JOIN_CONTACT_SEO[clubKey.value]).title),
  description: computed(() => (isEn.value ? seoEn.value : JOIN_CONTACT_SEO[clubKey.value]).description),
})

/** 從社群網址推導顯示用帳號（沿用 mockup 既有的 @handle 呈現方式，不新增資料欄位）。 */
function socialHandle(url: string): string {
  const last = url.replace(/\/$/, '').split('/').pop() ?? ''
  return last.startsWith('@') ? last : `@${last}`
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/join/')">{{ tx('加入與聯絡', 'Join / Contact') }}</a></li>
      <li aria-current="page">{{ tx('聯絡資訊', 'Contact Information') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="container">
    <p class="page-hero__eyebrow">Contact Information</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede" v-html="ledeHtml"></p>
  </div>
</section>

<section class="band" aria-labelledby="contact-title">
  <div class="container">
    <h2 class="visually-hidden" id="contact-title">{{ tx('聯絡資訊列表', 'Contact information list') }}</h2>
    <div class="contact-grid">
      <!-- S1-17 收尾修正（2026-09-29）：電話改讀 useSiteFacts('tcrfc').contact.phone
           （PublicSiteFactsDto.contact 既有欄位，先前這裡從未綁定，見 apps/web/README.md
           「S1-17」節「規格疑點」）。目前後台兩俱樂部皆未填值，phone 為 null 時不顯示
           假資料，只顯示標籤。 -->
      <!-- C-3：電話／地址／營業時間「API 有值就顯示」（兩隊都適用），沒值才隱藏整塊。 -->
      <div v-if="tcrfcFacts.contact.phone" class="contact-item">
        <p class="contact-item__label">{{ tx('電話', 'Phone') }}</p>
        <p class="contact-item__value">{{ tcrfcFacts.contact.phone }}</p>
      </div>

      <div v-if="isTcrfc || identity.social.email" class="contact-item">
        <p class="contact-item__label">Email</p>
        <p v-if="identity.social.email" class="contact-item__value">{{ identity.social.email }}</p>
      </div>

      <!-- GEO-03（S1-12d）：地址／主場名稱為單一來源（useSiteFacts 讀後端 API），不在此
           重複寫死字面值。 -->
      <div v-if="tcrfcFacts.contact.address" class="contact-item">
        <p class="contact-item__label">{{ tx('地址', 'Address') }}</p>
        <p class="contact-item__value">{{ tcrfcFacts.contact.address }}</p>
        <p v-if="isTcrfc" class="field-hint"><template v-if="isEn">Home ground: {{ tcrfcVenue.nameEn ?? tcrfcVenue.nameZh }}. For the exact location of each venue, see <a :href="lp('/zh/join/location/')">Location &amp; Map</a>.</template><template v-else>主場：{{ tcrfcVenue.nameZh }}。各場地詳細位置見<a :href="lp('/zh/join/location/')">場地位置與地圖</a>。</template></p>
      </div>

      <!-- S1-17 收尾修正（2026-09-29）：營業時間改讀 useSiteFacts('tcrfc').contact.hours，
           同上，hours 為 null 時只顯示標籤。 -->
      <div v-if="tcrfcFacts.contact.hours" class="contact-item">
        <p class="contact-item__label">{{ tx('營業時間', 'Opening hours') }}</p>
        <p class="contact-item__value">{{ isEn ? (tcrfcFacts.contact.hoursEn || tcrfcFacts.contact.hours) : tcrfcFacts.contact.hours }}</p>
      </div>

      <!-- C-2：各部門窗口（後台「部門窗口」）。有資料才列清單；磐石維持原有標籤列。 -->
      <div v-if="isTcrfc || departments.length" class="contact-item contact-item--full">
        <p class="contact-item__label">{{ tx(isTcrfc ? '各部門分機' : '各部門窗口', isTcrfc ? 'Department extensions' : 'Departments') }}</p>
        <ul v-if="departments.length" class="contact-depts">
          <li v-for="d in departments" :key="d.nameZh">
            <span class="contact-depts__name">{{ isEn ? (d.nameEn ?? d.nameZh) : d.nameZh }}</span>
            <a v-if="d.email" :href="`mailto:${d.email}`">{{ d.email }}</a>
            <span v-if="d.phoneExtension" class="contact-depts__ext">{{ tx('分機', 'Ext.') }} {{ d.phoneExtension }}</span>
          </li>
        </ul>
      </div>

      <div class="contact-item contact-item--full">
        <p class="contact-item__label">{{ tx('社群連結', 'Social media') }}</p>
        <ul class="contact-social">
          <li v-if="identity.social.facebook">
            <a :href="identity.social.facebook" target="_blank" rel="noopener">
              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z"/></svg>
              Facebook<span class="contact-social__handle">{{ socialHandle(identity.social.facebook) }}</span>
            </a>
          </li>
          <li v-if="identity.social.instagram">
            <a :href="identity.social.instagram" target="_blank" rel="noopener">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5"/><circle cx="12" cy="12" r="4"/><circle cx="17.2" cy="6.8" r="1"/></svg>
              Instagram<span class="contact-social__handle">{{ socialHandle(identity.social.instagram) }}</span>
            </a>
          </li>
          <li v-if="identity.social.youtube">
            <a :href="identity.social.youtube" target="_blank" rel="noopener">
              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8"/><path d="M10 9.5l6 2.5-6 2.5z"/></svg>
              YouTube<span class="contact-social__handle">{{ socialHandle(identity.social.youtube) }}</span>
            </a>
          </li>
          <li v-if="identity.social.line">
            <a :href="identity.social.line" target="_blank" rel="noopener">
              {{ tx('LINE 官方帳號', 'LINE Official Account') }}
            </a>
          </li>
        </ul>
      </div>
    </div>
  </div>
</section>

<section class="band grain" aria-labelledby="cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="band-inner container" style="text-align:center">
    <p class="kicker kicker--on-dark" style="justify-content:center">{{ tx('找不到你要的資訊？', 'Can\'t find what you are looking for?') }}</p>
    <h2 class="section-title" id="cta-title" style="color:#fff">{{ tx('直接透過表單聯絡我們', 'Contact us directly through a form') }}</h2>
    <p class="section-lede on-dark" style="margin-inline:auto">{{ tx('七種表單各自送達對應部門，會比一般聯絡信箱更快得到回覆。', 'Each of the seven forms goes straight to the relevant department, so you will get a faster reply than through the general contact email.') }}</p>
    <div style="margin-top:2rem">
      <a class="btn btn--primary" :href="lp('/zh/join/')">{{ tx('查看所有表單', 'View all forms') }}</a>
    </div>
  </div>
</section>
</template>

<style>
/* 僅本頁使用：聯絡資訊卡片與社群列表 */
.contact-grid{ display:grid; grid-template-columns:repeat(2,1fr); gap:clamp(1rem,2.5vw,1.75rem); }
@media (max-width:700px){ .contact-grid{ grid-template-columns:1fr; } }
.contact-item{ padding:1.75rem clamp(1.25rem,3vw,2rem); background:var(--paper-2); border:1px solid var(--rule); }
.contact-item--full{ grid-column:1 / -1; }
.contact-item__label{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.75rem; }
.contact-item__value{ font-size:1.05rem; font-weight:700; color:var(--heading); line-height:1.6; }
.contact-item .field-hint{ margin-top:.6rem; }
.contact-item .field-hint a{ color:var(--brand-aa); text-decoration:underline; }
.contact-item .pending{ font-size:.85rem; }
.contact-item .pending a{ color:var(--brand-deep); text-decoration:underline; }

.contact-depts{ display:grid; gap:.5rem; margin:.4rem 0 0; padding:0; list-style:none; }
.contact-depts li{ display:flex; flex-wrap:wrap; gap:.25rem 1rem; align-items:baseline; }
.contact-depts__name{ font-weight:700; }
.contact-depts__ext{ color:var(--muted); }
.contact-social{ display:flex; flex-wrap:wrap; gap:1rem; }
.contact-social li{ flex:1 1 200px; }
.contact-social a{
  display:flex; align-items:center; gap:.75rem; padding:1rem 1.25rem;
  background:var(--paper); border:1px solid var(--rule); font-weight:700; color:var(--heading);
  transition:border-color var(--dur-fast) var(--ease);
}
.contact-social a:hover{ border-color:var(--brand-aa); }
.contact-social svg{ width:22px; height:22px; flex:none; color:var(--brand-aa); }
.contact-social__handle{ display:block; font-size:.78rem; font-weight:500; color:var(--muted); }
</style>
