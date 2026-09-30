<script setup lang="ts">
/** 試訓簽到表：資料載入後可直接列印（不產生檔案）。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import SignInSheet from '@/components/SignInSheet.vue'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { getTrialSignInSheet, type TrialSignInSheetDto } from '@/api/adminTrials'

const props = defineProps<{ id: string }>()
const router = useRouter()
const club = computed(() => activeClubId.value)

const sheet = ref<TrialSignInSheetDto | null>(null)
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    sheet.value = await getTrialSignInSheet(club.value, props.id)
  } catch (error) {
    sheet.value = null
    loadError.value =
      error instanceof AdminApiError && error.kind === 'not-found'
        ? '找不到這場試訓，可能已被刪除，或不屬於目前選擇的俱樂部。'
        : error instanceof AdminApiError
          ? error.message
          : '簽到表載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(load)
watch([club, () => props.id], load)

function printSheet() {
  window.print()
}

const infos = computed(() => {
  const s = sheet.value
  if (!s) return []
  const list = [{ label: '試訓日', value: s.trialOn }]
  list.push({ label: '球隊', value: s.teamName || '俱樂部整體' })
  if (s.venueName) list.push({ label: '場地', value: s.venueName })
  if (s.audienceZh) list.push({ label: '對象', value: s.audienceZh })
  return list
})

function back() {
  router.push(`/programs/trials/${props.id}/registrations`)
}
</script>

<template>
  <div class="sign-in-page">
    <PageHeader title="試訓簽到表">
      <template #meta><FrontendUnitBanner module-code="P4" /></template>
    </PageHeader>

    <div class="sign-in-page__bar no-print">
      <el-button @click="back"><el-icon><ArrowLeft /></el-icon>返回報名名單</el-button>
      <el-button type="primary" :disabled="!sheet" @click="printSheet">列印簽到表</el-button>
    </div>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <SignInSheet v-else-if="sheet" title="試訓簽到表" :infos="infos" :rows="sheet.rows" :generated-at="sheet.generatedAt" />
  </div>
</template>

<style scoped>
.sign-in-page__bar { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 12px; }
</style>
