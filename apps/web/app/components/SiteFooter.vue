<script setup lang="ts">
import type { PublicPartner } from '#shared/utils/partners'
import { menuItemHref, type PublicMenuItem } from '#shared/utils/site-settings'
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
// 藍鯨英文版（B-5，2026-10-05）：英文識別依俱樂部取，藍鯨英文不得出現磐石字樣（Academy／TCRFC／Taichung Rock）。
// C-2：社群／Email／頁尾簡介以後台值優先（useClubIdentity），後台未維護時沿用 club-copy 過渡值。
const identity = useClubIdentity()
const clubNameEn = computed(() => (club.value === 'bw' ? BW_NAME_EN : CLUB_NAME_EN))
const acaEn = computed(() => (club.value === 'bw' ? 'Youth' : 'Academy'))
const clubShortName = computed(() => (isEn.value ? clubNameEn.value : assets.value.shortNameZh))
const showWomens = computed(() => isUnitEnabledForClub('06', club.value))
const showCharity = computed(() => isUnitEnabledForClub('11', club.value))

// 頁尾導覽連結（本檔 19 處 href）一律用 lp() 換算成目前語系版本（S1-13，
// shared/utils/locale.ts 單一真實來源）；語系切換器本身另外用 switchTo()。
const { locale, lp, switchTo, isEn, tx } = useLocale()

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

// I2 選單管理（H 批）：後台設定了頁尾選單就改讀 API，沒有時沿用下方寫死的三欄連結（過渡策略，見 useSiteMenus.ts）。
// 每個有子項目的頂層項目是一欄（標題＋連結）；沒有子項目的頂層項目集中放進最後一欄「更多連結」。
const { footer: apiFooter } = useSiteMenus()
const useApiFooter = computed(() => apiFooter.value.length > 0)
interface FooterColumn { id: string, title: string, links: PublicMenuItem[] }
const footerColumns = computed<FooterColumn[]>(() => {
  const columns: FooterColumn[] = apiFooter.value
    .filter((i) => i.children.length > 0)
    .map((i) => ({ id: i.id, title: i.label, links: i.children }))
  const loose = apiFooter.value.filter((i) => i.children.length === 0)
  if (loose.length > 0) columns.push({ id: 'loose', title: tx('更多連結', 'More links'), links: loose })
  return columns
})
function hrefOf(item: PublicMenuItem): string | null {
  return menuItemHref(item, lp)
}

// G-09 電子報訂閱（H 批）：POST newsletter/subscribe。單一確認（勾選同意即訂閱）、不寄確認信；
// 曾退訂的信箱再送出不會改回訂閱，且回應不透露名單狀態，所以成功文案刻意中性（「已收到」而非「訂閱完成」）。
// 介面字串（`newsletter.*`）是 I4 字串翻譯表的示範接入點：後台建立同代號字串即覆寫，沒建立就用原本的文字。
const { t } = useUiStrings()
const nlEmail = ref('')
const nlConsent = ref(false)
const nlWebsite = ref('')
const nlStatus = ref<'idle' | 'submitting' | 'success' | 'error'>('idle')
const nlMessage = ref('')
async function onSubscribe() {
  if (nlStatus.value === 'submitting') return
  nlMessage.value = ''
  if (!nlConsent.value) {
    nlStatus.value = 'error'
    nlMessage.value = t('newsletter.consent_required', tx('請先勾選同意，才能訂閱電子報。', 'Please tick the consent box to subscribe to the newsletter.'))
    return
  }
  // 誘捕欄位有值＝機器人：安靜當作成功，不送出（端點本身另有依 IP 的限流）。
  if (nlWebsite.value) {
    nlStatus.value = 'success'
    nlMessage.value = t('newsletter.success', tx('已收到您的訂閱申請，感謝您！', 'We have received your subscription request. Thank you!'))
    return
  }
  nlStatus.value = 'submitting'
  try {
    await $fetch(`/api/backend/${club.value}/newsletter/subscribe`, {
      method: 'POST',
      body: { email: nlEmail.value.trim(), consent: true, source: 'footer', website: nlWebsite.value || undefined },
    })
    nlStatus.value = 'success'
    nlMessage.value = t('newsletter.success', tx('已收到您的訂閱申請，感謝您！', 'We have received your subscription request. Thank you!'))
    nlEmail.value = ''
    nlConsent.value = false
  }
  catch (err: unknown) {
    nlStatus.value = 'error'
    const status = (err as { statusCode?: number, status?: number } | null)?.statusCode ?? (err as { status?: number } | null)?.status
    nlMessage.value = extractErrorMessage(err, isEn.value)
      ?? (status === 429 ? tx('送出次數過多，請稍候幾分鐘再試。', 'Too many attempts. Please wait a few minutes and try again.') : t('newsletter.error', tx('訂閱失敗，請確認 Email 格式後再試一次。', 'Subscription failed. Please check your email address and try again.')))
  }
}
</script>

<template>
  <footer class="site-footer">
    <div class="container">
      <div class="footer-top">
        <div class="footer-brand">
          <img class="footer-brand__logo" :src="assets.footerMark.src" :alt="isEn ? clubNameEn : assets.nameZh" :width="assets.footerMark.width" :height="assets.footerMark.height">
          <p>{{ identity.footerBlurb }}</p>
          <nav class="footer-social" :aria-label="tx('社群媒體', 'Social media')">
            <a v-if="identity.social.facebook" :href="identity.social.facebook" :aria-label="tx('前往 Facebook 粉絲專頁', 'Visit our Facebook page')" target="_blank" rel="noopener"><svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z" /></svg></a>
            <a v-if="identity.social.instagram" :href="identity.social.instagram" :aria-label="tx('前往 Instagram', 'Visit our Instagram')" target="_blank" rel="noopener"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5" /><circle cx="12" cy="12" r="4" /><circle cx="17.2" cy="6.8" r="1" /></svg></a>
            <a v-if="identity.social.youtube" :href="identity.social.youtube" :aria-label="tx('前往 YouTube 頻道', 'Visit our YouTube channel')" target="_blank" rel="noopener"><svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8" /><path d="M10 9.5l6 2.5-6 2.5z" /></svg></a>
          </nav>
        </div>

        <template v-if="useApiFooter">
          <div v-for="col in footerColumns" :key="col.id" class="footer-col">
            <h4 aria-level="2">{{ col.title }}</h4>
            <ul>
              <li v-for="link in col.links" :key="link.id">
                <a v-if="hrefOf(link)" :href="hrefOf(link)!" v-bind="link.isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {}">{{ link.label }}</a>
                <span v-else>{{ link.label }}</span>
              </li>
            </ul>
          </div>
        </template>
        <template v-else>
        <div class="footer-col">
          <h4 aria-level="2">{{ tx('俱樂部', 'Club') }}</h4>
          <ul>
            <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
            <li><a :href="lp('/zh/club/first-team/')">{{ tx('一線隊', 'First Team') }}</a></li>
            <li><a :href="lp('/zh/schedule/')">{{ tx('賽事行事曆', 'Schedule') }}</a></li>
            <li><a :href="lp('/zh/news/')">{{ tx('最新消息', 'Latest news') }}</a></li>
            <li><a :href="lp('/zh/partners/')">{{ tx('合作夥伴與贊助', 'Partners & Sponsors') }}</a></li>
          </ul>
        </div>
        <div class="footer-col">
          <h4 aria-level="2">{{ tx('青訓與課程', `${acaEn} & Programs`) }}</h4>
          <ul>
            <li><a :href="lp('/zh/academy/')">{{ identity.academyLabelZh }}</a></li>
            <li><a :href="lp('/zh/programs/')">{{ tx('課程與活動', 'Programs') }}</a></li>
            <li v-if="showWomens"><a :href="lp('/zh/womens/')">{{ tx('女子足球', "Women's Football") }}</a></li>
            <li><a :href="lp('/zh/join/player/')">{{ tx('加入球隊', 'Join as a Player') }}</a></li>
            <li><a :href="lp('/zh/academy/join/')">{{ isEn ? `Join the ${club === 'bw' ? 'Youth Teams' : 'Academy'}` : '加入' + identity.academyShortLabelZh }}</a></li>
          </ul>
        </div>
        <div class="footer-col">
          <h4 aria-level="2">{{ isEn ? 'Get involved' : '參與' + assets.shortNameZh }}</h4>
          <ul>
            <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
            <li><a :href="lp('/zh/shop/')">{{ tx('官方商店', 'Shop') }}</a></li>
            <li><a :href="lp('/zh/order/lookup/')">{{ tx('訂單查詢', 'Order lookup') }}</a></li>
            <li v-if="showCharity"><a :href="lp('/zh/charity/')">{{ tx('慈善與社會影響', 'Charity & Impact') }}</a></li>
            <li><a :href="lp('/zh/faq/')">{{ tx('常見問題 FAQ', 'FAQ') }}</a></li>
            <li><a :href="lp('/zh/join/')">{{ tx('加入與聯絡', 'Join / Contact') }}</a></li>
            <li><a :href="lp('/zh/join/location/')">{{ tx('場地位置與地圖', 'Venues & map') }}</a></li>
            <li><a :href="lp('/zh/app/')">{{ tx('下載 App', 'Download the app') }}</a></li>
          </ul>
        </div>
        </template>
        <div class="footer-col newsletter">
          <h4 aria-level="2">{{ t('newsletter.title', tx('訂閱電子報', 'Newsletter')) }}</h4>
          <p>{{ t('newsletter.lede', isEn ? `Get ${clubShortName} match reports and event news first.` : `第一時間收到${assets.shortNameZh}賽事戰報與活動資訊。`) }}</p>
          <form @submit.prevent="onSubscribe">
            <label class="visually-hidden" for="newsletter-email">{{ tx('電子郵件地址', 'Email address') }}</label>
            <input id="newsletter-email" v-model="nlEmail" type="email" :placeholder="tx('輸入您的 Email', 'Enter your email')" autocomplete="email" required maxlength="254">
            <div class="visually-hidden" aria-hidden="true">
              <label for="newsletter-website">Leave this field blank</label>
              <input id="newsletter-website" v-model="nlWebsite" type="text" name="website" tabindex="-1" autocomplete="off">
            </div>
            <button type="submit" :disabled="nlStatus === 'submitting'">{{ t('newsletter.button', tx('訂閱', 'Subscribe')) }}</button>
          </form>
          <div class="newsletter__consent">
            <input id="newsletter-consent" v-model="nlConsent" type="checkbox">
            <label for="newsletter-consent">{{ t('newsletter.consent', tx('我同意接收電子報，並已閱讀', 'I agree to receive the newsletter and have read the ')) }}<a :href="lp('/zh/privacy/')">{{ t('newsletter.privacy_link', tx('隱私權政策', 'Privacy Policy')) }}</a></label>
          </div>
          <p v-if="nlMessage" class="newsletter__status" :class="`newsletter__status--${nlStatus}`" :role="nlStatus === 'error' ? 'alert' : 'status'">{{ nlMessage }}</p>
        </div>
      </div>

      <nav v-if="footerPartners.length" class="footer-partners" :aria-label="tx('合作夥伴', 'Partners')">
        <p class="footer-partners__label">{{ tx('合作夥伴', 'Partners') }}</p>
        <ul class="footer-partners__list">
          <li v-for="p in footerPartners" :key="p.id">
            <a v-if="safeExternalUrl(p.websiteUrl)" :href="safeExternalUrl(p.websiteUrl) ?? undefined" target="_blank" rel="noopener noreferrer" :aria-label="tx(`${p.name}（另開新分頁）`, `${p.name} (opens in a new tab)`)">
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
          <a :href="lp('/zh/privacy/')">{{ tx('隱私權政策', 'Privacy Policy') }}</a>
          <a :href="lp('/zh/cookies/')">{{ tx('Cookie 政策', 'Cookie Policy') }}</a>
          <a :href="lp('/zh/member-terms/')">{{ tx('會員條款', 'Membership Terms') }}</a>
          <div class="footer-lang lang-switch" role="group" :aria-label="tx('網站語言切換', 'Site language')">
            <button type="button" :aria-current="locale === 'zh' ? 'true' : undefined" @click="switchTo('zh')">繁中</button><span aria-hidden="true">|</span><button type="button" :aria-current="locale === 'en' ? 'true' : undefined" @click="switchTo('en')">EN</button>
          </div>
        </div>
      </div>
    </div>
  </footer>
</template>

<style>
/* 頁尾底列對齊：政策連結（<a>，繼承 .76rem）與語系切換（<button>，瀏覽器預設字級 13.33px、自帶 padding）原本字級與基線不同，
 * 語系切換看起來比左邊連結低一截、字也較大。統一字級／行高並讓 .legal-links 垂直置中。 */
.footer-bottom .legal-links{ align-items:center; }
.footer-bottom .footer-lang{ gap:.35em; line-height:1.5; }
.footer-bottom .footer-lang button{ font:inherit; line-height:inherit; padding:0 .2em; min-height:0; }
/* G-09 電子報訂閱回饋（H 批）：深色頁尾底，勾選同意與狀態訊息 */
.newsletter__consent{ display:flex; align-items:flex-start; gap:.5rem; margin-top:.75rem; font-size:.78rem; line-height:1.5; color:var(--muted-dark); }
.newsletter .newsletter__consent{ max-width:320px; }
.newsletter .newsletter__consent input[type="checkbox"]{ width:16px; min-width:16px; height:16px; min-height:0; padding:0; margin-top:.2rem; flex:0 0 auto; }
.newsletter .newsletter__consent label{ flex:1 1 auto; min-width:0; }
.newsletter__consent a{ color:#fff; text-decoration:underline; }
.newsletter__status{ margin-top:.6rem; font-size:.8rem; line-height:1.5; }
.newsletter__status--success{ color:#fff; }
.newsletter__status--error{ color:#FFB4B4; }
/* 頁尾贊助夥伴 Logo 列（S2-7）：深色底、單排可換行，Logo 一律縮到同高 */
.footer-partners{ display:flex; flex-wrap:wrap; align-items:center; gap:1rem 2rem; padding:1.75rem 0; border-bottom:1px solid rgba(255,255,255,.08); }
.footer-partners__label{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:#fff; }
.footer-partners__list{ display:flex; flex-wrap:wrap; align-items:center; gap:1rem 1.75rem; list-style:none; margin:0; padding:0; }
.footer-partners__list img{ display:block; height:36px; width:auto; max-width:140px; object-fit:contain; opacity:.8; transition:opacity var(--dur-fast) var(--ease); }
.footer-partners__list a:hover img{ opacity:1; }
.footer-partners__list span{ font-size:.85rem; font-weight:700; color:var(--muted-dark); }
</style>
