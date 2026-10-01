<script setup lang="ts">
// app/pages/zh/club/first-team/player/[id]/index.vue — 3.1 球員詳情頁（S3-9；取代原本寫死楊朝景示範資料的「範本」頁）
//
// 主站規劃書 §3.3 3.1「點擊進入球員詳情頁（基本資料、生涯數據、本季出賽、相關新聞、影片）」。
// 資料：球員基本資料取自 `GET /{club}/players?team=`（與一線隊名單同一份，沒有單一球員端點）；
// 逐季數據 `GET /{club}/players/{id}/stats`（新→舊；後台手動輸入的球季數據以手動為準，否則由已結束賽事自動彙總）。
// 🔴 助攻自動彙總時為 `null`（賽事紀錄沒有助攻資料）→ 顯示「—」，不是 0。
// 🔴 照片只用後端給的 `photoUrl`（肖像同意 fail-closed，未同意為 null → 改用隊徽卡）；不顯示生日（未成年球員個資不上網頁）。
// 🔴 找不到（不存在／別隊／別隊球員）一律真 404。動態路由不在 collect-routes 的檢查範圍，所以本頁沒有任何固定的磐石字樣。
// 「相關新聞」「影片」兩區塊沒有公開資料來源（球員與文章／影片沒有可查詢的關聯端點），不放空殼；見 README 缺口。
import type { PlayerCareerStatsResponse } from '#shared/utils/standings'

definePageMeta({ nav: 'club', unit: '3.1' })

const route = useRoute()
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const assets = computed(() => getClubAssets(clubKey.value))
const teamCode = computed(() => (clubKey.value === 'tcrfc' ? 'D1' : 'BW1'))
const { lp, locale } = useLocale()

const id = computed(() => String(route.params.id))
if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id.value)) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

interface PlayerRow {
  id: string
  teamCode: string
  shirtNo: number | null
  position: string | null
  heightCm: number | null
  weightKg: number | null
  nationality: string | null
  preferredFoot: string | null
  name: string | null
  bio: string | null
  photoUrl: string | null
}
const { data: playersData } = await useFetch<{ items: PlayerRow[] }>(`/api/backend/${config.public.club}/players`, {
  query: { team: teamCode.value, pageSize: 100, lang: locale.value },
  key: `first-team-players-${config.public.club}-${locale.value}`,
})
const player = computed(() => (playersData.value?.items ?? []).find(p => p.id.toLowerCase() === id.value.toLowerCase()) ?? null)
if (!player.value) {
  throw createError({ statusCode: 404, statusMessage: 'Not Found' })
}

const { data: career } = await useFetch<PlayerCareerStatsResponse | null>(() => `/api/backend/${config.public.club}/players/${id.value}/stats`, {
  default: () => null,
})
const seasons = computed(() => career.value?.seasons ?? [])
const current = computed(() => seasons.value[0] ?? null)

const FOOT: Record<string, string> = { right: '右腳', left: '左腳', both: '雙腳' }
const footLabel = computed(() => {
  const f = player.value?.preferredFoot
  return f ? (FOOT[f.toLowerCase()] ?? f) : null
})

useSeoMeta({
  title: computed(() => `${player.value?.shirtNo ? `${player.value.shirtNo} ` : ''}${player.value?.name ?? '球員'}｜一線隊｜${assets.value.nameZh}`),
  description: computed(() => `${assets.value.nameZh}一線隊球員${player.value?.name ?? ''}的基本資料與逐季出賽數據。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/club/')">俱樂部</a></li>
      <li><a :href="lp('/zh/club/first-team/')">一線隊</a></li>
      <li aria-current="page">{{ player?.name }}</li>
    </ol>
  </div>
</nav>

<section class="player-hero" aria-labelledby="player-name">
  <div class="container player-hero__inner">
    <div class="player-hero__visual clip-card clip-card--on-dark">
      <img v-if="player?.photoUrl" class="player-hero__photo" :src="player.photoUrl" :alt="`${player.name} 球員照片`" width="160" height="160">
      <template v-else>
        <img class="player-hero__crest" :src="assets.headerMark.src" alt="" width="140" height="146" aria-hidden="true">
        <span class="player-hero__num">{{ player?.shirtNo ?? '—' }}</span>
      </template>
    </div>
    <div class="player-hero__info">
      <p class="player-hero__pos">{{ player?.position ?? '—' }}</p>
      <h1 id="player-name">{{ player?.name }}</h1>
      <p class="player-hero__meta">一線隊 First Team<template v-if="player?.shirtNo"> · 背號 {{ player.shirtNo }}</template></p>
    </div>
  </div>
</section>

<section id="player-basic" class="band" aria-labelledby="player-basic-title">
  <div class="band-inner container">
    <h2 id="player-basic-title" class="section-title">基本資料</h2>
    <dl class="def-grid">
      <div><dt>背號</dt><dd>{{ statCell(player?.shirtNo) }}</dd></div>
      <div><dt>位置</dt><dd>{{ player?.position ?? '—' }}</dd></div>
      <div><dt>所屬隊伍</dt><dd>一線隊 First Team</dd></div>
      <div v-if="player?.nationality"><dt>國籍</dt><dd>{{ player.nationality }}</dd></div>
      <div v-if="player?.heightCm"><dt>身高</dt><dd>{{ player.heightCm }} cm</dd></div>
      <div v-if="player?.weightKg"><dt>體重</dt><dd>{{ player.weightKg }} kg</dd></div>
      <div v-if="footLabel"><dt>慣用腳</dt><dd>{{ footLabel }}</dd></div>
    </dl>
    <p v-if="player?.bio" class="sh-narrative">{{ player.bio }}</p>
  </div>
</section>

<section id="player-season" class="band paper-2-band" aria-labelledby="player-season-title">
  <div class="band-inner container">
    <h2 id="player-season-title" class="section-title">本季出賽</h2>
    <template v-if="current">
      <p class="section-lede">{{ current.seasonCode }} 賽季</p>
      <dl class="def-grid">
        <div><dt>出賽</dt><dd>{{ statCell(current.appearances) }}</dd></div>
        <div><dt>進球</dt><dd>{{ statCell(current.goals) }}</dd></div>
        <div><dt>助攻</dt><dd>{{ statCell(current.assists) }}</dd></div>
        <div><dt>黃牌</dt><dd>{{ statCell(current.yellowCards) }}</dd></div>
        <div><dt>紅牌</dt><dd>{{ statCell(current.redCards) }}</dd></div>
      </dl>
    </template>
    <p v-else>目前尚無本季出賽紀錄。</p>
  </div>
</section>

<section id="player-stats" class="band" aria-labelledby="player-stats-title">
  <div class="band-inner container">
    <h2 id="player-stats-title" class="section-title">生涯數據</h2>
    <div v-if="seasons.length" class="sh-table-wrap">
      <table class="sh-stats-table">
        <caption class="visually-hidden">{{ player?.name }} 逐季數據</caption>
        <thead>
          <tr><th scope="col">賽季</th><th scope="col">出賽</th><th scope="col">進球</th><th scope="col">助攻</th><th scope="col">黃牌</th><th scope="col">紅牌</th></tr>
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
    <p v-else>生涯數據準備中，稍後將於本頁公開。</p>
    <p v-if="seasons.length" class="field-hint" style="margin-top:.75rem">數據由已結束的賽事紀錄自動彙總，或由後台手動登錄（以手動為準）。賽事紀錄沒有助攻資料，助攻欄「—」表示未記錄，不是 0。</p>
  </div>
</section>

<section id="player-cta" class="band grain cta-band" aria-labelledby="player-cta-title">
  <div class="band-inner container">
    <h2 id="player-cta-title" class="visually-hidden">回到一線隊</h2>
    <div class="cta-grid" style="grid-template-columns:repeat(2,minmax(0,1fr))">
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">回到球員名單</p>
        <p class="cta-card__desc">查看一線隊完整球員名單。</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/#roster')">返回球員名單</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.1</p>
        <p class="cta-card__title">球員數據總表</p>
        <p class="cta-card__desc">查看本賽季全隊球員數據與積分榜。</p>
        <a class="btn btn--primary" :href="lp('/zh/club/first-team/#player-stats')">查看球員數據</a>
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
