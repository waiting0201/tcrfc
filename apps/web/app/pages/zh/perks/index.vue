<script setup lang="ts">
// app/pages/zh/perks/index.vue — 由 site/src/pages/zh/perks/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: 'culture', unit: '08', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：改讀既有的 getClubAssets()／getClubIdentity()，理由同
// privacy/index.vue；麵包屑第二層文字改讀既有的 identity.cultureLabelZh（原本已是
// SiteHeader／SiteFooter 共用的既有欄位，本頁先前沒有引用，是既有缺口）。
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => (isEn.value ? (config.public.club === 'bw' ? getClubIdentityEnBw() : getClubIdentityEn()) : getClubIdentity(config.public.club)))
const isTcrfc = computed(() => config.public.club !== 'bw')

// S2-11（8.4 特約店家）：清單來自後台 K4（`partner-stores`，本俱樂部專屬＋兩隊共同、已上架且在合作期間內），
// 篩選（類別／地區／適用層級）走網址 query，SSR 與無 JS 皆可用；沒有已上架店家時顯示誠實的空狀態。
const route = useRoute()
const router = useRouter()
const filterQuery = computed(() => ({
  category: typeof route.query.category === 'string' ? route.query.category : '',
  region: typeof route.query.region === 'string' ? route.query.region : '',
  tier: route.query.tier === 'all' || route.query.tier === 'fan_club' ? route.query.tier : '',
}))
const { stores, failed, pending } = await usePartnerStores(() => filterQuery.value)
const { filters } = await usePartnerStoreFilters()
const filtered = computed(() => Boolean(filterQuery.value.category || filterQuery.value.region || filterQuery.value.tier))

function setFilter(key: 'category' | 'region' | 'tier', value: string) {
  const query: Record<string, string> = {}
  for (const [k, v] of Object.entries({ ...filterQuery.value, [key]: value })) if (v) query[k] = v
  router.replace({ query })
}

useSeoMeta({
  title: computed(() => (isEn.value ? (isTcrfc.value ? CLUB_PERKS_SEO_EN : CLUB_PERKS_SEO_EN_BW).title : `特約店家 Partner Perks｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value
    ? (isTcrfc.value ? CLUB_PERKS_SEO_EN : CLUB_PERKS_SEO_EN_BW).description
    : `${clubAssets.value.shortNameZh}會員的特約店家折扣清單。到店出示電子會員卡即可享有優惠，依店家標示適用一般會員或付費球迷會員。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li aria-current="page">{{ tx('特約店家', 'Partner Perks') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">08</span>
  <div class="container">
    <p class="page-hero__eyebrow">Partner Perks</p>
    <h1>{{ tx('特約店家', 'Partner Perks') }}<span v-if="!isEn" class="en">Partner Perks</span></h1>
    <p class="page-hero__lede">{{ isEn ? (isTcrfc ? 'Local stores working with Taichung Rock. Members can enjoy offers by showing their digital membership card in store. This page is public and can be viewed without logging in.' : CLUB_PERKS_HERO_LEDE_EN_BW) : `與${clubAssets.shortNameZh}合作的在地店家，會員到店出示電子會員卡即可享有優惠。本頁公開，不需登入即可瀏覽。` }}</p>
  </div>
</section>

<!-- SPEC 3.8 §8.4 — 店家清單（依類別與地區篩選） -->
<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(stores)" partial />
<section class="band" id="stores" aria-labelledby="stores-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">STORE LIST</p>
        <h2 class="section-title" id="stores-title">{{ tx('合作店家', 'Partner stores') }}</h2>
      </div>
      <a class="btn btn--dark btn--sm" :href="lp('/zh/member/#tab-register')">{{ tx('加入會員', 'Join as a member') }}</a>
    </div>

    <div class="store-filters" role="group" :aria-label="tx('店家篩選', 'Filter stores')">
      <div class="store-filters__group">
        <label class="store-filters__label" for="sf-category">{{ tx('類別', 'Category') }}</label>
        <select id="sf-category" :value="filterQuery.category" @change="setFilter('category', ($event.target as HTMLSelectElement).value)">
          <option value="">{{ tx('全部類別', 'All categories') }}</option>
          <option v-for="c in filters.categories" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div class="store-filters__group">
        <label class="store-filters__label" for="sf-region">{{ tx('地區', 'Region') }}</label>
        <select id="sf-region" :value="filterQuery.region" @change="setFilter('region', ($event.target as HTMLSelectElement).value)">
          <option value="">{{ tx('全部地區', 'All regions') }}</option>
          <option v-for="r in filters.regions" :key="r" :value="r">{{ r }}</option>
        </select>
      </div>
      <div class="store-filters__group">
        <label class="store-filters__label" for="sf-tier">{{ tx('適用層級', 'Eligible tier') }}</label>
        <select id="sf-tier" :value="filterQuery.tier" @change="setFilter('tier', ($event.target as HTMLSelectElement).value)">
          <option value="">{{ tx('全部', 'All') }}</option>
          <option value="all">{{ tx('全會員適用', 'All members') }}</option>
          <option value="fan_club">{{ tx('限付費會員', 'Paid Fan Club members only') }}</option>
        </select>
      </div>
    </div>

    <p class="result-count" role="status">{{ pending ? tx('載入中…', 'Loading…') : (isEn ? `${stores.length} ${stores.length === 1 ? 'store' : 'stores'}` : `共 ${stores.length} 家店家`) }}</p>

    <div v-if="stores.length > 0" class="grid grid--3 store-grid">
      <article v-for="st in stores" :key="st.slug" class="store-card">
        <a class="store-card__link" :href="lp(`/zh/perks/${st.slug}/`)">
          <div class="store-card__media">
            <img v-if="safeImageUrl(st.imageUrl)" :src="safeImageUrl(st.imageUrl)!" :alt="imgAlt(st.imageAlt, st.name)" loading="lazy" v-bind="imgAttrs(st.imageWidth, st.imageHeight)">
          </div>
          <div class="store-card__body">
            <p v-if="st.category" class="store-card__cat">{{ st.category }}<template v-if="st.region">・{{ st.region }}</template></p>
            <h3 class="store-card__name">{{ st.name }}</h3>
            <p v-if="st.offerContent" class="store-card__offer">{{ st.offerContent }}</p>
            <ul class="store-card__meta">
              <li v-if="st.address">{{ st.address }}</li>
              <li v-if="st.phone">{{ st.phone }}</li>
              <li v-if="st.businessHours">{{ st.businessHours }}</li>
            </ul>
            <p class="store-card__tier" :class="{ 'store-card__tier--paid': st.applicableTier === 'fan_club' }">{{ st.applicableTierLabel }}</p>
          </div>
        </a>
      </article>
    </div>
    <div v-else class="pending-note">
      <template v-if="failed">{{ tx('店家名單暫時無法載入，請稍後再試。', 'The store list cannot be loaded right now. Please try again later.') }}</template>
      <template v-else-if="filtered">{{ tx('沒有符合條件的店家，請調整篩選條件。', 'No stores match your filters. Please adjust them and try again.') }}</template>
      <template v-else-if="isEn">
        <strong>The partner store list has not been announced yet</strong> — there are no partner stores listed at the moment. Once the list is announced, this page will show each store's offer, address, opening hours and eligible tier.
      </template>
      <template v-else>
        <strong>合作店家名單尚未公布</strong> —— 目前沒有已上架的特約店家。名單公布後會在這裡顯示每家店的優惠內容、地址、營業時間與適用層級。
      </template>
    </div>
  </div>
</section>

<!-- SPEC 3.8 §8.4 — 使用方式與注意事項 -->
<section class="band paper-2-band" id="how-to-use" aria-labelledby="how-title">
  <div class="container">
    <p class="kicker">HOW TO USE</p>
    <h2 class="section-title" id="how-title">{{ tx('怎麼使用', 'How to use') }}</h2>

    <ol class="usage-steps">
      <li>
        <span class="usage-steps__num">1</span>
        <div>
          <h3>{{ tx('加入會員', 'Join as a member') }}</h3>
          <p>{{ tx('以 Email 或 LINE 加入，完成驗證後即可在會員中心取得電子會員卡。', 'Sign up with your email or LINE. Once you have been verified, you can get your digital membership card in the Member Centre.') }}</p>
        </div>
      </li>
      <li>
        <span class="usage-steps__num">2</span>
        <div>
          <h3>{{ tx('到店出示會員卡', 'Show your card in store') }}</h3>
          <p>{{ tx('結帳前出示會員中心的電子會員卡，店家目視查驗即可。卡片上的 QR Code 可由店家掃描，開啟後僅顯示會籍是否有效，不會顯示其他個人資料。', 'Before you pay, show the digital membership card from the Member Centre and the store will check it by eye. The store can also scan the QR code on the card, which only shows whether the membership is valid and no other personal data.') }}</p>
        </div>
      </li>
      <li>
        <span class="usage-steps__num">3</span>
        <div>
          <h3>{{ tx('享有折扣', 'Enjoy the discount') }}</h3>
          <p>{{ tx('各店優惠內容與適用層級以本頁標示為準。標示「限付費會員」者，需為有效期內的球迷會員。', 'Each store\'s offer and eligible tier are as shown on this page. Offers marked "Paid Fan Club members only" require a Fan Club membership that is still valid.') }}</p>
        </div>
      </li>
    </ol>

    <div class="notes-block">
      <h3 class="notes-block__title">{{ tx('注意事項', 'Please note') }}</h3>
      <ul>
        <li>{{ tx('優惠內容與適用條件由各店家提供，實際以店家現場公告為準。', 'Offers and conditions are provided by each store; the notice displayed in store takes precedence.') }}</li>
        <li>{{ tx('除另有標示外，恕不與店家其他優惠或活動併用。', 'Unless stated otherwise, offers cannot be combined with other store promotions or events.') }}</li>
        <li>{{ tx('會籍到期後即無法使用特約店家優惠，請留意會員卡上的有效期限。', 'Partner store offers can no longer be used once the membership expires, so please check the expiry date on your membership card.') }}</li>
        <li>{{ tx('俱樂部保留調整合作店家與優惠內容的權利，異動將於本頁更新。', 'The club reserves the right to change partner stores and offers, and changes will be updated on this page.') }}</li>
      </ul>
    </div>
  </div>
</section>

<!-- 轉換帶：把瀏覽店家的人導回加入會員 -->
<section class="band" aria-labelledby="join-cta-title">
  <div class="container">
    <div class="perks-cta">
      <div>
        <h2 class="section-title" id="join-cta-title">{{ tx('還不是會員？', 'Not a member yet?') }}</h2>
        <p class="section-lede">{{ tx('免費加入即可享標示「全會員適用」的店家折扣；升級付費球迷會員，另可獲得球衣與更多店家優惠。', 'Join for free to enjoy store discounts marked "All members"; upgrade to a Paid Fan Club member to also receive a jersey and more store offers.') }}</p>
      </div>
      <div class="perks-cta__actions">
        <a class="btn btn--primary" :href="lp('/zh/member/#tab-register')">{{ tx('加入會員', 'Join as a member') }}</a>
        <a class="btn btn--dark" :href="lp('/zh/culture/fan-club/')">{{ tx('了解付費會籍', 'About paid membership') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* ── 08.4 特約店家 ── */
.paper-2-band{ background:var(--paper-2); }
.store-filters{ display:flex; flex-wrap:wrap; gap:1.25rem; margin-top:1.5rem; }
.store-filters__group{ display:flex; align-items:center; gap:.6rem; }
.store-filters__label{ font-size:.8rem; font-weight:800; color:var(--muted); letter-spacing:.04em; }
.store-filters select{
  padding:.55rem .9rem; border:1px solid var(--rule); background:var(--paper);
  font-size:.85rem; color:var(--text);
}

.store-grid{ margin-top:1.75rem; }
.store-card__media img{ width:100%; height:100%; object-fit:cover; display:block; }
.store-card{ background:var(--paper); border:1px solid var(--rule); display:flex; flex-direction:column; }
.store-card__media{ aspect-ratio:3/2; background:var(--paper-2); border-bottom:1px solid var(--rule); }
.store-card__body{ padding:1.35rem 1.5rem 1.5rem; }
.store-card__cat{ font-size:.72rem; font-weight:800; color:var(--muted); letter-spacing:.08em; margin-bottom:.4rem; }
.store-card__name{ font-size:1.05rem; font-weight:800; color:var(--heading); margin-bottom:.5rem; }
.store-card__offer{ font-size:.9rem; font-weight:700; color:var(--brand-aa); margin-bottom:.75rem; }
.store-card__meta{ list-style:none; padding:0; margin:0 0 .9rem; font-size:.82rem; line-height:1.8; color:var(--muted); }
.store-card__tier{
  display:inline-block; font-size:.75rem; font-weight:800; padding:.3rem .7rem;
  background:var(--paper-2); border:1px solid var(--rule); color:var(--muted);
}

.usage-steps{ list-style:none; padding:0; margin:1.75rem 0 0; display:grid; gap:1.5rem; }
@media (min-width:900px){ .usage-steps{ grid-template-columns:repeat(3,1fr); } }
.usage-steps li{ display:flex; gap:1rem; align-items:flex-start; }
.usage-steps__num{
  flex:0 0 auto; width:2rem; height:2rem; display:grid; place-items:center;
  background:var(--brand-aa); color:#fff; font-weight:800; font-size:.9rem;
}
.usage-steps h3{ font-size:1rem; font-weight:800; color:var(--heading); margin-bottom:.4rem; }
.usage-steps p{ font-size:.86rem; line-height:1.7; color:var(--text); }

.notes-block{ margin-top:2.5rem; padding:1.5rem; background:var(--paper); border:1px solid var(--rule); }
.notes-block__title{ font-size:.95rem; font-weight:800; color:var(--heading); margin-bottom:.75rem; }
.notes-block ul{ margin:0; padding-left:1.2rem; font-size:.85rem; line-height:1.9; color:var(--text); }

.perks-cta{ display:flex; flex-wrap:wrap; gap:2rem; align-items:center; justify-content:space-between; }
.perks-cta__actions{ display:flex; flex-wrap:wrap; gap:.75rem; }
</style>
