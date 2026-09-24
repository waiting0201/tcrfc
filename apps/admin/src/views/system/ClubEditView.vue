<script setup lang="ts">
/**
 * J4 俱樂部主檔編輯頁。
 * 🔴 標誌／favicon／OG 圖三組欄位本輪唯讀（見 `@/api/adminClubs` 檔頭說明），畫面上只顯示
 * 目前是否已設定，不提供上傳；之後要補時應比照新聞封面圖片的作法（選檔即時預覽、儲存才上傳）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { createAdminClub, getAdminClub, updateAdminClub } from '@/api/adminClubs'
import { AdminApiError } from '@/api/http'

const route = useRoute()
const router = useRouter()

const isCreate = computed(() => route.name === 'system-club-new')
const clubId = ref<string | undefined>(route.params.id as string | undefined)

const form = reactive({
  code: '',
  domain: '',
  nameZh: '',
  descriptionZh: '',
  nameEn: '',
  descriptionEn: '',
  brandColor: '',
  brandSecondaryColor: '',
  invoiceTitle: '',
  taxId: '',
  isCollectingSubject: true,
  defaultLocale: 'zh-Hant',
  sortOrder: 0,
  status: 'active',
})
const logoKeys = reactive({ logoLightKey: null as string | null, logoDarkKey: null as string | null, faviconKey: null as string | null, ogImageKey: null as string | null })
const baselineJson = ref('')

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
const formError = ref<string | null>(null)

async function loadClub() {
  loadState.value = 'loading'
  try {
    if (!isCreate.value && clubId.value) {
      const detail = await getAdminClub(clubId.value)
      form.code = detail.code
      form.domain = detail.domain
      form.nameZh = detail.zh.name
      form.descriptionZh = detail.zh.description ?? ''
      form.nameEn = detail.en?.name ?? ''
      form.descriptionEn = detail.en?.description ?? ''
      form.brandColor = detail.brandColor ?? ''
      form.brandSecondaryColor = detail.brandSecondaryColor ?? ''
      form.invoiceTitle = detail.invoiceTitle ?? ''
      form.taxId = detail.taxId ?? ''
      form.isCollectingSubject = detail.isCollectingSubject
      form.defaultLocale = detail.defaultLocale
      form.sortOrder = detail.sortOrder
      form.status = detail.status ?? 'active'
      logoKeys.logoLightKey = detail.logoLightKey ?? null
      logoKeys.logoDarkKey = detail.logoDarkKey ?? null
      logoKeys.faviconKey = detail.faviconKey ?? null
      logoKeys.ogImageKey = detail.ogImageKey ?? null
    }
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadClub)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

const pageTitle = computed(() => (isCreate.value ? '新增俱樂部' : `編輯俱樂部：${form.nameZh}`))

/** 英文名稱是必填才有意義的欄位（跟中文一樣，`AdminClubLocaleContent.name` 是必填字串），
 * 沒有英文名稱就視為「還沒有英文版本」，不整份送出（避免送出一個沒有名稱的英文版本）。 */
function isEnEmpty(): boolean {
  return !form.nameEn.trim()
}

function validate(): boolean {
  formError.value = null
  if (isCreate.value && !form.code.trim()) {
    formError.value = '請輸入俱樂部代碼'
    return false
  }
  if (!form.domain.trim()) {
    formError.value = '請輸入前台網域'
    return false
  }
  if (!form.nameZh.trim()) {
    formError.value = '請輸入中文名稱'
    return false
  }
  return true
}

async function handleSave() {
  if (!validate()) return
  saving.value = true
  try {
    const content = {
      zh: { name: form.nameZh.trim(), description: form.descriptionZh || null },
      en: isEnEmpty() ? undefined : { name: form.nameEn.trim(), description: form.descriptionEn || null },
    }
    if (isCreate.value) {
      const created = await createAdminClub({
        code: form.code.trim(),
        domain: form.domain.trim(),
        content,
        brandColor: form.brandColor || null,
        brandSecondaryColor: form.brandSecondaryColor || null,
        invoiceTitle: form.invoiceTitle || null,
        taxId: form.taxId || null,
        isCollectingSubject: form.isCollectingSubject,
        defaultLocale: form.defaultLocale,
        sortOrder: form.sortOrder,
      })
      ElMessage.success('已建立俱樂部')
      router.replace(`/system/clubs/${created.id}/edit`)
      clubId.value = created.id
    } else {
      await updateAdminClub(clubId.value!, {
        domain: form.domain.trim(),
        content,
        brandColor: form.brandColor || null,
        brandSecondaryColor: form.brandSecondaryColor || null,
        invoiceTitle: form.invoiceTitle || null,
        taxId: form.taxId || null,
        isCollectingSubject: form.isCollectingSubject,
        defaultLocale: form.defaultLocale,
        sortOrder: form.sortOrder,
        status: form.status,
      })
      ElMessage.success('已儲存')
    }
    baselineJson.value = JSON.stringify(form)
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function handleBack() {
  router.push('/system/clubs')
}
</script>

<template>
  <div class="club-edit">
    <PageHeader :title="pageTitle">
      <template #back>
        <el-button text @click="handleBack">
          <el-icon><ArrowLeft /></el-icon>
          返回列表
        </el-button>
      </template>
      <template #meta>
        <FrontendUnitBanner module-code="J4" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="8" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadClub">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="club-edit__form-error"
        @close="formError = null"
      />

      <el-card shadow="never" header="基本資料" class="club-edit__section">
        <el-form label-position="top">
          <el-form-item label="俱樂部代碼" required>
            <el-input v-model="form.code" :disabled="!isCreate" placeholder="建立後不可修改，例如 tcrfc、bw" />
          </el-form-item>
          <el-form-item label="前台網域" required>
            <el-input v-model="form.domain" placeholder="例如 www.tcrfc.tw" />
          </el-form-item>
          <BilingualShortField
            label="名稱"
            :zh="form.nameZh"
            :en="form.nameEn"
            required
            @update:zh="(v) => (form.nameZh = v)"
            @update:en="(v) => (form.nameEn = v)"
          />
          <BilingualShortField
            label="簡介"
            :zh="form.descriptionZh"
            :en="form.descriptionEn"
            @update:zh="(v) => (form.descriptionZh = v)"
            @update:en="(v) => (form.descriptionEn = v)"
          />
          <el-form-item label="排序">
            <el-input-number v-model="form.sortOrder" :min="0" />
          </el-form-item>
          <el-form-item v-if="!isCreate" label="啟用狀態">
            <el-select v-model="form.status" style="width: 160px">
              <el-option label="啟用" value="active" />
              <el-option label="停用" value="inactive" />
            </el-select>
          </el-form-item>
        </el-form>
      </el-card>

      <el-card shadow="never" header="法人資料" class="club-edit__section">
        <el-alert
          title="⚠️ 收款主體目前只有俱樂部本身。把合作球隊誤設為收款主體，會讓款項與發票歸屬出錯。"
          type="warning"
          show-icon
          :closable="false"
          class="club-edit__hint"
        />
        <el-form label-position="top">
          <el-form-item label="發票抬頭">
            <el-input v-model="form.invoiceTitle" placeholder="選填" />
          </el-form-item>
          <el-form-item label="統一編號">
            <el-input v-model="form.taxId" placeholder="選填" />
          </el-form-item>
          <el-form-item label="是否為收款主體">
            <el-switch v-model="form.isCollectingSubject" />
          </el-form-item>
        </el-form>
      </el-card>

      <el-card shadow="never" header="品牌設定" class="club-edit__section">
        <el-form label-position="top">
          <el-form-item label="主色（十六進位色碼）">
            <el-input v-model="form.brandColor" placeholder="例如 #E0218A" />
          </el-form-item>
          <el-form-item label="次要色（十六進位色碼）">
            <el-input v-model="form.brandSecondaryColor" placeholder="選填" />
          </el-form-item>
        </el-form>
        <p class="club-edit__note">
          標誌、favicon、社群分享圖片目前僅供檢視，尚未開放在這裡上傳（已知缺口，見交付說明）。
        </p>
        <ul class="club-edit__logo-status">
          <li>淺底標誌：{{ logoKeys.logoLightKey ? '已設定' : '尚未設定' }}</li>
          <li>深底標誌：{{ logoKeys.logoDarkKey ? '已設定' : '尚未設定' }}</li>
          <li>favicon：{{ logoKeys.faviconKey ? '已設定' : '尚未設定' }}</li>
          <li>社群分享圖片：{{ logoKeys.ogImageKey ? '已設定' : '尚未設定' }}</li>
        </ul>
      </el-card>

      <div class="club-edit__actions">
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.club-edit__section {
  margin-bottom: 12px;
}

.club-edit__form-error,
.club-edit__hint {
  margin-bottom: 12px;
}

.club-edit__note {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin: 8px 0;
}

.club-edit__logo-status {
  font-size: 13px;
  color: var(--admin-text-secondary);
  padding-left: 18px;
  margin: 0;
}

.club-edit__actions {
  margin-top: 16px;
}
</style>
