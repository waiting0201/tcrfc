<script setup lang="ts">
// app/pages/zh/club/first-team/index.vue — 由 site/src/pages/zh/club/first-team/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: 'club', unit: '3.1' })

// 文案依俱樂部切換：hero／SEO／球隊介紹段落取自 club-copy.ts。下方球員名單、
// 教練團、賽程表、成績、榮譽時間軸都是動態內容（真人真事的名單／賽果，不進
// club-copy.ts）——藍鯨目前這些區塊 0 素材（客戶尚未提供 2025 名單、12 個月
// 賽程，docs/13-blue-whale-site.md §5 擋開發第 3、4 項），一律不顯示，不沿用
// 磐石的球員／賽程資料頂替。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const hero = computed(() => FIRST_TEAM_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => FIRST_TEAM_SEO[clubKey.value].title),
  description: computed(() => FIRST_TEAM_SEO[clubKey.value].description),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/club/">俱樂部</a></li>
      <li aria-current="page">一線隊</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <!-- 藍鯨無一線隊合影照片可用（客戶尚未提供，肖像同意狀態未知），不沿用磐石球員合影頂替 -->
  <img v-if="isTcrfc" class="page-hero__bg" src="/assets/img/club/first-team-01-squad.jpg" alt="" width="1920" height="1280">
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
      <p>{{ FIRST_TEAM_INTRO[clubKey] }}</p>
    </div>
  </div>
</section>

<section v-if="isTcrfc" class="band paper-2-band" id="roster" aria-labelledby="roster-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">SQUAD 2026/27</p>
        <h2 class="section-title" id="roster-title">球員名單</h2>
      </div>
      <p class="section-lede">28 名一線隊註冊球員，依背號排序。目前尚未提供球員照片，卡片以隊徽與背號作為識別視覺。</p>
    </div>

    <div class="player-grid" id="player-grid">
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">4</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">蔡俊昇</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">5</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">周宇杰</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">6</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">孫恩祈</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">7</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">龔致宇</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="FW">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">9</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">FW</span>
            <p class="player-card__name">劉建緯</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">10</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">菲利普・希梅哲克</p>
            <span class="player-card__en">Filip Šimeček</span>
          </div>
        </article>
        <article class="player-card clip-card player-card--demo" data-pos="MF">
          <a class="player-card__link" href="/zh/club/first-team/player/" aria-label="查看 11 號楊朝景 球員詳情頁範本">
            <div class="player-card__visual">
              <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
              <span class="player-card__num">11</span>
            </div>
            <div class="player-card__body">
              <span class="player-card__pos">MF</span>
              <p class="player-card__name">楊朝景</p>
              <span class="player-card__demo-tag">詳情頁範本 →</span>
            </div>
          </a>
        </article>
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">12</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">李毓霖</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">13</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">陳柏崴</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="FW">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">14</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">FW</span>
            <p class="player-card__name">李偉綸</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">16</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">魏志荃</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">18</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">尼基塔・若桑</p>
            <span class="player-card__en">Nichita Josan</span>
          </div>
        </article>
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">24</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">多米尼克・林普瑞希特</p>
            <span class="player-card__en">Dominik Limprecht</span>
          </div>
        </article>
        <article class="player-card clip-card" data-pos="GK">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">25</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">GK</span>
            <p class="player-card__name">伊戈・澤維斯</p>
            <span class="player-card__en">Igor Zavis</span>
          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">27</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">施靖堂</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="FW">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">28</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">FW</span>
            <p class="player-card__name">陳治瑋</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">29</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">江均堯</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">32</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">柯岳廷</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">35</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">李鴻均</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">37</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">梁顥騰</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="FW">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">44</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">FW</span>
            <p class="player-card__name">山內大空</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="MF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">45</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">MF</span>
            <p class="player-card__name">胡淯翔</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">48</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">王義友</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">66</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">曾畇浩</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="GK">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">70</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">GK</span>
            <p class="player-card__name">安德烈亞・柏思祺</p>
            <span class="player-card__en">Andrea Boschi</span>
          </div>
        </article>
        <article class="player-card clip-card" data-pos="FW">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">77</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">FW</span>
            <p class="player-card__name">林偉傑</p>

          </div>
        </article>
        <article class="player-card clip-card" data-pos="DF">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">78</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">DF</span>
            <p class="player-card__name">羅倫佐・柯思達</p>
            <span class="player-card__en">Lorenzo Costa</span>
          </div>
        </article>
        <article class="player-card clip-card" data-pos="GK">
          <div class="player-card__visual">
            <img class="player-card__crest" src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="64" height="67" aria-hidden="true">
            <span class="player-card__num">99</span>
          </div>
          <div class="player-card__body">
            <span class="player-card__pos">GK</span>
            <p class="player-card__name">林駿樺</p>

          </div>
        </article>
    </div>
    <p class="roster-note">2026/27 賽季一線隊 28 人名單，可依守門員、後衛、中場、前鋒篩選。</p>
  </div>
</section>

<section v-if="isTcrfc" class="band" id="coaches" aria-labelledby="coaches-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">STAFF</p>
        <h2 class="section-title" id="coaches-title">教練團</h2>
      </div>
    </div>
    <div class="coach-grid">
      <article class="coach-card clip-card">
        <div class="coach-card__visual">
          <img src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="52" height="55" aria-hidden="true">
        </div>
        <div class="coach-card__body">
          <p class="coach-card__role">總教練 Head Coach</p>
          <p class="coach-card__name">瑪蒂諾<span class="en">Matino Sofia</span></p>
          
        </div>
      </article>
      <article class="coach-card clip-card">
        <div class="coach-card__visual">
          <img src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="52" height="55" aria-hidden="true">
        </div>
        <div class="coach-card__body">
          <p class="coach-card__role">教練 Coach</p>
          <p class="coach-card__name">托馬斯・卡斯泰洛<span class="en">Thomas Castello</span></p>
          
        </div>
      </article>
      <article class="coach-card clip-card">
        <div class="coach-card__visual">
          <img src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="52" height="55" aria-hidden="true">
        </div>
        <div class="coach-card__body">
          <p class="coach-card__role">守門員教練 Goalkeeping Coach</p>
          <p class="coach-card__name">儒利亞諾・羅德里格斯<span class="en">Juliano Rodrigues</span></p>
          
        </div>
      </article>
      <article class="coach-card clip-card">
        <div class="coach-card__visual">
          <img src="/assets/brand/svg/tcrfc-mark-black.svg" alt="" width="52" height="55" aria-hidden="true">
        </div>
        <div class="coach-card__body">
          <p class="coach-card__role">體能教練 Fitness Coach</p>
          <p class="coach-card__name">江奕璠</p>
          
        </div>
      </article>
    </div>
  </div>
</section>

<section v-if="isTcrfc" class="band grain paper-2-band" id="fixtures" aria-labelledby="fixtures-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">2026/27 SEASON</p>
        <h2 class="section-title" id="fixtures-title">賽程表</h2>
      </div>
      <div class="fixtures-actions">
        <a class="btn btn--dark btn--sm" href="/zh/schedule/">查看完整行事曆</a>
        <!-- 這是 public/assets/ics/ 底下的靜態下載檔，不是路由。link-checker 的 ESLint 規則只比對
             .nuxt/link-checker/routes.json 的路由清單、看不到 public/，所以任何指向靜態檔的 href
             都會被判成 "does not match any known route"（該規則只接受 routesFile／rootDir 兩個選項，
             nuxt.config 的 linkChecker.excludeLinks 對它無效，已實測）。
             ⚠️ 2026-09-22 的教訓：這個誤判曾經掩蓋一個真的壞掉的連結——S0-9 搬遷 80 頁時漏把
             site/src/assets/ics/first-team-2026-27.ics 複製到 public/assets/ics/，按鈕真的會 404，
             但這條錯誤長期被當成既有雜訊，沒有人去看它在說什麼。檔案已於同日補上。
             所以這裡用單行排除而不是關掉整條規則：下一個指向不存在靜態檔的連結仍然要被抓到。 -->
        <!-- eslint-disable-next-line link-checker/valid-route -->
        <a class="btn btn--primary btn--sm" href="/assets/ics/first-team-2026-27.ics" download>訂閱一線隊賽程 (.ics)</a>
      </div>
    </div>
    <p class="fixtures-note">資料與行事曆「一線隊」分類同源，共 21 場企業甲級聯賽例行賽，實際時間與場地請以官方最新公告為準（部分場地標示為 <b>TBC</b> 表示尚未確定）。</p>

    <div class="table-wrap">
      <table class="sched-table">
        <caption class="visually-hidden">2026/27 企業甲級聯賽賽程</caption>
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
            <tr>
              <td>1</td>
              <td>2026/09/13（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>高雄先鋒 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>楠梓足球場</td>
            </tr>
            <tr>
              <td>2</td>
              <td>2026/09/20（週日）</td>
              <td>16:30</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 台灣電力</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>3</td>
              <td>2026/10/11（週日）</td>
              <td>16:00</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>陽信北競 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>汐止綜合運動場</td>
            </tr>
            <tr>
              <td>4</td>
              <td>2026/10/18（週日）</td>
              <td>18:30</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>南市台鋼 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>台南市立足球場</td>
            </tr>
            <tr>
              <td>5</td>
              <td>2026/10/25（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 台中FUTURO</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>6</td>
              <td>2026/11/01（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>新北航源 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>輔仁大學足球場</td>
            </tr>
            <tr>
              <td>7</td>
              <td>2026/11/22（週日）</td>
              <td>15:30</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 大同足球</td>
              <td>TBC</td>
            </tr>
            <tr>
              <td>8</td>
              <td>2026/11/29（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 高雄先鋒</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>9</td>
              <td>2026/12/06（週日）</td>
              <td>15:30</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>台灣電力 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>楠梓足球場</td>
            </tr>
            <tr>
              <td>10</td>
              <td>2026/12/13（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 陽信北競</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>11</td>
              <td>2027/01/24（週日）</td>
              <td>16:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 南市台鋼</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>12</td>
              <td>2027/01/31（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>台中FUTURO <span class="sched-vs">vs</span> 台中磐石</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>13</td>
              <td>2027/02/21（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 新北航源</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>14</td>
              <td>2027/02/28（週日）</td>
              <td>16:00</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>大同足球 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>輔仁大學足球場</td>
            </tr>
            <tr>
              <td>15</td>
              <td>2027/03/07（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>高雄先鋒 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>楠梓足球場</td>
            </tr>
            <tr>
              <td>16</td>
              <td>2027/03/14（週日）</td>
              <td>16:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 台灣電力</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>17</td>
              <td>2027/03/21（週日）</td>
              <td>15:30</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>陽信北競 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>汐止綜合運動場</td>
            </tr>
            <tr>
              <td>18</td>
              <td>2027/04/11（週日）</td>
              <td>18:30</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>南市台鋼 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>台南市立足球場</td>
            </tr>
            <tr>
              <td>19</td>
              <td>2027/04/18（週日）</td>
              <td>19:00</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 台中FUTURO</td>
              <td>西屯足球場</td>
            </tr>
            <tr>
              <td>20</td>
              <td>2027/04/25（週日）</td>
              <td>15:30</td>
              <td><span class="sched-ha sched-ha--away">客場</span></td>
              <td>新北航源 <span class="sched-vs">vs</span> 台中磐石</td>
              <td>台北田徑場</td>
            </tr>
            <tr>
              <td>21</td>
              <td>2027/05/02（週日）</td>
              <td>16:30</td>
              <td><span class="sched-ha sched-ha--home">主場</span></td>
              <td>台中磐石 <span class="sched-vs">vs</span> 大同足球</td>
              <td>西屯足球場</td>
            </tr>
        </tbody>
      </table>
    </div>
  </div>
</section>

<section v-if="isTcrfc" class="band" id="results" aria-labelledby="results-title">
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">RESULTS &amp; STANDINGS</p>
        <h2 class="section-title" id="results-title">成績與積分榜</h2>
      </div>
    </div>
    <p>2026/27 企甲賽季首場賽事為 2026/09/13 客場對高雄先鋒，本頁建置時尚未開打，因此暫無比賽結果。</p>
    
  </div>
</section>

<section v-if="isTcrfc" class="band grain honours-band" id="honours" aria-labelledby="honours-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">01</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">ACHIEVEMENTS</p>
        <h2 class="section-title" id="honours-title">榮譽時間軸</h2>
      </div>
    </div>
    <div class="honours-layout">
      <ol class="timeline">
        <li class="timeline-item">
          <p class="timeline-item__year">2024</p>
          <p class="timeline-item__title">全國乙級聯賽冠軍</p>
          <p class="timeline-item__desc">俱樂部創立首年即拿下全國乙級聯賽冠軍，隔年晉升企業甲級聯賽出賽。</p>
        </li>
        
      </ol>
      <figure class="honours-photo clip-card clip-card--on-dark">
        <img src="/assets/img/club/first-team-02-trophy.jpg" alt="台中磐石獲得的獎盃，攝於俱樂部榮譽紀錄留影" loading="lazy" width="1920" height="1280">
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
        <a class="btn btn--primary" href="/zh/join/player/">填寫報名表</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.3</p>
        <p class="cta-card__title">試訓場次</p>
        <p class="cta-card__desc">查看近期試訓場次日期、地點與報名方式。</p>
        <a class="btn btn--primary" href="/zh/club/opportunities/">前往球員機會</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">3.2</p>
        <p class="cta-card__title">球員發展系統</p>
        <p class="cta-card__desc">了解一線隊如何透過八大模組培養球員的職業競爭力。</p>
        <a class="btn btn--primary" href="/zh/club/player-development/">查看發展系統</a>
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
