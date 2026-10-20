<script setup lang="ts">
/**
 * C4「對手球隊」——新增／編輯（主站規劃書 v3.31）。multipart：`payload`（JSON）＋選填檔案 `crest`（隊徽）。
 * 英文隊名可空；隊名在同一俱樂部內不可重複（後端回 409，訊息直接顯示）。
 * 隊徽適用圖片上傳通則：選檔不上傳、按儲存才一併送出；建立時不可勾「移除隊徽」。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  createAdminOpponentTeam,
  deleteAdminOpponentTeam,
  getAdminOpponentTeam,
  updateAdminOpponentTeam,
  type AdminOpponentTeamDetail,
  type SaveOpponentTeamPayload,
} from '@/api/adminOpponentTeams'

const route = useRoute()
const router = useRouter()
// 🔴 必須是 computed（建立成功後 router.replace 同一元件不會重新掛載）
const isCreate = computed(() => route.name === 'opponent-team-new')
const teamId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate, canDelete } = useCrudPermissions('team.match')

const form = reactive({ name: '', nameEn: '', sortOrder: 0 })
const baselineJson = ref('')
const crestFile = ref<File | null>(null)
const removeCrest = ref(false)
const crestUrl = ref<string | null>(null)
const hasCrest = ref(false)
const usageCount = ref(0)
const matchCount = ref(0)
const standingCount = ref(0)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const deleting = ref(false)
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
watch([crestFile, removeCrest], () => formErrors.clear('crest'))

const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增對手球隊' : `編輯：${form.name || '（未命名）'}`))

function applyDetail(d: AdminOpponentTeamDetail) {
  form.name = d.name ?? ''
  form.nameEn = d.nameEn ?? ''
  form.sortOrder = d.sortOrder
  crestUrl.value = d.crestThumbUrl ?? d.crestUrl ?? null
  hasCrest.value = !!d.crestUrl
  usageCount.value = d.usageCount
  matchCount.value = d.matchCount
  standingCount.value = d.standingCount
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && teamId.value) applyDetail(await getAdminOpponentTeam(activeClubId.value, teamId.value))
    crestFile.value = null
    removeCrest.value = false
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
onMounted(load)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || !!crestFile.value || removeCrest.value),
)
useUnsavedChanges(isDirty)

function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.name.trim()) errors.name = '請填寫對手球隊的中文隊名。'
  else if (form.name.trim().length > 128) errors.name = '中文隊名最多 128 字。'
  if (form.nameEn.trim().length > 128) errors.nameEn = '英文隊名最多 128 字。'
  return errors
}

function buildPayload(): SaveOpponentTeamPayload {
  return {
    name: form.name.trim(),
    nameEn: form.nameEn.trim() || null,
    sortOrder: form.sortOrder,
    // 選了新隊徽就不送移除（兩者同時給後端會回 400）；建立時不可移除
    removeCrest: !isCreate.value && !crestFile.value && removeCrest.value,
  }
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const saved = isCreate.value
      ? await createAdminOpponentTeam(activeClubId.value, buildPayload(), crestFile.value)
      : await updateAdminOpponentTeam(activeClubId.value, teamId.value!, buildPayload(), crestFile.value)
    if (isCreate.value) {
      teamId.value = saved.id
      void router.replace(`/teams/opponent-teams/${saved.id}/edit`)
    }
    applyDetail(saved)
    crestFile.value = null
    removeCrest.value = false
    baselineJson.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  if (!teamId.value) return
  try {
    await ElMessageBox.confirm(`確定要刪除對手球隊「${form.name}」嗎？此動作無法復原。`, '刪除對手球隊', {
      confirmButtonText: '刪除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
  } catch {
    return
  }
  deleting.value = true
  try {
    await deleteAdminOpponentTeam(activeClubId.value, teamId.value)
    baselineJson.value = JSON.stringify(form)
    crestFile.value = null
    removeCrest.value = false
    ElMessage.success('已刪除')
    void router.push('/teams/opponent-teams')
  } catch (error) {
    // 409：被賽事或積分榜用到，訊息已含數量
    formError.value = error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試'
  } finally {
    deleting.value = false
  }
}

const backToList = () => router.push('/teams/opponent-teams')
</script>

<template>
  <div class="opponent-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="backToList"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="C4" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這支對手球隊，可能已被刪除。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="backToList">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="opponent-edit__alert" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視對手球隊，不能修改。" type="info" show-icon :closable="false" class="opponent-edit__alert" />
      <el-alert
        v-if="!isCreate && usageCount > 0"
        :title="`這支球隊目前被 ${matchCount} 場賽事、${standingCount} 筆積分榜使用，修改隊名或隊徽會同步套用到這些地方，也無法刪除。`"
        type="info"
        show-icon
        :closable="false"
        class="opponent-edit__alert"
      />
      <el-form label-position="top" :disabled="readOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField
                    field-zh="name"
                    field-en="nameEn"
                    label="隊名"
                    :zh="form.name"
                    :en="form.nameEn"
                    required
                    :maxlength="128"
                    @update:zh="(v: string) => (form.name = v)"
                    @update:en="(v: string) => (form.nameEn = v)"
                  />
                </FormSection>
                <FormSection v-if="!isCreate && canDelete" title="刪除對手球隊">
                  <p class="opponent-edit__hint">被賽事或積分榜使用的對手球隊不能刪除。</p>
                  <el-button type="danger" plain :loading="deleting" @click="handleDelete">刪除這支對手球隊</el-button>
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormField field="sortOrder" label="排序值">
                  <el-input-number v-model="form.sortOrder" :min="0" />
                  <p class="opponent-edit__hint">數字小的排前面。</p>
                </FormField>
                <FormSection title="隊徽">
                  <FormField field="crest" label="隊徽">
                    <ImageUploader v-model:file="crestFile" v-model:remove-cover="removeCrest" variant="logo" :min-width="0" :min-height="0" :has-existing-image="hasCrest" :existing-preview-url="crestUrl" :disabled="saving || readOnly" />
                  </FormField>
                  <p class="opponent-edit__hint">沒有上傳隊徽時，前台會顯示隊名文字。</p>
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
.opponent-edit { max-width: 1200px; margin: 0 auto; min-width: 0; }
.opponent-edit__alert { margin-bottom: 16px; }
.opponent-edit__hint { margin: 6px 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
