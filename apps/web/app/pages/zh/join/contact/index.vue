<script setup lang="ts">
// app/pages/zh/join/contact/index.vue — 由 site/src/pages/zh/join/contact/index.html 轉來
// 🔴 main 內容與 mockup 逐段一致，DOM 結構、class、文字內容不動；{{ROOT}} 已由 codemod-root.mjs 轉為絕對路徑。
definePageMeta({ nav: '', unit: '10-contact' })

// 文案依俱樂部切換：hero／SEO 與社群連結取自 club-copy.ts。藍鯨無實體地址、
// 電話與各部門分機（舊站盤點：content/blue-whale/gap-analysis.md §2 單元 10，
// 「沒有任何實體地址、電話或聯絡表單」），這幾格本站一律不顯示。
const config = useRuntimeConfig()
const clubKey = computed<'tcrfc' | 'bw'>(() => (config.public.club === 'bw' ? 'bw' : 'tcrfc'))
const isTcrfc = computed(() => clubKey.value === 'tcrfc')
const identity = computed(() => getClubIdentity(clubKey.value))
const hero = computed(() => JOIN_CONTACT_HERO[clubKey.value])

useSeoMeta({
  title: computed(() => JOIN_CONTACT_SEO[clubKey.value].title),
  description: computed(() => JOIN_CONTACT_SEO[clubKey.value].description),
})

/** 從社群網址推導顯示用帳號（沿用 mockup 既有的 @handle 呈現方式，不新增資料欄位）。 */
function socialHandle(url: string): string {
  const last = url.replace(/\/$/, '').split('/').pop() ?? ''
  return last.startsWith('@') ? last : `@${last}`
}
</script>

<template>
<nav class="breadcrumb" aria-label="麵包屑">
  <div class="container">
    <ol>
      <li><a href="/zh/">首頁</a></li>
      <li><a href="/zh/join/">加入與聯絡</a></li>
      <li aria-current="page">聯絡資訊</li>
    </ol>
  </div>
</nav>

<section class="page-hero">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="container">
    <p class="page-hero__eyebrow">Contact Information</p>
    <h1>{{ hero.h1Zh }}<span v-if="hero.h1En" class="en">{{ hero.h1En }}</span></h1>
    <p class="page-hero__lede" v-html="hero.lede"></p>
  </div>
</section>

<section class="band" aria-labelledby="contact-title">
  <div class="container">
    <h2 class="visually-hidden" id="contact-title">聯絡資訊列表</h2>
    <div class="contact-grid">
      <div v-if="isTcrfc" class="contact-item">
        <p class="contact-item__label">電話</p>
      </div>

      <div v-if="isTcrfc || identity.social.email" class="contact-item">
        <p class="contact-item__label">Email</p>
        <p v-if="identity.social.email" class="contact-item__value">{{ identity.social.email }}</p>
      </div>

      <div v-if="isTcrfc" class="contact-item">
        <p class="contact-item__label">地址</p>
        <p class="contact-item__value">台中市北屯區崇平路二段景谷巷 11 弄 41 號</p>
        <p class="field-hint">主場：西屯足球場。各場地詳細位置見<a href="/zh/join/location/">場地位置與地圖</a>。</p>
      </div>

      <div v-if="isTcrfc" class="contact-item">
        <p class="contact-item__label">營業時間</p>
      </div>

      <div v-if="isTcrfc" class="contact-item contact-item--full">
        <p class="contact-item__label">各部門分機</p>
      </div>

      <div class="contact-item contact-item--full">
        <p class="contact-item__label">社群連結</p>
        <ul class="contact-social">
          <li v-if="identity.social.facebook">
            <a :href="identity.social.facebook" target="_blank" rel="noopener">
              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M14 9h3V5h-3c-2.2 0-4 1.8-4 4v2H7v4h3v7h4v-7h3l1-4h-4v-2c0-.6.4-1 1-1z"/></svg>
              Facebook<span class="contact-social__handle">{{ socialHandle(identity.social.facebook) }}</span>
            </a>
          </li>
          <li v-if="identity.social.instagram">
            <a :href="identity.social.instagram" target="_blank" rel="noopener">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5"/><circle cx="12" cy="12" r="4"/><circle cx="17.2" cy="6.8" r="1"/></svg>
              Instagram<span class="contact-social__handle">{{ socialHandle(identity.social.instagram) }}</span>
            </a>
          </li>
          <li v-if="identity.social.youtube">
            <a :href="identity.social.youtube" target="_blank" rel="noopener">
              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="2" y="5.5" width="20" height="13" rx="3.5" fill="none" stroke="currentColor" stroke-width="1.8"/><path d="M10 9.5l6 2.5-6 2.5z"/></svg>
              YouTube<span class="contact-social__handle">{{ socialHandle(identity.social.youtube) }}</span>
            </a>
          </li>
          <li v-if="identity.social.line">
            <a :href="identity.social.line" target="_blank" rel="noopener">
              LINE 官方帳號
            </a>
          </li>
        </ul>
      </div>
    </div>
  </div>
</section>

<section class="band grain" aria-labelledby="cta-title">
  <span class="ghost-num ghost-num--dark" aria-hidden="true">10</span>
  <div class="band-inner container" style="text-align:center">
    <p class="kicker kicker--on-dark" style="justify-content:center">找不到你要的資訊？</p>
    <h2 class="section-title" id="cta-title" style="color:#fff">直接透過表單聯絡我們</h2>
    <p class="section-lede on-dark" style="margin-inline:auto">七種表單各自送達對應部門，會比一般聯絡信箱更快得到回覆。</p>
    <div style="margin-top:2rem">
      <a class="btn btn--primary" href="/zh/join/">查看所有表單</a>
    </div>
  </div>
</section>
</template>

<style>
/* 僅本頁使用：聯絡資訊卡片與社群列表 */
.contact-grid{ display:grid; grid-template-columns:repeat(2,1fr); gap:clamp(1rem,2.5vw,1.75rem); }
@media (max-width:700px){ .contact-grid{ grid-template-columns:1fr; } }
.contact-item{ padding:1.75rem clamp(1.25rem,3vw,2rem); background:var(--paper-2); border:1px solid var(--rule); }
.contact-item--full{ grid-column:1 / -1; }
.contact-item__label{ font-size:.72rem; font-weight:800; letter-spacing:.08em; text-transform:uppercase; color:var(--brand-aa); margin-bottom:.75rem; }
.contact-item__value{ font-size:1.05rem; font-weight:700; color:var(--heading); line-height:1.6; }
.contact-item .field-hint{ margin-top:.6rem; }
.contact-item .field-hint a{ color:var(--brand-aa); text-decoration:underline; }
.contact-item .pending{ font-size:.85rem; }
.contact-item .pending a{ color:var(--brand-deep); text-decoration:underline; }

.contact-social{ display:flex; flex-wrap:wrap; gap:1rem; }
.contact-social li{ flex:1 1 200px; }
.contact-social a{
  display:flex; align-items:center; gap:.75rem; padding:1rem 1.25rem;
  background:var(--paper); border:1px solid var(--rule); font-weight:700; color:var(--heading);
  transition:border-color var(--dur-fast) var(--ease);
}
.contact-social a:hover{ border-color:var(--brand-aa); }
.contact-social svg{ width:22px; height:22px; flex:none; color:var(--brand-aa); }
.contact-social__handle{ display:block; font-size:.78rem; font-weight:500; color:var(--muted); }
</style>
