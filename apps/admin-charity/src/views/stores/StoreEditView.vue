<script setup lang="ts">
/**
 * N1 店家管理與 QR Code — 編輯頁（docs/22-charity-ui.md §3.7.1）：接真 API。
 * 版面依 docs/21 §3／§3.3a 與 docs/22 §3.10：整頁一組語言分頁、主欄一張卡、右側欄「基本設定」＋「發布設定」，
 * 驗證一次檢查全部並標到欄位。欄位鍵沿用 StoreInput 的屬性名。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import FormSection from '@/components/FormSection.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import ErrorState from '@/components/ErrorState.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import {
  createStore,
  getStore,
  removeStoreLogo,
  updateStore,
  uploadStoreLogo,
  type StoreDetail,
  type StoreInput,
} from '@/api/stores'
import { AdminApiError } from '@/api/http'
import { formatMoney } from '@/utils/format'
import { hasPermission } from '@/auth/session'

// 路由參數名稱沿用 storeKey，值現在是店家編號（API 的識別碼）。
const props = defineProps<{ storeKey?: string }>()
const router = useRouter()

const isEdit = computed(() => Boolean(props.storeKey))
const canSetShare = computed(() => hasPermission('n1.donation_store.share_pct'))

const formErrors = provideFormErrors()
/** 只放沒有欄位歸屬的錯誤（API 回來的訊息）；前端驗證一律進 formErrors。 */
const formError = ref<string | null>(null)

const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const existing = ref<StoreDetail | null>(null)

const form = reactive({
  nameZh: '',
  nameEn: '',
  logoAltZh: '',
  logoAltEn: '',
  category: '',
  address: '',
  contactName: '',
  contactPhone: '',
  startOn: '' as string | null,
  endOn: null as string | null,
  sharePct: 0,
  status: 'active' as 'active' | 'inactive',
})

const logoUrl = ref<string | null>(null)
const pendingLogo = ref<File | null>(null)
const logoRemoved = ref(false)

function fill(store: StoreDetail) {
  existing.value = store
  form.nameZh = store.nameZh ?? ''
  form.nameEn = store.nameEn ?? ''
  form.logoAltZh = store.logoAltZh ?? ''
  form.logoAltEn = store.logoAltEn ?? ''
  form.category = store.category ?? ''
  form.address = store.address ?? ''
  form.contactName = store.contactName ?? ''
  form.contactPhone = store.contactPhone ?? ''
  form.startOn = store.startOn
  form.endOn = store.endOn
  form.sharePct = store.storeSharePct
  form.status = store.status
  logoUrl.value = store.logoUrl
}

async function load() {
  if (!props.storeKey) return
  loading.value = true
  loadError.value = ''
  try {
    fill(await getStore(props.storeKey))
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.detail : '讀取店家資料時發生問題'
  } finally {
    loading.value = false
  }
}

onMounted(load)

/** 分潤試算：捐款 NT$100 時，此店家可獲得多少（docs/22 §3.7.1 要求即時顯示試算範例） */
const shareExample = computed(() => Math.floor((100 * form.sharePct) / 100))

function handleLogoUpdate(file: File | null) {
  formErrors.clear('logo')
  if (file) {
    pendingLogo.value = file
    logoRemoved.value = false
  } else {
    pendingLogo.value = null
    logoRemoved.value = true
    logoUrl.value = null
  }
}

function blank(value: string): string | null {
  const v = value.trim()
  return v === '' ? null : v
}

/** 一次檢查全部（欄位鍵 → 訊息），不要遇到第一個就停。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請填寫店名（中文）'
  if (form.startOn && form.endOn && form.endOn < form.startOn) errors.endOn = '合作迄日不能早於合作起日'
  return errors
}

async function handleSave() {
  if (saving.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  const input: StoreInput = {
    nameZh: form.nameZh.trim(),
    nameEn: blank(form.nameEn),
    logoAltZh: blank(form.logoAltZh),
    logoAltEn: blank(form.logoAltEn),
    category: blank(form.category),
    address: blank(form.address),
    contactName: blank(form.contactName),
    contactPhone: blank(form.contactPhone),
    startOn: form.startOn || null,
    endOn: form.endOn || null,
    status: form.status,
  }
  // 分潤比例需要獨立授權：沒有權限就不送（後端把「省略」視為不變），有權限才送。
  if (canSetShare.value) input.storeSharePct = form.sharePct

  saving.value = true
  try {
    const saved = isEdit.value && props.storeKey ? await updateStore(props.storeKey, input) : await createStore(input)
    // Logo 與資料分開處理：資料已存好，圖片失敗時留在原頁讓使用者重試，不假裝整筆成功。
    try {
      if (pendingLogo.value) await uploadStoreLogo(saved.id, pendingLogo.value)
      else if (logoRemoved.value && existing.value?.logoUrl) await removeStoreLogo(saved.id)
    } catch (error) {
      ElMessage.error(`店家資料已儲存，但 Logo 處理失敗：${error instanceof AdminApiError ? error.detail : '請稍後再試'}`)
      if (!isEdit.value) await router.replace(`/stores/${saved.id}/edit`)
      else fill(await getStore(saved.id))
      return
    }
    ElMessage.success(isEdit.value ? '已儲存店家資料' : '已新增店家')
    await router.push('/stores')
  } catch (error) {
    // 後端有標欄位就標到欄位；對不到（或沒有欄位資訊）才退回頁首提示
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <PageHeader :title="isEdit ? '編輯店家' : '新增店家'" frontend-unit="掃碼落地頁">
      <template #back>
        <el-button text :icon="'ArrowLeft'" @click="router.push('/stores')">返回列表</el-button>
      </template>
    </PageHeader>

    <ErrorState v-if="loadError" :text="loadError" @retry="load" />
    <el-form v-else v-loading="loading" label-position="top" @submit.prevent>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="store-edit__alert" @close="formError = null" />

      <LangTabsBar>
        <EditLayout>
          <template #main>
            <el-card shadow="never">
              <FormSection>
                <BilingualShortField field="name" label="店名" required :zh="form.nameZh" :en="form.nameEn" @update:zh="form.nameZh = $event" @update:en="form.nameEn = $event" />
                <FormField field="address" label="地址">
                  <el-input v-model="form.address" />
                </FormField>
              </FormSection>
            </el-card>
          </template>

          <template #aside>
            <el-card shadow="never" header="基本設定">
              <FormSection>
                <FormField field="category" label="類別" required>
                  <el-input v-model="form.category" placeholder="例如：餐飲、飲料、零售" />
                </FormField>
                <FormField field="contactName" label="聯絡人">
                  <el-input v-model="form.contactName" />
                </FormField>
                <FormField field="contactPhone" label="聯絡電話">
                  <el-input v-model="form.contactPhone" />
                </FormField>
                <FormField field="startOn" label="合作起日">
                  <el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
                </FormField>
                <FormField field="endOn" label="合作迄日（未填表示持續合作中）">
                  <el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" clearable />
                </FormField>
              </FormSection>

              <FormSection title="店家 Logo（選填）">
                <FormField field="logo">
                  <ImageUploader :existing-url="logoUrl" :min-width="200" :min-height="200" variant="logo" :saving="saving" @update:file="handleLogoUpdate" />
                </FormField>
                <BilingualShortField field="logoAlt" label="Logo 替代文字" :zh="form.logoAltZh" :en="form.logoAltEn" @update:zh="form.logoAltZh = $event" @update:en="form.logoAltEn = $event" />
              </FormSection>

              <FormSection title="分潤設定">
                <FormField field="storeSharePct" label="店家分潤比例（%）">
                  <el-input-number v-if="canSetShare" v-model="form.sharePct" :min="0" :max="100" :step="0.5" :precision="2" />
                  <span v-else>{{ form.sharePct }}%（設定分潤需要額外授權，請洽系統管理員）</span>
                </FormField>
                <p class="store-edit__example">捐款 NT$100 時，此店家可獲得 {{ formatMoney(shareExample) }}。店家分潤與項目分潤相加不得超過 100%，儲存時會檢查。</p>
              </FormSection>
            </el-card>

            <el-card shadow="never" header="發布設定">
              <FormSection>
                <FormField field="status" label="合作狀態">
                  <el-radio-group v-model="form.status">
                    <el-radio value="active">合作中</el-radio>
                    <el-radio value="inactive">已停止</el-radio>
                  </el-radio-group>
                </FormField>
              </FormSection>
            </el-card>
          </template>
        </EditLayout>
      </LangTabsBar>

      <EditActionBar>
        <template #status><FormErrorStatus /></template>
        <el-button @click="router.push('/stores')">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </el-form>
  </div>
</template>

<style scoped>
.store-edit__alert {
  margin-bottom: var(--charity-admin-space-3);
}

.store-edit__example {
  margin: 0;
  font-size: 13px;
  line-height: 1.6;
  color: var(--charity-admin-text-secondary);
}
</style>
