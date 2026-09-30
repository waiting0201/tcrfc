<script setup lang="ts">
/**
 * 捐款導流與參與方式設定（每個俱樂部各一份）。前台不寫死捐款網址，一律讀這裡的設定。
 * 慈善捐款由「台灣足球策略發展協會」收受，所以設了捐款網址時，按鈕文案必須點明協會，避免讓人誤以為捐給球團。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { getCharitySettings, saveCharitySettings, type BilingualTextDto } from '@/api/adminCharity'

const { canUpdate } = useViewUpdatePermissions('charity.setting')
const club = computed(() => activeClubId.value)
const ASSOCIATION = '台灣足球策略發展協會'

const form = reactive({
  donationUrl: '', donationCtaZh: '', donationCtaEn: '',
  fanCtaZh: '', fanCtaEn: '',
  corporateUrl: '', corporateCtaZh: '', corporateCtaEn: '',
})
const baseline = ref('')
const loading = ref(true)
const loadError = ref<string | null>(null)
const saving = ref(false)
const formError = ref<string | null>(null)
const isDirty = computed(() => !loading.value && JSON.stringify(form) !== baseline.value)
useUnsavedChanges(isDirty)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const s = await getCharitySettings(club.value)
    form.donationUrl = s.donationUrl ?? ''
    form.donationCtaZh = s.donationCta?.zh ?? ''
    form.donationCtaEn = s.donationCta?.en ?? ''
    form.fanCtaZh = s.fanCta?.zh ?? ''
    form.fanCtaEn = s.fanCta?.en ?? ''
    form.corporateUrl = s.corporateUrl ?? ''
    form.corporateCtaZh = s.corporateCta?.zh ?? ''
    form.corporateCtaEn = s.corporateCta?.en ?? ''
    baseline.value = JSON.stringify(form)
  } catch (error) {
    loadError.value = error instanceof AdminApiError ? error.message : '設定載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

function bilingual(zh: string, en: string): BilingualTextDto | null {
  const z = nullIfBlank(zh)
  const e = nullIfBlank(en)
  return z || e ? { zh: z, en: e } : null
}

function validate(): string | null {
  const url = form.donationUrl.trim()
  if (url) {
    if (!/^https:\/\//i.test(url)) return '捐款網址必須是以 https:// 開頭的完整網址'
    if (!form.donationCtaZh.trim()) return '設定了捐款網址，就必須填寫中文的捐款按鈕文案'
    if (!form.donationCtaZh.includes(ASSOCIATION)) return `捐款按鈕文案必須點明捐款由「${ASSOCIATION}」收受，不能讓人誤以為捐給台中磐石`
  }
  const corp = form.corporateUrl.trim()
  if (corp && !(/^https:\/\//i.test(corp) || (corp.startsWith('/') && !corp.startsWith('//')))) {
    return '企業合作連結必須是以 / 開頭的站內路徑，或以 https:// 開頭的網址'
  }
  return null
}

async function handleSave() {
  formError.value = validate()
  if (formError.value) return
  saving.value = true
  try {
    // 整份取代：沒填的欄位就是清空
    await saveCharitySettings(club.value, {
      donationUrl: nullIfBlank(form.donationUrl),
      donationCta: bilingual(form.donationCtaZh, form.donationCtaEn),
      corporateCta: bilingual(form.corporateCtaZh, form.corporateCtaEn),
      corporateUrl: nullIfBlank(form.corporateUrl),
      fanCta: bilingual(form.fanCtaZh, form.fanCtaEn),
    })
    baseline.value = JSON.stringify(form)
    ElMessage.success('已儲存')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="settings">
    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never"><el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty></el-card>
    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="settings__block" @close="formError = null" />
      <el-alert v-if="!canUpdate" title="你的帳號只能檢視這些設定，不能修改。" type="info" show-icon :closable="false" class="settings__block" />
      <el-form label-position="top" :disabled="!canUpdate">
        <el-card shadow="never" header="球迷捐款導流" class="settings__block">
          <p class="settings__hint">前台各慈善頁的「捐款」按鈕會導向這個網址。不填網址就不會顯示球迷捐款按鈕。</p>
          <el-form-item label="捐款網址（慈善捐款平台）"><el-input v-model="form.donationUrl" placeholder="https://" /></el-form-item>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="捐款按鈕文案（中文）"><el-input v-model="form.donationCtaZh" :placeholder="`例如 前往${ASSOCIATION}捐款平台`" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="捐款按鈕文案（英文）"><el-input v-model="form.donationCtaEn" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="球迷參與文案（中文）"><el-input v-model="form.fanCtaZh" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="球迷參與文案（英文）"><el-input v-model="form.fanCtaEn" /></el-form-item></el-col>
          </el-row>
          <p class="settings__hint">中文捐款文案必須包含「{{ ASSOCIATION }}」，因為捐款由協會收受，不是台中磐石。球迷參與按鈕固定導向上面的捐款網址。</p>
        </el-card>
        <el-card shadow="never" header="企業合作" class="settings__block">
          <el-form-item label="企業合作連結"><el-input v-model="form.corporateUrl" placeholder="/zh/partners/ 或 https://" /></el-form-item>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="企業合作文案（中文）"><el-input v-model="form.corporateCtaZh" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="企業合作文案（英文）"><el-input v-model="form.corporateCtaEn" /></el-form-item></el-col>
          </el-row>
        </el-card>
      </el-form>
      <EditActionBar v-if="canUpdate"><el-button type="primary" :loading="saving" @click="handleSave">儲存設定</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.settings { max-width: 780px; margin: 0 auto 88px; }
.settings__block { margin-bottom: 16px; }
.settings__hint { margin: 0 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
</style>
