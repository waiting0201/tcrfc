<script setup lang="ts">
/**
 * 多語系 → 翻譯狀態總覽（規劃書 §4.9「以矩陣列出每筆內容的 zh／en 完成狀態，可篩選『缺英文』」）。
 * 涵蓋 9 類內容（新聞、常見問題、課程、球員、教練、慈善計畫、夥伴、贊助商、首頁輪播）；「完成」＝該語系的
 * 翻譯存在且主要文字欄位（標題／名稱／問題）不是空的。唯讀，沒有編輯動作——要補翻譯請到各內容的編輯頁。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { AdminApiError } from '@/api/http'
import { getTranslationOverview, type AdminLocale, type TranslationOverview } from '@/api/adminSiteSettings'

const props = defineProps<{ club: string; locales: AdminLocale[] }>()

const PAGE_SIZE = 50

const data = ref<TranslationOverview | null>(null)
const state = ref<'loading' | 'ready' | 'error'>('loading')
const errorMessage = ref('')
const typeFilter = ref('')
const missingFilter = ref('')
const keyword = ref('')
const page = ref(1)

const localeName = (code: string): string => props.locales.find((l) => l.code === code)?.name ?? code

const nonDefaultLocales = computed(() => (data.value?.locales ?? []).filter((c) => props.locales.find((l) => l.code === c)?.isDefault !== true))
const typeOptions = computed(() => data.value?.summary.map((s) => ({ value: s.type, label: s.typeLabel })) ?? [])

let seq = 0
async function load() {
  const my = ++seq
  state.value = 'loading'
  try {
    const result = await getTranslationOverview(props.club, {
      type: typeFilter.value || undefined,
      missing: missingFilter.value || undefined,
      keyword: keyword.value.trim() || undefined,
      page: page.value,
      pageSize: PAGE_SIZE,
    })
    if (my !== seq) return
    data.value = result
    state.value = 'ready'
  } catch (error) {
    if (my !== seq) return
    errorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    state.value = 'error'
  }
}
onMounted(load)
watch(() => props.club, () => { page.value = 1; void load() })
watch([typeFilter, missingFilter], () => { page.value = 1; void load() })
watch(page, load)

let keywordTimer: ReturnType<typeof setTimeout> | undefined
function onKeyword() {
  clearTimeout(keywordTimer)
  keywordTimer = setTimeout(() => { page.value = 1; void load() }, 350)
}
</script>

<template>
  <div class="trans-overview">
    <el-skeleton v-if="state === 'loading' && !data" :rows="6" animated />
    <el-empty v-else-if="state === 'error'" :image-size="64" :description="errorMessage">
      <el-button type="primary" @click="load">重新載入</el-button>
    </el-empty>
    <template v-else-if="data">
      <el-row :gutter="12" class="trans-overview__summary">
        <el-col v-for="s in data.summary" :key="s.type" :xs="12" :sm="8" :lg="6">
          <el-card shadow="never" class="trans-overview__card">
            <div class="trans-overview__card-title">{{ s.typeLabel }}</div>
            <div class="trans-overview__card-total">共 {{ s.total }} 筆</div>
            <div v-for="code in nonDefaultLocales" :key="code" class="trans-overview__card-missing" :class="{ 'trans-overview__card-missing--warn': (s.missing[code] ?? 0) > 0 }">
              缺{{ localeName(code) }}：{{ s.missing[code] ?? 0 }}
            </div>
          </el-card>
        </el-col>
      </el-row>

      <div class="trans-overview__filters">
        <el-select v-model="typeFilter" clearable placeholder="全部類型" aria-label="內容類型">
          <el-option v-for="o in typeOptions" :key="o.value" :label="o.label" :value="o.value" />
        </el-select>
        <el-select v-model="missingFilter" clearable placeholder="全部狀態" aria-label="翻譯狀態">
          <el-option v-for="code in nonDefaultLocales" :key="code" :label="`只看缺${localeName(code)}`" :value="code" />
        </el-select>
        <el-input v-model="keyword" clearable placeholder="搜尋標題或名稱" aria-label="搜尋標題或名稱" @input="onKeyword" @clear="onKeyword" />
      </div>

      <div v-loading="state === 'loading'">
        <el-empty v-if="data.items.length === 0" description="沒有符合條件的內容" :image-size="64" />
        <el-table v-else :data="data.items" row-key="id" border size="small">
          <el-table-column label="類型" prop="typeLabel" width="110" />
          <el-table-column label="內容" min-width="220">
            <template #default="{ row }">
              {{ row.label }}
              <el-tag v-if="row.isShared" size="small" type="info" class="trans-overview__tag">兩隊共用</el-tag>
            </template>
          </el-table-column>
          <el-table-column v-for="code in data.locales" :key="code" :label="localeName(code)" width="110" align="center">
            <template #default="{ row }">
              <el-tag v-if="row.done[code]" size="small" type="success">已完成</el-tag>
              <el-tag v-else size="small" type="warning">尚缺</el-tag>
            </template>
          </el-table-column>
        </el-table>
        <el-pagination
          v-if="data.totalCount > PAGE_SIZE"
          v-model:current-page="page"
          class="trans-overview__pager"
          background
          layout="prev, pager, next, total"
          :page-size="PAGE_SIZE"
          :total="data.totalCount"
        />
      </div>
    </template>
  </div>
</template>

<style scoped>
.trans-overview { min-width: 0; }
.trans-overview__summary { margin-bottom: 8px; }
.trans-overview__card { margin-bottom: 12px; --el-card-padding: 12px; }
.trans-overview__card-title { font-weight: 600; }
.trans-overview__card-total { font-size: 12px; color: var(--el-text-color-secondary); margin: 2px 0 6px; }
.trans-overview__card-missing { font-size: 13px; }
.trans-overview__card-missing--warn { color: var(--el-color-warning); font-weight: 600; }
.trans-overview__filters { display: flex; flex-wrap: wrap; gap: 8px; margin: 4px 0 12px; }
.trans-overview__filters > * { min-width: 160px; flex: 1 1 160px; max-width: 280px; }
.trans-overview__tag { margin-left: 6px; }
.trans-overview__pager { margin-top: 12px; justify-content: flex-end; flex-wrap: wrap; }
</style>
