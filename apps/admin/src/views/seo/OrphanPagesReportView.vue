<script setup lang="ts">
/**
 * H3 孤立頁面偵測（對應主站規劃書 §4.8 H「內部連結建議：依網站層級提示未被連結的孤立頁面」；
 * apps/api/README.md「S1-12」「規劃書沒寫清楚、本輪自行判斷的部分」第 4 點）。唯讀報表。
 *
 * 🔴 這份報表目前是「掃描已發布內容彼此的正文，看有沒有互相提到對方的網址」這種簡化做法，
 * 不是完整的網站連結地圖——看不到前台目前尚未資料庫化的主選單／頁尾連結，所以清單上出現的頁面
 * 不代表訪客真的完全找不到路徑過去，只代表「內容彼此之間沒有互相連結」。畫面上把這個限制講清楚，
 * 不假裝這是一份完整可靠的報表（STATUS.md S1-12 列明列的既有待辦，等日後有選單管理或頁面動態路由
 * 落地後才能改善）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { getAdminOrphanPages, type OrphanPageDto } from '@/api/adminSeo'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)

const items = ref<OrphanPageDto[]>([])
const loading = ref(true)
const errorMessage = ref<string | null>(null)

async function loadReport() {
  loading.value = true
  errorMessage.value = null
  try {
    const result = await getAdminOrphanPages(club.value)
    items.value = result.items
  } catch (error) {
    items.value = []
    errorMessage.value = error instanceof AdminApiError ? error.message : '報表載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(loadReport)
watch(club, loadReport)

const ENTITY_TYPE_LABEL: Record<OrphanPageDto['entityType'], string> = {
  page: '頁面',
  article: '新聞與故事',
}
</script>

<template>
  <div class="orphan-report">
    <PageHeader title="孤立頁面偵測">
      <template #meta>
        <FrontendUnitBanner module-code="H" />
      </template>
    </PageHeader>

    <el-alert type="info" :closable="false" show-icon class="orphan-report__notice">
      <p class="orphan-report__notice-text">
        這份清單列出「已發布，但目前找不到其他任何一篇內容有連到它」的頁面或文章——訪客要看到這些內容，只能透過直接輸入網址，或搜尋引擎剛好收錄到。
      </p>
      <p class="orphan-report__notice-text">
        ⚠️ <strong>目前的偵測方式有限制</strong>：只會比對已發布內容彼此的正文有沒有互相提到對方的網址，看不到網站的主選單、頁尾這類目前還沒有獨立管理畫面的固定連結。也就是說，清單上的頁面不代表訪客完全找不到路徑過去——只代表「內容跟內容之間沒有互相連結」，僅供參考，還不是一份完整可靠的連結地圖。
      </p>
    </el-alert>

    <el-card shadow="never">
      <el-skeleton v-if="loading" :rows="6" animated />
      <el-empty v-else-if="errorMessage" :description="errorMessage">
        <el-button type="primary" @click="loadReport">重新載入</el-button>
      </el-empty>
      <el-empty v-else-if="items.length === 0" description="目前沒有偵測到孤立頁面" />
      <el-table v-else :data="items" row-key="id">
        <el-table-column label="類型" width="120">
          <template #default="{ row }">{{ ENTITY_TYPE_LABEL[row.entityType as OrphanPageDto['entityType']] }}</template>
        </el-table-column>
        <el-table-column label="標題" min-width="200">
          <template #default="{ row }">{{ row.titleZh || '（未命名）' }}</template>
        </el-table-column>
        <el-table-column label="網址" min-width="240">
          <template #default="{ row }">
            <a :href="row.path" target="_blank" rel="noopener noreferrer" class="orphan-report__link">{{ row.path }}</a>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.orphan-report__notice {
  margin-bottom: 12px;
}

.orphan-report__notice-text {
  margin: 0;
  line-height: 1.7;
}

.orphan-report__notice-text + .orphan-report__notice-text {
  margin-top: 8px;
}

.orphan-report__link {
  color: var(--admin-primary);
  text-decoration: none;
  word-break: break-all;
}

.orphan-report__link:hover {
  text-decoration: underline;
}
</style>
