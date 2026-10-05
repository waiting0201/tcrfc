<script setup lang="ts">
// app/pages/zh/member/index.vue — 會員中心（主站規劃書 §3.14，S2-11）
//
// 未登入：登入／加入會員（MemberAuthPanel，版型沿用 mockup 的 member-tabs）。
// 已登入：會員中心（MemberDashboard：我的會籍／電子會員卡／球衣登記／個人資料與安全）。
//
// 🔴 會員頁不得被快取、不得被索引：頁面 `noindex`，回應 `Cache-Control: no-store`（nuxt.config.ts routeRules `/zh/member/**`），
// 且所有會員資料只在瀏覽器端載入（SSR 輸出永遠是「載入中」殼，不含任何個人資料）。
// 工作階段怎麼運作見 app/composables/useMemberSession.ts 檔頭。
definePageMeta({ nav: '', unit: '14', enReady: true })

const { lp, isEn, tx } = useLocale()
const route = useRoute()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const { isLoggedIn, restored, restore } = useMemberSession()

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('member', CLUB_NAME_EN).title : `會員中心 Member｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('member', CLUB_NAME_EN).description : `${clubAssets.value.nameZh}會員中心：登入與加入會員。會員享特約店家折扣，付費球迷會員另可獲得球衣。`)),
  robots: 'noindex, nofollow',
})

onMounted(() => { restore() })

/** 登入成功（或已登入）後：有 `?next=` 就回去（只接受站內 /zh/、/en/ 路徑），否則留在這裡看會員中心。
 * 用 watch 而不是 AuthPanel 的 `done` 事件：登入一成功 `isLoggedIn` 就翻轉、AuthPanel 立刻被卸載，
 * 卸載後的 emit 會被丟掉。 */
watch([isLoggedIn, restored], async ([loggedIn, ready]) => {
  if (!loggedIn || !ready) return
  const next = safeNextPath(route.query.next)
  if (next) await navigateTo(next)
})

async function onLoggedOut() {
  await navigateTo(lp('/zh/member/'))
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('會員中心', 'Member Centre') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">14</span>
  <div class="container">
    <p class="page-hero__eyebrow">Member Centre</p>
    <h1>{{ tx('會員中心', 'Member Centre') }}<span v-if="!isEn" class="en">Member</span></h1>
    <p v-if="isEn" class="page-hero__lede">Join {{ CLUB_NAME_EN }} as a member and show your membership card at partner stores to enjoy discounts. Upgrade to Paid Fan Club membership to also receive a jersey.</p>
    <p v-else class="page-hero__lede">加入{{ clubAssets.shortNameZh }}會員，到特約店家出示會員卡即享折扣；升級付費球迷會員，另可獲得球衣。</p>
  </div>
</section>

<section class="band member-band" aria-labelledby="member-title">
  <div class="band-inner container">
    <h2 id="member-title" class="visually-hidden">{{ tx('會員登入與會員中心', 'Member sign-in and Member Centre') }}</h2>

    <noscript><p class="mc-alert mc-alert--info">{{ tx('會員中心需要啟用 JavaScript 才能使用。', 'The Member Centre requires JavaScript to be enabled.') }}</p></noscript>
    <p v-if="!restored" class="mc-empty" role="status">{{ tx('確認登入狀態中…', 'Checking your sign-in status…') }}</p>

    <MemberDashboard v-else-if="isLoggedIn" @logged-out="onLoggedOut" />

    <template v-else>
      <MemberAuthPanel />

      <h2 class="section-title" style="margin-top:4rem;">{{ tx('登入後可以做什麼', 'What you can do after signing in') }}</h2>
      <div class="member-features">
        <div class="feature-card">
          <p class="feature-card__title">{{ tx('電子會員卡', 'Digital membership card') }}</p>
          <p class="feature-card__desc">{{ tx('會員編號、QR Code、層級與有效期限。到特約店家出示即可，店家目視查驗，不需掃碼核銷。', 'Your member number, QR code, tier and expiry date. Just show it at a partner store: the store checks it by eye, and no scanning or redemption is needed.') }}</p>
        </div>
        <div class="feature-card">
          <p class="feature-card__title">{{ tx('特約店家', 'Partner stores') }}</p>
          <p v-if="isEn" class="feature-card__desc">Browse <a :href="lp('/zh/perks/')">partner stores</a> by category and region, with each store's offer and eligible tier shown (no sign-in needed to browse).</p>
          <p v-else class="feature-card__desc">依類別與地區瀏覽<a :href="lp('/zh/perks/')">合作店家</a>，每家標示優惠內容與適用層級（不需登入即可瀏覽）。</p>
        </div>
        <div class="feature-card">
          <p class="feature-card__title">{{ tx('會籍與續會', 'Membership and renewal') }}</p>
          <p class="feature-card__desc">{{ tx('升級付費會籍、查看有效期限，球季末辦理續會。', 'Upgrade to a paid membership, check your expiry date, and renew at the end of the season.') }}</p>
        </div>
        <div class="feature-card">
          <p class="feature-card__title">{{ tx('球衣登記', 'Jersey registration') }}</p>
          <p class="feature-card__desc">{{ tx('付費會籍開通後填寫尺寸與領取方式（寄送或到場領取），並查看發放狀態。', 'Once your paid membership is activated, enter your size and how you will receive the jersey (delivery or collection), and check its status.') }}</p>
        </div>
        <div class="feature-card">
          <p class="feature-card__title">{{ tx('我的訂單', 'My orders') }}</p>
          <p v-if="isEn" class="feature-card__desc">Your orders from the <a :href="lp('/zh/shop/')">shop</a>: items, amounts, payment and shipping status, tracking numbers, e-invoice numbers, and how to request a return or exchange. Non-members can use <a :href="lp('/zh/order/lookup/')">Order lookup</a>.</p>
          <p v-else class="feature-card__desc"><a :href="lp('/zh/shop/')">官方商店</a>的訂單一覽：品項、金額、付款與出貨狀態、物流單號、電子發票號碼，以及退換貨申請入口。非會員請用<a :href="lp('/zh/order/lookup/')">訂單查詢</a>。</p>
        </div>
      </div>

      <div class="pending-note" style="margin-top:2rem;">
        {{ tx('會費採 LINE Pay 收款連結與現場收款，網頁只接受升級申請、不收款，不走商店結帳（見規劃書 3.14）；商店的商品才走站內 LINE Pay 結帳。付費會籍的權益不含商品折扣。', 'Membership fees are collected through a LINE Pay payment link or in person. This website only accepts upgrade requests and does not take payment, and it does not use shop checkout. Only shop products are paid for through on-site LINE Pay checkout. Paid membership benefits do not include product discounts.') }}
      </div>
    </template>
  </div>
</section>
</template>

<style>
.member-band{ padding-block:clamp(3.5rem,6vw,6rem); }

/* 功能卡：白底＋頂部品牌色細線 */
.member-features{
  display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr));
  gap:1.25rem; margin-top:1.5rem;
}
.feature-card{
  background:var(--paper); border:1px solid var(--rule); border-top:3px solid var(--brand);
  padding:1.4rem 1.5rem 1.5rem;
}
.feature-card__title{ font-weight:800; color:var(--heading); margin-bottom:.5rem; font-size:.98rem; }
.feature-card__desc{ font-size:.85rem; line-height:1.7; color:var(--text); text-wrap:pretty; }
.feature-card__desc a{ color:var(--brand-aa); text-decoration:underline; }
</style>
