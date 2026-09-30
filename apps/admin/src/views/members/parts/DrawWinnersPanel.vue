<script setup lang="ts">
/**
 * 中獎人與備取：以抽獎序號回填（實體抽獎在現場或直播由人工進行，系統不抽出）。
 * - 系統會比對序號是否存在於目前版本的名單，不符會被擋下。
 * - 備取：原中獎人逾期未領時遞補。「遞補」＝同一個序號再送一次，改成中獎並填獎項。
 * - 中獎人不能直接改成備取，請先取消中獎。首次回填中獎人時，活動狀態變為「已抽出」。
 * - 名單公布後再修改，必須填寫修改原因。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listRoster, removeWinners, saveWinners, type DrawDetailDto, type RosterRowDto } from '@/api/adminDraws'

const props = defineProps<{ draw: DrawDetailDto }>()
const emit = defineEmits<{ (e: 'changed'): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canUpdate } = useViewUpdatePermissions('member.draw')
const canReveal = usePermission('member.pii.reveal')
const club = computed(() => activeClubId.value)
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)
const canRecord = computed(() => canUpdate.value && props.draw.availableActions.includes('record_winners'))
const needReason = computed(() => props.draw.status === 'announced')

const rows = ref<RosterRowDto[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const selected = ref<number[]>([])
const hasRoster = computed(() => props.draw.rosterVersion > 0)

async function load() {
  if (!hasRoster.value) {
    rows.value = []
    return
  }
  loading.value = true
  loadError.value = null
  try {
    rows.value = (await listRoster(club.value, props.draw.id, { winnersOnly: true, page: 1, pageSize: 200 })).items
    selected.value = selected.value.filter((s) => rows.value.some((r) => r.serialNo === s))
  } catch (error) {
    rows.value = []
    loadError.value = errorText(error, '中獎名單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(() => [props.draw.id, props.draw.rosterVersion, props.draw.winnerCount, props.draw.backupCount], load)

const winners = computed(() => rows.value.filter((r) => r.isWinner))
const backups = computed(() => rows.value.filter((r) => r.isBackup))
function onSelectionChange(sel: RosterRowDto[]) {
  selected.value = sel.map((r) => r.serialNo)
}
function toggleSelected(serialNo: number, checked: boolean) {
  selected.value = checked ? [...selected.value, serialNo] : selected.value.filter((x) => x !== serialNo)
}

// ── 回填 ──
interface Entry {
  key: number
  serialNo: number | null
  prizeName: string
  isBackup: boolean
}
let keySeed = 0
const open = ref(false)
const saving = ref(false)
const formError = ref<string | null>(null)
const entries = ref<Entry[]>([])
const reason = ref('')
const pasteText = ref('')

function newEntry(partial: Partial<Entry> = {}): Entry {
  return { key: ++keySeed, serialNo: null, prizeName: '', isBackup: false, ...partial }
}
function openDialog(preset?: Partial<Entry>) {
  entries.value = [newEntry(preset)]
  reason.value = ''
  pasteText.value = ''
  formError.value = null
  open.value = true
}
/** 每行一筆：序號、獎項（以逗號、Tab 或空白分隔），行尾多一個「備取」就是備取。 */
function importPaste() {
  const parsed: Entry[] = []
  for (const line of pasteText.value.split(/\r?\n/)) {
    const text = line.trim()
    if (!text) continue
    const parts = text.split(/[,\t，\s]+/).filter(Boolean)
    const serial = Number(parts[0])
    if (!Number.isInteger(serial) || serial < 1) {
      formError.value = `無法辨識這一行：「${text}」，第一欄必須是序號`
      return
    }
    const isBackup = parts[parts.length - 1] === '備取' && parts.length > 1
    const prize = (isBackup ? parts.slice(1, -1) : parts.slice(1)).join(' ')
    parsed.push(newEntry({ serialNo: serial, prizeName: prize, isBackup }))
  }
  if (parsed.length === 0) return (formError.value = '沒有可匯入的內容')
  formError.value = null
  entries.value = parsed
  pasteText.value = ''
}
async function save() {
  formError.value = null
  const list = entries.value
  if (list.length === 0 || list.length > 200) return (formError.value = '一次回填 1 到 200 筆')
  const serials = new Set<number>()
  for (const e of list) {
    if (e.serialNo === null || !Number.isInteger(e.serialNo) || e.serialNo < 1) return (formError.value = '每一筆都要填寫抽獎序號')
    if (serials.has(e.serialNo)) return (formError.value = `序號 ${e.serialNo} 重複了`)
    serials.add(e.serialNo)
    if (!e.isBackup && !e.prizeName.trim()) return (formError.value = `序號 ${e.serialNo} 是中獎，必須填寫獎項`)
    if (e.prizeName.trim().length > 128) return (formError.value = '獎項名稱最多 128 字')
  }
  if (needReason.value && !reason.value.trim()) return (formError.value = '名單已公布，修改必須填寫原因')
  saving.value = true
  try {
    await saveWinners(club.value, props.draw.id, {
      winners: list.map((e) => ({ serialNo: e.serialNo!, prizeName: e.prizeName.trim(), isBackup: e.isBackup || undefined })),
      reason: reason.value.trim() || undefined,
    })
    open.value = false
    ElMessage.success('已回填')
    await load()
    emit('changed')
  } catch (error) {
    formError.value = errorText(error, '回填失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}

// ── 遞補 ──
async function promote(row: RosterRowDto) {
  openDialog({ serialNo: row.serialNo, prizeName: row.prizeName ?? '', isBackup: false })
}

// ── 取消中獎 ──
const removeOpen = ref(false)
const removeReason = ref('')
const removing = ref(false)
const removeError = ref<string | null>(null)
function openRemove() {
  if (selected.value.length === 0) return
  removeReason.value = ''
  removeError.value = null
  removeOpen.value = true
}
async function doRemove() {
  if (needReason.value && !removeReason.value.trim()) return (removeError.value = '名單已公布，修改必須填寫原因')
  try {
    await ElMessageBox.confirm(`確定要取消 ${selected.value.length} 位的中獎／備取標記嗎？他們已填的獎品發放資料會一併清除。`, '取消中獎', { confirmButtonText: '確定取消', cancelButtonText: '返回', type: 'warning' })
  } catch {
    return
  }
  removing.value = true
  try {
    await removeWinners(club.value, props.draw.id, { serialNos: selected.value, reason: removeReason.value.trim() || undefined })
    removeOpen.value = false
    selected.value = []
    ElMessage.success('已取消')
    await load()
    emit('changed')
  } catch (error) {
    removeError.value = errorText(error, '取消失敗，請稍後再試')
  } finally {
    removing.value = false
  }
}
</script>

<template>
  <div class="winners">
    <el-alert v-if="!hasRoster" type="info" show-icon :closable="false" title="要先產生並鎖定名單，才能回填中獎人。" />
    <template v-else>
      <div class="winners__row">
        <el-button v-if="canRecord" type="primary" @click="openDialog()">+ 回填中獎人／備取</el-button>
        <el-button v-if="canRecord" :disabled="selected.length === 0" @click="openRemove">取消勾選者的中獎／備取（{{ selected.length }}）</el-button>
        <span class="winners__hint">中獎 {{ winners.length }} 人・備取 {{ backups.length }} 人。實體抽獎由人工進行，這裡以序號回填。</span>
      </div>
      <p v-if="!canReveal" class="winners__hint">你的帳號沒有檢視完整個資的權限，姓名顯示為遮罩。</p>
      <p v-if="!canRecord && canUpdate" class="winners__hint">目前活動狀態不能回填中獎人。</p>

      <el-skeleton v-if="loading" :rows="4" animated />
      <el-empty v-else-if="loadError" :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
      <el-empty v-else-if="rows.length === 0" description="還沒有回填中獎人" :image-size="64" />
      <template v-else>
        <el-table v-if="!isMobile" :data="rows" row-key="serialNo" @selection-change="onSelectionChange">
          <el-table-column v-if="canRecord" type="selection" width="44" />
          <el-table-column label="抽獎序號" width="100" prop="serialNo" />
          <el-table-column label="會員編號" width="130" prop="memberNo" />
          <el-table-column label="姓名" min-width="110"><template #default="{ row }">{{ row.name || '—' }}</template></el-table-column>
          <el-table-column label="結果" width="90"><template #default="{ row }"><el-tag :type="row.isWinner ? 'success' : 'warning'" size="small">{{ row.isWinner ? '中獎' : '備取' }}</el-tag></template></el-table-column>
          <el-table-column label="獎項" min-width="160"><template #default="{ row }">{{ row.prizeName || '—' }}</template></el-table-column>
          <el-table-column v-if="canRecord" label="操作" width="100">
            <template #default="{ row }"><el-button v-if="row.isBackup" size="small" text type="primary" @click="promote(row)">遞補為中獎</el-button></template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="serialNo">
          <template #title="{ row }">
            <el-checkbox v-if="canRecord" :model-value="selected.includes(row.serialNo)" @change="(v: unknown) => toggleSelected(row.serialNo, v === true)" />
            序號 {{ row.serialNo }}・{{ row.name || '—' }}
          </template>
          <template #meta="{ row }"><el-tag :type="row.isWinner ? 'success' : 'warning'" size="small">{{ row.isWinner ? '中獎' : '備取' }}</el-tag><span>{{ row.prizeName || '—' }}</span></template>
          <template #actions="{ row }"><el-button v-if="canRecord && row.isBackup" size="small" text type="primary" @click="promote(row)">遞補為中獎</el-button></template>
        </MobileCardList>
      </template>
    </template>

    <el-dialog v-model="open" title="回填中獎人／備取" width="640px" :close-on-click-modal="false" class="winners__dialog">
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="winners__block" @close="formError = null" />
      <p class="winners__hint">序號必須存在於目前版本的名單；中獎必須填獎項，備取可以不填。「遞補」就是把備取的序號改成中獎並填獎項。</p>
      <div class="winners__entries">
        <div v-for="(e, i) in entries" :key="e.key" class="winners__entry">
          <el-input-number v-model="e.serialNo" :min="1" :controls="false" placeholder="序號" class="winners__serial" />
          <el-input v-model="e.prizeName" maxlength="128" placeholder="獎項名稱" class="winners__prize" />
          <el-checkbox v-model="e.isBackup">備取</el-checkbox>
          <el-button size="small" text type="danger" aria-label="移除這一筆" :disabled="entries.length === 1" @click="entries.splice(i, 1)"><el-icon><Delete /></el-icon></el-button>
        </div>
      </div>
      <el-button size="small" :disabled="entries.length >= 200" @click="entries.push(newEntry())">+ 再加一筆</el-button>
      <el-collapse class="winners__paste">
        <el-collapse-item title="一次貼上多筆" name="paste">
          <p class="winners__hint">每行一筆：序號、獎項（用逗號、Tab 或空白隔開），行尾加「備取」就是備取。貼上後按「帶入清單」會取代上面的清單。</p>
          <el-input v-model="pasteText" type="textarea" :rows="5" placeholder="例如：&#10;12 球衣一件&#10;305,簽名球&#10;88 備取" />
          <el-button size="small" class="winners__import" @click="importPaste">帶入清單</el-button>
        </el-collapse-item>
      </el-collapse>
      <el-form v-if="needReason" label-position="top" class="winners__reason"><el-form-item label="修改原因（名單已公布，必填）" required><el-input v-model="reason" maxlength="200" show-word-limit /></el-form-item></el-form>
      <template #footer>
        <el-button @click="open = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">儲存回填</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="removeOpen" title="取消中獎／備取" width="440px" :close-on-click-modal="false" class="winners__dialog">
      <el-alert v-if="removeError" :title="removeError" type="warning" show-icon class="winners__block" @close="removeError = null" />
      <p class="winners__hint">將取消 {{ selected.length }} 位的標記並清除他們的發放資料；全部取消後，活動狀態會回到「名單已鎖定」。</p>
      <el-input v-model="removeReason" maxlength="200" show-word-limit :placeholder="needReason ? '取消原因（名單已公布，必填）' : '取消原因（選填）'" />
      <template #footer>
        <el-button @click="removeOpen = false">返回</el-button>
        <el-button type="danger" :loading="removing" @click="doRemove">確定取消</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.winners__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 8px; }
.winners__hint { margin: 6px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.winners__block { margin-bottom: 12px; }
.winners__entries { display: flex; flex-direction: column; gap: 8px; margin-bottom: 10px; max-height: 320px; overflow-y: auto; }
.winners__entry { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.winners__serial { width: 110px; }
.winners__prize { flex: 1; min-width: 160px; }
.winners__paste { margin-top: 12px; }
.winners__import { margin-top: 8px; }
.winners__reason { margin-top: 12px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
