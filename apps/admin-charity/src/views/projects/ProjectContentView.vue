<script setup lang="ts">
/**
 * 捐款項目的內文編輯：一句話介紹、說明、善款用途（中英各一份）。
 * 呼叫 `PUT /projects/{id}/content`，這個端點的語意是「省略＝不變」：
 *   · 只把「有改動的欄位」放進請求；沒動的欄位完全不送，所以不會蓋掉別人同時改的內容，
 *     也不會把還沒翻譯的英文欄位誤清空。
 *   · 把欄位改成空白要明確送出清空：文字欄位送空字串，說明內文送空物件。
 * 這個端點不碰分潤、金額選項與撥付對象，所以沒有分潤授權的角色（例如商務）也能編輯內文。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { onBeforeRouteLeave } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BlockEditor from '@/components/BlockEditor.vue'
import ErrorState from '@/components/ErrorState.vue'
import { getProject, updateProjectContent, type ProjectContentInput, type ProjectDetail } from '@/api/projects'
import { AdminApiError } from '@/api/http'
import { hasPermission } from '@/auth/session'
import { blocksKey, parseBlocks, serializeBlocks, type EditorBlock } from '@/utils/blocks'

const props = defineProps<{ projectKey: string }>()
const router = useRouter()

const canManage = computed(() => hasPermission('n2.donation_project.manage'))

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const project = ref<ProjectDetail | null>(null)

const form = reactive({
  oneLinerZh: '',
  oneLinerEn: '',
  fundUsageZh: '',
  fundUsageEn: '',
})
const descriptionZh = ref<EditorBlock[]>([])
const descriptionEn = ref<EditorBlock[]>([])

/** 載入當下的標準化內容，拿來判斷哪些欄位有改動。 */
const baseline = reactive({ oneLinerZh: '', oneLinerEn: '', fundUsageZh: '', fundUsageEn: '', descriptionZh: '', descriptionEn: '' })

function fill(p: ProjectDetail) {
  project.value = p
  form.oneLinerZh = p.oneLinerZh ?? ''
  form.oneLinerEn = p.oneLinerEn ?? ''
  form.fundUsageZh = p.fundUsageZh ?? ''
  form.fundUsageEn = p.fundUsageEn ?? ''
  descriptionZh.value = parseBlocks(p.descriptionZh)
  descriptionEn.value = parseBlocks(p.descriptionEn)
  baseline.oneLinerZh = form.oneLinerZh.trim()
  baseline.oneLinerEn = form.oneLinerEn.trim()
  baseline.fundUsageZh = form.fundUsageZh.trim()
  baseline.fundUsageEn = form.fundUsageEn.trim()
  baseline.descriptionZh = blocksKey(descriptionZh.value)
  baseline.descriptionEn = blocksKey(descriptionEn.value)
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    fill(await getProject(props.projectKey))
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取資料時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)

/** 只列出有改動的欄位（省略＝不變）。 */
function buildChanges(): ProjectContentInput {
  const changes: ProjectContentInput = {}
  const text = ['oneLinerZh', 'oneLinerEn', 'fundUsageZh', 'fundUsageEn'] as const
  for (const key of text) {
    const now = form[key].trim()
    if (now !== baseline[key]) changes[key] = now
  }
  if (blocksKey(descriptionZh.value) !== baseline.descriptionZh) changes.descriptionZh = serializeBlocks(descriptionZh.value)
  if (blocksKey(descriptionEn.value) !== baseline.descriptionEn) changes.descriptionEn = serializeBlocks(descriptionEn.value)
  return changes
}

const dirty = computed(() => Object.keys(buildChanges()).length > 0)

const CHANGE_LABELS: Record<keyof ProjectContentInput, string> = {
  oneLinerZh: '一句話介紹（中文）',
  oneLinerEn: '一句話介紹（英文）',
  descriptionZh: '說明（中文）',
  descriptionEn: '說明（英文）',
  fundUsageZh: '善款用途（中文）',
  fundUsageEn: '善款用途（英文）',
}

async function save() {
  if (saving.value) return
  const changes = buildChanges()
  const keys = Object.keys(changes) as (keyof ProjectContentInput)[]
  if (keys.length === 0) {
    ElMessage.info('沒有任何變更')
    return
  }
  saving.value = true
  try {
    fill(await updateProjectContent(props.projectKey, changes))
    ElMessage.success(`已儲存：${keys.map((k) => CHANGE_LABELS[k]).join('、')}`)
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試')
  } finally {
    saving.value = false
  }
}

// 有未儲存的修改時離開頁面要確認，避免一個誤點就丟掉寫了很久的說明。
onBeforeRouteLeave(async () => {
  if (!dirty.value || saving.value) return true
  try {
    await ElMessageBox.confirm('有尚未儲存的修改，確定要離開嗎？', '離開前確認', {
      confirmButtonText: '離開',
      cancelButtonText: '留在這裡',
      type: 'warning',
    })
    return true
  } catch {
    return false
  }
})
</script>

<template>
  <div>
    <PageHeader title="編輯項目內文" frontend-unit="項目詳情頁">
      <template #back>
        <el-button text @click="router.push('/projects')">返回列表</el-button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <el-form v-else v-loading="loading" label-position="top" class="project-content__form" :disabled="!canManage" @submit.prevent>
      <p v-if="project" class="project-content__name">
        {{ project.nameZh }}
        <span v-if="project.nameEn" class="project-content__name-en">{{ project.nameEn }}</span>
      </p>
      <el-alert
        v-if="!canManage"
        type="info"
        :closable="false"
        show-icon
        title="你的角色只能檢視這些內容，不能修改。"
        class="project-content__alert"
      />
      <el-alert
        type="info"
        :closable="false"
        show-icon
        title="只有你改過的欄位才會被儲存；沒動的欄位（例如還沒翻譯的英文）不會被影響。把欄位清空再儲存，就是清掉該欄位的內容。"
        class="project-content__alert"
      />

      <div class="project-content__fieldset">
        <BilingualShortField
          label="一句話介紹"
          :zh="form.oneLinerZh"
          :en="form.oneLinerEn"
          placeholder="顯示在項目卡片與詳情頁標題下方"
          @update:zh="form.oneLinerZh = $event"
          @update:en="form.oneLinerEn = $event"
        />

        <h2 class="project-content__section-title">說明</h2>
        <el-tabs>
          <el-tab-pane label="中文">
            <BlockEditor v-model="descriptionZh" label="說明（中文）" />
          </el-tab-pane>
          <el-tab-pane label="英文">
            <BlockEditor v-model="descriptionEn" label="說明（英文）" />
          </el-tab-pane>
        </el-tabs>

        <h2 class="project-content__section-title">善款用途</h2>
        <el-tabs>
          <el-tab-pane label="中文">
            <el-input v-model="form.fundUsageZh" type="textarea" :rows="5" aria-label="善款用途（中文）" />
          </el-tab-pane>
          <el-tab-pane label="英文">
            <el-input v-model="form.fundUsageEn" type="textarea" :rows="5" aria-label="善款用途（英文）" />
          </el-tab-pane>
        </el-tabs>
      </div>

      <div class="project-content__actions">
        <el-button @click="router.push('/projects')">返回列表</el-button>
        <el-button v-if="canManage" type="primary" :loading="saving" :disabled="!dirty" @click="save">儲存</el-button>
      </div>
    </el-form>
  </div>
</template>

<style scoped>
.project-content__form {
  max-width: 760px;
}

.project-content__name {
  margin: 0 0 var(--charity-admin-space-3);
  font-size: 16px;
  font-weight: 600;
}

.project-content__name-en {
  margin-left: var(--charity-admin-space-2);
  font-size: 13px;
  font-weight: 400;
  color: var(--charity-admin-text-secondary);
}

.project-content__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.project-content__fieldset {
  border: none;
  padding: 0;
  margin: 0;
  min-width: 0;
}

.project-content__section-title {
  font-size: 15px;
  margin: var(--charity-admin-space-6) 0 var(--charity-admin-space-3);
  padding-top: var(--charity-admin-space-3);
  border-top: 1px solid var(--charity-admin-border);
}

.project-content__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-6);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
