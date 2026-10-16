<script setup lang="ts">
// app/pages/zh/member/upgrade/index.vue — 會員中心「會籍方案」入口（主站規劃書 §3.14／§8.2；
// App 規劃書 §2.3：`tcrfc://upgrade` 的官網回退網址 `/zh/member/upgrade/`；v3.29 起意義是「會籍方案頁」——不提供升級，網址與深連結維持不變）
//
// 沒有新的資料與流程：購買會籍／到期續約的真正流程是會員中心「我的會籍」分頁（MemberMemberships，只送出 `created`
// 申請、不收款，見該元件檔頭），本頁只是它的**直達入口**：
//   🔴 v3.29：**不提供會籍期間內升級**；頁面標題與文案改稱「會籍方案」，網址 `/member/upgrade/` 為相容外流網址與深連結而保留。
//   - 已登入 → 直接顯示會員中心（預設分頁就是「我的會籍」，購買／續約區塊在裡面）。規劃書 §8.2「已登入者直接進方案頁」。
//   - 未登入 → 權益對照表（未登入亦可檢視，§3.14 ＋ 三處共用同一份資料）＋登入／加入會員，登入成功後
//     同一頁換成會員中心，不需要 `?next=`。
// 🔴 與會員中心同一套限制：noindex、`Cache-Control: no-store`（nuxt.config.ts routeRules `/zh/member/**` 已涵蓋）、
// 所有個人資料只在瀏覽器端載入。單元代號沿用會員中心 '14'（check-site-units-coverage 已明文排除，GEO-02）。
definePageMeta({ nav: '', unit: '14', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const clubAssets = computed(() => getClubAssets(config.public.club))
const clubNameEn = computed(() => getShopClubNameEn(config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const { isLoggedIn, restored, restore } = useMemberSession()

useSeoMeta({
  title: computed(() => (isEn.value ? getShopSeoEn('upgrade', clubNameEn.value).title : `會籍方案｜會員中心｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? getShopSeoEn('upgrade', clubNameEn.value).description : `${clubAssets.value.nameZh}會籍方案的權益對照與入會申請入口。`)),
  robots: 'noindex, nofollow',
})

onMounted(() => { restore() })

async function onLoggedOut() {
  await navigateTo(lp('/zh/member/'))
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/member/')">{{ tx('會員中心', 'Member Centre') }}</a></li>
      <li aria-current="page">{{ tx('會籍方案', 'Membership plans') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">14</span>
  <div class="container">
    <p class="page-hero__eyebrow">Membership Plans</p>
    <h1>{{ tx('會籍方案', 'Membership plans') }}<span v-if="!isEn" class="en">Membership Plans</span></h1>
    <p class="page-hero__lede">{{ tx('比較各會籍方案的權益，登入後即可在「我的會籍」送出入會申請，由工作人員與您聯繫完成收款與開通。', 'Compare the benefits of each membership plan. After signing in you can submit a membership request under My Memberships, and our staff will contact you to complete payment and activation.') }}</p>
  </div>
</section>

<section class="band member-band" aria-labelledby="upgrade-title">
  <div class="band-inner container">
    <h2 id="upgrade-title" class="visually-hidden">{{ tx('會籍方案', 'Membership plans') }}</h2>

    <noscript><p class="mc-alert mc-alert--info">{{ tx('會員中心需要啟用 JavaScript 才能使用。', 'The Member Centre requires JavaScript to be enabled.') }}</p></noscript>
    <p v-if="!restored" class="mc-empty" role="status">{{ tx('確認登入狀態中…', 'Checking your sign-in status…') }}</p>

    <MemberDashboard v-else-if="isLoggedIn" @logged-out="onLoggedOut" />

    <template v-else>
      <ContentMembershipBenefits />
      <h2 class="section-title" style="margin-top:4rem;">{{ tx('登入後送出入會申請', 'Sign in to submit a membership request') }}</h2>
      <MemberAuthPanel />
      <div class="pending-note" style="margin-top:2rem;">
        {{ tx('會費採 LINE Pay 收款連結與現場收款，網頁只接受入會申請、不收款，不走商店結帳（見規劃書 3.14）。', 'Membership fees are collected through a LINE Pay payment link or in person. This website only accepts membership requests and does not take payment, and it does not use shop checkout.') }}
      </div>
    </template>
  </div>
</section>
</template>
