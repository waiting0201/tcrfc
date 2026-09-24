<script setup lang="ts">
/**
 * C1 球隊——編輯頁。對照 apps/api/README.md「S1-7」。
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

const isCreate = route.name === 'team-new'
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
})
const baselineJson = ref('')
const heroKey = ref<string | null>(null)
const heroFile = ref<File | null>(null)
const removeHero = ref(false)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadTeam() {
  loadState.value = 'loading'
  try {
    if (!isCreate && teamId.value) {
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
      heroKey.value = detail.heroKey ?? null
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

const pageTitle = computed(() => (isCreate ? '新增球隊' : `編輯球隊：${form.nameZh || form.code}`))

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.introEn.trim()
}

function validate(): boolean {
  formError.value = null
  if (!form.code.trim()) {
    formError.value = '請輸入隊別代號'
    return false
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入中文名稱'
    return false
  }
  return true
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
      zh: { name: form.nameZh.trim(), intro: form.introZh || null },
      en: isEnEmpty() ? undefined : { name: form.nameEn || null, intro: form.introEn || null },
    },
  }
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate) {
      const created = await createAdminClubTeam(activeClubId.value, buildPayload(), heroFile.value)
      ElMessage.success('已建立')
      router.replace(`/teams/clubs/${created.id}/edit`)
      teamId.value = created.id
      heroKey.value = created.heroKey ?? null
    } else {
      const updated = await updateAdminClubTeam(
        activeClubId.value,
        teamId.value!,
        { ...buildPayload(), removeHero: removeHero.value },
        heroFile.value,
      )
      heroKey.value = updated.heroKey ?? null
      ElMessage.success('已儲存')
    }
    heroFile.value = null
    removeHero.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
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
        <el-card shadow="never" header="基本資料" class="team-edit__section">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="隊別代號" required>
                <el-input v-model="form.code" placeholder="例如 D1、BW1、U15" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="類型" required>
                <el-select v-model="form.type" style="width: 100%">
                  <el-option v-for="(label, value) in TEAM_TYPE_LABEL" :key="value" :label="label" :value="value" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="性別" required>
                <el-select v-model="form.gender" style="width: 100%">
                  <el-option v-for="(label, value) in TEAM_GENDER_LABEL" :key="value" :label="label" :value="value" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="年齡層">
                <el-input v-model="form.ageBand" placeholder="選填，例如 U15" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="代表色">
                <el-input v-model="form.teamColor" placeholder="選填，例如 #E0218A" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="排序">
                <el-input-number v-model="form.sortOrder" :min="0" style="width: 100%" />
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

        <el-card shadow="never" header="主視覺" class="team-edit__section">
          <el-form-item label="主視覺圖片">
            <ImageUploader
              v-model:file="heroFile"
              v-model:remove-cover="removeHero"
              :has-existing-image="!!heroKey"
              :disabled="saving"
            />
          </el-form-item>
        </el-card>
      </el-form>

      <div class="team-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.team-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.team-edit__form-error {
  margin-bottom: 16px;
}

.team-edit__section {
  margin-bottom: 16px;
}

.team-edit__action-bar {
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
  .team-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .team-edit__action-bar {
    justify-content: stretch;
  }

  .team-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>
