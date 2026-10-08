<script setup lang="ts">
/**
 * C2 球員——編輯頁。對照 apps/api/README.md「S1-7」「S1-7a」（肖像同意欄位）。
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
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { listAdminClubTeams, type AdminTeamAdminListItemDto } from '@/api/adminTeams'
import { useWritableTeamScope } from '@/composables/useWritableTeamScope'
import PlayerSeasonStatsPanel from './parts/PlayerSeasonStatsPanel.vue'
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
import { dateOnlyToPickerDate as fromDateOnlyString, pickerDateToDateOnly as toDateOnlyString } from '@/utils/dateTime'

const route = useRoute()
const router = useRouter()

// 🔴 必須是 computed 不能是一次性求值的 const：建立成功後 handleSave() 呼叫
// `router.replace('/teams/players/:id/edit')`，Vue Router 對同一個元件實例的路由切換預設不會
// 重新掛載（component reuse），若 isCreate 只在 setup 當下算一次，之後緊接著再按一次「儲存」
// 會誤判成仍在建立模式，重複呼叫 createAdminPlayer 產生第二筆重複資料。比照
// `CompetitionEditView.vue`／`MatchEditView.vue` 既有寫法（`docs/18` 回報項）。
const isCreate = computed(() => route.name === 'player-new')
const playerId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  teamId: '',
  slug: '',
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
// 載入時的網址代稱：改了它會讓舊的球員頁連結失效，要在欄位旁提醒。
const originalSlug = ref('')
const photoKey = ref<string | null>(null)
/** 既有圖片的預覽網址（後端附帶的 photoThumbUrl／photoUrl，沒有就是 null）。 */
const photoPreviewUrl = ref<string | null>(null)
const photoFile = ref<File | null>(null)
const removePhoto = ref(false)

const teams = ref<AdminTeamAdminListItemDto[]>([])
// 「所屬球隊」只列出這個帳號能寫的球隊（S1-8 續作新增的端點），見 useWritableTeamScope 檔頭說明。
const { writableTeams, loadWritableTeams, outOfScopeIds, buildOptions } = useWritableTeamScope('player')
const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有欄位歸屬的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
// 選檔或移除照片就清掉該欄位的錯誤
watch([photoFile, removePhoto], () => formErrors.clear('photo'))

async function loadPlayer() {
  loadState.value = 'loading'
  try {
    ;[teams.value] = await Promise.all([listAdminClubTeams(activeClubId.value), loadWritableTeams(activeClubId.value)])
    if (!isCreate.value && playerId.value) {
      const detail = await getAdminPlayer(activeClubId.value, playerId.value)
      form.teamId = detail.teamId
      form.slug = detail.slug ?? ''
      originalSlug.value = detail.slug ?? ''
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
      photoPreviewUrl.value = detail.photoThumbUrl ?? detail.photoUrl ?? null
    } else if (writableTeams.value.length > 0) {
      form.teamId = writableTeams.value[0].id
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

const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/
const slugChanged = computed(() => !isCreate.value && !!originalSlug.value && !!form.slug.trim() && form.slug.trim() !== originalSlug.value)

const pageTitle = computed(() => (isCreate.value ? '新增球員' : `編輯球員：${form.nameZh || '（未命名）'}`))

// 🔴 既有球員的所屬球隊不在這個帳號的可寫清單內時（例如學院管理者打開一線隊球員），後端
// `AdminPlayersRepository.UpdateAsync` 一律先檢查「既有」`player.TeamId` 是否在授權範圍內，
// 範圍外時整筆更新（不只換球隊，改其他欄位也一樣）都會被 403 擋下——因此這裡整頁鎖成唯讀，
// 不是只鎖「所屬球隊」這一個欄位，避免使用者填完整份表單才在存檔瞬間才發現被拒絕。
const isTeamOutOfScope = computed(() => !isCreate.value && outOfScopeIds([form.teamId]).length > 0)
const isReadOnly = computed(() => loadState.value === 'ready' && isTeamOutOfScope.value)
const teamOptions = computed(() => buildOptions(teams.value, [form.teamId]))
const lockedTeamLabel = computed(() => teamOptions.value.find((o) => o.id === form.teamId)?.label ?? form.teamId)

function isEnEmpty(): boolean {
  return !form.nameEn.trim() && !form.bioEn.trim()
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.teamId) errors.teamId = '請選擇所屬球隊'
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文姓名'
  const slug = form.slug.trim()
  if (slug && !SLUG_PATTERN.test(slug)) {
    errors.slug = '網址代稱只能用英文小寫、數字與連字號（例如 lin-zhi-ming），且不能以連字號開頭或結尾'
  }
  if (form.shirtNo != null && (form.shirtNo < 1 || form.shirtNo > 99)) {
    errors.shirtNo = '背號只能是 1 到 99 之間的整數'
  }
  return errors
}

function buildPayload(): SavePlayerPayload {
  return {
    teamId: form.teamId,
    // 留空＝建立時自動產生、更新時維持原值（見 SavePlayerPayload.slug）。
    slug: form.slug.trim() || undefined,
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
  if (isReadOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    if (isCreate.value) {
      const created = await createAdminPlayer(activeClubId.value, buildPayload(), photoFile.value)
      ElMessage.success('已建立')
      router.replace(`/teams/players/${created.id}/edit`)
      playerId.value = created.id
      photoKey.value = created.photoKey ?? null
      photoPreviewUrl.value = created.photoThumbUrl ?? created.photoUrl ?? null
      form.slug = created.slug ?? ''
      originalSlug.value = created.slug ?? ''
    } else {
      const updated = await updateAdminPlayer(
        activeClubId.value,
        playerId.value!,
        { ...buildPayload(), removePhoto: removePhoto.value },
        photoFile.value,
      )
      photoKey.value = updated.photoKey ?? null
      photoPreviewUrl.value = updated.photoThumbUrl ?? updated.photoUrl ?? null
      form.slug = updated.slug ?? form.slug
      originalSlug.value = updated.slug ?? originalSlug.value
      ElMessage.success('已儲存')
    }
    photoFile.value = null
    removePhoto.value = false
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
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
        <span v-if="loadState === 'ready' && isTeamOutOfScope" class="player-edit__locked-note">
          <el-tag type="info" size="small">唯讀</el-tag>
          你的角色不能編輯「{{ lockedTeamLabel }}」（例如學院管理者只能編輯學院梯隊），這筆球員資料僅能檢視，如需修改請聯繫系統管理員
        </span>
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

      <el-form label-position="top" :disabled="isReadOnly">
        <LangTabsBar>
          <EditLayout>
            <template #main>
              <el-card shadow="never">
                <FormSection>
                  <BilingualShortField
                    field="name"
                    label="姓名"
                    :zh="form.nameZh"
                    :en="form.nameEn"
                    required
                    @update:zh="(v) => (form.nameZh = v)"
                    @update:en="(v) => (form.nameEn = v)"
                  />
                  <BilingualTextareaField
                    field="bio"
                    label="簡介"
                    :zh="form.bioZh"
                    :en="form.bioEn"
                    @update:zh="(v) => (form.bioZh = v)"
                    @update:en="(v) => (form.bioEn = v)"
                  />
                </FormSection>
                <PlayerSeasonStatsPanel
                  v-if="!isCreate && playerId"
                  :club="activeClubId"
                  :player-id="playerId"
                  :readonly="isReadOnly"
                />
              </el-card>
            </template>

            <template #aside>
              <el-card shadow="never" header="基本設定">
                <FormSection>
                  <FormField field="teamId" label="所屬球隊" required>
                    <el-select
                      v-model="form.teamId"
                      filterable
                      style="width: 100%"
                      no-data-text="你的帳號目前沒有任何可以寫入的球隊，請聯繫系統管理員確認你的角色資料範圍"
                      @change="formErrors.clear('teamId')"
                    >
                      <el-option v-for="t in teamOptions" :key="t.id" :label="t.label" :value="t.id" :disabled="t.disabled" />
                    </el-select>
                    <p v-if="isTeamOutOfScope" class="player-edit__hint player-edit__hint--warning">
                      你的帳號沒有這支球隊的異動權限，所屬球隊無法變更。
                    </p>
                  </FormField>
                  <FormField field="shirtNo" label="背號">
                    <el-input-number v-model="form.shirtNo" :min="1" :max="99" style="width: 100%" @change="formErrors.clear('shirtNo')" />
                  </FormField>
                  <el-form-item label="位置">
                    <el-input v-model="form.position" placeholder="例如 門將、中場" />
                  </el-form-item>

                  <FormField field="slug" label="網址代稱">
                    <el-input
                      v-model="form.slug"
                      maxlength="160"
                      :placeholder="isCreate ? '留空會依英文姓名自動產生' : '例如 lin-zhi-ming'"
                      clearable
                    />
                    <p class="player-edit__hint">
                      出現在球員頁網址，建議用英文小寫與連字號；同一個俱樂部內不能重複。{{ isCreate ? '留空會自動產生。' : '留空則維持目前的代稱。' }}
                    </p>
                    <p v-if="slugChanged" class="player-edit__hint player-edit__hint--warning">
                      修改網址代稱後，舊的球員頁連結（包含已分享出去的連結與 App 內的連結）會失效，請確認後再儲存。
                    </p>
                  </FormField>
                </FormSection>
                <FormSection title="生涯資料">
                  <el-form-item label="生日">
                    <el-date-picker v-model="form.birthOn" type="date" style="width: 100%" />
                  </el-form-item>
                  <FormField field="heightCm" label="身高（公分）">
                    <el-input-number v-model="form.heightCm" :min="100" :max="250" style="width: 100%" @change="formErrors.clear('heightCm')" />
                  </FormField>
                  <FormField field="weightKg" label="體重（公斤）">
                    <el-input-number v-model="form.weightKg" :min="30" :max="150" style="width: 100%" @change="formErrors.clear('weightKg')" />
                  </FormField>
                  <el-form-item label="國籍">
                    <el-input v-model="form.nationality" placeholder="例如 中華台北" />
                  </el-form-item>
                  <el-form-item label="慣用腳">
                    <el-select v-model="form.preferredFoot" clearable filterable allow-create placeholder="左腳／右腳／雙腳" style="width: 100%">
                      <el-option label="左腳" value="左腳" />
                      <el-option label="右腳" value="右腳" />
                      <el-option label="雙腳" value="雙腳" />
                    </el-select>
                  </el-form-item>
                  <el-form-item label="加入日期">
                    <el-date-picker v-model="form.joinedOn" type="date" style="width: 100%" />
                  </el-form-item>
                  <FormField field="status" label="狀態">
                    <el-radio-group v-model="form.status">
                      <el-radio v-for="s in PLAYER_STATUS_ORDER" :key="s" :value="s">{{ PLAYER_STATUS_LABEL[s] }}</el-radio>
                    </el-radio-group>
                  </FormField>
                </FormSection>
                <FormSection title="肖像同意">
                  <FormField field="portraitConsentStatus" label="肖像同意">
                    <el-radio-group v-model="form.portraitConsentStatus">
                      <el-radio v-for="s in PORTRAIT_CONSENT_STATUS_ORDER" :key="s" :value="s">
                        {{ PORTRAIT_CONSENT_STATUS_LABEL[s] }}
                      </el-radio>
                    </el-radio-group>
                    <p class="player-edit__hint">未同意時前台不顯示照片（會改用預設圖或純文字卡呈現）；未成年球員須由監護人代簽同意。</p>
                  </FormField>
                </FormSection>
                <FormSection title="照片">
                  <FormField field="photo" label="照片">
                    <ImageUploader
                      v-model:file="photoFile"
                      v-model:remove-cover="removePhoto"
                      :has-existing-image="!!photoKey"
                      :existing-preview-url="photoPreviewUrl"
                      :disabled="saving || isReadOnly"
                    />
                  </FormField>
                </FormSection>
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
.player-edit {
  max-width: 1200px;
  margin: 0 auto 88px;
}

.player-edit__locked-note {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--admin-text-secondary);
  flex-wrap: wrap;
}

.player-edit__form-error {
  margin-bottom: 16px;
}

.player-edit__hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  line-height: 1.6;
}

.player-edit__hint--warning {
  color: var(--admin-warning-text);
}
</style>
