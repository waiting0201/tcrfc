<script setup lang="ts">
// app/components/FormStatusBanner.vue — 10 表單中心（7 類）共用的送出結果提示（S1-17）。
//
// 成功／失敗兩種狀態的視覺呈現集中在這裡，7 張表單頁面共用一份，避免七份 <style> 各自寫一次
// 同樣的東西（同樣的理由見 app/components/news/NewsListBody.vue 檔頭）。**不是 mockup 原有的
// 區塊**——mockup 時代表單完全沒有送出後的畫面（`action=""`，見各頁檔頭註解「純靜態站，
// 實際送出由後端接手」），這是本輪（S1-17）補齊「表單真的能送出」時新增的必要 UI，不是
// 「改寫版型」；沒有這塊，使用者送出後不會知道成功或失敗。
import type { FormSubmitStatus } from '~/composables/useFormSubmit'

defineProps<{
  status: FormSubmitStatus
  errorMessage?: string
  /** 成功訊息文字，各表單的「送出後會發生什麼事」文案略有差異（收件單位不同），
   * 由呼叫端傳入，這裡不寫死。 */
  successMessage: string
}>()
</script>

<template>
  <div v-if="status === 'success'" class="form-status form-status--success" role="status">
    <p>{{ successMessage }}</p>
  </div>
  <div v-else-if="status === 'error'" class="form-status form-status--error" role="alert">
    <p>{{ errorMessage }}</p>
  </div>
</template>

<style>
/* 共用於 7 張表單頁面（10.1–10.7），集中一份定義，不逐頁複製。 */
.form-status{ padding:1.25rem 1.5rem; margin-bottom:1.75rem; font-size:.9rem; line-height:1.7; }
.form-status--success{ background:var(--paper-2); border-left:4px solid var(--brand-aa); color:var(--text); }
.form-status--error{ background:var(--paper-2); border-left:4px solid var(--brand-deep); color:var(--brand-deep); font-weight:700; }
</style>
