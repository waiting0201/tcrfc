<script setup lang="ts">
// app/pages/zh/culture/fan-club/index.vue — 8.2 球迷會（付費會籍的介紹與加入頁，主站 §3.8，S3-2）
//
// 區塊：會籍方案（後台 K2 `membership/plans`）／權益對照（K4，與加入頁、會員中心共用同一元件與資料）／
// 特約店家預告（8.4）／球迷活動列表與回顧（F2 `fan-events`）／球迷會員抽獎（純說明段落，非互動區塊）。
// 兩個俱樂部同一版型（藍鯨規劃書 §1.3、§5.1）；資料依容器的 `club` 由後端分區，藍鯨目前沒有現行方案與活動，
// 顯示誠實的空狀態。BW-C1 當時的「藍鯨尚未推出」整頁空狀態已隨後端資料到位而移除。
import { formatPlainDate, formatTaipeiDateTime } from '#shared/utils/member'
import type { FanEvent } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '8.2', enReady: true, enReadyBw: true })

const { lp, locale, isEn, tx } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const isTcrfc = computed(() => club !== 'bw')
const clubAssets = computed(() => getClubAssets(club))
const identity = computed(() => (isEn.value ? (club === 'bw' ? getClubIdentityEnBw() : getClubIdentityEn()) : getClubIdentity(club)))

const { plans, failed: plansFailed } = await useMembershipPlans()
const lang = locale.value
const [{ data: upcoming }, { data: past }] = await Promise.all([
  useFetch<FanEvent[]>(`/api/backend/${club}/fan-events`, { query: { phase: 'upcoming', lang }, key: `fan-events-${club}-upcoming-${lang}` }),
  useFetch<FanEvent[]>(`/api/backend/${club}/fan-events`, { query: { phase: 'past', lang }, key: `fan-events-${club}-past-${lang}` }),
])
const feeText = (n: number) => `NT$ ${n.toLocaleString('zh-TW')}`
// 過渡內容：磐石既有的 4 張真實活動照片，後台 F2 出現任何一筆「活動回顧」資料即整批退場，不混搭
const showStaticReview = computed(() => isTcrfc.value && (past.value ?? []).length === 0)

useSeoMeta({
  title: computed(() => (isEn.value ? (isTcrfc.value ? CLUB_FAN_CLUB_SEO_EN : CLUB_FAN_CLUB_SEO_EN_BW).title : isTcrfc.value
    ? '台中磐石球迷會 Fan Club｜台中磐石文化｜台中磐石足球俱樂部'
    : `球迷會｜${identity.value.cultureLabelZh}｜${clubAssets.value.nameZh}`)),
  description: computed(() => (isEn.value ? (isTcrfc.value ? CLUB_FAN_CLUB_SEO_EN : CLUB_FAN_CLUB_SEO_EN_BW).description : `加入${clubAssets.value.nameZh}球迷會：會籍方案、會員福利分級對照，以及球迷活動報名與回顧。`)),
})
</script>

<template>
<nav class="breadcrumb" :aria-label="tx('麵包屑', 'Breadcrumb')">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">{{ tx('首頁', 'Home') }}</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li aria-current="page">{{ isEn ? (isTcrfc ? 'TCRFC Fan Club' : 'Fan Club') : (isTcrfc ? '台中磐石球迷會' : '球迷會') }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/fanclub/fanclub-event-04.jpg')" :alt="tx('台中磐石球員、學員與球迷於球場合影留念', 'Taichung Rock players, Academy students and fans posing together at the ground')" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">8.2 Fan Club</p>
    <h1 v-if="isEn">{{ isTcrfc ? 'TCRFC Fan Club' : 'Fan Club' }}</h1>
    <h1 v-else-if="isTcrfc">台中磐石球迷會<span class="en">Fan Club</span></h1>
    <h1 v-else>球迷會<span class="en">Fan Club</span></h1>
    <p v-if="isEn && !isTcrfc" class="page-hero__lede">{{ CLUB_FAN_CLUB_HERO_LEDE_EN_BW }}</p>
    <p v-else-if="isEn" class="page-hero__lede">Cheer with Taichung Rock from the touchline. The Fan Club is the club's paid membership: besides a jersey, you get more discounts at partner stores and priority access to fan events.</p>
    <p v-else class="page-hero__lede">與{{ clubAssets.shortNameZh }}一起在場邊吶喊。球迷會即{{ clubAssets.shortNameZh }}的付費會籍，除了球衣，還能在特約店家享有更多折扣，並優先參與球迷活動。</p>
  </div>
</section>

<!-- SPEC 3.8 §8.2 — Membership Plans 會籍方案 -->
<LocaleFallbackNotice v-if="isEn && hasFallbackLocale(plans)" partial />
<section class="band" id="join" aria-labelledby="join-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">MEMBERSHIP PLANS</p>
        <h2 class="section-title" id="join-title">{{ tx('會籍方案', 'Membership plans') }}</h2>
      </div>
    </div>

    <p v-if="isEn" class="section-lede" style="margin-top:.75rem">
      Fan Club membership is the club's paid membership. It runs by <strong>season</strong>, all memberships expire at the same time, and renewals are handled at the end of the season.
      The fee is paid by LINE Pay payment link or in person, and the club activates the membership once it is paid.
    </p>
    <p v-else class="section-lede" style="margin-top:.75rem">
      球迷會員即{{ clubAssets.shortNameZh }}的付費會籍，以<strong>球季</strong>為單位計算，全體同時到期，於球季末辦理續會。
      會費以 LINE Pay 收款連結或現場收款繳交，完成後由俱樂部開通會籍。
    </p>

    <div v-if="plans.length > 0" class="grid grid--2 plan-grid">
      <article v-for="p in plans" :key="p.code" class="plan-card">
        <h3 class="plan-card__name">{{ p.name }}</h3>
        <p class="plan-card__price">{{ feeText(p.fee) }}{{ isEn ? ` / ${p.seasonCode} season` : `／${p.seasonCode} 球季` }}</p>
        <ul class="plan-card__list">
          <li>{{ isEn ? `${p.cardQuota} digital membership ${p.cardQuota === 1 ? 'card' : 'cards'}` : `電子會員卡 ${p.cardQuota} 張` }}</li>
          <li v-if="p.jerseyQuota > 0">{{ isEn ? `${p.jerseyQuota} membership ${p.jerseyQuota === 1 ? 'jersey' : 'jerseys'}` : `入會球衣 ${p.jerseyQuota} 件` }}</li>
          <li>{{ tx('特約店家折扣，含「限付費會員」品項', 'Partner store discounts, including items for Paid Fan Club members only') }}</li>
          <li>{{ tx('球迷活動優先報名', 'Priority registration for fan events') }}</li>
          <li v-if="p.startsOn || p.endsOn">{{ tx('會籍期間', 'Membership period') }} {{ formatPlainDate(p.startsOn) }} – {{ formatPlainDate(p.endsOn) }}</li>
          <li v-if="p.benefitNote">{{ p.benefitNote }}</li>
          <li v-if="p.midSeasonRule">{{ tx('季中入會：', 'Joining mid-season: ') }}{{ p.midSeasonRule }}</li>
        </ul>
        <a class="btn btn--primary btn--block" :href="lp('/zh/member/')">{{ tx('加入或升級', 'Join or upgrade') }}</a>
      </article>
    </div>
    <div v-else class="pending-note">
      <template v-if="plansFailed">{{ tx('會籍方案暫時無法載入，請稍後再試。', 'Membership plans cannot be loaded right now. Please try again later.') }}</template>
      <template v-else-if="isEn">
        <strong>There are no membership plans open for sign-up at the moment</strong> — once the season and plans (including the fee, and how many cards and jerseys are included) are announced, they will appear here and applications will open.
        You can <a :href="lp('/zh/member/#tab-register')">join as a registered member for free</a> in the meantime.
      </template>
      <template v-else>
        <strong>目前沒有開放加入的會籍方案</strong> —— 球季與方案（含年費、含幾張卡與幾件球衣）公布後，會在這裡顯示並開放申請。
        您可以先<a :href="lp('/zh/member/#tab-register')">免費加入會員</a>。
      </template>
    </div>
  </div>
</section>

<!-- SPEC 3.8 §8.2 — Fan Benefits 福利對照表（與 3.14 加入頁、升級頁共用同一份資料） -->
<section class="band" id="benefits" aria-labelledby="benefits-title">
  <div class="container">
    <ContentMembershipBenefits />
  </div>
</section>

<!-- SPEC 3.8 §8.4 — 特約店家導引 -->
<section class="band paper-2-band" aria-labelledby="perks-title">
  <div class="container">
    <p class="kicker">PARTNER PERKS</p>
    <h2 class="section-title" id="perks-title">{{ tx('特約店家折扣', 'Partner store discounts') }}</h2>
    <p class="section-lede">{{ tx('到店出示電子會員卡即可享折扣，店家目視查驗，不需額外手續。付費會員另可使用標示「限付費會員」的優惠。', 'Show your digital membership card in store to enjoy a discount; the store checks it by eye and no extra steps are needed. Paid members can also use offers marked "Paid Fan Club members only".') }}</p>
    <MemberPerksTeaser />
  </div>
</section>

<!-- SPEC 3.8 §8.2 — Fan Events 活動列表 + 報名 + 回顧 -->
<section class="band" id="events" aria-labelledby="events-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAN EVENTS</p>
        <h2 class="section-title" id="events-title">{{ tx('球迷活動', 'Fan events') }}</h2>
      </div>
    </div>

    <ul v-if="(upcoming ?? []).length > 0" class="fe-list">
      <li v-for="e in upcoming" :key="e.slug" class="fe-card">
        <div v-if="safeImageUrl(e.coverThumbUrl || e.coverUrl)" class="fe-card__media"><img :src="safeImageUrl(e.coverThumbUrl || e.coverUrl)!" :alt="imgAlt(e.coverAlt, e.name)" loading="lazy" v-bind="imgAttrs(e.coverWidth, e.coverHeight)"></div>
        <div class="fe-card__body">
          <h3 class="fe-card__title">{{ e.name }}</h3>
          <p class="fe-card__meta">
            <template v-if="e.startsAt">{{ formatTaipeiDateTime(e.startsAt, locale) }}<br></template>
            <template v-if="e.location">{{ e.location }}<br></template>
            <span v-if="e.isPaidMembersOnly" class="mc-badge mc-badge--ok">{{ tx('限付費球迷會員', 'Paid Fan Club members only') }}</span>
            <span v-if="e.isFull" class="mc-badge">{{ tx('名額已滿・可候補', 'Full · waitlist open') }}</span>
            <span v-else-if="e.spotsLeft !== null" class="mc-badge">{{ isEn ? `${e.spotsLeft} ${e.spotsLeft === 1 ? 'place' : 'places'} left` : `剩餘 ${e.spotsLeft} 名` }}</span>
          </p>
          <a class="btn btn--dark btn--sm" :href="lp(`/zh/culture/fan-club/events/${e.slug}/`)">{{ e.isRegistrationOpen ? tx('詳情與報名', 'Details and registration') : tx('活動詳情', 'Event details') }}</a>
        </div>
      </li>
    </ul>
    <p v-else class="is-pending" style="margin-top:1.25rem;">{{ tx('目前沒有即將舉辦的球迷活動，公布後會在這裡開放報名。', 'There are no upcoming fan events at the moment. Registration will open here once they are announced.') }}</p>

    <h3 style="margin-top:2.5rem;font-size:1.15rem;font-weight:800;color:var(--heading)">{{ tx('活動回顧', 'Event reviews') }}</h3>
    <ul v-if="(past ?? []).length > 0" class="fe-list">
      <li v-for="e in past" :key="e.slug" class="fe-card">
        <div v-if="safeImageUrl(e.coverThumbUrl || e.coverUrl)" class="fe-card__media"><img :src="safeImageUrl(e.coverThumbUrl || e.coverUrl)!" :alt="imgAlt(e.coverAlt, e.name)" loading="lazy" v-bind="imgAttrs(e.coverWidth, e.coverHeight)"></div>
        <div class="fe-card__body">
          <h4 class="fe-card__title">{{ e.name }}</h4>
          <p class="fe-card__meta"><template v-if="e.startsAt">{{ formatTaipeiDateTime(e.startsAt, locale) }}<br></template><template v-if="e.location">{{ e.location }}</template></p>
          <a class="btn btn--dark btn--sm" :href="lp(`/zh/culture/fan-club/events/${e.slug}/`)">{{ tx('活動回顧', 'Event review') }}</a>
        </div>
      </li>
    </ul>
    <div v-else-if="showStaticReview" class="grid grid--4" style="margin-top:1.25rem">
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-01.jpg')" :alt="tx('球迷會周邊展示：主場球衣、TCRFC 球帽、背包與造型抱枕', 'Fan Club merchandise display: the home jersey, a TCRFC cap, a backpack and a novelty cushion')" loading="lazy" width="1600" height="1067">
      </figure>
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-02.jpg')" :alt="tx('學院學員身著台中磐石球衣於球場圍網前合影', 'Academy students in Taichung Rock jerseys posing in front of the pitch fence')" loading="lazy" width="1600" height="1067">
      </figure>
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-03.jpg')" :alt="tx('三位學員手持桃紅色 TCRFC 加油棒於戶外合影', 'Three students holding pink TCRFC cheering sticks outdoors')" loading="lazy" width="1600" height="1067">
      </figure>
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-04.jpg')" :alt="tx('球員、學員與球迷於球場大合照', 'Players, students and fans in a group photo at the ground')" loading="lazy" width="1600" height="1067">
      </figure>
    </div>
    <p v-else class="is-pending" style="margin-top:1.25rem;">{{ tx('還沒有活動回顧。', 'There are no event reviews yet.') }}</p>
  </div>
</section>

<!-- SPEC 3.8 §8.2 / 3.14 — Member Draw 球迷會員抽獎（純說明段落，非互動區塊）
     規劃書明訂前台不做：抽獎頁與報名按鈕、我的抽獎、序號查詢、中獎名單頁、線上開獎動畫。
     此區塊只說明機制，不得加入任何按鈕或表單。 -->
<section class="band paper-2-band" id="draw" aria-labelledby="draw-title">
  <div class="container">
    <p class="kicker">MEMBER DRAW</p>
    <h2 class="section-title" id="draw-title">{{ tx('球迷會員抽獎', 'Fan Club member draw') }}</h2>
    <p v-if="isEn" class="section-lede">
      During the valid membership period you are <strong>automatically eligible for the draw, with no sign-up or registration needed</strong>.
      For each draw, every Fan Club member with a valid membership at the eligibility cut-off time is added to the eligible list and assigned a draw number.
    </p>
    <p v-else class="section-lede">
      會籍有效期間<strong>自動具備抽獎資格，不需報名也不需登記</strong>。
      每次抽獎在資格基準時間當下，將所有會籍有效的球迷會員全數列入合格名單並配發抽獎序號。
    </p>
    <ul v-if="isEn" class="draw-points">
      <li><strong>There is nothing you need to do</strong> — there is no sign-up form, no points to collect, and how often you take part does not affect your chances. One member, one number.</li>
      <li><strong>The draw is made manually, in person or by live stream</strong>. What is drawn is the draw number, and the process can be witnessed publicly.</li>
      <li><strong>Prizes are physical items</strong>, sent by hand or collected in person, in the same way as the membership jersey.</li>
      <li><strong>Results are announced in News</strong>, and every list is masked (draw number, member number and name are masked).</li>
      <li><strong>Each club runs its own draws</strong>: a member who holds paid memberships at both clubs can enter both clubs' draws (and still has only one number in each draw).</li>
    </ul>
    <ul v-else class="draw-points">
      <li><strong>不需要做任何事</strong>——沒有報名表單、不必累積點數、參加次數多寡不影響中獎機會，一人一號。</li>
      <li><strong>現場或直播人工開獎</strong>，抽出的是抽獎序號，過程公開可見證。</li>
      <li><strong>獎品為實體物品</strong>，人工寄送或現場領取，領取方式比照入會球衣。</li>
      <li><strong>結果公布於最新消息</strong>，名單一律遮罩（抽獎序號、會員編號、姓名遮罩）。</li>
      <li><strong>各俱樂部各自舉辦抽獎</strong>：同時具備兩隊付費會籍者，可分別參加兩隊的抽獎（在每一次抽獎中仍只有一個號）。</li>
    </ul>
    <p class="benefits__note">
      {{ tx('各次抽獎的獎品、名額、資格基準時間與開獎時間，一律於最新消息公布。', 'The prizes, number of winners, eligibility cut-off time and draw time for each draw are announced in News.') }}
      <a :href="lp('/zh/news/')">{{ tx('前往最新消息 →', 'Go to News →') }}</a>
    </p>
  </div>
</section>
</template>

<style>
.is-pending{ color:var(--muted); font-style:italic; }
.page-hero__bg--pending{ background:linear-gradient(160deg, var(--ink) 0%, var(--brand-deep) 100%); }
.paper-2-band{ background:var(--paper-2); }

/* 球迷會員抽獎說明 */
.draw-points{ list-style:none; padding:0; margin:1.75rem 0 0; font-size:.9rem; line-height:1.9; color:var(--text); }
.draw-points li{ padding-left:1.4rem; position:relative; margin-bottom:.5rem; }
.draw-points li::before{ content:"—"; position:absolute; left:0; color:var(--brand-aa); font-weight:800; }

/* 會籍方案卡 */
.plan-grid{ margin-top:1.75rem; }
.plan-card{ background:var(--paper); border:1px solid var(--rule); border-top:3px solid var(--brand); padding:1.75rem 1.5rem; }
.plan-card__name{ font-size:1.15rem; font-weight:800; color:var(--heading); margin-bottom:.4rem; }
.plan-card__price{ font-size:.95rem; font-weight:700; margin-bottom:1rem; }
.plan-card__list{ list-style:none; padding:0; margin:0 0 1.5rem; font-size:.86rem; line-height:1.9; color:var(--text); }
.plan-card__list li{ padding-left:1.2rem; position:relative; }
.plan-card__list li::before{ content:"✓"; position:absolute; left:0; color:var(--brand-aa); font-weight:800; }

.perks-lead{ display:flex; flex-wrap:wrap; gap:1.5rem; align-items:center; justify-content:space-between; }


/* 活動卡骨架 */
.event-card{ background:var(--paper-2); border:1px solid var(--rule); padding:1.5rem; display:flex; flex-direction:column; gap:1rem; }
.event-card__title{ flex:1; }

.event-photo{ margin:0; aspect-ratio:3/2; overflow:hidden; background:var(--paper-2); }
.event-photo img{ width:100%; height:100%; object-fit:cover; }
</style>
