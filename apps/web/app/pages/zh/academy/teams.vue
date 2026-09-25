<script setup lang="ts">
// app/pages/zh/academy/teams.vue — 由 site/src/pages/zh/academy/teams/index.html 轉來
// （S0-9 資料驅動頁搬遷）。
//
// 🔵 SSR 打真實球員／教練 API（依梯隊代碼 U15／U14／U12 過濾），但目前種子資料
// 28 名球員與 8 名教練全部是 teamCode 'D1'（一線隊），U15／U14／U12 完全沒有
// 任何名單或教練資料（apps/api README「驗收紀錄」：/players?team=... 對不存在
// 的隊別代碼一樣回 200 + 空陣列，不是 404）。這頁因此仍會渲染 mockup 原本的
// 「名單／教練準備中」預留文字——不是沒接 API，是接了 API、API 也確實回傳
// 「目前沒有資料」，兩者剛好一致。之後球員名單一旦補齊 U15/U14/U12 資料，
// 這頁會自動顯示真實名單，不需要再改樣板。
// 「賽程與成績」區塊維持純靜態（含 disabled 的訂閱行事曆按鈕）——mockup 原文
// 明講是「待行事曆模組上線」，屬於功能尚未開發，與名單／教練的「資料尚未到位」
// 是兩件不同的事，不應該混著接。
//
// 分頁籤（ARIA tablist）行為改寫自 mockup 的 31 行 client script，邏輯逐條保留
// （點擊切換、方向鍵／Home／End 鍵盤導覽、切換後 focus 移到該分頁籤）。
definePageMeta({ nav: 'academy', unit: '04' })

const config = useRuntimeConfig()
const club = config.public.club

const [{ data: playersData }, { data: staffData }] = await Promise.all([
  useFetch(`/api/backend/${club}/players`, { query: { pageSize: 200, lang: 'zh' } }),
  useFetch(`/api/backend/${club}/staff`, { query: { pageSize: 200, lang: 'zh' } }),
])

function playersForTeam(team: string) {
  return (playersData.value?.items ?? []).filter((p) => p.teamCode === team)
}
function staffForTeam(team: string) {
  return (staffData.value?.items ?? []).filter((s) => s.teamCodes?.includes(team))
}

const TAB_IDS = ['u15', 'u14', 'u12', 'other'] as const
const active = ref<(typeof TAB_IDS)[number]>('u15')
const tabRefs = ref<Record<string, HTMLElement | null>>({})

function selectTab(id: (typeof TAB_IDS)[number], focus = true) {
  active.value = id
  if (focus) tabRefs.value[id]?.focus()
}

function onTabKeydown(e: KeyboardEvent, index: number) {
  let idx = index
  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') idx = (index + 1) % TAB_IDS.length
  else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') idx = (index - 1 + TAB_IDS.length) % TAB_IDS.length
  else if (e.key === 'Home') idx = 0
  else if (e.key === 'End') idx = TAB_IDS.length - 1
  else return
  e.preventDefault()
  selectTab(TAB_IDS[idx]!)
}

useSeoMeta({
  title: '學院隊伍 Our Teams｜台中磐石足球學院｜台中磐石足球俱樂部',
  description: '台中磐石足球學院 U15／U14／U12 及其他年齡層隊伍——各梯隊名單、教練、賽程與成績（資料收集中），並提供訂閱本隊行事曆功能。',
})

// ⛔ 本頁刻意不輸出 Person JSON-LD（S1-12f，2026-09-25）：梯隊球員是未成年學員。主站規劃書 GEO-02
// 把「未成年素材路徑」列為個資防線，明文「不得因為想讓 AI 多抓一點而放寬」，本路徑也已在 robots.txt
// 對所有爬蟲排除。把未成年者的姓名與照片整理成結構化資料，與這條防線的用意相反。見 docs/14。
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/academy/">足球學院</a></li>
      <li aria-current="page">學院隊伍</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/academy/life-12.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">4.2 Our Teams</p>
    <h1>學院隊伍<span class="en">Our Teams</span></h1>
    <p class="page-hero__lede">
      台中磐石足球學院依年齡分為 U15、U14、U12 及其他年齡層梯隊，各隊皆設有專屬名單、教練、賽程與成績頁面，
      並可訂閱該隊行事曆，掌握每一場訓練與比賽。
    </p>
  </div>
</section>

<section class="band">
  <div class="container">

    <div class="team-tabs" data-team-tabs>
      <div class="team-tabs__list" role="tablist" aria-label="學院隊伍年齡層">
        <button
          :ref="(el) => (tabRefs['u15'] = el as HTMLElement)"
          type="button" role="tab" id="tab-u15" aria-controls="panel-u15"
          :aria-selected="active === 'u15'" class="team-tabs__tab" :tabindex="active === 'u15' ? undefined : -1"
          @click="selectTab('u15', false)" @keydown="onTabKeydown($event, 0)"
        >U15</button>
        <button
          :ref="(el) => (tabRefs['u14'] = el as HTMLElement)"
          type="button" role="tab" id="tab-u14" aria-controls="panel-u14"
          :aria-selected="active === 'u14'" class="team-tabs__tab" :tabindex="active === 'u14' ? 0 : -1"
          @click="selectTab('u14', false)" @keydown="onTabKeydown($event, 1)"
        >U14</button>
        <button
          :ref="(el) => (tabRefs['u12'] = el as HTMLElement)"
          type="button" role="tab" id="tab-u12" aria-controls="panel-u12"
          :aria-selected="active === 'u12'" class="team-tabs__tab" :tabindex="active === 'u12' ? 0 : -1"
          @click="selectTab('u12', false)" @keydown="onTabKeydown($event, 2)"
        >U12</button>
        <button
          :ref="(el) => (tabRefs['other'] = el as HTMLElement)"
          type="button" role="tab" id="tab-other" aria-controls="panel-other"
          :aria-selected="active === 'other'" class="team-tabs__tab" :tabindex="active === 'other' ? 0 : -1"
          @click="selectTab('other', false)" @keydown="onTabKeydown($event, 3)"
        >其他年齡層</button>
      </div>

      <div v-for="team in (['U15', 'U14', 'U12'] as const)" :key="team" class="team-tabs__panel" :id="`panel-${team.toLowerCase()}`" role="tabpanel" :aria-labelledby="`tab-${team.toLowerCase()}`" tabindex="0" :hidden="active !== team.toLowerCase()">

        <div class="grid grid--2" style="margin-top:1.5rem;">
          <div>
            <h3>名單</h3>
            <p v-if="playersForTeam(team).length === 0">名單準備中，稍後將於本頁公布。</p>
            <ul v-else>
              <li v-for="p in playersForTeam(team)" :key="p.id">{{ p.name }}</li>
            </ul>
          </div>
          <div>
            <h3>教練</h3>
            <p v-if="staffForTeam(team).length === 0">教練陣容準備中，稍後將於本頁公布。</p>
            <ul v-else>
              <li v-for="s in staffForTeam(team)" :key="s.id">{{ s.name }}　{{ s.title }}</li>
            </ul>
          </div>
        </div>
        <div style="margin-top:2rem;">
          <h3>賽程與成績</h3>
          <p>賽程與成績準備中，稍後將於本頁公布。</p>
          <button type="button" class="btn btn--dark btn--sm" style="margin-top:1.25rem;" disabled aria-disabled="true">
            訂閱本隊行事曆（待行事曆模組上線）
          </button>
        </div>
      </div>

      <div class="team-tabs__panel" id="panel-other" role="tabpanel" aria-labelledby="tab-other" tabindex="0" :hidden="active !== 'other'">
        <p>其他年齡層梯隊資訊準備中，稍後將於本頁公布。</p>
      </div>
    </div>

  </div>
</section>

<section class="band grain cta-band">
  <div class="container">
    <div class="cta-grid">
      <a class="cta-card" href="/zh/academy/pathway/">
        <span class="cta-card__num">4.3</span>
        <span class="cta-card__title">學院發展路徑</span>
        <p class="cta-card__desc">從 U12 到一線隊／海外的成長路徑</p>
      </a>
      <a class="cta-card" href="/zh/academy/coaches/">
        <span class="cta-card__num">4.5</span>
        <span class="cta-card__title">學院教練團</span>
        <p class="cta-card__desc">認識帶領各梯隊的教練</p>
      </a>
      <a class="cta-card" href="/zh/schedule/">
        <span class="cta-card__num">06</span>
        <span class="cta-card__title">賽事行事曆</span>
        <p class="cta-card__desc">查看俱樂部完整賽事時程</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
</style>
