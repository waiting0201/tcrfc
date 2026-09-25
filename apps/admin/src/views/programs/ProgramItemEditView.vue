<script setup lang="ts">
/**
 * P1 課程／營隊項目——編輯頁。對照 apps/api/README.md「S1-9」。
 *
 * ⚠️ **合作夥伴（`partnerIds`，關聯 E1）本輪不提供選擇介面**——E1 合作夥伴管理（`S2-1`）尚未
 * 開發，後台目前沒有任何端點可以列出俱樂部的合作夥伴清單，沒有清單就做不出有意義的選單
 * （比照 `MatchEditView.vue` 場地選單「沒有清單就不做」的既有先例）。`partnerIds` 因此一律不
 * 送出（省略＝維持不變），已在 apps/admin/README.md 回報這個相依關係。
 *
 * ⚠️ **課程內容（`content`，區塊編輯）本輪以純 JSON 文字欄位呈現**——後端只驗證語法合法性，
 * 不驗證區塊結構（規劃書沒有像 B1 頁面那樣明訂區塊型別清單，見 `apps/api`
 * `AdminProgramLocaleContent` 檔頭），沒有現成的區塊編輯器可以重用（B1 的 `pageBlocks/` 是
 * 針對 `Page` 模型設計，區塊型別完全不同），比照後端自身「不超出範圍另外發明一套」的判斷。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminStaff, type AdminStaffListItemDto } from '@/api/adminStaff'
import { createAdminProgram, getAdminProgram, updateAdminProgram, type SaveProgramPayload } from '@/api/adminPrograms'
import { AdminApiError } from '@/api/http'
import { PROGRAM_TYPE_LABEL, PROGRAM_TYPE_ORDER, type ProgramType } from '@/types/program'

const route = useRoute()
const router = useRouter()
const { canManageItems } = useProgramPermissions()

// 🔴 必須是 computed，不能是一次性求值的 const——理由同 `PlayerEditView.vue` 檔頭說明：建立成功
// 後 `router.replace` 不會重新掛載這個元件實例，`isCreate` 若只算一次會讓下一次「儲存」誤判成
// 仍在建立模式而重複建立。
const isCreate = computed(() => route.name === 'program-item-new')
const programId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  slug: '',
  programType: '' as ProgramType | '',
  audience: '',
  ageMin: null as number | null,
  ageMax: null as number | null,
  status: 'draft' as 'draft' | 'published',
  nameZh: '',
  nameEn: '',
  introZh: '',
  introEn: '',
  contentZh: '',
  contentEn: '',
  staffIds: [] as string[],
})
const baselineJson = ref('')
const coverKey = ref<string | null>(null)
const coverFile = ref<File | null>(null)
const removeCover = ref(false)

const staffOptions = ref<AdminStaffListItemDto[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadStaffOptions() {
  try {
    staffOptions.value = await listAdminStaff(activeClubId.value)
  } catch {
    staffOptions.value = []
  }
}

async function loadProgram() {
  loadState.value = 'loading'
  try {
    await loadStaffOptions()
    if (!isCreate.value && programId.value) {
      const detail = await getAdminProgram(activeClubId.value, programId.value)
      form.slug = detail.slug
      form.programType = (detail.programType as ProgramType) ?? ''
      form.audience = detail.audience ?? ''
      form.ageMin = detail.ageMin ?? null
      form.ageMax = detail.ageMax ?? null
      form.status = (detail.status as 'draft' | 'published') ?? 'draft'
      form.nameZh = detail.zh.name ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.introZh = detail.zh.intro ?? ''
      form.introEn = detail.en?.intro ?? ''
      form.contentZh = detail.zh.content ?? ''
      form.contentEn = detail.en?.content ?? ''
      form.staffIds = detail.staff.map((s) => s.staffId)
      coverKey.value = detail.coverKey ?? null
    }
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') {
      loadState.value = 'not-found'
    } else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}

onMounted(loadProgram)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || coverFile.value !== null || removeCover.value),
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增課程／營隊項目' : `${canManageItems.value ? '編輯' : '檢視'}課程／營隊項目：${form.nameZh || '（未命名）'}`))
const isReadOnly = computed(() => !canManageItems.value)

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.introEn.trim() && !form.contentEn.trim()
}

/** JSON 語法先在前端擋一次——後端只驗證語法合法性（見檔頭說明），送出不合法的 JSON 只會換來
 * 「課程內容不是合法的 JSON 格式」這種 400，不如提早在畫面上說清楚。留空視為沒有內容，不驗證。 */
function validateJsonField(value: string, label: string): boolean {
  if (!value.trim()) return true
  try {
    JSON.parse(value)
    return true
  } catch {
    formError.value = `${label}不是合法的 JSON 格式，請確認內容（或留空）`
    return false
  }
}

function validate(): boolean {
  formError.value = null
  if (!form.slug.trim()) {
    formError.value = '請輸入網址代稱（slug）'
    return false
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入中文名稱'
    return false
  }
  if (form.ageMin != null && form.ageMax != null && form.ageMin > form.ageMax) {
    formError.value = '最小年齡不能大於最大年齡'
    return false
  }
  if (!validateJsonField(form.contentZh, '課程內容（中文）')) return false
  if (!validateJsonField(form.contentEn, '課程內容（英文）')) return false
  return true
}

function buildPayload(): SaveProgramPayload {
  return {
    slug: form.slug.trim(),
    programType: form.programType || null,
    audience: form.audience || null,
    ageMin: form.ageMin,
    ageMax: form.ageMax,
    status: form.status,
    content: {
      zh: { name: form.nameZh.trim(), intro: form.introZh || null, content: form.contentZh || null },
      en: isEnEmpty() ? undefined : { name: form.nameEn || null, intro: form.introEn || null, content: form.contentEn || null },
    },
    staffIds: form.staffIds,
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate.value) {
      const created = await createAdminProgram(activeClubId.value, buildPayload(), coverFile.value)
      ElMessage.success('已建立')
      router.replace(`/programs/items/${created.id}/edit`)
      programId.value = created.id
      coverKey.value = created.coverKey ?? null
    } else {
      const updated = await updateAdminProgram(
        activeClubId.value,
        programId.value!,
        { ...buildPayload(), removeCover: removeCover.value },
        coverFile.value,
      )
      coverKey.value = updated.coverKey ?? null
      ElMessage.success('已儲存')
    }
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/programs/items')
}

function retryLoad() {
  loadProgram()
}
</script>

<template>
  <div class="program-item-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="P1" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個項目，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState !== 'not-found'" type="primary" @click="retryLoad">重新載入</el-button>
        <el-button v-else type="primary" @click="handleBack">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="program-item-edit__form-error"
        @close="formError = null"
      />
      <el-alert
        v-if="isReadOnly"
        title="你的帳號只有檢視權限，沒有這個模組的建立／編輯權限"
        type="info"
        show-icon
        :closable="false"
        class="program-item-edit__form-error"
      />

      <el-form label-position="top" :disabled="isReadOnly">
        <el-card shadow="never" header="基本資料" class="program-item-edit__section">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="網址代稱（slug）" required>
                <el-input v-model="form.slug" placeholder="例如 u8-summer-camp" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="類型">
                <el-select v-model="form.programType" clearable placeholder="請選擇類型" style="width: 100%">
                  <el-option v-for="t in PROGRAM_TYPE_ORDER" :key="t" :label="PROGRAM_TYPE_LABEL[t]" :value="t" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="適合對象">
                <el-input v-model="form.audience" placeholder="例如 國小中低年級" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="最小年齡">
                <el-input-number v-model="form.ageMin" :min="0" :max="99" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="最大年齡">
                <el-input-number v-model="form.ageMax" :min="0" :max="99" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="狀態">
                <el-radio-group v-model="form.status">
                  <el-radio value="draft">草稿</el-radio>
                  <el-radio value="published">已發布</el-radio>
                </el-radio-group>
              </el-form-item>
            </el-col>
          </el-row>

          <BilingualShortField
            label="名稱"
            :zh="form.nameZh"
            :en="form.nameEn"
            required
            @update:zh="(v) => (form.nameZh = v)"
            @update:en="(v) => (form.nameEn = v)"
          />
          <BilingualTextareaField
            label="簡介"
            :zh="form.introZh"
            :en="form.introEn"
            @update:zh="(v) => (form.introZh = v)"
            @update:en="(v) => (form.introEn = v)"
          />
        </el-card>

        <el-card shadow="never" header="課程內容" class="program-item-edit__section">
          <p class="program-item-edit__hint">
            區塊編輯器的原始 JSON 輸出（選填）。後端只檢查語法是否為合法 JSON，不檢查區塊結構；留空表示這個語言版本沒有內文。
          </p>
          <BilingualTextareaField
            label="內容（JSON）"
            :zh="form.contentZh"
            :en="form.contentEn"
            :rows="6"
            @update:zh="(v) => (form.contentZh = v)"
            @update:en="(v) => (form.contentEn = v)"
          />
        </el-card>

        <el-card shadow="never" header="教練團與封面圖" class="program-item-edit__section">
          <el-form-item label="教練團">
            <el-select v-model="form.staffIds" multiple filterable placeholder="請選擇負責教練（可複選）" style="width: 100%">
              <el-option v-for="s in staffOptions" :key="s.id" :label="s.nameZh || '（未命名）'" :value="s.id" />
            </el-select>
          </el-form-item>
          <el-form-item label="合作夥伴">
            <p class="program-item-edit__hint">
              合作夥伴管理（E1）尚未開發，後台目前沒有清單可以選擇，這裡暫不開放設定。
            </p>
          </el-form-item>
          <el-form-item label="封面圖">
            <ImageUploader
              v-model:file="coverFile"
              v-model:remove-cover="removeCover"
              :has-existing-image="!!coverKey"
              :disabled="saving || isReadOnly"
            />
          </el-form-item>
        </el-card>
      </el-form>

      <div v-if="!isReadOnly" class="program-item-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.program-item-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.program-item-edit__form-error {
  margin-bottom: 16px;
}

.program-item-edit__section {
  margin-bottom: 16px;
}

.program-item-edit__hint {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.program-item-edit__action-bar {
  position: fixed;
  bottom: 0;
  left: var(--admin-sidebar-width-expanded);
  right: 0;
  background: var(--admin-bg-surface-2);
  border-top: 1px solid var(--admin-border);
  padding: 12px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  z-index: 10;
}

@media (max-width: 1023px) {
  .program-item-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .program-item-edit__action-bar {
    justify-content: stretch;
  }

  .program-item-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>
