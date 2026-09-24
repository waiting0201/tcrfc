<script setup lang="ts">
/**
 * C2 球員——編輯頁。對照 apps/api/README.md「S1-7」「S1-7a」（肖像同意欄位）。
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
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { createAdminPlayer, getAdminPlayer, updateAdminPlayer, type SavePlayerPayload } from '@/api/adminPlayers'
import { AdminApiError } from '@/api/http'
import {
  PLAYER_STATUS_LABEL,
  PLAYER_STATUS_ORDER,
  PORTRAIT_CONSENT_STATUS_LABEL,
  PORTRAIT_CONSENT_STATUS_ORDER,
  type PlayerStatus,
  type PortraitConsentStatus,
} from '@/types/team'

const route = useRoute()
const router = useRouter()

const isCreate = route.name === 'player-new'
const playerId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  teamId: '',
  shirtNo: null as number | null,
  position: '',
  birthOn: null as Date | null,
  heightCm: null as number | null,
  weightKg: null as number | null,
  nationality: '',
  preferredFoot: '',
  joinedOn: null as Date | null,
  status: 'active' as PlayerStatus,
  portraitConsentStatus: 'not_consented' as PortraitConsentStatus,
  nameZh: '',
  nameEn: '',
  bioZh: '',
  bioEn: '',
})
const baselineJson = ref('')
const photoKey = ref<string | null>(null)
const photoFile = ref<File | null>(null)
const removePhoto = ref(false)

const teams = ref<AdminTeamAdminListItemDto[]>([])
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

function toDateOnlyString(date: Date | null): string | null {
  if (!date) return null
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function fromDateOnlyString(value: string | null | undefined): Date | null {
  if (!value) return null
  const parsed = new Date(`${value}T00:00:00`)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

async function loadPlayer() {
  loadState.value = 'loading'
  try {
    teams.value = await listAdminClubTeams(activeClubId.value)
    if (!isCreate && playerId.value) {
      const detail = await getAdminPlayer(activeClubId.value, playerId.value)
      form.teamId = detail.teamId
      form.shirtNo = detail.shirtNo ?? null
      form.position = detail.position ?? ''
      form.birthOn = fromDateOnlyString(detail.birthOn)
      form.heightCm = detail.heightCm ?? null
      form.weightKg = detail.weightKg ?? null
      form.nationality = detail.nationality ?? ''
      form.preferredFoot = detail.preferredFoot ?? ''
      form.joinedOn = fromDateOnlyString(detail.joinedOn)
      form.status = (detail.status as PlayerStatus) ?? 'active'
      form.portraitConsentStatus = detail.portraitConsentStatus as PortraitConsentStatus
      form.nameZh = detail.zh.name ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.bioZh = detail.zh.bio ?? ''
      form.bioEn = detail.en?.bio ?? ''
      photoKey.value = detail.photoKey ?? null
    } else if (teams.value.length > 0) {
      form.teamId = teams.value[0].id
    }
    photoFile.value = null
    removePhoto.value = false
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

onMounted(loadPlayer)

const isDirty = computed(
  () => loadState.value === 'ready' && (JSON.stringify(form) !== baselineJson.value || photoFile.value !== null || removePhoto.value),
)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate ? '新增球員' : `編輯球員：${form.nameZh || '（未命名）'}`))

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.bioEn.trim()
}

function validate(): boolean {
  formError.value = null
  if (!form.teamId) {
    formError.value = '請選擇所屬球隊'
    return false
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入中文姓名'
    return false
  }
  if (form.shirtNo != null && (form.shirtNo < 1 || form.shirtNo > 99)) {
    formError.value = '背號只能是 1 到 99 之間的整數'
    return false
  }
  return true
}

function buildPayload(): SavePlayerPayload {
  return {
    teamId: form.teamId,
    shirtNo: form.shirtNo,
    position: form.position || null,
    birthOn: toDateOnlyString(form.birthOn),
    heightCm: form.heightCm,
    weightKg: form.weightKg,
    nationality: form.nationality || null,
    preferredFoot: form.preferredFoot || null,
    joinedOn: toDateOnlyString(form.joinedOn),
    status: form.status,
    // 🔴 一律明確帶出，見 src/api/adminPlayers.ts 的檔頭說明——省略會被後端回退成 not_consented。
    portraitConsentStatus: form.portraitConsentStatus,
    content: {
      zh: { name: form.nameZh.trim(), bio: form.bioZh || null },
      en: isEnEmpty() ? undefined : { name: form.nameEn || null, bio: form.bioEn || null },
    },
  }
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  formError.value = null
  try {
    if (isCreate) {
      const created = await createAdminPlayer(activeClubId.value, buildPayload(), photoFile.value)
      ElMessage.success('已建立')
      router.replace(`/teams/players/${created.id}/edit`)
      playerId.value = created.id
      photoKey.value = created.photoKey ?? null
    } else {
      const updated = await updateAdminPlayer(
        activeClubId.value,
        playerId.value!,
        { ...buildPayload(), removePhoto: removePhoto.value },
        photoFile.value,
      )
      photoKey.value = updated.photoKey ?? null
      ElMessage.success('已儲存')
    }
    photoFile.value = null
    removePhoto.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/teams/players')
}

function retryLoad() {
  loadPlayer()
}
</script>

<template>
  <div class="player-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="C2" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這位球員，可能已經被刪除，或不屬於目前選擇的俱樂部。</p>
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
        class="player-edit__form-error"
        @close="formError = null"
      />

      <el-form label-position="top">
        <el-card shadow="never" header="基本資料" class="player-edit__section">
          <el-row :gutter="12">
            <el-col :span="12">
              <el-form-item label="所屬球隊" required>
                <el-select v-model="form.teamId" filterable style="width: 100%" no-data-text="這個俱樂部目前還沒有任何球隊，請先到「球隊」建立一支">
                  <el-option v-for="t in teams" :key="t.id" :label="t.nameZh || t.code" :value="t.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="6">
              <el-form-item label="背號">
                <el-input-number v-model="form.shirtNo" :min="1" :max="99" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="6">
              <el-form-item label="位置">
                <el-input v-model="form.position" placeholder="例如 門將、中場" />
              </el-form-item>
            </el-col>
          </el-row>

          <BilingualShortField
            label="姓名"
            :zh="form.nameZh"
            :en="form.nameEn"
            required
            @update:zh="(v) => (form.nameZh = v)"
            @update:en="(v) => (form.nameEn = v)"
          />
          <BilingualTextareaField
            label="簡介"
            :zh="form.bioZh"
            :en="form.bioEn"
            @update:zh="(v) => (form.bioZh = v)"
            @update:en="(v) => (form.bioEn = v)"
          />
        </el-card>

        <el-card shadow="never" header="生涯資料" class="player-edit__section">
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="生日">
                <el-date-picker v-model="form.birthOn" type="date" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="身高（公分）">
                <el-input-number v-model="form.heightCm" :min="100" :max="250" style="width: 100%" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="體重（公斤）">
                <el-input-number v-model="form.weightKg" :min="30" :max="150" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="國籍">
                <el-input v-model="form.nationality" placeholder="例如 中華台北" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="慣用腳">
                <el-select v-model="form.preferredFoot" clearable filterable allow-create placeholder="左腳／右腳／雙腳" style="width: 100%">
                  <el-option label="左腳" value="左腳" />
                  <el-option label="右腳" value="右腳" />
                  <el-option label="雙腳" value="雙腳" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="加入日期">
                <el-date-picker v-model="form.joinedOn" type="date" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="狀態">
            <el-radio-group v-model="form.status">
              <el-radio v-for="s in PLAYER_STATUS_ORDER" :key="s" :value="s">{{ PLAYER_STATUS_LABEL[s] }}</el-radio>
            </el-radio-group>
          </el-form-item>
        </el-card>

        <el-card shadow="never" header="照片與肖像同意" class="player-edit__section">
          <el-form-item label="照片">
            <ImageUploader
              v-model:file="photoFile"
              v-model:remove-cover="removePhoto"
              :has-existing-image="!!photoKey"
              :disabled="saving"
            />
          </el-form-item>
          <el-form-item label="肖像同意">
            <el-radio-group v-model="form.portraitConsentStatus">
              <el-radio v-for="s in PORTRAIT_CONSENT_STATUS_ORDER" :key="s" :value="s">
                {{ PORTRAIT_CONSENT_STATUS_LABEL[s] }}
              </el-radio>
            </el-radio-group>
            <p class="player-edit__hint">未同意時前台不顯示照片（會改用預設圖或純文字卡呈現）；未成年球員須由監護人代簽同意。</p>
          </el-form-item>
        </el-card>
      </el-form>

      <div class="player-edit__action-bar">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.player-edit {
  max-width: 780px;
  margin: 0 auto 88px;
}

.player-edit__form-error {
  margin-bottom: 16px;
}

.player-edit__section {
  margin-bottom: 16px;
}

.player-edit__hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.player-edit__action-bar {
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
  .player-edit__action-bar {
    left: 0;
  }
}

@media (max-width: 767px) {
  .player-edit__action-bar {
    justify-content: stretch;
  }

  .player-edit__action-bar :deep(.el-button) {
    flex: 1;
  }
}
</style>
