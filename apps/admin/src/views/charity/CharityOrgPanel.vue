<script setup lang="ts">
/** 公益團體清單。標示「兩隊共用」的團體只能檢視；仍被計畫或事蹟引用的團體不能刪除。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { deleteOrg, listOrgs, type OrgListItemDto } from '@/api/adminCharity'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { canCreate, canUpdate, canDelete } = useCrudPermissions('charity.content')
const club = computed(() => activeClubId.value)

const rows = ref<OrgListItemDto[]>([])
const keyword = ref('')
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    rows.value = await listOrgs(club.value, keyword.value.trim() || undefined)
  } catch (error) {
    rows.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, () => {
  keyword.value = ''
  load()
})

async function handleDelete(row: OrgListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除公益團體「${row.nameZh || '（未命名）'}」嗎？`, '刪除公益團體', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteOrg(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

const edit = (row: OrgListItemDto) => router.push(`/content/charity/organizations/${row.id}/edit`)
const editable = (row: OrgListItemDto) => canUpdate.value && !row.isShared
</script>

<template>
  <div>
    <el-card shadow="never" class="panel__block">
      <div class="panel__row">
        <el-input v-model="keyword" placeholder="搜尋團體名稱" clearable class="panel__keyword" @keyup.enter="load" @clear="load">
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-button type="primary" @click="load">搜尋</el-button>
        <span class="panel__spacer" />
        <el-button v-if="canCreate" type="primary" @click="router.push('/content/charity/organizations/new')">+ 新增公益團體</el-button>
      </div>
      <p class="panel__hint">公益團體資料可以重複引用在多個計畫與事蹟，不用重複輸入。</p>
    </el-card>
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="id">
          <el-table-column label="標誌" width="84">
            <template #default="{ row }"><img v-if="row.logoThumbUrl" :src="row.logoThumbUrl" alt="" class="panel__logo"><span v-else class="panel__muted">—</span></template>
          </el-table-column>
          <el-table-column label="團體名稱" min-width="200">
            <template #default="{ row }">{{ row.nameZh || '（未命名）' }}<el-tag v-if="row.isShared" type="info" size="small" class="panel__tag">兩隊共用</el-tag></template>
          </el-table-column>
          <el-table-column label="聯絡窗口" min-width="140"><template #default="{ row }">{{ row.contactName || '—' }}</template></el-table-column>
          <el-table-column label="計畫／事蹟" width="110"><template #default="{ row }">{{ row.programCount }}／{{ row.recordCount }}</template></el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button size="small" text type="primary" @click="edit(row)">{{ editable(row) ? '編輯' : '檢視' }}</el-button>
              <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
            </template>
          </el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="id">
          <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
          <template #meta="{ row }"><el-tag v-if="row.isShared" type="info" size="small">兩隊共用</el-tag><span>計畫 {{ row.programCount }}・事蹟 {{ row.recordCount }}</span></template>
          <template #actions="{ row }">
            <el-button size="small" text type="primary" @click="edit(row)">{{ editable(row) ? '編輯' : '檢視' }}</el-button>
            <el-button v-if="canDelete && !row.isShared" size="small" text type="danger" @click="handleDelete(row)">刪除</el-button>
          </template>
        </MobileCardList>
      </template>
      <el-empty v-else description="還沒有公益團體" />
    </el-card>
  </div>
</template>

<style scoped>
.panel__block { margin-bottom: 12px; }
.panel__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.panel__keyword { width: 220px; max-width: 100%; }
.panel__spacer { flex: 1; }
.panel__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.panel__logo { width: 56px; height: 36px; object-fit: contain; background: var(--admin-lightbox-neutral); border-radius: 3px; }
.panel__muted { color: var(--admin-text-tertiary); }
.panel__tag { margin-left: 6px; }
</style>
