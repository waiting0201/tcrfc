<script setup lang="ts">
/** N2 捐款項目管理（docs/22-charity-ui.md §3.7.2）：接真 API。列表刻意沒有累計金額與筆數（累計數字只在報表）。 */
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { listProjects, publishProject, unpublishProject, type ProjectListItem } from '@/api/projects'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度的次要欄位用 v-if 整欄不渲染（el-table 欄位總寬不看 CSS 隱藏，見 README「響應式」）。
const isDesktop = computed(() => breakpoint.value === 'desktop')

const canManage = computed(() => hasPermission('n2.donation_project.manage'))
const canPublish = computed(() => hasPermission('n2.donation_project.publish'))

const items = ref<ProjectListItem[]>([])
const loading = ref(false)
const loadError = ref('')
const busyId = ref<string | null>(null)

const sorted = computed(() => [...items.value].sort((a, b) => a.sortOrder - b.sortOrder))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    items.value = await listProjects()
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '查詢時發生問題，請稍後再試一次'
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function toggle(row: ProjectListItem) {
  busyId.value = row.id
  try {
    if (row.status === 'published') {
      await unpublishProject(row.id)
      ElMessage.success(`已將「${row.nameZh}」下架`)
    } else {
      await publishProject(row.id)
      ElMessage.success(`已將「${row.nameZh}」上架`)
    }
    await load()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '操作失敗')
  } finally {
    busyId.value = null
  }
}

const statusLabel = (s: string) => (s === 'published' ? '上架' : '未上架')
const modeLabel = (m: string) => (m === 'b2c_invoice' ? '電子發票' : '捐贈收據')
</script>

<template>
  <div>
    <PageHeader title="捐款項目管理" frontend-unit="項目詳情頁">
      <template #actions>
        <el-button v-if="canManage" type="primary" @click="router.push('/projects/new')">新增捐款項目</el-button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <EmptyState v-else-if="!loading && items.length === 0" text="目前沒有捐款項目" />

    <el-table v-else-if="!isMobile" v-loading="loading" :data="sorted" style="width: 100%">
      <el-table-column label="項目名稱" min-width="200">
        <template #default="{ row }: { row: ProjectListItem }">
          <div>{{ row.nameZh }}</div>
          <div class="project-list__en">{{ row.nameEn }}</div>
        </template>
      </el-table-column>
      <el-table-column label="狀態" width="90">
        <template #default="{ row }: { row: ProjectListItem }">
          <SemanticTag :variant="row.status === 'published' ? 'success' : 'neutral'">{{ statusLabel(row.status) }}</SemanticTag>
        </template>
      </el-table-column>
      <el-table-column v-if="isDesktop" prop="sortOrder" label="排序" width="70" />
      <el-table-column v-if="isDesktop" label="憑證模式" width="110">
        <template #default="{ row }: { row: ProjectListItem }">{{ modeLabel(row.invoiceMode) }}</template>
      </el-table-column>
      <el-table-column v-if="isDesktop" label="撥付對象" min-width="140">
        <template #default="{ row }: { row: ProjectListItem }">{{ row.charityName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="操作" width="230" fixed="right">
        <template #default="{ row }: { row: ProjectListItem }">
          <el-button v-if="canManage" size="small" text type="primary" @click="router.push(`/projects/${row.id}/edit`)">編輯</el-button>
          <el-button v-if="canManage" size="small" text type="primary" @click="router.push(`/projects/${row.id}/content`)">編輯內文</el-button>
          <el-button v-if="canPublish" size="small" text :loading="busyId === row.id" @click="toggle(row)">
            {{ row.status === 'published' ? '下架' : '上架' }}
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <MobileCardList v-else :items="sorted">
      <template #default="{ item }: { item: ProjectListItem }">
        <div class="project-list__card-head">
          <div>
            <div class="project-list__card-name">{{ item.nameZh }}</div>
            <div class="project-list__en">{{ item.nameEn }}</div>
          </div>
          <SemanticTag :variant="item.status === 'published' ? 'success' : 'neutral'">{{ statusLabel(item.status) }}</SemanticTag>
        </div>
        <p class="project-list__card-meta">{{ modeLabel(item.invoiceMode) }}</p>
        <div class="project-list__card-actions">
          <el-button v-if="canManage" size="small" text type="primary" @click="router.push(`/projects/${item.id}/edit`)">編輯</el-button>
          <el-button v-if="canManage" size="small" text type="primary" @click="router.push(`/projects/${item.id}/content`)">編輯內文</el-button>
          <el-button v-if="canPublish" size="small" text :loading="busyId === item.id" @click="toggle(item)">
            {{ item.status === 'published' ? '下架' : '上架' }}
          </el-button>
        </div>
      </template>
    </MobileCardList>
  </div>
</template>

<style scoped>
.project-list__en {
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-list__card-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: var(--charity-admin-space-2);
}

.project-list__card-name {
  font-weight: 600;
  color: var(--charity-admin-text-primary);
}

.project-list__card-meta {
  margin: var(--charity-admin-space-2) 0;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-list__card-actions {
  padding-top: var(--charity-admin-space-2);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
