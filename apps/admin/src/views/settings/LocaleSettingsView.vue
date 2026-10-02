<script setup lang="ts">
/**
 * I 多語系（規劃書 §4.9「多語系管理」）：語系設定／翻譯狀態總覽／介面字串翻譯表三個分頁。
 * 依權限顯示分頁：語系設定與總覽需要 `site.locale.view`（總覽另開放給字串翻譯權限者）；
 * 介面字串需要任一字串權限。翻譯人員（只有字串檢視＋翻譯）會看到「翻譯狀態」與「介面字串」。
 * 語系清單（全站共用）由這裡載入一次，傳給三個分頁。
 */
import { computed, onMounted, ref, watch } from 'vue'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import LocaleConfigPanel from './parts/LocaleConfigPanel.vue'
import TranslationOverviewPanel from './parts/TranslationOverviewPanel.vue'
import UiStringsPanel from './parts/UiStringsPanel.vue'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listAdminLocales, type AdminLocale } from '@/api/adminSiteSettings'

const club = computed(() => activeClubId.value)
const localeView = usePermission('site.locale.view')
const stringView = usePermission('site.string.view')
const stringUpdate = usePermission('site.string.update')
const stringTranslate = usePermission('site.string.translate')
const anyString = computed(() => stringView.value || stringUpdate.value || stringTranslate.value)

type TabName = 'locales' | 'overview' | 'strings'
const tabs = computed<{ name: TabName; label: string }[]>(() => {
  const list: { name: TabName; label: string }[] = []
  if (localeView.value) list.push({ name: 'locales', label: '語言設定' })
  if (localeView.value || anyString.value) list.push({ name: 'overview', label: '翻譯狀態' })
  if (anyString.value) list.push({ name: 'strings', label: '介面字串' })
  return list
})
const activeTab = ref<TabName>('overview')

const locales = ref<AdminLocale[]>([])
const state = ref<'loading' | 'ready' | 'error'>('loading')
const errorMessage = ref('')

async function load() {
  state.value = 'loading'
  try {
    locales.value = await listAdminLocales(club.value)
    state.value = 'ready'
  } catch (error) {
    errorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    state.value = 'error'
  }
}
/** 語系儲存後的靜默重讀：不切換載入狀態，才不會把分頁整個卸載重來。 */
async function reloadLocales() {
  try {
    locales.value = await listAdminLocales(club.value)
  } catch {
    /* 保留舊清單，畫面不打斷 */
  }
}
onMounted(() => {
  activeTab.value = tabs.value[0]?.name ?? 'overview'
  void load()
})
watch(club, load)
</script>

<template>
  <div class="locale-settings">
    <PageHeader title="多語系">
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-card v-if="state === 'loading'" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="state === 'error'" shadow="never">
      <el-empty :image-size="96" :description="errorMessage"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-empty v-else-if="tabs.length === 0" description="你的帳號沒有可使用的多語系功能。" />
    <el-tabs v-else v-model="activeTab">
      <el-tab-pane v-for="t in tabs" :key="t.name" :label="t.label" :name="t.name" lazy>
        <LocaleConfigPanel v-if="t.name === 'locales'" :club="club" :locales="locales" @locales-changed="reloadLocales" />
        <TranslationOverviewPanel v-else-if="t.name === 'overview'" :club="club" :locales="locales" />
        <UiStringsPanel v-else :club="club" :locales="locales" />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.locale-settings { max-width: 1200px; margin: 0 auto 48px; min-width: 0; }
</style>
