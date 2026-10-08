<script setup lang="ts">
// app/pages/zh/culture/manga/index.vue — 8.1 台中磐石漫畫（主站 §3.8，S3-2）
//
// 資料來自後台 F1（`GET {club}/comic/about|characters|episodes|episodes/latest`）。**全部免費開放、不需登入、沒有付費牆**。
// 🔴 藍鯨不設 8.1（藍鯨規劃書 v1.9 §2.1）：頁面宣告 `unit: '8.1'`，unit-gate middleware 對藍鯨容器直接 404；
// 後端對藍鯨一律 403。導覽（SiteHeader／culture hub）也用同一個 `isUnitEnabledForClub('8.1')` 擋，所以前台沒有入口。
// 資料不足時顯示誠實的空狀態（後端種子的 3 集都是草稿，公開列表目前是空的），不放示意集數。
import type { ComicAbout, ComicCharacter, ComicEpisode } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '8.1', enReady: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(club)))

const lang = locale.value
const [{ data: about }, { data: characters }, { data: episodes }, { data: latest }] = await Promise.all([
  useFetch<ComicAbout>(`/api/backend/${club}/comic/about`, { query: { lang }, key: `comic-about-${club}-${lang}` }),
  useFetch<ComicCharacter[]>(`/api/backend/${club}/comic/characters`, { query: { lang }, key: `comic-chars-${club}-${lang}` }),
  useFetch<ComicEpisode[]>(`/api/backend/${club}/comic/episodes`, { query: { lang }, key: `comic-eps-${club}-${lang}` }),
  // 沒有可見集數時後端回 404：視為「沒有最新集數」，不是錯誤
  useFetch<ComicEpisode>(`/api/backend/${club}/comic/episodes/latest`, { query: { lang }, key: `comic-latest-${club}-${lang}` }),
])

// B-20：角色關聯球員 → 連到該球員頁。公開 API 沒有「以 id 查球員」的列表條件，所以取一次球員列表（只含現役）
// 以 id 對出 slug；不在名單內（已離隊／不公開）就不顯示連結，不連到會 404 的頁面。藍鯨不設 8.1，這頁只會在主站出現。
const hasLinkedPlayers = (characters.value ?? []).some(c => c.playerId)
const { data: playersData } = await useFetch<{ items: { id: string, slug: string, name: string | null }[] }>(`/api/backend/${club}/players`, {
  query: { pageSize: 200, lang },
  key: `comic-players-${club}-${lang}`,
  immediate: hasLinkedPlayers,
})
const playerById = computed(() => new Map((playersData.value?.items ?? []).map(p => [p.id.toLowerCase(), p])))
function playerOf(c: ComicCharacter) {
  return c.playerId ? (playerById.value.get(c.playerId.toLowerCase()) ?? null) : null
}

const paragraphs = computed(() => (about.value?.body ?? '').split(/\n{1,}/).map(p => p.trim()).filter(Boolean))
const fmtDate = (d: string | null) => formatPlainDate(d)

useSeoMeta({
  title: computed(() => (isEn.value ? CLUB_MANGA_SEO_EN.title : '台中磐石漫畫 TCRFC Manga｜台中磐石文化｜台中磐石足球俱樂部')),
  description: computed(() => (isEn.value
    ? CLUB_MANGA_SEO_EN.description
    : '台中磐石漫畫是台中磐石足球俱樂部的原創漫畫企劃：世界觀設定、角色卡牆與集數線上閱讀器，全部免費開放、不需登入。')),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li aria-current="page">{{ tx('台中磐石漫畫', 'TCRFC Manga') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">8.1 TCRFC Manga / Comics</p>
    <h1>{{ tx('台中磐石漫畫', 'TCRFC Manga') }}<span v-if="!isEn" class="en">Manga / Comics</span></h1>
    <p v-if="isEn" class="page-hero__lede">An original comic project inspired by Taichung Rock, weaving together character design and player stories. Every episode is <b style="color:#fff">free to read, with no login and no paywall</b>.</p>
    <p v-else class="page-hero__lede">以台中磐石為原型的原創漫畫企劃，角色設定與球員故事交織。全部集數<b style="color:#fff">免費開放、不需登入、無付費牆</b>。</p>
  </div>
</section>

<!-- SPEC 3.8 §8.1 — Latest Episode 最新集數（置頂區塊） -->
<section v-if="latest" id="latest" class="band" aria-labelledby="latest-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">LATEST EPISODE</p>
        <h2 id="latest-title" class="section-title">{{ tx('最新集數', 'Latest episode') }}</h2>
      </div>
    </div>
    <div class="mg-latest">
      <a class="mg-latest__cover" :href="lp(`/zh/culture/manga/${latest.episodeNo}/`)" :aria-label="isEn ? `Read episode ${latest.episodeNo}: ${latest.title}` : `閱讀第 ${latest.episodeNo} 集：${latest.title}`">
        <img v-if="safeImageUrl(latest.coverUrl)" :src="safeImageUrl(latest.coverUrl)!" :alt="isEn ? `Cover of episode ${latest.episodeNo}` : `第 ${latest.episodeNo} 集封面`" width="480" height="640">
        <span v-else>EP{{ String(latest.episodeNo).padStart(2, '0') }}</span>
      </a>
      <div>
        <p class="mg-latest__no">{{ isEn ? `Episode ${latest.episodeNo}` : `第 ${latest.episodeNo} 集` }}</p>
        <h3 class="mg-latest__title">{{ latest.title }}</h3>
        <p v-if="latest.publishedOn" class="mc-note mc-note--small">{{ isEn ? `Published ${fmtDate(latest.publishedOn)}, ${latest.pageCount} pages` : `發布日 ${fmtDate(latest.publishedOn)}　共 ${latest.pageCount} 頁` }}</p>
        <p><a class="btn btn--primary" :href="lp(`/zh/culture/manga/${latest.episodeNo}/`)">{{ tx('開始閱讀', 'Start reading') }}</a></p>
      </div>
    </div>
  </div>
</section>

<!-- SPEC 3.8 §8.1 — About the Project 關於企劃 -->
<section id="about-project" class="band" aria-labelledby="about-project-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">ABOUT THE PROJECT</p>
        <h2 id="about-project-title" class="section-title">{{ about?.title || tx('關於企劃', 'About the project') }}</h2>
      </div>
    </div>
    <div class="prose">
      <p v-for="(p, i) in paragraphs" :key="i">{{ p }}</p>
      <p v-if="paragraphs.length === 0">{{ tx('企劃介紹準備中，稍後將於本頁公開。', 'The project introduction is being prepared and will be published on this page soon.') }}</p>
    </div>
  </div>
</section>

<!-- SPEC 3.8 §8.1 — Characters 角色卡牆 -->
<section id="characters" class="band grain" aria-labelledby="characters-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">CHARACTERS</p>
        <h2 id="characters-title" class="section-title" style="color:#fff">{{ tx('角色介紹', 'Characters') }}</h2>
      </div>
      <p class="section-lede">{{ tx('角色設定可關聯一線隊原型球員。', 'A character can be linked to the First Team player it is modelled on.') }}</p>
    </div>

    <div v-if="(characters ?? []).length > 0" class="grid grid--4 char-wall">
      <article v-for="c in characters" :key="c.id" class="char-card char-card--live">
        <div class="char-card__portrait">
          <img v-if="safeImageUrl(c.imageThumbUrl || c.imageUrl)" :src="safeImageUrl(c.imageThumbUrl || c.imageUrl)!" :alt="c.name" loading="lazy" width="320" height="427">
          <span v-else aria-hidden="true">?</span>
        </div>
        <h3 class="char-card__name">{{ c.name }}</h3>
        <p v-if="c.description" class="char-card__desc">{{ c.description }}</p>
        <p v-if="playerOf(c)" class="char-card__desc"><a :href="lp(playerPath(playerOf(c)!.slug))">{{ tx('原型球員：', 'Based on: ') }}{{ playerOf(c)!.name }} →</a></p>
      </article>
    </div>
    <p v-else class="char-card__pending" style="margin-top:1.5rem;">{{ tx('角色設定尚未公開，敬請期待。', 'The characters have not been revealed yet. Stay tuned.') }}</p>
  </div>
</section>

<!-- SPEC 3.8 §8.1 — Episodes 集數列表 -->
<section id="episodes" class="band" aria-labelledby="episodes-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">EPISODES</p>
        <h2 id="episodes-title" class="section-title">{{ tx('集數列表', 'Episodes') }}</h2>
      </div>
    </div>
    <div v-if="(episodes ?? []).length > 0" class="grid grid--3">
      <article v-for="e in episodes" :key="e.episodeNo" class="ep-card">
        <a class="ep-card__link" :href="lp(`/zh/culture/manga/${e.episodeNo}/`)">
          <div class="ep-card__cover">
            <img v-if="safeImageUrl(e.coverThumbUrl || e.coverUrl)" :src="safeImageUrl(e.coverThumbUrl || e.coverUrl)!" :alt="isEn ? `Cover of episode ${e.episodeNo}` : `第 ${e.episodeNo} 集封面`" loading="lazy" width="480" height="640">
            <span v-else>EP{{ String(e.episodeNo).padStart(2, '0') }}</span>
          </div>
          <p class="ep-card__title">
            <strong>{{ isEn ? `Episode ${e.episodeNo}: ${e.title}` : `第 ${e.episodeNo} 集　${e.title}` }}</strong>
            <span v-if="e.isLatest" class="mc-badge mc-badge--ok" style="margin-left:.5rem;">{{ tx('最新', 'Latest') }}</span>
            <br><span class="mc-note mc-note--small">{{ isEn ? `${fmtDate(e.publishedOn)}, ${e.pageCount} pages` : `${fmtDate(e.publishedOn)}　${e.pageCount} 頁` }}</span>
          </p>
        </a>
      </article>
    </div>
    <p v-else class="is-pending">{{ tx('尚未發布任何集數，敬請期待。', 'No episodes have been published yet. Stay tuned.') }}</p>
  </div>
</section>

<!-- SPEC 3.1 — 頁尾 CTA -->
<section class="band" aria-labelledby="manga-cta-title">
  <div class="container">
    <h2 id="manga-cta-title" class="visually-hidden">{{ tx('加入球迷會看更多台中磐石文化內容', 'Join the Fan Club for more Taichung Rock culture content') }}</h2>
    <div class="grid grid--2">
      <div class="cta-card" style="background:var(--ink)">
        <p class="cta-card__num">8.2</p>
        <p class="cta-card__title">{{ tx('加入台中磐石球迷會', 'Join the TCRFC Fan Club') }}</p>
        <p class="cta-card__desc">{{ tx('成為球迷會成員，優先參與球迷活動，並享有特約店家折扣與入會球衣。', 'Become a Fan Club member to get priority for fan events, partner store discounts and a membership jersey.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/culture/fan-club/')">{{ tx('前往球迷會', 'Go to the Fan Club') }}</a>
      </div>
      <div class="cta-card" style="background:var(--ink)">
        <p class="cta-card__num">8.3</p>
        <p class="cta-card__title">{{ tx('官方商品', 'Merchandise') }}</p>
        <p class="cta-card__desc">{{ tx('把台中磐石漫畫角色與球隊主場球衣一起帶回家。', 'Take the TCRFC Manga characters and the team\'s home jersey home with you.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/culture/merchandise/')">{{ tx('看官方商品', 'View merchandise') }}</a>
      </div>
    </div>
  </div>
</section>
</template>

<style>
.is-pending{ color:var(--muted); font-style:italic; }
/* 角色卡牆（char-wall）：無照片時的骨架卡，建議收進共用 CSS */
.char-wall{ margin-top:1rem; }
.char-card{ background:rgba(255,255,255,.04); border:1px dashed rgba(255,255,255,.25); padding:1.25rem; text-align:center; }
.char-card__portrait{
  aspect-ratio:3/4; display:flex; align-items:center; justify-content:center;
  background:rgba(255,255,255,.05); color:rgba(255,255,255,.3); font-size:2.5rem; font-weight:900;
}
.char-card__pending{ margin-top:.85rem; font-size:.82rem; color:var(--muted-dark); }

/* 集數卡 */
.ep-card{ background:var(--paper-2); border:1px solid var(--rule); }
.ep-card__cover{
  aspect-ratio:3/4; display:flex; align-items:center; justify-content:center;
  background:var(--ink-2); color:rgba(255,255,255,.3); font-weight:900; font-size:1.3rem; letter-spacing:.06em;
}
.ep-card__title{ padding:1rem 1.1rem; font-size:.85rem; }

/* 最新集數 */
.mg-latest{ display:grid; grid-template-columns:minmax(0,14rem) minmax(0,1fr); gap:2rem; align-items:center; margin-top:1.5rem; }
@media (max-width:640px){ .mg-latest{ grid-template-columns:minmax(0,1fr); } }
.mg-latest__cover{ display:flex; aspect-ratio:3/4; align-items:center; justify-content:center; background:var(--ink-2); color:rgba(255,255,255,.3); font-weight:900; overflow:hidden; text-decoration:none; }
.mg-latest__cover img{ width:100%; height:100%; object-fit:cover; display:block; }
.mg-latest__no{ font-size:.8rem; font-weight:800; color:var(--brand-aa); letter-spacing:.06em; }
.mg-latest__title{ font-size:1.4rem; font-weight:900; color:var(--heading); margin:.3rem 0 .75rem; }
.char-card__desc a{ color:inherit; font-weight:700; text-decoration:underline; }
</style>
