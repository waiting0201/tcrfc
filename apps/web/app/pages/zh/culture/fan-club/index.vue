<script setup lang="ts">
// app/pages/zh/culture/fan-club/index.vue — 8.2 球迷會（付費會籍的介紹與加入頁，主站 §3.8，S3-2）
//
// 區塊：會籍方案（後台 K2 `membership/plans`）／權益對照（K4，與加入頁、會員中心共用同一元件與資料）／
// 特約店家預告（8.4）／球迷活動列表與回顧（F2 `fan-events`）／球迷會員抽獎（純說明段落，非互動區塊）。
// 兩個俱樂部同一版型（藍鯨規劃書 §1.3、§5.1）；資料依容器的 `club` 由後端分區，藍鯨目前沒有現行方案與活動，
// 顯示誠實的空狀態。BW-C1 當時的「藍鯨尚未推出」整頁空狀態已隨後端資料到位而移除。
import { formatPlainDate, formatTaipeiDateTime } from '#shared/utils/member'
import type { FanEvent } from '#shared/utils/member'

definePageMeta({ nav: 'culture', unit: '8.2' })

const { lp, locale } = useLocale()
const config = useRuntimeConfig()
const club = config.public.club
const isTcrfc = computed(() => club !== 'bw')
const clubAssets = computed(() => getClubAssets(club))
const identity = computed(() => getClubIdentity(club))

const { plans, failed: plansFailed } = await useMembershipPlans()
const lang = locale.value
const [{ data: upcoming }, { data: past }] = await Promise.all([
  useFetch<FanEvent[]>(`/api/backend/${club}/fan-events`, { query: { phase: 'upcoming', lang }, key: `fan-events-${club}-upcoming-${lang}` }),
  useFetch<FanEvent[]>(`/api/backend/${club}/fan-events`, { query: { phase: 'past', lang }, key: `fan-events-${club}-past-${lang}` }),
])
const safeImg = (u: string | null | undefined) => (u && /^(https:\/\/|\/)/.test(u) ? u : null)
const feeText = (n: number) => `NT$ ${n.toLocaleString('zh-TW')}`
// 過渡內容：磐石既有的 4 張真實活動照片，後台 F2 出現任何一筆「活動回顧」資料即整批退場，不混搭
const showStaticReview = computed(() => isTcrfc.value && (past.value ?? []).length === 0)

useSeoMeta({
  title: computed(() => (isTcrfc.value
    ? '台中磐石球迷會 Fan Club｜台中磐石文化｜台中磐石足球俱樂部'
    : `球迷會｜${identity.value.cultureLabelZh}｜${clubAssets.value.nameZh}`)),
  description: computed(() => `加入${clubAssets.value.nameZh}球迷會：會籍方案、會員福利分級對照，以及球迷活動報名與回顧。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li aria-current="page">{{ isTcrfc ? '台中磐石球迷會' : '球迷會' }}</li>
    </ol>
  </div>
</nav>

<section class="page-hero page-hero--media">
  <img v-if="isTcrfc" class="page-hero__bg" :src="siteImg('/assets/img/fanclub/fanclub-event-04.jpg')" alt="台中磐石球員、學員與球迷於球場合影留念" width="1600" height="900">
  <div v-else class="page-hero__bg page-hero__bg--pending" aria-hidden="true" />
  <div class="container">
    <p class="page-hero__eyebrow">8.2 Fan Club</p>
    <h1 v-if="isTcrfc">台中磐石球迷會<span class="en">Fan Club</span></h1>
    <h1 v-else>球迷會<span class="en">Fan Club</span></h1>
    <p class="page-hero__lede">與{{ clubAssets.shortNameZh }}一起在場邊吶喊。球迷會即{{ clubAssets.shortNameZh }}的付費會籍，除了球衣，還能在特約店家享有更多折扣，並優先參與球迷活動。</p>
  </div>
</section>

<!-- SPEC 3.8 §8.2 — Membership Plans 會籍方案 -->
<section class="band" id="join" aria-labelledby="join-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">MEMBERSHIP PLANS</p>
        <h2 class="section-title" id="join-title">會籍方案</h2>
      </div>
    </div>

    <p class="section-lede" style="margin-top:.75rem">
      球迷會員即{{ clubAssets.shortNameZh }}的付費會籍，以<strong>球季</strong>為單位計算，全體同時到期，於球季末辦理續會。
      會費以 LINE Pay 收款連結或現場收款繳交，完成後由俱樂部開通會籍。
    </p>

    <div v-if="plans.length > 0" class="grid grid--2 plan-grid">
      <article v-for="p in plans" :key="p.code" class="plan-card">
        <h3 class="plan-card__name">{{ p.name }}</h3>
        <p class="plan-card__price">{{ feeText(p.fee) }}／{{ p.seasonCode }} 球季</p>
        <ul class="plan-card__list">
          <li>電子會員卡 {{ p.cardQuota }} 張</li>
          <li v-if="p.jerseyQuota > 0">入會球衣 {{ p.jerseyQuota }} 件</li>
          <li>特約店家折扣，含「限付費會員」品項</li>
          <li>球迷活動優先報名</li>
          <li v-if="p.startsOn || p.endsOn">會籍期間 {{ formatPlainDate(p.startsOn) }} – {{ formatPlainDate(p.endsOn) }}</li>
          <li v-if="p.benefitNote">{{ p.benefitNote }}</li>
          <li v-if="p.midSeasonRule">季中入會：{{ p.midSeasonRule }}</li>
        </ul>
        <a class="btn btn--primary btn--block" :href="lp('/zh/member/')">加入或升級</a>
      </article>
    </div>
    <div v-else class="pending-note">
      <template v-if="plansFailed">會籍方案暫時無法載入，請稍後再試。</template>
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
    <h2 class="section-title" id="perks-title">特約店家折扣</h2>
    <p class="section-lede">到店出示電子會員卡即可享折扣，店家目視查驗，不需額外手續。付費會員另可使用標示「限付費會員」的優惠。</p>
    <MemberPerksTeaser />
  </div>
</section>

<!-- SPEC 3.8 §8.2 — Fan Events 活動列表 + 報名 + 回顧 -->
<section class="band" id="events" aria-labelledby="events-title">
  <div class="container">
    <div class="eyebrow-row">
      <div>
        <p class="kicker">FAN EVENTS</p>
        <h2 class="section-title" id="events-title">球迷活動</h2>
      </div>
    </div>

    <ul v-if="(upcoming ?? []).length > 0" class="fe-list">
      <li v-for="e in upcoming" :key="e.slug" class="fe-card">
        <div v-if="safeImg(e.coverThumbUrl || e.coverUrl)" class="fe-card__media"><img :src="safeImg(e.coverThumbUrl || e.coverUrl)!" :alt="e.name" loading="lazy" width="640" height="427"></div>
        <div class="fe-card__body">
          <h3 class="fe-card__title">{{ e.name }}</h3>
          <p class="fe-card__meta">
            <template v-if="e.startsAt">{{ formatTaipeiDateTime(e.startsAt, locale) }}<br></template>
            <template v-if="e.location">{{ e.location }}<br></template>
            <span v-if="e.isPaidMembersOnly" class="mc-badge mc-badge--ok">限付費球迷會員</span>
            <span v-if="e.isFull" class="mc-badge">名額已滿・可候補</span>
            <span v-else-if="e.spotsLeft !== null" class="mc-badge">剩餘 {{ e.spotsLeft }} 名</span>
          </p>
          <a class="btn btn--dark btn--sm" :href="lp(`/zh/culture/fan-club/events/${e.slug}/`)">{{ e.isRegistrationOpen ? '詳情與報名' : '活動詳情' }}</a>
        </div>
      </li>
    </ul>
    <p v-else class="is-pending" style="margin-top:1.25rem;">目前沒有即將舉辦的球迷活動，公布後會在這裡開放報名。</p>

    <h3 style="margin-top:2.5rem;font-size:1.15rem;font-weight:800;color:var(--heading)">活動回顧</h3>
    <ul v-if="(past ?? []).length > 0" class="fe-list">
      <li v-for="e in past" :key="e.slug" class="fe-card">
        <div v-if="safeImg(e.coverThumbUrl || e.coverUrl)" class="fe-card__media"><img :src="safeImg(e.coverThumbUrl || e.coverUrl)!" :alt="e.name" loading="lazy" width="640" height="427"></div>
        <div class="fe-card__body">
          <h4 class="fe-card__title">{{ e.name }}</h4>
          <p class="fe-card__meta"><template v-if="e.startsAt">{{ formatTaipeiDateTime(e.startsAt, locale) }}<br></template><template v-if="e.location">{{ e.location }}</template></p>
          <a class="btn btn--dark btn--sm" :href="lp(`/zh/culture/fan-club/events/${e.slug}/`)">活動回顧</a>
        </div>
      </li>
    </ul>
    <div v-else-if="showStaticReview" class="grid grid--4" style="margin-top:1.25rem">
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-01.jpg')" alt="球迷會周邊展示：主場球衣、TCRFC 球帽、背包與造型抱枕" loading="lazy" width="1600" height="1067">
      </figure>
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-02.jpg')" alt="學院學員身著台中磐石球衣於球場圍網前合影" loading="lazy" width="1600" height="1067">
      </figure>
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-03.jpg')" alt="三位學員手持桃紅色 TCRFC 加油棒於戶外合影" loading="lazy" width="1600" height="1067">
      </figure>
      <figure class="event-photo">
        <img :src="siteImg('/assets/img/fanclub/fanclub-event-04.jpg')" alt="球員、學員與球迷於球場大合照" loading="lazy" width="1600" height="1067">
      </figure>
    </div>
    <p v-else class="is-pending" style="margin-top:1.25rem;">還沒有活動回顧。</p>
  </div>
</section>

<!-- SPEC 3.8 §8.2 / 3.14 — Member Draw 球迷會員抽獎（純說明段落，非互動區塊）
     規劃書明訂前台不做：抽獎頁與報名按鈕、我的抽獎、序號查詢、中獎名單頁、線上開獎動畫。
     此區塊只說明機制，不得加入任何按鈕或表單。 -->
<section class="band paper-2-band" id="draw" aria-labelledby="draw-title">
  <div class="container">
    <p class="kicker">MEMBER DRAW</p>
    <h2 class="section-title" id="draw-title">球迷會員抽獎</h2>
    <p class="section-lede">
      會籍有效期間<strong>自動具備抽獎資格，不需報名也不需登記</strong>。
      每次抽獎在資格基準時間當下，將所有會籍有效的球迷會員全數列入合格名單並配發抽獎序號。
    </p>
    <ul class="draw-points">
      <li><strong>不需要做任何事</strong>——沒有報名表單、不必累積點數、參加次數多寡不影響中獎機會，一人一號。</li>
      <li><strong>現場或直播人工開獎</strong>，抽出的是抽獎序號，過程公開可見證。</li>
      <li><strong>獎品為實體物品</strong>，人工寄送或現場領取，領取方式比照入會球衣。</li>
      <li><strong>結果公布於最新消息</strong>，名單一律遮罩（抽獎序號、會員編號、姓名遮罩）。</li>
      <li><strong>各俱樂部各自舉辦抽獎</strong>：同時具備兩隊付費會籍者，可分別參加兩隊的抽獎（在每一次抽獎中仍只有一個號）。</li>
    </ul>
    <p class="benefits__note">
      各次抽獎的獎品、名額、資格基準時間與開獎時間，一律於最新消息公布。
      <a :href="lp('/zh/news/')">前往最新消息 →</a>
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
