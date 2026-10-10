<script setup lang="ts">
/**
 * 抽獎活動——新增／編輯（活動辦法、獎品說明、資格基準時間、開獎時間、領獎期限與封面）。
 * - 資格條件固定、不可自訂：基準時間當下持有本俱樂部球迷會員會籍（狀態有效，或已批次到期但基準時間落在會籍期間內）且帳號啟用。
 * - 「活動辦法」鎖定名單前必填；名單鎖定後不能再改活動代碼與資格基準時間；已結案或已作廢的活動不能編輯。
 * - 時間以台灣時間輸入。基準時間沒填但有開獎時間時，系統以開獎日（台灣時間）當天 00:00 為準。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageAltField from '@/components/ImageAltField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormSection from '@/components/FormSection.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { createDraw, DRAW_OCCASION_OPTIONS, getDraw, updateDraw, type DrawDetailDto } from '@/api/adminDraws'
import { taipeiInputToUtc, utcToTaipeiInput } from '@/utils/dateTime'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'draw-new')
const drawId = ref<string | undefined>(route.params.id as string | undefined)
const { canUpdate } = useViewUpdatePermissions('member.draw')
const club = computed(() => activeClubId.value)

const form = reactive({
  drawCode: '',
  snapshotAt: '', drawnAt: '', drawOccasion: '', claimDeadlineOn: '',
  internalNote: '',
  nameZh: '', nameEn: '', prizeZh: '', prizeEn: '', rulesZh: '', rulesEn: '', notesZh: '', notesEn: '',
  coverAltZh: '',
  coverAltEn: '',
})
const baselineJson = ref('')
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const coverUrl = ref<string | null>(null)
const hasCover = ref(false)
const coverWidth = ref<number | null>(null)
const coverHeight = ref<number | null>(null)
const status = ref<DrawDetailDto['status']>('draft')
const canEditDraw = ref(true)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有對到欄位的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除封面就清掉該欄位的錯誤
watch([coverFile, removeCover], () => formErrors.clear('cover'))
const readOnly = computed(() => !canUpdate.value || !canEditDraw.value)
/** 名單鎖定後不能改活動代碼與資格基準時間 */
const locked = computed(() => !isCreate.value && status.value !== 'draft')
const pageTitle = computed(() => (isCreate.value ? '新增抽獎活動' : `編輯：${form.nameZh || form.drawCode}`))

function apply(d: DrawDetailDto) {
  form.drawCode = d.drawCode
  form.snapshotAt = utcToTaipeiInput(d.snapshotAt)
  form.drawnAt = utcToTaipeiInput(d.drawnAt)
  form.drawOccasion = d.drawOccasion ?? ''
  form.claimDeadlineOn = d.claimDeadlineOn ?? ''
  form.internalNote = d.internalNote ?? ''
  form.nameZh = d.zh?.name ?? ''; form.nameEn = d.en?.name ?? ''
  form.prizeZh = d.zh?.prizeDescription ?? ''; form.prizeEn = d.en?.prizeDescription ?? ''
  form.rulesZh = d.zh?.rules ?? ''; form.rulesEn = d.en?.rules ?? ''
  form.notesZh = d.zh?.notes ?? ''; form.notesEn = d.en?.notes ?? ''
  coverUrl.value = d.coverThumbUrl ?? d.coverUrl ?? null
  hasCover.value = !!d.coverKey
  coverWidth.value = d.coverWidth ?? null
  coverHeight.value = d.coverHeight ?? null
  form.coverAltZh = d.zh?.coverAlt ?? ''
  form.coverAltEn = d.en?.coverAlt ?? ''
  status.value = d.status
  canEditDraw.value = d.availableActions.includes('edit')
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && drawId.value) apply(await getDraw(club.value, drawId.value))
    coverFile.value = null
    removeCover.value = false
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)

const isDirty = computed(() => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!coverFile.value || removeCover.value))
useUnsavedChanges(isDirty)

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文活動名稱'
  if (form.drawCode.trim() && !/^[A-Za-z0-9-]{1,32}$/.test(form.drawCode.trim())) errors.drawCode = '活動代碼只能用英數字與連字號，最多 32 字'
  return errors
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  const payload = {
    drawCode: locked.value ? undefined : nullIfBlank(form.drawCode) ?? undefined,
    snapshotAt: locked.value ? undefined : taipeiInputToUtc(form.snapshotAt),
    drawnAt: taipeiInputToUtc(form.drawnAt),
    drawOccasion: form.drawOccasion || null,
    claimDeadlineOn: form.claimDeadlineOn || null,
    internalNote: nullIfBlank(form.internalNote),
    removeCover: coverFile.value ? false : removeCover.value,
    content: {
      zh: { name: form.nameZh.trim(), prizeDescription: nullIfBlank(form.prizeZh), rules: nullIfBlank(form.rulesZh), notes: nullIfBlank(form.notesZh), coverAlt: nullIfBlank(form.coverAltZh) },
      en: enOrUndefined(
        { name: form.nameEn.trim(), prizeDescription: nullIfBlank(form.prizeEn) as string, rules: nullIfBlank(form.rulesEn) as string, notes: nullIfBlank(form.notesEn) as string, coverAlt: nullIfBlank(form.coverAltEn) as string },
        'name', 'prizeDescription', 'rules', 'notes', 'coverAlt',
      ),
    },
  }
  try {
    const wasCreate = isCreate.value
    const saved = wasCreate ? await createDraw(club.value, payload, coverFile.value) : await updateDraw(club.value, drawId.value!, payload, coverFile.value)
    ElMessage.success('已儲存')
    baselineJson.value = JSON.stringify(form)
    coverFile.value = null
    removeCover.value = false
    // 儲存後回到活動管理頁，接著就能產生名單
    router.replace(`/members/lottery/${saved?.id ?? drawId.value}`)
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function back() {
  router.push(isCreate.value || !drawId.value ? '/members/lottery' : `/members/lottery/${drawId.value}`)
}
</script>

<template>
  <div class="draw-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="K5" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這個抽獎活動，可能不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="router.push('/members/lottery')">返回清單</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="draw-edit__block" @close="formError = null" />
      <el-alert v-if="!canUpdate" title="你的帳號只能檢視抽獎活動，不能修改。" type="info" show-icon :closable="false" class="draw-edit__block" />
      <el-alert v-else-if="!canEditDraw" title="這個活動已結案或已作廢，不能再編輯。" type="info" show-icon :closable="false" class="draw-edit__block" />
      <el-alert v-if="locked && !readOnly" title="名單已鎖定：活動代碼與資格基準時間不能再修改。" type="info" show-icon :closable="false" class="draw-edit__block" />

      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField field="name" label="活動名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                  <BilingualTextareaField field="prize" label="獎品說明" :zh="form.prizeZh" :en="form.prizeEn" :rows="3" @update:zh="(v) => (form.prizeZh = v)" @update:en="(v) => (form.prizeEn = v)" />
                  <BilingualTextareaField field="rules" label="活動辦法（鎖定名單前必填）" :zh="form.rulesZh" :en="form.rulesEn" :rows="5" @update:zh="(v) => (form.rulesZh = v)" @update:en="(v) => (form.rulesEn = v)" />
                  <p class="draw-edit__hint">活動辦法必須明示：<strong>同時具備兩隊會籍的人可以分別參加兩隊的抽獎</strong>。</p>
                  <BilingualTextareaField field="notes" label="注意事項" :zh="form.notesZh" :en="form.notesEn" :rows="3" @update:zh="(v) => (form.notesZh = v)" @update:en="(v) => (form.notesEn = v)" />
                </FormSection>

                <FormSection title="內部備註">
                  <FormField field="internalNote" label="內部備註（只有後台看得到）"><el-input v-model="form.internalNote" type="textarea" :rows="3" maxlength="500" show-word-limit /></FormField>
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection title="時間與代碼">
                  <el-row :gutter="12">
                    <el-col :span="24"><FormField field="snapshotAt" label="資格基準時間（台灣時間）"><el-date-picker v-model="form.snapshotAt" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" :disabled="readOnly || locked" style="width: 100%" /></FormField></el-col>
                    <el-col :span="24"><el-form-item label="開獎時間（台灣時間）"><el-date-picker v-model="form.drawnAt" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" style="width: 100%" /></el-form-item></el-col>
                    <el-col :span="24">
                      <FormField field="drawOccasion" label="舉辦場合">
                        <el-select v-model="form.drawOccasion" clearable placeholder="未指定" style="width: 100%"><el-option v-for="o in DRAW_OCCASION_OPTIONS" :key="o.value" :label="o.label" :value="o.value" /></el-select>
                      </FormField>
                    </el-col>
                    <el-col :span="24"><el-form-item label="領獎期限"><el-date-picker v-model="form.claimDeadlineOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
                    <el-col :span="24"><FormField field="drawCode" label="活動代碼（選填）"><el-input v-model="form.drawCode" maxlength="32" :disabled="readOnly || locked" placeholder="留空由系統自動產生" /></FormField></el-col>
                  </el-row>
                  <p class="draw-edit__hint">基準時間沒填、但有開獎時間時，以開獎日（台灣時間）當天 00:00 為準。活動代碼只能用英數字與連字號，會出現在匯出的檔名裡。</p>
                </FormSection>

                <FormSection title="封面圖片">
                  <FormField field="cover" label="封面圖片">
                    <ImageUploader v-model:file="coverFile" v-model:remove-cover="removeCover" :min-width="0" :min-height="0" :has-existing-image="hasCover" :existing-preview-url="coverUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <ImageAltField
                    v-model:zh="form.coverAltZh"
                    v-model:en="form.coverAltEn"
                    field="coverAlt"
                    :has-image="(hasCover && !removeCover) || coverFile !== null"
                    :width="coverWidth"
                    :height="coverHeight"
                    fallback="抽獎活動名稱"
                  />
                </FormSection>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>
      <EditActionBar v-if="!readOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.draw-edit { max-width: 1200px; margin: 0 auto; }
.draw-edit__block { margin-bottom: 16px; }
.draw-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
