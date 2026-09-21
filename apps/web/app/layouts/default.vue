<!-- app/layouts/default.vue — 由 site/src/partials/shell.html 的 body 骨架轉來
     （<head> 內容改由 app/app.vue 與各頁面的 useHead／useSeoMeta 負責，
     見 docs/14-invariants.md「<head> 不受本條約束」）。
     skip-link → header → <main id="main"> → footer，DOM 順序與 class 一律不動。 -->
<template>
  <div>
    <a class="skip-link" href="#main">跳至主要內容</a>
    <SiteHeader />
    <main id="main">
      <slot />
    </main>
    <SiteFooter />
  </div>
</template>
