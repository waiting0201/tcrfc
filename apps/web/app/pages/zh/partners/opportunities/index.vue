<script setup lang="ts">
// app/pages/zh/partners/opportunities/index.vue — 9.4 贊助方案（S2-7 起接上真實 API）
//
// 資料來源：
//   - 方案卡片：`GET /api/backend/{club}/sponsor-packages?lang=`（後台 E2，只回已發布；價格區間只有後台設為
//     公開時才有值，兩欄皆 null 就完全不顯示價格）。
//   - 提案簡介下載：`GET .../proposals`（已發布且至少一份檔案）＋ `POST .../proposals/{id}/download-requests`
//     （建立 Lead 並回傳 30 分鐘限時連結，後台 E3）。提案 PDF 只放私有容器，**任何地方都不得輸出檔案的公開網址**
//     （docs/14 E1a 規則①），下載一律走同源代理 `/api/backend/{club}/proposals/downloads/{token}`。
// 🔴 方案卡片：API 沒有任何已發布方案時，維持既有 9 張（藍鯨 8 張）靜態方案卡——那是規劃書 §3.9 9.4 列出的
// 方案名稱，不是假資料；後台一旦發布任何一張方案，整區換成後台資料（不混搭）。
// 🔴 A/B 提案：規劃書允許多份提案（版本號不同）做 A/B。前台目前**不隨機分流**（隨機會讓 SSR 與 hydration 不一致，
// 且 Lead 追蹤的比較基準需要可重現的規則），固定挑「有目前語系檔案、版本號最大」的一份（pickProposal）。
import { formatPackagePrice, pickProposal, proposalDownloadHref, splitBenefitList } from '#shared/utils/partners'

definePageMeta({ nav: 'partners', unit: '9.4', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const route = useRoute()
const club = config.public.club
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()／getClubIdentity()。
// 「學院贊助」比照 join/partnership/index.vue 既有做法改讀 identity.academyShortLabelZh。
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => getClubIdentity(config.public.club))
const clubEn = computed(() => bizClubNameEn(config.public.club))
const isTcrfc = computed(() => config.public.club !== 'bw')
// 藍鯨規劃書 v1.9 §2.1：藍鯨不設 8.1 漫畫，因此也沒有「漫畫內容合作」方案；
// 方案數與後續編號跟著同一個單元開關走，不另寫俱樂部判斷。
const mangaEnabled = computed(() => isUnitEnabledForClub('8.1', config.public.club))

// G-12 常見問題快捷區塊：sponsorship 掛載點（db/seed FAQ_EMBED_SLOTS「贊助方案頁（9.4）」，四個固定掛載點
// 的最後一個，S2-7 首次消費）。並掛 FAQPage Schema（GEO-06；沒有題目時 useFaqPageSchema 不輸出）。
const { faqs } = useFaqEmbed(config.public.club, 'sponsorship', locale.value)
useFaqPageSchema(faqs)

const { packages } = await useSponsorPackages()
const { proposals } = await usePublicProposals()
const proposal = computed(() => pickProposal(proposals.value, locale.value))
const hasApiPackages = computed(() => packages.value.length > 0)

const CN_NUM = ['', '一', '兩', '三', '四', '五', '六', '七', '八', '九', '十']
const staticPlanCountZh = computed(() => (mangaEnabled.value ? '九' : '八'))
/** 方案數文字：有後台資料用實際張數（≤10 用中文數字，其餘阿拉伯數字），否則沿用規劃書固定的九／八。 */
const planCountZh = computed(() => {
  if (!hasApiPackages.value) return staticPlanCountZh.value
  const n = packages.value.length
  return n <= 10 ? (CN_NUM[n] ?? String(n)) : String(n)
})
const CN_NUM_EN = ['', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten']
const planCountEn = computed(() => {
  if (!hasApiPackages.value) return mangaEnabled.value ? 'nine' : 'eight'
  const n = packages.value.length
  return n <= 10 ? (CN_NUM_EN[n] ?? String(n)) : String(n)
})
const planCountEnCap = computed(() => planCountEn.value.charAt(0).toUpperCase() + planCountEn.value.slice(1))
const planNum = (n: number) => String(mangaEnabled.value || n < 6 ? n : n - 1).padStart(2, '0')

useSeoMeta({
  title: computed(() => (isEn.value ? `Sponsorship Opportunities | Partners & Sponsors | ${clubEn.value}` : `贊助方案 Sponsorship Opportunities｜合作夥伴與贊助｜${clubAssets.value.nameZh}`)),
  description: computed(() => isEn.value
    ? (hasApiPackages.value
      ? `${clubEn.value} offers ${planCountEn.value} sponsorship packages: ${packages.value.map((p) => p.name).filter(Boolean).join(', ')}.`
      : `${clubEn.value} offers ${planCountEn.value} sponsorship packages: club, ${isTcrfc.value ? 'Academy' : 'Youth team'}, team, camp, international program, ${mangaEnabled.value ? 'manga content, ' : ''}merchandise, Fan Club and venue naming rights.`)
    : hasApiPackages.value
    ? `${clubAssets.value.nameZh}${planCountZh.value}種贊助方案：${packages.value.map((p) => p.name).filter(Boolean).join('、')}。`
    : `${clubAssets.value.nameZh}${planCountZh.value}種贊助方案：俱樂部、${identity.value.academyShortLabelZh}、球隊、營隊、國際計畫、${mangaEnabled.value ? '漫畫內容、' : ''}商品、球迷會、場館冠名。`),
})

// ---- 提案簡介下載表單（建立 Lead）----
const company = ref('')
const contactName = ref('')
const email = ref('')
const consent = ref(false)
const website = ref('')
const dlStatus = ref<'idle' | 'submitting' | 'success' | 'error'>('idle')
const dlError = ref('')
const dlHref = ref<string | null>(null)

async function onDownloadSubmit() {
  if (!proposal.value) return
  dlStatus.value = 'submitting'
  dlError.value = ''
  dlHref.value = null
  try {
    const q = route.query
    const res = await $fetch<{ downloadPath: string | null, expiresAt: string | null }>(
      `/api/backend/${club}/proposals/${proposal.value.id}/download-requests`,
      {
        method: 'POST',
        body: {
          company: company.value,
          name: contactName.value,
          email: email.value,
          consent: consent.value,
          lang: locale.value,
          sourcePath: route.fullPath,
          utmSource: typeof q.utm_source === 'string' ? q.utm_source : undefined,
          utmCampaign: typeof q.utm_campaign === 'string' ? q.utm_campaign : undefined,
          website: website.value || undefined,
        },
      },
    )
    // 誘捕欄位被觸發時後端安靜回成功但沒有連結：照常顯示成功，不洩漏判斷。
    dlHref.value = proposalDownloadHref(res.downloadPath, club)
    dlStatus.value = 'success'
  }
  catch (err: unknown) {
    dlStatus.value = 'error'
    dlError.value = (err as { statusCode?: number }).statusCode === 429
      ? tx('送出過於頻繁，請稍候幾分鐘再試。', 'Too many requests. Please wait a few minutes and try again.')
      : extractErrorMessage(err, isEn.value) ?? tx('送出失敗，請確認各欄位已正確填寫後再試一次；若持續發生，請改用「前往贊助洽詢表單」與我們聯繫。', 'We could not submit your request. Please check that all fields are filled in correctly and try again. If the problem continues, please use the "Go to the sponsorship enquiry form" button to contact us.')
  }
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/partners/')">{{ tx('夥伴', 'Partners') }}</a></li>
      <li aria-current="page">{{ tx('贊助方案', 'Sponsorship Opportunities') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">9.4 Sponsorship Opportunities</p>
    <h1><template v-if="isEn">Sponsorship Opportunities</template><template v-else>{{ tx('贊助方案', 'Sponsorship Opportunities') }}<span class="en">Sponsorship Opportunities</span></template></h1>
    <p v-if="hasApiPackages" class="page-hero__lede"><template v-if="isEn">{{ planCountEnCap }} sponsorship packages to help you find the partnership scale that suits you best.</template><template v-else>{{ planCountZh }}種贊助方案，找到最適合的合作規模。</template></p>
    <p v-else class="page-hero__lede"><template v-if="isEn">{{ planCountEnCap }} sponsorship packages, from the club, {{ isTcrfc ? 'the Academy' : 'the Youth team' }}, teams and camps through to venue naming rights, to help you find the partnership scale that suits you best.</template><template v-else>{{ planCountZh }}種贊助方案，涵蓋俱樂部、{{ identity.academyShortLabelZh }}、球隊、營隊到場館冠名，找到最適合的合作規模。</template></p>
  </div>
</section>

<!-- SPEC 3.9 §9.4 — 贊助方案卡片（磐石九種；藍鯨不設漫畫為八種，藍鯨規劃書 v1.9 §2.1） -->
<section class="band grain" id="plans" aria-labelledby="plans-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="plans-title"><template v-if="isEn">{{ planCountEnCap }} sponsorship packages</template><template v-else>{{ planCountZh }}種贊助方案</template></h2>
    <div v-if="hasApiPackages" class="grid grid--3 plan-grid">
      <article v-for="(pkg, i) in packages" :key="pkg.id" class="plan-card">
        <p class="plan-card__num">{{ String(i + 1).padStart(2, '0') }}</p>
        <h3 class="plan-card__title">{{ pkg.name }}</h3>
        <p v-if="pkg.content" class="plan-card__desc plan-card__desc--content">{{ pkg.content }}</p>
        <ul v-if="splitBenefitList(pkg.benefitList).length" class="plan-card__benefits">
          <li v-for="b in splitBenefitList(pkg.benefitList)" :key="b">{{ b }}</li>
        </ul>
        <p v-if="pkg.audience" class="plan-card__meta"><span>{{ tx('適合對象', 'Suited to') }}</span>{{ pkg.audience }}</p>
        <p v-if="formatPackagePrice(pkg.priceMin, pkg.priceMax)" class="plan-card__meta"><span>{{ tx('參考價格', 'Reference price') }}</span>{{ formatPackagePrice(pkg.priceMin, pkg.priceMax, isEn) }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
    </div>
    <div v-else class="grid grid--3 plan-grid">
      <article class="plan-card">
        <p class="plan-card__num">01</p>
        <h3 class="plan-card__title"><template v-if="isEn">Club Sponsorship</template><template v-else>俱樂部贊助<span class="en">Club Sponsorship</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">02</p>
        <h3 class="plan-card__title"><template v-if="isEn">{{ getPartnersPlanYouthTitleEn(club) }}</template><template v-else>{{ identity.academyShortLabelZh }}贊助<span class="en">{{ getPartnersPlanYouthTitleEn(club) }}</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">03</p>
        <h3 class="plan-card__title"><template v-if="isEn">Team Sponsorship</template><template v-else>球隊贊助<span class="en">Team Sponsorship</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">04</p>
        <h3 class="plan-card__title"><template v-if="isEn">Camp Sponsorship</template><template v-else>營隊贊助<span class="en">Camp Sponsorship</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">05</p>
        <h3 class="plan-card__title"><template v-if="isEn">International Program Sponsorship</template><template v-else>國際計畫贊助<span class="en">International Programme Sponsorship</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article v-if="mangaEnabled" class="plan-card">
        <p class="plan-card__num">06</p>
        <h3 class="plan-card__title"><template v-if="isEn">Manga Content Partnership</template><template v-else>漫畫內容合作<span class="en">Manga Content Partnership</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">{{ planNum(7) }}</p>
        <h3 class="plan-card__title"><template v-if="isEn">Merchandise Partnership</template><template v-else>商品合作<span class="en">Merchandise Partnership</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">{{ planNum(8) }}</p>
        <h3 class="plan-card__title"><template v-if="isEn">Fan Club Sponsorship</template><template v-else>球迷會贊助<span class="en">Fan Club Sponsorship</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
      <article class="plan-card">
        <p class="plan-card__num">{{ planNum(9) }}</p>
        <h3 class="plan-card__title"><template v-if="isEn">Venue Naming Rights</template><template v-else>場館冠名<span class="en">Venue Naming Rights</span></template></h3>
        <p class="plan-card__desc">{{ tx('洽詢方案時將提供完整權益說明。', 'Full benefit details are provided when you enquire about this package.') }}</p>
        <a class="btn btn--light btn--sm" :href="lp('/zh/join/partnership/')">{{ tx('洽詢方案', 'Enquire about this package') }}</a>
      </article>
    </div>
  </div>
</section>

<!-- SPEC 3.9 CTA — 提案簡介下載表單 -->
<section class="band" id="deck-download" aria-labelledby="deck-download-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">DOWNLOAD</p>
        <h2 class="section-title" id="deck-download-title">{{ tx('下載提案簡介', 'Download the sponsorship deck') }}</h2>
      </div>
      <p class="section-lede">{{ tx('留下公司與聯絡資訊，我們將提供完整贊助提案簡介的下載連結。', 'Leave your company and contact details and we will send you a download link for the full sponsorship deck.') }}</p>
    </div>

    <!-- 提案簡介下載：Lead 追蹤（公司／姓名／Email＋個資同意）。沒有已發布提案時不顯示表單，改為說明與洽詢入口。 -->
    <div v-if="!proposal" class="deck-empty" role="status">
      <p><template v-if="isEn">The sponsorship deck is being prepared and will be available for download soon. To learn about sponsorship in the meantime, please <a :href="lp('/zh/join/partnership/')">fill in the sponsorship enquiry form</a> and our Partnerships Department will get back to you shortly.</template><template v-else>提案簡介準備中，稍後將開放下載。想先了解贊助內容，歡迎直接<a :href="lp('/zh/join/partnership/')">填寫贊助洽詢表單</a>，商務部將盡快與您聯繫。</template></p>
    </div>
    <template v-else>
      <div v-if="dlStatus === 'success'" class="form-status form-status--success" role="status" style="margin-top:1.75rem;max-width:760px">
        <p v-if="dlHref"><template v-if="isEn">We have received your details. The sponsorship deck download link is below (valid for 30 minutes; please resubmit the form if it expires): <a :href="dlHref" download>Download "{{ proposal.title }}"</a></template><template v-else>已收到您的資料，提案簡介下載連結如下（30 分鐘內有效，過期請重新填寫）：<a :href="dlHref" download>下載「{{ proposal.title }}」</a></template></p>
        <p v-else>{{ tx('已收到您的資料，感謝您的關注。', 'We have received your details. Thank you for your interest.') }}</p>
      </div>
      <div v-else-if="dlStatus === 'error'" class="form-status form-status--error" role="alert" style="margin-top:1.75rem;max-width:760px">
        <p>{{ dlError }}</p>
      </div>
      <form v-if="dlStatus !== 'success'" class="form-grid" style="margin-top:1.75rem;max-width:760px" @submit.prevent="onDownloadSubmit">
        <HoneypotField v-model="website" />
        <div class="form-field">
          <label for="deck-company">{{ tx('公司名稱', 'Company name') }}</label>
          <input id="deck-company" v-model="company" type="text" name="company" autocomplete="organization" required>
        </div>
        <div class="form-field">
          <label for="deck-name">{{ tx('聯絡人姓名', 'Contact name') }}</label>
          <input id="deck-name" v-model="contactName" type="text" name="name" autocomplete="name" required>
        </div>
        <div class="form-field form-field--full">
          <label for="deck-email">Email</label>
          <input id="deck-email" v-model="email" type="email" name="email" autocomplete="email" required>
        </div>
        <div class="consent-block deck-consent">
          <p v-if="isEn" class="field-hint">The consent notice below is pending legal review and is shown in Traditional Chinese.</p>
          <div class="checkbox-field">
            <input id="deck-consent" v-model="consent" type="checkbox" name="consent" required>
            <label for="deck-consent">我已閱讀並同意<a :href="lp('/zh/privacy/')">隱私權政策</a>，並同意{{ clubAssets.nameZh }}蒐集上述聯絡資料，用於提供提案簡介下載與後續商務聯繫。</label>
          </div>
        </div>
        <div class="form-field form-field--full">
          <button class="btn btn--primary" type="submit" :disabled="dlStatus === 'submitting'">{{ dlStatus === 'submitting' ? tx('送出中…', 'Submitting...') : tx('取得下載連結', 'Get download link') }}</button>
        </div>
      </form>
    </template>
  </div>
</section>

<!-- G-12 FAQ 快捷區塊：9.4 贊助方案常見問題 -->
<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(faqs)" partial />
<section id="sponsorship-faq" class="band" aria-labelledby="sponsorship-faq-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAQ</p>
        <h2 id="sponsorship-faq-title" class="section-title">{{ tx('贊助方案常見問題', 'Sponsorship FAQ') }}</h2>
      </div>
      <a :href="lp('/zh/faq/')">{{ tx('查看全部常見問題 →', 'View all FAQs →') }}</a>
    </div>
    <p v-if="faqs.length === 0" class="is-pending" style="margin-top:1.5rem;">{{ tx('常見問題收錄中，稍後將於本頁公布。', 'FAQs are being compiled and will be published here soon.') }}</p>
    <dl v-else class="faq-embed-list">
      <div v-for="f in faqs" :key="f.id" class="faq-embed-item">
        <dt>{{ f.question }}</dt>
        <dd>{{ f.answer }}</dd>
      </div>
    </dl>
  </div>
</section>

<!-- SPEC 3.9 CTA — 聯絡我們 -->
<section class="band grain cta-band" aria-labelledby="contact-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="contact-cta-title">{{ tx('聯絡商務部洽談贊助', 'Contact our Partnerships Department about sponsorship') }}</h2>
    <div class="cta-card" style="max-width:640px">
      <p class="cta-card__num">CONTACT US</p>
      <p class="cta-card__title">{{ tx('聯絡我們', 'Contact us') }}</p>
      <p class="cta-card__desc">{{ tx('想進一步討論贊助內容與權益？商務部將盡快與您聯繫，導向 10.5 合作夥伴與贊助洽詢表單。', 'Want to discuss sponsorship details and benefits? Our Partnerships Department will get back to you shortly, via the Partnership & Sponsorship enquiry form (10.5).') }}</p>
      <a class="btn btn--primary" :href="lp('/zh/join/partnership/')">{{ tx('前往贊助洽詢表單', 'Go to the sponsorship enquiry form') }}</a>
    </div>
  </div>
</section>
</template>

<style>
/* 贊助方案卡（plan-card）：9 張方案卡，建議收進共用 CSS */
.plan-grid{ margin-top:1rem; }
.plan-card{
  position:relative; background:var(--ink); border:1px solid rgba(255,255,255,.1);
  padding:2rem 1.6rem 1.75rem; display:flex; flex-direction:column; gap:.75rem; color:#fff;
  clip-path:polygon(0 0,100% 0,100% calc(100% - 20px),calc(100% - 20px) 100%,0 100%);
}
.plan-card__num{ font-size:.72rem; font-weight:800; letter-spacing:.1em; color:var(--brand); }
.plan-card__title{ font-size:1.15rem; font-weight:900; letter-spacing:-.01em; }
.plan-card__title .en{ display:block; font-size:.62em; font-weight:700; letter-spacing:.06em; text-transform:uppercase; color:var(--muted-dark); margin-top:.35em; }
.plan-card__desc{ font-size:.85rem; color:var(--muted-dark); line-height:1.6; flex:1; }
.plan-card .btn{ align-self:flex-start; margin-top:.25rem; }
/* 後台方案卡的內容欄位（S2-7）：方案說明、權益清單、適合對象、參考價格 */
.plan-card__desc--content{ flex:none; white-space:pre-line; }
.plan-card__benefits{ list-style:none; margin:0; padding:0; display:flex; flex-direction:column; gap:.35rem; flex:1; }
.plan-card__benefits li{ position:relative; padding-left:1.1rem; font-size:.82rem; line-height:1.55; color:var(--muted-dark); }
.plan-card__benefits li::before{ content:""; position:absolute; left:0; top:.55em; width:.45rem; height:.45rem; background:var(--brand); }
.plan-card__meta{ font-size:.82rem; color:var(--muted-dark); line-height:1.5; }
.plan-card__meta span{ display:block; font-size:.66rem; font-weight:800; letter-spacing:.08em; color:var(--brand); margin-bottom:.15rem; }
.deck-empty{ margin-top:1.75rem; max-width:760px; padding:1.5rem; background:var(--paper-2); border:1px dashed var(--rule); font-size:.9rem; line-height:1.7; }
.is-pending{ color:var(--muted); font-style:italic; }
.faq-embed-list{ margin-top:1.5rem; display:flex; flex-direction:column; gap:1.25rem; }
.faq-embed-item dt{ font-weight:800; color:var(--heading); }
.faq-embed-item dd{ margin:.4rem 0 0; color:var(--muted); font-size:.9rem; line-height:1.7; }
.deck-consent{ grid-column:1 / -1; margin:.5rem 0; }
.deck-empty a{ color:var(--brand-aa); text-decoration:underline; }

/* 表單欄位格線（form-grid）：與 fan-club 頁共用寫法，建議收進共用 CSS */
.form-grid{ display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:1.25rem 1.5rem; }
</style>
