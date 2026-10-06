<script setup lang="ts">
/**
 * A 儀表板（規劃書 §4.1；apps/api/README.md「H 批」§1）。三支端點各自載入、各自失敗，互不拖累：
 * - 主資料 `dashboard`：待辦、內容概況、常見問題、未來 14 天行程、會籍、快速入口。
 * - `dashboard/conversion`：轉換概況（週／月切換）。
 * - `dashboard/traffic`：流量概況；目前一律「尚未串接」，畫面清楚說明，不顯示 0。
 *
 * 🔴 **回 `null`（或不在清單）＝這個帳號沒有該區塊的權限，整塊不顯示，不是顯示 0**。
 * 切換站台時整頁重新載入。已刪除原本的假資料檔（`data/dashboard.ts`）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import {
  getDashboard,
  getDashboardConversion,
  getDashboardTraffic,
  type ConversionBucket,
  type ConversionDto,
  type ConversionPeriod,
  type DashboardDto,
  type DashboardUpcomingItem,
  type TrafficDto,
} from '@/api/adminDashboard'
import { formatDateTime } from '@/utils/dateTime'
import { todoItemRoute, todoRoute } from '@/utils/todoRoutes'

const router = useRouter()
const club = computed(() => activeClubId.value)

const dashboard = ref<DashboardDto | null>(null)
const mainState = ref<'loading' | 'ready' | 'forbidden' | 'error'>('loading')
const mainError = ref('')

const period = ref<ConversionPeriod>('week')
const conversion = ref<ConversionDto | null>(null)
const conversionState = ref<'loading' | 'ready' | 'hidden' | 'error'>('loading')

const traffic = ref<TrafficDto | null>(null)
const trafficState = ref<'loading' | 'ready' | 'hidden' | 'error'>('loading')

let loadSeq = 0

async function loadMain() {
  const seq = ++loadSeq
  mainState.value = 'loading'
  try {
    const data = await getDashboard(club.value)
    if (seq !== loadSeq) return
    dashboard.value = data
    mainState.value = 'ready'
  } catch (error) {
    if (seq !== loadSeq) return
    dashboard.value = null
    if (error instanceof AdminApiError && error.kind === 'forbidden') {
      mainState.value = 'forbidden'
    } else {
      mainError.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      mainState.value = 'error'
    }
  }
}

async function loadConversion() {
  conversionState.value = 'loading'
  const requestedPeriod = period.value
  const requestedClub = club.value
  try {
    const data = await getDashboardConversion(requestedClub, requestedPeriod)
    if (requestedPeriod !== period.value || requestedClub !== club.value) return
    conversion.value = data
    conversionState.value = 'ready'
  } catch (error) {
    if (requestedPeriod !== period.value || requestedClub !== club.value) return
    conversion.value = null
    conversionState.value = error instanceof AdminApiError && error.kind === 'forbidden' ? 'hidden' : 'error'
  }
}

async function loadTraffic() {
  trafficState.value = 'loading'
  const requestedClub = club.value
  try {
    const data = await getDashboardTraffic(requestedClub)
    if (requestedClub !== club.value) return
    traffic.value = data
    trafficState.value = 'ready'
  } catch (error) {
    if (requestedClub !== club.value) return
    traffic.value = null
    trafficState.value = error instanceof AdminApiError && error.kind === 'forbidden' ? 'hidden' : 'error'
  }
}

function loadAll() {
  void loadMain()
  void loadConversion()
  void loadTraffic()
}

onMounted(loadAll)
watch(club, loadAll)
watch(period, loadConversion)

// ───────────── 待辦 ─────────────

function go(path: string | null) {
  if (path) void router.push(path)
}

// ───────────── 行程 ─────────────

const SOURCE_LABEL: Record<string, string> = {
  match: '賽事',
  session: '課程梯次',
  trial: '試訓',
  event: '行事曆',
  fan_event: '球迷會活動',
}

function upcomingRoute(item: DashboardUpcomingItem): string | null {
  switch (item.source) {
    case 'match': return `/teams/matches/${item.id}/edit`
    case 'session': return `/programs/sessions/${item.id}/edit`
    case 'trial': return `/programs/trials/${item.id}/edit`
    case 'event': return `/calendar/events/${item.id}/edit`
    case 'fan_event': return `/culture/fan-events/${item.id}/edit`
    default: return null
  }
}

// ───────────── 快速入口 ─────────────

const QUICK_ROUTES: Record<string, string> = {
  publish_news: '/content/news/new',
  add_match: '/teams/matches/new',
  add_session: '/programs/sessions/new',
  add_faq: '/content/faq/new',
  add_calendar_event: '/calendar/events/new',
}

const quickEntries = computed(() =>
  (dashboard.value?.quickEntries ?? []).filter((entry) => QUICK_ROUTES[entry.code]),
)

// ───────────── 內容概況 ─────────────

const untranslatedTotal = computed(() =>
  (dashboard.value?.content?.untranslated ?? []).reduce((sum, u) => sum + u.count, 0),
)

// ───────────── 轉換概況 ─────────────

interface SeriesDef {
  key: keyof ConversionBucket
  label: string
}

const SERIES: SeriesDef[] = [
  { key: 'enquiries', label: '詢問' },
  { key: 'registrations', label: '報名' },
  { key: 'proposalDownloads', label: '提案下載' },
  { key: 'newMembers', label: '新註冊會員' },
  { key: 'newPaidMemberships', label: '新加入付費會籍' },
  { key: 'renewals', label: '續會' },
]

/** 只列有權限（回傳不是 null）的序列。 */
const visibleSeries = computed(() => {
  const totals = conversion.value?.totals
  if (!totals) return []
  return SERIES.filter((s) => totals[s.key] !== null && totals[s.key] !== undefined)
})

function bucketValue(bucket: ConversionBucket, key: keyof ConversionBucket): number {
  const v = bucket[key]
  return typeof v === 'number' ? v : 0
}

function bucketLabel(bucket: ConversionBucket): string {
  if (!bucket.start) return ''
  const [y, m, d] = bucket.start.split('-')
  return period.value === 'month' ? `${y}/${m}` : `${m}/${d}`
}

const bucketMax = computed(() => {
  let max = 0
  for (const bucket of conversion.value?.buckets ?? []) {
    for (const s of visibleSeries.value) max = Math.max(max, bucketValue(bucket, s.key))
  }
  return max
})

function barHeight(value: number): string {
  if (bucketMax.value <= 0) return '0%'
  return `${Math.max(value > 0 ? 4 : 0, Math.round((value / bucketMax.value) * 100))}%`
}

const SERIES_CLASS: Record<string, string> = {
  enquiries: 'dash-bar--a',
  registrations: 'dash-bar--b',
  proposalDownloads: 'dash-bar--c',
  newMembers: 'dash-bar--d',
  newPaidMemberships: 'dash-bar--e',
  renewals: 'dash-bar--f',
}

const periodRangeText = computed(() =>
  conversion.value ? `${conversion.value.from} 至 ${conversion.value.to}` : '',
)
</script>

<template>
  <div class="dashboard">
    <PageHeader title="儀表板">
      <template #meta>
        <FrontendUnitBanner module-code="A" />
      </template>
    </PageHeader>

    <div v-if="mainState === 'loading'" v-loading="true" class="dashboard__loading" />

    <el-alert
      v-else-if="mainState === 'error'"
      :title="mainError"
      type="error"
      show-icon
      :closable="false"
    >
      <el-button size="small" @click="loadAll">重新載入</el-button>
    </el-alert>

    <el-empty
      v-else-if="mainState === 'forbidden'"
      description="你的帳號目前沒有可顯示在儀表板的項目。需要的話請聯絡系統管理員。"
    />

    <template v-else-if="dashboard">
      <!-- 會籍與內容概況：沒有權限（null）的整張卡都不出現 -->
      <el-row :gutter="16" class="dashboard__stats">
        <template v-if="dashboard.members">
          <el-col :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">有效會籍</div>
              <div class="dashboard__stat-value">{{ dashboard.members.activeMemberships }}</div>
            </el-card>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">有效的付費會籍</div>
              <div class="dashboard__stat-value">{{ dashboard.members.activePaidMemberships }}</div>
            </el-card>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">30 天內到期的會籍</div>
              <div class="dashboard__stat-value dashboard__stat-value--warning">{{ dashboard.members.expiringIn30Days }}</div>
            </el-card>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">待處理的升級申請</div>
              <div class="dashboard__stat-value">{{ dashboard.members.pendingUpgrades }}</div>
            </el-card>
          </el-col>
        </template>
        <template v-if="dashboard.content">
          <el-col v-if="dashboard.content.publishedThisMonth != null" :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">本月已發布的文章</div>
              <div class="dashboard__stat-value">{{ dashboard.content.publishedThisMonth }}</div>
            </el-card>
          </el-col>
          <el-col v-if="dashboard.content.draftCount != null" :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">草稿中的文章</div>
              <div class="dashboard__stat-value">{{ dashboard.content.draftCount }}</div>
            </el-card>
          </el-col>
          <el-col v-if="dashboard.content.scheduledCount != null" :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">排程發布中的文章</div>
              <div class="dashboard__stat-value">{{ dashboard.content.scheduledCount }}</div>
            </el-card>
          </el-col>
          <el-col v-if="dashboard.content.untranslated.length > 0" :xs="24" :sm="12" :lg="6">
            <el-card shadow="never" class="dashboard__stat-card">
              <div class="dashboard__stat-label">尚未翻譯的內容</div>
              <div class="dashboard__stat-value" :class="{ 'dashboard__stat-value--warning': untranslatedTotal > 0 }">
                {{ untranslatedTotal }}
              </div>
              <ul class="dashboard__mini-list">
                <li v-for="u in dashboard.content.untranslated" :key="u.locale">
                  缺{{ u.localeName }}：{{ u.count }} 筆
                  <span v-if="u.byType.length" class="dashboard__muted">
                    （{{ u.byType.map((t) => `${t.typeLabel} ${t.count}`).join('、') }}）
                  </span>
                </li>
              </ul>
            </el-card>
          </el-col>
        </template>
      </el-row>

      <el-row :gutter="16" class="dashboard__row">
        <el-col :xs="24" :lg="8">
          <el-card shadow="never" header="待辦提醒" class="dashboard__card">
            <el-empty v-if="dashboard.todos.length === 0" description="目前沒有待辦事項" :image-size="48" />
            <ul v-else class="dashboard__list">
              <li v-for="todo in dashboard.todos" :key="todo.code" class="dashboard__todo">
                <a class="dashboard__list-link" role="link" tabindex="0" @click="go(todoRoute(todo))" @keydown.enter="go(todoRoute(todo))">
                  <span>{{ todo.label }}</span>
                  <el-tag size="small" :type="todo.count > 0 ? 'warning' : 'info'">{{ todo.count }}</el-tag>
                </a>
                <div v-if="todo.hint" class="dashboard__muted dashboard__hint">{{ todo.hint }}</div>
                <ul v-if="todo.items.length" class="dashboard__sub-list">
                  <li v-for="item in todo.items" :key="item.id">
                    <a role="link" tabindex="0" @click="go(todoItemRoute(todo, item.id))" @keydown.enter="go(todoItemRoute(todo, item.id))">
                      <span v-if="item.date" class="dashboard__event-date">{{ item.date }}</span>{{ item.title }}
                    </a>
                  </li>
                </ul>
              </li>
            </ul>
          </el-card>
        </el-col>

        <el-col :xs="24" :lg="8" class="dashboard__col-spacing">
          <el-card shadow="never" header="未來 14 天行程" class="dashboard__card">
            <el-empty v-if="dashboard.upcoming.length === 0" description="未來 14 天沒有行程" :image-size="48" />
            <ul v-else class="dashboard__list">
              <li v-for="item in dashboard.upcoming" :key="`${item.source}-${item.id}`" class="dashboard__upcoming">
                <div class="dashboard__upcoming-head">
                  <span class="dashboard__event-date">{{ item.date }}<template v-if="item.time"> {{ item.time }}</template></span>
                  <el-tag size="small">{{ SOURCE_LABEL[item.source] ?? '其他' }}</el-tag>
                </div>
                <a class="dashboard__event-title" role="link" tabindex="0" @click="go(upcomingRoute(item))" @keydown.enter="go(upcomingRoute(item))">{{ item.title }}</a>
                <div v-if="item.venueName" class="dashboard__muted">{{ item.venueName }}</div>
                <div v-if="item.warnings.length" class="dashboard__warnings">
                  <el-tag v-for="w in item.warnings" :key="w" size="small" type="warning" effect="plain">{{ w }}</el-tag>
                </div>
              </li>
            </ul>
          </el-card>
        </el-col>

        <el-col :xs="24" :lg="8" class="dashboard__col-spacing">
          <el-card v-if="quickEntries.length" shadow="never" header="快速入口" class="dashboard__card dashboard__card--quick">
            <div class="dashboard__quick-entries">
              <el-button v-for="entry in quickEntries" :key="entry.code" @click="go(QUICK_ROUTES[entry.code])">
                {{ entry.label }}
              </el-button>
            </div>
          </el-card>
          <el-card v-if="dashboard.faq" shadow="never" header="常見問題概況" class="dashboard__card">
            <div class="dashboard__sub-title">瀏覽次數最多</div>
            <el-empty v-if="dashboard.faq.topQuestions.length === 0" description="還沒有瀏覽紀錄" :image-size="32" />
            <ol v-else class="dashboard__ranked">
              <li v-for="q in dashboard.faq.topQuestions" :key="q.id">
                <a role="link" tabindex="0" @click="go(`/content/faq/${q.id}/edit`)" @keydown.enter="go(`/content/faq/${q.id}/edit`)">{{ q.question }}</a>
                <span class="dashboard__muted">{{ q.viewCount }} 次</span>
              </li>
            </ol>
            <div class="dashboard__sub-title">負評偏多（可能需要改寫）</div>
            <el-empty v-if="dashboard.faq.negativeFeedback.length === 0" description="目前沒有負評偏多的題目" :image-size="32" />
            <ul v-else class="dashboard__ranked dashboard__ranked--plain">
              <li v-for="q in dashboard.faq.negativeFeedback" :key="q.id">
                <a role="link" tabindex="0" @click="go(`/content/faq/${q.id}/edit`)" @keydown.enter="go(`/content/faq/${q.id}/edit`)">{{ q.question }}</a>
                <span class="dashboard__muted">有幫助 {{ q.helpfulCount }}／沒幫助 {{ q.unhelpfulCount }}</span>
              </li>
            </ul>
          </el-card>
        </el-col>
      </el-row>
    </template>

    <!-- 轉換概況：403 代表沒有任何一項轉換序列的權限，整塊不顯示 -->
    <el-card v-if="conversionState !== 'hidden' && mainState !== 'forbidden'" shadow="never" class="dashboard__wide">
      <template #header>
        <div class="dashboard__card-header">
          <span>轉換概況</span>
          <el-radio-group v-model="period" size="small" aria-label="統計週期">
            <el-radio-button value="week">每週（最近 8 週）</el-radio-button>
            <el-radio-button value="month">每月（最近 6 個月）</el-radio-button>
          </el-radio-group>
        </div>
      </template>
      <div v-if="conversionState === 'loading'" v-loading="true" class="dashboard__loading dashboard__loading--small" />
      <el-alert v-else-if="conversionState === 'error'" title="轉換概況載入失敗，請稍後再試。" type="error" show-icon :closable="false">
        <el-button size="small" @click="loadConversion">重新載入</el-button>
      </el-alert>
      <template v-else-if="conversion">
        <div class="dashboard__muted dashboard__range">統計區間：{{ periodRangeText }}</div>
        <el-row :gutter="12" class="dashboard__totals">
          <el-col v-for="s in visibleSeries" :key="s.key" :xs="12" :sm="8" :lg="4">
            <div class="dashboard__total">
              <span class="dashboard__swatch" :class="SERIES_CLASS[s.key]" aria-hidden="true" />
              <div class="dashboard__stat-label">{{ s.label }}</div>
              <div class="dashboard__total-value">{{ bucketValue(conversion.totals, s.key) }}</div>
            </div>
          </el-col>
        </el-row>
        <el-empty v-if="visibleSeries.length === 0" description="你的帳號沒有可顯示的轉換項目" :image-size="48" />
        <div v-else class="dash-chart" role="img" :aria-label="`${period === 'week' ? '每週' : '每月'}轉換概況長條圖`">
          <div v-for="(bucket, idx) in conversion.buckets" :key="bucket.start ?? idx" class="dash-chart__group">
            <div class="dash-chart__bars">
              <div
                v-for="s in visibleSeries"
                :key="s.key"
                class="dash-bar"
                :class="SERIES_CLASS[s.key]"
                :style="{ height: barHeight(bucketValue(bucket, s.key)) }"
                :title="`${bucketLabel(bucket)} ${s.label}：${bucketValue(bucket, s.key)}`"
              />
            </div>
            <div class="dash-chart__label">{{ bucketLabel(bucket) }}</div>
          </div>
        </div>
        <div v-if="conversion.forms.length" class="dashboard__forms">
          <div class="dashboard__sub-title">各表單送出次數</div>
          <el-table :data="conversion.forms" size="small" border>
            <el-table-column prop="formName" label="表單" min-width="160" />
            <el-table-column prop="count" label="送出次數" width="120" align="right" />
          </el-table>
        </div>
      </template>
    </el-card>

    <!-- 流量概況：目前未串接，清楚說明而不是顯示 0 -->
    <el-card v-if="trafficState !== 'hidden' && mainState !== 'forbidden'" shadow="never" header="流量概況" class="dashboard__wide">
      <div v-if="trafficState === 'loading'" v-loading="true" class="dashboard__loading dashboard__loading--small" />
      <el-alert v-else-if="trafficState === 'error'" title="流量概況載入失敗，請稍後再試。" type="error" show-icon :closable="false">
        <el-button size="small" @click="loadTraffic">重新載入</el-button>
      </el-alert>
      <template v-else-if="traffic">
        <el-empty v-if="!traffic.configured" description="流量統計尚未串接" :image-size="48">
          <p class="dashboard__muted dashboard__traffic-note">{{ traffic.message }}</p>
        </el-empty>
        <el-alert v-else-if="!traffic.overview" :title="traffic.message" type="warning" show-icon :closable="false" />
        <template v-else>
          <div class="dashboard__muted dashboard__range">統計區間：{{ traffic.from }} 至 {{ traffic.to }}</div>
          <el-row :gutter="16">
            <el-col :xs="12" :sm="6">
              <div class="dashboard__stat-label">瀏覽量</div>
              <div class="dashboard__total-value">{{ traffic.overview.pageViews }}</div>
            </el-col>
            <el-col :xs="12" :sm="6">
              <div class="dashboard__stat-label">造訪次數</div>
              <div class="dashboard__total-value">{{ traffic.overview.sessions }}</div>
            </el-col>
          </el-row>
          <el-row :gutter="16" class="dashboard__row">
            <el-col :xs="24" :md="12">
              <div class="dashboard__sub-title">熱門頁面</div>
              <el-table :data="traffic.overview.topPages" size="small" border>
                <el-table-column prop="path" label="頁面" min-width="160" />
                <el-table-column prop="views" label="瀏覽量" width="100" align="right" />
              </el-table>
            </el-col>
            <el-col :xs="24" :md="12" class="dashboard__col-spacing">
              <div class="dashboard__sub-title">來源分佈</div>
              <el-table :data="traffic.overview.sources" size="small" border>
                <el-table-column prop="source" label="來源" min-width="120" />
                <el-table-column prop="sessions" label="造訪次數" width="110" align="right" />
              </el-table>
            </el-col>
          </el-row>
        </template>
      </template>
    </el-card>

    <div v-if="dashboard" class="dashboard__muted dashboard__generated">資料更新時間：{{ formatDateTime(dashboard.generatedAt) }}</div>
  </div>
</template>

<style scoped>
.dashboard__loading { min-height: 160px; }
.dashboard__loading--small { min-height: 80px; }

.dashboard__stat-card { margin-bottom: 16px; }
.dashboard__stat-label { color: var(--el-text-color-secondary); font-size: 13px; }
.dashboard__stat-value { font-size: 28px; font-weight: 600; margin-top: 4px; }
.dashboard__stat-value--warning { color: var(--el-color-warning); }
.dashboard__mini-list { list-style: none; margin: 8px 0 0; padding: 0; font-size: 12px; color: var(--el-text-color-secondary); }
.dashboard__muted { color: var(--el-text-color-secondary); font-size: 12px; }

.dashboard__row { margin-top: 4px; }
.dashboard__col-spacing { margin-top: 16px; }
@media (min-width: 1200px) { .dashboard__col-spacing { margin-top: 0; } }

.dashboard__card { margin-bottom: 16px; }
.dashboard__wide { margin-top: 16px; }
.dashboard__card-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }

.dashboard__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; }
.dashboard__list-link { display: flex; align-items: center; justify-content: space-between; cursor: pointer; color: var(--el-text-color-primary); gap: 8px; }
.dashboard__list-link:hover, .dashboard__sub-list a:hover, .dashboard__event-title:hover, .dashboard__ranked a:hover { color: var(--el-color-primary); }
.dashboard__hint { margin-top: 2px; }
.dashboard__sub-list { list-style: none; margin: 6px 0 0; padding: 0 0 0 12px; font-size: 13px; display: flex; flex-direction: column; gap: 4px; }
.dashboard__sub-list a, .dashboard__event-title, .dashboard__ranked a { cursor: pointer; color: var(--el-text-color-regular); overflow-wrap: anywhere; }
.dashboard__event-date { color: var(--el-text-color-secondary); margin-right: 8px; font-size: 12px; }
.dashboard__upcoming-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.dashboard__warnings { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 4px; }
.dashboard__quick-entries { display: flex; flex-wrap: wrap; gap: 8px; }
.dashboard__card--quick .el-button { margin-left: 0; }

.dashboard__sub-title { font-size: 13px; font-weight: 600; margin: 12px 0 6px; }
.dashboard__sub-title:first-child { margin-top: 0; }
.dashboard__ranked { margin: 0; padding-left: 20px; display: flex; flex-direction: column; gap: 6px; font-size: 13px; }
.dashboard__ranked--plain { list-style: none; padding-left: 0; }
.dashboard__ranked li { display: flex; flex-direction: column; }

.dashboard__range { margin-bottom: 8px; }
.dashboard__totals { margin-bottom: 12px; }
.dashboard__total { position: relative; padding: 8px 8px 8px 16px; margin-bottom: 8px; }
.dashboard__total-value { font-size: 22px; font-weight: 600; }
.dashboard__swatch { position: absolute; left: 0; top: 10px; bottom: 10px; width: 4px; border-radius: 2px; }
.dashboard__forms { margin-top: 16px; }
.dashboard__traffic-note { max-width: 520px; }
.dashboard__generated { margin-top: 16px; text-align: right; }

/* 長條圖：每個區間一組、每個序列一根（只畫有權限的序列）；數字另有上方合計與 hover 提示。 */
.dash-chart { display: flex; align-items: stretch; gap: 6px; height: 180px; overflow-x: auto; padding-bottom: 4px; }
.dash-chart__group { flex: 1 0 44px; min-width: 44px; display: flex; flex-direction: column; }
.dash-chart__bars { flex: 1; display: flex; align-items: flex-end; justify-content: center; gap: 2px; border-bottom: 1px solid var(--admin-border); }
.dash-chart__label { text-align: center; font-size: 11px; color: var(--el-text-color-secondary); margin-top: 4px; white-space: nowrap; }
.dash-bar { width: 6px; min-height: 0; border-radius: 2px 2px 0 0; }
.dash-bar--a, .dashboard__swatch.dash-bar--a { background: var(--el-color-primary); }
.dash-bar--b, .dashboard__swatch.dash-bar--b { background: var(--el-color-success); }
.dash-bar--c, .dashboard__swatch.dash-bar--c { background: var(--el-color-warning); }
.dash-bar--d, .dashboard__swatch.dash-bar--d { background: var(--el-color-info); }
.dash-bar--e, .dashboard__swatch.dash-bar--e { background: var(--el-color-danger); }
.dash-bar--f, .dashboard__swatch.dash-bar--f { background: var(--el-text-color-secondary); }
</style>
