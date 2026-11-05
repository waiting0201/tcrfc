<script setup lang="ts">
// app/pages/zh/app/index.vue — App 下載頁（`/zh/app/`、`/en/app/`；App 規劃書 §2.3「對官網的依賴」第 2 點）
//
// 「台中足球 Taichung Football」是雙隊共同的 App（App 規劃書 v3.0 摘要第 4 點），所以
// 本頁兩站內容相同、只有配色與站名不同——文案刻意不出現任一俱樂部的名稱（藍鯨站不得出現磐石詞彙，check-club-brand-leak 會擋），沒有單元代號對應（不在 13 個單元之內），`unit: 'G-08'`
// 是站務頁代號（比照隱私權／Cookie 政策的 'G-07'，check-site-units-coverage 的排除清單明文記載）。
// 🔴 商店連結只讀 runtimeConfig（`NUXT_PUBLIC_APP_STORE_URL`／`NUXT_PUBLIC_PLAY_STORE_URL`），**不寫死、不臆造**：
// App 尚未上架，兩者為空時顯示「即將上線」，只有 https 網址才會變成按鈕。功能描述只列 App 規劃書 §2.3
// 對照表已有的畫面（賽程、新聞、球員、特約店家、課程、會員卡、會籍方案），不新增承諾。
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
  { title: '電子會員卡與會籍', desc: '會員卡隨身帶著，也可在 App 內查看會籍方案。' },
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

<section id="delete-account" class="band app-delete" aria-labelledby="app-delete-title">
  <div class="band-inner container">
    <h2 id="app-delete-title" class="section-title">{{ tx('刪除帳號', 'Delete account') }}</h2>
    <p class="app-note app-note--lead">{{ tx('本節說明如何刪除帳號，不需登入即可閱讀；此頁不提供刪除操作。', 'This section explains how to delete your account. You can read it without signing in; no deletion can be made on this page.') }}</p>

    <h3 class="app-delete__h">{{ tx('在 App 裡刪除', 'Delete in the app') }}</h3>
    <p class="app-delete__p">{{ tx('在 App 內前往「設定 → 刪除帳號」，二次確認後即可完成。', 'In the app, go to Settings → Delete account. The deletion is completed after a two-step confirmation.') }}</p>

    <h3 class="app-delete__h">{{ tx('在官網會員中心刪除', 'Delete in the website member centre') }}</h3>
    <p class="app-delete__p">{{ tx('登入會員中心 → 個人資料與安全 → 刪除帳號，輸入密碼確認。以 LINE 註冊、尚未設定密碼者，請輸入 DELETE 確認。', 'Sign in to the member centre → Profile and security → Delete account, and confirm with your password. If you registered through LINE and have not set a password, type DELETE to confirm.') }}</p>

    <h3 class="app-delete__h">{{ tx('刪除後的資料', 'Data after deletion') }}</h3>
    <ul class="app-delete__list">
      <li><strong>{{ tx('清除：', 'Erased: ') }}</strong>{{ tx('姓名、Email、電話、生日、LINE 綁定與推播綁定；所有電子會員卡立即作廢；未完成的會籍申請取消；刪除後無法復原。', 'name, email, phone number, date of birth, LINE link and push-notification binding. All digital membership cards are voided immediately, unfinished membership requests are cancelled, and deletion cannot be undone.') }}</li>
      <li><strong>{{ tx('保留：', 'Retained: ') }}</strong>{{ tx('已鎖定的抽獎名單快照僅留會員編號與遮罩姓名；已成立的會籍、付款與訂單紀錄依法保存（不含可識別身分的個資）。保存年限依法規辦理。', 'a locked prize-draw snapshot keeps only the member number and masked name. Membership, payment and order records already created are kept as required by law (without personally identifying data). The retention period follows the law.') }}</li>
    </ul>

    <h3 class="app-delete__h">{{ tx('無法登入時', 'If you cannot sign in') }}</h3>
    <p class="app-delete__p"><template v-if="isEn">If you cannot sign in to the app or the website, please use the general form on the <a :href="lp('/zh/join/contact/')">Contact us</a> page to request deletion, and state the email address you registered with. Contact details also appear in the website footer.</template><template v-else>無法登入 App 或官網的人，請使用<a :href="lp('/zh/join/contact/')">聯絡我們</a>的一般聯絡表單提出刪除帳號的申請，並註明註冊時的 Email。官網頁尾亦有聯絡資訊。</template></p>
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
.app-delete{ scroll-margin-top:6rem; }
.app-note--lead{ margin-top:.5rem; }
.app-delete__h{ margin-top:2rem; font-size:1.05rem; font-weight:800; color:var(--heading); }
.app-delete__p, .app-delete__list{ margin-top:.5rem; font-size:.92rem; line-height:1.85; max-width:46rem; text-wrap:pretty; }
.app-delete__list{ padding-left:1.25rem; }
.app-delete__list li + li{ margin-top:.5rem; }
.app-delete__p a{ color:var(--brand-aa); text-decoration:underline; }
</style>
