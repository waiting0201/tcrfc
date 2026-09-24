<script setup lang="ts">
/**
 * 賽事系列（Competition）編輯頁。
 *
 * 🔴 「賽季」欄位需要輸入識別碼，是本輪回報的已知 API 缺口：`apps/api` 目前沒有任何端點可以
 * 列出俱樂部有哪些賽季（`Season` 只在資料庫 seed 腳本裡建立，沒有對應的維護或列表端點），
 * 所以這裡沒有下拉選單可用。建議之後補一支賽季清單端點（甚至一併補上賽季的維護端點），
 * 屆時把這裡換成下拉選單即可，不影響其餘邏輯。編輯既有資料時會顯示目前的賽季代碼供對照。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { createAdminCompetition, getAdminCompetition, updateAdminCompetition } from '@/api/adminCompetitions'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = computed(() => route.name === 'competition-new')
const competitionId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  seasonId: '',
  code: '',
  compType: '',
  sortOrder: 0,
  status: 'draft' as 'draft' | 'published',
  nameZh: '',
  organizerZh: '',
  nameEn: '',
  organizerEn: '',
})
const existingSeasonCode = ref('')
const baselineJson = ref('')

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadCompetition() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && competitionId.value) {
      const detail = await getAdminCompetition(activeClubId.value, competitionId.value)
      form.seasonId = detail.seasonId
      form.code = detail.code
      form.compType = detail.compType ?? ''
      form.sortOrder = detail.sortOrder
      form.status = detail.status
      form.nameZh = detail.zh.name
      form.organizerZh = detail.zh.organizer ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.organizerEn = detail.en?.organizer ?? ''
      existingSeasonCode.value = detail.seasonCode
    }
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadCompetition)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增賽事系列' : `編輯賽事系列：${form.nameZh}`))
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** 跟俱樂部主檔同樣的判斷方式：英文名稱是必填才有意義的欄位，沒有英文名稱就視為
 * 「還沒有英文版本」，不整份送出。 */
function isEnEmpty(): boolean {
  return !form.nameEn.trim()
}

function validate(): boolean {
  formError.value = null
  if (!GUID_PATTERN.test(form.seasonId.trim())) {
    formError.value = '賽季識別碼格式不正確，請確認是否完整複製'
    return false
  }
  if (!form.code.trim()) {
    formError.value = '請輸入代碼'
    return false
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入中文名稱'
    return false
  }
  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    const payload = {
      seasonId: form.seasonId.trim(),
      code: form.code.trim(),
      compType: form.compType || null,
      sortOrder: form.sortOrder,
      status: form.status,
      content: {
        zh: { name: form.nameZh.trim(), organizer: form.organizerZh || null },
        en: isEnEmpty() ? undefined : { name: form.nameEn.trim(), organizer: form.organizerEn || null },
      },
    }
    if (isCreate.value) {
      const created = await createAdminCompetition(activeClubId.value, payload)
      ElMessage.success('已建立')
      router.replace(`/teams/competitions/${created.id}/edit`)
      competitionId.value = created.id
    } else {
      await updateAdminCompetition(activeClubId.value, competitionId.value!, payload)
      ElMessage.success('已儲存')
    }
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/teams/competitions')
}
</script>

<template>
  <div class="competition-edit">
    <PageHeader :title="pageTitle">
      <template #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadCompetition">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="competition-edit__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="基本資料" class="competition-edit__section">
        <el-form label-position="top">
          <el-form-item label="賽季識別碼" required>
            <el-input v-model="form.seasonId" placeholder="請輸入賽季的識別碼" />
            <span v-if="existingSeasonCode" class="competition-edit__hint">目前的賽季代碼：{{ existingSeasonCode }}</span>
            <span class="competition-edit__hint competition-edit__hint--warning">
              目前後台尚無「賽季」清單可供選擇，需自行輸入識別碼（已知缺口，見交付說明）。
            </span>
          </el-form-item>
          <el-form-item label="代碼" required>
            <el-input v-model="form.code" placeholder="例如 corp-a" />
          </el-form-item>
          <el-form-item label="類型">
            <el-input v-model="form.compType" placeholder="選填，例如：聯賽、盃賽、友誼賽" />
          </el-form-item>
          <BilingualShortField
            label="名稱"
            :zh="form.nameZh"
            :en="form.nameEn"
            required
            @update:zh="(v) => (form.nameZh = v)"
            @update:en="(v) => (form.nameEn = v)"
          />
          <BilingualShortField
            label="主辦單位"
            :zh="form.organizerZh"
            :en="form.organizerEn"
            @update:zh="(v) => (form.organizerZh = v)"
            @update:en="(v) => (form.organizerEn = v)"
          />
          <el-form-item label="排序">
            <el-input-number v-model="form.sortOrder" :min="0" />
          </el-form-item>
          <el-form-item label="狀態">
            <el-radio-group v-model="form.status">
              <el-radio value="draft">草稿</el-radio>
              <el-radio value="published">已發布</el-radio>
            </el-radio-group>
          </el-form-item>
        </el-form>
      </el-card>

      <div class="competition-edit__actions">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.competition-edit__section {
  margin-bottom: 12px;
}

.competition-edit__form-error {
  margin-bottom: 12px;
}

.competition-edit__hint {
  display: block;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin-top: 4px;
}

.competition-edit__hint--warning {
  color: var(--el-color-warning);
}

.competition-edit__actions {
  margin-top: 16px;
}
</style>
