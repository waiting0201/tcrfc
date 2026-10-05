<script setup lang="ts">
// app/components/news/NewsFilterForm.vue — 標籤／年月篩選＋關鍵字搜尋表單
// 6 頁共用（news/index／club／camps-events／community／international／match）；
// news/index 另有分類篩選鈕，年月搜尋欄位結構相同但與分類鈕包在同一個 toolbar，
// 為了不把 news/index 的 cat-tabs 邏輯也塞進這個元件，index 頁另外自己組（見 news/index.vue）。
// academy／media／player-stories 3 頁沒有這個表單（原 mockup 沒有，資料量太少不需要）。
//
// 🔴 S1-17 新增「標籤」下拉（規劃書 3.7「列表頁：分類 Tab、標籤篩選、年月篩選、關鍵字搜尋」，
// 這個表單原本只做到年月與關鍵字）。`tags` 選項清單由呼叫端算好傳入（見
// app/utils/news.ts 的 newsDistinctTags()），本元件不重新計算——單一來源。
import type { NewsTagOption } from '~/utils/news'

withDefaults(defineProps<{ years: string[]; months: string[]; tags?: NewsTagOption[] }>(), { tags: () => [] })
const tag = defineModel<string>('tag', { default: '' })
const year = defineModel<string>('year', { default: '' })
const month = defineModel<string>('month', { default: '' })
const search = defineModel<string>('search', { default: '' })
// 英文版（主站 /en/）文案：短字串用 tx()，月份名稱用 newsMonthLabelEn()
const { isEn, tx } = useLocale()
</script>

<template>
  <form class="filter-row" role="search" onsubmit="return false;" :aria-label="tx('標籤、年月篩選與關鍵字搜尋', 'Filter by tag, year and month, or search by keyword')">
    <div v-if="tags.length > 0" class="filter-field">
      <label for="filter-tag">{{ tx('標籤', 'Tag') }}</label>
      <select id="filter-tag" v-model="tag">
        <option value="">{{ tx('全部標籤', 'All tags') }}</option>
        <option v-for="t in tags" :key="t.slug" :value="t.slug">{{ t.name }}</option>
      </select>
    </div>
    <div class="filter-field">
      <label for="filter-year">{{ tx('年份', 'Year') }}</label>
      <select id="filter-year" v-model="year">
        <option value="">{{ tx('全部年份', 'All years') }}</option>
        <option v-for="y in years" :key="y" :value="y">{{ y }}</option>
      </select>
    </div>
    <div class="filter-field">
      <label for="filter-month">{{ tx('月份', 'Month') }}</label>
      <select id="filter-month" v-model="month">
        <option value="">{{ tx('全部月份', 'All months') }}</option>
        <option v-for="m in months" :key="m" :value="m">{{ isEn ? newsMonthLabelEn(m) : newsMonthLabel(m) }}</option>
      </select>
    </div>
    <div class="filter-field filter-field--search">
      <label for="filter-search">{{ tx('關鍵字搜尋', 'Keyword search') }}</label>
      <input id="filter-search" v-model="search" type="search" :placeholder="tx('搜尋文章標題…', 'Search article titles…')" autocomplete="off">
    </div>
  </form>
</template>
