<script setup lang="ts">
/**
 * 「點擊後前往」連結選擇器（推播、公告條共用；稽核 docs/23 M-3）。
 * 兩步：先選「要前往哪裡」（賽程、會員卡、某一則最新消息…），目的地需要對象時再出現對象選擇器（可搜尋）。
 * 完整連結由伺服器組好、這裡原樣送出——畫面上不顯示任何程式用的連結字樣，也不讓人手打。
 * 編輯既有資料時由伺服器把已存的連結還原成「目的地＋對象」；還原不了的（舊的未填完連結、對象已下架）
 * 保留原值並明確提示重新選擇，不靜默清空（E-357）。
 * `incomplete`：選了需要對象的目的地卻還沒挑對象，或選了官網頁面卻還沒填網址——父層儲存前要擋下。
 */
import { computed, onMounted, ref, watch } from 'vue'
import {
  describeLink, listLinkDestinations, searchLinkTargets,
  type LinkDescriptionDto, type LinkDestinationDto, type LinkTargetDto,
} from '@/api/adminApp'

const NONE = '__none'
const WEB = '__web'
const props = defineProps<{ modelValue: string; disabled?: boolean }>()
const emit = defineEmits<{ (e: 'update:modelValue', v: string): void; (e: 'update:incomplete', v: boolean): void }>()

const destinations = ref<LinkDestinationDto[]>([])
const destKey = ref<string>(NONE)
const targets = ref<LinkTargetDto[]>([])
const targetLink = ref<string>('')
const webUrl = ref('')
const loadingTargets = ref(false)
const problem = ref<LinkDescriptionDto | null>(null)
const loadFailed = ref(false)
/** 最後一次由本元件送出的值：避免把自己送出的值當成外部變更再還原一次。 */
let lastEmitted = props.modelValue

const current = computed(() => destinations.value.find((d) => d.key === destKey.value) ?? null)
const needsTarget = computed(() => !!current.value?.targetKind)

/** 使用者動過選單才檢查是否填完：還原出來的舊連結（需要重新選擇的）不動它就能照原樣儲存。 */
const touched = ref(false)
watch([destKey, targetLink, webUrl, touched], () => {
  const incomplete = touched.value && ((needsTarget.value && !targetLink.value) || (destKey.value === WEB && !webUrl.value.trim()))
  emit('update:incomplete', incomplete)
})

function push(v: string) {
  lastEmitted = v
  emit('update:modelValue', v)
}

async function restore(link: string) {
  problem.value = null
  if (!link) {
    destKey.value = NONE
    targetLink.value = ''
    webUrl.value = ''
    return
  }
  try {
    const d = await describeLink(link)
    if (d.status === 'web') {
      destKey.value = WEB
      webUrl.value = link
    } else if (d.status === 'app' && d.destinationKey) {
      destKey.value = d.destinationKey
      if (d.targetLabel) {
        targets.value = [{ link, label: d.targetLabel, hint: null }]
        targetLink.value = link
      }
    } else {
      // 舊的未填完連結、App 開不了、對象已下架：保留原值，提示重新選擇（不靜默清空）
      problem.value = d
      destKey.value = d.destinationKey ?? NONE
      targetLink.value = ''
    }
  } catch {
    loadFailed.value = true
  }
}

onMounted(async () => {
  try {
    destinations.value = await listLinkDestinations()
  } catch {
    loadFailed.value = true
  }
  await restore(props.modelValue)
})
watch(() => props.modelValue, (v) => {
  if (v !== lastEmitted) {
    lastEmitted = v
    void restore(v)
  }
})

async function loadTargets(q: string) {
  const kind = current.value?.targetKind
  if (!kind) return
  loadingTargets.value = true
  try {
    const found = await searchLinkTargets(kind, q.trim() || undefined)
    const keep = targets.value.find((t) => t.link === targetLink.value)
    targets.value = keep && !found.some((t) => t.link === keep.link) ? [keep, ...found] : found
  } catch {
    // 搜尋失敗保留目前清單；選擇器下方的空白提示會讓使用者知道沒有結果
  } finally {
    loadingTargets.value = false
  }
}

function onDestination(key: string) {
  touched.value = true
  destKey.value = key
  problem.value = null
  targetLink.value = ''
  targets.value = []
  webUrl.value = ''
  const d = destinations.value.find((x) => x.key === key)
  if (key === NONE || key === WEB) push('')
  else if (d?.link) push(d.link)
  else {
    push('') // 還沒挑對象：先清空，incomplete 會擋住儲存
    void loadTargets('')
  }
}
function onTarget(link: string) {
  touched.value = true
  targetLink.value = link
  problem.value = null
  push(link)
}
function onWeb(v: string) {
  touched.value = true
  webUrl.value = v
  problem.value = null
  push(v.trim())
}
</script>

<template>
  <div class="alp">
    <el-alert v-if="loadFailed" type="warning" show-icon :closable="false" title="目的地清單載入失敗，請重新整理頁面後再試" class="alp__gap" />
    <el-alert v-if="problem" type="warning" show-icon :closable="false" class="alp__gap"
      :title="`這個連結需要重新選擇：${problem.message ?? ''}`" description="在下方重新選擇前，原本的連結維持不變。" />
    <el-select :model-value="destKey" :disabled="disabled" placeholder="要前往哪裡" style="width: 100%" @change="onDestination">
      <el-option :value="NONE" label="不設定（點了沒有動作）" />
      <el-option v-for="d in destinations" :key="d.key" :value="d.key" :label="d.label" />
      <el-option :value="WEB" label="官網頁面或其他網址" />
    </el-select>
    <el-select v-if="needsTarget" :model-value="targetLink" :disabled="disabled" filterable remote reserve-keyword clearable
      :remote-method="loadTargets" :loading="loadingTargets" placeholder="輸入關鍵字搜尋，再選擇要前往的對象"
      no-data-text="找不到符合的對象（只列出已公開的內容）" class="alp__gap-top" style="width: 100%"
      @change="onTarget" @visible-change="(v: boolean) => v && loadTargets('')">
      <el-option v-for="t in targets" :key="t.link" :value="t.link" :label="t.label">
        <span>{{ t.label }}</span>
        <span v-if="t.hint" class="alp__hint">{{ t.hint }}</span>
      </el-option>
    </el-select>
    <el-input v-if="destKey === WEB" :model-value="webUrl" :disabled="disabled" placeholder="完整網址（https:// 開頭）" class="alp__gap-top" @update:model-value="onWeb" />
  </div>
</template>

<style scoped>
.alp { width: 100%; }
.alp__gap { margin-bottom: 8px; }
.alp__gap-top { margin-top: 8px; }
.alp__hint { float: right; margin-left: 12px; font-size: 12px; color: var(--admin-text-tertiary); }
</style>
