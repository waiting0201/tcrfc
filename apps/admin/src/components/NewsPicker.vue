<script setup lang="ts">
/**
 * 關聯報導挑選器（贊助故事、慈善計畫、球迷會活動回顧、抽獎公布稿共用）。
 * 用「新聞挑選搜尋」端點（關鍵字＋分頁），舊文章翻頁也選得到，不受「一次最多 100 篇」限制。
 * 已選取的識別碼由本元件自行解回標題（外層只要給 `v-model`，可另給 `seed` 預先提供標題）。
 * 前台只顯示已發布的報導；這裡草稿也能選，只是在標籤上標明。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { lookupNews, type NewsLookupItemDto } from '@/api/adminNews'
import { useBreakpoint } from '@/composables/useBreakpoint'

const props = withDefaults(
  defineProps<{
    modelValue: string[]
    disabled?: boolean
    /** 已知標題的項目（詳情回應附的關聯文章），省得再查一次 */
    seed?: { id: string; label: string; status?: string }[]
    emptyText?: string
  }>(),
  { disabled: false, seed: () => [], emptyText: '還沒有選取任何報導' },
)
const emit = defineEmits<{ (e: 'update:modelValue', value: string[]): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)

const labels = reactive(new Map<string, { label: string; draft: boolean }>())
function remember(item: { id: string; slug?: string; titleZh?: string | null; label?: string; status?: string }) {
  labels.set(item.id, { label: item.label || item.titleZh || item.slug || '（未命名）', draft: !!item.status && item.status !== 'published' })
}
watch(() => props.seed, (seed) => seed.forEach(remember), { immediate: true, deep: true })

// 已選但還沒有標題的識別碼：用 ids 解回標題（一次最多 200 個）
const resolving = ref(false)
watch(
  () => [props.modelValue, club.value] as const,
  async ([ids]) => {
    const missing = ids.filter((id) => !labels.has(id))
    if (missing.length === 0) return
    resolving.value = true
    try {
      for (let i = 0; i < missing.length; i += 200) {
        const page = await lookupNews(club.value, { ids: missing.slice(i, i + 200), pageSize: 50 })
        page.items.forEach(remember)
      }
    } catch {
      // 解不回標題不影響儲存，畫面以「（無法顯示標題）」代替
    } finally {
      resolving.value = false
    }
  },
  { immediate: true, deep: true },
)

const selected = computed(() =>
  props.modelValue.map((id) => ({ id, ...(labels.get(id) ?? { label: resolving.value ? '載入中…' : '（無法顯示標題）', draft: false }) })),
)

function removeOne(id: string) {
  emit('update:modelValue', props.modelValue.filter((x) => x !== id))
}

// ── 挑選視窗 ──
const open = ref(false)
const keyword = ref('')
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)
const rows = ref<NewsLookupItemDto[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const draft = ref<string[]>([])

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await lookupNews(club.value, { keyword: keyword.value.trim() || undefined, page: page.value, pageSize: pageSize.value })
    rows.value = result.items
    total.value = result.totalCount
    result.items.forEach(remember)
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = error instanceof AdminApiError ? error.message : '報導清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

function openDialog() {
  draft.value = [...props.modelValue]
  keyword.value = ''
  page.value = 1
  open.value = true
  load()
}
function search() {
  page.value = 1
  load()
}
function toggle(id: string, checked: boolean) {
  draft.value = checked ? [...draft.value, id] : draft.value.filter((x) => x !== id)
}
function confirm() {
  if (draft.value.length > 200) {
    ElMessage.warning('一次最多關聯 200 篇報導')
    return
  }
  emit('update:modelValue', draft.value)
  open.value = false
}
</script>

<template>
  <div class="news-picker">
    <div v-if="selected.length > 0" class="news-picker__tags">
      <el-tag
        v-for="item in selected"
        :key="item.id"
        :closable="!disabled"
        :type="item.draft ? 'info' : undefined"
        class="news-picker__tag"
        @close="removeOne(item.id)"
      >
        {{ item.label }}{{ item.draft ? '（尚未發布）' : '' }}
      </el-tag>
    </div>
    <p v-else class="news-picker__empty">{{ emptyText }}</p>
    <el-button v-if="!disabled" size="small" @click="openDialog">挑選報導</el-button>

    <el-dialog v-model="open" title="挑選關聯報導" width="640px" class="news-picker__dialog" :close-on-click-modal="false">
      <div class="news-picker__bar">
        <el-input v-model="keyword" placeholder="搜尋標題（中文或英文）" clearable @keyup.enter="search" @clear="search">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-button type="primary" @click="search">搜尋</el-button>
      </div>
      <el-skeleton v-if="loading" :rows="5" animated />
      <el-empty v-else-if="loadError" :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
      <el-empty v-else-if="rows.length === 0" description="找不到符合的報導" :image-size="64" />
      <ul v-else class="news-picker__list">
        <li v-for="row in rows" :key="row.id" class="news-picker__row">
          <el-checkbox :model-value="draft.includes(row.id)" @change="(v: unknown) => toggle(row.id, v === true)">
            <span class="news-picker__title">{{ row.titleZh || row.titleEn || row.slug }}</span>
          </el-checkbox>
          <span class="news-picker__meta">
            <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
            <el-tag v-if="row.isShared" size="small" type="info">兩隊共用</el-tag>
          </span>
        </li>
      </ul>
      <el-pagination
        v-if="total > pageSize"
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        :layout="isMobile ? 'prev, pager, next' : 'total, prev, pager, next'"
        :small="isMobile"
        :pager-count="5"
        class="news-picker__pager"
        @current-change="load"
      />
      <p class="news-picker__hint">已勾選 {{ draft.length }} 篇；翻頁後勾選會保留。前台只顯示已發布的報導。</p>
      <template #footer>
        <el-button @click="open = false">取消</el-button>
        <el-button type="primary" @click="confirm">確定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.news-picker__tags { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 8px; }
.news-picker__tag { max-width: 100%; height: auto; white-space: normal; line-height: 1.5; padding: 2px 8px; }
.news-picker__empty { margin: 0 0 8px; font-size: 13px; color: var(--admin-text-tertiary); }
.news-picker__bar { display: flex; gap: 8px; margin-bottom: 12px; }
.news-picker__list { list-style: none; margin: 0; padding: 0; }
.news-picker__row { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 6px 0; border-bottom: 1px solid var(--admin-border); min-width: 0; }
.news-picker__title { white-space: normal; word-break: break-word; }
.news-picker__meta { display: flex; gap: 4px; flex-shrink: 0; }
.news-picker__pager { margin-top: 10px; justify-content: center; }
.news-picker__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } .news-picker__row { flex-direction: column; align-items: flex-start; } }
</style>
