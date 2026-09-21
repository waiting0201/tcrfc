<script setup lang="ts">
// app/components/news/NewsFilterForm.vue — 年月篩選＋關鍵字搜尋表單
// 5 頁共用（club／camps-events／community／international／match）；
// news/index 另有分類篩選鈕，年月搜尋欄位結構相同但與分類鈕包在同一個 toolbar，
// 為了不把 news/index 的 cat-tabs 邏輯也塞進這個元件，index 頁另外自己組（見 news/index.vue）。
// academy／media／player-stories 3 頁沒有這個表單（原 mockup 沒有，資料量太少不需要）。
defineProps<{ years: string[]; months: string[] }>()
const year = defineModel<string>('year', { default: '' })
const month = defineModel<string>('month', { default: '' })
const search = defineModel<string>('search', { default: '' })
</script>

<template>
  <form class="filter-row" role="search" onsubmit="return false;" aria-label="年月篩選與關鍵字搜尋">
    <div class="filter-field">
      <label for="filter-year">年份</label>
      <select id="filter-year" v-model="year">
        <option value="">全部年份</option>
        <option v-for="y in years" :key="y" :value="y">{{ y }}</option>
      </select>
    </div>
    <div class="filter-field">
      <label for="filter-month">月份</label>
      <select id="filter-month" v-model="month">
        <option value="">全部月份</option>
        <option v-for="m in months" :key="m" :value="m">{{ newsMonthLabel(m) }}</option>
      </select>
    </div>
    <div class="filter-field filter-field--search">
      <label for="filter-search">關鍵字搜尋</label>
      <input id="filter-search" v-model="search" type="search" placeholder="搜尋文章標題…" autocomplete="off">
    </div>
  </form>
</template>
