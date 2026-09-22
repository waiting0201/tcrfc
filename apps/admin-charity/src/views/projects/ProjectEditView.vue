<script setup lang="ts">
/** N2 捐款項目管理 — 編輯頁（docs/22-charity-ui.md §3.7.2） */
import { computed, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import {
  STORES,
  addProject,
  findProjectByKey,
  updateProject,
  type Project,
} from '@/data/fixtures'
import raw from '@fixtures/charity-fixtures.json'
import type { CharityFixturesFile } from '@/types/fixtures'

const fixtures = raw as unknown as CharityFixturesFile

const props = defineProps<{ projectKey?: string }>()
const router = useRouter()

const existing = computed(() => (props.projectKey ? findProjectByKey(props.projectKey) : undefined))
const isEdit = computed(() => Boolean(existing.value))

const form = reactive({
  nameZh: existing.value?.nameZh ?? '',
  nameEn: existing.value?.nameEn ?? '',
  oneLinerZh: existing.value?.oneLinerZh ?? '',
  oneLinerEn: existing.value?.oneLinerEn ?? '',
  fundUsageZh: existing.value?.fundUsageZh ?? '',
  fundUsageEn: existing.value?.fundUsageEn ?? '',
  minAmount: existing.value?.minAmount ?? 100,
  maxAmount: existing.value?.maxAmount ?? 100000,
  amountOptionsText: (existing.value?.amountOptions ?? [300, 500, 1000, 3000]).join('、'),
  sharePct: existing.value?.sharePct ?? 10,
  charityRefCode: existing.value?.charityRefCode ?? fixtures.charityRefs[0]?.[0] ?? '',
  programRefCode: existing.value?.programRefCode ?? '',
  invoiceMode: existing.value?.invoiceMode ?? 'b2c_invoice',
  status: existing.value?.status ?? 'draft',
  sortOrder: existing.value?.sortOrder ?? 0,
})

const programOptions = computed(() =>
  fixtures.charityProgramRefs.filter(([, charityRefCode]) => charityRefCode === form.charityRefCode),
)

/**
 * 分潤約束檢查（docs/22 §3.7.2）：規劃書的約束是「店家分潤＋項目分潤 ≤ 100%」，但捐款項目
 * 本身不綁定單一店家（同一項目可能被多家店家的 QR 導流）。這裡的 mockup 簡化做法是拿「目前
 * 所有店家裡分潤比例最高的那一家」試算最壞情況，並在提示文字裡明講試算依據——這是本次任務
 * 為了在沒有店家選擇欄位的情況下仍能示範這條驗證規則而做的簡化，不是規劃書明文的計算方式，
 * 正式規格如何界定「哪個店家」需要與這條約束一起在 §6 待決清單追蹤。
 */
const maxStoreSharePct = computed(() => Math.max(0, ...STORES.map((s) => s.sharePct)))
const combinedPct = computed(() => form.sharePct + maxStoreSharePct.value)
const overLimit = computed(() => combinedPct.value > 100)

function handleCoverUpdate() {
  // mockup：只做預覽，不上傳
}

function handleSave() {
  if (!form.nameZh) {
    ElMessage.warning('請填寫項目名稱')
    return
  }
  if (overLimit.value) {
    ElMessage.error(`店家分潤與項目分潤相加不得超過 100%，目前合計 ${combinedPct.value}%`)
    return
  }
  const amountOptions = form.amountOptionsText
    .split(/[、,]/)
    .map((s) => Number(s.trim()))
    .filter((n) => !Number.isNaN(n) && n > 0)

  const payload: Omit<Project, 'key' | 'slug' | 'donationCount' | 'donationTotal'> = {
    nameZh: form.nameZh,
    nameEn: form.nameEn,
    oneLinerZh: form.oneLinerZh,
    oneLinerEn: form.oneLinerEn,
    fundUsageZh: form.fundUsageZh,
    fundUsageEn: form.fundUsageEn,
    minAmount: form.minAmount,
    maxAmount: form.maxAmount,
    amountOptions,
    sharePct: form.sharePct,
    charityRefCode: form.charityRefCode,
    charityRefName: fixtures.charityRefs.find(([code]) => code === form.charityRefCode)?.[1] ?? form.charityRefCode,
    programRefCode: form.programRefCode,
    programRefName: fixtures.charityProgramRefs.find(([code]) => code === form.programRefCode)?.[2] ?? form.programRefCode,
    invoiceMode: form.invoiceMode,
    status: form.status,
    sortOrder: form.sortOrder,
  }

  if (isEdit.value && existing.value) {
    updateProject(existing.value.key, payload)
    ElMessage.success('已儲存捐款項目')
  } else {
    addProject(payload)
    ElMessage.success('已新增捐款項目')
  }
  router.push('/projects')
}
</script>

<template>
  <div>
    <PageHeader :title="isEdit ? '編輯捐款項目' : '新增捐款項目'" frontend-unit="項目詳情頁">
      <template #back>
        <el-button text @click="router.push('/projects')">返回列表</el-button>
      </template>
    </PageHeader>

    <el-form label-position="top" class="project-edit__form">
      <h2 class="project-edit__section-title">基本資訊</h2>
      <BilingualShortField label="項目名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="form.nameZh = $event" @update:en="form.nameEn = $event" />
      <BilingualShortField label="一句話說明" :zh="form.oneLinerZh" :en="form.oneLinerEn" @update:zh="form.oneLinerZh = $event" @update:en="form.oneLinerEn = $event" />

      <h2 class="project-edit__section-title">內容</h2>
      <el-tabs>
        <el-tab-pane label="款項用途（中文）">
          <el-input v-model="form.fundUsageZh" type="textarea" :rows="4" />
        </el-tab-pane>
        <el-tab-pane label="款項用途（英文）">
          <el-input v-model="form.fundUsageEn" type="textarea" :rows="4" />
        </el-tab-pane>
      </el-tabs>

      <h2 class="project-edit__section-title">封面圖</h2>
      <ImageUploader :existing-url="null" variant="photo" @update:file="handleCoverUpdate" />

      <h2 class="project-edit__section-title">金額設定</h2>
      <el-row :gutter="16">
        <el-col :sm="12" :xs="24">
          <el-form-item label="最低金額">
            <el-input-number v-model="form.minAmount" :min="1" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :sm="12" :xs="24">
          <el-form-item label="最高金額">
            <el-input-number v-model="form.maxAmount" :min="form.minAmount" style="width: 100%" />
          </el-form-item>
        </el-col>
      </el-row>
      <el-form-item label="金額選項卡（以頓號分隔）">
        <el-input v-model="form.amountOptionsText" placeholder="例如：300、500、1000、3000" />
      </el-form-item>

      <h2 class="project-edit__section-title">分潤與撥付對象</h2>
      <el-form-item label="項目分潤比例（%）">
        <el-input-number v-model="form.sharePct" :min="0" :max="100" :step="0.5" />
      </el-form-item>
      <p class="project-edit__validation" :class="{ 'project-edit__validation--error': overLimit }">
        店家分潤與項目分潤相加不得超過 100%，以目前分潤比例最高的店家（{{ maxStoreSharePct }}%）試算，目前合計 {{ combinedPct }}%
      </p>
      <el-form-item label="撥付對象（公益機構）">
        <el-select v-model="form.charityRefCode" style="width: 100%">
          <el-option v-for="[code, name] in fixtures.charityRefs" :key="code" :value="code" :label="name" />
        </el-select>
      </el-form-item>
      <el-form-item label="撥付對象（公益計畫，選填）">
        <el-select v-model="form.programRefCode" style="width: 100%" clearable>
          <el-option v-for="[code, , name] in programOptions" :key="code" :value="code" :label="name" />
        </el-select>
      </el-form-item>
      <p class="project-edit__hint">此清單來自官方網站的公益團體資料，如需新增或更新請聯絡官網管理端</p>

      <h2 class="project-edit__section-title">憑證模式</h2>
      <el-form-item>
        <el-radio-group v-model="form.invoiceMode">
          <el-radio value="b2c_invoice">電子發票</el-radio>
          <el-radio value="donation_receipt">捐贈收據</el-radio>
        </el-radio-group>
      </el-form-item>

      <h2 class="project-edit__section-title">上下架</h2>
      <el-form-item>
        <el-radio-group v-model="form.status">
          <el-radio value="published">上架</el-radio>
          <el-radio value="draft">下架</el-radio>
        </el-radio-group>
      </el-form-item>

      <div class="project-edit__actions">
        <el-button @click="router.push('/projects')">取消</el-button>
        <el-button type="primary" @click="handleSave">儲存</el-button>
      </div>
    </el-form>
  </div>
</template>

<style scoped>
.project-edit__form {
  max-width: 760px;
}

.project-edit__section-title {
  font-size: 15px;
  color: var(--charity-admin-text-primary);
  margin: var(--charity-admin-space-6) 0 var(--charity-admin-space-3);
  padding-top: var(--charity-admin-space-3);
  border-top: 1px solid var(--charity-admin-border);
}

.project-edit__section-title:first-child {
  margin-top: 0;
  padding-top: 0;
  border-top: none;
}

.project-edit__validation {
  margin: -8px 0 16px;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-edit__validation--error {
  color: var(--charity-danger-text);
  font-weight: 600;
}

.project-edit__hint {
  margin: -8px 0 16px;
  font-size: 12px;
  color: var(--charity-admin-text-tertiary);
}

.project-edit__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--charity-admin-space-2);
  margin-top: var(--charity-admin-space-6);
  padding-top: var(--charity-admin-space-4);
  border-top: 1px solid var(--charity-admin-border);
}
</style>
