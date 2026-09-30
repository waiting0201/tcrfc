<script setup lang="ts">
/** 課程簽到表：先選梯次（也可由報名管理帶入），載入後可直接列印。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import SignInSheet from '@/components/SignInSheet.vue'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { listAdminProgramSessions, type AdminSessionListItemDto } from '@/api/adminProgramSessions'
import { getRegistrationSignInSheet, type RegistrationSignInSheetDto } from '@/api/adminRegistrations'

const route = useRoute()
const router = useRouter()
const club = computed(() => activeClubId.value)

const sessions = ref<AdminSessionListItemDto[]>([])
const sessionId = ref('')
const sheet = ref<RegistrationSignInSheetDto | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)

function sessionLabel(s: AdminSessionListItemDto): string {
  return `${s.programNameZh ?? '（未命名項目）'}（${s.startOn ?? '—'} ～ ${s.endOn ?? '—'}）`
}

async function loadSheet() {
  sheet.value = null
  loadError.value = null
  if (!sessionId.value) return
  loading.value = true
  try {
    sheet.value = await getRegistrationSignInSheet(club.value, sessionId.value)
  } catch (error) {
    loadError.value =
      error instanceof AdminApiError && error.kind === 'not-found'
        ? '找不到這個梯次，可能已被刪除，或不屬於目前選擇的俱樂部。'
        : error instanceof AdminApiError
          ? error.message
          : '簽到表載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

async function bootstrap() {
  try {
    sessions.value = await listAdminProgramSessions(club.value)
  } catch {
    sessions.value = []
  }
  sessionId.value = typeof route.query.sessionId === 'string' ? route.query.sessionId : ''
  await loadSheet()
}

onMounted(bootstrap)
// 切換俱樂部：梯次屬於原俱樂部，清掉選擇重新來
watch(club, () => {
  router.replace({ query: {} })
  bootstrap()
})

function handleSelect() {
  router.replace({ query: sessionId.value ? { sessionId: sessionId.value } : {} })
  loadSheet()
}

function printSheet() {
  window.print()
}

const infos = computed(() => {
  const s = sheet.value
  if (!s) return []
  const list = [{ label: '課程', value: s.programNameZh ?? '（未命名項目）' }]
  list.push({ label: '梯次期間', value: `${s.startOn ?? '—'} ～ ${s.endOn ?? '—'}` })
  if (s.venueName) list.push({ label: '場地', value: s.venueName })
  return list
})
</script>

<template>
  <div class="sign-in-page">
    <PageHeader title="課程簽到表">
      <template #meta><FrontendUnitBanner module-code="P3" /></template>
    </PageHeader>

    <el-card shadow="never" class="sign-in-page__bar no-print">
      <div class="sign-in-page__bar-row">
        <el-button @click="router.push('/programs/enrollments')"><el-icon><ArrowLeft /></el-icon>返回報名管理</el-button>
        <el-select v-model="sessionId" placeholder="請選擇梯次" filterable class="sign-in-page__select" @change="handleSelect">
          <el-option v-for="s in sessions" :key="s.id" :label="sessionLabel(s)" :value="s.id" />
        </el-select>
        <el-button type="primary" :disabled="!sheet" @click="printSheet">列印簽到表</el-button>
      </div>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="loadSheet">重新載入</el-button></el-empty>
    </el-card>
    <SignInSheet v-else-if="sheet" title="課程簽到表" :infos="infos" :rows="sheet.rows" :generated-at="sheet.generatedAt" />
    <el-card v-else shadow="never"><el-empty description="請先選擇梯次，再產生簽到表" /></el-card>
  </div>
</template>

<style scoped>
.sign-in-page__bar { margin-bottom: 12px; }
.sign-in-page__bar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.sign-in-page__select { width: 360px; max-width: 100%; }
</style>
