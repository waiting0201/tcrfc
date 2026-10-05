<script setup lang="ts">
// app/pages/zh/app/index.vue — App 下載頁（`/zh/app/`、`/en/app/`；App 規劃書 §2.3「對官網的依賴」第 2 點）
//
// 「台中足球 Taichung Football」是雙隊共同的 App（App 規劃書 v3.0 摘要第 4 點），所以
// 本頁兩站內容相同、只有配色與站名不同——文案刻意不出現任一俱樂部的名稱（藍鯨站不得出現磐石詞彙，check-club-brand-leak 會擋），沒有單元代號對應（不在 13 個單元之內），`unit: 'G-08'`
// 是站務頁代號（比照隱私權／Cookie 政策的 'G-07'，check-site-units-coverage 的排除清單明文記載）。
// 🔴 商店連結只讀 runtimeConfig（`NUXT_PUBLIC_APP_STORE_URL`／`NUXT_PUBLIC_PLAY_STORE_URL`），**不寫死、不臆造**：
// App 尚未上架，兩者為空時顯示「即將上線」，只有 https 網址才會變成按鈕。功能描述只列 App 規劃書 §2.3
// 對照表已有的畫面（賽程、新聞、球員、特約店家、課程、會員卡、會籍升級），不新增承諾。
// 頁面 SEO 適用主站規範（App 規劃書 §1.3「SEO／GEO」列）：有 title／description，canonical 由全站機制產生。
definePageMeta({ nav: '', unit: 'G-08', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))

const httpsOnly = (u: unknown) => (typeof u === 'string' && /^https:\/\//i.test(u) ? u : null)
const appStoreUrl = computed(() => httpsOnly(config.public.appStoreUrl))
const playStoreUrl = computed(() => httpsOnly(config.public.playStoreUrl))
const available = computed(() => Boolean(appStoreUrl.value || playStoreUrl.value))

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('app', clubNameEn.value).title : `下載 App｜台中足球 Taichung Football｜${clubAssets.value.shortNameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('app', clubNameEn.value).description : '台中足球 App：兩支球隊共同的官方 App，賽程、新聞、球員、特約店家、課程與電子會員卡，一個 App 全部帶著走。')),
})

const FEATURES: ReadonlyArray<{ title: string, desc: string }> = [
  { title: '賽程與賽事', desc: '兩隊的賽程集中查看，點進單場賽事看詳情。' },
  { title: '新聞與球員', desc: '最新消息與球員資料，從官網同步。' },
  { title: '特約店家', desc: '瀏覽合作店家與優惠內容。' },
  { title: '課程', desc: '查看課程資訊與報名。' },
  { title: '電子會員卡與會籍', desc: '會員卡隨身帶著，會籍升級也在 App 內申請。' },
]
const features = computed(() => (isEn.value ? SHOP_APP_FEATURES_EN : FEATURES))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('下載 App', 'Download the app') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Mobile App</p>
    <h1>{{ isEn ? 'Taichung Football app' : '台中足球 App' }}<span v-if="!isEn" class="en">Taichung Football</span></h1>
    <p class="page-hero__lede">{{ tx('兩支球隊共同的官方 App。已安裝 App 的裝置，點開官網上的賽程、新聞、球員等連結會直接開啟 App。', 'The official app shared by both teams. On a device with the app installed, links to fixtures, news, players and more on the website open the app directly.') }}</p>
  </div>
</section>

<section class="band" aria-labelledby="app-download-title">
  <div class="band-inner container">
    <h2 id="app-download-title" class="section-title">{{ tx('下載', 'Download') }}</h2>
    <p v-if="available" class="app-stores">
      <a v-if="appStoreUrl" class="btn btn--primary" :href="appStoreUrl" target="_blank" rel="noopener noreferrer">App Store<span class="visually-hidden">{{ tx('（另開新視窗）', ' (opens in a new window)') }}</span></a>
      <a v-if="playStoreUrl" class="btn btn--dark" :href="playStoreUrl" target="_blank" rel="noopener noreferrer">Google Play<span class="visually-hidden">{{ tx('（另開新視窗）', ' (opens in a new window)') }}</span></a>
    </p>
    <p v-else class="app-coming-soon" role="status">{{ tx('App 即將上線，上架後這裡會提供 App Store 與 Google Play 的下載連結。', 'The app is coming soon. Once it is published, download links for the App Store and Google Play will appear here.') }}</p>

    <h2 class="section-title" style="margin-top:3.5rem;">{{ tx('App 裡有什麼', 'What is in the app') }}</h2>
    <div class="app-features">
      <div v-for="f in features" :key="f.title" class="feature-card">
        <p class="feature-card__title">{{ f.title }}</p>
        <p class="feature-card__desc">{{ f.desc }}</p>
      </div>
    </div>
    <p class="app-note"><template v-if="isEn">You do not need the app to follow along: everything can be browsed on the website. The <a :href="lp('/zh/schedule/')">schedule</a>, <a :href="lp('/zh/news/')">latest news</a> and <a :href="lp('/zh/club/first-team/')">First Team</a> pages match what is in the app.</template><template v-else>還沒安裝也沒關係：所有內容都能在官網瀏覽，<a :href="lp('/zh/schedule/')">賽事行事曆</a>、<a :href="lp('/zh/news/')">最新消息</a>與<a :href="lp('/zh/club/first-team/')">一線隊</a>與 App 內容一致。</template></p>
  </div>
</section>
</template>

<style>
.app-stores{ display:flex; flex-wrap:wrap; gap:1rem; margin-top:1.25rem; }
.app-coming-soon{ margin-top:1.25rem; padding:1.1rem 1.4rem; border:1px dashed var(--rule); background:var(--paper); color:var(--text); }
.app-features{ display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:1.25rem; margin-top:1.5rem; }
.app-features .feature-card{
  background:var(--paper); border:1px solid var(--rule); border-top:3px solid var(--brand);
  padding:1.4rem 1.5rem 1.5rem;
}
.app-features .feature-card__title{ font-weight:800; color:var(--heading); margin-bottom:.5rem; font-size:.98rem; }
.app-features .feature-card__desc{ font-size:.85rem; line-height:1.7; color:var(--text); text-wrap:pretty; }
.app-note{ margin-top:2rem; font-size:.9rem; line-height:1.8; }
.app-note a{ color:var(--brand-aa); text-decoration:underline; }
</style>
