<script setup lang="ts">
/** N1 店家管理與 QR Code — 編輯頁（docs/22-charity-ui.md §3.7.1） */
import { computed, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import { addStore, findStoreByKey, updateStore } from '@/data/fixtures'
import { formatMoney } from '@/utils/format'

const props = defineProps<{ storeKey?: string }>()
const router = useRouter()

const existing = computed(() => (props.storeKey ? findStoreByKey(props.storeKey) : undefined))
const isEdit = computed(() => Boolean(existing.value))

const form = reactive({
  nameZh: existing.value?.nameZh ?? '',
  nameEn: existing.value?.nameEn ?? '',
  category: existing.value?.category ?? '',
  address: existing.value?.address ?? '',
  contactName: existing.value?.contactName ?? '',
  contactPhone: existing.value?.contactPhone ?? '',
  startOn: existing.value?.startOn ?? '',
  endOn: existing.value?.endOn ?? null,
  sharePct: existing.value?.sharePct ?? 5,
  status: existing.value?.status ?? 'active',
  hasLogo: existing.value?.hasLogo ?? false,
})

/** 分潤試算：捐款 NT$100 時，此店家可獲得多少（docs/22 §3.7.1 要求即時顯示試算範例） */
const shareExample = computed(() => Math.floor((100 * form.sharePct) / 100))

function handleLogoUpdate(file: File | null) {
  form.hasLogo = Boolean(file)
}

function handleSave() {
  if (!form.nameZh || !form.category) {
    ElMessage.warning('請填寫店名與類別')
    return
  }
  if (isEdit.value && existing.value) {
    updateStore(existing.value.key, { ...form })
    ElMessage.success('已儲存店家資料')
  } else {
    addStore({ ...form, endOn: form.endOn || null })
    ElMessage.success('已新增店家')
  }
  router.push('/stores')
}
</script>

<template>
  <div>
    <PageHeader :title="isEdit ? '編輯店家' : '新增店家'" frontend-unit="掃碼落地頁">
      <template #back>
        <el-button text :icon="'ArrowLeft'" @click="router.push('/stores')">返回列表</el-button>
      </template>
    </PageHeader>

    <el-form label-position="top" class="store-edit__form">
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
      <ImageUploader :existing-url="null" variant="logo" @update:file="handleLogoUpdate" />

      <h2 class="store-edit__section-title">分潤設定</h2>
      <el-form-item label="店家分潤比例（%）">
        <el-input-number v-model="form.sharePct" :min="0" :max="100" :step="0.5" />
      </el-form-item>
      <p class="store-edit__example">捐款 NT$100 時，此店家可獲得 {{ formatMoney(shareExample) }}</p>

      <h2 class="store-edit__section-title">狀態</h2>
      <el-form-item label="合作狀態">
        <el-radio-group v-model="form.status">
          <el-radio value="active">合作中</el-radio>
          <el-radio value="inactive">已停止</el-radio>
        </el-radio-group>
      </el-form-item>

      <div class="store-edit__actions">
        <el-button @click="router.push('/stores')">取消</el-button>
        <el-button type="primary" @click="handleSave">儲存</el-button>
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
