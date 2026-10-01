<script setup lang="ts">
// app/components/member/MemberQr.vue — QR Code（純前端產生，內容不離開瀏覽器）。
// 只在登入後的會員卡畫面使用（客戶端渲染），**不得在 SSR 輸出**：QR 內容含會員卡 token。
import qrcode from 'qrcode-generator'

const props = defineProps<{ value: string, label: string }>()

const svg = computed(() => {
  const qr = qrcode(0, 'M')
  qr.addData(props.value)
  qr.make()
  // createSvgTag 只輸出數字座標的 <svg><rect>，內容不含任何使用者輸入，可安全 v-html
  return qr.createSvgTag({ cellSize: 4, margin: 2, scalable: true })
})
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- qrcode-generator 只輸出數字座標的 svg/rect，不含使用者輸入 -->
  <div class="member-qr" role="img" :aria-label="label" v-html="svg" />
</template>

<style>
.member-qr{ background:#fff; padding:.6rem; line-height:0; border-radius:2px; }
.member-qr svg{ width:100%; height:auto; display:block; }
</style>
