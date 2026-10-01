<script setup lang="ts">
// app/components/member/MemberPerksTeaser.vue — 特約店家預告（加入頁、8.2 球迷會頁共用，S2-11／S3-2）
// 資料來自 8.4 `partner-stores`（後台 K4 維護）；尚無已上架店家時如實顯示「名單尚未公布」，不放示意店家。
const { lp } = useLocale()
const { stores, failed } = await usePartnerStores()
const shown = computed(() => stores.value.slice(0, 6))
</script>

<template>
  <div class="perks-teaser">
    <h3 class="perks-teaser__title">特約店家</h3>
    <p v-if="stores.length > 0">
      目前合作店家 <strong>{{ stores.length }}</strong> 家：<template v-for="(s, i) in shown" :key="s.slug"><a :href="lp(`/zh/perks/${s.slug}/`)">{{ s.name }}</a><template v-if="i < shown.length - 1">、</template></template><template v-if="stores.length > shown.length">…等</template>。
    </p>
    <p v-else class="pending-cell">{{ failed ? '店家名單暫時無法載入。' : '合作店家名單尚未公布。' }}</p>
    <a class="btn btn--dark btn--sm" :href="lp('/zh/perks/')">前往特約店家清單</a>
  </div>
</template>

<style>
.perks-teaser{ margin-top:2rem; padding:1.5rem; background:var(--paper-2); border:1px solid var(--rule); }
.perks-teaser__title{ font-weight:800; color:var(--heading); margin-bottom:.5rem; }
.perks-teaser p{ font-size:.9rem; line-height:1.8; }
.perks-teaser a:not(.btn){ color:var(--brand-aa); text-decoration:underline; }
.perks-teaser .btn{ margin-top:1rem; }
</style>
