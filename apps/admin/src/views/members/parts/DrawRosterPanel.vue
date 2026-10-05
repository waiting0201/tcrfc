<script setup lang="ts">
/**
 * 抽獎合格名單：試算、產生並鎖定、作廢重產、檢視與匯出公開版。
 * - 名單依會員編號升冪配發連號序號 1…N（一人一號），記錄基準時間、合格人數與名單雜湊（SHA-256）。
 * - 名單鎖定後如需重產，必須填作廢原因；舊版保留在版本歷程、不可刪除。已進入抽出階段後不可重產。
 * - 姓名依「檢視完整個資」權限遮罩；沒有這個權限時，搜尋只比對序號與會員編號。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import ExportPurposeDialog from '@/components/ExportPurposeDialog.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useViewUpdatePermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { eligibleCountText, exportDrawPublic, generateRoster, listRoster, previewRoster, type DrawDetailDto, type RosterRowDto } from '@/api/adminDraws'
import { formatDateTime } from '@/utils/dateTime'

const props = defineProps<{ draw: DrawDetailDto }>()
const emit = defineEmits<{ (e: 'changed'): void }>()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canView, canUpdate } = useViewUpdatePermissions('member.draw')
const canReveal = usePermission('member.pii.reveal')
const club = computed(() => activeClubId.value)
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)
const can = (a: string) => props.draw.availableActions.includes(a as never)

const version = ref<number | undefined>(undefined)
const revealed = ref(false)
const revealing = ref(false)
const filters = reactive({ keyword: '', winnersOnly: false })
const rows = ref<RosterRowDto[]>([])
const loading = ref(false)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
/** 姓名提示依 API 回傳的 isMasked 決定，不假設遮罩規則（沒有資料列時不下結論）。 */
const nameHint = computed(() => {
  if (rows.value.length === 0) return ''
  const masked = rows.value.filter((r) => r.isMasked).length
  if (masked === rows.value.length) return '姓名為遮罩顯示。'
  if (masked === 0) return '姓名顯示為完整姓名，請妥善保管，勿外流。'
  return '部分姓名為遮罩顯示。'
})
const hasRoster = computed(() => props.draw.rosterVersion > 0)

async function load() {
  if (!hasRoster.value) {
    rows.value = []
    total.value = 0
    return
  }
  loading.value = true
  loadError.value = null
  try {
    const result = await listRoster(club.value, props.draw.id, { version: version.value, keyword: filters.keyword.trim() || undefined, winnersOnly: filters.winnersOnly || undefined, reveal: revealed.value, page: page.value, pageSize: pageSize.value })
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorText(error, '名單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
async function setReveal(value: boolean) {
  if (value) {
    try {
      await ElMessageBox.confirm('即將顯示名單的完整姓名。這個動作會被系統記錄，請只在必要時使用；離開此頁後會回到遮罩狀態。', '顯示完整資料', { confirmButtonText: '確定顯示', cancelButtonText: '取消', type: 'warning' })
    } catch {
      return
    }
  }
  revealing.value = true
  revealed.value = value
  await load()
  if (value && loadError.value) revealed.value = false
  revealing.value = false
}
function applyFilter() {
  page.value = 1
  load()
}
onMounted(load)
watch(() => [props.draw.id, props.draw.rosterVersion, props.draw.winnerCount, props.draw.backupCount], () => {
  version.value = undefined
  page.value = 1
  revealed.value = false
  load()
})

// ── 試算 ──
const previewing = ref(false)
const previewResult = ref<{ asOf: string; eligibleCount: number } | null>(null)
async function doPreview() {
  previewing.value = true
  try {
    previewResult.value = await previewRoster(club.value, props.draw.id)
  } catch (error) {
    previewResult.value = null
    ElMessage.error(errorText(error, '試算失敗，請稍後再試'))
  } finally {
    previewing.value = false
  }
}

// ── 產生／重產 ──
const generating = ref(false)
async function doGenerate() {
  try {
    await ElMessageBox.confirm('產生名單後會鎖定：依會員編號配發抽獎序號，並記錄基準時間與名單雜湊。確定要產生並鎖定名單嗎？（需要已確認蒐集告知、已填基準時間與活動辦法）', '產生並鎖定名單', {
      confirmButtonText: '產生並鎖定', cancelButtonText: '取消', type: 'warning',
    })
  } catch {
    return
  }
  generating.value = true
  try {
    await generateRoster(club.value, props.draw.id)
    ElMessage.success('已產生並鎖定名單')
    emit('changed')
  } catch (error) {
    ElMessage.error(errorText(error, '產生名單失敗，請稍後再試'))
  } finally {
    generating.value = false
  }
}
const regenOpen = ref(false)
const regenReason = ref('')
const regenError = ref<string | null>(null)
async function doRegenerate() {
  if (!regenReason.value.trim()) return (regenError.value = '重產必須填寫作廢原因')
  regenError.value = null
  generating.value = true
  try {
    await generateRoster(club.value, props.draw.id, regenReason.value.trim())
    regenOpen.value = false
    ElMessage.success('已作廢舊版並產生新名單')
    emit('changed')
  } catch (error) {
    regenError.value = errorText(error, '重產失敗，請稍後再試')
  } finally {
    generating.value = false
  }
}

// ── 匯出公開版 ──
const exportOpen = ref(false)
const exporting = ref(false)
async function doExport(purpose: string) {
  exporting.value = true
  try {
    await exportDrawPublic(club.value, props.draw.id, purpose)
    exportOpen.value = false
    ElMessage.success('已匯出，檔案已下載')
  } catch (error) {
    ElMessage.error(errorText(error, '匯出失敗，請稍後再試'))
  } finally {
    exporting.value = false
  }
}
</script>

<template>
  <div class="roster">
    <el-alert v-if="!hasRoster" type="info" show-icon :closable="false" class="roster__block" title="還沒有產生名單。產生前請先確認：蒐集告知已確認、已填資格基準時間、活動辦法（中文）已填。可以先用「試算」看目前有多少合格會員。" />
    <el-card v-if="hasRoster" shadow="never" class="roster__block">
      <dl class="roster__dl">
        <div><dt>目前版本</dt><dd>第 {{ draw.rosterVersion }} 版</dd></div>
        <div><dt>合格人數</dt><dd>{{ eligibleCountText(draw.totalCount) }}</dd></div>
        <div><dt>資格基準時間</dt><dd>{{ formatDateTime(draw.snapshotAt) }}</dd></div>
        <div><dt>鎖定</dt><dd>{{ formatDateTime(draw.lockedAt) }}（{{ draw.lockedByName || '—' }}）</dd></div>
        <div><dt>名單雜湊</dt><dd class="roster__hash">{{ draw.rosterHash || '—' }}</dd></div>
      </dl>
      <p class="roster__hint">名單雜湊用來證明名單鎖定後沒有被更動；同時具備兩隊會籍的人在兩邊的名單各佔一個序號。</p>
    </el-card>

    <div class="roster__row roster__block">
      <template v-if="canUpdate">
        <el-button :loading="previewing" @click="doPreview">試算合格人數</el-button>
        <el-button v-if="can('generate_roster')" type="primary" :loading="generating" @click="doGenerate">產生並鎖定名單</el-button>
        <el-button v-if="can('regenerate_roster')" type="danger" plain @click="regenReason = ''; regenError = null; regenOpen = true">作廢並重產名單</el-button>
      </template>
      <el-button v-if="canView && hasRoster" @click="exportOpen = true">匯出公開版名單</el-button>
      <span v-if="previewResult" class="roster__preview">試算結果：以 {{ formatDateTime(previewResult.asOf) }} 為基準，共 {{ previewResult.eligibleCount }} 位合格會員（不寫入資料、不配發序號）。</span>
    </div>

    <template v-if="hasRoster">
      <el-card shadow="never" header="名單版本歷程" class="roster__block">
        <el-table :data="draw.versions" row-key="version" size="small">
          <el-table-column label="版本" width="70"><template #default="{ row }">第 {{ row.version }} 版</template></el-table-column>
          <el-table-column label="產生時間" width="150"><template #default="{ row }">{{ formatDateTime(row.generatedAt) }}</template></el-table-column>
          <el-table-column label="合格人數" width="90"><template #default="{ row }">{{ row.totalCount ?? '—' }}</template></el-table-column>
          <el-table-column label="狀態" min-width="180">
            <template #default="{ row }">
              <el-tag v-if="row.isCurrent" type="success" size="small">目前使用</el-tag>
              <span v-else-if="row.voidedAt">已作廢（{{ row.voidReason || '未填原因' }}）</span>
            </template>
          </el-table-column>
          <el-table-column label="檢視" width="90"><template #default="{ row }"><el-button size="small" text type="primary" :disabled="(version ?? draw.rosterVersion) === row.version" @click="version = row.version; applyFilter()">看這一版</el-button></template></el-table-column>
        </el-table>
      </el-card>

      <el-card shadow="never" class="roster__block">
        <div class="roster__row">
          <el-input v-model="filters.keyword" :placeholder="canReveal ? '搜尋序號、會員編號或姓名' : '搜尋序號或會員編號'" clearable class="roster__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
            <template #prefix><el-icon><Search /></el-icon></template>
          </el-input>
          <el-switch v-model="filters.winnersOnly" active-text="只看中獎與備取" @change="applyFilter" />
          <el-button type="primary" @click="applyFilter">搜尋</el-button>
          <el-button v-if="canReveal && !revealed" :loading="revealing" @click="setReveal(true)">顯示完整資料</el-button>
          <el-button v-if="revealed" :loading="revealing" @click="setReveal(false)">重新遮罩</el-button>
          <span class="roster__muted">目前檢視第 {{ version ?? draw.rosterVersion }} 版</span>
        </div>
        <p class="roster__hint">{{ nameHint }}<template v-if="!canReveal">你的帳號沒有檢視完整個資的權限，搜尋只比對序號與會員編號。</template></p>
        <el-skeleton v-if="loading" :rows="5" animated />
        <el-empty v-else-if="loadError" :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
        <el-empty v-else-if="rows.length === 0" description="沒有符合條件的名單" :image-size="64" />
        <template v-else>
          <el-table v-if="!isMobile" :data="rows" row-key="serialNo">
            <el-table-column label="抽獎序號" width="100" prop="serialNo" />
            <el-table-column label="會員編號" width="130" prop="memberNo" />
            <el-table-column label="姓名" min-width="110"><template #default="{ row }">{{ row.name || '—' }}</template></el-table-column>
            <el-table-column label="會員層級" width="110" prop="tierLabel" />
            <el-table-column label="會籍到期日" width="120"><template #default="{ row }">{{ row.membershipEndOn || '—' }}</template></el-table-column>
            <el-table-column label="結果" min-width="150">
              <template #default="{ row }">
                <el-tag v-if="row.isWinner" type="success" size="small">中獎</el-tag>
                <el-tag v-else-if="row.isBackup" type="warning" size="small">備取</el-tag>
                <span v-if="row.prizeName" class="roster__prize">{{ row.prizeName }}</span>
              </template>
            </el-table-column>
          </el-table>
          <MobileCardList v-else :rows="rows" row-key="serialNo">
            <template #title="{ row }">序號 {{ row.serialNo }}・{{ row.name || '—' }}</template>
            <template #meta="{ row }">
              <span>{{ row.memberNo }}</span><span>{{ row.tierLabel }}</span>
              <el-tag v-if="row.isWinner" type="success" size="small">中獎</el-tag>
              <el-tag v-else-if="row.isBackup" type="warning" size="small">備取</el-tag>
              <span v-if="row.prizeName">{{ row.prizeName }}</span>
            </template>
            <template #actions><span /></template>
          </MobileCardList>
          <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="roster__pager" @current-change="load" @size-change="applyFilter" />
        </template>
      </el-card>
    </template>

    <ExportPurposeDialog v-model="exportOpen" title="匯出公開版名單" description="公開版含抽獎序號、會員編號與遮罩後的姓名（全體合格名單），可用於現場或直播投影。" :loading="exporting" @confirm="doExport" />

    <el-dialog v-model="regenOpen" title="作廢並重產名單" width="460px" :close-on-click-modal="false" class="roster__dialog">
      <el-alert v-if="regenError" :title="regenError" type="warning" show-icon class="roster__block" @close="regenError = null" />
      <p class="roster__hint">舊版名單會標記為作廢並保留在版本歷程（不可刪除），新名單版本 +1、重新配發序號。已進入抽出階段（已抽出、已公布）後不可重產。</p>
      <el-input v-model="regenReason" type="textarea" :rows="3" maxlength="200" show-word-limit placeholder="作廢原因（必填）" />
      <template #footer>
        <el-button @click="regenOpen = false">取消</el-button>
        <el-button type="danger" :loading="generating" @click="doRegenerate">確定作廢並重產</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.roster__block { margin-bottom: 16px; }
.roster__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.roster__keyword { width: 260px; max-width: 100%; }
.roster__hint { margin: 8px 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.roster__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.roster__preview { font-size: 13px; color: var(--admin-text-secondary); }
.roster__dl { margin: 0; display: flex; flex-direction: column; gap: 6px; font-size: 14px; }
.roster__dl > div { display: flex; gap: 8px; min-width: 0; }
.roster__dl dt { width: 96px; flex-shrink: 0; color: var(--admin-text-secondary); }
.roster__dl dd { margin: 0; min-width: 0; word-break: break-all; }
.roster__hash { font-family: ui-monospace, monospace; font-size: 12px; }
.roster__prize { margin-left: 6px; }
.roster__pager { margin-top: 12px; justify-content: flex-end; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
