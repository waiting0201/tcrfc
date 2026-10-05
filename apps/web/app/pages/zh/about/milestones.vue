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
definePageMeta({ nav: 'about', unit: '02', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()

// 文案依俱樂部切換：hero／SEO 取自 club-copy.ts。藍鯨這一輪不重建本頁的年份
// 篩選時間軸元件（12 年份、資料量與磐石的 3 年份差異太大，須另外設計互動），
// 完整年度大事記改放在「俱樂部歷程」頁（見 history.vue），本頁對藍鯨只顯示
// 指向該頁的說明，不沿用磐石的時間軸內容頂替。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const identity = computed(() => (isEn.value ? getClubIdentityEnFor(clubKey.value) : getClubIdentity(clubKey.value)))
const hero = computed(() => (isEn.value ? (clubKey.value === 'bw' ? MILESTONES_HERO_EN_BW : getMilestonesHeroEn(facts.value)) : MILESTONES_HERO[clubKey.value]))
const seo = computed(() => (isEn.value ? (clubKey.value === 'bw' ? MILESTONES_SEO_EN_BW : MILESTONES_SEO_EN) : MILESTONES_SEO[clubKey.value]))
// 英文版的年份文字取自事實單一來源（成立年份），不在樣板寫死。
const { facts } = useSiteFacts(clubKey.value)

useSeoMeta({
  title: computed(() => seo.value.title),
  description: computed(() => seo.value.description),
})

const { activeYear, isPressed, isPanelHidden } = useYearChips()

// ---- S2-7 輪補上（C5 里程碑，後端公開端點已存在）----
// `GET /api/backend/{club}/milestones?lang=`（後台 C5「里程碑」，只列「顯示於時間軸」者、日期由舊到新）。
// 🔴 前面檔頭「沒有里程碑資源」的說明已過時（後端 E1a 批新增了端點）。處理方式比照 11.3 慈善事蹟：
// 後端有任何一筆里程碑就整頁換成後台資料（兩俱樂部共用同一套時間軸版型，藍鯨也能顯示）；後端沒有（含 API
// 打不到）時，磐石維持下方人工整理的靜態時間軸（真實大事記，過渡內容，應由內容人員補登後台），藍鯨維持既有
// 說明文字。後台資料沒有「分類標籤」欄位（俱樂部／國際／榮譽），所以動態版不顯示標籤行，不臆造分類。
interface MilestoneDto {
  id: string
  happenedOn: string
  title: string | null
  description: string | null
  imageUrl: string | null
  imageAlt: string | null
  imageWidth: number | null
  imageHeight: number | null
}
const { data: milestoneData } = await useFetch<MilestoneDto[]>(`/api/backend/${config.public.club}/milestones`, {
  query: { lang: locale.value },
  key: `milestones-${config.public.club}-${locale.value}`,
})
const apiMilestones = computed(() => (milestoneData.value ?? []).filter((m) => m.title))
const usingApi = computed(() => apiMilestones.value.length > 0)
/** 藍鯨 hero 文案原寫「時間軸尚未依藍鯨資料重建」，後台有里程碑資料時這句就不成立，改用中性說明。 */
const lede = computed(() => (usingApi.value && clubKey.value === 'bw' ? (isEn.value ? MILESTONES_API_LEDE_EN_BW : `${getClubAssets(clubKey.value).shortNameZh}的重要里程碑，可依年份篩選查看。`) : hero.value.lede))
const milestoneYears = computed(() => {
  const groups = new Map<string, MilestoneDto[]>()
  for (const m of apiMilestones.value) {
    const y = m.happenedOn.slice(0, 4)
    groups.set(y, [...(groups.get(y) ?? []), m])
  }
  return [...groups.entries()].sort((a, b) => a[0].localeCompare(b[0])).map(([year, items]) => ({ year, items }))
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/about/')">{{ identity.aboutLabelZh }}</a></li>
      <li aria-current="page">{{ tx('重要里程碑', 'Key Milestones') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <ClubHeroBg :src="siteImg('/assets/img/nav-about.jpg')" width="1600" height="900" />
  <div class="container">
    <p class="page-hero__eyebrow">{{ isEn ? aboutEyebrowEn('2.8', clubKey) : aboutEyebrow('2.8', clubKey) }}</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ lede }}</p>
  </div>
</section>

<section v-if="usingApi" class="band milestones-band" aria-labelledby="milestones-title-api">
  <div class="band-inner container">
    <h2 id="milestones-title-api" class="visually-hidden">{{ tx('重要里程碑時間軸', 'Key milestones timeline') }}</h2>

    <div class="year-filter" role="group" :aria-label="tx('選擇年份', 'Select a year')">
      <button class="year-chip" type="button" data-year="all" :aria-pressed="isPressed('all')" @click="activeYear = 'all'">{{ tx('全部', 'All') }}</button>
      <button v-for="y in milestoneYears" :key="y.year" class="year-chip" type="button" :data-year="y.year" :aria-pressed="isPressed(y.year)" @click="activeYear = y.year">{{ y.year }}</button>
    </div>

    <div class="timeline">
      <section v-for="y in milestoneYears" :id="`milestones-${y.year}`" :key="y.year" class="timeline-year" :data-year-panel="y.year" :hidden="isPanelHidden(y.year)">
        <h3 class="timeline-year__anchor">{{ y.year }}</h3>
        <ol class="timeline-list">
          <li v-for="m in y.items" :key="m.id" :class="['timeline-item', { 'timeline-item--no-media': !m.imageUrl }]">
            <p class="timeline-item__date">{{ m.happenedOn }}</p>
            <div v-if="m.imageUrl" class="timeline-item__media"><img :src="m.imageUrl" :alt="m.imageAlt ?? ''" loading="lazy" :width="m.imageWidth ?? 640" :height="m.imageHeight ?? 427"></div>
            <div class="timeline-item__body">
              <h4 class="timeline-item__title">{{ m.title }}</h4>
              <p v-if="m.description" class="timeline-item__desc">{{ m.description }}</p>
            </div>
          </li>
        </ol>
      </section>
    </div>
  </div>
</section>

<section v-if="!usingApi && clubKey !== 'tcrfc'" class="band milestones-band" aria-labelledby="milestones-title-bw">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="milestones-title-bw">{{ tx('重要里程碑', 'Key Milestones') }}</h2>
    <p v-if="isEn" class="section-lede">The year-filter timeline on this page has not yet been rebuilt from {{ BW_NAME_EN }} data. For the complete year-by-year record from 2014 to 2025, see <a :href="lp('/zh/about/history/')">2.7 Club History</a>.</p>
    <p v-else class="section-lede">本頁的年份篩選時間軸尚未依藍鯨資料重建，完整的 2014～2025 逐年沿革請見 <a :href="lp('/zh/about/history/')">2.7 俱樂部歷程</a>。</p>
  </div>
</section>

<section v-if="!usingApi && clubKey === 'tcrfc'" class="band milestones-band" aria-labelledby="milestones-title">
  <div class="band-inner container">
    <h2 class="visually-hidden" id="milestones-title">{{ tx('重要里程碑時間軸', 'Key milestones timeline') }}</h2>

    <div class="year-filter" role="group" :aria-label="tx('選擇年份', 'Select a year')">
      <button class="year-chip" type="button" data-year="all" :aria-pressed="isPressed('all')" @click="activeYear = 'all'">{{ tx('全部', 'All') }}</button>
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
              <p class="timeline-item__tag">{{ tx('俱樂部 Club', 'Club') }}</p>
              <h4 class="timeline-item__title">{{ tx('台中磐石足球俱樂部成立', 'Taichung Rock FC is founded') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024</p>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('俱樂部 Club', 'Club') }}</p>
              <h4 class="timeline-item__title">{{ tx('全國乙級聯賽冠軍', 'National Second Division champions') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024-11-05</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2024-11-05-international-082.jpg')" :alt="tx('台中磐石與RC Alcobendas達成合作協議', 'Taichung Rock FC and RC Alcobendas reach a cooperation agreement')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('國際 International', 'International') }}</p>
              <h4 class="timeline-item__title">{{ tx('台中磐石與 RC Alcobendas 達成合作協議', 'Taichung Rock FC reaches a cooperation agreement with RC Alcobendas') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024-12-18</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2024-12-18-club-079.jpg')" :alt="tx('台中磐石有條件地通過甲級俱樂部認證', 'Taichung Rock FC conditionally passes top-tier club accreditation')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('俱樂部 Club', 'Club') }}</p>
              <h4 class="timeline-item__title">{{ tx('有條件地通過甲級俱樂部認證', 'Conditionally passes top-tier club accreditation') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2024-12-18</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2024-12-18-club-080.jpg')" :alt="tx('林教練獲最佳教練獎、楊朝景獲金靴獎', '林教練 wins the Best Coach award and 楊朝景 wins the Golden Boot')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('榮譽 Honours', 'Honours') }}</p>
              <h4 class="timeline-item__title">{{ tx('林教練獲最佳教練獎、楊朝景獲金靴獎', '林教練 wins the Best Coach award and 楊朝景 wins the Golden Boot') }}</h4>
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
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-01-07-club-078.jpg')" :alt="tx('台中磐石獲臺中市政府運動局在合作及冠名上的認可', 'Taichung Rock FC recognised by the Taichung City Government Sports Bureau for cooperation and title naming')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('俱樂部 Club', 'Club') }}</p>
              <h4 class="timeline-item__title">{{ tx('獲臺中市政府運動局在合作及冠名上的認可', 'Recognised by the Taichung City Government Sports Bureau for cooperation and title naming') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-04-11</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-04-11-club-061.jpg')" :alt="tx('周宇杰加盟台中磐石', '周宇杰 joins Taichung Rock FC')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('引援 Signing', 'Signing') }}</p>
              <h4 class="timeline-item__title">{{ tx('周宇杰加盟台中磐石', '周宇杰 joins Taichung Rock FC') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-04-11</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-04-11-club-062.jpg')" :alt="tx('廖奕盛加盟台中磐石', '廖奕盛 joins Taichung Rock FC')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('引援 Signing', 'Signing') }}</p>
              <h4 class="timeline-item__title">{{ tx('廖奕盛加盟台中磐石', '廖奕盛 joins Taichung Rock FC') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-04-11</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-04-11-club-063.jpg')" :alt="tx('旅德好手王義友加盟台中磐石', 'Germany-based player 王義友 joins Taichung Rock FC')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('引援 Signing', 'Signing') }}</p>
              <h4 class="timeline-item__title">{{ tx('旅德好手王義友加盟台中磐石', 'Germany-based player 王義友 joins Taichung Rock FC') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-07-25</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-07-25-club-046.jpg')" :alt="tx('2025台中磐石國際足球盃記者會', 'Press conference for the 2025 Taichung Rock FC International Football Cup')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('俱樂部 Club', 'Club') }}</p>
              <h4 class="timeline-item__title">{{ tx('主辦「2025 台中磐石國際足球盃」，舉行賽前記者會', 'Hosts the “2025 Taichung Rock FC International Football Cup” and holds a pre-tournament press conference') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-07-27</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-07-27-international-043.jpg')" :alt="tx('台中磐石與德國 Rot Weiss Ahlen 簽署合作諒解備忘錄', 'Taichung Rock FC signs a memorandum of understanding on cooperation with Rot Weiss Ahlen of Germany')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('國際 International', 'International') }}</p>
              <h4 class="timeline-item__title">{{ tx('與德國 Rot Weiss Ahlen 簽署合作諒解備忘錄', 'Signs a memorandum of understanding on cooperation with Rot Weiss Ahlen of Germany') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-07-30</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-07-30-international-040.jpg')" :alt="tx('台中磐石將與義甲球會 Hellas Verona 簽署合作備忘錄', 'Taichung Rock FC to sign a memorandum of cooperation with Serie A club Hellas Verona')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('國際 International', 'International') }}</p>
              <h4 class="timeline-item__title">{{ tx('與義甲球會 Hellas Verona 簽署合作備忘錄，推動台義足球交流', 'Signs a memorandum of cooperation with Serie A club Hellas Verona to promote Taiwan–Italy football exchange') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-11-03</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-11-03-club-027.jpg')" :alt="tx('陳曉明出任台中磐石足球俱樂部技術顧問', '陳曉明 becomes technical adviser to Taichung Rock FC')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('俱樂部 Club', 'Club') }}</p>
              <h4 class="timeline-item__title">{{ tx('陳曉明出任俱樂部技術顧問', '陳曉明 becomes the club\'s technical adviser') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2025-11-04</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2025-11-04-international-026.jpg')" :alt="tx('台中磐石球員啟程赴義大利訓練，與維羅納合作邁出第一步', 'Taichung Rock FC players leave for training in Italy as cooperation with Verona takes its first step')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('國際 International', 'International') }}</p>
              <h4 class="timeline-item__title">{{ tx('與義甲維羅納合作邁出第一步，球員啟程赴義大利訓練', 'Cooperation with Serie A side Verona takes its first step as players leave for training in Italy') }}</h4>
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
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2026-01-12-community-017.jpg')" :alt="tx('台中磐石攜手Subkarma深耕在地公益，捐贈英語書籍走進潭秀非營利幼兒園', 'Taichung Rock FC partners with Subkarma on local community work, donating English books to the 潭秀非營利幼兒園 non-profit kindergarten')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('社區 Community', 'Community') }}</p>
              <h4 class="timeline-item__title">{{ tx('攜手 Subkarma 深耕在地公益，捐贈英語書籍走進潭秀非營利幼兒園', 'Partners with Subkarma on local community work, donating English books to the 潭秀非營利幼兒園 non-profit kindergarten') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2026-02-06</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2026-02-06-international-016.jpg')" :alt="tx('台中磐石5名球員獲義大利萊尼亞戈點名赴義訓練', '5 Taichung Rock FC players are selected to train in Italy by the Italian club 萊尼亞戈')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('國際 International', 'International') }}</p>
              <h4 class="timeline-item__title">{{ tx('5 名球員獲義大利萊尼亞戈點名赴義訓練', '5 players are selected to train in Italy by the Italian club 萊尼亞戈') }}</h4>
            </div>
          </li>
          <li class="timeline-item">
            <p class="timeline-item__date">2026-08-10</p>
            <div class="timeline-item__media"><img :src="siteImg('/assets/img/news/2026-08-10-international-000.jpg')" :alt="tx('台中磐石與AS Trenčín深化青訓合作', 'Taichung Rock FC deepens youth development cooperation with AS Trenčín')" loading="lazy" width="640" height="427"></div>
            <div class="timeline-item__body">
              <p class="timeline-item__tag">{{ tx('國際 International', 'International') }}</p>
              <h4 class="timeline-item__title">{{ tx('與 AS Trenčín 深化青訓合作，共創台斯足球交流新篇章', 'Deepens youth development cooperation with AS Trenčín, opening a new chapter in Taiwan–Slovakia football exchange') }}</h4>
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
.timeline-item__desc{ font-size:.88rem; line-height:1.7; color:var(--text); white-space:pre-line; }
</style>
