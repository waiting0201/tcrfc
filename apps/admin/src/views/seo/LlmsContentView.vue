<script setup lang="ts">
/**
 * H4 AI 摘要資料（`llms.txt` 維護，對應主站規劃書 §7 `GEO-01`／§4.8「`llms.txt` 維護」；
 * apps/api/README.md「S1-12a」）。單一設定表單，逐俱樂部各自一份，跟著站台切換器切換
 * （`activeClubId`），版面比照 `SeoSettingsView.vue`（H1）既有的單筆設定編輯頁寫法。
 *
 * 五個區塊皆可為空（後端檔頭：留白時前台 `/llms.txt`／`llms-en.txt` 有內建預設文字可回退，
 * 不會因為「還沒填」而讓輸出壞掉或消失），因此這裡不做必填檢查。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { getAdminLlmsContent, updateAdminLlmsContent, type AdminLlmsContentDto } from '@/api/adminSeo'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)

interface LlmsForm {
  positioningZh: string
  positioningEn: string
  keyPagesZh: string
  keyPagesEn: string
  factsSummaryZh: string
  factsSummaryEn: string
  licenseZh: string
  licenseEn: string
  contactZh: string
  contactEn: string
}

function emptyForm(): LlmsForm {
  return {
    positioningZh: '',
    positioningEn: '',
    keyPagesZh: '',
    keyPagesEn: '',
    factsSummaryZh: '',
    factsSummaryEn: '',
    licenseZh: '',
    licenseEn: '',
    contactZh: '',
    contactEn: '',
  }
}

const loadState = ref<'loading' | 'error' | 'ready'>('loading')
const loadErrorMessage = ref('')
const form = reactive<LlmsForm>(emptyForm())
const baselineJson = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

function applyLoaded(dto: AdminLlmsContentDto) {
  form.positioningZh = dto.positioningZh ?? ''
  form.positioningEn = dto.positioningEn ?? ''
  form.keyPagesZh = dto.keyPagesZh ?? ''
  form.keyPagesEn = dto.keyPagesEn ?? ''
  form.factsSummaryZh = dto.factsSummaryZh ?? ''
  form.factsSummaryEn = dto.factsSummaryEn ?? ''
  form.licenseZh = dto.licenseZh ?? ''
  form.licenseEn = dto.licenseEn ?? ''
  form.contactZh = dto.contactZh ?? ''
  form.contactEn = dto.contactEn ?? ''
  baselineJson.value = JSON.stringify(form)
}

async function loadContent() {
  loadState.value = 'loading'
  try {
    const dto = await getAdminLlmsContent(club.value)
    applyLoaded(dto)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadContent)
watch(club, loadContent)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

async function handleSave() {
  formError.value = null
  saving.value = true
  try {
    const saved = await updateAdminLlmsContent(club.value, {
      positioningZh: form.positioningZh || null,
      positioningEn: form.positioningEn || null,
      keyPagesZh: form.keyPagesZh || null,
      keyPagesEn: form.keyPagesEn || null,
      factsSummaryZh: form.factsSummaryZh || null,
      factsSummaryEn: form.factsSummaryEn || null,
      licenseZh: form.licenseZh || null,
      licenseEn: form.licenseEn || null,
      contactZh: form.contactZh || null,
      contactEn: form.contactEn || null,
    })
    applyLoaded(saved)
    ElMessage.success('已儲存，前台立即更新')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="llms-content">
    <PageHeader title="AI 摘要資料">
      <template #meta>
        <FrontendUnitBanner module-code="H" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="10" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadContent">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert type="info" :closable="false" show-icon class="llms-content__notice">
        這裡填寫的內容是給 AI 系統（例如 ChatGPT、Claude 這類服務）快速認識本站用的摘要資料，跟訪客在網站上實際看到的頁面是分開的兩件事。<strong>存檔後立即生效</strong>，不需要另外發布或部署。五個區塊都可以先留白，留白時系統會用內建的預設內容，之後有空再回來慢慢補齊即可。
      </el-alert>

      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="llms-content__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="站點定位" class="llms-content__section">
        <p class="llms-content__hint">一到兩句話說明這個網站是誰、做什麼，例如俱樂部的定位與主要服務對象。</p>
        <el-form label-position="top">
          <BilingualTextareaField
            label="站點定位"
            :zh="form.positioningZh"
            :en="form.positioningEn"
            :rows="3"
            placeholder="例如：台中磐石足球俱樂部官方網站，提供球隊資訊、賽程與賽果、球員招募與周邊商品。"
            @update:zh="(v) => (form.positioningZh = v)"
            @update:en="(v) => (form.positioningEn = v)"
          />
        </el-form>
      </el-card>

      <el-card shadow="never" header="代表頁清單" class="llms-content__section">
        <p class="llms-content__hint">
          列出最能代表本站的重要頁面，建議一行一個連結（例如「- [關於我們](/zh/about/)」），系統會原樣輸出，不會另外解析或檢查連結是否存在。留白時系統會自動列出目前啟用單元的清單。
        </p>
        <el-form label-position="top">
          <BilingualTextareaField
            label="代表頁清單"
            :zh="form.keyPagesZh"
            :en="form.keyPagesEn"
            :rows="6"
            :placeholder="'- [關於我們](/zh/about/)\n- [賽程與賽果](/zh/teams/matches/)\n- [加入我們](/zh/join/)'"
            @update:zh="(v) => (form.keyPagesZh = v)"
            @update:en="(v) => (form.keyPagesEn = v)"
          />
        </el-form>
      </el-card>

      <el-card shadow="never" header="事實摘要" class="llms-content__section">
        <p class="llms-content__hint">
          成立年份、主場、參與聯賽等重要事實的濃縮摘要，供 AI 系統摘要引用。⚠️ 這裡只是給 AI 看的摘要，不是這些事實的正式維護處——請確保跟網站上明文寫的內容一致，不要出現矛盾的說法。
        </p>
        <el-form label-position="top">
          <BilingualTextareaField
            label="事實摘要"
            :zh="form.factsSummaryZh"
            :en="form.factsSummaryEn"
            :rows="4"
            placeholder="例如：台中磐石足球俱樂部（Taichung Rock FC）成立於 2024 年，主場為……，現征戰企業甲級足球聯賽。"
            @update:zh="(v) => (form.factsSummaryZh = v)"
            @update:en="(v) => (form.factsSummaryEn = v)"
          />
        </el-form>
      </el-card>

      <el-card shadow="never" header="授權與引用方式" class="llms-content__section">
        <p class="llms-content__hint">說明 AI 系統可以怎麼引用本站內容，例如是否需要標示來源、是否允許摘要轉述等。</p>
        <el-form label-position="top">
          <BilingualTextareaField
            label="授權與引用方式"
            :zh="form.licenseZh"
            :en="form.licenseEn"
            :rows="3"
            placeholder="例如：歡迎引用本站公開內容並標示來源與連結，請勿逐字大量複製。"
            @update:zh="(v) => (form.licenseZh = v)"
            @update:en="(v) => (form.licenseEn = v)"
          />
        </el-form>
      </el-card>

      <el-card shadow="never" header="聯絡窗口" class="llms-content__section">
        <p class="llms-content__hint">AI 系統或其他單位若想進一步確認資訊來源，可以聯絡的窗口資訊。</p>
        <el-form label-position="top">
          <BilingualTextareaField
            label="聯絡窗口"
            :zh="form.contactZh"
            :en="form.contactEn"
            :rows="2"
            placeholder="例如：媒體與合作聯繫請洽 info@taichungrock.example"
            @update:zh="(v) => (form.contactZh = v)"
            @update:en="(v) => (form.contactEn = v)"
          />
        </el-form>
      </el-card>

      <div class="llms-content__actions">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.llms-content {
  max-width: 780px;
  margin: 0 auto;
}

.llms-content__notice {
  margin-bottom: 12px;
}

.llms-content__section {
  margin-bottom: 12px;
}

.llms-content__form-error {
  margin-bottom: 12px;
}

.llms-content__hint {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin: 4px 0 12px;
}

.llms-content__actions {
  margin-top: 16px;
}
</style>
