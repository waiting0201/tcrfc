<script setup lang="ts">
/**
 * H5 AI 爬蟲授權（對應主站規劃書 §7 `GEO-02`／§4.8「AI 爬蟲授權」；apps/api/README.md
 * 「S1-12b」）。單一設定表單：AI 服務清單（允許／拒絕，可新增刪除）＋ 後台自訂排除路徑
 * （可新增刪除）＋ 系統保護的頁面（唯讀陳列，規格強制、後台不能刪除，見下方說明）。
 *
 * 🔴 「系統保護的頁面」對應後端 `mandatoryExcludePaths`——這個清單程式碼寫死，
 * `AdminCrawlerSettingsDto` 本身沒有可寫入欄位承載它，這裡只顯示、不提供任何刪除操作
 * （docs/14-invariants.md：「這條排除是個資防線，不是 SEO 設定，不得為了『讓 AI 多抓一點』而放寬」）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import {
  getAdminCrawlerSettings,
  updateAdminCrawlerSettings,
  type AdminCrawlerSettingsDto,
  type CrawlerAgentDto,
} from '@/api/adminSeo'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)

const USER_AGENT_PATTERN = /^[A-Za-z0-9._-]{1,100}$/

const loadState = ref<'loading' | 'error' | 'ready'>('loading')
const loadErrorMessage = ref('')

const agents = reactive<CrawlerAgentDto[]>([])
const additionalPaths = reactive<{ value: string }[]>([])
const mandatoryPaths = ref<string[]>([])
const baselineJson = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

function snapshot() {
  return JSON.stringify({ agents, additionalPaths: additionalPaths.map((p) => p.value) })
}

function applyLoaded(dto: AdminCrawlerSettingsDto) {
  agents.splice(0, agents.length, ...dto.userAgents.map((a) => ({ ...a })))
  additionalPaths.splice(0, additionalPaths.length, ...dto.additionalExcludePaths.map((p) => ({ value: p })))
  mandatoryPaths.value = dto.mandatoryExcludePaths
  baselineJson.value = snapshot()
}

async function loadSettings() {
  loadState.value = 'loading'
  try {
    const dto = await getAdminCrawlerSettings(club.value)
    applyLoaded(dto)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadSettings)
watch(club, loadSettings)

const isDirty = computed(() => loadState.value === 'ready' && snapshot() !== baselineJson.value)
useUnsavedChanges(isDirty)

function addAgent() {
  agents.push({ userAgent: '', allowed: true })
}

function removeAgent(index: number) {
  agents.splice(index, 1)
}

function addPath() {
  additionalPaths.push({ value: '' })
}

function removePath(index: number) {
  additionalPaths.splice(index, 1)
}

function validate(): boolean {
  formError.value = null

  const seenAgents = new Set<string>()
  for (const agent of agents) {
    const name = agent.userAgent.trim()
    if (!name) {
      formError.value = 'AI 服務名稱不能留空，請刪除空白列或填入名稱'
      return false
    }
    if (!USER_AGENT_PATTERN.test(name)) {
      formError.value = `「${name}」格式不正確，只能包含英數字、句點、連字號或底線`
      return false
    }
    const key = name.toLowerCase()
    if (seenAgents.has(key)) {
      formError.value = `「${name}」重複，同一份清單不能有兩筆相同的名稱`
      return false
    }
    seenAgents.add(key)
  }

  for (const path of additionalPaths) {
    const value = path.value.trim()
    if (!value) {
      formError.value = '排除路徑不能留空，請刪除空白列或填入路徑'
      return false
    }
    if (!value.startsWith('/')) {
      formError.value = `「${value}」必須以「/」開頭`
      return false
    }
    if (!value.endsWith('/')) {
      formError.value = `「${value}」必須以「/」結尾（僅接受目錄前綴）`
      return false
    }
    if (/\s/.test(value)) {
      formError.value = `「${value}」不能包含空白字元`
      return false
    }
  }

  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    const saved = await updateAdminCrawlerSettings(club.value, {
      userAgents: agents.map((a) => ({ userAgent: a.userAgent.trim(), allowed: a.allowed })),
      additionalExcludePaths: additionalPaths.map((p) => p.value.trim()),
    })
    applyLoaded(saved)
    ElMessage.success('已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="ai-crawler">
    <PageHeader title="AI 爬蟲授權">
      <template #meta>
        <FrontendUnitBanner module-code="H" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="10" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadSettings">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert type="warning" :closable="false" show-icon class="ai-crawler__notice">
        目前網站尚未正式上線，這裡的設定要等<strong>網站正式上線後才會生效</strong>——上線前不論這裡設定什麼，AI 爬蟲一律讀不到本站任何內容。
      </el-alert>

      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="ai-crawler__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="AI 服務清單" class="ai-crawler__section">
        <p class="ai-crawler__hint">
          列出想要明確表態的 AI 服務（例如 GPTBot、ClaudeBot、PerplexityBot 這類讀取網站內容的程式），並設定是否允許它讀取本站。清單以外的其他一般搜尋引擎或爬蟲不受這裡影響，一律遵守「搜尋引擎收錄規則」的整體設定。
        </p>
        <div v-if="agents.length > 0" class="ai-crawler__agent-list">
          <div v-for="(agent, index) in agents" :key="index" class="ai-crawler__agent-row">
            <el-input v-model="agent.userAgent" placeholder="例如：GPTBot" class="ai-crawler__agent-name" />
            <div class="ai-crawler__agent-toggle">
              <el-switch v-model="agent.allowed" active-text="允許" inactive-text="拒絕" inline-prompt />
            </div>
            <el-button text type="danger" @click="removeAgent(index)">刪除</el-button>
          </div>
        </div>
        <el-empty v-else description="目前沒有設定任何 AI 服務" :image-size="64" />
        <el-button class="ai-crawler__add-button" @click="addAgent">+ 新增 AI 服務</el-button>
      </el-card>

      <el-card shadow="never" header="自訂不開放的頁面路徑" class="ai-crawler__section">
        <p class="ai-crawler__hint">
          除了下方「系統保護的頁面」以外，如果還有其他不想讓 AI 讀取的頁面範圍，可以在這裡自行新增，格式是以「/」開頭與結尾的路徑（例如 <code>/zh/private-event/</code>）。
        </p>
        <div v-if="additionalPaths.length > 0" class="ai-crawler__path-list">
          <div v-for="(path, index) in additionalPaths" :key="index" class="ai-crawler__path-row">
            <el-input v-model="path.value" placeholder="例如：/zh/private-event/" class="ai-crawler__path-input" />
            <el-button text type="danger" @click="removePath(index)">刪除</el-button>
          </div>
        </div>
        <el-empty v-else description="目前沒有自訂的排除路徑" :image-size="64" />
        <el-button class="ai-crawler__add-button" @click="addPath">+ 新增路徑</el-button>
      </el-card>

      <el-card shadow="never" header="系統保護的頁面（不可移除）" class="ai-crawler__section">
        <p class="ai-crawler__hint">
          以下頁面範圍為了保護會員資料與未成年學員照片，系統一律禁止任何 AI 讀取，這份清單由系統固定管理，後台無法刪除或關閉。
        </p>
        <div class="ai-crawler__mandatory-list">
          <el-tag v-for="path in mandatoryPaths" :key="path" size="large" class="ai-crawler__mandatory-tag" effect="plain">
            {{ path }}
          </el-tag>
        </div>
      </el-card>

      <div class="ai-crawler__actions">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.ai-crawler {
  max-width: 780px;
  margin: 0 auto;
}

.ai-crawler__notice,
.ai-crawler__form-error {
  margin-bottom: 12px;
}

.ai-crawler__section {
  margin-bottom: 12px;
}

.ai-crawler__hint {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin: 4px 0 12px;
}

.ai-crawler__agent-list,
.ai-crawler__path-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 12px;
}

.ai-crawler__agent-row,
.ai-crawler__path-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.ai-crawler__agent-name {
  max-width: 260px;
}

.ai-crawler__path-input {
  flex: 1;
}

.ai-crawler__agent-toggle {
  flex-shrink: 0;
}

.ai-crawler__add-button {
  margin-top: 4px;
}

.ai-crawler__mandatory-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.ai-crawler__mandatory-tag {
  font-family: var(--admin-font-mono, monospace);
}

.ai-crawler__actions {
  margin-top: 16px;
}
</style>
