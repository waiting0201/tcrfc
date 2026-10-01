<script setup lang="ts">
import type { PublicPartner } from '#shared/utils/partners'
// app/components/SiteFooter.vue — 由 site/src/partials/footer.html 轉來（DOM／class 不動）
//
// 文案依俱樂部切換（docs/13-blue-whale-site.md §6 紀律 11）：品牌欄一句話介紹、
// 社群連結、版權列都是「俱樂部自己的事實」，一律從 shared/utils/club-copy.ts 取值。
//
// 🔵 S1-12e（GEO-07 標題結構）：頁尾四個標題維持原本的 h4 標籤不動（tag 名稱、class、
// `.footer-col h4` 的 CSS 選擇器都不能改，見上一段「DOM／class 不動」），但頁尾出現在
// 每一頁的最後面，而多數頁面走到頁尾前最後一個標題只到 h2（例如實測首頁 `cta-title`
// 之後直接接頁尾），視覺標題層級與可存取性標題大綱因此對不上——這正是 h4 相對於 h2
// 跳兩級，違反 GEO-07「H2/H3 不跳階」。加上 `aria-level="2"` 是 WAI-ARIA 允許的既有
// 技巧：在原生 h4 元素上覆寫輔助技術讀到的標題層級，不改變元素本身的 tag／class／
// 視覺樣式，`.footer-col h4` 選擇器與既有 DOM 比對機制完全不受影響，只有螢幕閱讀器與
// 遵循 ARIA 的爬蟲看到的「大綱層級」改變。見 scripts/check-heading-structure.mjs
// 檔頭說明與 apps/web/README.md「S1-12e」節。
const config = useRuntimeConfig()
const club = computed(() => config.public.club)
const assets = computed(() => getClubAssets(club.value))
const identity = computed(() => getClubIdentity(club.value))
const showWomens = computed(() => isUnitEnabledForClub('06', club.value))
const showCharity = computed(() => isUnitEnabledForClub('11', club.value))

// 頁尾導覽連結（本檔 19 處 href）一律用 lp() 換算成目前語系版本（S1-13，
// shared/utils/locale.ts 單一真實來源）；語系切換器本身另外用 switchTo()。
const { locale, lp, switchTo } = useLocale()

// 贊助夥伴 Logo（主站規劃書 §2 全域導覽「Footer：…贊助夥伴 Logo 輪播…」，S2-7 接上）：
// `GET /api/backend/{club}/partners?footer=true`（後台 E1 勾選「頁尾曝光」；只回合作期間涵蓋今天者，各俱樂部
// 只讀自己的夥伴）。🔴 刻意**不 await**：頁尾在每一頁，await 會把元件變成非同步元件（需要 Suspense）、
// 且 API 慢時拖住整頁；useFetch 不 await 時 SSR 仍會等資料（onServerPrefetch），client 端導覽時才非同步填入。
// 沒有任何頁尾夥伴（含 API 打不到）整段不輸出，不留空框。頁尾是深色底，Logo 優先用「深底版」。
// 「輪播」目前不做動態輪播（最多靜態列出 8 家），理由見 README「S2-7」節。
const { data: footerPartnerData } = useFetch<PublicPartner[]>(() => `/api/backend/${club.value}/partners`, {
  query: computed(() => ({ lang: locale.value, footer: true })),
  key: `partners-footer-${config.public.club}-${locale.value}`,
})
const footerPartners = computed(() => (footerPartnerData.value ?? []).slice(0, 8))
</script>

<template>
  <footer class="site-footer">
    <div class="container">
      <div class="footer-top">
        <div class="footer-brand">
          <img class="footer-brand__logo" :src="assets.footerMark.src" :alt="assets.nameZh" :width="assets.footerMark.width" :height="assets.footerMark.height">
          <p>{{ identity.footerBlurb }}</p>
          <nav class="footer-social" aria-label="社群媒體">
            <a v-if="identity.social.facebook" :href="identity.social.facebook" aria-label="前往 Facebook 粉絲專頁" target="_blank" rel="noopener"><svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z" /></svg></a>
            <a v-if="identity.social.instagram" :href="identity.social.instagram" aria-label="前往 Instagram" target="_blank" rel="noopener"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.2" cy="6.8" r="1" /></svg></a>
            <a v-if="identity.social.youtube" :href="identity.social.youtube" aria-label="前往 YouTube 頻道" target="_blank" rel="noopener"><svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8" /><path d="M10 9.5l6 2.5-6 2.5z" /></svg></a>
          </nav>
        </div>

        <div class="footer-col">
          <h4 aria-level="2">俱樂部</h4>
          <ul>
            <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
            <li><a :href="lp('/zh/club/first-team/')">一線隊</a></li>
            <li><a :href="lp('/zh/schedule/')">賽事行事曆</a></li>
            <li><a :href="lp('/zh/news/')">最新消息</a></li>
            <li><a :href="lp('/zh/partners/')">合作夥伴與贊助</a></li>
          </ul>
        </div>
        <div class="footer-col">
          <h4 aria-level="2">青訓與課程</h4>
          <ul>
            <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
            <li><a :href="lp('/zh/programs/')">課程與活動</a></li>
            <li v-if="showWomens"><a :href="lp('/zh/womens/')">女子足球</a></li>
            <li><a :href="lp('/zh/join/player/')">加入球隊</a></li>
            <li><a :href="lp('/zh/academy/join/')">加入{{ identity.academyShortLabelZh }}</a></li>
          </ul>
        </div>
        <div class="footer-col">
          <h4 aria-level="2">參與{{ assets.shortNameZh }}</h4>
          <ul>
            <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
            <li><a :href="lp('/zh/shop/')">官方商店</a></li>
            <li><a :href="lp('/zh/order/lookup/')">訂單查詢</a></li>
            <li v-if="showCharity"><a :href="lp('/zh/charity/')">慈善與社會影響</a></li>
            <li><a :href="lp('/zh/faq/')">常見問題 FAQ</a></li>
            <li><a :href="lp('/zh/join/')">加入與聯絡</a></li>
            <li><a :href="lp('/zh/join/location/')">場地位置與地圖</a></li>
          </ul>
        </div>
        <div class="footer-col newsletter">
          <h4 aria-level="2">訂閱電子報</h4>
          <p>第一時間收到{{ assets.shortNameZh }}賽事戰報與活動資訊。</p>
          <form @submit.prevent>
            <label class="visually-hidden" for="newsletter-email">電子郵件地址</label>
            <input type="email" id="newsletter-email" placeholder="輸入您的 Email" autocomplete="email" required>
            <button type="submit">訂閱</button>
          </form>
        </div>
      </div>

      <nav v-if="footerPartners.length" class="footer-partners" aria-label="合作夥伴">
        <p class="footer-partners__label">合作夥伴</p>
        <ul class="footer-partners__list">
          <li v-for="p in footerPartners" :key="p.id">
            <a v-if="safeExternalUrl(p.websiteUrl)" :href="safeExternalUrl(p.websiteUrl) ?? undefined" target="_blank" rel="noopener noreferrer" :aria-label="`${p.name}（另開新分頁）`">
              <img v-if="p.logoDarkUrl ?? p.logoLightUrl" :src="p.logoDarkUrl ?? p.logoLightUrl ?? undefined" alt="" loading="lazy" width="120" height="48">
              <span v-else>{{ p.name }}</span>
            </a>
            <template v-else>
              <img v-if="p.logoDarkUrl ?? p.logoLightUrl" :src="p.logoDarkUrl ?? p.logoLightUrl ?? undefined" :alt="p.name ?? ''" loading="lazy" width="120" height="48">
              <span v-else>{{ p.name }}</span>
            </template>
          </li>
        </ul>
      </nav>

      <div class="footer-bottom">
        <p>{{ identity.copyrightZh }}</p>
        <div class="legal-links">
          <a :href="lp('/zh/privacy/')">隱私權政策</a>
          <a :href="lp('/zh/cookies/')">Cookie 政策</a>
          <div class="footer-lang lang-switch" role="group" aria-label="網站語言切換">
            <button type="button" :aria-current="locale === 'zh' ? 'true' : undefined" @click="switchTo('zh')">繁中</button><span aria-hidden="true">|</span><button type="button" :aria-current="locale === 'en' ? 'true' : undefined" @click="switchTo('en')">EN</button>
          </div>
        </div>
      </div>
    </div>
  </footer>
</template>

<style>
/* 頁尾贊助夥伴 Logo 列（S2-7）：深色底、單排可換行，Logo 一律縮到同高 */
.footer-partners{ display:flex; flex-wrap:wrap; align-items:center; gap:1rem 2rem; padding:1.75rem 0; border-bottom:1px solid rgba(255,255,255,.08); }
.footer-partners__label{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:#fff; }
.footer-partners__list{ display:flex; flex-wrap:wrap; align-items:center; gap:1rem 1.75rem; list-style:none; margin:0; padding:0; }
.footer-partners__list img{ display:block; height:36px; width:auto; max-width:140px; object-fit:contain; opacity:.8; transition:opacity var(--dur-fast) var(--ease); }
.footer-partners__list a:hover img{ opacity:1; }
.footer-partners__list span{ font-size:.85rem; font-weight:700; color:var(--muted-dark); }
</style>
