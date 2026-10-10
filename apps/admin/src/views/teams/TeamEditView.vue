<script setup lang="ts">
/**
 * C1 球隊——編輯頁。對照 apps/api/README.md「S1-7」。
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
import ImageAltField from '@/components/ImageAltField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import {
  createAdminClubTeam,
  getAdminClubTeam,
  updateAdminClubTeam,
  type SaveTeamPayload,
} from '@/api/adminTeams'
import { AdminApiError } from '@/api/http'
import { TEAM_GENDER_LABEL, TEAM_TYPE_LABEL, type TeamGender, type TeamType } from '@/types/team'

const route = useRoute()
const router = useRouter()

// 🔴 必須是 computed 不能是一次性求值的 const：建立成功後 handleSave() 呼叫
// `router.replace('/teams/clubs/:id/edit')`，Vue Router 對同一個元件實例的路由切換預設不會
// 重新掛載（component reuse），若 isCreate 只在 setup 當下算一次，之後緊接著再按一次「儲存」
// 會誤判成仍在建立模式，重複呼叫 createAdminClubTeam 產生第二筆重複資料。比照
// `CompetitionEditView.vue`／`MatchEditView.vue` 既有寫法（`docs/18` 回報項）。
const isCreate = computed(() => route.name === 'team-new')
const teamId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  code: '',
  type: 'academy' as TeamType,
  gender: 'men' as TeamGender,
  ageBand: '',
  teamColor: '',
  sortOrder: 0,
  nameZh: '',
  nameEn: '',
  introZh: '',
  introEn: '',
  heroAltZh: '',
  heroAltEn: '',
})
const baselineJson = ref('')
const heroKey = ref<string | null>(null)
/** 既有圖片的預覽網址（後端附帶的 heroThumbUrl／heroUrl，沒有就是 null）。 */
const heroPreviewUrl = ref<string | null>(null)
const heroWidth = ref<number | null>(null)
const heroHeight = ref<number | null>(null)
const heroFile = ref<File | null>(null)
const removeHero = ref(false)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除主視覺就清掉該欄位的錯誤
watch([heroFile, removeHero], () => formErrors.clear('hero'))

async function loadTeam() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && teamId.value) {
      const detail = await getAdminClubTeam(activeClubId.value, teamId.value)
      form.code = detail.code
      form.type = detail.type as TeamType
      form.gender = detail.gender as TeamGender
      form.ageBand = detail.ageBand ?? ''
      form.teamColor = detail.teamColor ?? ''
      form.sortOrder = detail.sortOrder
      form.nameZh = detail.zh.name ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.introZh = detail.zh.intro ?? ''
      form.introEn = detail.en?.intro ?? ''
      form.heroAltZh = detail.zh.heroAlt ?? ''
      form.heroAltEn = detail.en?.heroAlt ?? ''
      heroWidth.value = detail.heroWidth ?? null
      heroHeight.value = detail.heroHeight ?? null
      heroKey.value = detail.heroKey ?? null
      heroPreviewUrl.value = detail.heroThumbUrl ?? detail.heroUrl ?? null
    }
    heroFile.value = null
    removeHero.value = false
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

onMounted(loadTeam)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || heroFile.value !== null || removeHero.value),
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增球隊' : `編輯球隊：${form.nameZh || form.code}`))

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.introEn.trim() && !form.heroAltEn.trim()
}

/** 一次檢查全部必填，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.code.trim()) errors.code = '請輸入隊別代號'
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文名稱'
  return errors
}

function buildPayload(): SaveTeamPayload {
  return {
    code: form.code.trim(),
    type: form.type,
    gender: form.gender,
    ageBand: form.ageBand || null,
    teamColor: form.teamColor || null,
    sortOrder: form.sortOrder,
    content: {
      zh: { name: form.nameZh.trim(), intro: form.introZh || null, heroAlt: form.heroAltZh.trim() || null },
      en: isEnEmpty()
        ? undefined
        : { name: form.nameEn || null, intro: form.introEn || null, heroAlt: form.heroAltEn.trim() || null },
    },
  }
}

async function handleSave() {
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    if (isCreate.value) {
      const created = await createAdminClubTeam(activeClubId.value, buildPayload(), heroFile.value)
      ElMessage.success('已建立')
      router.replace(`/teams/clubs/${created.id}/edit`)
      teamId.value = created.id
      heroKey.value = created.heroKey ?? null
      heroPreviewUrl.value = created.heroThumbUrl ?? created.heroUrl ?? null
      heroWidth.value = created.heroWidth ?? null
      heroHeight.value = created.heroHeight ?? null
    } else {
      const updated = await updateAdminClubTeam(
        activeClubId.value,
        teamId.value!,
        { ...buildPayload(), removeHero: removeHero.value },
        heroFile.value,
      )
      heroKey.value = updated.heroKey ?? null
      heroPreviewUrl.value = updated.heroThumbUrl ?? updated.heroUrl ?? null
      heroWidth.value = updated.heroWidth ?? null
      heroHeight.value = updated.heroHeight ?? null
      ElMessage.success('已儲存')
    }
    heroFile.value = null
    removeHero.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/teams/clubs')
}

function retryLoad() {
  loadTeam()
}
</script>

<template>
  <div class="team-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="C1" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這支球隊，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
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
        class="team-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top">
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
                </FormSection>
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="code" label="隊別代號" required>
                    <el-input v-model="form.code" placeholder="例如 D1、BW1、U15" />
                  </FormField>
                  <FormField field="type" label="類型" required>
                    <el-select v-model="form.type" style="width: 100%" @change="formErrors.clear('type')">
                      <el-option v-for="(label, value) in TEAM_TYPE_LABEL" :key="value" :label="label" :value="value" />
                    </el-select>
                  </FormField>
                  <FormField field="gender" label="性別" required>
                    <el-select v-model="form.gender" style="width: 100%" @change="formErrors.clear('gender')">
                      <el-option v-for="(label, value) in TEAM_GENDER_LABEL" :key="value" :label="label" :value="value" />
                    </el-select>
                  </FormField>
                  <el-form-item label="年齡層">
                    <el-input v-model="form.ageBand" placeholder="選填，例如 U15" />
                  </el-form-item>
                  <el-form-item label="代表色">
                    <el-input v-model="form.teamColor" placeholder="選填，例如 #E0218A" />
                  </el-form-item>
                </FormSection>
                <FormSection title="主視覺">
                  <FormField field="hero" label="主視覺圖片">
                    <ImageUploader
                      v-model:file="heroFile"
                      v-model:remove-cover="removeHero"
                      :has-existing-image="!!heroKey"
                      :existing-preview-url="heroPreviewUrl"
                      :disabled="saving"
                    />
                  </FormField>
                  <ImageAltField
                    v-model:zh="form.heroAltZh"
                    v-model:en="form.heroAltEn"
                    field="heroAlt"
                    :has-image="(!!heroKey && !removeHero) || heroFile !== null"
                    :width="heroWidth"
                    :height="heroHeight"
                    fallback="球隊名稱"
                  />
                </FormSection>
              </el-card>
              <el-card shadow="never" header="發布設定">
                <FormSection>
                  <el-form-item label="排序">
                    <el-input-number v-model="form.sortOrder" :min="0" style="width: 100%" />
                  </el-form-item>
                </FormSection>
              </el-card>
            </template>
          </EditLayout>
        </LangTabsBar>
      </el-form>

      <EditActionBar>
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.team-edit {
  max-width: 1200px;
  margin: 0 auto;
}

.team-edit__form-error {
  margin-bottom: 16px;
}
</style>
