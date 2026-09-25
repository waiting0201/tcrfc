<script setup lang="ts">
// app/pages/zh/schedule.vue — 由 site/src/pages/zh/schedule/index.html 轉來
// （S0-9 資料驅動頁搬遷，本次規模最大的一頁，251 行 client script）。
//
// 🔴 SSR 打真實賽程 API（21 場，與 mockup 原本讀 site/src/data/schedule.json
// 的 21 場一一對應，日期／輪次／對手／場地／主客場逐筆核對一致）。
// 已知落差（回報用，見各自檔頭／行內註解）：
//   - status-pill 的 finished（已結束）已於 2026-09-23（S0-9j）用藍鯨真實資料
//     （21 場 status='played'）核對過；postponed／cancelled／live 三種仍未有真實
//     資料可核對，細節見 app/utils/schedule.ts 的 MATCH_STATUS_MAP 檔頭註解。
//   - fixture-card 的 id 屬性已改用 matches.match_no（v3.11 補進規格與 DDL 的聯賽官方
//     場次編號欄位）與 mockup 逐字元一致（fx-{日期}-{h|a}-{場次編號}），
//     原本「API 未吐出 match_no、id 退化成 fx-{日期}-{h|a}」的落差已消除。
//   - <head> 內的 SportsEvent JSON-LD（GEO-08）已改為動態產生（見檔尾 `sportsEvents`／
//     `useHead`），比照 mockup 原本 `@context`＋`@graph` 的原始 JSON-LD 寫法直接組字串，
//     刻意不透過 `useSchemaOrg`／`defineEvent`——後者是 schema-org-js 的通用 `Event`
//     定義器，沒有 `SportsEvent` 專用型別，硬塞 `@type: 'SportsEvent'` 進去能不能穿過
//     它的節點正規化未經查證，而 mockup 本來就是手刻原始 JSON-LD，直接複製這個已知
//     可行的做法風險最低。逐場檢查 `matchOn`／`kickoff`／`homeAway`／`opponent`／
//     `venue`／`competitionName` 六個欄位齊全才輸出該筆（GEO-05：資料不足時不輸出
//     該型別，不得用假值填滿必填欄位），全部場次都不齊全時整個 head 區塊不輸出，
//     不視為錯誤（藍鯨目前 21 場歷史賽果多數缺 `kickoff`／`homeAway`，正是這條路徑
//     的實際案例）。
//
// 互動行為（隊別分頁、賽程／賽果切換、賽事類型與主客場篩選、列表／月曆檢視、
// .ics 下載、分享連結、深層連結 #fx-... 定位）逐條照原 script 邏輯改寫，
// 月曆檢視沿用原本「JS 組字串塞 innerHTML」的做法（原本就是純 client 產生的內容，
// SSR 兩邊都是空 div，不影響 compare-dom，改用 Vue 樣板反而要多開一堆狀態
// 對應不到任何驗收收益）。
definePageMeta({ nav: 'schedule', unit: '13', bodyClass: 'page-schedule' })

const config = useRuntimeConfig()
const club = config.public.club

const { data } = await useFetch(`/api/backend/${club}/schedule`, { query: { pageSize: 200, lang: 'zh' } })
const matches = computed(() => data.value?.items ?? [])

interface MatchItem {
  id: string
  teamCode: string
  matchOn: string
  kickoff: string | null
  homeAway: string | null
  opponent: string | null
  venue: string | null
  competitionTag: string | null
  competitionName: string | null
  status: string | null
  roundNo: number | null
  matchNo: number | null
  /** 僅賽事狀態為「延賽」時有值，其餘一律 `null`（規劃書 v3.13 §3.13） */
  originalMatchOn: string | null
  originalKickoff: string | null
  /** GEO-05／S1-12c：後端已經用單一來源（apps/api Features/Seo/SchemaRequiredFields）算好
   * 這筆賽事夠不夠格輸出 SportsEvent Schema，這裡直接讀，不在前台重新判斷一次「六個欄位夠不夠」
   * （docs/18-work-errors.md E-39）。 */
  schemaEligible: boolean
}

const monthGroups = computed(() => {
  const map = new Map<string, MatchItem[]>()
  for (const m of matches.value as MatchItem[]) {
    const key = m.matchOn.slice(0, 7)
    if (!map.has(key)) map.set(key, [])
    map.get(key)!.push(m)
  }
  return Array.from(map.entries()).map(([key, items]) => ({ key, items }))
})

// ---- 篩選狀態 ----
const TEAM_TABS = [
  { id: 'all', filter: 'all', zh: '全部', en: null },
  { id: 'd1', filter: 'D1', zh: '一線隊', en: 'First Team' },
  { id: 'u15', filter: 'U15', zh: 'U15', en: null },
  { id: 'u14', filter: 'U14', zh: 'U14', en: null },
  { id: 'u12', filter: 'U12', zh: 'U12', en: null },
  { id: 'club', filter: 'club', zh: '俱樂部活動', en: null },
] as const

const teamLabels: Record<string, string> = {
  all: '全部隊別', D1: '一線隊 First Team', U15: 'U15 梯隊', U14: 'U14 梯隊', U12: 'U12 梯隊', club: '俱樂部活動',
}

const state = reactive({ team: 'all', mode: 'fixtures', comp: 'all', ha: 'all', view: 'list' })
const mounted = ref(false)
/** 月曆點擊某天或帶 #fx-... 造訪時，即使不符目前篩選也要強制顯示該場 */
const forcedVisibleId = ref<string | null>(null)

onMounted(() => {
  mounted.value = true
  handleInitialHash()
})

function cardStatusCode(m: MatchItem): string {
  return mapMatchStatus(m.status).code
}
function cardMatches(m: MatchItem): boolean {
  const id = fixtureId(m.matchOn, m.homeAway, m.matchNo)
  if (forcedVisibleId.value === id) return true
  const teamOk = state.team === 'all' ? true : m.teamCode === state.team
  const statusOk = state.mode === 'fixtures' ? cardStatusCode(m) === 'upcoming' : cardStatusCode(m) === 'finished'
  const compOk = state.comp === 'all' ? true : m.competitionTag === state.comp
  const haOk = state.ha === 'all' ? true : haCode(m.homeAway) === state.ha
  return teamOk && statusOk && compOk && haOk
}

// SSR／掛載前：全部顯示（比照 mockup 執行 JS 前的原始 HTML：team=all、
// mode=fixtures 時 21 場全是 upcoming，故本來就全部可見，不需要額外的
// 「掛載前一律顯示」特例，直接算 cardMatches 在預設狀態下就是全對）。
const visibleMatches = computed(() => (mounted.value ? matches.value.filter(cardMatches) : matches.value))
const visibleIds = computed(() => new Set(visibleMatches.value.map((m) => fixtureId(m.matchOn, m.homeAway, m.matchNo))))

function isCardHidden(m: MatchItem): boolean {
  return mounted.value && !visibleIds.value.has(fixtureId(m.matchOn, m.homeAway, m.matchNo))
}
function isGroupHidden(key: string): boolean {
  if (!mounted.value) return false
  return !visibleMatches.value.some((m) => m.matchOn.slice(0, 7) === key)
}

const teamHeadName = computed(() => teamLabels[state.team] ?? '全部隊別')
const teamHeadMeta = computed(() =>
  state.mode === 'results'
    ? '2026/27 賽季 · 企業甲級聯賽 · 賽果'
    : `2026/27 賽季 · 企業甲級聯賽 · 共 ${visibleMatches.value.length} 場`,
)
const isEmpty = computed(() => mounted.value && visibleMatches.value.length === 0)
const emptyDesc = computed(() => {
  if (state.mode === 'results') return '本季（2026/27）尚未有已完成的賽事，賽果會在比賽結束後更新。'
  if (state.team === 'club') return '目前尚無公告的俱樂部活動（記者會、簽名會、球迷見面會等），請持續關注官方社群與最新消息。'
  if (state.team === 'U15' || state.team === 'U14' || state.team === 'U12') {
    return `${teamLabels[state.team]}的賽程資料尚未提供，待客戶提供各梯隊賽程表後將更新於本頁。`
  }
  return '此隊別、賽事類型或主客場組合目前尚無排定賽事。'
})

// ---- 隊別分頁鍵盤導覽（比照 app/pages/zh/academy/teams.vue 的 roving tabindex）----
const tabRefs = ref<Record<string, HTMLElement | null>>({})
function selectTab(id: (typeof TEAM_TABS)[number]['id'], filter: string, focus = true) {
  state.team = filter
  forcedVisibleId.value = null
  if (focus) tabRefs.value[id]?.focus()
}
function onTabKeydown(e: KeyboardEvent, index: number) {
  let idx = index
  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') idx = (index + 1) % TEAM_TABS.length
  else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') idx = (index - 1 + TEAM_TABS.length) % TEAM_TABS.length
  else if (e.key === 'Home') idx = 0
  else if (e.key === 'End') idx = TEAM_TABS.length - 1
  else return
  e.preventDefault()
  const t = TEAM_TABS[idx]!
  selectTab(t.id, t.filter)
}

function setMode(mode: string) {
  state.mode = mode
  forcedVisibleId.value = null
}
function setView(view: string) {
  state.view = view
  if (view === 'calendar') nextTick(renderCalendar)
}

watch([() => state.comp, () => state.ha], () => {
  forcedVisibleId.value = null
})
watch(visibleMatches, () => {
  if (state.view === 'calendar') renderCalendar()
})

// ---- 月曆檢視（比照原 script：純字串組 innerHTML，client only）----
const calRoot = ref<HTMLElement | null>(null)

function renderCalendar() {
  const root = calRoot.value
  if (!root) return
  root.innerHTML = ''
  const visible = visibleMatches.value
  if (visible.length === 0) {
    root.innerHTML = '<p class="sched-empty__desc">此篩選條件下沒有可顯示於月曆的賽事。</p>'
    return
  }
  const byMonth = new Map<string, { day: number; id: string; opponent: string }[]>()
  for (const m of visible) {
    const key = m.matchOn.slice(0, 7)
    const day = Number.parseInt(m.matchOn.slice(8, 10), 10)
    if (!byMonth.has(key)) byMonth.set(key, [])
    byMonth.get(key)!.push({ day, id: fixtureId(m.matchOn, m.homeAway, m.matchNo), opponent: m.opponent ?? '' })
  }
  const DOW = ['日', '一', '二', '三', '四', '五', '六']
  for (const key of Array.from(byMonth.keys()).sort()) {
    const [yStr, mStr] = key.split('-')
    const y = Number(yStr)
    const mo = Number(mStr)
    const firstDow = new Date(Date.UTC(y, mo - 1, 1)).getUTCDay()
    const daysInMonth = new Date(Date.UTC(y, mo, 0)).getUTCDate()
    const matchesByDay = new Map(byMonth.get(key)!.map((ev) => [ev.day, ev]))
    let html = `<div class="cal-month"><p class="cal-month__title">${calMonthTitle(key)}</p><div class="cal-grid">`
    for (const w of DOW) html += `<span class="cal-dow">${w}</span>`
    for (let i = 0; i < firstDow; i++) html += '<span class="cal-day cal-day--pad"></span>'
    for (let d = 1; d <= daysInMonth; d++) {
      const ev = matchesByDay.get(d)
      html += ev
        ? `<span class="cal-day cal-day--match"><a href="#${ev.id}" data-cal-link data-target="${ev.id}" title="vs ${ev.opponent}">${d}</a></span>`
        : `<span class="cal-day">${d}</span>`
    }
    html += '</div></div>'
    root.insertAdjacentHTML('beforeend', html)
  }
  root.querySelectorAll<HTMLAnchorElement>('[data-cal-link]').forEach((a) => {
    a.addEventListener('click', (e) => {
      e.preventDefault()
      const targetId = a.getAttribute('data-target')
      if (!targetId) return
      jumpToMatch(targetId)
    })
  })
}

function jumpToMatch(id: string) {
  forcedVisibleId.value = id
  setView('list')
  nextTick(() => {
    const el = document.getElementById(id)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' })
      el.focus({ preventScroll: true })
    }
  })
}

function handleInitialHash() {
  if (location.hash && location.hash.indexOf('#fx-') === 0) {
    const id = location.hash.slice(1)
    forcedVisibleId.value = id
    setTimeout(() => {
      const el = document.getElementById(id)
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    }, 50)
  }
}

// ---- .ics 產生（單場 ＋ 目前檢視批次下載）----
function pad(n: number): string {
  return (n < 10 ? '0' : '') + n
}
function icsEscape(s: string): string {
  return s.replace(/[;,]/g, (c) => `\\${c}`)
}
function toUtcIcs(dateStr: string, timeStr: string): string {
  const [Y, M, D] = dateStr.split('-').map(Number)
  const [h, mi] = timeStr.split(':').map(Number)
  const local = new Date(Date.UTC(Y!, M! - 1, D!, h! - 8, mi!))
  return `${local.getUTCFullYear()}${pad(local.getUTCMonth() + 1)}${pad(local.getUTCDate())}T${pad(local.getUTCHours())}${pad(local.getUTCMinutes())}00Z`
}
function eventToVevent(m: MatchItem): string {
  const date = m.matchOn
  const kickoff = m.kickoff ?? '00:00'
  const opponent = m.opponent ?? ''
  const venue = m.venue ?? ''
  const ha = haCode(m.homeAway) === 'home' ? '主場' : '客場'
  const start = toUtcIcs(date, kickoff)
  const startDate = new Date(`${date}T${kickoff}:00+08:00`)
  const endDate = new Date(startDate.getTime() + 2 * 60 * 60 * 1000)
  const end = `${endDate.getUTCFullYear()}${pad(endDate.getUTCMonth() + 1)}${pad(endDate.getUTCDate())}T${pad(endDate.getUTCHours())}${pad(endDate.getUTCMinutes())}00Z`
  const title = `台中磐石 vs ${opponent}（企業甲級聯賽・${ha}）`
  const loc = venue === 'TBC' ? '場地未定' : venue
  return [
    'BEGIN:VEVENT',
    `UID:${fixtureId(m.matchOn, m.homeAway, m.matchNo)}@tcrfc.tw`,
    `DTSTAMP:${start}`,
    `DTSTART:${start}`,
    `DTEND:${end}`,
    `SUMMARY:${icsEscape(title)}`,
    `LOCATION:${icsEscape(loc)}`,
    `DESCRIPTION:${icsEscape('賽程可能異動，請以台中磐石足球俱樂部官方公告為準。')}`,
    'END:VEVENT',
  ].join('\r\n')
}
function downloadIcs(filename: string, vevents: string[]) {
  const body = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//TCRFC//Schedule//ZH', 'CALSCALE:GREGORIAN']
    .concat(vevents)
    .concat(['END:VCALENDAR'])
    .join('\r\n')
  const blob = new Blob([body], { type: 'text/calendar;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
function onIcsClick(m: MatchItem) {
  downloadIcs(`tcrfc-${m.matchOn}-vs-${m.opponent}.ics`, [eventToVevent(m)])
}

const copiedId = ref<string | null>(null)
function onShareClick(m: MatchItem) {
  const id = fixtureId(m.matchOn, m.homeAway, m.matchNo)
  const shareUrl = `${location.origin}${location.pathname}#${id}`
  if (navigator.clipboard?.writeText) {
    navigator.clipboard.writeText(shareUrl).then(() => {
      copiedId.value = id
      setTimeout(() => {
        if (copiedId.value === id) copiedId.value = null
      }, 2000)
    })
  } else {
    window.prompt('複製此賽事連結：', shareUrl)
  }
}

function onBulkIcs() {
  const visible = visibleMatches.value
  if (visible.length === 0) {
    window.alert('目前檢視沒有可下載的賽事。')
    return
  }
  downloadIcs(`tcrfc-schedule-${state.team}-${state.mode}.ics`, visible.map(eventToVevent))
}

useSeoMeta({
  title: '賽事行事曆 Schedule｜台中磐石足球俱樂部',
  description: '台中磐石足球俱樂部完整賽事行事曆：2026/27 企業甲級聯賽 21 場賽程，依隊別（一線隊／U15／U14／U12）分類，支援賽程賽果切換、月曆檢視與單場加入行事曆。',
})

// SportsEvent JSON-LD（GEO-08）。siteConfig.url 是 nuxt-site-config 的 priority-stack
// 解出的值，S0-9b 已實測 NUXT_PUBLIC_SITE_URL 能在 runtime 正確覆寫（docs/13 §6 紀律 4）；
// 這裡直接沿用同一個結論，兩站各自跑出自己網域的絕對網址，不寫死 tcrfc.tw。
const siteConfig = useSiteConfig()
const selfTeamName = computed(() => getClubAssets(club).nameZh)

// SportsEvent JSON-LD（GEO-08）的 status → schema.org 對照已收斂進
// app/utils/schedule.ts 的 matchStatusSchemaOrg()（S0-9j）。此頁與畫面
// status-pill（mapMatchStatus()）共用同一份 MATCH_STATUS_MAP，不再各自維護一份——
// 舊版本頁曾經在這裡自己開一份 EVENT_STATUS_MAP，鍵值對到 'played'，
// 但 mapMatchStatus() 當時的 switch 對到的是 'finished'，兩份表各寫各的、
// 沒有任何機制互相對照，才會讓藍鯨 21 場已完成賽事在畫面上顯示成「未開始」。

const sportsEvents = computed(() => {
  const nodes: Record<string, unknown>[] = []
  for (const m of matches.value as MatchItem[]) {
    // GEO-05（S1-12c）：改讀後端算好的 schemaEligible，不再自己重新判斷一次「這六個欄位
    // 夠不夠」——必填欄位清單只在 apps/api 的 SchemaRequiredFields 宣告一次（E-39）。
    if (!m.schemaEligible) continue
    const isHome = m.homeAway === 'HOME'
    const selfTeam = { '@type': 'SportsTeam', name: selfTeamName.value }
    const oppTeam = { '@type': 'SportsTeam', name: m.opponent }
    const homeTeam = isHome ? selfTeam : oppTeam
    const awayTeam = isHome ? oppTeam : selfTeam
    const roundLabel = m.roundNo ? `第${m.roundNo}輪：` : ''
    nodes.push({
      '@type': 'SportsEvent',
      name: `${m.competitionName} ${roundLabel}${homeTeam.name} vs ${awayTeam.name}`,
      startDate: `${m.matchOn}T${m.kickoff}:00+08:00`,
      eventStatus: matchStatusSchemaOrg(m.status),
      eventAttendanceMode: 'https://schema.org/OfflineEventAttendanceMode',
      sport: 'Soccer',
      location: {
        '@type': 'Place',
        name: m.venue,
        address: { '@type': 'PostalAddress', addressCountry: 'TW' },
      },
      homeTeam,
      awayTeam,
      competitor: [selfTeam, oppTeam],
      url: `${siteConfig.url}/zh/schedule/#${fixtureId(m.matchOn, m.homeAway, m.matchNo)}`,
    })
  }
  return nodes
})

// 全部場次都不齊全時（GEO-05）回傳空物件，不輸出任何 <script> 標籤，不是輸出一個空
// 的 @graph 陣列——「不輸出殘缺 Schema」也涵蓋「不輸出一個沒有任何節點的殼」。
useHead(() => (
  sportsEvents.value.length > 0
    ? {
        script: [{
          key: 'schedule-sports-events',
          type: 'application/ld+json',
          innerHTML: JSON.stringify({ '@context': 'https://schema.org', '@graph': sportsEvents.value }),
        }],
      }
    : {}
))
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li aria-current="page">賽事行事曆</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">13</span>
  <div class="container">
    <p class="page-hero__eyebrow">13 Schedule</p>
    <h1>賽事行事曆<span class="en">Schedule</span></h1>
    <p class="page-hero__lede">一線隊與各梯隊的完整賽程與賽果，一頁掌握。所有時間皆依<strong>瀏覽器所在時區</strong>顯示，<strong>時間可能異動，正式時間請以官方公告為準</strong>。</p>
  </div>
</section>

<section class="band schedule-band" aria-labelledby="schedule-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="schedule-title">賽事行事曆</h2>

    <!-- 隊別分頁（第一層分類） -->
    <div class="team-tabs" data-team-tabs>
      <div class="team-tabs__list" role="tablist" aria-label="選擇隊別">
        <button
          v-for="(tab, i) in TEAM_TABS" :key="tab.id"
          :ref="(el) => (tabRefs[tab.id] = el as HTMLElement)"
          type="button" role="tab" :id="`tab-${tab.id}`" aria-controls="panel-sched"
          :aria-selected="state.team === tab.filter" class="team-tabs__tab"
          :tabindex="state.team === tab.filter ? undefined : -1"
          :data-team-filter="tab.filter"
          @click="selectTab(tab.id, tab.filter, false)" @keydown="onTabKeydown($event, i)"
        >{{ tab.zh }}<span v-if="tab.en" class="en"> {{ tab.en }}</span></button>
      </div>

      <div class="team-tabs__panel" id="panel-sched" role="tabpanel" aria-labelledby="tab-all" tabindex="0">

        <div class="sched-teamhead">
          <p class="sched-teamhead__name" data-teamhead-name>{{ teamHeadName }}</p>
          <p class="sched-teamhead__meta" data-teamhead-meta>{{ teamHeadMeta }}</p>
        </div>

        <!-- 次層控制列 -->
        <div class="sched-controls">
          <div class="sched-controls__group" role="group" aria-label="賽程或賽果">
            <button type="button" class="sched-toggle" data-mode="fixtures" :aria-pressed="state.mode === 'fixtures'" @click="setMode('fixtures')">賽程 <span class="en">Fixtures</span></button>
            <button type="button" class="sched-toggle" data-mode="results" :aria-pressed="state.mode === 'results'" @click="setMode('results')">賽果 <span class="en">Results</span></button>
          </div>

          <div class="sched-controls__selects">
            <label class="sched-select">
              <span class="visually-hidden">賽季</span>
              <select data-season disabled>
                <option>2026/27 賽季</option>
              </select>
            </label>
            <label class="sched-select">
              <span class="visually-hidden">賽事類型</span>
              <select data-comp-filter v-model="state.comp">
                <option value="all">賽事類型：全部</option>
                <option value="league">聯賽</option>
                <option value="cup">盃賽</option>
                <option value="friendly">友誼賽</option>
                <option value="other">其他</option>
              </select>
            </label>
            <label class="sched-select">
              <span class="visually-hidden">主客場</span>
              <select data-ha-filter v-model="state.ha">
                <option value="all">主客場：全部</option>
                <option value="home">主場</option>
                <option value="away">客場</option>
              </select>
            </label>
          </div>

          <div class="sched-controls__group" role="group" aria-label="檢視方式">
            <button type="button" class="sched-toggle" data-view="list" :aria-pressed="state.view === 'list'" @click="setView('list')">列表 <span class="en">List</span></button>
            <button type="button" class="sched-toggle" data-view="calendar" :aria-pressed="state.view === 'calendar'" @click="setView('calendar')">月曆 <span class="en">Calendar</span></button>
          </div>
        </div>

        <p class="sched-official-note">賽程如有異動，一律以俱樂部官方公告與企業甲級聯賽主辦單位公告為準。</p>

        <!-- 列表檢視 -->
        <div class="sched-view" data-view-panel="list" :hidden="state.view !== 'list'">
          <div v-for="group in monthGroups" :key="group.key" class="month-group" :data-month="group.key" :hidden="isGroupHidden(group.key)">
            <h3 class="month-heading">{{ monthHeading(group.key) }}</h3>
            <div class="fixture-list">
              <article
                v-for="m in group.items" :key="m.id"
                :id="fixtureId(m.matchOn, m.homeAway, m.matchNo)" class="fixture-card"
                :data-team="m.teamCode" :data-ha="haCode(m.homeAway)" :data-comp="m.competitionTag"
                :data-status="mapMatchStatus(m.status).code" :data-date="m.matchOn" :data-kickoff="m.kickoff"
                :data-opponent="m.opponent" :data-venue="m.venue" :data-round="m.roundNo"
                :hidden="isCardHidden(m)"
              >
                <div class="fixture-card__time">
                  <span class="fixture-card__wd">{{ matchWeekday(m.matchOn).zh }} {{ matchWeekday(m.matchOn).en }}</span>
                  <span class="fixture-card__date">{{ matchDay(m.matchOn) }}</span>
                  <span class="fixture-card__mon">{{ matchMonthAbbr(m.matchOn) }}</span>
                  <time class="fixture-card__kickoff" :datetime="`${m.matchOn}T${m.kickoff}:00+08:00`">{{ m.kickoff }}</time>
                </div>
                <div class="fixture-card__body">
                  <div class="fixture-card__meta">
                    <span :class="['tag', `tag--${m.competitionTag}`]">{{ compTagLabel(m.competitionTag) }}</span>
                    <span class="fixture-card__round">第 {{ m.roundNo }} 輪</span>
                    <span :class="['status-pill', `status-pill--${mapMatchStatus(m.status).code}`]">{{ mapMatchStatus(m.status).label }}</span>
                  </div>
                  <p v-if="postponedNote(m.originalMatchOn, m.originalKickoff)" class="fixture-card__postponed">{{ postponedNote(m.originalMatchOn, m.originalKickoff) }}</p>
                  <div class="fixture-card__matchup">
                    <template v-if="haCode(m.homeAway) === 'away'">
                      <span class="fx-side fx-side--them">
                        <span class="fx-crest fx-crest--ph" aria-hidden="true">{{ (m.opponent ?? '').charAt(0) }}</span>
                        <span class="fx-name">{{ m.opponent }}</span>
                      </span>
                      <span class="fx-vs">VS</span>
                      <span class="fx-side fx-side--us">
                        <img class="fx-crest" src="/assets/brand/svg/tcrfc-mark-pink.svg" width="26" height="28" alt="">
                        <span class="fx-name">台中磐石</span>
                      </span>
                    </template>
                    <template v-else>
                      <span class="fx-side fx-side--us">
                        <img class="fx-crest" src="/assets/brand/svg/tcrfc-mark-pink.svg" width="26" height="28" alt="">
                        <span class="fx-name">台中磐石</span>
                      </span>
                      <span class="fx-vs">VS</span>
                      <span class="fx-side fx-side--them">
                        <span class="fx-crest fx-crest--ph" aria-hidden="true">{{ (m.opponent ?? '').charAt(0) }}</span>
                        <span class="fx-name">{{ m.opponent }}</span>
                      </span>
                    </template>
                  </div>
                  <p class="fixture-card__venue">
                    <span :class="['ha-pill', haCode(m.homeAway) === 'home' ? 'ha-pill--home' : 'ha-pill--away']">{{ haCode(m.homeAway) === 'home' ? '主場 HOME' : '客場 AWAY' }}</span>
                    <template v-if="m.venue === 'TBC'">
                      <span class="tbc-note">場地未定 · VENUE TBC</span>
                    </template>
                    <template v-else>
                      <span>{{ m.venue }}</span><a class="fixture-card__map" :href="venueMapUrl(m.venue ?? '')" target="_blank" rel="noopener">地圖<span class="visually-hidden">（另開新視窗）</span></a>
                    </template>
                  </p>
                </div>
                <div class="fixture-card__actions">
                  <button type="button" class="btn btn--primary btn--sm" data-ics-btn @click="onIcsClick(m)">加入行事曆 <span class="en">.ics</span></button>
                  <button type="button" class="btn btn--dark btn--sm" data-share-btn @click="onShareClick(m)">{{ copiedId === fixtureId(m.matchOn, m.homeAway, m.matchNo) ? '連結已複製' : '分享此賽事' }}</button>
                </div>
              </article>
            </div>
          </div>

          <div class="sched-empty" data-empty-state :hidden="!isEmpty">
            <p class="sched-empty__title">目前沒有符合條件的賽事</p>
            <p class="sched-empty__desc" data-empty-desc>{{ emptyDesc }}</p>
          </div>
        </div>

        <!-- 月曆檢視 -->
        <div class="sched-view" data-view-panel="calendar" :hidden="state.view !== 'calendar'">
          <div ref="calRoot" class="sched-calendar" data-calendar-root aria-live="polite"></div>
        </div>

      </div>
    </div>

    <!-- 訂閱 -->
    <div class="sched-subscribe">
      <div class="sched-subscribe__copy">
        <h2 class="section-title" style="color:var(--heading);">訂閱賽程</h2>
        <p>下載目前篩選結果的完整賽程 <span class="en">.ics</span> 檔，匯入 Google 日曆、Apple 行事曆或 Outlook；也可以在任一場賽事卡片上按「加入行事曆」單獨下載該場比賽。</p>
        <button type="button" class="btn btn--dark" data-ics-bulk-btn @click="onBulkIcs">下載目前檢視賽程 <span class="en">.ics</span></button>
        <p class="sched-subscribe__fine">依隊別自動更新的 <span class="en">webcal://</span> 訂閱網址（訂閱後賽程異動會自動同步至個人行事曆）需要後台持續產生動態行事曆檔案，屬於後續系統開發項目，目前尚未上線；現在請使用上方 <span class="en">.ics</span> 下載功能取得賽程。</p>
      </div>
    </div>

  </div>
</section>

<section class="band grain cta-band" aria-labelledby="sched-cta-title">
  <span class="ghost-num" aria-hidden="true">13</span>
  <div class="band-inner container">
    <h2 class="section-title" id="sched-cta-title">相關連結</h2>
    <div class="cta-grid">
      <a class="cta-card" href="/zh/club/first-team/">
        <span class="cta-card__num">3.1</span>
        <span class="cta-card__title">一線隊 First Team</span>
        <p class="cta-card__desc">認識球員名單、教練團與成績積分榜</p>
      </a>
      <a class="cta-card" href="/zh/academy/teams/">
        <span class="cta-card__num">4.2</span>
        <span class="cta-card__title">學院隊伍</span>
        <p class="cta-card__desc">U15／U14／U12 梯隊介紹</p>
      </a>
      <a class="cta-card" href="/zh/join/general/">
        <span class="cta-card__num">10.7</span>
        <span class="cta-card__title">聯絡我們</span>
        <p class="cta-card__desc">媒體、球迷或家長的賽程相關詢問</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* ── 13 SCHEDULE 專屬元件：賽事卡片 fixture-card、隊別頁籤延伸、月曆檢視 ──
   fixture-card／status-pill／tag／ha-pill 為賽事資料的核心呈現元件，
   3.1 一線隊賽程、4.2 各梯隊賽程、首頁近期賽事嵌入都會需要，
   建議連同 .team-tabs（已於 4.2 使用）一併收進共用 tcrfc.css（見 AGENT_BRIEF §6）。 */

.schedule-band{ padding-block:clamp(3rem,5vw,5rem); }

.sched-teamhead{ display:flex; align-items:baseline; gap:1rem; flex-wrap:wrap; margin-bottom:1.75rem; }
.sched-teamhead__name{ font-size:1.3rem; font-weight:900; color:var(--heading); }
.sched-teamhead__meta{ font-size:.85rem; color:var(--muted); }

.sched-controls{
  display:flex; align-items:center; justify-content:space-between; gap:1.25rem; flex-wrap:wrap;
  padding:1rem 1.25rem; background:var(--paper-2); border:1px solid var(--rule); margin-bottom:1rem;
}
.sched-controls__group{ display:inline-flex; border:1px solid var(--rule); background:var(--paper); }
.sched-controls__selects{ display:flex; gap:.75rem; flex-wrap:wrap; }
.sched-toggle{
  padding:.6rem 1.1rem; font-size:.82rem; font-weight:700; color:var(--muted); background:transparent;
}
.sched-toggle[aria-pressed="true"]{ background:var(--ink); color:#fff; }
.sched-select select{
  border:1px solid var(--rule); background:var(--paper); padding:.6rem .8rem; font-size:.82rem;
  font-family:inherit; color:var(--text); min-height:44px;
}
.sched-select select:disabled{ color:var(--muted); background:var(--paper-2); }

.sched-official-note{ font-size:.8rem; color:var(--muted); margin-bottom:2rem; }
.sched-official-note::before{ content:"※ "; color:var(--brand-aa); font-weight:800; }

.sched-view[hidden]{ display:none; }

.month-group{ margin-bottom:2.5rem; }
.month-group[hidden]{ display:none; }
.month-heading{
  font-size:.85rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase;
  color:var(--brand-aa); padding-bottom:.6rem; border-bottom:2px solid var(--rule); margin-bottom:1.25rem;
}
.fixture-list{ display:flex; flex-direction:column; gap:1rem; }

.fixture-card{
  display:grid; grid-template-columns:5.5rem 1fr auto; gap:1.5rem; align-items:center;
  padding:1.25rem 1.5rem; background:var(--paper); border:1px solid var(--rule);
  scroll-margin-top:6rem;
}
.fixture-card:target{ border-color:var(--brand-aa); box-shadow:inset 3px 0 0 var(--brand-aa); }
.fixture-card[hidden]{ display:none; }

.fixture-card__time{ display:flex; flex-direction:column; align-items:center; text-align:center; gap:.1rem; border-right:1px solid var(--rule); padding-right:1.5rem; }
.fixture-card__wd{ font-size:.68rem; font-weight:800; color:var(--muted); letter-spacing:.04em; }
.fixture-card__date{ font-size:2rem; font-weight:900; line-height:1; color:var(--heading); }
.fixture-card__mon{ font-size:.68rem; font-weight:800; color:var(--muted); letter-spacing:.06em; }
.fixture-card__kickoff{ display:block; margin-top:.35rem; font-size:.85rem; font-weight:800; color:var(--brand-aa); font-variant-numeric:tabular-nums; }

.fixture-card__meta{ display:flex; align-items:center; gap:.75rem; flex-wrap:wrap; margin-bottom:.75rem; }
.tag{ display:inline-flex; align-items:center; font-size:.68rem; font-weight:800; letter-spacing:.05em; text-transform:uppercase; padding:.3rem .6rem; color:#fff; }
.tag--league{ background:var(--brand-aa); }
.tag--cup{ background:var(--ink); }
.tag--friendly{ background:var(--muted); }
.fixture-card__round{ font-size:.78rem; color:var(--muted); font-weight:700; }
.status-pill{ font-size:.68rem; font-weight:800; letter-spacing:.05em; text-transform:uppercase; padding:.3rem .6rem; border:1px solid var(--rule); color:var(--muted); margin-left:auto; }
.status-pill--upcoming{ border-color:var(--rule); color:var(--muted); }
.status-pill--live{ border-color:var(--brand-aa); color:var(--brand-aa); }
.status-pill--finished{ border-color:var(--ink); color:var(--ink); }
.status-pill--postponed, .status-pill--cancelled{ border-color:var(--brand-deep); color:var(--brand-deep); }
.fixture-card__postponed{ font-size:.78rem; font-weight:700; color:var(--brand-deep); margin:-.35rem 0 .65rem; }

.fixture-card__matchup{ display:flex; align-items:center; gap:1rem; margin-bottom:.65rem; }
.fx-side{ display:flex; align-items:center; gap:.55rem; flex:1 1 0; min-width:0; }
.fx-side--them{ flex-direction:row-reverse; text-align:right; }
.fx-crest{ flex:none; width:26px; height:28px; object-fit:contain; }
.fx-crest--ph{
  width:26px; height:26px; display:inline-flex; align-items:center; justify-content:center;
  background:var(--paper-2); border:1px solid var(--rule); font-size:.72rem; font-weight:800; color:var(--muted);
}
.fx-name{ font-weight:800; font-size:.95rem; color:var(--heading); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.fx-side--us .fx-name{ color:var(--brand-deep); }
.fx-vs{ flex:none; font-size:.72rem; font-weight:800; color:var(--ghost); }

.fixture-card__venue{ display:flex; align-items:center; gap:.6rem; font-size:.85rem; color:var(--text); flex-wrap:wrap; }
.ha-pill{ font-size:.65rem; font-weight:800; letter-spacing:.04em; padding:.2rem .5rem; flex:none; }
.ha-pill--home{ background:rgba(224,33,138,.1); color:var(--brand-aa); }
.ha-pill--away{ background:var(--paper-2); color:var(--muted); border:1px solid var(--rule); }
.fixture-card__map{ font-size:.78rem; font-weight:700; color:var(--brand-aa); text-decoration:underline; }
.tbc-note{ font-size:.82rem; font-weight:700; color:var(--brand-deep); font-style:italic; }

.fixture-card__actions{ display:flex; flex-direction:column; gap:.5rem; }

@media (max-width:860px){
  .fixture-card{ grid-template-columns:1fr; }
  .fixture-card__time{ flex-direction:row; justify-content:flex-start; border-right:0; border-bottom:1px solid var(--rule); padding-right:0; padding-bottom:.85rem; margin-bottom:.85rem; gap:.6rem; }
  .fixture-card__date{ font-size:1.4rem; }
  .fixture-card__actions{ flex-direction:row; }
}

.sched-empty{ display:none; text-align:center; padding:4rem 1.5rem; border:1px dashed var(--rule); background:var(--paper-2); }
.sched-empty[hidden]{ display:none; }
.sched-empty:not([hidden]){ display:block; }
.sched-empty__title{ font-weight:800; color:var(--heading); margin-bottom:.5rem; }
.sched-empty__desc{ font-size:.88rem; color:var(--muted); }

/* 月曆檢視 */
.sched-calendar{ display:grid; grid-template-columns:repeat(auto-fill,minmax(280px,1fr)); gap:1.5rem; }
.cal-month{ border:1px solid var(--rule); padding:1.25rem; }
.cal-month__title{ font-size:.85rem; font-weight:800; letter-spacing:.04em; color:var(--heading); margin-bottom:.9rem; text-align:center; }
.cal-grid{ display:grid; grid-template-columns:repeat(7,1fr); gap:2px; text-align:center; }
.cal-dow{ font-size:.62rem; font-weight:800; color:var(--muted); padding-bottom:.4rem; }
.cal-day{ font-size:.78rem; padding:.45rem 0; color:var(--text); position:relative; }
.cal-day--pad{ visibility:hidden; }
.cal-day--match{ background:var(--paper-2); }
.cal-day--match a{ display:block; color:var(--brand-deep); font-weight:800; text-decoration:none; }
.cal-day--match a::after{ content:""; display:block; width:5px; height:5px; background:var(--brand-aa); margin:.2rem auto 0; border-radius:50%; }
.cal-day--match a:hover, .cal-day--match a:focus-visible{ text-decoration:underline; }

.sched-subscribe{ margin-top:3rem; padding:clamp(1.75rem,4vw,2.5rem); background:var(--paper-2); border:1px solid var(--rule); max-width:62ch; }
.sched-subscribe h2{ margin-bottom:.75rem; }
.sched-subscribe p{ font-size:.92rem; line-height:1.75; color:var(--text); margin-bottom:1.25rem; }
.sched-subscribe__fine{ font-size:.78rem; color:var(--muted); margin-top:1rem; margin-bottom:0; }
</style>
