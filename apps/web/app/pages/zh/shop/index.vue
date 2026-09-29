<script setup lang="ts">
// app/pages/zh/shop/index.vue — 由 site/src/pages/zh/shop/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: 'culture', unit: '8.3' })

const { lp } = useLocale()
const config = useRuntimeConfig()
// BW-C1（品牌外洩全站盤點）：title／description／breadcrumb／篩選選單／示範商品名稱
// 改讀既有的 getClubAssets()／getClubIdentity()。示範球衣的 alt 文字原本描述磐石球衣
// 的實際配色與贊助商標誌（桃紅色、Joma、TCRFC 隊徽），藍鯨球衣配色與贊助商狀況未知，
// 不得沿用磐石的具體設計細節，改為不描述顏色與贊助商的通用說明。
// ⚠️「收款方為台中磐石足球俱樂部」（購物須知區塊）不在此次修改範圍——理由同
// checkout/index.vue 檔頭說明（藍鯨規劃書 §1.3／主站規劃書 §1.3「前台必須明示收款方」），
// 已列入 check-club-brand-leak.mjs 例外清單。
const clubAssets = computed(() => getClubAssets(config.public.club))
const identity = computed(() => getClubIdentity(config.public.club))
const isTcrfc = computed(() => config.public.club !== 'bw')

useSeoMeta({
  title: computed(() => `官方商店 Shop｜${clubAssets.value.nameZh}`),
  description: computed(() => `${clubAssets.value.nameZh}官方商店：主場球衣與厚底緩震機能襪。以 LINE Pay 付款、自動開立電子發票，可宅配、超商取貨或現場自取。`),
})
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a :href="lp('/zh/')">首頁</a></li>
      <li><a :href="lp('/zh/culture/')">{{ identity.cultureLabelZh }}</a></li>
      <li><a :href="lp('/zh/culture/merchandise/')">官方商品</a></li>
      <li aria-current="page">官方商店</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <div class="container">
    <p class="page-hero__eyebrow">8.3 Official Shop</p>
    <h1>官方商店<span class="en">Shop</span></h1>
    <p class="page-hero__lede">桃紅戰袍與訓練配件。選好尺寸與顏色加入購物車，以 LINE Pay 付款，系統自動開立電子發票。</p>
    <p style="margin-top:1.25rem"><span class="mock-flag">流程骨架 — 尚未串接金流、庫存與後端，按鈕不會真的送出</span></p>
  </div>
</section>

<!-- SPEC 3.8 §8.3 — 商品列表：分類與篩選（比照舊站既有篩選項：分類／價格／尺寸／顏色） -->
<section class="band band--tight" aria-labelledby="filters-title">
  <div class="container">
    <h2 class="visually-hidden" id="filters-title">商品篩選</h2>
    <div class="filter-row">
      <div class="filter-field">
        <label for="f-collection">系列</label>
        <select id="f-collection">
          <option value="">全部商品</option>
          <option value="club">俱樂部商品 Club</option>
          <option value="academy">{{ identity.academyShortLabelZh }}商品 Academy</option>
          <option value="fan">球迷商品 Fan</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-size">尺寸</label>
        <select id="f-size">
          <option value="">不限</option>
          <option>M</option><option>L</option><option>XL</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-color">顏色</label>
        <select id="f-color">
          <option value="">不限</option>
          <option>桃紅</option><option>向日黃</option><option>經典紅</option>
          <option>櫻桃紅</option><option>海軍藍</option><option>極簡黑</option><option>純淨白</option>
        </select>
      </div>
      <div class="filter-field">
        <label for="f-sort">排序</label>
        <select id="f-sort">
          <option>最新上架</option>
          <option>價格由低到高</option>
          <option>價格由高到低</option>
        </select>
      </div>
    </div>
    <p class="result-count">{{ isTcrfc ? '共 2 件商品' : '共 1 件商品' }}</p>
  </div>
</section>

<section class="band band--tight" aria-labelledby="products-title">
  <div class="container">
    <h2 class="visually-hidden" id="products-title">商品列表</h2>
    <div class="product-grid">

      <!-- BW-C1：主場球衣示範照片是磐石真實球衣（桃紅配色、Joma／San Pellegrino 贊助標誌），
           藍鯨球衣配色與贊助商狀況與磐石不同，不得沿用同一張照片假裝是藍鯨商品，本卡片僅
           對 tcrfc 顯示；藍鯨商店現況只有機能襪這款無隊徽通用配件可共用。 -->
      <article v-if="isTcrfc" class="product-card">
        <a class="product-card__media" :href="lp('/zh/shop/home-jersey-2026/')" aria-label="台中磐石主場球衣 2026 賽季">
          <span class="product-card__flag">新上市</span>
          <img src="/assets/img/merch/merch-jersey-01.jpg" alt="台中磐石主場球衣 2026 賽季" loading="lazy" width="1600" height="1067">
        </a>
        <div class="product-card__body">
          <p class="product-card__name"><a :href="lp('/zh/shop/home-jersey-2026/')">台中磐石主場球衣｜2026 賽季</a></p>
          <p class="product-card__opts">尺寸 M／L／XL</p>
          <div class="product-card__foot">
            <span class="price price--sale">NT$1,200</span>
            <span class="price__was">NT$1,600</span>
          </div>
          <a class="btn btn--primary btn--sm btn--block" :href="lp('/zh/shop/home-jersey-2026/')" style="margin-top:1rem">選購</a>
        </div>
      </article>

      <article class="product-card">
        <a class="product-card__media" :href="lp('/zh/shop/cushioned-socks/')" aria-label="厚底緩震機能襪">
          <img src="/assets/img/merch/merch-socks-01.jpg" alt="厚底緩震機能襪六色排列：向日黃、經典紅、櫻桃紅、海軍藍、極簡黑、純淨白" loading="lazy" width="1600" height="1600">
        </a>
        <div class="product-card__body">
          <p class="product-card__name"><a :href="lp('/zh/shop/cushioned-socks/')">厚底緩震機能襪</a></p>
          <p class="product-card__opts">尺寸 M／L・六色可選</p>
          <div class="product-card__foot">
            <span class="price">NT$120</span>
          </div>
          <a class="btn btn--primary btn--sm btn--block" :href="lp('/zh/shop/cushioned-socks/')" style="margin-top:1rem">選購</a>
        </div>
      </article>

    </div>

    <div class="editorial-note" style="margin-top:2.5rem">
      <p><strong>{{ identity.academyShortLabelZh }}商品 Academy Collection</strong> 與 <strong>球迷商品 Fan Collection</strong> 尚在開發，首波商品確定後於此上架。</p>
      <p class="pending-inline">兩個系列的商品內容待補。</p>
    </div>
  </div>
</section>

<section class="band grain" aria-labelledby="shop-info-title">
  <div class="band-inner container">
    <p class="kicker kicker--on-dark">SHOP INFO</p>
    <h2 class="section-title" id="shop-info-title" style="color:#fff">購物須知</h2>
    <div class="grid grid--3" style="margin-top:2rem">
      <div class="clip-card clip-card--on-dark">
        <h3>付款方式</h3>
        <p>結帳一律以 <strong>LINE Pay</strong> 付款，收款方為台中磐石足球俱樂部。本站不經手也不儲存信用卡資料。</p>
      </div>
      <div class="clip-card clip-card--on-dark">
        <h3>電子發票</h3>
        <p>結帳時開立電子發票，可選擇手機載具、統一編號或捐贈碼；退貨時同步作廢或折讓。</p>
      </div>
      <div class="clip-card clip-card--on-dark">
        <h3>配送方式</h3>
        <p>宅配到府、超商取貨（不付款）、主場賽事日或俱樂部現場自取。<span class="pending-inline">運費金額與免運門檻待確認。</span></p>
      </div>
    </div>
    <p style="margin-top:2rem"><a class="btn btn--light" :href="lp('/zh/order/lookup/')">查詢訂單</a></p>
  </div>
</section>
</template>
