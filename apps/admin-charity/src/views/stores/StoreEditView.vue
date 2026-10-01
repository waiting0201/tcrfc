<script setup lang="ts">
/** N1 店家管理與 QR Code — 編輯頁（docs/22-charity-ui.md §3.7.1）：接真 API。 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import ErrorState from '@/components/ErrorState.vue'
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

async function handleSave() {
  if (saving.value) return
  if (!form.nameZh.trim()) {
    ElMessage.warning('請填寫店名（中文）')
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
    ElMessage.error(error instanceof AdminApiError ? error.detail : '儲存失敗，請稍後再試')
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
    <el-form v-else v-loading="loading" label-position="top" class="store-edit__form">
      <h2 class="store-edit__section-title">基本資訊</h2>
      <BilingualShortField label="店名" :zh="form.nameZh" :en="form.nameEn" required @update:zh="form.nameZh = $event" @update:en="form.nameEn = $event" />
      <el-row :gutter="16">
        <el-col :sm="8" :xs="24">
          <el-form-item label="類別" required>
            <el-input v-model="form.category" placeholder="例如：餐飲、飲料、零售" />
          </el-form-item>
        </el-col>
        <el-col :sm="8" :xs="24">
          <el-form-item label="聯絡人">
            <el-input v-model="form.contactName" />
          </el-form-item>
        </el-col>
        <el-col :sm="8" :xs="24">
          <el-form-item label="聯絡電話">
            <el-input v-model="form.contactPhone" />
          </el-form-item>
        </el-col>
      </el-row>
      <el-form-item label="地址">
        <el-input v-model="form.address" />
      </el-form-item>
      <el-row :gutter="16">
        <el-col :sm="12" :xs="24">
          <el-form-item label="合作起日">
            <el-date-picker v-model="form.startOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :sm="12" :xs="24">
          <el-form-item label="合作迄日（未填表示持續合作中）">
            <el-date-picker v-model="form.endOn" type="date" value-format="YYYY-MM-DD" style="width: 100%" clearable />
          </el-form-item>
        </el-col>
      </el-row>

      <h2 class="store-edit__section-title">店家 Logo（選填）</h2>
      <ImageUploader :existing-url="logoUrl" :min-width="200" :min-height="200" variant="logo" :saving="saving" @update:file="handleLogoUpdate" />
      <BilingualShortField label="Logo 替代文字" :zh="form.logoAltZh" :en="form.logoAltEn" @update:zh="form.logoAltZh = $event" @update:en="form.logoAltEn = $event" />

      <h2 class="store-edit__section-title">分潤設定</h2>
      <el-form-item label="店家分潤比例（%）">
        <el-input-number v-if="canSetShare" v-model="form.sharePct" :min="0" :max="100" :step="0.5" :precision="2" />
        <span v-else>{{ form.sharePct }}%（設定分潤需要額外授權，請洽系統管理員）</span>
      </el-form-item>
      <p class="store-edit__example">捐款 NT$100 時，此店家可獲得 {{ formatMoney(shareExample) }}。店家分潤與項目分潤相加不得超過 100%，儲存時會檢查。</p>

      <h2 class="store-edit__section-title">狀態</h2>
      <el-form-item label="合作狀態">
        <el-radio-group v-model="form.status">
          <el-radio value="active">合作中</el-radio>
          <el-radio value="inactive">已停止</el-radio>
        </el-radio-group>
      </el-form-item>

      <div class="store-edit__actions">
        <el-button @click="router.push('/stores')">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </el-form>
  </div>
</template>

<style scoped>
.store-edit__form {
  max-width: 720px;
}

.store-edit__section-title {
  font-size: 15px;
  color: var(--charity-admin-text-primary);
  margin: var(--charity-admin-space-6) 0 var(--charity-admin-space-3);
  padding-top: var(--charity-admin-space-3);
  border-top: 1px solid var(--charity-admin-border);
}

.store-edit__section-title:first-child {
  margin-top: 0;
  padding-top: 0;
  border-top: none;
}

.store-edit__example {
  margin: -8px 0 16px;
  font-size: 13px;
  color: var(--charity-admin-text-secondary);
}

.store-edit__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-6);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
