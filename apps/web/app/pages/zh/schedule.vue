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
//
// 🔴 S1-19（2026-09-29）修正的既有落差（S1-12d 當時已記錄「留給 S1-19」）：
//   - **隊別分頁改依俱樂部動態產生**：磐石一線隊 D1 ＋既有的各梯隊代碼；藍鯨一線隊
//     `BW1` ＋既有的藍鯨梯隊代碼（比磐石少一個年齡層，squadCodes 已反映這個事實，
//     不在本頁重複列出，避免又是另一份會過期的字面值清單）——原本 TEAM_TABS 是寫死
//     磐石那組固定代碼的常數陣列，bw 容器會拿磐石的隊別代碼去查藍鯨的賽事，永遠查不到
//     （`BW1` 不是第二個 `D1`，docs/14 踩雷點）。梯隊代碼沿用 `getAcademyTeamTabs()`
//     （4.2 學院隊伍頁既有的單一來源，其底層事實來自 GEO-03 的 `squadCodes`），一線隊
//     代碼沿用新增的 `getFirstTeamCode()`（見 shared/utils/club.ts），不在本頁重新
//     維護一份代碼清單。
//   - **fixture-card 「我方」一側原本整段字面寫死「台中磐石」與磐石隊徽 SVG**，藍鯨容器
//     會顯示磐石的名稱與隊徽——已改讀 `getClubAssets(club)`。
//   - **`.ics` 內容與逸出規則**：`DESCRIPTION` 原本字面寫死「台中磐石足球俱樂部官方公告」，
//     已改為俱樂部名稱動態組字；逸出函式原本只處理分號與逗號，未處理反斜線本身與換行——
//     反斜線沒有優先逸出時，後續新增的逸出反斜線會被自己的規則二次跳脫，且欄位若含真實
//     換行字元（例如場地或對手名稱有多行）會直接破壞 `.ics` 檔案結構（下一行被誤判為新的
//     屬性行），是可被資料內容觸發的注入風險。已比照 `apps/api/Common/IcsBuilder.cs` 的
//     `Escape()` 補齊反斜線／換行逸出（順序：反斜線最先），並補上同檔案的 75 位元組行折疊
//     （`FoldLine()`），兩邊各自實作但規則與涵蓋字元一致。
//   - **月曆檢視同一天有兩場賽事時，原本的 `Map<日期, 單一事件>` 會讓第二場悄悄從月曆消失**
//     （只保留 `Array.map` 的最後一筆）；改為 `Map<日期, 事件陣列>`，同一天多場賽事全部
//     可達（點擊日期即「展開當日賽事」，規劃書 v3.13 §3.13 原文用語）。
//   - **月曆日期連結補上 `aria-label`**（含日期、對手、「查看詳情」），原本只有滑鼠 `title`
//     屬性（螢幕閱讀器不保證讀出），且月曆是用字串組 `innerHTML`，`ev.opponent`（後台可
//     自由輸入的文字欄位）原本未經 HTML 逸出就直接插入屬性值與文字節點，是可被資料內容
//     觸發的 HTML 注入風險——已加上 `escapeHtml()`。
//
// 🔴 S1-19 補完（2026-09-29，主 session 對照規劃書 §3.13 逐條複查後要求補齊）：
//   - **時區換算**：原本頁首文案宣稱「依瀏覽器所在時區顯示」，但賽事卡片實際顯示的是
//     資料庫存的台灣牆上時間字面值，從未真正換算——文案與行為不一致。已改為真的換算：
//     `app/utils/schedule.ts` 新增 `matchTimeDisplay()`／`instantTimeDisplay()`，SSR／
//     掛載前一律顯示台灣時間（與現行行為逐字元相同，不會有 hydration mismatch），掛載後
//     （`onMounted`）用 `Intl.DateTimeFormat().resolvedOptions().timeZone` 偵測瀏覽器
//     時區，不是 `Asia/Taipei` 時才用 `Intl` 換算更新顯示（這次更新在 hydration 完成後
//     才發生，是正常的反應式 DOM patch，不算 mismatch）。
//   - **俱樂部活動**：接上既有 `GET /api/v1/{club}/calendar/events?team=club`（本頁面
//     一直沒接的既有公開端點），「俱樂部活動」分頁與「全部」檢視皆會顯示；賽果模式不顯示
//     （後端 `ListClubEventsAsync` 檔頭明講「俱樂部活動沒有『賽果』的語意」）。**整合方式
//     的決定**：club events 用獨立的 `clubEvents`／`visibleClubEvents` 計算屬性與獨立的
//     卡片樣板（`club-event-card`），附加在既有賽事列表之後，不併入 `matches`／
//     `monthGroups`／`visibleMatches` 那條既有管線——後者牽動月曆檢視、批次 `.ics`、
//     `SportsEvent` JSON-LD、深層連結等大量既有邏輯，且 club events 目前完全沒有真實
//     種子資料，不併入可以用最小改動涵蓋規格要求，同時把已驗證過的既有賽事管線風險降到
//     最低。月曆檢視本輪未涵蓋俱樂部活動（只在列表檢視顯示），列為已知範圍縮減。
//   - **依隊別訂閱**：查證 `apps/api` 只有單場 `.ics` 下載端點
//     （`GET /api/v1/{club}/matches/{id}/ics`），沒有任何 webcal／訂閱 feed 端點
//     （`L4` 行事曆進階訂閱匯出排在 `S2-6`，尚未開發）——**前台不自行產生假的訂閱網址**，
//     選定隊別時頁首改為顯示「下一場賽事倒數」（真正可做的部分）與一句誠實的訂閱狀態說明
//     （對應既有「訂閱賽程」區塊的既有文案，不是新造的說詞）。
//   - **賽季篩選**：原本是 `disabled` 的裝飾用下拉選單，字面寫死「2026/27 賽季」——這對
//     藍鯨是錯的事實（藍鯨兩個球季代碼是「2023」「2025」，不是「2026/27」），且藍鯨
//     21 場歷史賽果其實橫跨兩個不同球季，原本的篩選功能對藍鯨完全不可用。已改為從
//     `matches` 既有回應的 `seasonCode` 欄位（`MatchDto.SeasonCode`，前台介面原本沒有
//     宣告這個既有欄位）動態算出可選賽季清單，預設選最新一季，真正可回溯往季，不需要
//     新增後端端點。
//   - **動作按鈕「賽事詳情」**：規劃書「賽事卡片欄位」表非條件式列出（「購票」「轉播
//     資訊」皆標「若有」，`MatchDto` 沒有票務／轉播欄位，維持不顯示，屬於資料不存在
//     而非漏做）。原本完全沒有這顆按鈕。查證站內沒有任何獨立的單場賽事詳情頁路由，
//     比照規劃書「前台功能」表「事件詳情｜側邊抽屜或彈窗」實作為原生 `<dialog>` 彈窗
//     （原生 focus trap、Escape 關閉、`::backdrop`，不需要手刻鍵盤陷阱）。
//   - **行動版次要篩選收合**：賽季／賽事類型／主客場三個下拉原本在窄螢幕會直接换行擠在
//     一起。新增「篩選」按鈕（`aria-expanded`），窄螢幕預設收合、點擊展開；桌面版不受
//     影響（純 CSS media query 控制預設可見度，`filtersOpen` 初始值 `false` 在 SSR／
//     掛載前 client 端第一次渲染皆相同，不影響 hydration）。
// S1-20：Event（俱樂部活動）與 SportsEvent 場地地址的純函式判斷抽到 shared/utils/，
// 需要明確 import（比照 app/composables/useSiteFacts.ts／useFaqPageSchema.ts 既有慣例，
// shared/utils/ 不像 app/utils/ 會被自動引入）。
import { venueAddressByName } from '#shared/utils/schema-batch2'
import { resolveScheduleSlug } from '#shared/utils/schedule-route'

definePageMeta({ nav: 'schedule', unit: '13', bodyClass: 'page-schedule', enReady: true })

const config = useRuntimeConfig()
const club = config.public.club
const clubKey = computed<'tcrfc' | 'bw'>(() => (club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const clubAssets = computed(() => getClubAssets(club))
/** 一線隊 `Team.code`：磐石 `D1`、藍鯨 `BW1`——單一來源見 shared/utils/club.ts。 */
const firstTeamCode = computed(() => getFirstTeamCode(club))

// S1-12d 收尾：聯賽名稱與梯隊代碼改讀 useSiteFacts(club)（後端公開端點），
// 不再是 shared/utils/site-facts.ts 的靜態快照——見 app/composables/useSiteFacts.ts 檔頭。
const { facts: clubFacts, academyLabel: clubAcademyLabel } = useSiteFacts(club)

// S1-13：lang 改跟隨目前路由語系（/zh/ 或 /en/），不再寫死 'zh'——apps/api 對
// ?lang=en 已有完整欄位回退機制（apps/api/README.md「已知落差」段的真實 curl 驗證），
// 前台只要把正確的語系傳過去即可，不需要在這裡自己做回退判斷。
// 英文版（主站 /en/）：isEn／tx 見 app/composables/useLocale.ts；zh 版輸出逐字不變。
const { locale, lp, isEn, tx } = useLocale()
const { data } = await useFetch(`/api/backend/${club}/schedule`, { query: { pageSize: 200, lang: locale.value } })
const matches = computed(() => data.value?.items ?? [])

// 俱樂部活動（規劃書 v3.13 §3.13「資料來源」第二列：📣 俱樂部活動 Club Event，行事曆
// 自建事件）——接上既有 `GET /api/v1/{club}/calendar/events?team=club`（本頁面 S0-9／
// S1-12d 時期建成時就存在的既有公開端點，但從未接上，見檔頭「S1-19 補完」說明）。
// 整合方式：獨立管線（`clubEvents`／`visibleClubEvents`），不併入 `matches`，理由同見檔頭。
const { data: clubEventsData } = await useFetch(`/api/backend/${club}/calendar/events`, {
  query: { team: 'club', pageSize: 100, lang: locale.value },
})

// SportsEvent JSON-LD（GEO-08）與 .ics UID 的網域皆用這裡的 `siteConfig.url`——S0-9b 已實測
// `NUXT_PUBLIC_SITE_URL` 能在 runtime 正確覆寫（docs/13 §6 紀律 4），兩站各自跑出自己網域的
// 絕對網址，不寫死 tcrfc.tw。提前宣告到這裡（原本宣告在檔案後段），供下方 `.ics` 產生函式使用。
const siteConfig = useSiteConfig()

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
  /** `MatchDto.SeasonCode`——API 一直都有回這個欄位，前台介面原本沒有宣告、也從未使用
   * （S1-19 補完：賽季篩選改讀這個既有欄位，見 `availableSeasons`）。 */
  seasonCode: string
}

/** 俱樂部活動卡片（`PublicCalendarEventDto` 的 `sourceType === 'custom'` 子集）。
 * `startsAt`／`endsAt` 是真正的 UTC 時間戳（非牆上時間字面值），與 `MatchItem` 的
 * `matchOn`／`kickoff` 是兩套不同語意，顯示換算各自呼叫 `app/utils/schedule.ts`
 * 對應的函式（`instantTimeDisplay` vs. `matchTimeDisplay`），不要互用。 */
interface ClubEventItem {
  id: string
  startsAt: string
  endsAt: string | null
  isAllDay: boolean
  title: string
  venueName: string | null
  eventTypeCode: string | null
  description: string | null
  ctaUrl: string | null
  coverUrl: string | null
}
const clubEvents = computed<ClubEventItem[]>(() =>
  (clubEventsData.value?.items ?? [])
    .filter((e) => (e as { sourceType?: string }).sourceType === 'custom')
    .map((e) => {
      const r = e as unknown as ClubEventItem
      return {
        id: r.id, startsAt: r.startsAt, endsAt: r.endsAt ?? null, isAllDay: r.isAllDay,
        title: r.title, venueName: r.venueName ?? null, eventTypeCode: r.eventTypeCode ?? null,
        description: r.description ?? null, ctaUrl: r.ctaUrl ?? null, coverUrl: r.coverUrl ?? null,
      }
    }),
)

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
interface ScheduleTeamTab {
  id: string
  /** 'all'／'club' 是虛擬篩選值；其餘一律是真實 `Team.code`（`D1`／`BW1`／`U15`／`BW-U15`…）。 */
  filter: string
  zh: string
  en: string | null
}

/**
 * 隊別分頁依俱樂部動態產生（S1-19）：磐石一線隊 `D1` ＋ `U15`／`U14`／`U12`；
 * 藍鯨一線隊 `BW1` ＋ `BW-U15`／`BW-U12`（沒有 U14，docs/12 §2b 種子資料；不得把
 * `BW1` 當 `D1`，docs/14 踩雷點）。梯隊代碼沿用 4.2 學院隊伍頁既有的單一來源
 * `getAcademyTeamTabs()`，其「其他年齡層」靜態說明分頁（`teamCode: null`）在此排除——
 * 賽事行事曆的分頁必須對應真實可查詢的 `Team.code`，沒有隊伍就沒有賽程可篩選。
 */
const teamTabs = computed<ScheduleTeamTab[]>(() => {
  const youthTabs = getAcademyTeamTabs(clubKey.value, clubFacts.value)
    .filter((t) => t.teamCode !== null)
    .map((t) => ({ id: t.teamCode!.toLowerCase(), filter: t.teamCode!, zh: t.labelZh, en: null }))
  return [
    { id: 'all', filter: 'all', zh: tx('全部', 'All'), en: null },
    { id: 'first-team', filter: firstTeamCode.value, zh: tx('一線隊', 'First Team'), en: isTcrfc.value && !isEn.value ? 'First Team' : null },
    ...youthTabs,
    { id: 'club', filter: 'club', zh: tx('俱樂部活動', 'Club events'), en: null },
  ]
})

/** 隊別頭部標題與「無此隊別資料」文案共用的顯示字，見 `teamHeadName`／`emptyDesc`。 */
function teamHeadLabel(filter: string): string {
  if (filter === 'all') return tx('全部隊別', 'All teams')
  if (filter === 'club') return tx('俱樂部活動', 'Club events')
  const tab = teamTabs.value.find((t) => t.filter === filter)
  if (!tab) return tx('全部隊別', 'All teams')
  if (tab.filter === firstTeamCode.value) return tab.en ? `${tab.zh} ${tab.en}` : tab.zh
  return isEn.value ? `${tab.zh} squad` : `${tab.zh} 梯隊`
}

/** 賽季篩選（規劃書 v3.13 §3.13「分頁與篩選」：賽季，預設當季，可回溯往季）。
 * 從既有 `matches` 回應的 `seasonCode` 動態算出，不需要新增後端端點——藍鯨兩個球季
 * 代碼是「2023」「2025」（`db/seed` 既有事實），字典序排序恰好等於時間序，磐石目前只有
 * 一季「2026-27」，字典序排序同樣成立。取不到任何賽季代碼時退回 `'all'`（全部）。 */
const availableSeasons = computed(() => Array.from(new Set(matches.value.map((m) => m.seasonCode).filter(Boolean))).sort())
const defaultSeason = computed(() => availableSeasons.value.at(-1) ?? 'all')

const state = reactive({ team: 'all', mode: 'fixtures', comp: 'all', ha: 'all', view: 'list', season: defaultSeason.value })

// ---- App 深連結回退網址：`/schedule/{隊別}`（`tcrfc://schedule/d1`）與 `/schedule/{賽事 id}`
// （`tcrfc://match/{id}`）。規劃書 App §2.3 對照表；解析規則與未知參數的回退見 docs/19 §2。
// 🔴 同一個元件服務三種網址（`/schedule/`、`/schedule/{隊別}/`、`/schedule/{賽事 id}/`），第二條路由在
// nuxt.config.ts 的 `pages:extend` 加、指向本檔，所以 definePageMeta（單元 13）兩站一致。
// 解析結果在 SSR 就決定（初始 `state` 一次到位），hydration 兩邊相同；不認得的參數
// 一律 302 回 `/schedule/`（規劃書只規定「不得顯示錯誤頁」，不是 404）。
const scheduleRoute = useRoute()
const routeSlug = computed(() => {
  const raw = scheduleRoute.params.slug
  return typeof raw === 'string' ? raw : ''
})
const scheduleTarget = computed(() => resolveScheduleSlug(
  routeSlug.value,
  teamTabs.value.map((t) => ({ id: t.id, filter: t.filter })),
  matches.value,
))
const deepLinkedMatch = computed(() => {
  const t = scheduleTarget.value
  return t.kind === 'match' ? (matches.value.find((m) => m.id === t.matchId) ?? null) : null
})
if (scheduleTarget.value.kind === 'team') {
  state.team = scheduleTarget.value.filter
}
else if (deepLinkedMatch.value) {
  const m = deepLinkedMatch.value
  state.team = m.teamCode
  if (m.seasonCode) state.season = m.seasonCode
  state.mode = mapMatchStatus(m.status).code === 'finished' ? 'results' : 'fixtures'
}
else if (scheduleTarget.value.kind === 'unknown') {
  await navigateTo(lp('/zh/schedule/'), { redirectCode: 302, replace: true })
}
const mounted = ref(false)
/** 月曆點擊某天或帶 #fx-... 造訪時，即使不符目前篩選也要強制顯示這些場次——
 * 同一天可能不只一場賽事，S1-19 改為集合（見 `renderCalendar`／`jumpToMatches` 檔頭說明），
 * 原本用單一 id 會讓同一天第二場之後的賽事無法被展開。 */
const forcedVisibleIds = ref<Set<string>>(new Set())

/** 瀏覽器實際時區（規劃書 v3.13 §3.13「時區處理」）。`null`＝尚未偵測（SSR／掛載前）
 * 或偵測到的就是 `Asia/Taipei`（兩者對顯示邏輯而言效果相同，見 `matchTimeDisplay()`
 * 檔頭說明）。只在 `onMounted` 設定一次，不隨時間改變（瀏覽器時區在同一次瀏覽階段
 * 不會變動）。 */
const viewerTimeZone = ref<string | null>(null)

/** 行動版次要篩選（賽季／賽事類型／主客場）收合狀態（規劃書 v3.13 §3.13「行動版次要
 * 篩選收合於『篩選』按鈕內」）。初始值 `false` 在 SSR 與 client 掛載前第一次渲染皆相同
 * （純 CSS media query 控制桌面／行動版預設可見度，本欄位只影響行動版），不影響
 * hydration。 */
const filtersOpen = ref(false)

/** 單場賽事詳情彈窗（規劃書 v3.13 §3.13「賽事卡片欄位」動作按鈕「賽事詳情」／「前台
 * 功能」「事件詳情：側邊抽屜或彈窗」）。站內沒有獨立的單場賽事頁路由，改用原生
 * `<dialog>`（內建 focus trap、Escape 關閉、`::backdrop`）。 */
const detailDialog = ref<HTMLDialogElement | null>(null)
const detailMatch = ref<MatchItem | null>(null)
function openDetail(m: MatchItem) {
  detailMatch.value = m
  nextTick(() => detailDialog.value?.showModal())
}
function closeDetail() {
  detailDialog.value?.close()
}
/** 點擊 `<dialog>` 自身（即點擊 `::backdrop` 覆蓋的區域，不是內容區）視同關閉——`<dialog>`
 * 原生只有 Escape 會關閉，點擊背景不會，這裡補上這個慣例互動。 */
function onDetailDialogClick(e: MouseEvent) {
  if (e.target === detailDialog.value) closeDetail()
}

/** 下一場賽事倒數（規劃書 v3.13 §3.13「隊別分類規則」第 3 點：「選定隊別後，頁面標頭
 * 顯示：隊伍名稱、下一場賽事倒數」）。**只在 client 端計算並顯示**（`nextMatchCountdown`
 * 初始為 `null`，SSR／掛載前不輸出）——倒數文字本質上是「現在時刻」與賽事時刻的相對差，
 * SSR 渲染的當下與瀏覽器 hydration 完成的當下必然相差幾秒到幾百毫秒，若在 SSR 就算好
 * 倒數文字，會跟 client 掛載後重新計算的文字不一致，是另一種 hydration mismatch 來源
 * （比照時區換算的處理原則：非確定性內容一律延後到掛載後才計算並显示）。掛載後每分鐘
 * 重新計算一次（毫秒級或秒級更新對「倒數」這種粗粒度資訊沒有實益，且會造成更頻繁的
 * DOM 更新）。 */
const nextMatchCountdown = ref<string | null>(null)
let countdownTimer: ReturnType<typeof setInterval> | undefined
function computeNextMatchCountdown(): string | null {
  if (state.team === 'all' || state.team === 'club') return null
  const upcoming = matches.value
    .filter((m) => m.teamCode === state.team && mapMatchStatus(m.status).code === 'upcoming')
    .slice()
    .sort((a, b) => (a.matchOn === b.matchOn ? (a.kickoff ?? '').localeCompare(b.kickoff ?? '') : a.matchOn.localeCompare(b.matchOn)))[0]
  if (!upcoming) return null
  const instant = matchInstantUtc(upcoming.matchOn, upcoming.kickoff)
  if (!instant) return null
  const diffMs = instant.getTime() - Date.now()
  if (diffMs <= 0) return tx('比賽即將開始', 'The match is about to start')
  const days = Math.floor(diffMs / 86_400_000)
  const hours = Math.floor((diffMs % 86_400_000) / 3_600_000)
  if (isEn.value) {
    const u = (n: number, w: string) => `${n} ${w}${n === 1 ? '' : 's'}`
    if (days > 0) return `Next match in ${u(days, 'day')} ${u(hours, 'hour')}`
    const mins = Math.floor((diffMs % 3_600_000) / 60_000)
    return hours > 0 ? `Next match in ${u(hours, 'hour')} ${u(mins, 'minute')}` : `Next match in ${u(mins, 'minute')}`
  }
  if (days > 0) return `距離下一場比賽尚有 ${days} 天 ${hours} 小時`
  const minutes = Math.floor((diffMs % 3_600_000) / 60_000)
  return hours > 0 ? `距離下一場比賽尚有 ${hours} 小時 ${minutes} 分鐘` : `距離下一場比賽尚有 ${minutes} 分鐘`
}
watch(() => state.team, () => {
  if (mounted.value) nextMatchCountdown.value = computeNextMatchCountdown()
})

onMounted(() => {
  mounted.value = true
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone
    if (tz && tz !== TAIPEI_TIME_ZONE) viewerTimeZone.value = tz
  } catch {
    // Intl 不支援時區偵測：維持 null，畫面照舊顯示台灣時間，不視為錯誤。
  }
  nextMatchCountdown.value = computeNextMatchCountdown()
  countdownTimer = setInterval(() => {
    nextMatchCountdown.value = computeNextMatchCountdown()
  }, 60_000)
  handleInitialHash()
  if (deepLinkedMatch.value) {
    const m = deepLinkedMatch.value
    forcedVisibleIds.value = new Set([fixtureId(m.matchOn, m.homeAway, m.matchNo)])
    openDetail(m)
  }
})
onBeforeUnmount(() => {
  if (countdownTimer) clearInterval(countdownTimer)
})

/** 延賽原定時間文字：zh「原定 …」、en「Originally scheduled …」。 */
function postponedLine(originalMatchOn: string | null, originalKickoff: string | null): string | null {
  return isEn.value ? postponedNoteEn(originalMatchOn, originalKickoff) : postponedNote(originalMatchOn, originalKickoff)
}

function cardStatusCode(m: MatchItem): string {
  return mapMatchStatus(m.status).code
}
/** 賽事卡片左側時間欄的顯示值（規劃書 v3.13 §3.13「時區處理」），見
 * `app/utils/schedule.ts` 的 `matchTimeDisplay()` 檔頭說明：`viewerTimeZone` 為 `null`
 * 時（SSR／掛載前）回傳與既有行為逐字元相同的台灣時間，不會有 hydration mismatch。 */
function timeOf(m: MatchItem) {
  return matchTimeDisplay(m.matchOn, m.kickoff, viewerTimeZone.value)
}
/** 俱樂部活動卡片的時間欄顯示值。`isAllDay` 事件不顯示鐘點（只保留週幾／日期／月份，
 * 一律以 Asia/Taipei 呈現——全天事件沒有確切時刻可換算），有確切時刻的事件則呼叫
 * `instantTimeDisplay()` 依瀏覽器時區換算，與 `timeOf()` 是同一套顯示邏輯。 */
function eventTimeOf(e: ClubEventItem) {
  const instant = new Date(e.startsAt)
  const display = instantTimeDisplay(instant, viewerTimeZone.value)
  return e.isAllDay ? { ...display, kickoff: null, converted: false } : display
}
function cardMatches(m: MatchItem): boolean {
  const id = fixtureId(m.matchOn, m.homeAway, m.matchNo)
  if (forcedVisibleIds.value.has(id)) return true
  const teamOk = state.team === 'all' ? true : m.teamCode === state.team
  const statusOk = state.mode === 'fixtures' ? cardStatusCode(m) === 'upcoming' : cardStatusCode(m) === 'finished'
  const compOk = state.comp === 'all' ? true : m.competitionTag === state.comp
  const haOk = state.ha === 'all' ? true : haCode(m.homeAway) === state.ha
  const seasonOk = state.season === 'all' ? true : m.seasonCode === state.season
  return teamOk && statusOk && compOk && haOk && seasonOk
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

// 「俱樂部活動」只在 all／club 分頁顯示；賽果模式一律不顯示（後端 `ListClubEventsAsync`
// 檔頭明講「俱樂部活動沒有『賽果』的語意」，見檔頭「S1-19 補完」說明）。
const showClubEvents = computed(() => state.mode === 'fixtures' && (state.team === 'all' || state.team === 'club'))
const visibleClubEvents = computed(() => (showClubEvents.value ? clubEvents.value : []))

const teamHeadName = computed(() => teamHeadLabel(state.team))
// GEO-03（S1-12d）：聯賽名稱為單一來源（useSiteFacts 讀後端 API），不在此重複寫死
// 字面值（改動前本頁不論 club 皆寫死磐石的聯賽全名，藍鯨容器會顯示錯誤的聯賽名稱）。
const leagueName = computed(() => clubFacts.value.league.nameZh)
// 英文版：聯賽英文名只取 facts.league.nameEn，沒有就不自創全名（docs/06 §1.1「企業甲級足球聯賽」列）。
const leagueNameEn = computed(() => clubFacts.value.league.nameEn)
const leagueProseEn = computed(() => leagueNameEn.value ?? 'the league')
// S1-19 補完：賽季不再字面寫死「2026/27」（對藍鯨本來就是錯的事實，藍鯨球季代碼是
// 「2023」「2025」），改讀 `state.season`（見 `availableSeasons`／`defaultSeason`）。
const seasonLabel = computed(() => (state.season === 'all'
  ? tx('全部賽季', 'All seasons')
  : (isEn.value ? `${state.season} season` : `${state.season} 賽季`)))
const teamHeadMeta = computed(() => {
  if (isEn.value) {
    if (state.team === 'club') return `${visibleClubEvents.value.length} club ${visibleClubEvents.value.length === 1 ? 'event' : 'events'}`
    const tail = state.mode === 'results' ? 'Results' : `${visibleMatches.value.length} ${visibleMatches.value.length === 1 ? 'fixture' : 'fixtures'}`
    return [seasonLabel.value, leagueNameEn.value, tail].filter(Boolean).join(' · ')
  }
  if (state.team === 'club') return `共 ${visibleClubEvents.value.length} 則俱樂部活動`
  return state.mode === 'results'
    ? `${seasonLabel.value} · ${leagueName.value} · 賽果`
    : `${seasonLabel.value} · ${leagueName.value} · 共 ${visibleMatches.value.length} 場`
})
const isEmpty = computed(() => mounted.value && visibleMatches.value.length === 0 && visibleClubEvents.value.length === 0)
const emptyDesc = computed(() => {
  if (isEn.value) {
    if (state.team === 'club') return 'There are no announced club events yet (press conferences, signing sessions, fan meet-ups and so on). Please follow our official social channels and latest news.'
    if (state.mode === 'results') return `No completed matches yet (${seasonLabel.value}). Results will be added once matches are played.`
    if (state.team !== 'all' && state.team !== firstTeamCode.value) return `Fixtures for ${teamHeadLabel(state.team)} are not available yet. They will be added to this page once the schedule is confirmed.`
    return 'No fixtures are scheduled for this combination of team, competition type and home / away.'
  }
  if (state.team === 'club') return '目前尚無公告的俱樂部活動（記者會、簽名會、球迷見面會等），請持續關注官方社群與最新消息。'
  if (state.mode === 'results') return `本季（${seasonLabel.value}）尚未有已完成的賽事，賽果會在比賽結束後更新。`
  // 一線隊（D1／BW1）與「全部」皆已有真實賽事資料，其餘（各梯隊）尚無賽程可用。
  if (state.team !== 'all' && state.team !== firstTeamCode.value) {
    return `${teamHeadLabel(state.team)}的賽程資料尚未提供，待客戶提供各梯隊賽程表後將更新於本頁。`
  }
  return '此隊別、賽事類型或主客場組合目前尚無排定賽事。'
})

// ---- 隊別分頁鍵盤導覽（比照 app/pages/zh/academy/teams.vue 的 roving tabindex）----
const tabRefs = ref<Record<string, HTMLElement | null>>({})
function selectTab(id: string, filter: string, focus = true) {
  state.team = filter
  forcedVisibleIds.value = new Set()
  if (focus) tabRefs.value[id]?.focus()
}
function onTabKeydown(e: KeyboardEvent, index: number) {
  const tabs = teamTabs.value
  let idx = index
  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') idx = (index + 1) % tabs.length
  else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') idx = (index - 1 + tabs.length) % tabs.length
  else if (e.key === 'Home') idx = 0
  else if (e.key === 'End') idx = tabs.length - 1
  else return
  e.preventDefault()
  const t = tabs[idx]!
  selectTab(t.id, t.filter)
}

function setMode(mode: string) {
  state.mode = mode
  forcedVisibleIds.value = new Set()
}
function setView(view: string) {
  state.view = view
  if (view === 'calendar') nextTick(renderCalendar)
}

watch([() => state.comp, () => state.ha], () => {
  forcedVisibleIds.value = new Set()
})
watch(visibleMatches, () => {
  if (state.view === 'calendar') renderCalendar()
})

/** 月曆／分享連結組出的 HTML 屬性與文字節點裡插入的資料（對手名稱、場地……）一律先跑過
 * 這裡，避免後台可自由輸入的文字欄位（`matches.opponent` 等）被當成 HTML 標記解讀——
 * 月曆檢視是用字串組 `innerHTML`（非 Vue 樣板），沒有樣板引擎自動逸出這層保護。 */
function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

// ---- 月曆檢視（比照原 script：純字串組 innerHTML，client only）----
const calRoot = ref<HTMLElement | null>(null)

interface CalDayEvent { day: number; id: string; opponent: string }

function renderCalendar() {
  const root = calRoot.value
  if (!root) return
  root.innerHTML = ''
  const visible = visibleMatches.value
  if (visible.length === 0) {
    root.innerHTML = `<p class="sched-empty__desc">${tx('此篩選條件下沒有可顯示於月曆的賽事。', 'No matches to show on the calendar for these filters.')}</p>`
    return
  }
  const byMonth = new Map<string, CalDayEvent[]>()
  for (const m of visible) {
    const key = m.matchOn.slice(0, 7)
    const day = Number.parseInt(m.matchOn.slice(8, 10), 10)
    if (!byMonth.has(key)) byMonth.set(key, [])
    byMonth.get(key)!.push({ day, id: fixtureId(m.matchOn, m.homeAway, m.matchNo), opponent: m.opponent ?? '' })
  }
  const DOW = isEn.value ? ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'] : ['日', '一', '二', '三', '四', '五', '六']
  for (const key of Array.from(byMonth.keys()).sort()) {
    const [yStr, mStr] = key.split('-')
    const y = Number(yStr)
    const mo = Number(mStr)
    const firstDow = new Date(Date.UTC(y, mo - 1, 1)).getUTCDay()
    const daysInMonth = new Date(Date.UTC(y, mo, 0)).getUTCDate()
    // 同一天可能不只一場賽事——原本 `Map<day, 單一事件>` 用 `Array.map` 建構時，同一天
    // 第二場之後的賽事會直接覆蓋掉前一場，悄悄從月曆消失（S1-19 修正）。改用
    // `Map<day, 事件陣列>`，同一天的賽事全部可達。
    const grouped = new Map<number, CalDayEvent[]>()
    for (const ev of byMonth.get(key)!) {
      if (!grouped.has(ev.day)) grouped.set(ev.day, [])
      grouped.get(ev.day)!.push(ev)
    }
    const monthTitleId = `cal-month-${key}`
    let html = `<div class="cal-month"><p class="cal-month__title" id="${monthTitleId}">${isEn.value ? calMonthTitleEn(key) : calMonthTitle(key)}</p><div class="cal-grid" aria-labelledby="${monthTitleId}">`
    for (const w of DOW) html += `<span class="cal-dow" aria-hidden="true">${w}</span>`
    for (let i = 0; i < firstDow; i++) html += '<span class="cal-day cal-day--pad" aria-hidden="true"></span>'
    for (let d = 1; d <= daysInMonth; d++) {
      const evs = grouped.get(d)
      if (!evs || evs.length === 0) {
        html += `<span class="cal-day">${d}</span>`
        continue
      }
      const ids = evs.map((ev) => ev.id).join(',')
      const opponents = evs.map((ev) => ev.opponent).filter(Boolean).join(isEn.value ? ', ' : '、')
      const label = isEn.value
        ? (evs.length > 1
            ? `${calDayLabelEn(key, d)}: ${evs.length} matches vs ${opponents}. View details`
            : `${calDayLabelEn(key, d)}: vs ${opponents}. View details`)
        : (evs.length > 1
            ? `${calMonthTitle(key)} ${d} 日，共 ${evs.length} 場賽事：對 ${opponents}，查看詳情`
            : `${calMonthTitle(key)} ${d} 日對 ${opponents}，查看詳情`)
      const countBadge = evs.length > 1 ? `<sup class="cal-day__count" aria-hidden="true">${evs.length}</sup>` : ''
      html += `<span class="cal-day cal-day--match"><a href="#${escapeHtml(evs[0]!.id)}" data-cal-link data-targets="${escapeHtml(ids)}" aria-label="${escapeHtml(label)}">${d}${countBadge}</a></span>`
    }
    html += '</div></div>'
    root.insertAdjacentHTML('beforeend', html)
  }
  root.querySelectorAll<HTMLAnchorElement>('[data-cal-link]').forEach((a) => {
    a.addEventListener('click', (e) => {
      e.preventDefault()
      const targets = a.getAttribute('data-targets')
      if (!targets) return
      jumpToMatches(targets.split(','))
    })
  })
}

/** 點擊月曆日期＝「展開當日賽事」（規劃書 v3.13 §3.13 原文用語）：把當天全部賽事
 * 一併納入強制顯示集合，並捲動、聚焦到當天的第一場。 */
function jumpToMatches(ids: string[]) {
  forcedVisibleIds.value = new Set(ids)
  setView('list')
  nextTick(() => {
    const el = document.getElementById(ids[0]!)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' })
      el.focus({ preventScroll: true })
    }
  })
}

function handleInitialHash() {
  if (location.hash && location.hash.indexOf('#fx-') === 0) {
    const id = location.hash.slice(1)
    forcedVisibleIds.value = new Set([id])
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

/**
 * RFC 5545 §3.3.11 逸出規則：反斜線、分號、逗號、換行皆須逸出，**反斜線必須最先處理**
 * ——否則後續新增的逸出反斜線會被自己的規則二次跳脫。比照 `apps/api/Common/IcsBuilder.cs`
 * 的 `Escape()`（前後端各自實作、同一份規則，見檔頭「S1-19 修正的既有落差」）。
 * 換行字元若不逸出，含真實換行的資料（例如場地或對手名稱誤貼多行文字）會被解讀成
 * `.ics` 的下一個屬性行，破壞整份行事曆檔案——這是可被資料內容觸發的注入風險，
 * 不是理論疑慮。
 */
function icsEscape(s: string): string {
  return s
    .replace(/\\/g, '\\\\')
    .replace(/;/g, '\\;')
    .replace(/,/g, '\\,')
    .replace(/\r\n/g, '\\n')
    .replace(/\n/g, '\\n')
}

/**
 * RFC 5545 §3.1 行折疊：內容行以八位元組計超過 75 就要折行，延續行以單一空白開頭；
 * 中文字元在 UTF-8 是 3 個位元組，不能從字元中間切斷（否則產生亂碼）。比照
 * `apps/api/Common/IcsBuilder.cs` 的 `FoldLine()`——`.ics` 目前是 client-side 產生，
 * 前後端沒有共用程式碼的機制，兩邊各自實作但規則與位元組安全切點邏輯逐字對應。
 * 中文全名（俱樂部＋對手＋聯賽）疊在同一行很容易超過 75 位元組，不折行的話部分行事曆
 * 應用程式可能誤判或截斷內容。
 */
function foldIcsLine(line: string): string[] {
  const maxOctets = 75
  const bytes = new TextEncoder().encode(line)
  if (bytes.length <= maxOctets) return [line]
  const decoder = new TextDecoder()
  const out: string[] = []
  let offset = 0
  let first = true
  while (offset < bytes.length) {
    let limit = Math.min(maxOctets - (first ? 0 : 1), bytes.length - offset)
    while (limit > 1 && (bytes[offset + limit]! & 0xc0) === 0x80) limit--
    out.push((first ? '' : ' ') + decoder.decode(bytes.slice(offset, offset + limit)))
    offset += limit
    first = false
  }
  return out
}
function toUtcIcs(dateStr: string, timeStr: string): string {
  const [Y, M, D] = dateStr.split('-').map(Number)
  const [h, mi] = timeStr.split(':').map(Number)
  const local = new Date(Date.UTC(Y!, M! - 1, D!, h! - 8, mi!))
  return `${local.getUTCFullYear()}${pad(local.getUTCMonth() + 1)}${pad(local.getUTCDate())}T${pad(local.getUTCHours())}${pad(local.getUTCMinutes())}00Z`
}
/** `.ics` UID 網域取自目前站台的實際網址（`siteConfig.url`，已提前宣告於檔案開頭），
 * 不寫死 `tcrfc.tw`——藍鯨容器跑出來的網址本來就不是這個網域，UID 只需要「合理唯一」，
 * 不需要真的可解析，但至少不該讓兩站的行事曆項目共用同一個網域字面值。取不到合法網址時
 * （本機未設定 `NUXT_PUBLIC_SITE_URL`）退回俱樂部代碼組出的預留網域。 */
const uidHost = computed(() => {
  try {
    return new URL(siteConfig.url).hostname
  } catch {
    return club === 'bw' ? 'bw.tcrfc.tw' : 'tcrfc.tw'
  }
})

function eventToVeventLines(m: MatchItem): string[] {
  const date = m.matchOn
  const kickoff = m.kickoff ?? '00:00'
  const opponent = m.opponent ?? ''
  const venue = m.venue ?? ''
  const ha = haCode(m.homeAway) === 'home' ? tx('主場', 'Home') : tx('客場', 'Away')
  const start = toUtcIcs(date, kickoff)
  const startDate = new Date(`${date}T${kickoff}:00+08:00`)
  const endDate = new Date(startDate.getTime() + 2 * 60 * 60 * 1000)
  const end = `${endDate.getUTCFullYear()}${pad(endDate.getUTCMonth() + 1)}${pad(endDate.getUTCDate())}T${pad(endDate.getUTCHours())}${pad(endDate.getUTCMinutes())}00Z`
  const title = isEn.value
    ? `${CLUB_NAME_EN} vs ${opponent} (${[leagueNameEn.value, ha].filter(Boolean).join(' · ')})`
    : `${clubAssets.value.nameZh} vs ${opponent}（${leagueName.value}・${ha}）`
  const loc = venue === 'TBC' ? tx('場地未定', 'Venue TBC') : venue
  return [
    'BEGIN:VEVENT',
    `UID:${icsEscape(fixtureId(m.matchOn, m.homeAway, m.matchNo))}@${uidHost.value}`,
    `DTSTAMP:${start}`,
    `DTSTART:${start}`,
    `DTEND:${end}`,
    `SUMMARY:${icsEscape(title)}`,
    `LOCATION:${icsEscape(loc)}`,
    // 內容依俱樂部（S1-19 修正——原本這句字面寫死「台中磐石足球俱樂部」，
    // 藍鯨容器下載的 .ics 會顯示錯誤的官方公告主體）。
    `DESCRIPTION:${icsEscape(isEn.value ? `Fixtures are subject to change. Please refer to official announcements from ${CLUB_NAME_EN}.` : `賽程可能異動，請以${clubAssets.value.nameZh}官方公告為準。`)}`,
    'END:VEVENT',
  ]
}
function downloadIcs(filename: string, veventBlocks: string[][]) {
  const rawLines = ['BEGIN:VCALENDAR', 'VERSION:2.0', `PRODID:-//${club === 'bw' ? 'TCBW' : 'TCRFC'}//Schedule//${isEn.value ? 'EN' : 'ZH'}`, 'CALSCALE:GREGORIAN']
    .concat(veventBlocks.flat())
    .concat(['END:VCALENDAR'])
  // RFC 5545 行折疊（見 foldIcsLine 檔頭），逐行補上規定的 CRLF（含最後一行，
  // 比照 apps/api/Common/IcsBuilder.cs 的既有寫法）。
  const body = rawLines.flatMap(foldIcsLine).map((line) => `${line}\r\n`).join('')
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
  downloadIcs(`${club}-${m.matchOn}-vs-${m.opponent}.ics`, [eventToVeventLines(m)])
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
    window.prompt(tx('複製此賽事連結：', 'Copy this match link:'), shareUrl)
  }
}

function onBulkIcs() {
  const visible = visibleMatches.value
  if (visible.length === 0) {
    window.alert(tx('目前檢視沒有可下載的賽事。', 'There are no matches in the current view to download.'))
    return
  }
  downloadIcs(`${club}-schedule-${state.team}-${state.mode}.ics`, visible.map(eventToVeventLines))
}

/** 英文版 SEO 參數（事實由 useSiteFacts 與賽程回應取得，不在 club-copy-en-sched.ts 寫死）。 */
function scheduleSeoArgsEn() {
  return {
    leagueNameEn: leagueNameEn.value,
    seasonCode: defaultSeason.value === 'all' ? null : defaultSeason.value,
    matchCount: matches.value.length,
    squadLabel: clubAcademyLabel(' / '),
  }
}

useSeoMeta({
  title: computed(() => (isEn.value ? getScheduleSeoEn(scheduleSeoArgsEn()).title : `賽事行事曆 Schedule｜${getClubAssets(club).nameZh}`)),
  description: computed(() => {
    if (isEn.value) return getScheduleSeoEn(scheduleSeoArgsEn()).description
    // S1-19 補完：賽季不再字面寫死「2026/27」（對藍鯨是錯的事實，藍鯨球季代碼是
    // 「2023」「2025」，見 availableSeasons／seasonLabel 的既有說明）。
    const seasonPart = defaultSeason.value === 'all' ? '' : `${defaultSeason.value} `
    return `${getClubAssets(club).nameZh}完整賽事行事曆：${seasonPart}${leagueName.value} ${matches.value.length} 場賽程，依隊別（一線隊／${clubAcademyLabel()}）分類，支援賽程賽果切換、月曆檢視與單場加入行事曆。`
  }),
})

// SportsEvent JSON-LD（GEO-08）。siteConfig 已提前宣告於檔案開頭（供 .ics UID 使用），
// 這裡直接沿用同一個結論，兩站各自跑出自己網域的絕對網址，不寫死 tcrfc.tw。
const selfTeamName = computed(() => (isEn.value ? CLUB_NAME_EN : getClubAssets(club).nameZh))

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
    const roundLabel = m.roundNo ? (isEn.value ? `Round ${m.roundNo}: ` : `第${m.roundNo}輪：`) : ''
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
        // S1-20：場地地址（GEO-08「場地與地址，來自場地資料」）——matches.venue 只是自由
        // 文字欄位，用名稱比對 useSiteFacts(club) 既有的 venues 清單找地址，見
        // shared/utils/schema-batch2.ts 的 venueAddressByName() 檔頭說明。找不到時只保留
        // addressCountry，不臆造街址。
        address: {
          '@type': 'PostalAddress',
          ...(venueAddressByName(m.venue, clubFacts.value.venues) ? { streetAddress: venueAddressByName(m.venue, clubFacts.value.venues)! } : {}),
          addressCountry: 'TW',
        },
      },
      homeTeam,
      awayTeam,
      competitor: [selfTeam, oppTeam],
      url: `${siteConfig.url}${lp('/zh/schedule/')}#${fixtureId(m.matchOn, m.homeAway, m.matchNo)}`,
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

// Event JSON-LD（GEO-05／GEO-08，S1-20）俱樂部活動。用未經 client 端篩選的完整清單
// （clubEvents，不是 visibleClubEvents）——SSR 輸出應反映「這一頁完整收錄的資料」，
// 跟上方 sportsEvents 用 matches（不是 visibleMatches）同一個既有理由（比照 S1-18a
// FAQPage schema「用 faqsByCategory 而不是套用搜尋篩選後的 visibleByCategory」的說明）。
// 合不合格、欄位怎麼組見 shared/utils/schema-batch2.ts；沒有任何一筆合格時完全不輸出
// （GEO-05）。錨點 id 見下方樣板 `:id="'ce-' + e.id"`，與這裡組出的 url 對應。
useClubEventSchema(clubEvents, {
  siteUrl: computed(() => siteConfig.url ?? ''),
  pagePath: computed(() => lp('/zh/schedule/')),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('賽事行事曆', 'Schedule') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">13</span>
  <div class="container">
    <p class="page-hero__eyebrow">13 Schedule</p>
    <h1>{{ tx('賽事行事曆', 'Schedule') }}<span v-if="!isEn" class="en">Schedule</span></h1>
    <p v-if="isEn" class="page-hero__lede">Full fixtures and results for the First Team and every Academy squad, all on one page. <strong>All times are local and subject to change</strong>; please refer to official announcements for confirmed times.</p>
    <p v-else class="page-hero__lede">一線隊與各梯隊的完整賽程與賽果，一頁掌握。<strong>所有時間為當地時間，可能異動</strong>，正式時間請以官方公告為準。</p>
    <p v-if="isEn && mounted && viewerTimeZone" class="page-hero__tz-note">Times have been converted to your device's time zone (<span class="en">{{ viewerTimeZone }}</span>); official Taiwan times are shown in each match's details.</p>
    <p v-else-if="mounted && viewerTimeZone" class="page-hero__tz-note">已依您目前的裝置時區（<span class="en">{{ viewerTimeZone }}</span>）換算顯示；台灣官方公告時間請見各賽事詳情。</p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && (hasFallbackLocale(data) || hasFallbackLocale(clubEventsData))" partial />

<section class="band schedule-band" aria-labelledby="schedule-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="schedule-title">{{ tx('賽事行事曆', 'Schedule') }}</h2>

    <!-- 隊別分頁（第一層分類） -->
    <div class="team-tabs" data-team-tabs>
      <div class="team-tabs__list" role="tablist" :aria-label="tx('選擇隊別', 'Select a team')">
        <button
          v-for="(tab, i) in teamTabs" :key="tab.id"
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
          <!-- 規劃書 v3.13 §3.13「選定隊別後，頁面標頭顯示：隊伍名稱、下一場賽事倒數；
               並提供該隊別專屬的行事曆訂閱網址」。倒數只在 client 端算好才顯示（見
               nextMatchCountdown 檔頭說明，避免 hydration mismatch）；訂閱網址目前後端
               沒有 webcal feed 端點（見檔頭「S1-19 補完」），不自行產生假網址，改為誠實
               告知現況並指向下方「訂閱賽程」區塊既有的 .ics 下載功能。 -->
          <p v-if="mounted && nextMatchCountdown" class="sched-teamhead__countdown">{{ nextMatchCountdown }}</p>
          <p v-if="isEn && state.team !== 'all' && state.team !== 'club'" class="sched-teamhead__subscribe-note">
            A calendar subscription (<span class="en">webcal</span>) link for {{ teamHeadName }} is not available yet. For now, use the <span class="en">.ics</span> download below or on any match card to get the fixtures.
          </p>
          <p v-else-if="state.team !== 'all' && state.team !== 'club'" class="sched-teamhead__subscribe-note">
            {{ teamHeadName }}專屬的行事曆訂閱（<span class="en">webcal</span>）網址尚未上線（後端訂閱 feed 端點待開發，見下方「訂閱賽程」說明）；目前請於下方或各賽事卡片使用 <span class="en">.ics</span> 下載取得賽程。
          </p>
        </div>

        <!-- 次層控制列 -->
        <div class="sched-controls">
          <div class="sched-controls__group" role="group" :aria-label="tx('賽程或賽果', 'Fixtures or results')">
            <button type="button" class="sched-toggle" data-mode="fixtures" :aria-pressed="state.mode === 'fixtures'" @click="setMode('fixtures')">{{ tx('賽程 ', 'Fixtures') }}<span v-if="!isEn" class="en">Fixtures</span></button>
            <button type="button" class="sched-toggle" data-mode="results" :aria-pressed="state.mode === 'results'" @click="setMode('results')">{{ tx('賽果 ', 'Results') }}<span v-if="!isEn" class="en">Results</span></button>
          </div>

          <!-- 規劃書 v3.13 §3.13「行動版次要篩選收合於『篩選』按鈕內」。桌面版由 CSS
               media query 恆常顯示（本按鈕在桌面版隱藏），行動版預設收合。 -->
          <button
            type="button" class="sched-filters-toggle" :aria-expanded="filtersOpen"
            aria-controls="sched-filters-panel" @click="filtersOpen = !filtersOpen"
          >{{ tx('篩選 ', 'Filters') }}<span v-if="!isEn" class="en">Filters</span></button>

          <div id="sched-filters-panel" class="sched-controls__selects" :class="{ 'is-open': filtersOpen }">
            <label class="sched-select">
              <span class="visually-hidden">{{ tx('賽季', 'Season') }}</span>
              <select v-model="state.season" data-season :disabled="availableSeasons.length === 0">
                <option value="all">{{ tx('全部賽季', 'All seasons') }}</option>
                <option v-for="s in availableSeasons" :key="s" :value="s">{{ isEn ? `${s} season` : `${s} 賽季` }}</option>
              </select>
            </label>
            <label class="sched-select">
              <span class="visually-hidden">{{ tx('賽事類型', 'Competition type') }}</span>
              <select data-comp-filter v-model="state.comp">
                <option value="all">{{ tx('賽事類型：全部', 'Competition: All') }}</option>
                <option value="league">{{ tx('聯賽', 'League') }}</option>
                <option value="cup">{{ tx('盃賽', 'Cup') }}</option>
                <option value="friendly">{{ tx('友誼賽', 'Friendly') }}</option>
                <option value="other">{{ tx('其他', 'Other') }}</option>
              </select>
            </label>
            <label class="sched-select">
              <span class="visually-hidden">{{ tx('主客場', 'Home or away') }}</span>
              <select data-ha-filter v-model="state.ha">
                <option value="all">{{ tx('主客場：全部', 'Home / away: All') }}</option>
                <option value="home">{{ tx('主場', 'Home') }}</option>
                <option value="away">{{ tx('客場', 'Away') }}</option>
              </select>
            </label>
          </div>

          <div class="sched-controls__group" role="group" :aria-label="tx('檢視方式', 'View')">
            <button type="button" class="sched-toggle" data-view="list" :aria-pressed="state.view === 'list'" @click="setView('list')">{{ tx('列表 ', 'List') }}<span v-if="!isEn" class="en">List</span></button>
            <button type="button" class="sched-toggle" data-view="calendar" :aria-pressed="state.view === 'calendar'" @click="setView('calendar')">{{ tx('月曆 ', 'Calendar') }}<span v-if="!isEn" class="en">Calendar</span></button>
          </div>
        </div>

        <p class="sched-official-note">{{ isEn ? `Fixtures are subject to change. Announcements from the club and from the organiser of ${leagueProseEn} take precedence.` : `賽程如有異動，一律以俱樂部官方公告與${leagueName}主辦單位公告為準。` }}</p>

        <!-- 列表檢視 -->
        <div class="sched-view" data-view-panel="list" :hidden="state.view !== 'list'">
          <div v-for="group in monthGroups" :key="group.key" class="month-group" :data-month="group.key" :hidden="isGroupHidden(group.key)">
            <h3 class="month-heading">{{ isEn ? monthHeadingEn(group.key) : monthHeading(group.key) }}</h3>
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
                  <span class="fixture-card__wd">{{ isEn ? timeOf(m).weekdayEn : `${timeOf(m).weekdayZh} ${timeOf(m).weekdayEn}` }}</span>
                  <span class="fixture-card__date">{{ timeOf(m).day }}</span>
                  <span class="fixture-card__mon">{{ timeOf(m).monthAbbr }}</span>
                  <time v-if="timeOf(m).kickoff" class="fixture-card__kickoff" :datetime="matchInstantUtc(m.matchOn, m.kickoff)?.toISOString()">{{ timeOf(m).kickoff }}</time>
                </div>
                <div class="fixture-card__body">
                  <div class="fixture-card__meta">
                    <span :class="['tag', `tag--${m.competitionTag}`]">{{ isEn ? compTagLabelEn(m.competitionTag) : compTagLabel(m.competitionTag) }}</span>
                    <span class="fixture-card__round">{{ isEn ? `Round ${m.roundNo}` : `第 ${m.roundNo} 輪` }}</span>
                    <span :class="['status-pill', `status-pill--${mapMatchStatus(m.status).code}`]">{{ isEn ? mapMatchStatusEn(m.status).label : mapMatchStatus(m.status).label }}</span>
                  </div>
                  <p v-if="postponedLine(m.originalMatchOn, m.originalKickoff)" class="fixture-card__postponed">{{ postponedLine(m.originalMatchOn, m.originalKickoff) }}</p>
                  <div class="fixture-card__matchup">
                    <template v-if="haCode(m.homeAway) === 'away'">
                      <span class="fx-side fx-side--them">
                        <span class="fx-crest fx-crest--ph" aria-hidden="true">{{ (m.opponent ?? '').charAt(0) }}</span>
                        <span class="fx-name">{{ m.opponent }}</span>
                      </span>
                      <span class="fx-vs">VS</span>
                      <span class="fx-side fx-side--us">
                        <img class="fx-crest" :src="clubAssets.headerMark.src" width="26" height="28" alt="">
                        <span class="fx-name">{{ clubAssets.shortNameZh }}</span>
                      </span>
                    </template>
                    <template v-else>
                      <span class="fx-side fx-side--us">
                        <img class="fx-crest" :src="clubAssets.headerMark.src" width="26" height="28" alt="">
                        <span class="fx-name">{{ clubAssets.shortNameZh }}</span>
                      </span>
                      <span class="fx-vs">VS</span>
                      <span class="fx-side fx-side--them">
                        <span class="fx-crest fx-crest--ph" aria-hidden="true">{{ (m.opponent ?? '').charAt(0) }}</span>
                        <span class="fx-name">{{ m.opponent }}</span>
                      </span>
                    </template>
                  </div>
                  <p class="fixture-card__venue">
                    <span :class="['ha-pill', haCode(m.homeAway) === 'home' ? 'ha-pill--home' : 'ha-pill--away']">{{ haCode(m.homeAway) === 'home' ? tx('主場 HOME', 'Home') : tx('客場 AWAY', 'Away') }}</span>
                    <template v-if="m.venue === 'TBC'">
                      <span class="tbc-note">{{ tx('場地未定 · VENUE TBC', 'Venue TBC') }}</span>
                    </template>
                    <template v-else>
                      <span>{{ m.venue }}</span><a class="fixture-card__map" :href="venueMapUrl(m.venue ?? '')" target="_blank" rel="noopener">{{ tx('地圖', 'Map') }}<span class="visually-hidden">{{ tx('（另開新視窗）', ' (opens in a new window)') }}</span></a>
                    </template>
                  </p>
                </div>
                <div class="fixture-card__actions">
                  <button type="button" class="btn btn--outline btn--sm" data-detail-btn @click="openDetail(m)">{{ tx('賽事詳情', 'Match details') }}</button>
                  <button type="button" class="btn btn--primary btn--sm" data-ics-btn @click="onIcsClick(m)">{{ tx('加入行事曆 ', 'Add to calendar ') }}<span class="en">.ics</span></button>
                  <button type="button" class="btn btn--dark btn--sm" data-share-btn @click="onShareClick(m)">{{ copiedId === fixtureId(m.matchOn, m.homeAway, m.matchNo) ? tx('連結已複製', 'Link copied') : tx('分享此賽事', 'Share this match') }}</button>
                </div>
              </article>
            </div>
          </div>

          <!-- 俱樂部活動（規劃書 v3.13 §3.13「資料來源」第二列，S1-19 補完）。獨立於
               賽事列表之後，見檔頭「S1-19 補完」的整合方式說明。 -->
          <div v-if="visibleClubEvents.length > 0" class="club-events-block">
            <h3 class="month-heading">{{ tx('俱樂部活動 ', 'Club events') }}<span v-if="!isEn" class="en">Club Events</span></h3>
            <div class="fixture-list">
              <article v-for="e in visibleClubEvents" :id="`ce-${e.id}`" :key="e.id" class="fixture-card club-event-card">
                <div class="fixture-card__time">
                  <span class="fixture-card__wd">{{ isEn ? eventTimeOf(e).weekdayEn : `${eventTimeOf(e).weekdayZh} ${eventTimeOf(e).weekdayEn}` }}</span>
                  <span class="fixture-card__date">{{ eventTimeOf(e).day }}</span>
                  <span class="fixture-card__mon">{{ eventTimeOf(e).monthAbbr }}</span>
                  <time v-if="eventTimeOf(e).kickoff" class="fixture-card__kickoff" :datetime="e.startsAt">{{ eventTimeOf(e).kickoff }}</time>
                </div>
                <div class="fixture-card__body">
                  <div class="fixture-card__meta">
                    <span class="tag tag--club-event">{{ tx('俱樂部活動', 'Club event') }}</span>
                  </div>
                  <p class="club-event-card__title">{{ e.title }}</p>
                  <p v-if="e.description" class="club-event-card__desc">{{ e.description }}</p>
                  <p v-if="e.venueName" class="fixture-card__venue">
                    <span>{{ e.venueName }}</span><a class="fixture-card__map" :href="venueMapUrl(e.venueName)" target="_blank" rel="noopener">{{ tx('地圖', 'Map') }}<span class="visually-hidden">{{ tx('（另開新視窗）', ' (opens in a new window)') }}</span></a>
                  </p>
                </div>
                <div class="fixture-card__actions">
                  <a v-if="e.ctaUrl" class="btn btn--primary btn--sm" :href="e.ctaUrl" target="_blank" rel="noopener">{{ tx('活動詳情', 'Event details') }}</a>
                </div>
              </article>
            </div>
          </div>

          <div class="sched-empty" data-empty-state :hidden="!isEmpty">
            <p class="sched-empty__title">{{ tx('目前沒有符合條件的賽事', 'No matches match your filters') }}</p>
            <p class="sched-empty__desc" data-empty-desc>{{ emptyDesc }}</p>
          </div>
        </div>

        <!-- 月曆檢視 -->
        <div class="sched-view" data-view-panel="calendar" :hidden="state.view !== 'calendar'">
          <div ref="calRoot" class="sched-calendar" data-calendar-root aria-live="polite"></div>
        </div>

      </div>
    </div>

    <!-- 單場賽事詳情彈窗（規劃書 v3.13 §3.13「動作按鈕：賽事詳情」／「前台功能：事件
         詳情」）。原生 <dialog>：內建 focus trap、Escape 關閉、::backdrop。 -->
    <dialog ref="detailDialog" class="match-detail" aria-labelledby="match-detail-title" @click="onDetailDialogClick" @close="detailMatch = null">
      <button type="button" class="match-detail__close" :aria-label="tx('關閉賽事詳情', 'Close match details')" @click="closeDetail">✕</button>
      <template v-if="detailMatch">
        <p class="match-detail__comp">{{ isEn ? `${compTagLabelEn(detailMatch.competitionTag)} · Round ${detailMatch.roundNo} · ${mapMatchStatusEn(detailMatch.status).label}` : `${compTagLabel(detailMatch.competitionTag)} · 第 ${detailMatch.roundNo} 輪 · ${mapMatchStatus(detailMatch.status).label}` }}</p>
        <h3 id="match-detail-title" class="match-detail__title">
          {{ haCode(detailMatch.homeAway) === 'home' ? clubAssets.shortNameZh : detailMatch.opponent }}
          <span class="match-detail__vs">vs</span>
          {{ haCode(detailMatch.homeAway) === 'home' ? detailMatch.opponent : clubAssets.shortNameZh }}
        </h3>
        <p class="match-detail__time">{{ isEn ? timeOf(detailMatch).weekdayEn : timeOf(detailMatch).weekdayZh }} {{ detailMatch.matchOn }}<template v-if="timeOf(detailMatch).kickoff"> · {{ timeOf(detailMatch).kickoff }}</template></p>
        <p v-if="postponedLine(detailMatch.originalMatchOn, detailMatch.originalKickoff)" class="fixture-card__postponed">{{ postponedLine(detailMatch.originalMatchOn, detailMatch.originalKickoff) }}</p>
        <p class="match-detail__venue">
          <span :class="['ha-pill', haCode(detailMatch.homeAway) === 'home' ? 'ha-pill--home' : 'ha-pill--away']">{{ haCode(detailMatch.homeAway) === 'home' ? tx('主場 HOME', 'Home') : tx('客場 AWAY', 'Away') }}</span>
          <template v-if="detailMatch.venue === 'TBC'">
            <span class="tbc-note">{{ tx('場地未定 · VENUE TBC', 'Venue TBC') }}</span>
          </template>
          <template v-else>
            <span>{{ detailMatch.venue }}</span><a class="fixture-card__map" :href="venueMapUrl(detailMatch.venue ?? '')" target="_blank" rel="noopener">{{ tx('地圖', 'Map') }}<span class="visually-hidden">{{ tx('（另開新視窗）', ' (opens in a new window)') }}</span></a>
          </template>
        </p>
        <div class="match-detail__actions">
          <button type="button" class="btn btn--primary btn--sm" @click="onIcsClick(detailMatch)">{{ tx('加入行事曆 ', 'Add to calendar ') }}<span class="en">.ics</span></button>
          <button type="button" class="btn btn--dark btn--sm" @click="onShareClick(detailMatch)">{{ copiedId === fixtureId(detailMatch.matchOn, detailMatch.homeAway, detailMatch.matchNo) ? tx('連結已複製', 'Link copied') : tx('分享此賽事', 'Share this match') }}</button>
        </div>
      </template>
    </dialog>

    <!-- 訂閱 -->
    <div class="sched-subscribe">
      <div class="sched-subscribe__copy">
        <h2 class="section-title" style="color:var(--heading);">{{ tx('訂閱賽程', 'Subscribe to the schedule') }}</h2>
        <p v-if="isEn">Download the full schedule for your current filters as an <span class="en">.ics</span> file and import it into Google Calendar, Apple Calendar or Outlook. You can also press "Add to calendar" on any match card to download that match on its own.</p>
        <p v-else>下載目前篩選結果的完整賽程 <span class="en">.ics</span> 檔，匯入 Google 日曆、Apple 行事曆或 Outlook；也可以在任一場賽事卡片上按「加入行事曆」單獨下載該場比賽。</p>
        <button type="button" class="btn btn--dark" data-ics-bulk-btn @click="onBulkIcs">{{ tx('下載目前檢視賽程 ', 'Download current fixtures ') }}<span class="en">.ics</span></button>
        <p v-if="isEn" class="sched-subscribe__fine">Per-team <span class="en">webcal://</span> subscription links, which keep your personal calendar in sync when fixtures change, are not available yet. For now, please use the <span class="en">.ics</span> download above to get the schedule.</p>
        <p v-else class="sched-subscribe__fine">依隊別自動更新的 <span class="en">webcal://</span> 訂閱網址（訂閱後賽程異動會自動同步至個人行事曆）需要後台持續產生動態行事曆檔案，屬於後續系統開發項目，目前尚未上線；現在請使用上方 <span class="en">.ics</span> 下載功能取得賽程。</p>
      </div>
    </div>

  </div>
</section>

<section class="band grain cta-band" aria-labelledby="sched-cta-title">
  <span class="ghost-num" aria-hidden="true">13</span>
  <div class="band-inner container">
    <h2 class="section-title" id="sched-cta-title">{{ tx('相關連結', 'Related links') }}</h2>
    <div class="cta-grid">
      <a class="cta-card" :href="lp('/zh/club/first-team/')">
        <span class="cta-card__num">3.1</span>
        <span class="cta-card__title">{{ tx('一線隊', 'First Team') }}<span v-if="isTcrfc && !isEn" class="en"> First Team</span></span>
        <p class="cta-card__desc">{{ tx('認識球員名單、教練團與成績積分榜', 'Meet the squad, the coaching staff and the league table') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/teams/')">
        <span class="cta-card__num">4.2</span>
        <!-- S1-19：原本字面寫死「學院隊伍」，藍鯨規劃書 §3.4 本單元是「青年隊」不是學院。 -->
        <span class="cta-card__title">{{ isEn ? 'Our Teams' : (isTcrfc ? '學院隊伍' : '青年隊') }}</span>
        <p class="cta-card__desc">{{ isEn ? `Meet the ${clubAcademyLabel(' / ')} squads` : `${clubAcademyLabel()} 梯隊介紹` }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/join/general/')">
        <span class="cta-card__num">10.7</span>
        <span class="cta-card__title">{{ tx('聯絡我們', 'Contact us') }}</span>
        <p class="cta-card__desc">{{ tx('媒體、球迷或家長的賽程相關詢問', 'Schedule enquiries from media, fans and parents') }}</p>
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
/* S1-19 補完：下一場賽事倒數／訂閱狀態說明，強制各自獨立成一行（flex-basis:100% 是
   在已 flex-wrap 的父層裡讓子項目強制換行的既有技巧，不需要改動外層版面結構）。 */
.sched-teamhead__countdown{ font-size:.85rem; font-weight:800; color:var(--brand-aa); flex-basis:100%; }
.sched-teamhead__subscribe-note{ font-size:.78rem; color:var(--muted); flex-basis:100%; margin-top:.15rem; }

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

/* S1-19 補完：行動版次要篩選收合（規劃書 v3.13 §3.13「行動版次要篩選收合於『篩選』
   按鈕內」）。桌面版：按鈕隱藏、篩選列永遠顯示（既有行為不變）。行動版：篩選列預設
   收合，按鈕顯示，點擊展開（`.is-open` 由 `filtersOpen` 控制）。 */
.sched-filters-toggle{ display:none; }
@media (max-width:720px){
  .sched-filters-toggle{
    display:inline-flex; align-items:center; gap:.4rem; padding:.6rem 1rem; font-size:.82rem; font-weight:700;
    border:1px solid var(--rule); background:var(--paper); color:var(--text); min-height:44px;
  }
  .sched-filters-toggle[aria-expanded="true"]{ background:var(--ink); color:#fff; }
  .sched-controls__selects{ display:none; flex-basis:100%; }
  .sched-controls__selects.is-open{ display:flex; }
}

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
/* S1-19：鍵盤操作可見的焦點樣式（原本只有底線，對比不足）；同一天多場賽事的小數字角標。 */
.cal-day--match a:focus-visible{ outline:2px solid var(--brand-aa); outline-offset:2px; border-radius:2px; }
.cal-day__count{ font-size:.55rem; font-weight:900; color:var(--brand-aa); vertical-align:super; margin-left:1px; }

.sched-subscribe{ margin-top:3rem; padding:clamp(1.75rem,4vw,2.5rem); background:var(--paper-2); border:1px solid var(--rule); max-width:62ch; }
.sched-subscribe h2{ margin-bottom:.75rem; }
.sched-subscribe p{ font-size:.92rem; line-height:1.75; color:var(--text); margin-bottom:1.25rem; }
.sched-subscribe__fine{ font-size:.78rem; color:var(--muted); margin-top:1rem; margin-bottom:0; }

/* S1-19 補完：頁首時區換算提示（規劃書 v3.13 §3.13「時區處理」）。 */
.page-hero__tz-note{ font-size:.85rem; color:var(--muted); margin-top:.5rem; }

/* S1-19 補完：俱樂部活動（規劃書 v3.13 §3.13「資料來源」第二列），沿用 fixture-card
   的既有版面（同一個 class），只新增卡片內文與標籤兩個小元件。 */
.club-events-block{ margin-top:2.5rem; }
.tag--club-event{ background:var(--muted); }
.club-event-card__title{ font-weight:800; font-size:1rem; color:var(--heading); margin-bottom:.4rem; }
.club-event-card__desc{ font-size:.85rem; color:var(--text); line-height:1.6; margin-bottom:.5rem; }

/* S1-19 補完：賽事詳情彈窗用到的按鈕樣式（既有 tcrfc.css 只有 primary／dark／light
   三種，這裡新增一種低強調的外框樣式，供「賽事詳情」這類次要動作使用）。 */
.btn--outline{ background:transparent; color:var(--ink); border:1px solid var(--rule); }
.btn--outline:hover{ background:var(--paper-2); }

/* S1-19 補完：單場賽事詳情彈窗（原生 <dialog>，規劃書 v3.13 §3.13「事件詳情：側邊
   抽屜或彈窗」）。 */
.match-detail{
  position:relative; border:none; padding:clamp(1.5rem,4vw,2.5rem); max-width:32rem; width:calc(100% - 2rem);
  background:var(--paper); box-shadow:0 10px 40px rgba(0,0,0,.25);
}
.match-detail::backdrop{ background:rgba(0,0,0,.5); }
.match-detail__close{
  position:absolute; top:.75rem; right:.75rem; width:2.25rem; height:2.25rem;
  display:inline-flex; align-items:center; justify-content:center;
  background:transparent; border:1px solid var(--rule); font-size:1rem; color:var(--muted);
}
.match-detail__close:hover{ background:var(--paper-2); }
.match-detail__comp{ font-size:.75rem; font-weight:800; letter-spacing:.05em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.5rem; }
.match-detail__title{ font-size:1.2rem; font-weight:900; color:var(--heading); margin:0 0 .6rem; padding-right:2rem; }
.match-detail__vs{ font-size:.75rem; font-weight:800; color:var(--ghost); margin:0 .35rem; }
.match-detail__time{ font-size:.95rem; color:var(--text); margin-bottom:.4rem; }
.match-detail__venue{ display:flex; align-items:center; gap:.6rem; font-size:.9rem; color:var(--text); flex-wrap:wrap; margin:.75rem 0 1.25rem; }
.match-detail__actions{ display:flex; gap:.6rem; flex-wrap:wrap; }
</style>
