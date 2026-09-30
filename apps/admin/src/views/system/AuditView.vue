<script setup lang="ts">
/**
 * 稽核與備份（僅系統管理員）。
 * 🔴 這一頁**不是稽核查詢**：目前系統不保存「誰在何時改了哪筆資料」的操作稽核紀錄（委託方本期範圍指示不含紀錄表，
 * 使用者 2026-09-23 裁決撤回既有的兩張紀錄表），所以沒有可以查的東西——不做假的查詢介面。
 * 這裡只呈現不需要紀錄表就能算出來的「帳號活動概況與登入異常提醒」，並以說明文字交代備份的實際做法與缺口。
 */
import { computed, onMounted, ref } from 'vue'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { AdminApiError } from '@/api/http'
import { getSecurityOverview, type SecurityAccountDto, type SecurityOverviewDto } from '@/api/adminSecurity'
import { formatDateTime } from '@/utils/dateTime'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')

const data = ref<SecurityOverviewDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
async function load() {
  loading.value = true
  error.value = null
  try {
    data.value = await getSecurityOverview()
  } catch (e) {
    data.value = null
    error.value = e instanceof AdminApiError ? e.message : '帳號活動概況載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)

const alertType = (kind: string) => (kind === 'locked' ? 'danger' : kind === 'failed_attempts' ? 'warning' : 'info')
const statusText = (s: string) => (s === 'active' ? '啟用中' : s === 'disabled' || s === 'inactive' ? '已停用' : s)
const lastLoginText = (a: SecurityAccountDto) => (a.lastLoginAt ? `${formatDateTime(a.lastLoginAt)}（${a.daysSinceLastLogin ?? 0} 天前）` : '從未登入')
</script>

<template>
  <div class="audit">
    <PageHeader title="稽核與備份">
      <template #meta><FrontendUnitBanner module-code="J3" /></template>
    </PageHeader>

    <el-alert class="audit__block" type="warning" show-icon :closable="false" title="目前系統不保存操作稽核紀錄">
      <p class="audit__p">也就是說，「誰在什麼時候新增、修改、刪除或發布了哪一筆資料」<strong>沒有紀錄可以查</strong>，這一頁因此沒有稽核查詢功能，也不會假裝有。</p>
      <p class="audit__p">匯出名單、檢視完整個資、推播覆核這類敏感操作，系統只會寫進「系統運作紀錄」（不含個人資料本身），由維運人員在雲端監控平台查看，無法在後台查詢。</p>
      <p v-if="data" class="audit__p">系統說明：{{ data.auditTrailMessage }}</p>
      <p class="audit__p">若客戶要求可查詢的操作稽核與登入紀錄（規劃書要求保存 12 個月），需要先由客戶重新確認稽核政策，再另行開發紀錄表。</p>
    </el-alert>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="error" shadow="never"><el-empty :description="error"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>

    <template v-else-if="data">
      <div class="audit__stats audit__block">
        <el-card shadow="never"><div class="audit__num">{{ data.activeAccounts }}</div><div class="audit__muted">啟用中的帳號</div></el-card>
        <el-card shadow="never"><div class="audit__num" :class="{ 'audit__num--bad': data.lockedAccounts > 0 }">{{ data.lockedAccounts }}</div><div class="audit__muted">目前被鎖定</div></el-card>
        <el-card shadow="never"><div class="audit__num" :class="{ 'audit__num--warn': data.dormantAccounts > 0 }">{{ data.dormantAccounts }}</div><div class="audit__muted">久未登入</div></el-card>
      </div>

      <el-card shadow="never" class="audit__block" header="登入異常提醒">
        <p class="audit__hint">只針對「啟用中」的帳號提醒：連續登入失敗 3 次以上、被鎖定、超過 90 天沒有登入、建立超過 7 天仍從未登入。提醒依系統當下的帳號狀態計算，不是歷史紀錄。</p>
        <el-empty v-if="data.alerts.length === 0" description="目前沒有需要注意的帳號" :image-size="64" />
        <ul v-else class="audit__alerts">
          <li v-for="(a, i) in data.alerts" :key="`${a.accountId}-${a.kind}-${i}`">
            <el-tag :type="alertType(a.kind)" size="small">{{ a.kindLabel }}</el-tag>
            <strong>{{ a.username }}</strong>
            <span class="audit__muted">{{ a.message }}</span>
          </li>
        </ul>
      </el-card>

      <el-card shadow="never" class="audit__block" header="帳號活動概況">
        <p class="audit__hint">資料產生時間：{{ formatDateTime(data.generatedAt) }}（台灣時間）。不含密碼與任何驗證資料。</p>
        <el-empty v-if="data.accounts.length === 0" description="沒有帳號資料" :image-size="64" />
        <template v-else>
          <el-table v-if="!isMobile" :data="data.accounts" row-key="id">
            <el-table-column label="帳號" min-width="180">
              <template #default="{ row }">{{ row.username }}<div class="audit__muted">{{ row.displayName || '（未填姓名）' }}<template v-if="row.isSuperAdmin">・系統管理員</template></div></template>
            </el-table-column>
            <el-table-column label="狀態" width="150">
              <template #default="{ row }">
                <el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">{{ statusText(row.status) }}</el-tag>
                <el-tag v-if="row.isLockedNow" type="danger" size="small" class="audit__tag">已鎖定</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="最近登入" min-width="200"><template #default="{ row }">{{ lastLoginText(row) }}</template></el-table-column>
            <el-table-column label="連續登入失敗" width="120"><template #default="{ row }">{{ row.failedAttemptCount }} 次</template></el-table-column>
            <el-table-column label="鎖定到" width="150"><template #default="{ row }">{{ row.isLockedNow ? formatDateTime(row.lockedUntil) : '—' }}</template></el-table-column>
            <el-table-column label="上次改密碼" width="150"><template #default="{ row }">{{ formatDateTime(row.passwordChangedAt) || '—' }}</template></el-table-column>
          </el-table>
          <MobileCardList v-else :rows="data.accounts" row-key="id">
            <template #title="{ row }">{{ row.username }}<span class="audit__muted">&emsp;{{ row.displayName }}</span></template>
            <template #meta="{ row }">
              <el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">{{ statusText(row.status) }}</el-tag>
              <el-tag v-if="row.isLockedNow" type="danger" size="small">已鎖定</el-tag>
              <span>最近登入：{{ lastLoginText(row) }}</span><span>連續失敗 {{ row.failedAttemptCount }} 次</span>
            </template>
            <template #actions><span /></template>
          </MobileCardList>
        </template>
      </el-card>
    </template>

    <el-card shadow="never" class="audit__block" header="資料備份（說明，後台沒有備份與還原功能）">
      <ul class="audit__list">
        <li><strong>自動備份：</strong>資料庫由雲端平台每天自動備份，不需要也無法在後台排程。備份可讓資料庫還原到保留期內的任一時間點。</li>
        <li><strong>保留天數：</strong>目前的資料庫方案最長保留 7 天。若發現得晚（例如誤刪超過一週才察覺）就還原不回來；建議正式上線時另外啟用每週長期備份（費用另計）。</li>
        <li><strong>「手動建立還原點」：</strong>雲端資料庫沒有這個動作。需要固定留存某個時間點時，要由維運人員在雲端入口網站設定長期備份，或手動匯出一份資料庫檔案存放。</li>
        <li><strong>圖片與檔案：</strong>不在資料庫備份內，另有獨立的檔案儲存空間；需要啟用刪除保護與版本保留，私有的提案檔案要另外備份。</li>
        <li><strong>看不到備份狀態：</strong>後台無法顯示備份是否成功、有哪些還原點，請維運人員到雲端入口網站查看。</li>
        <li><strong>還原之後：</strong>因為沒有操作稽核紀錄，還原後無法比對「還原點之後有誰做了什麼」。</li>
      </ul>
    </el-card>
  </div>
</template>

<style scoped>
.audit { min-width: 0; }
.audit__block { margin-bottom: 12px; }
.audit__p { margin: 6px 0 0; font-size: 13px; line-height: 1.7; }
.audit__stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; }
.audit__num { font-size: 26px; font-weight: 600; color: var(--admin-text-primary); }
.audit__num--bad { color: var(--el-color-danger); }
.audit__num--warn { color: var(--el-color-warning); }
.audit__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.audit__hint { margin: 0 0 10px; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.audit__alerts { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.audit__alerts li { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.audit__tag { margin-left: 6px; }
.audit__list { margin: 0; padding-left: 20px; display: flex; flex-direction: column; gap: 8px; font-size: 13px; line-height: 1.7; color: var(--admin-text-secondary); }
</style>
