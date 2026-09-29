<script setup lang="ts">
// app/components/HoneypotField.vue — 誘捕欄位（S1-17）。
//
// 對應 apps/api SubmitFormRequest.Website（apps/api/Features/Forms/FormDtos.cs）：正常訪客看不到
// 這個欄位、也不會填，機器人（尤其會無差別填滿表單所有 <input> 的簡易爬蟲）填了值即被後端安靜
// 判定為濫用、不寫入任何資料。這是 apps/api README 檔頭記錄的「本輪兩層不需要外部服務的防線」
// 之一（另一層是 Program.cs 的 Rate Limiting），本輪（S1-17）補上前台這一半。
//
// 用 .visually-hidden（tcrfc.css 既有全站共用 class，不是本頁新發明）而不是 type="hidden"——
// 後者部分基礎的表單填寫機器人反而會刻意跳過 hidden input（因為那是最常見的誘捕手法），
// 用視覺隱藏但仍是 type="text" 且存在於可見 DOM 樹裡的欄位更容易騙到不分析 CSS 的簡易腳本。
// tabindex="-1" 與 aria-hidden 避免鍵盤使用者或螢幕報讀器不小心跳進這個欄位。
const model = defineModel<string>({ default: '' })
</script>

<template>
  <div class="visually-hidden" aria-hidden="true">
    <label for="website">Leave this field blank</label>
    <input
      id="website"
      v-model="model"
      type="text"
      name="website"
      tabindex="-1"
      autocomplete="off"
    >
  </div>
</template>
