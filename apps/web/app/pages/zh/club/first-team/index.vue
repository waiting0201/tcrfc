<script setup lang="ts">
// app/pages/zh/club/first-team/index.vue — 由 site/src/pages/zh/club/first-team/index.html 轉來
// （S0-9 靜態頁搬遷），S1-15 改為打真實球員／教練／賽程 API。
//
// 🔴 S1-15（2026-09-29）之前，本頁球員名單／教練團／賽程表／成績／榮譽時間軸五個區塊
// 對藍鯨一律用 `v-if="isTcrfc"` 整段隱藏，理由寫的是「藍鯨目前這些區塊 0 素材」——
// 這句話在寫下的當下沒有錯，但 db/seed/generate-club-seed-sql.py 其實已經替藍鯨一線隊
// （`BW1`）種了 21 筆真實歷史賽果、真實球員與教練名單（S1-14 首頁改動時已用真實資料
// 驗證過 BW1 的 21 場賽果），只是這一頁從未接上——是「轉述沒有跟著來源更新」的典型案例
// （docs/14-invariants.md「轉述會過期，來源不會」）。本輪改為依俱樂部算出正確的
// `Team.code`（磐石 `D1`／藍鯨 `BW1`，docs/14 踩雷點「BW1 不是第二個 D1」），球員／
// 教練／賽程三個區塊改成資料驅動、兩俱樂部共用同一套樣板，不再整段隱藏。
//
// 「榮譽時間軸」維持 `isTcrfc` 靜態內容——沒有比賽結果或球隊主檔以外的公開 API
// 可以查詢「俱樂部歷史榮譽」（比照 about/milestones.vue 同樣沒有公開 API 的現況），
// 且本頁目前唯一能引用的藍鯨舊站文字（content/blue-whale/club-profile.md）沒有逐年
// 可查證的獎盃時間軸，不得比照磐石那樣編一個出來。
import type { PlayerStatsResponse, StandingsResponse } from '#shared/utils/standings'
import type { PlayerDto } from '#shared/utils/player'
import { playerPath } from '#shared/utils/player'

definePageMeta({ nav: 'club', unit: '3.1' })

const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))
const assets = computed(() => getClubAssets(clubKey.value))

/** 一線隊代碼：磐石 `D1`／藍鯨 `BW1`（docs/14-invariants.md「隊別代號」：BW1 不是第二個
 * D1，全站代號唯一）。 */
const teamCode = computed(() => (isTcrfc.value ? 'D1' : 'BW1'))

const { lp, locale } = useLocale()

// S1-12d 收尾第二輪：hero／SEO／球隊介紹三者都含主場／成立年份／首季頭銜／聯賽事實，
// club-copy.ts 已改為工廠函式，一次呼叫 useSiteFacts(clubKey.value) 即可覆蓋整頁需求
// ——原本固定寫 useSiteFacts('tcrfc') 只是因為當時只有榮譽時間軸（isTcrfc 專屬區塊）
// 用得到，現在 hero／SEO／intro 兩俱樂部都要讀，改成動態帶入目前 club。
const { facts, primaryVenue } = useSiteFacts(clubKey.value)
const hero = computed(() => getFirstTeamHero(clubKey.value, facts.value))
const intro = computed(() => getFirstTeamIntro(clubKey.value, facts.value))

useSeoMeta({
  title: computed(() => getFirstTeamSeo(clubKey.value, facts.value).title),
  description: computed(() => getFirstTeamSeo(clubKey.value, facts.value).description),
})

// SportsTeam JSON-LD（GEO-05／S1-12f）：teamCode 依俱樂部算出（原本寫死 'D1'，藍鯨容器
// 永遠查不到 'D1' 這支球隊，等於白跑一次 API——同一批修正一併處理）。
useSportsTeamSchema(teamCode.value)

// ---- 球員／教練／賽程：真實公開 API（03.1 的三個區塊）。積分榜與球員數據原本沒有公開端點，
// S3-9（2026-10-01）起接上 `standings`／`stats/players`（見下方「S3-9」段與 apps/web/README.md「S3-5／S3-5a／S3-9」節） ----
const club = config.public.club
const [{ data: playersData }, { data: staffData }, { data: scheduleData }] = await Promise.all([
  useFetch<{ items: PlayerDto[] }>(`/api/backend/${club}/players`, { query: { team: teamCode.value, pageSize: 100, lang: locale.value } }),
  useFetch(`/api/backend/${club}/staff`, { query: { team: teamCode.value, pageSize: 100, lang: locale.value } }),
  useFetch(`/api/backend/${club}/schedule`, { query: { team: teamCode.value, pageSize: 100, lang: locale.value } }),
])

const players = computed(() => playersData.value?.items ?? [])
/** 數據總表只有 playerId，網址要用 slug 正規形式：由名單反查（查不到才退回 id，詳情頁會 301 到 slug）。 */
const slugById = computed(() => new Map(players.value.map((p: PlayerDto) => [p.id, p.slug] as const)))

// ---- S3-9：積分榜與球員數據（自動彙總）----
// `GET /{club}/standings`（C4 手動維護或匯入，只有 名次／出賽／積分）與 `GET /{club}/stats/players`
// （只計「已結束」賽事：進球、黃紅牌、出賽＝先發名單＋有事件的替補；**助攻沒有資料來源，自動彙總為 null**，
// 顯示「—」而不是 0；後台手動輸入的球季數據以手動為準）。球季以網址 `?season=` 表示（兩區塊共用同一個球季選單，
// `<form method="get">` 無 JS 也能切換）；不指定時由後端挑「今天落在起訖內」的球季。
// 不放「更多統計」：規劃書只寫「積分榜表格（自動或手動維護）」，沒有勝負平與得失球欄位，後端也沒有，不自行加欄位。
const route = useRoute()
const seasonParam = computed(() => safeSeasonParam(route.query.season))
const [{ data: standings }, { data: playerStats }] = await Promise.all([
  useFetch<StandingsResponse | null>(`/api/backend/${config.public.club}/standings`, {
    query: computed(() => (seasonParam.value ? { season: seasonParam.value } : {})),
    default: () => null,
  }),
  useFetch<PlayerStatsResponse | null>(`/api/backend/${config.public.club}/stats/players`, {
    query: computed(() => ({ team: teamCode.value, lang: locale.value, ...(seasonParam.value ? { season: seasonParam.value } : {}) })),
    default: () => null,
  }),
])
const standingRows = computed(() => standings.value?.items ?? [])
const statRows = computed(() => playerStats.value?.items ?? [])
const seasonOptions = computed(() => [...new Set([...(standings.value?.seasons ?? []), ...(playerStats.value?.seasons ?? [])])])
const shownSeason = computed(() => standings.value?.season?.code ?? playerStats.value?.season?.code ?? seasonParam.value)
const hasManualStat = computed(() => statRows.value.some(r => r.source === 'manual'))

// 榮譽時間軸（C5 榮譽，S2-7 輪補上）：`GET /api/backend/{club}/achievements?team={一線隊代碼}`（年份新到舊）。
// 🔴 前面檔頭「沒有公開 API 可查歷史榮譽」已過時（後端 E1a 批新增）。後端有任何一筆榮譽就換成後台資料
// （兩俱樂部共用同一版型，藍鯨有資料也會顯示）；沒有（含 API 打不到）時，磐石維持下方以 site-facts 單一來源組出
// 的既有一筆（成立首年頭銜），藍鯨整段不顯示（沒有可查證的獎盃資料，不得編造）。
const { data: achievementsData } = await useFetch<Array<{
  id: string
  year: number | null
  teamName: string | null
  competitionName: string | null
  placing: string | null
}>>(`/api/backend/${config.public.club}/achievements`, {
  query: { team: teamCode.value, lang: locale.value },
  key: `achievements-${config.public.club}-${teamCode.value}-${locale.value}`,
})
const achievements = computed(() => achievementsData.value ?? [])

// Person JSON-LD（GEO-05／S1-12f 遺留項目，S2-7 輪補上）：一線隊球員（成年）逐人輸出。
// 🔴 閘門三道，缺一不輸出：①`schemaEligible`（apps/api 單一來源，E-39；只要求姓名）；
// ②**已知未滿 18 歲者一律不輸出**（GEO-02 個資防線：未成年素材不給 AI 爬蟲，主站規劃書；一線隊名單
// 理論上是成年球員，但名單可能含 17 歲的高中生簽約球員，不能只靠「一線隊＝成年」的推論）——出生日期
// 沒填的視為依球隊性質成年，這是判斷不是規格，見 README「S2-7」節規格疑點；③`image` 只在 `photoUrl`
// 有值（＝已同意肖像使用，後端 fail-closed）時才帶。`jobTitle` 只用後台填的位置，沒填不輸出。
function isKnownMinor(birthOn: string | null | undefined): boolean {
  if (!birthOn) return false
  const [y, m, d] = birthOn.split('-').map(Number)
  if (!y || !m || !d) return false
  const now = new Date()
  let age = now.getUTCFullYear() - y
  if (now.getUTCMonth() + 1 < m || (now.getUTCMonth() + 1 === m && now.getUTCDate() < d)) age -= 1
  return age < 18
}
const siteUrl = (useSiteConfig().url ?? '').replace(/\/$/, '')
watchEffect(() => {
  const eligible = players.value.filter((p: PlayerDto) =>
    p.schemaEligible && p.name && !isKnownMinor(p.birthOn))
  if (eligible.length === 0) return
  useSchemaOrg(
    eligible.map((p: { id: string, slug?: string, name: string, position?: string | null, photoUrl?: string | null }) =>
      definePerson({
        // 唯一 @id（見 about/our-people.vue 同一處說明、docs/18 E-96）：不給就全部合併成站台身分節點。
        '@id': `player-${p.id}`,
        name: p.name,
        // 詳情頁的正規網址（slug）；沒有 slug 時不輸出 url，不拿 id 網址頂替（id 網址會 301，不是正規形式）。
        url: p.slug ? `${siteUrl}${lp(playerPath(p.slug))}` : undefined,
        jobTitle: p.position ?? undefined,
        image: p.photoUrl ?? undefined,
      }),
    ),
  )
})
const coaches = computed(() => staffData.value?.items ?? [])
const fixtures = computed(() => scheduleData.value?.items ?? [])
const results = computed(() => fixtures.value.filter((m) => mapMatchStatus(m.status).code === 'finished'))
/** 尚無已完賽數據時，「成績與積分榜」改顯示下一場的動態占位文字（比照
 * app/pages/zh/index.vue 首頁「最新賽事區」既有的 upcomingMatch 計算邏輯，
 * 這裡不重複 import 那支 composable，直接在本頁算一次即可）。 */
const nextScheduledFixture = computed(() =>
  fixtures.value.find((m) => mapMatchStatus(m.status).code === 'upcoming'),
)

function homeAwayLabel(homeAway: string | null): string {
  if (homeAway === 'home') return '主場'
  if (homeAway === 'away') return '客場'
  return '—'
}

/** 賽程表「賽事」欄的對戰組合文字，依主客場把自家隊名排在正確的一邊。 */
function matchupLabel(m: { homeAway: string | null; opponent: string | null }): string {
  const self = identity.value.shortNameZh
  const opponent = m.opponent ?? 'TBC'
  if (m.homeAway === 'away') return `${opponent} vs ${self}`
  return `${self} vs ${opponent}`
}

function formatMatchDate(dateStr: string): string {
  const [y, m, d] = dateStr.split('-')
  return `${y}/${m}/${d}（${matchWeekday(dateStr).zh}）`
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/club/')">俱樂部</a></li>
      <li aria-current="page">一線隊</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無一線隊合影照片可用（客戶尚未提供，肖像同意狀態未知），不沿用磐石球員合影頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/club/first-team-01-squad.jpg')" :alt="`台中磐石一線隊球員於${primaryVenue.nameZh}合影`" width="1920" height="1280">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true"></div>
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '3.1 First Team' : '3.1' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band" id="team-overview" aria-labelledby="team-overview-title">
  <div class="band-inner container">
    <div class="prose">
      <h2 id="team-overview-title">球隊介紹</h2>
      <p>{{ intro }}</p>
    </div>
  </div>
</section>

<section class="band paper-2-band" id="roster" aria-labelledby="roster-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">SQUAD</p>
        <h2 class="section-title" id="roster-title">球員名單</h2>
      </div>
      <p class="section-lede">{{ players.length }} 名一線隊註冊球員，依背號排序。未取得肖像使用同意的球員以隊徽卡呈現，不顯示照片。</p>
    </div>

    <p v-if="players.length === 0" class="roster-note">名單準備中，稍後將於本頁公布。</p>
    <div v-else class="player-grid" id="player-grid">
      <article v-for="p in players" :key="p.id" class="player-card clip-card" :data-pos="p.position">
        <div class="player-card__visual">
          <img v-if="p.photoUrl" :src="p.photoUrl" :alt="`${p.name} 球員照片`" width="300" height="300" loading="lazy" style="position:absolute; inset:0; width:100%; height:100%; object-fit:cover;">
          <img class="player-card__crest" :src="assets.headerMark.src" alt="" width="64" height="67" aria-hidden="true">
          <span class="player-card__num">{{ p.shirtNo ?? '—' }}</span>
        </div>
        <div class="player-card__body">
          <span class="player-card__pos">{{ p.position ?? '—' }}</span>
          <p class="player-card__name"><a class="sh-player-link" :href="lp(playerPath(p.slug || p.id))">{{ p.name }}</a></p>
        </div>
      </article>
    </div>
    <p v-if="players.length > 0" class="roster-note">2026/27 賽季{{ players.length }}人名單，依背號排序（球員位置分類與篩選未提供）。</p>
  </div>
</section>

<section class="band" id="coaches" aria-labelledby="coaches-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">STAFF</p>
        <h2 class="section-title" id="coaches-title">教練團</h2>
      </div>
    </div>
    <p v-if="coaches.length === 0" class="roster-note">教練陣容準備中，稍後將於本頁公布。</p>
    <div v-else class="coach-grid">
      <article v-for="c in coaches" :key="c.id" class="coach-card clip-card">
        <div class="coach-card__visual">
          <img v-if="c.photoUrl" :src="c.photoUrl" :alt="`${c.name} 教練照片`" width="240" height="150" loading="lazy" style="width:100%; height:100%; object-fit:cover;">
          <img v-else :src="assets.headerMark.src" alt="" width="52" height="55" aria-hidden="true">
        </div>
        <div class="coach-card__body">
          <p class="coach-card__role">{{ c.title ?? '教練團成員' }}</p>
          <p class="coach-card__name">{{ c.name }}</p>
        </div>
      </article>
    </div>
  </div>
</section>

<section class="band grain paper-2-band" id="fixtures" aria-labelledby="fixtures-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">SEASON</p>
        <h2 class="section-title" id="fixtures-title">賽程表</h2>
      </div>
      <div class="fixtures-actions">
        <a class="btn btn--dark btn--sm" :href="lp('/zh/schedule/')">查看完整行事曆</a>
      </div>
    </div>
    <p class="fixtures-note">資料與行事曆「一線隊」分類同源，實際時間與場地請以官方最新公告為準（部分場地標示為 <b>TBC</b> 表示尚未確定）。</p>

    <p v-if="fixtures.length === 0" class="fixtures-note">賽程準備中，稍後將於本頁公布。</p>
    <div v-else class="table-wrap">
      <table class="sched-table">
        <caption class="visually-hidden">一線隊賽程</caption>
        <thead>
          <tr>
            <th scope="col">輪次</th>
            <th scope="col">日期</th>
            <th scope="col">開賽</th>
            <th scope="col">主客</th>
            <th scope="col">賽事</th>
            <th scope="col">場地</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="m in fixtures" :key="m.id">
            <td>{{ m.roundNo ?? '—' }}</td>
            <td>{{ formatMatchDate(m.matchOn) }}</td>
            <td>{{ m.kickoff ?? '—' }}</td>
            <td><span :class="['sched-ha', m.homeAway === 'home' ? 'sched-ha--home' : 'sched-ha--away']">{{ homeAwayLabel(m.homeAway) }}</span></td>
            <td>
              {{ matchupLabel(m) }}
              <template v-if="mapMatchStatus(m.status).code === 'finished'">（{{ m.scoreHome }} : {{ m.scoreAway }}）</template>
              <template v-else-if="mapMatchStatus(m.status).code !== 'upcoming'">　{{ mapMatchStatus(m.status).label }}</template>
            </td>
            <td>{{ m.venue ?? 'TBC' }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<section class="band" id="results" aria-labelledby="results-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">RESULTS &amp; STANDINGS</p>
        <h2 class="section-title" id="results-title">成績與積分榜</h2>
      </div>
    </div>

    <template v-if="results.length > 0">
      <ul class="results-list">
        <li v-for="m in results" :key="m.id">
          <span class="results-list__date">{{ formatMatchDate(m.matchOn) }}</span>
          <span class="results-list__matchup">{{ matchupLabel(m) }}</span>
          <span class="results-list__score">{{ m.scoreHome }} : {{ m.scoreAway }}</span>
          <span v-if="m.competitionName" class="results-list__comp">{{ m.competitionName }}</span>
        </li>
      </ul>
    </template>
    <p v-else-if="nextScheduledFixture">
      本季目前尚無已完賽數據，下一場為 {{ formatMatchDate(nextScheduledFixture.matchOn) }}
      {{ homeAwayLabel(nextScheduledFixture.homeAway) }}對{{ nextScheduledFixture.opponent ?? 'TBC' }}。
    </p>
    <p v-else>本季目前尚無已完賽數據，賽程尚未公布。</p>

    <h3 class="sh-subhead" id="standings">積分榜<template v-if="shownSeason">　{{ shownSeason }}</template></h3>
    <form v-if="seasonOptions.length > 1" class="sh-season-form" method="get" :action="route.path">
      <div>
        <label for="season-select">賽季</label>
        <select id="season-select" name="season">
          <option v-for="c in seasonOptions" :key="c" :value="c" :selected="c === shownSeason">{{ c }}</option>
        </select>
      </div>
      <button class="btn btn--dark btn--sm" type="submit">切換賽季</button>
    </form>
    <div v-if="standingRows.length" class="sh-table-wrap">
      <table class="sh-stats-table">
        <caption class="visually-hidden">{{ shownSeason }} 賽季積分榜</caption>
        <thead>
          <tr><th scope="col">名次</th><th scope="col">球隊</th><th scope="col">出賽</th><th scope="col">積分</th></tr>
        </thead>
        <tbody>
          <tr v-for="(r, i) in standingRows" :key="`${r.teamName}-${i}`">
            <td>{{ rankCell(r.rank) }}</td>
            <td>{{ r.teamName }}</td>
            <td :class="{ 'is-null': r.played === null }">{{ statCell(r.played) }}</td>
            <td :class="{ 'is-null': r.points === null }">{{ statCell(r.points) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="fixtures-note" style="margin-top:1rem;">本賽季積分榜尚無資料，後台登錄後將於此公布。</p>
    <p v-if="standingRows.length" class="fixtures-note">
      積分榜由本俱樂部手動維護或匯入聯賽主辦單位公告的資料，實際名次以聯賽公告為準<template v-if="standings?.updatedAt">；最後更新 {{ formatTaipeiDateTime(standings.updatedAt, locale) }}</template>。
    </p>
  </div>
</section>

<section class="band paper-2-band" id="player-stats" aria-labelledby="player-stats-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">PLAYER STATS</p>
        <h2 class="section-title" id="player-stats-title">球員數據</h2>
      </div>
      <p v-if="shownSeason" class="section-lede">{{ shownSeason }} 賽季</p>
    </div>
    <div v-if="statRows.length" class="sh-table-wrap">
      <table class="sh-stats-table">
        <caption class="visually-hidden">{{ shownSeason }} 賽季球員數據</caption>
        <thead>
          <tr>
            <th scope="col">球員</th><th scope="col">背號</th><th scope="col">位置</th>
            <th scope="col">出賽</th><th scope="col">進球</th><th scope="col">助攻</th><th scope="col">黃牌</th><th scope="col">紅牌</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in statRows" :key="r.playerId">
            <td><a class="sh-player-link" :href="lp(playerPath(slugById.get(r.playerId) ?? r.playerId))">{{ r.name ?? '—' }}</a></td>
            <td>{{ statCell(r.shirtNo) }}</td>
            <td>{{ r.position ?? '—' }}</td>
            <td>{{ statCell(r.appearances) }}</td>
            <td>{{ statCell(r.goals) }}</td>
            <td :class="{ 'is-null': r.assists === null }">{{ statCell(r.assists) }}</td>
            <td>{{ statCell(r.yellowCards) }}</td>
            <td>{{ statCell(r.redCards) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="roster-note">本賽季尚無球員數據。</p>
    <p v-if="statRows.length" class="fixtures-note">
      數據由已結束的賽事紀錄自動彙總（出賽＝先發名單，加上雖列替補但該場有進球或黃紅牌者）；賽事紀錄沒有助攻資料，助攻欄顯示「—」表示未記錄，不是 0。
      <template v-if="hasManualStat">部分球員的數據為後台手動登錄，以手動登錄為準。</template>
    </p>
  </div>
</section>

<section v-if="isTcrfc || achievements.length" class="band grain honours-band" id="honours" aria-labelledby="honours-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">01</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">ACHIEVEMENTS</p>
        <h2 class="section-title" id="honours-title">榮譽時間軸</h2>
      </div>
    </div>
    <div class="honours-layout" :style="isTcrfc ? undefined : 'grid-template-columns:1fr'">
      <ol v-if="achievements.length" class="timeline">
        <li v-for="a in achievements" :key="a.id" class="timeline-item">
          <p class="timeline-item__year">{{ a.year ?? '—' }}</p>
          <p class="timeline-item__title">{{ [a.competitionName, a.placing].filter(Boolean).join(' ') }}</p>
          <p v-if="a.teamName" class="timeline-item__desc">{{ a.teamName }}</p>
        </li>
      </ol>
      <ol v-else class="timeline">
        <!-- GEO-03（S1-12d）：成立年份／首季頭銜／聯賽為單一來源 site-facts.ts，不在此重複寫死字面值。 -->
        <li class="timeline-item">
          <p class="timeline-item__year">{{ facts.foundedYear }}</p>
          <p class="timeline-item__title">{{ facts.foundingTitleZh }}</p>
          <p class="timeline-item__desc">俱樂部創立首年即拿下{{ facts.foundingTitleZh }}，隔年晉升{{ facts.league.nameZh }}出賽。</p>
        </li>
      </ol>
      <figure v-if="isTcrfc" class="honours-photo clip-card clip-card--on-dark">
        <img :src="siteImg('/assets/img/club/first-team-02-trophy.jpg')" alt="台中磐石獲得的獎盃，攝於俱樂部榮譽紀錄留影" loading="lazy" width="1920" height="1280">
      </figure>
    </div>
  </div>
</section>

<section class="band grain cta-band" id="first-team-cta" aria-labelledby="first-team-cta-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="first-team-cta-title">加入一線隊</h2>
    <div class="cta-grid">
      <div class="cta-card">
        <p class="cta-card__num">10.1</p>
        <p class="cta-card__title">加入一線隊</p>
        <p class="cta-card__desc">具備競技實力、渴望在企甲聯賽舞台證明自己？我們持續招募一線隊球員。</p>
        <a class="btn btn--primary" :href="lp('/zh/join/player/')">填寫報名表</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.3</p>
        <p class="cta-card__title">試訓場次</p>
        <p class="cta-card__desc">查看近期試訓場次日期、地點與報名方式。</p>
        <a class="btn btn--primary" :href="lp('/zh/club/opportunities/')">前往球員機會</a>
      </div>
      <!-- BW-C1：3.2 已重開（見 shared/utils/units.ts 檔頭），藍鯨版標題與敘述避免
           「系統」這個暗示已建制機構框架的用詞，理由同 club-copy.ts
           getPlayerDevelopmentSeo()／getPlayerDevelopmentHero()。 -->
      <div class="cta-card">
        <p class="cta-card__num">3.2</p>
        <p class="cta-card__title">{{ isTcrfc ? '球員發展系統' : '球員培育重點' }}</p>
        <p class="cta-card__desc">{{ isTcrfc ? '了解一線隊如何透過八大模組培養球員的職業競爭力。' : '了解一線隊如何透過八大面向持續培育球員。' }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/player-development/')">{{ isTcrfc ? '查看發展系統' : '查看培育重點' }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 03.1 First Team — 頁面專用元件
   player-card / coach-card / sched-table / timeline
   已在 3.2（模組卡）與 3.5（故事卡）沿用相近結構，建議收進共用 CSS：
   .player-card 系列、.table-wrap + 資料表樣式、.timeline 系列 */

.paper-2-band{ background:var(--paper-2); }

/* 藍鯨無一線隊合影照片時的純色回退（見 script setup 開頭說明），只用既有 token */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }

/* 球員卡 */
.player-grid{ display:grid; grid-template-columns:repeat(auto-fill,minmax(150px,1fr)); gap:1.1rem; }
.player-card{ background:var(--paper); display:flex; flex-direction:column; }
.player-card__visual{
  position:relative; aspect-ratio:1/1; background:var(--paper-3);
  display:flex; align-items:center; justify-content:center; overflow:hidden;
}
.player-card__crest{ position:absolute; inset:0; margin:auto; width:58%; height:58%; object-fit:contain; opacity:.06; }
.player-card__num{
  position:relative; font-size:2.5rem; font-weight:900; color:var(--brand-aa);
  font-variant-numeric:tabular-nums; letter-spacing:-.02em;
}
.player-card__body{ padding:.85rem .95rem 1.1rem; }
.player-card__pos{ font-size:.62rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--muted); }
.player-card__name{ font-size:.92rem; font-weight:800; color:var(--heading); margin-top:.3rem; line-height:1.3; }
.player-card__en{ display:block; font-size:.7rem; color:var(--muted); margin-top:.15rem; }
.player-card--demo{ position:relative; }
.player-card--demo::before{
  content:"範本 DEMO"; position:absolute; top:.5rem; left:.5rem; z-index:3;
  background:var(--brand-aa); color:#fff; font-size:.6rem; font-weight:800;
  letter-spacing:.06em; padding:.2em .5em;
}
.player-card__link{ display:flex; flex-direction:column; height:100%; color:inherit; }
.player-card__link:hover .player-card__name{ color:var(--brand-deep); }
.player-card__demo-tag{ display:block; font-size:.7rem; font-weight:700; color:var(--brand-aa); margin-top:.3rem; }
.roster-note{ margin-top:1.5rem; font-size:.82rem; color:var(--muted); }
.roster-note b{ color:var(--heading); }

/* 教練卡 */
.coach-grid{ display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:1.5rem; margin-top:2rem; }
.coach-card{ background:var(--paper-2); display:flex; flex-direction:column; }
.coach-card__visual{
  aspect-ratio:16/10; background:var(--paper-3); display:flex; align-items:center; justify-content:center;
}
.coach-card__visual img{ width:56px; height:59px; opacity:.5; }
.coach-card__body{ padding:1.1rem 1.25rem 1.4rem; display:flex; flex-direction:column; gap:.4rem; }
.coach-card__role{ font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); }
.coach-card__name{ font-size:1.05rem; font-weight:800; color:var(--heading); }
.coach-card__name .en{ display:block; font-size:.78rem; font-weight:600; color:var(--muted); margin-top:.15rem; }
.coach-card .pending{ margin-top:.3rem; font-size:.78rem; }

/* 賽程表 */
.fixtures-actions{ display:flex; gap:.75rem; flex-wrap:wrap; }
.fixtures-note{ font-size:.85rem; color:var(--muted); margin:1.25rem 0 1.5rem; max-width:70ch; }
.fixtures-note b{ color:var(--heading); }
.table-wrap{ overflow-x:auto; }
.sched-table{ width:100%; min-width:720px; border-collapse:collapse; font-size:.86rem; }
.sched-table th, .sched-table td{ padding:.85rem 1rem; text-align:left; border-bottom:1px solid var(--rule); white-space:nowrap; }
.sched-table th{ font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--muted); background:var(--paper); }
.sched-table td:nth-child(5){ white-space:normal; font-weight:700; color:var(--heading); }
.sched-table tbody tr:hover{ background:rgba(224,33,138,.04); }
.sched-ha{ display:inline-block; font-size:.68rem; font-weight:800; letter-spacing:.04em; padding:.25em .6em; }
.sched-ha--home{ background:var(--brand-aa); color:#fff; }
.sched-ha--away{ background:var(--paper-2); color:var(--muted); border:1px solid var(--rule); }
.sched-vs{ color:var(--muted); font-weight:600; margin:0 .35em; }

/* 成績列表（S1-15 新增，資料驅動——原本這裡是純靜態一句話，見檔頭說明） */
.results-list{ list-style:none; margin-top:1.5rem; display:flex; flex-direction:column; gap:.75rem; }
.results-list li{ display:flex; flex-wrap:wrap; gap:.5rem 1rem; align-items:baseline; padding:.75rem 1rem; background:var(--paper-2); font-size:.88rem; }
.results-list__date{ font-weight:700; color:var(--muted); min-width:9rem; }
.results-list__matchup{ font-weight:800; color:var(--heading); }
.results-list__score{ font-weight:900; color:var(--brand-aa); font-variant-numeric:tabular-nums; }
.results-list__comp{ font-size:.78rem; color:var(--muted); }

/* 榮譽時間軸 */
.honours-band{ color:#fff; padding-block:clamp(4rem,7vw,6.5rem); }
.honours-band .section-title{ color:#fff; }
.honours-band .ghost-num{ left:var(--edge); bottom:-1.5rem; }
.honours-layout{ display:grid; grid-template-columns:1fr 1fr; gap:3rem; align-items:start; margin-top:2rem; }
.timeline{ list-style:none; border-left:2px solid rgba(255,255,255,.15); margin-left:.4rem; }
.timeline-item{ position:relative; padding:0 0 2.25rem 2.25rem; }
.timeline-item::before{
  content:""; position:absolute; left:-7px; top:.35rem; width:12px; height:12px; background:var(--brand-aa);
}
.timeline-item__year{ font-size:1.9rem; font-weight:900; color:#fff; letter-spacing:-.02em; }
.timeline-item__title{ font-weight:800; color:#fff; margin-top:.25rem; }
.timeline-item__desc{ font-size:.85rem; color:var(--muted-dark); margin-top:.4rem; line-height:1.6; max-width:42ch; }
.timeline-item--pending::before{ background:rgba(255,255,255,.25); }
.timeline-item--pending .pending{ background:rgba(255,255,255,.06); border-color:rgba(255,255,255,.35); color:var(--muted-dark); }
.timeline-item--pending .pending::before{ color:#fff; }
.honours-photo{ aspect-ratio:3/2; overflow:hidden; }
.honours-photo img{ width:100%; height:100%; object-fit:cover; }

@media (max-width:900px){
  .honours-layout{ grid-template-columns:1fr; }
}
@media (max-width:640px){
  .player-grid{ grid-template-columns:repeat(auto-fill,minmax(130px,1fr)); }
  .coach-card__visual img{ width:46px; height:48px; }
}
</style>
