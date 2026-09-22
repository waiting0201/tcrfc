<script setup lang="ts">
/** N2 捐款項目管理（docs/22-charity-ui.md §3.7.2） */
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import PageHeader from '@/components/PageHeader.vue'
import SemanticTag from '@/components/SemanticTag.vue'
import EmptyState from '@/components/EmptyState.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import { PROJECTS, type Project } from '@/data/fixtures'
import { formatMoney } from '@/utils/format'
import { hasPermission } from '@/data/session'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
// 平板寬度（768–1023px）表格仍是 el-table，但欄位總寬度會超出可視寬度——CSS display:none
// 隱藏儲存格不會讓 el-table 縮小欄位總寬（它的版面計算是照 el-table-column 的數量與 width
// 參數加總，不看 CSS 有沒有把某個儲存格藏起來），所以次要欄位要用 v-if 整欄不渲染，
// 不能只用 CSS 隱藏（這是本次驗收在平板寬度實測抓到的問題，不是理論假設）。
const isDesktop = computed(() => breakpoint.value === 'desktop')
const sortedProjects = computed(() => [...PROJECTS].sort((a, b) => a.sortOrder - b.sortOrder))
</script>

<template>
  <div>
    <PageHeader title="捐款項目管理" frontend-unit="項目詳情頁">
      <template #actions>
        <el-button v-if="hasPermission('n2.donation_project.manage')" type="primary" @click="router.push('/projects/new')">
          新增捐款項目
        </el-button>
      </template>
    </PageHeader>

    <EmptyState v-if="PROJECTS.length === 0" text="目前沒有捐款項目" />

    <el-table v-else-if="!isMobile" :data="sortedProjects" style="width: 100%">
      <el-table-column label="項目名稱" min-width="200">
        <template #default="{ row }: { row: Project }">
          <div>{{ row.nameZh }}</div>
          <div class="project-list__en">{{ row.nameEn }}</div>
        </template>
      </el-table-column>
      <el-table-column label="狀態" width="90">
        <template #default="{ row }: { row: Project }">
          <SemanticTag :variant="row.status === 'published' ? 'success' : 'neutral'">
            {{ row.status === 'published' ? '上架' : '下架' }}
          </SemanticTag>
        </template>
      </el-table-column>
      <el-table-column v-if="isDesktop" label="累計捐款" width="150">
        <template #default="{ row }: { row: Project }">{{ row.donationCount }} 筆・{{ formatMoney(row.donationTotal) }}</template>
      </el-table-column>
      <el-table-column v-if="isDesktop" prop="sortOrder" label="排序" width="70" />
      <el-table-column v-if="isDesktop" label="憑證模式" width="110">
        <template #default="{ row }: { row: Project }">{{ row.invoiceMode === 'b2c_invoice' ? '電子發票' : '捐贈收據' }}</template>
      </el-table-column>
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ row }: { row: Project }">
          <el-button size="small" text type="primary" @click="router.push(`/projects/${row.key}/edit`)">編輯</el-button>
        </template>
      </el-table-column>
    </el-table>

    <MobileCardList v-else :items="sortedProjects">
      <template #default="{ item }: { item: Project }">
        <div class="project-list__card-head">
          <div>
            <div class="project-list__card-name">{{ item.nameZh }}</div>
            <div class="project-list__en">{{ item.nameEn }}</div>
          </div>
          <SemanticTag :variant="item.status === 'published' ? 'success' : 'neutral'">
            {{ item.status === 'published' ? '上架' : '下架' }}
          </SemanticTag>
        </div>
        <p class="project-list__card-meta">
          累計 {{ item.donationCount }} 筆・{{ formatMoney(item.donationTotal) }}・{{ item.invoiceMode === 'b2c_invoice' ? '電子發票' : '捐贈收據' }}
        </p>
        <div class="project-list__card-actions">
          <el-button size="small" text type="primary" @click="router.push(`/projects/${item.key}/edit`)">編輯</el-button>
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
