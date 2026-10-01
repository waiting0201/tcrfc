<script setup lang="ts">
// app/pages/m/[token].vue — 電子會員卡公開驗證頁（`/m/{token}`，主站 §3.14「電子會員卡與折扣使用方式」，S2-11）
//
// 店家掃會員卡 QR 開到這一頁，**目視**確認「有效／已過期」。🔴 約束（規劃書與後端契約）：
//   - 只顯示後端回的五個欄位：姓名首字、會員編號、層級、有效／已過期。**不得新增任何欄位**（尤其「適用球隊」：
//     token 已隱含俱樂部，掃出來就是那一隊的會籍狀態）。前台不補算、不加註。
//   - 查無、已撤銷、帳號停用或刪除一律 404（不分原因），畫面用同一句話，不透露原因。
//   - 不得快取：頁面回應 `Cache-Control: no-store`（nuxt.config.ts routeRules `/m/**`），後端回應也是 no-store；
//     這裡用 SSR 取資料（店員的手機要立刻看到結果、不依賴 JS），每次請求都直打後端，不經任何快取層。
//   - `noindex`（token 是機密連結，不得被搜尋引擎收錄）。
// 語系：QR 內容沒有語系前綴，預設繁中；`?lang=en` 切換英文。這一頁刻意不屬於 /zh/、/en/ 孿生路由。
import type { CardVerification } from '#shared/utils/member'

definePageMeta({ nav: '' })

const route = useRoute()
const token = String(route.params.token ?? '')
const lang = computed<'zh' | 'en'>(() => (route.query.lang === 'en' ? 'en' : 'zh'))

const T = computed(() => lang.value === 'en'
  ? { title: 'Membership card verification', h1: 'Membership Card', name: 'Name', no: 'Member No.', tier: 'Tier', status: 'Status', bad: 'Card not found or no longer valid', badNote: 'This QR code is invalid, has been replaced, or the account is no longer active.', note: 'Visual check only. This page shows no other personal data.' }
  : { title: '電子會員卡驗證', h1: '電子會員卡驗證', name: '姓名', no: '會員編號', tier: '層級', status: '狀態', bad: '查無此會員卡，或已失效', badNote: '此 QR Code 無效、已被重新產生，或帳號已停用。', note: '本頁僅供目視查驗，不顯示其他個人資料。' })

useSeoMeta({
  title: computed(() => T.value.title),
  robots: 'noindex, nofollow',
})

// `server: true`＋固定 key：SSR 時直接取資料；key 帶 token，避免不同卡互相讀到對方的 payload 快取
const { data, error } = await useFetch<CardVerification>(`/api/backend/m/${encodeURIComponent(token)}`, {
  query: { lang: lang.value },
  key: `card-verify-${token}-${lang.value}`,
  // 驗證結果隨時可能變（撤銷、過期），不得沿用 payload 內的舊值：每次客戶端導覽也重新取
  getCachedData: () => undefined,
})

if (error.value && import.meta.server) {
  const event = useRequestEvent()
  if (event) setResponseStatus(event, 404)
}
const valid = computed(() => data.value?.status === 'valid')
</script>

<template>
<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Member Card</p>
    <h1>{{ T.h1 }}</h1>
  </div>
</section>

<section class="band member-band" aria-labelledby="verify-title">
  <div class="band-inner container">
    <h2 id="verify-title" class="visually-hidden">{{ T.title }}</h2>

    <div v-if="data" class="verify-card" :class="valid ? 'verify-card--valid' : 'verify-card--bad'" role="status">
      <p class="verify-card__status">{{ data.statusLabel }}</p>
      <dl>
        <div><dt>{{ T.name }}</dt><dd>{{ data.nameInitial }}</dd></div>
        <div><dt>{{ T.no }}</dt><dd>{{ data.memberNo }}</dd></div>
        <div><dt>{{ T.tier }}</dt><dd>{{ data.tierLabel }}</dd></div>
        <div><dt>{{ T.status }}</dt><dd>{{ data.statusLabel }}</dd></div>
      </dl>
    </div>
    <div v-else class="verify-card verify-card--bad" role="status">
      <p class="verify-card__status">{{ T.bad }}</p>
      <p class="mc-note">{{ T.badNote }}</p>
    </div>

    <p class="mc-note mc-note--small" style="text-align:center;margin-top:1.5rem;">{{ T.note }}</p>
  </div>
</section>
</template>
