<script setup lang="ts">
// app/pages/zh/club/first-team/player/[slug]/index.vue — 3.1 球員詳情頁（S3-9；取代原本寫死楊朝景示範資料的「範本」頁）
//
// 主站規劃書 §3.3 3.1「點擊進入球員詳情頁（基本資料、生涯數據、本季出賽、相關新聞、影片）」。
// 資料：球員基本資料取自 `GET /{club}/players/{slug}?lang=`（slug 與 id 都接受、大小寫不敏感；別隊球員 404）；
// 逐季數據 `GET /{club}/players/{id}/stats`（新→舊；後台手動輸入的球季數據以手動為準，否則由已結束賽事自動彙總）。
// 🔴 助攻自動彙總時為 `null`（賽事紀錄沒有助攻資料）→ 顯示「—」，不是 0。
// 🔴 照片只用後端給的 `photoUrl`（肖像同意 fail-closed，未同意為 null → 改用隊徽卡）；不顯示生日（未成年球員個資不上網頁）。
// 🔴 找不到（不存在／別隊／別隊球員）不 404，302 回名單頁（App 深連結回退，docs/19 §2）。動態路由不在 collect-routes 的檢查範圍，所以本頁沒有任何固定的磐石字樣。
// 「相關新聞」「影片」兩區塊沒有公開資料來源（球員與文章／影片沒有可查詢的關聯端點），不放空殼；見 README 缺口。
import type { PlayerCareerStatsResponse } from '#shared/utils/standings'
import type { PlayerDto } from '#shared/utils/player'
import { playerPath } from '#shared/utils/player'

definePageMeta({ nav: 'club', unit: '3.1', enReady: true, enReadyBw: true })

const route = useRoute()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const assets = computed(() => getClubAssets(clubKey.value))
const teamCode = computed(() => (clubKey.value === 'tcrfc' ? 'D1' : 'BW1'))
/** 英文版俱樂部名稱：磐石 `CLUB_NAME_EN`、藍鯨簡稱 `BW_NAME_EN`（B-5）。 */
const clubNameEn = computed(() => (clubKey.value === 'tcrfc' ? CLUB_NAME_EN : BW_NAME_EN))
const { lp, locale, isEn, tx } = useLocale()

// 🔴 路由參數 `slug`（App 規劃書 §2.3：`tcrfc://player/{slug}` → `/zh/club/first-team/player/{slug}`）。
// 正規網址是 slug；以 id（UUID）進來的舊連結／搜尋結果解析成功後 **301 到 slug 網址**，canonical 由全站機制依最終路徑產生，
// 所以永遠指向 slug 網址（不會有兩個網址同內容）。參數認不得（不存在、別隊）時**不顯示 404**：App 未安裝的裝置點到深連結
// 一律回退官網、不得顯示錯誤頁（規劃書 §2.3），所以 302 回名單頁（docs/19 §2 的回退規則）。
const slugParam = computed(() => String(route.params.slug ?? '').trim())

const { data: playerData } = await useFetch<PlayerDto | null>(
  () => `/api/backend/${config.public.club}/players/${encodeURIComponent(slugParam.value)}`,
  {
    query: { lang: locale.value },
    key: `player-${config.public.club}-${locale.value}-${slugParam.value.toLowerCase()}`,
    default: () => null,
  },
)
// 只接一線隊球員（梯隊球員有自己的頁面規劃，且此頁文案寫死「一線隊」）。
const player = computed(() => (playerData.value && playerData.value.teamCode === teamCode.value ? playerData.value : null))
if (!player.value) {
  await navigateTo(`${lp('/zh/club/first-team/')}#roster`, { redirectCode: 302, replace: true })
} else if (player.value.slug && player.value.slug !== slugParam.value) {
  // id 網址、或大小寫不同 → 301 到 slug 正規網址（`lp()` 負責 /en/ 前綴）。
  await navigateTo(lp(playerPath(player.value.slug)), { redirectCode: 301, replace: true })
}
const id = computed(() => player.value?.id ?? '')

const { data: career } = await useFetch<PlayerCareerStatsResponse | null>(() => `/api/backend/${config.public.club}/players/${id.value}/stats`, {
  default: () => null,
  immediate: Boolean(player.value),
})
const seasons = computed(() => career.value?.seasons ?? [])
const current = computed(() => seasons.value[0] ?? null)

const FOOT: Record<string, string> = { right: '右腳', left: '左腳', both: '雙腳' }
const footLabel = computed(() => {
  const f = player.value?.preferredFoot
  if (!f) return null
  return isEn.value ? clubFootLabelEn(f) : (FOOT[f.toLowerCase()] ?? f)
})

useSeoMeta({
  title: computed(() => (isEn.value
    ? `${player.value?.shirtNo ? `${player.value.shirtNo} ` : ''}${player.value?.name ?? 'Player'} | First Team | ${clubNameEn.value}`
    : `${player.value?.shirtNo ? `${player.value.shirtNo} ` : ''}${player.value?.name ?? '球員'}｜一線隊｜${assets.value.nameZh}`)),
  description: computed(() => (isEn.value
    ? `Profile and season-by-season appearance statistics for ${clubNameEn.value} First Team player ${player.value?.name ?? ''}.`
    : `${assets.value.nameZh}一線隊球員${player.value?.name ?? ''}的基本資料與逐季出賽數據。`)),
})
/** 場上位置顯示（英文版把 GK／DF／MF／FW 轉成全名）。 */
const positionText = computed(() => (isEn.value ? clubPositionLabelEn(player.value?.position) : (player.value?.position ?? '—')))
const hasFallback = computed(() => hasFallbackLocale(playerData.value))
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/club/')">{{ tx('俱樂部', 'Football Club') }}</a></li>
      <li><a :href="lp('/zh/club/first-team/')">{{ tx('一線隊', 'First Team') }}</a></li>
      <li aria-current="page">{{ player?.name }}</li>
    </ol>
  </div>
</nav>

<LocaleFallbackNotice v-if="isEn && hasFallback" partial />
<section class="player-hero" aria-labelledby="player-name">
  <div class="container player-hero__inner">
    <div class="player-hero__visual clip-card clip-card--on-dark">
      <img v-if="player?.photoUrl" class="player-hero__photo" :src="player.photoUrl" :alt="imgAlt(player.photoAlt, isEn ? `Photo of ${player.name}` : `${player.name} 球員照片`)" v-bind="imgAttrs(player.photoWidth, player.photoHeight)">
      <template v-else>
        <img class="player-hero__crest" :src="assets.headerMark.src" alt="" width="140" height="146" aria-hidden="true">
        <span class="player-hero__num">{{ player?.shirtNo ?? '—' }}</span>
      </template>
    </div>
    <div class="player-hero__info">
      <p class="player-hero__pos">{{ positionText }}</p>
      <h1 id="player-name">{{ player?.name }}</h1>
      <p class="player-hero__meta">{{ tx('一線隊 First Team', 'First Team') }}<template v-if="player?.shirtNo"> · {{ tx('背號', 'No.') }} {{ player.shirtNo }}</template></p>
    </div>
  </div>
</section>

<section id="player-basic" class="band" aria-labelledby="player-basic-title">
  <div class="band-inner container">
    <h2 id="player-basic-title" class="section-title">{{ tx('基本資料', 'Profile') }}</h2>
    <dl class="def-grid">
      <div><dt>{{ tx('背號', 'Shirt number') }}</dt><dd>{{ statCell(player?.shirtNo) }}</dd></div>
      <div><dt>{{ tx('位置', 'Position') }}</dt><dd>{{ positionText }}</dd></div>
      <div><dt>{{ tx('所屬隊伍', 'Team') }}</dt><dd>{{ tx('一線隊 First Team', 'First Team') }}</dd></div>
      <div v-if="player?.nationality"><dt>{{ tx('國籍', 'Nationality') }}</dt><dd>{{ player.nationality }}</dd></div>
      <div v-if="player?.heightCm"><dt>{{ tx('身高', 'Height') }}</dt><dd>{{ player.heightCm }} cm</dd></div>
      <div v-if="player?.weightKg"><dt>{{ tx('體重', 'Weight') }}</dt><dd>{{ player.weightKg }} kg</dd></div>
      <div v-if="footLabel"><dt>{{ tx('慣用腳', 'Preferred foot') }}</dt><dd>{{ footLabel }}</dd></div>
    </dl>
    <p v-if="player?.bio" class="sh-narrative">{{ player.bio }}</p>
  </div>
</section>

<section id="player-season" class="band paper-2-band" aria-labelledby="player-season-title">
  <div class="band-inner container">
    <h2 id="player-season-title" class="section-title">{{ tx('本季出賽', 'This season') }}</h2>
    <template v-if="current">
      <p class="section-lede">{{ current.seasonCode }} {{ tx('賽季', 'season') }}</p>
      <dl class="def-grid">
        <div><dt>{{ tx('出賽', 'Appearances') }}</dt><dd>{{ statCell(current.appearances) }}</dd></div>
        <div><dt>{{ tx('進球', 'Goals') }}</dt><dd>{{ statCell(current.goals) }}</dd></div>
        <div><dt>{{ tx('助攻', 'Assists') }}</dt><dd>{{ statCell(current.assists) }}</dd></div>
        <div><dt>{{ tx('黃牌', 'Yellow cards') }}</dt><dd>{{ statCell(current.yellowCards) }}</dd></div>
        <div><dt>{{ tx('紅牌', 'Red cards') }}</dt><dd>{{ statCell(current.redCards) }}</dd></div>
      </dl>
    </template>
    <p v-else>{{ tx('目前尚無本季出賽紀錄。', 'There are no appearance records for this season yet.') }}</p>
  </div>
</section>

<section id="player-stats" class="band" aria-labelledby="player-stats-title">
  <div class="band-inner container">
    <h2 id="player-stats-title" class="section-title">{{ tx('生涯數據', 'Career statistics') }}</h2>
    <div v-if="seasons.length" class="sh-table-wrap">
      <table class="sh-stats-table">
        <caption class="visually-hidden">{{ player?.name }} {{ tx('逐季數據', 'season-by-season statistics') }}</caption>
        <thead>
          <tr><th scope="col">{{ tx('賽季', 'Season') }}</th><th scope="col">{{ tx('出賽', 'Apps') }}</th><th scope="col">{{ tx('進球', 'Goals') }}</th><th scope="col">{{ tx('助攻', 'Assists') }}</th><th scope="col">{{ tx('黃牌', 'Yellow cards') }}</th><th scope="col">{{ tx('紅牌', 'Red cards') }}</th></tr>
        </thead>
        <tbody>
          <tr v-for="s in seasons" :key="s.seasonCode">
            <td>{{ s.seasonCode }}</td>
            <td>{{ statCell(s.appearances) }}</td>
            <td>{{ statCell(s.goals) }}</td>
            <td :class="{ 'is-null': s.assists === null }">{{ statCell(s.assists) }}</td>
            <td>{{ statCell(s.yellowCards) }}</td>
            <td>{{ statCell(s.redCards) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else>{{ tx('生涯數據準備中，稍後將於本頁公開。', 'Career statistics are being prepared and will be published on this page soon.') }}</p>
    <p v-if="seasons.length" class="field-hint" style="margin-top:.75rem">{{ tx('數據由已結束的賽事紀錄自動彙總，或由後台手動登錄（以手動為準）。賽事紀錄沒有助攻資料，助攻欄「—」表示未記錄，不是 0。', 'Statistics are compiled automatically from completed match records, or entered manually in the back office (manual entry takes precedence). Match records have no assist data, so a dash in the assists column means not recorded, not zero.') }}</p>
  </div>
</section>

<section id="player-cta" class="band grain cta-band" aria-labelledby="player-cta-title">
  <div class="band-inner container">
    <h2 id="player-cta-title" class="visually-hidden">{{ tx('回到一線隊', 'Back to the First Team') }}</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">{{ tx('回到球員名單', 'Back to the squad list') }}</p>
        <p class="cta-card__desc">{{ tx('查看一線隊完整球員名單。', 'See the full First Team squad list.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/#roster')">{{ tx('返回球員名單', 'Return to the squad list') }}</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">{{ tx('球員數據總表', 'Player statistics table') }}</p>
        <p class="cta-card__desc">{{ tx('查看本賽季全隊球員數據與積分榜。', 'See this season\'s statistics for the whole squad and the league table.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/#player-stats')">{{ tx('查看球員數據', 'View player statistics') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
/* 3.1 球員詳情頁專用元件：player-hero / def-grid（原本在「球員詳情頁範本」頁，範本頁移除後由本頁承接） */
.player-hero{ background:var(--ink); color:#fff; padding-block:clamp(3rem,6vw,5rem); }
.player-hero__inner{ display:flex; align-items:center; gap:2.5rem; flex-wrap:wrap; }
.player-hero__visual{
  position:relative; width:160px; height:160px; flex:none; background:var(--ink-2);
  display:flex; align-items:center; justify-content:center; overflow:hidden;
}
.player-hero__photo{ position:absolute; inset:0; width:100%; height:100%; object-fit:cover; }
.player-hero__crest{ position:absolute; inset:0; margin:auto; width:70%; height:70%; object-fit:contain; opacity:.1; }
.player-hero__num{ position:relative; font-size:4rem; font-weight:900; color:var(--brand-bright); font-variant-numeric:tabular-nums; }
.player-hero__pos{ font-size:.78rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-bright); }
.player-hero h1{ font-size:var(--fs-h2); font-weight:900; margin-top:.3rem; letter-spacing:-.01em; }
.player-hero__meta{ margin-top:.75rem; font-size:.92rem; color:var(--muted-dark); display:flex; align-items:center; gap:.6rem; flex-wrap:wrap; }

.def-grid{
  display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:1.25rem;
  margin:1.75rem 0 1.5rem; padding:0; max-width:900px;
}
.def-grid > div{ border-top:2px solid var(--ink); padding-top:.6rem; }
.def-grid dt{ font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--muted); }
.def-grid dd{ margin:.35rem 0 0; font-size:1.05rem; font-weight:700; color:var(--heading); }
</style>
