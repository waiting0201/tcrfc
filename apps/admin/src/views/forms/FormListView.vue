<script setup lang="ts">
/**
 * G1 表單設計器——列表頁（對應主站規劃書 §4.7 G1；apps/api/README.md「S1-10」）。
 * 9 個固定表單目錄，**沒有新增／刪除**，只能點進去編輯設定與底下的動態欄位。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminForms, type AdminFormListItemDto } from '@/api/adminForms'
import { AdminApiError } from '@/api/http'
import { useFormsPermissions } from '@/composables/useFormsPermissions'
import { FORM_CODE_ORDER, formCodeLabel } from '@/types/forms'
import { formatDateTime } from '@/utils/formatDateTime'

const router = useRouter()
const club = computed(() => activeClubId.value)
const { canManageForms } = useFormsPermissions()

const forms = ref<AdminFormListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

const orderedForms = computed(() => {
  const rank = new Map<string, number>(FORM_CODE_ORDER.map((code, index) => [code, index]))
  return [...forms.value].sort((a, b) => (rank.get(a.formCode) ?? 99) - (rank.get(b.formCode) ?? 99))
})

async function loadForms() {
  loading.value = true
  loadError.value = null
  try {
    forms.value = await listAdminForms(club.value)
  } catch (error) {
    forms.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '表單清單載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(loadForms)
watch(club, loadForms)

function handleEdit(row: AdminFormListItemDto) {
  router.push(`/inquiries/builder/${row.id}/edit`)
}
</script>

<template>
  <div class="form-list">
    <PageHeader title="表單設計器">
      <template #meta>
        <FrontendUnitBanner module-code="G1" />
      </template>
    </PageHeader>

    <el-alert
      title="表單種類固定為 9 種（招募、學院與營隊、國際球員、合作贊助、媒體、一般聯絡、提案下載、捐助洽詢），不能新增或刪除；能調整的是每張表單的通知信、自動回覆信、送出後導向與底下的欄位。"
      type="info"
      show-icon
      :closable="false"
      class="form-list__hint"
    />

    <el-card v-if="loading" shadow="never">
      <el-skeleton :rows="6" animated />
    </el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError">
        <el-button type="primary" @click="loadForms">重新載入</el-button>
      </el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <el-table :data="orderedForms" row-key="id">
        <el-table-column label="表單" min-width="200">
          <template #default="{ row }">{{ formCodeLabel(row.formCode) }}</template>
        </el-table-column>
        <el-table-column label="欄位數" width="90">
          <template #default="{ row }">{{ row.fieldCount }}</template>
        </el-table-column>
        <el-table-column label="收件通知" min-width="200">
          <template #default="{ row }">{{ row.notifyEmails || '（未設定）' }}</template>
        </el-table-column>
        <el-table-column label="防機器人驗證" width="120">
          <template #default="{ row }">
            <el-tag :type="row.captchaEnabled ? 'success' : 'info'" size="small">
              {{ row.captchaEnabled ? '已開啟' : '未開啟' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最後更新" width="160">
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" text type="primary" @click="handleEdit(row)">{{ canManageForms ? '編輯' : '檢視' }}</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.form-list__hint {
  margin-bottom: 12px;
}
</style>
