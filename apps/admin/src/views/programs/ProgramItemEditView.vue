<script setup lang="ts">
/**
 * P1 課程／營隊項目——編輯頁。對照 apps/api/README.md「S1-9」。
 *
 * 課程內容（`content`）重用靜態頁（B1）的區塊編輯器（`PageBlockListEditor`），存成與靜態頁相同的
 * 區塊 JSON 陣列 `[{ blockType, content }]`（區塊內的雙語欄位是 `{ zh, en }`，由前台依語系擇一）。
 * - 只開放不需要圖片上傳的 7 種區塊（文字、引言、數據卡、步驟條、時間軸、表格、CTA）：
 *   課程儲存端點沒有區塊圖片的上傳通道，前台也只渲染這幾種。
 * - 只有一份區塊清單（雙語欄位在區塊內編輯），儲存時同一份 JSON 寫進中文版，英文版有建立時一併寫入。
 * - 舊資料若不是區塊 JSON（純文字），載入時轉成一個「文字」區塊，內容不會遺失；
 *   區塊 JSON 裡本編輯器不支援的類型原樣保留、儲存時附在最後。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormSection from '@/components/FormSection.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import PageBlockListEditor from '@/components/pageBlocks/PageBlockListEditor.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminStaff, type AdminStaffListItemDto } from '@/api/adminStaff'
import { listPartners, type PartnerListItemDto } from '@/api/adminPartners'
import { parseBlockContent, serializeBlocksForSubmit, PageBlockValidationError } from '@/utils/pageBlockSerializer'
import { createEmptyBlock, type PageBlockState, type PageBlockType } from '@/types/pageBlocks'
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
  audienceEn: '',
  ageMin: null as number | null,
  ageMax: null as number | null,
  status: 'draft' as 'draft' | 'published',
  nameZh: '',
  nameEn: '',
  introZh: '',
  introEn: '',
  staffIds: [] as string[],
  partnerIds: [] as string[],
})

/** 課程內容可用的區塊類型：不含需要圖片上傳的「圖文左右」「圖片藝廊」，也不含前台尚未渲染的類型。 */
const CONTENT_BLOCK_TYPES: PageBlockType[] = ['text', 'quote', 'stat_cards', 'steps', 'timeline', 'table', 'cta']
const blocks = ref<PageBlockState[]>([])
/** 區塊 JSON 裡本編輯器不支援的區塊（舊資料），原樣保留、儲存時附在最後。 */
const preservedBlocks = ref<unknown[]>([])

/** 把後端存的內容字串轉成區塊清單。非 JSON 的舊純文字轉成一個文字區塊。 */
function parseContent(zhRaw: string | null | undefined, enRaw: string | null | undefined): { blocks: PageBlockState[]; preserved: unknown[] } {
  const zh = (zhRaw ?? '').trim()
  const en = (enRaw ?? '').trim()
  const source = zh || en
  if (!source) return { blocks: [], preserved: [] }
  let parsed: unknown
  try {
    parsed = JSON.parse(source)
  } catch {
    parsed = undefined
  }
  if (Array.isArray(parsed)) {
    const result: PageBlockState[] = []
    const preserved: unknown[] = []
    for (const item of parsed) {
      const raw = (item ?? {}) as { blockType?: unknown; content?: unknown }
      if (typeof raw.blockType === 'string' && (CONTENT_BLOCK_TYPES as string[]).includes(raw.blockType)) {
        const block = createEmptyBlock(raw.blockType as PageBlockType)
        block.content = parseBlockContent(raw.blockType as PageBlockType, raw.content)
        result.push(block)
      } else {
        preserved.push(item)
      }
    }
    return { blocks: result, preserved }
  }
  // 純文字（或不是區塊清單的 JSON）：整段轉成一個文字區塊，中英各自帶入，不遺失內容。
  const block = createEmptyBlock('text')
  block.content = { body: { zh, en: en !== zh ? en : '' } }
  return { blocks: [block], preserved: [] }
}
const baselineJson = ref('')
function snapshot(): string {
  return JSON.stringify({ form, blocks: blocks.value })
}
const coverKey = ref<string | null>(null)
const coverFile = ref<File | null>(null)
const removeCover = ref(false)

const staffOptions = ref<AdminStaffListItemDto[]>([])
const partnerOptions = ref<{ id: string; label: string }[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除封面就清掉該欄位的錯誤
watch([coverFile, removeCover], () => formErrors.clear('cover'))

async function loadStaffOptions() {
  try {
    staffOptions.value = await listAdminStaff(activeClubId.value)
  } catch {
    staffOptions.value = []
  }
}

/** 列出本俱樂部已發布的夥伴；已選但目前未發布的夥伴由 `loadProgram` 補進選項，避免顯示成一串編號。 */
async function loadPartnerOptions() {
  try {
    const list: PartnerListItemDto[] = await listPartners(activeClubId.value)
    partnerOptions.value = list.filter((p) => p.isActive).map((p) => ({ id: p.id, label: p.nameZh || p.slug }))
  } catch {
    partnerOptions.value = []
  }
}

async function loadProgram() {
  loadState.value = 'loading'
  try {
    await Promise.all([loadStaffOptions(), loadPartnerOptions()])
    if (!isCreate.value && programId.value) {
      const detail = await getAdminProgram(activeClubId.value, programId.value)
      form.slug = detail.slug
      form.programType = (detail.programType as ProgramType) ?? ''
      form.audience = detail.audience ?? ''
      form.audienceEn = detail.audienceEn ?? ''
      form.ageMin = detail.ageMin ?? null
      form.ageMax = detail.ageMax ?? null
      form.status = (detail.status as 'draft' | 'published') ?? 'draft'
      form.nameZh = detail.zh.name ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.introZh = detail.zh.intro ?? ''
      form.introEn = detail.en?.intro ?? ''
      const parsed = parseContent(detail.zh.content, detail.en?.content)
      blocks.value = parsed.blocks
      preservedBlocks.value = parsed.preserved
      form.staffIds = detail.staff.map((s) => s.staffId)
      form.partnerIds = detail.partners.map((p) => p.partnerId)
      for (const p of detail.partners) {
        if (!partnerOptions.value.some((o) => o.id === p.partnerId)) {
          partnerOptions.value.push({ id: p.partnerId, label: `${p.slug}（未發布）` })
        }
      }
      coverKey.value = detail.coverKey ?? null
    }
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = snapshot()
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
  () => loadState.value === 'ready' && (snapshot() !== baselineJson.value || coverFile.value !== null || removeCover.value),
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增課程／營隊項目' : `${canManageItems.value ? '編輯' : '檢視'}課程／營隊項目：${form.nameZh || '（未命名）'}`))
const isReadOnly = computed(() => !canManageItems.value)

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.introEn.trim()
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.slug.trim()) errors.slug = '請輸入網址代稱'
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文名稱'
  if (form.audience.trim().length > 64) errors.audience = '適合對象（中文）最多 64 字'
  if (form.audienceEn.trim().length > 64) errors.audienceEn = '適合對象（英文）最多 64 字'
  if (form.ageMin != null && form.ageMax != null && form.ageMin > form.ageMax) {
    errors.ageMax = '最小年齡不能大於最大年齡'
  }
  // 區塊內容的驗證在序列化工具裡（遇錯即丟例外），逐區塊各跑一次，每個區塊最多標一則錯誤。
  blocks.value.forEach((block, index) => {
    try {
      serializeBlocksForSubmit([block])
    } catch (error) {
      errors[`blocks[${index}]`] =
        error instanceof PageBlockValidationError
          ? error.message.replace(/^第 \d+ 個區塊/, `第 ${index + 1} 個區塊`)
          : '這個區塊的內容有誤，請檢查後再試'
    }
  })
  return errors
}

/** 區塊清單 → 與靜態頁相同的區塊 JSON 陣列文字；沒有任何區塊就回 `null`（這個項目沒有內文）。 */
function buildContentJson(): string | null {
  const serialized = serializeBlocksForSubmit(blocks.value).blocks
  const all = [...serialized, ...preservedBlocks.value]
  return all.length > 0 ? JSON.stringify(all) : null
}

function buildPayload(): SaveProgramPayload {
  const contentJson = buildContentJson()
  return {
    slug: form.slug.trim(),
    programType: form.programType || null,
    audience: form.audience.trim() || null,
    audienceEn: form.audienceEn.trim() || null,
    ageMin: form.ageMin,
    ageMax: form.ageMax,
    status: form.status,
    content: {
      zh: { name: form.nameZh.trim(), intro: form.introZh || null, content: contentJson },
      en: isEnEmpty() ? undefined : { name: form.nameEn || null, intro: form.introEn || null, content: contentJson },
    },
    staffIds: form.staffIds,
    partnerIds: form.partnerIds,
  }
}

async function handleSave() {
  if (isReadOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
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
    baselineJson.value = snapshot()
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
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
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField
                    field="name"
                    label="名稱"
                    :zh="form.nameZh"
                    :en="form.nameEn"
                    required
                    @update:zh="(v) => (form.nameZh = v)"
                    @update:en="(v) => (form.nameEn = v)"
                  />
                  <BilingualTextareaField
                    field="intro"
                    label="簡介"
                    :zh="form.introZh"
                    :en="form.introEn"
                    @update:zh="(v) => (form.introZh = v)"
                    @update:en="(v) => (form.introEn = v)"
                  />
                  <BilingualShortField
                    field="audience"
                    label="適合對象"
                    :zh="form.audience"
                    :en="form.audienceEn"
                    :maxlength="64"
                    placeholder="例如 國小中低年級"
                    @update:zh="(v: string) => (form.audience = v)"
                    @update:en="(v: string) => (form.audienceEn = v)"
                  />
                </FormSection>

                <FormSection title="課程內容">
                  <PageBlockListEditor
                    v-model="blocks"
                    :allowed-types="CONTENT_BLOCK_TYPES"
                    empty-hint="目前還沒有任何課程內容，從下方選一種類型開始新增；留空表示這個項目沒有內文。"
                  />
                </FormSection>

                <FormSection title="教練團與合作夥伴">
                  <FormField field="staffIds" label="教練團">
                    <el-select v-model="form.staffIds" multiple filterable placeholder="請選擇負責教練（可複選）" style="width: 100%">
                      <el-option v-for="s in staffOptions" :key="s.id" :label="s.nameZh || '（未命名）'" :value="s.id" />
                    </el-select>
                  </FormField>
                  <FormField field="partnerIds" label="合作夥伴">
                    <el-select v-model="form.partnerIds" multiple filterable placeholder="請選擇合作夥伴（可複選）" style="width: 100%">
                      <el-option v-for="p in partnerOptions" :key="p.id" :label="p.label" :value="p.id" />
                    </el-select>
                  </FormField>
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection title="項目資料">
                  <FormField field="slug" label="網址代稱" required>
                    <el-input v-model="form.slug" placeholder="例如 u8-summer-camp" />
                  </FormField>
                  <FormField field="programType" label="類型">
                    <el-select v-model="form.programType" clearable placeholder="請選擇類型" style="width: 100%">
                      <el-option v-for="t in PROGRAM_TYPE_ORDER" :key="t" :label="PROGRAM_TYPE_LABEL[t]" :value="t" />
                    </el-select>
                  </FormField>
                  <el-row :gutter="12">
                    <el-col :span="24">
                      <FormField field="ageMin" label="最小年齡">
                        <el-input-number v-model="form.ageMin" :min="0" :max="99" style="width: 100%" @change="formErrors.clear('ageMin')" />
                      </FormField>
                    </el-col>
                    <el-col :span="24">
                      <FormField field="ageMax" label="最大年齡">
                        <el-input-number v-model="form.ageMax" :min="0" :max="99" style="width: 100%" @change="formErrors.clear('ageMax')" />
                      </FormField>
                    </el-col>
                  </el-row>
                </FormSection>

                <FormSection title="封面圖">
                  <FormField field="cover" label="封面圖">
                    <ImageUploader
                      v-model:file="coverFile"
                      v-model:remove-cover="removeCover"
                      :has-existing-image="!!coverKey"
                      :disabled="saving || isReadOnly"
                    />
                  </FormField>
                </FormSection>
              </el-card>

              <el-card shadow="never" header="發布設定">
                <FormField field="status" label="狀態">
                  <el-radio-group v-model="form.status">
                    <el-radio value="draft">草稿</el-radio>
                    <el-radio value="published">已發布</el-radio>
                  </el-radio-group>
                </FormField>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>

      <EditActionBar v-if="!isReadOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.program-item-edit {
  max-width: 1200px;
  margin: 0 auto;
}

.program-item-edit__form-error {
  margin-bottom: 16px;
}
</style>
