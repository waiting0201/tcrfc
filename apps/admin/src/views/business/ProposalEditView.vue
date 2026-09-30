<script setup lang="ts">
/** 提案——新增／編輯，含提案檔案（PDF 或壓縮檔）的上傳、預覽與移除。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/dateTime'
import { formatFileSize, saveBlob } from '@/utils/downloadFile'
import {
  addProposalFile,
  createProposal,
  deleteProposalFile,
  downloadProposalFile,
  getProposal,
  updateProposal,
  type ProposalDetailDto,
  type ProposalFileDto,
  type ProposalLocale,
} from '@/api/adminProposals'

const route = useRoute()
const router = useRouter()
const isCreate = computed(() => route.name === 'proposal-new')
const proposalId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('business.proposal')

const form = reactive({ title: '', versionNo: 1, status: 'draft' as 'draft' | 'published' })
const files = ref<ProposalFileDto[]>([])
const leadCount = ref(0)
const baselineJson = ref('')
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增提案' : `編輯：${form.title || '（未命名）'}`))

function apply(d: ProposalDetailDto) {
  form.title = d.title
  form.versionNo = d.versionNo
  form.status = d.status
  files.value = d.files
  leadCount.value = d.leadCount
}

async function load() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && proposalId.value) apply(await getProposal(activeClubId.value, proposalId.value))
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

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (!form.title.trim()) {
    formError.value = '請輸入提案名稱'
    return
  }
  if (isCreate.value && form.status === 'published') {
    formError.value = '新提案還沒有檔案，請先存成草稿，上傳檔案後再發布'
    return
  }
  if (!isCreate.value && form.status === 'published' && files.value.length === 0) {
    formError.value = '沒有任何檔案的提案不能發布，請先上傳檔案'
    return
  }
  saving.value = true
  try {
    const payload = { title: form.title.trim(), versionNo: form.versionNo, status: form.status }
    const saved = isCreate.value
      ? await createProposal(activeClubId.value, payload)
      : await updateProposal(activeClubId.value, proposalId.value!, payload)
    if (isCreate.value) {
      proposalId.value = saved.id
      router.replace(`/business/proposals/${saved.id}/edit`)
    }
    apply(saved)
    baselineJson.value = JSON.stringify(form)
    ElMessage.success(isCreate.value ? '已建立，接著可以上傳提案檔案' : '已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

// ── 檔案 ──
const fileInput = ref<HTMLInputElement | null>(null)
const uploadLocale = ref<ProposalLocale>('zh')
const uploadVersion = ref<number | null>(null)
const uploading = ref(false)
const MAX_BYTES = 50 * 1024 * 1024

async function handleFile(event: Event) {
  const el = event.target as HTMLInputElement
  const file = el.files?.[0]
  el.value = ''
  if (!file || !proposalId.value) return
  if (file.size > MAX_BYTES) {
    ElMessage.warning('檔案超過 50 MB，請壓縮後再上傳')
    return
  }
  uploading.value = true
  try {
    const updated = await addProposalFile(
      activeClubId.value,
      proposalId.value,
      { locale: uploadLocale.value, versionNo: uploadVersion.value ?? undefined },
      file,
    )
    files.value = updated.files
    ElMessage.success('檔案已上傳')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '上傳失敗，請稍後再試')
  } finally {
    uploading.value = false
  }
}

async function removeFile(f: ProposalFileDto) {
  try {
    await ElMessageBox.confirm('確定要刪除這個檔案嗎？刪除後立即生效。', '刪除提案檔案', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    const updated = await deleteProposalFile(activeClubId.value, proposalId.value!, f.id)
    files.value = updated.files
    ElMessage.success('已刪除')
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

async function preview(f: ProposalFileDto) {
  try {
    const { blob, filename } = await downloadProposalFile(activeClubId.value, proposalId.value!, f.id)
    saveBlob(blob, filename ?? `${form.title}-${f.locale}-v${f.versionNo}`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '下載失敗，請稍後再試')
  }
}

const back = () => router.push('/business/proposals')
</script>

<template>
  <div class="proposal-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="E3" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這份提案，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="proposal-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視提案，不能修改。" type="info" show-icon :closable="false" class="proposal-edit__block" />
      <el-form label-position="top" :disabled="readOnly">
        <el-card shadow="never" header="提案資料" class="proposal-edit__block">
          <el-form-item label="提案名稱" required><el-input v-model="form.title" maxlength="128" /></el-form-item>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="版本號（用來區分不同版本的成效）"><el-input-number v-model="form.versionNo" :min="1" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12">
              <el-form-item label="狀態">
                <el-radio-group v-model="form.status">
                  <el-radio value="draft">草稿</el-radio>
                  <el-radio value="published">已發布（前台可下載）</el-radio>
                </el-radio-group>
              </el-form-item>
            </el-col>
          </el-row>
          <p v-if="!isCreate" class="proposal-edit__hint">這份提案累計被下載 {{ leadCount }} 次。</p>
        </el-card>
      </el-form>

      <el-card shadow="never" header="提案檔案" class="proposal-edit__block">
        <p v-if="isCreate" class="proposal-edit__hint">請先儲存提案資料，儲存後就能上傳檔案。</p>
        <template v-else>
          <p class="proposal-edit__hint">檔案格式為 PDF 或壓縮檔（ZIP），單檔上限 50 MB，不會有公開網址。上傳與刪除會立即生效。已發布的提案至少要保留一個檔案。</p>
          <el-empty v-if="files.length === 0" description="還沒有上傳檔案" :image-size="64" />
          <div v-else class="proposal-edit__files">
            <div v-for="f in files" :key="f.id" class="proposal-edit__file">
              <div class="proposal-edit__file-main">
                <strong>{{ f.locale === 'zh' ? '中文版' : '英文版' }}・第 {{ f.versionNo }} 版</strong>
                <span class="proposal-edit__hint">{{ formatFileSize(f.fileBytes) }}・{{ formatDateTime(f.createdAt) }} 上傳</span>
              </div>
              <div>
                <el-button size="small" text type="primary" @click="preview(f)">下載檢視</el-button>
                <el-button v-if="canUpdate" size="small" text type="danger" @click="removeFile(f)">刪除</el-button>
              </div>
            </div>
          </div>
          <div v-if="canUpdate" class="proposal-edit__upload">
            <el-select v-model="uploadLocale" class="proposal-edit__upload-field"><el-option label="中文版" value="zh" /><el-option label="英文版" value="en" /></el-select>
            <el-input-number v-model="uploadVersion" :min="1" :controls="false" placeholder="版本號（不填＝同提案）" class="proposal-edit__upload-field" />
            <input ref="fileInput" type="file" accept="application/pdf,application/zip,.pdf,.zip" class="proposal-edit__input" @change="handleFile">
            <el-button :loading="uploading" @click="fileInput?.click()">+ 選擇檔案並上傳</el-button>
          </div>
        </template>
      </el-card>
      <EditActionBar v-if="!readOnly"><el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.proposal-edit { max-width: 780px; margin: 0 auto 88px; }
.proposal-edit__block { margin-bottom: 16px; }
.proposal-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.proposal-edit__files { display: flex; flex-direction: column; gap: 8px; margin-bottom: 12px; }
.proposal-edit__file { display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; padding: 8px 10px; border: 1px solid var(--admin-border); border-radius: 4px; }
.proposal-edit__file-main { display: flex; flex-direction: column; min-width: 0; }
.proposal-edit__upload { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.proposal-edit__upload-field { width: 170px; max-width: 100%; }
.proposal-edit__input { display: none; }
</style>
