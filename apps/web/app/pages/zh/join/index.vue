<script setup lang="ts">
// app/pages/zh/join/index.vue — 由 site/src/pages/zh/join/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: '', unit: '10', enReady: true, enReadyBw: true })

const { lp, isEn, tx } = useLocale()

// 文案依俱樂部切換：hero／SEO 與 10.2 卡片取自 club-copy.ts（藍鯨依
// docs/13-blue-whale-site.md §3 用「青年隊」，不沿用磐石學院的招生用詞）。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const hero = computed(() => (isEn.value ? (isTcrfc.value ? JOIN_INDEX_HERO_EN : JOIN_INDEX_HERO_EN_BW) : JOIN_INDEX_HERO[clubKey.value]))
// S1-12d 收尾第二輪：10.2 卡片描述含梯隊代碼事實，club-copy.ts 已改為工廠函式。
const { facts } = useSiteFacts(clubKey.value)
const academyCard = computed(() => (isEn.value ? (isTcrfc.value ? getJoinAcademyCardEn(facts.value) : getJoinAcademyCardEnBw(facts.value)) : getJoinAcademyCard(clubKey.value, facts.value)))
// 藍鯨英文版不得出現 Academy：10.2 按鈕字樣 bw 用 Youth Team（zh 版維持既有字面，不動）。
const academyBtn = computed(() => (isEn.value && !isTcrfc.value ? JOIN_ACADEMY_BUTTON_EN_BW : "Academy & Children's Training"))
const seoEn = computed(() => (isTcrfc.value ? JOIN_INDEX_SEO_EN : JOIN_INDEX_SEO_EN_BW))
const intlDesc = computed(() => JOIN_INTL_DESC[clubKey.value])
const identity = computed(() => getClubIdentity(clubKey.value))
// S0-9n（2026-09-23）：10.6「場地位置與地圖」卡片說明字面寫死「學院場地」，藍鯨站因此
// 殘留磐石 04 單元的舊詞——check-club-brand-leak.mjs 在已宣告完工的保護清單裡抓到。
// 與 SiteHeader 同一種修法：沿用既有欄位 identity.academyShortLabelZh，不新造文案。

useSeoMeta({
  title: computed(() => (isEn.value ? seoEn.value : JOIN_INDEX_SEO[clubKey.value]).title),
  description: computed(() => (isEn.value ? seoEn.value : JOIN_INDEX_SEO[clubKey.value]).description),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li aria-current="page">{{ tx('加入與聯絡', 'Join / Contact') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="container">
    <p class="page-hero__eyebrow">10 Join / Contact</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede">{{ hero.lede }}</p>
  </div>
</section>

<section class="band grain" aria-labelledby="join-forms-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">09</span>
  <div class="band-inner container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker kicker--on-dark">{{ tx('七種表單', 'Seven forms') }}</p>
        <h2 class="section-title" id="join-forms-title" style="color:#fff">{{ tx('選擇最符合你需求的表單', 'Choose the form that best fits your needs') }}</h2>
      </div>
      <p class="section-lede on-dark">{{ tx('每個表單各自獨立設計欄位，並直接送達對應的負責部門處理。', 'Each form has its own fields and goes straight to the department responsible.') }}</p>
    </div>

    <div class="cta-grid">
      <div class="cta-card">
        <p class="cta-card__num">10.1</p>
        <p class="cta-card__title">{{ tx('加入球隊', 'Join as a Player') }}</p>
        <p class="cta-card__desc">{{ tx('具備競技實力、渴望在企甲聯賽舞台證明自己？我們持續招募一線隊與各梯隊球員。', 'Got the ability and eager to prove yourself in the league? We are always recruiting players for the First Team and our age-group squads.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/player/')">Join as a Player</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.2</p>
        <p class="cta-card__title">{{ academyCard.titleZh }}</p>
        <p class="cta-card__desc">{{ academyCard.descZh }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/academy/')">{{ academyBtn }}</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.3</p>
        <p class="cta-card__title">{{ tx('營隊報名', 'Camp Registration') }}</p>
        <p class="cta-card__desc">{{ tx('寒暑假期間的短期足球營隊，讓孩子在密集訓練中快速累積比賽經驗。', 'Short football camps during the winter and summer breaks give children intensive training and a quick way to build match experience.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/camp-registration/')">Camp Registration</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.4</p>
        <p class="cta-card__title">International Player Enquiries</p>
        <p class="cta-card__desc">{{ intlDesc }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/international-player/')">International Enquiries</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.5</p>
        <p class="cta-card__title">{{ tx('合作夥伴與贊助洽詢', 'Partnership & Sponsorship') }}</p>
        <p class="cta-card__desc">{{ tx('長期合作夥伴關係或特定贊助方案，都在同一份表單洽詢，由商務部統一接手。', 'Long-term partnerships or specific sponsorship packages are all handled through one form, managed by our Partnerships Department.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/partnership/')">Partnership &amp; Sponsorship</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.6</p>
        <p class="cta-card__title">{{ tx('媒體詢問', 'Media Enquiries') }}</p>
        <p class="cta-card__desc">{{ tx('採訪邀約、新聞稿需求或媒體合作，公關團隊將盡快回覆採訪相關安排。', 'For interview requests, press release needs or media collaboration, our communications team will reply as soon as possible with interview arrangements.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/media/')">Media Enquiries</a>
      </div>
      <div class="cta-card">
        <p class="cta-card__num">10.7</p>
        <p class="cta-card__title">{{ tx('一般聯絡', 'General Contact') }}</p>
        <p class="cta-card__desc">{{ tx('以上分類都不符合你的需求？透過一般聯絡表單，我們會轉交給對應窗口。', 'None of the categories above fit? Use the general contact form and we will pass your message to the right person.') }}</p>
        <a class="btn btn--primary" :href="lp('/zh/join/general/')">General Contact</a>
      </div>
    </div>
  </div>
</section>

<section class="band" aria-labelledby="join-info-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">{{ tx('場地與聯絡資訊', 'Venues and contact details') }}</p>
        <h2 class="section-title" id="join-info-title">{{ tx('找到我們', 'Find us') }}</h2>
      </div>
    </div>
    <div class="grid grid--2">
      <a class="clip-card join-info-card" :href="lp('/zh/join/location/')">
        <p class="join-info-card__num">Location</p>
        <p class="join-info-card__title">{{ tx('場地位置與地圖', 'Location & Map') }}</p>
        <p class="join-info-card__desc"><template v-if="isEn">{{ isTcrfc ? 'Locations and directions for our training base, home ground and Academy venues.' : JOIN_LOCATION_DESC_EN_BW }}</template><template v-else>訓練基地、主場與{{ identity.academyShortLabelZh }}場地的位置與交通指引。</template></p>
      </a>
      <a class="clip-card join-info-card" :href="lp('/zh/join/contact/')">
        <p class="join-info-card__num">Contact</p>
        <p class="join-info-card__title">{{ tx('聯絡資訊', 'Contact Information') }}</p>
        <p class="join-info-card__desc">{{ tx('電話、Email、地址、營業時間與各部門分機。', 'Phone, email, address, opening hours and department extensions.') }}</p>
      </a>
    </div>
  </div>
</section>
</template>

<style>
/* 僅本頁使用：Location／Contact 兩張淺色導覽卡（沿用 .clip-card 切角，但用亮底） */
.join-info-card{
  display:block; padding:2rem clamp(1.5rem,3vw,2.25rem); background:var(--paper-2);
  border:1px solid var(--rule); transition:transform var(--dur) var(--ease), border-color var(--dur) var(--ease);
}
.join-info-card:hover{ transform:translateY(-4px); border-color:var(--brand-aa); }
.join-info-card__num{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.6rem; }
.join-info-card__title{ font-size:1.3rem; font-weight:900; letter-spacing:-.01em; color:var(--heading); margin-bottom:.5rem; }
.join-info-card__desc{ font-size:.9rem; color:var(--muted); line-height:1.6; }
</style>
