<script setup lang="ts">
// pages/[lang]/impact/index.vue — 成果回顧（規劃書 §2.1：「摘要呈現關聯的慈善計畫成果，並導回主站」）。
//
// 資料來自 `GET /impact`：已上架且關聯了慈善計畫（或只關聯公益團體）的項目，依計畫分組。
// 計畫與公益團體名稱是項目的「快照」，不即時查主站（規劃書 §9.3）；成果數據與故事在俱樂部官網，這裡只負責導流：
// `clubSiteUrl` 為 null 代表後台尚未設定，就不顯示導回連結——⛔ 不放猜測的網址（docs/22 §6.1 第 7 項）。
// ⚠️ docs/22 沒有給這一頁的版面規格（§6.1 第 6 項），目前版面是依資料結構做的最小版本，等版面規格補上後調整。
import { useLang } from '../../../composables/useLang'
import { useHreflang } from '../../../composables/useHreflang'

definePageMeta({ layout: 'default' })

const { lang, tr, tt } = useLang()
useHreflang('/impact/')

const { data: impact } = await useCharityImpact()

useHead(() => ({ title: `${tr.value.impact.heading} | ${tr.value.associationName}` }))

function projectHref(slug: string) {
  return `/${lang.value}/p/${slug}`
}
</script>

<template>
  <div class="container">
    <section class="section">
      <h1>{{ tr.impact.heading }}</h1>

      <p v-if="!impact" class="notice-row" role="alert">{{ tr.impact.loadError }}</p>

      <template v-else>
        <p class="text-secondary">{{ tt(tr.impact.intro) }}</p>
        <p v-if="impact.isFallback" class="field-hint">{{ tr.fallbackNotice }}</p>

        <p v-if="impact.programs.length === 0" class="text-secondary">{{ tr.impact.empty }}</p>

        <div v-else class="stack" style="margin-top: var(--sp-4);">
          <section v-for="(program, i) in impact.programs" :key="program.programRefCode ?? `none-${i}`" class="card">
            <h2 style="margin-bottom: var(--sp-1);">{{ program.programName ?? tr.impact.noProgram }}</h2>
            <p v-if="program.charityName" class="text-tertiary" style="margin-bottom: var(--sp-3);">
              {{ tr.impact.charity }}：{{ program.charityName }}
            </p>

            <ul class="stack" style="list-style: none; padding: 0; margin: 0;">
              <li v-for="project in program.projects" :key="project.slug">
                <NuxtLink
                  :to="projectHref(project.slug)"
                  style="display: flex; gap: var(--sp-4); align-items: center; text-decoration: none; color: inherit;"
                >
                  <img
                    v-if="project.coverUrl"
                    :src="project.coverUrl"
                    :alt="project.coverAlt ?? ''"
                    width="56"
                    height="56"
                    loading="lazy"
                    style="flex: none; width: 56px; height: 56px; border-radius: var(--radius-control); object-fit: cover;"
                  >
                  <span
                    v-else
                    aria-hidden="true"
                    style="
                      flex: none; width: 56px; height: 56px; border-radius: var(--radius-control);
                      background: var(--charity-bg-surface-2); color: var(--charity-text-tertiary);
                      display: flex; align-items: center; justify-content: center; font-weight: 700;
                    "
                  >{{ project.name.charAt(0) }}</span>
                  <span style="flex: 1; min-width: 0;">
                    <strong style="display: block;">{{ project.name }}</strong>
                    <span v-if="project.oneLiner" class="text-secondary" style="display: block; font-size: 0.9375rem;">{{ project.oneLiner }}</span>
                    <span class="btn-link" style="font-size: 0.875rem;">{{ tr.impact.viewProject }} →</span>
                  </span>
                </NuxtLink>
              </li>
            </ul>
          </section>
        </div>

        <p v-if="impact.clubSiteUrl" style="margin-top: var(--sp-6);">
          <a :href="impact.clubSiteUrl" class="btn btn-secondary" rel="noopener">{{ tr.impact.clubLink }} →</a>
        </p>
      </template>
    </section>
  </div>
</template>
