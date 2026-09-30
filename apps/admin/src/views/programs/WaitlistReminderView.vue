<script setup lang="ts">
/**
 * 候補遞補提醒：列出「有空位、而且還有人在候補」的梯次，展開就是候補名單（依登記先後）。
 * 系統不會自動寄通知信——承辦依名單電話聯繫後，在這裡按「遞補」把候補改為已確認。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listWaitlistReminders, promoteAdminRegistration, type WaitlistEntryDto, type WaitlistReminderDto } from '@/api/adminRegistrations'
import { formatDateTime } from '@/utils/dateTime'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)
const { canProcessRegistrations } = useProgramPermissions()

const items = ref<WaitlistReminderDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const busyId = ref<string | null>(null)
const opened = ref<string[]>([])

async function load() {
  loading.value = true
  loadError.value = null
  try {
    items.value = await listWaitlistReminders(club.value)
  } catch (error) {
    items.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '候補提醒載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch(club, () => {
  opened.value = []
  load()
})

function title(item: WaitlistReminderDto): string {
  return `${item.programNameZh ?? '（未命名項目）'}（${item.startOn ?? '—'} ～ ${item.endOn ?? '—'}）`
}

async function promote(entry: WaitlistEntryDto) {
  try {
    await ElMessageBox.confirm(
      `確定要遞補「${entry.applicantName}」嗎？遞補後狀態改為已確認並佔用名額。請先確認已用電話聯繫對方。`,
      '遞補',
      { confirmButtonText: '遞補', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  busyId.value = entry.registrationId
  try {
    await promoteAdminRegistration(club.value, entry.registrationId)
    ElMessage.success(`已遞補「${entry.applicantName}」`)
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '遞補失敗，請稍後再試')
  } finally {
    busyId.value = null
  }
}

const listPath = (item: WaitlistReminderDto) => `/programs/enrollments?sessionId=${item.sessionId}`
</script>

<template>
  <div class="waitlist">
    <PageHeader title="候補遞補提醒">
      <template #meta><FrontendUnitBanner module-code="P3" /></template>
    </PageHeader>

    <el-card shadow="never" class="waitlist__bar">
      <div class="waitlist__bar-row">
        <el-button @click="router.push('/programs/enrollments')"><el-icon><ArrowLeft /></el-icon>返回報名管理</el-button>
        <el-button @click="load">重新整理</el-button>
      </div>
      <p class="waitlist__hint">下面是「已經有空位、但還有人在候補」的梯次。系統不會自動寄通知信，請依名單上的電話聯繫，對方同意後再按「遞補」。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else-if="items.length === 0" shadow="never">
      <el-empty description="目前沒有需要遞補的梯次" />
    </el-card>
    <el-card v-else shadow="never">
      <el-collapse v-model="opened">
        <el-collapse-item v-for="item in items" :key="item.sessionId" :name="item.sessionId">
          <template #title>
            <div class="waitlist__title">
              <span class="waitlist__name">{{ title(item) }}</span>
              <el-tag type="warning" size="small">空位 {{ item.vacancy }}</el-tag>
              <el-tag type="info" size="small">候補 {{ item.waiting.length }} 人</el-tag>
              <span class="waitlist__quota">已報名 {{ item.enrolledCount }} / {{ item.capacity ?? '不限' }}</span>
            </div>
          </template>
          <el-table v-if="!isMobile" :data="item.waiting" row-key="registrationId">
            <el-table-column label="順位" width="70" prop="order" />
            <el-table-column label="報名人" min-width="110" prop="applicantName" />
            <el-table-column label="電話" min-width="130">
              <template #default="{ row }">{{ row.phone || '—' }}</template>
            </el-table-column>
            <el-table-column label="家長" min-width="140">
              <template #default="{ row }">{{ row.guardianName || '—' }} {{ row.guardianPhone || '' }}</template>
            </el-table-column>
            <el-table-column label="登記時間" width="150">
              <template #default="{ row }">{{ formatDateTime(row.queuedAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="90" fixed="right">
              <template #default="{ row }">
                <el-button v-if="canProcessRegistrations" size="small" text type="primary" :loading="busyId === row.registrationId" @click="promote(row)">遞補</el-button>
              </template>
            </el-table-column>
          </el-table>
          <MobileCardList v-else :rows="item.waiting" row-key="registrationId">
            <template #title="{ row }">第 {{ row.order }} 順位 {{ row.applicantName }}</template>
            <template #meta="{ row }">
              <span>電話 {{ row.phone || '—' }}</span>
              <span v-if="row.guardianName">家長 {{ row.guardianName }} {{ row.guardianPhone || '' }}</span>
              <span>登記 {{ formatDateTime(row.queuedAt) }}</span>
            </template>
            <template #actions="{ row }">
              <el-button v-if="canProcessRegistrations" size="small" text type="primary" :loading="busyId === row.registrationId" @click="promote(row)">遞補</el-button>
            </template>
          </MobileCardList>
          <div class="waitlist__foot">
            <el-button size="small" text type="primary" @click="router.push(listPath(item))">到這個梯次的報名名單</el-button>
          </div>
        </el-collapse-item>
      </el-collapse>
    </el-card>
  </div>
</template>

<style scoped>
.waitlist__bar { margin-bottom: 12px; }
.waitlist__bar-row { display: flex; flex-wrap: wrap; gap: 8px; }
.waitlist__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.waitlist__title { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; min-width: 0; padding-right: 8px; }
.waitlist__name { font-weight: 500; word-break: break-word; }
.waitlist__quota { font-size: 12px; color: var(--admin-text-secondary); }
.waitlist__foot { margin-top: 8px; }
</style>
