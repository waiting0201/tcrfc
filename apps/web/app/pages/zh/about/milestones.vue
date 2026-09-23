<script setup lang="ts">
// app/pages/zh/about/milestones.vue — 由 site/src/pages/zh/about/milestones/index.html 轉來
// （S0-9 資料驅動頁搬遷）。
//
// 🔵 這頁內容本身不是 API 資料——查過 apps/api（五組端點：clubs／players／staff／
// news／schedule）與 site/src/data/*.json（players／news／schedule／staff／
// coaches-d1／coaches-academy 六份），都沒有「里程碑」這個資源，時間軸內容在
// mockup 裡本來就是人工寫死在 HTML 裡的精選大事記，不是從 JSON 樣板產生的。
// 這頁要搬的是「共用的 17 行年份篩選 client script」（docs/13-blue-whale-site.md §6，
// 與 charity/impact-stories 共用），已抽成 useYearChips() composable；時間軸本文
// 逐字保留 mockup 內容，屬於「靜態頁」搬遷（搬遷方式同 21 個純靜態頁），不是
// 「資料驅動頁」搬遷——不要誤以為這裡漏接了 API。
definePageMeta({ nav: 'about', unit: '02' })

// 文案依俱樂部切換：hero／SEO 取自 club-copy.ts。藍鯨這一輪不重建本頁的年份
// 篩選時間軸元件（12 年份、資料量與磐石的 3 年份差異太大，須另外設計互動），
// 完整年度大事記改放在「俱樂部歷程」頁（見 history.vue），本頁對藍鯨只顯示
// 指向該頁的說明，不沿用磐石的時間軸內容頂替。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => MILESTONES_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => MILESTONES_SEO[clubKey.value].title),
  description: computed(() => MILESTONES_SEO[clubKey.value].description),
})

const { activeYear, isPressed, isPanelHidden } = useYearChips()
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/about/">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">重要里程碑</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img class="page-hero__bg" src="/assets/img/nav-about.jpg" alt="" width="1600" height="900">
  <div class="container">
    <p class="page-hero__eyebrow">{{ aboutEyebrow('2.8', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section v-if="clubKey !== 'tcrfc'" class="band milestones-band" aria-labelledby="milestones-title-bw">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="milestones-title-bw">重要里程碑</h2>
    <p class="section-lede">本頁的年份篩選時間軸尚未依藍鯨資料重建，完整的 2014～2025 逐年沿革請見 <a href="/zh/about/history/">2.7 俱樂部歷程</a>。</p>
  </div>
</section>

<section v-if="clubKey === 'tcrfc'" class="band milestones-band" aria-labelledby="milestones-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="milestones-title">重要里程碑時間軸</h2>

    <div class="year-filter" role="group" aria-label="選擇年份">
      <button class="year-chip" type="button" data-year="all" :aria-pressed="isPressed('all')" @click="activeYear = 'all'">全部</button>
      <button class="year-chip" type="button" data-year="2024" :aria-pressed="isPressed('2024')" @click="activeYear = '2024'">2024</button>
      <button class="year-chip" type="button" data-year="2025" :aria-pressed="isPressed('2025')" @click="activeYear = '2025'">2025</button>
      <button class="year-chip" type="button" data-year="2026" :aria-pressed="isPressed('2026')" @click="activeYear = '2026'">2026</button>
    </div>

    <div class="timeline">
      <!-- 2024 -->
      <section class="timeline-year" data-year-panel="2024" id="milestones-2024" :hidden="isPanelHidden('2024')">
        <h3 class="timeline-year__anchor">2024</h3>

        <ol class="timeline-list">
          <li class="timeline-item">
            <p class="timeline-item__date">2024</p>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">俱樂部 Club</p>
              <h4 class="timeline-item__title">台中磐石足球俱樂部成立</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024</p>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">俱樂部 Club</p>
              <h4 class="timeline-item__title">全國乙級聯賽冠軍</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024-11-05</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2024-11-05-international-082.jpg" alt="台中磐石與RC Alcobendas達成合作協議" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">國際 International</p>
              <h4 class="timeline-item__title">台中磐石與 RC Alcobendas 達成合作協議</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024-12-18</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2024-12-18-club-079.jpg" alt="台中磐石有條件地通過甲級俱樂部認證" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">俱樂部 Club</p>
              <h4 class="timeline-item__title">有條件地通過甲級俱樂部認證</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024-12-18</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2024-12-18-club-080.jpg" alt="林教練獲最佳教練獎、楊朝景獲金靴獎" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">榮譽 Honours</p>
              <h4 class="timeline-item__title">林教練獲最佳教練獎、楊朝景獲金靴獎</h4>
            </div>
          </li>
        </ol>
      </section>

      <!-- 2025 -->
      <section class="timeline-year" data-year-panel="2025" id="milestones-2025" :hidden="isPanelHidden('2025')">
        <h3 class="timeline-year__anchor">2025</h3>

        <ol class="timeline-list">
          <li class="timeline-item">
            <p class="timeline-item__date">2025-01-07</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-01-07-club-078.jpg" alt="台中磐石獲臺中市政府運動局在合作及冠名上的認可" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">俱樂部 Club</p>
              <h4 class="timeline-item__title">獲臺中市政府運動局在合作及冠名上的認可</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-04-11</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-04-11-club-061.jpg" alt="周宇杰加盟台中磐石" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">引援 Signing</p>
              <h4 class="timeline-item__title">周宇杰加盟台中磐石</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-04-11</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-04-11-club-062.jpg" alt="廖奕盛加盟台中磐石" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">引援 Signing</p>
              <h4 class="timeline-item__title">廖奕盛加盟台中磐石</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-04-11</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-04-11-club-063.jpg" alt="旅德好手王義友加盟台中磐石" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">引援 Signing</p>
              <h4 class="timeline-item__title">旅德好手王義友加盟台中磐石</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-07-25</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-07-25-club-046.jpg" alt="2025台中磐石國際足球盃記者會" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">俱樂部 Club</p>
              <h4 class="timeline-item__title">主辦「2025 台中磐石國際足球盃」，舉行賽前記者會</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-07-27</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-07-27-international-043.jpg" alt="台中磐石與德國 Rot Weiss Ahlen 簽署合作諒解備忘錄" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">國際 International</p>
              <h4 class="timeline-item__title">與德國 Rot Weiss Ahlen 簽署合作諒解備忘錄</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-07-30</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-07-30-international-040.jpg" alt="台中磐石將與義甲球會 Hellas Verona 簽署合作備忘錄" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">國際 International</p>
              <h4 class="timeline-item__title">與義甲球會 Hellas Verona 簽署合作備忘錄，推動台義足球交流</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-11-03</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-11-03-club-027.jpg" alt="陳曉明出任台中磐石足球俱樂部技術顧問" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">俱樂部 Club</p>
              <h4 class="timeline-item__title">陳曉明出任俱樂部技術顧問</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-11-04</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2025-11-04-international-026.jpg" alt="台中磐石球員啟程赴義大利訓練，與維羅納合作邁出第一步" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">國際 International</p>
              <h4 class="timeline-item__title">與義甲維羅納合作邁出第一步，球員啟程赴義大利訓練</h4>
            </div>
          </li>
        </ol>
      </section>

      <!-- 2026 -->
      <section class="timeline-year" data-year-panel="2026" id="milestones-2026" :hidden="isPanelHidden('2026')">
        <h3 class="timeline-year__anchor">2026</h3>

        <ol class="timeline-list">
          <li class="timeline-item">
            <p class="timeline-item__date">2026-01-12</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2026-01-12-community-017.jpg" alt="台中磐石攜手Subkarma深耕在地公益，捐贈英語書籍走進潭秀非營利幼兒園" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">社區 Community</p>
              <h4 class="timeline-item__title">攜手 Subkarma 深耕在地公益，捐贈英語書籍走進潭秀非營利幼兒園</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2026-02-06</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2026-02-06-international-016.jpg" alt="台中磐石5名球員獲義大利萊尼亞戈點名赴義訓練" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">國際 International</p>
              <h4 class="timeline-item__title">5 名球員獲義大利萊尼亞戈點名赴義訓練</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2026-08-10</p>
            <div class="timeline-item__media"><img src="/assets/img/news/2026-08-10-international-000.jpg" alt="台中磐石與AS Trenčín深化青訓合作" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">國際 International</p>
              <h4 class="timeline-item__title">與 AS Trenčín 深化青訓合作，共創台斯足球交流新篇章</h4>
            </div>
          </li>
        </ol>
      </section>
    </div>
  </div>
</section>
</template>

<style>
/* 【2.8 Key Milestones】時間軸元件 + 年份篩選 —— 全站若有其他時間軸需求（例如榮譽時間軸 3.1），建議收進共用 CSS/JS */
.milestones-band{ background:var(--paper); padding-block:clamp(3.5rem,6vw,6rem); }

.year-filter{ display:flex; gap:.6rem; flex-wrap:wrap; margin-bottom:3rem; }
.year-chip{
  min-height:44px; padding:0 1.3rem; font-weight:800; font-size:.88rem;
  border:2px solid var(--rule); color:var(--muted);
  transition:background var(--dur-fast) var(--ease), color var(--dur-fast) var(--ease), border-color var(--dur-fast) var(--ease);
}
.year-chip:hover{ border-color:var(--brand-aa); color:var(--brand-aa); }
.year-chip[aria-pressed="true"]{ background:var(--brand-aa); border-color:var(--brand-aa); color:#fff; }

.timeline{ position:relative; max-width:800px; }
.timeline-year{ margin-bottom:3.5rem; }
.timeline-year:last-child{ margin-bottom:0; }
.timeline-year__anchor{
  font-size:2.4rem; font-weight:900; color:var(--brand-aa); letter-spacing:-.02em;
  margin-bottom:1.5rem; scroll-margin-top:110px;
}

.timeline-list{ position:relative; list-style:none; margin:0; padding:0; }
.timeline-list::before{
  content:""; position:absolute; left:7px; top:.4rem; bottom:.4rem; width:2px; background:var(--rule);
}
.timeline-item{
  position:relative; padding-left:2.5rem; padding-bottom:2rem;
  display:grid; grid-template-columns:auto 1fr; gap:0 1.25rem; align-items:start;
}
.timeline-item:last-child{ padding-bottom:0; }
.timeline-item::before{
  content:""; position:absolute; left:0; top:.35rem; width:16px; height:16px; border-radius:50%;
  background:var(--paper); border:3px solid var(--brand-aa);
}
.timeline-item__date{
  grid-column:1/-1; font-size:.82rem; font-weight:800; color:var(--muted); letter-spacing:.03em;
  display:flex; align-items:center; flex-wrap:wrap; gap:.4rem; margin-bottom:.6rem;
}
.timeline-item__media{ width:140px; flex:none; aspect-ratio:3/2; overflow:hidden; }
.timeline-item__media img{ width:100%; height:100%; object-fit:cover; }
.timeline-item__body{ min-width:0; }
.timeline-item__tag{ font-size:.68rem; font-weight:800; letter-spacing:.06em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.35rem; }
.timeline-item__title{ font-size:1rem; font-weight:800; color:var(--heading); line-height:1.5; }

@media (max-width:520px){
  .timeline-item{ grid-template-columns:1fr; }
  .timeline-item__media{ width:100%; aspect-ratio:16/9; margin-bottom:.75rem; }
}
</style>
