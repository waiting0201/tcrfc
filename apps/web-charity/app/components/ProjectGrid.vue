<script setup lang="ts">
// ProjectGrid.vue — 項目卡片牆，掃碼落地頁與一般入口共用同一個元件（docs/22-charity-ui.md §2.3：
// 「兩個頁面共用同一個版面元件，差異只在有沒有店家識別區塊」）。
//
// 封面圖：本輪任務沒有任何真實的項目封面照片可用，⛔ 不自行生成或放置假圖片
// （CLAUDE.md 全域規定 7、docs/22 §2.2.1 的降級原則同樣適用於這裡）。改用中性色塊
// ＋ 項目名稱首字的佔位呈現，視覺上跟「未上傳封面圖」的正常狀態一致，不是壞掉的圖片。
import { useLang } from '../composables/useLang'
import { pickText } from '../utils/i18n'

const props = defineProps<{
  projects: Array<{
    slug: string
    name_zh: string
    name_en: string
    one_liner_zh: string
    one_liner_en: string
    min_amount: number
    max_amount: number
  }>
  storeSlug?: string | null
  loading?: boolean
}>()

const { lang, tr } = useLang()

function projectHref(slug: string) {
  const base = `/${lang.value}/p/${slug}`
  return props.storeSlug ? `${base}?s=${encodeURIComponent(props.storeSlug)}` : base
}
</script>

<template>
  <div>
    <div v-if="loading" class="amount-grid" style="grid-template-columns: 1fr;" aria-hidden="true">
      <div class="skeleton-card" />
      <div class="skeleton-card" />
      <div class="skeleton-card" />
    </div>

    <p v-else-if="projects.length === 0" class="text-secondary">
      {{ tr.home.noProjects }}
    </p>

    <ul v-else class="stack" style="list-style: none; padding: 0; margin: 0;">
      <li v-for="project in projects" :key="project.slug">
        <NuxtLink
          :to="projectHref(project.slug)"
          class="card"
          style="display: flex; gap: var(--sp-4); align-items: center; text-decoration: none; color: inherit;"
        >
          <span
            aria-hidden="true"
            style="
              flex: none; width: 64px; height: 64px; border-radius: var(--radius-control);
              background: var(--charity-bg-surface-2); color: var(--charity-text-tertiary);
              display: flex; align-items: center; justify-content: center;
              font-size: 1.5rem; font-weight: 700;
            "
          >{{ project.name_zh.replace('測試用．', '').charAt(0) }}</span>

          <span style="flex: 1;">
            <strong style="display: block; margin-bottom: 4px;">
              {{ pickText(lang, project.name_zh, project.name_en).text }}
            </strong>
            <span class="text-secondary" style="display: block; font-size: 0.9375rem; margin-bottom: 8px;">
              {{ pickText(lang, project.one_liner_zh, project.one_liner_en).text }}
            </span>
            <span class="btn btn-primary" style="min-height: 36px; padding: 6px 16px; font-size: 0.875rem;">
              {{ tr.home.learnMoreAndDonate }} →
            </span>
          </span>
        </NuxtLink>
      </li>
    </ul>
  </div>
</template>
