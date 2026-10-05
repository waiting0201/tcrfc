<script setup lang="ts">
// app/pages/zh/academy/teams.vue — 由 site/src/pages/zh/academy/teams/index.html 轉來
// （S0-9 資料驅動頁搬遷），S1-15 補上俱樂部分支與賽程真資料。
//
// 🔵 SSR 打真實球員／教練／賽程 API（依梯隊代碼過濾）。磐石梯隊代碼 U15／U14／U12
// 目前完全沒有建立 Team 主檔（db/seed/generate-club-seed-sql.py 只建了 D1，註解明講
// 「本俱樂部學院球隊本次也未建立，無球員名單佐證需要建隊」），API 對不存在的隊別代碼
// 一樣回 200 + 空陣列，不是 404——這頁因此仍會渲染「準備中」預留文字，不是沒接 API。
// 藍鯨的 BW-U15／BW-U12 有建立 Team 主檔，但同樣 0 名球員與 0 位專屬教練（教練與球員
// 一樣完全沒有球隊資料，見 db/seed 同一段落），賽程也 0 筆——三支 API 現況下對兩俱樂部
// 都是空陣列，畫面行為因此相同，但已經是「真的接了 API、資料本來就還沒進來」，跟
// 過去完全沒呼叫 API 是兩回事。之後任一隊補齊名單／教練／賽程，這頁會自動顯示，
// 不需要再改樣板。
//
// 🔴 S1-15（2026-09-29）之前本頁字面寫死「台中磐石足球學院」與固定 U15／U14／U12
// 三個分頁——bw 容器會顯示錯誤的隊別代碼（磐石的 U15，不是藍鯨的 BW-U15）且多出
// 一個藍鯨沒有的 U14 分頁，是既有缺口。本輪改為讀 shared/utils/club-copy.ts 的
// ACADEMY_TEAM_TABS（磐石 4 個分頁含「其他年齡層」；藍鯨只有 U15／U12 兩個真實隊伍，
// 藍鯨規劃書 §3.4「沿用主站 04 的梯隊版型」），分頁籤本身也改為依此陣列動態產生
// （原本 4 個分頁籤按鈕是各自手刻的 4 段重複標籤，不利於藍鯨只有 2 個分頁的情況）。
//
// 「賽程與成績」區塊本輪改讀真實 `/schedule` API（S1-8／S1-11 後台賽程模組已上線，
// mockup 原文「待行事曆模組上線」的前提已經成立）；「訂閱本隊行事曆」按鈕維持停用
// ——目前只有單場賽事 `.ics` 下載（`GET /{club}/matches/{id}/ics`），沒有整隊／整季
// 訂閱端點（L4 訂閱與匯出留給 S2-6），停用理由沒有改變。
//
// unit 由粗粒度 '04' 改為 '4.2'，理由同 overview.vue。
//
// 分頁籤（ARIA tablist）行為改寫自 mockup 的 31 行 client script，邏輯逐條保留
// （點擊切換、方向鍵／Home／End 鍵盤導覽、切換後 focus 移到該分頁籤）。
definePageMeta({ nav: 'academy', unit: '4.2', enReady: true, enReadyBw: true })

const config = useRuntimeConfig()
const club = config.public.club
const clubKey = computed<'tcrfc' | 'bw'>(() => (club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => (isEn.value ? getClubIdentityEn() : getClubIdentity(clubKey.value)))
const academyCrumb = computed(() => (isEn.value ? getAcademyUnitLabelEn(clubKey.value) : identity.value.academyLabelZh))
// S1-12d 收尾第二輪：hero／SEO／分頁定義三者含梯隊代碼事實，club-copy.ts 已改為工廠函式。
const { facts } = useSiteFacts(clubKey.value)
// C-6／S2-13：主站 /en/ 讀 shared/utils/club-copy-en-acad.ts（英文版文案；分頁 id／teamCode 與中文版相同）。
const hero = computed(() => (isEn.value ? getAcademyTeamsHeroEn(facts.value, clubKey.value) : getAcademyTeamsHero(clubKey.value, facts.value)))
const tabs = computed(() => (isEn.value ? getAcademyTeamTabsEn(facts.value, clubKey.value) : getAcademyTeamTabs(clubKey.value, facts.value)))
/** 有真實 `Team.code` 可查詢的分頁（排除磐石的「其他年齡層」靜態說明分頁）。 */
const teamTabs = computed(() => tabs.value.filter((t) => t.teamCode !== null))

// S1-13：lang 跟隨目前路由語系，見 app/pages/zh/schedule.vue 同一處的說明。
const { locale, lp, isEn, tx } = useLocale()
const [{ data: playersData }, { data: staffData }, { data: scheduleData }] = await Promise.all([
  useFetch(`/api/backend/${club}/players`, { query: { pageSize: 200, lang: locale.value } }),
  useFetch(`/api/backend/${club}/staff`, { query: { pageSize: 200, lang: locale.value } }),
  useFetch(`/api/backend/${club}/schedule`, { query: { pageSize: 200, lang: locale.value } }),
])

function playersForTeam(teamCode: string) {
  return (playersData.value?.items ?? []).filter((p) => p.teamCode === teamCode)
}
function staffForTeam(teamCode: string) {
  return (staffData.value?.items ?? []).filter((s) => s.teamCodes?.includes(teamCode))
}
function matchesForTeam(teamCode: string) {
  return (scheduleData.value?.items ?? []).filter((m) => m.teamCode === teamCode)
}

const active = ref(tabs.value[0]!.id)
const tabRefs = ref<Record<string, HTMLElement | null>>({})

function selectTab(id: string, focus = true) {
  active.value = id
  if (focus) tabRefs.value[id]?.focus()
}

function onTabKeydown(e: KeyboardEvent, index: number) {
  const ids = tabs.value.map((t) => t.id)
  let idx = index
  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') idx = (index + 1) % ids.length
  else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') idx = (index - 1 + ids.length) % ids.length
  else if (e.key === 'Home') idx = 0
  else if (e.key === 'End') idx = ids.length - 1
  else return
  e.preventDefault()
  selectTab(ids[idx]!)
}

const seo = computed(() => (isEn.value ? getAcademyTeamsSeoEn(facts.value, clubKey.value) : getAcademyTeamsSeo(clubKey.value, facts.value)))
useSeoMeta({
  title: computed(() => seo.value.title),
  description: computed(() => seo.value.description),
})

// ⛔ 本頁刻意不輸出 Person JSON-LD（S1-12f，2026-09-25）：梯隊球員是未成年學員。主站規劃書 GEO-02
// 把「未成年素材路徑」列為個資防線，明文「不得因為想讓 AI 多抓一點而放寬」，本路徑也已在 robots.txt
// 對所有爬蟲排除。把未成年者的姓名與照片整理成結構化資料，與這條防線的用意相反。見 docs/14。
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/academy/')">{{ academyCrumb }}</a></li>
      <li aria-current="page">{{ hero.h1Zh }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/academy/life-12.jpg')" alt="" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true"></div>
  <div class="container">
    <p class="page-hero__eyebrow">{{ isTcrfc ? '4.2 Our Teams' : '4.2' }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<LocaleFallbackNotice v-if="isEn && (hasFallbackLocale(playersData) || hasFallbackLocale(staffData) || hasFallbackLocale(scheduleData))" partial />
<section class="band" aria-labelledby="team-tabs-title">
  <div class="container">
    <!-- S1-12e（GEO-07）：分頁面板內的「名單」「教練」「賽程與成績」都是 h3，中間
         沒有 h2，原本從 H1 直接跳到 h3。加一個視覺隱藏的 h2 補上大綱層級，
         比照 zh/academy/coaches.vue 同一輪的修法，不影響版面（分頁按鈕本身另有
         role="tab" 與 aria-controls／aria-labelledby，跟這個 h2 是兩件事，互不取代）。 -->
    <h2 id="team-tabs-title" class="visually-hidden">{{ tx('梯隊名單、教練與賽程', 'Squad rosters, coaches and fixtures') }}</h2>

    <div class="team-tabs" data-team-tabs>
      <div class="team-tabs__list" role="tablist" :aria-label="tx(`${identity.academyShortLabelZh}年齡層`, 'Age groups')">
        <button
          v-for="(tab, idx) in tabs" :key="tab.id"
          :ref="(el) => (tabRefs[tab.id] = el as HTMLElement)"
          type="button" role="tab" :id="`tab-${tab.id}`" :aria-controls="`panel-${tab.id}`"
          :aria-selected="active === tab.id" class="team-tabs__tab" :tabindex="active === tab.id ? undefined : -1"
          @click="selectTab(tab.id, false)" @keydown="onTabKeydown($event, idx)"
        >{{ tab.labelZh }}</button>
      </div>

      <div
        v-for="tab in teamTabs" :key="tab.id"
        class="team-tabs__panel" :id="`panel-${tab.id}`" role="tabpanel"
        :aria-labelledby="`tab-${tab.id}`" tabindex="0" :hidden="active !== tab.id"
      >
        <div class="grid grid--2" style="margin-top:1.5rem;">
          <div>
            <h3>{{ tx('名單', 'Roster') }}</h3>
            <p v-if="playersForTeam(tab.teamCode!).length === 0">{{ tx('名單準備中，稍後將於本頁公布。', 'The roster is being prepared and will be published on this page soon.') }}</p>
            <ul v-else>
              <li v-for="p in playersForTeam(tab.teamCode!)" :key="p.id">{{ p.name }}</li>
            </ul>
          </div>
          <div>
            <h3>{{ tx('教練', 'Coaches') }}</h3>
            <p v-if="staffForTeam(tab.teamCode!).length === 0">{{ tx('教練陣容準備中，稍後將於本頁公布。', 'The coaching lineup is being prepared and will be published on this page soon.') }}</p>
            <ul v-else>
              <li v-for="s in staffForTeam(tab.teamCode!)" :key="s.id">{{ s.name }}{{ tx('　', ' - ') }}{{ s.title }}</li>
            </ul>
          </div>
        </div>
        <div style="margin-top:2rem;">
          <h3>{{ tx('賽程與成績', 'Fixtures & Results') }}</h3>
          <p v-if="matchesForTeam(tab.teamCode!).length === 0">{{ tx('賽程與成績準備中，稍後將於本頁公布。', 'Fixtures and results are being prepared and will be published on this page soon.') }}</p>
          <ul v-else>
            <li v-for="m in matchesForTeam(tab.teamCode!)" :key="m.id">
              {{ m.matchOn }}{{ tx('　', ' | ') }}{{ m.homeAway === 'home' ? tx('主場', 'Home') : m.homeAway === 'away' ? tx('客場', 'Away') : '' }}
              {{ tx('對', 'vs') }} {{ m.opponent }}
              <template v-if="mapMatchStatus(m.status).code === 'finished'">{{ tx('　', ' | ') }}{{ m.scoreHome }} : {{ m.scoreAway }}</template>
            </li>
          </ul>
          <button type="button" class="btn btn--dark btn--sm" style="margin-top:1.25rem;" disabled aria-disabled="true">
            {{ tx('訂閱本隊行事曆（待整隊訂閱功能上線）', 'Subscribe to this squad\'s calendar (available once squad subscriptions launch)') }}
          </button>
        </div>
      </div>

      <div v-if="isTcrfc" class="team-tabs__panel" id="panel-other" role="tabpanel" aria-labelledby="tab-other" tabindex="0" :hidden="active !== 'other'">
        <p>{{ tx('其他年齡層梯隊資訊準備中，稍後將於本頁公布。', 'Information on the other age-group squads is being prepared and will be published on this page soon.') }}</p>
      </div>
    </div>

  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <!-- BW-C1：4.3／4.5 已重開（4.3 是 S2-8 的既有缺口再現，4.5 見
           shared/utils/units.ts 檔頭），移除既有的 isTcrfc 隱藏。 -->
      <a class="cta-card" :href="lp('/zh/academy/pathway/')">
        <span class="cta-card__num">4.3</span>
        <span class="cta-card__title">{{ tx(isTcrfc ? '學院發展路徑' : '青年隊發展路徑', isTcrfc ? 'Academy Pathway' : 'Youth Pathway') }}</span>
        <p class="cta-card__desc">{{ tx(isTcrfc ? '從 U12 到一線隊／海外的成長路徑' : '從 U12 到一線隊的成長路徑', isTcrfc ? 'The pathway from U12 to the First Team and overseas' : 'The pathway from U12 to the First Team') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/academy/coaches/')">
        <span class="cta-card__num">4.5</span>
        <span class="cta-card__title">{{ tx(isTcrfc ? '學院教練團' : '青年隊教練團', 'Coaches') }}</span>
        <p class="cta-card__desc">{{ tx('認識帶領各梯隊的教練', 'Meet the coaches who lead each squad') }}</p>
      </a>
      <a class="cta-card" :href="lp('/zh/schedule/')">
        <span class="cta-card__num">{{ isTcrfc ? '06' : '13' }}</span>
        <span class="cta-card__title">{{ tx('賽事行事曆', 'Schedule') }}</span>
        <p class="cta-card__desc">{{ tx('查看俱樂部完整賽事時程', 'See the full match schedule') }}</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 藍鯨無青年隊訓練照片時的純色回退，只用既有 token（比照 club/first-team/index.vue 既有寫法） */
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
</style>
