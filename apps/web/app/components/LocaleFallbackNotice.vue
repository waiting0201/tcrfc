<script setup lang="ts">
// app/components/LocaleFallbackNotice.vue — 「本頁尚無此語系版本」提示（S1-13）
//
// docs/05-i18n-seo.md §1 Fallback 規則：「未翻譯內容顯示繁中，並標示『本頁尚無此語系
// 版本』（fallback 或隱藏可於後台設定）」——本輪（S1-13 框架階段）先落實「顯示繁中＋
// 標示」這一半，「後台可設定隱藏」留給真的有後台可管這件事的階段再做。
//
// 掛載時機見 app/layouts/default.vue：locale === 'en' 且該頁尚未透過
// definePageMeta({ enReady: true }) 宣告「本頁已有真正的英文內容」時顯示。
// 不擋內容——繁中內容原樣顯示在這則提示下方，這裡只是加註記，不是整頁不存在或轉址
// （規劃書沒有「本頁不存在」這個選項，未翻譯內容本來就要求顯示繁中，見檔頭引述）。
</script>

<template>
  <div class="locale-fallback-notice" role="status">
    <div class="container">
      <p>
        This page has not been translated into English yet — showing the Traditional Chinese version below.
        <span lang="zh-Hant">本頁尚無英文版本，以下為繁體中文內容。</span>
      </p>
    </div>
  </div>
</template>

<style>
.locale-fallback-notice{
  background:var(--paper-2, #f5f1ec);
  border-bottom:1px solid var(--rule, #e4ddd3);
  padding:.65rem 1rem;
}
.locale-fallback-notice p{
  margin:0;
  font-size:.82rem;
  line-height:1.6;
  color:var(--muted, #6b625a);
  text-align:center;
}
.locale-fallback-notice span{ display:block; }
</style>
