<script setup lang="ts">
// app/pages/zh/newsletter/unsubscribe/index.vue — G-09 電子報退訂頁（H 批，2026-10-02）
//
// 對應 apps/api `POST /api/v1/{club}/newsletter/unsubscribe`，body `{ token }`（apps/api/README.md「H 批」§3）。
// 退訂連結由電子報寄送平台為每位訂閱者產生：`/{lang}/newsletter/unsubscribe/?token=…`（憑證內含俱樂部與信箱、不過期、只能退訂）。
//
// 設計：
//   - 🔴 **不在載入頁面時自動退訂**——信箱服務與資安閘道常會預先「點開」信件裡的連結做掃描，自動退訂會讓人在不知情下被退訂；
//     一律要按「確認退訂」才送出（POST）。
//   - 端點冪等，找不到名單列也回成功（不透露名單狀態），所以成功文案只說「已完成退訂」。
//   - 憑證無效（含拿別的俱樂部的憑證）→ 400，顯示「連結無效或已失效」。
//   - noindex、nofollow（連結帶憑證）。
definePageMeta({ nav: '', enReady: true, enReadyBw: true })

const route = useRoute()
const { lp, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const clubNameZh = computed(() => (isEn.value ? (club === 'bw' ? BW_NAME_EN : CLUB_NAME_EN) : getClubAssets(club).nameZh))

useSeoMeta({
  title: computed(() => (isEn.value ? `Unsubscribe from the newsletter | ${clubNameZh.value}` : `取消訂閱電子報｜${clubNameZh.value}`)),
  description: computed(() => (isEn.value ? `Unsubscribe from the ${clubNameZh.value} newsletter.` : `取消訂閱${clubNameZh.value}電子報。`)),
  robots: 'noindex, nofollow',
})

const token = computed(() => (typeof route.query.token === 'string' ? route.query.token.trim() : ''))

type Phase = 'idle' | 'submitting' | 'success' | 'error'
const phase = ref<Phase>('idle')
const errorMessage = ref('')

async function onConfirm() {
  if (phase.value === 'submitting' || !token.value) return
  phase.value = 'submitting'
  errorMessage.value = ''
  try {
    await $fetch(`/api/backend/${club}/newsletter/unsubscribe`, { method: 'POST', body: { token: token.value } })
    phase.value = 'success'
  }
  catch (err: unknown) {
    phase.value = 'error'
    const status = (err as { statusCode?: number, status?: number } | null)?.statusCode ?? (err as { status?: number } | null)?.status
    errorMessage.value = status === 429
      ? tx('操作次數過多，請稍候幾分鐘再試。', 'Too many attempts. Please wait a few minutes and try again.')
      : (status === 400 || status === 404
          ? tx('這個取消訂閱連結無效或已失效。請使用最新一封電子報裡的連結，或聯絡我們協助處理。', 'This unsubscribe link is invalid or has expired. Please use the link in the most recent newsletter, or contact us for help.')
          : tx('取消訂閱失敗，請稍後再試；若持續發生，請聯絡我們協助處理。', 'We could not unsubscribe you. Please try again later; if the problem continues, please contact us for help.'))
  }
}
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('取消訂閱電子報', 'Unsubscribe from the Newsletter') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">Newsletter</p>
    <h1><template v-if="isEn">Unsubscribe from the Newsletter</template><template v-else>取消訂閱電子報<span class="en">Unsubscribe</span></template></h1>
    <p class="page-hero__lede">{{ tx(`不再收到${clubNameZh}的電子報。`, `Stop receiving the ${clubNameZh} newsletter.`) }}</p>
  </div>
</section>

<section class="band unsub-band" aria-labelledby="unsub-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="unsub-title">{{ tx('取消訂閱', 'Unsubscribe') }}</h2>

    <div v-if="!token" class="form-status form-status--error" role="alert">
      <p v-if="isEn">We could not find the information needed to unsubscribe. Please click the &ldquo;Unsubscribe&rdquo; link at the bottom of the newsletter email, or <a :href="lp('/zh/join/general/')">contact us</a> for help.</p>
      <p v-else>找不到取消訂閱所需的資訊。請直接點選電子報信件底部的「取消訂閱」連結，或<a :href="lp('/zh/join/general/')">聯絡我們</a>協助處理。</p>
    </div>

    <div v-else-if="phase === 'success'" class="form-status form-status--success" role="status">
      <p v-if="isEn"><strong>You have been unsubscribed.</strong> You will no longer receive our newsletter.</p>
      <p v-else><strong>已完成取消訂閱。</strong>之後不會再寄送電子報給您。</p>
      <p><a class="btn btn--dark btn--sm" :href="lp('/zh/')">{{ tx('回到首頁', 'Back to the Homepage') }}</a></p>
    </div>

    <div v-else class="unsub-card">
      <div v-if="phase === 'error'" class="form-status form-status--error" role="alert"><p>{{ errorMessage }}</p></div>
      <p>{{ tx('按下「確認取消訂閱」後，我們會停止寄送電子報給這個信箱。', 'Once you select "Confirm unsubscribe", we will stop sending the newsletter to this email address.') }}</p>
      <button type="button" class="btn btn--primary" :disabled="phase === 'submitting'" @click="onConfirm">{{ phase === 'submitting' ? tx('處理中…', 'Processing…') : tx('確認取消訂閱', 'Confirm unsubscribe') }}</button>
    </div>
  </div>
</section>
</template>

<style>
.unsub-band{ padding-block:clamp(3rem,6vw,5rem); }
.unsub-card{ max-width:560px; display:flex; flex-direction:column; align-items:flex-start; gap:1.25rem; }
</style>
