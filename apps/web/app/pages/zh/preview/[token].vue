<script setup lang="ts">
// app/pages/zh/preview/[token].vue — B1 靜態頁預覽（A-7，2026-10-06；規劃書 §4.2 B1「預覽連結（未發布可分享）」）
//
// 後台頁面編輯產生 `/{zh|en}/preview/{token}`（`apps/admin/.../PageEditView.vue`），本頁讀
// `GET /api/v1/pages/preview/{token}?lang=`（**沒有 {club} 路由段**：權杖本身就是授權，猜不到就看不到）。
// 🔴 預覽頁絕不可被索引：meta `noindex,nofollow`（全站 `/**` 的 X-Robots-Tag 之外再補，且 route rule 加 no-store）。
// 🔴 權杖視同密碼：不放進 title／canonical／JSON-LD／對外連結的 Referrer（全站 Referrer-Policy 已是 strict-origin-when-cross-origin，
//    且本頁沒有任何外部連結以外的 token 洩漏點）。權杖無效／不存在一律顯示同一句友善訊息，不洩漏是否曾經存在。
// 區塊以 `PageBlocks` 渲染（純文字、不 v-html）。後台頁面沒有標題欄位，預覽只顯示區塊內容。
import type { RawPageBlock } from '#shared/utils/page-blocks'

definePageMeta({ nav: '', enReady: true, enReadyBw: true })

const { lp, locale, tx } = useLocale()
const route = useRoute()
const mediaBaseUrl = useRuntimeConfig().public.mediaBaseUrl as string
const token = String(route.params.token ?? '').trim()
const tokenShapeOk = /^[A-Za-z0-9_-]{16,128}$/.test(token)

interface PreviewDto {
  pageId: string
  versionNo: number
  status: string
  slug: string
  seoTitle: string | null
  seoDescription: string | null
  blocks: RawPageBlock[]
}

const { data: preview } = await useFetch<PreviewDto | null>(`/api/backend/pages/preview/${encodeURIComponent(token)}`, {
  query: { lang: locale.value },
  key: `page-preview-${token.slice(0, 12)}-${locale.value}`,
  immediate: tokenShapeOk,
  default: () => null,
  // 任何錯誤（404、打不到）都視為「連結無效」，不讓錯誤頁顯示技術訊息
  onResponseError() { /* 交給 preview === null 的畫面 */ },
})

const blocks = computed(() => normalizePageBlocks(preview.value?.blocks, { locale: locale.value === 'en' ? 'en' : 'zh', mediaBaseUrl }))
const statusLabel = computed(() => {
  switch (preview.value?.status) {
    case 'draft': return tx('草稿', 'Draft')
    case 'scheduled': return tx('已排程', 'Scheduled')
    case 'published': return tx('已發布', 'Published')
    default: return null
  }
})
if (import.meta.server && !preview.value) {
  const event = useRequestEvent()
  if (event) setResponseStatus(event, 404)
}

useSeoMeta({
  title: computed(() => tx('頁面預覽', 'Page Preview')),
  description: computed(() => tx('後台頁面預覽（未公開）。', 'Admin page preview (not public).')),
  robots: 'noindex, nofollow',
})
</script>

<template>
<div class="preview-bar" role="status">
  <div class="container">
    <strong>{{ tx('預覽模式', 'Preview mode') }}</strong>
    <span>{{ tx('這是尚未公開的預覽，僅供確認內容與版面，請勿轉貼或對外分享。', 'This is a non-public preview for checking content and layout only. Please do not repost or share it publicly.') }}</span>
    <span v-if="preview" class="preview-bar__meta">{{ tx('版本', 'Version') }} {{ preview.versionNo }}<template v-if="statusLabel">{{ tx('｜頁面目前狀態：', ' | Page status: ') }}{{ statusLabel }}</template></span>
  </div>
</div>

<section v-if="preview" class="band" aria-labelledby="preview-title">
  <div class="band-inner container">
    <h1 id="preview-title" class="visually-hidden">{{ tx('頁面預覽', 'Page Preview') }}</h1>
    <ContentPageBlocks v-if="blocks.length" :blocks="blocks" />
    <p v-else class="is-pending">{{ tx('這個版本沒有可顯示的內容區塊。', 'This version has no content blocks to display.') }}</p>
  </div>
</section>

<section v-else class="band" aria-labelledby="preview-error-title">
  <div class="band-inner container prose">
    <h1 id="preview-error-title">{{ tx('預覽連結無法使用', 'This preview link is not available') }}</h1>
    <p>{{ tx('這個預覽連結可能輸入有誤，或已失效。請回到後台頁面編輯畫面，重新複製預覽連結。', 'The preview link may be mistyped or no longer valid. Please return to the page editor in the admin and copy the preview link again.') }}</p>
    <p><a class="btn btn--dark" :href="lp('/zh/')">{{ tx('回首頁', 'Back to Home') }}</a></p>
  </div>
</section>
</template>

<style>
.is-pending{ color:var(--muted); font-style:italic; }
.preview-bar{ background:#fff3bf; color:#5c4400; border-bottom:2px solid #e0b100; font-size:.88rem; }
.preview-bar .container{ display:flex; flex-wrap:wrap; gap:.4rem 1rem; align-items:center; padding-block:.6rem; }
.preview-bar__meta{ margin-left:auto; font-weight:700; }
</style>
