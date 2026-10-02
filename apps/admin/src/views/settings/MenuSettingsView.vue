<script setup lang="ts">
/**
 * I 選單管理（規劃書 §4.9「選單管理」；apps/api/README.md「H 批」§5）：主選單／大選單／頁尾三個位置，
 * 多層級（最多 3 層）、雙語、外部連結、可排序。
 *
 * - `PUT …/menus/{位置}` 是**整棵樹取代**：有 id 沿用、沒有新增、既有但不在請求裡的就刪除；同層順序＝陣列順序。
 *   所以每個位置各自「儲存這個選單」，儲存成功後重新讀取（拿到新項目的 id）。後儲存者覆蓋先儲存者（無版本檢查）。
 * - 排序用上移／下移按鈕（規劃書寫「拖曳排序」，按鈕版鍵盤與觸控皆可用，拖曳為後續優化）。
 * - 前台在 API 沒有選單資料時沿用寫死的過渡選單，所以「還沒設定」的位置前台不會是空的。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MenuNodeList from './parts/MenuNodeList.vue'
import { MENU_MAX_DEPTH, countNodes, fromDto, newMenuKey, toRequest, validateTree, type MenuNode } from './parts/menuNode'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { getAdminMenus, replaceAdminMenu, type MenuLocation } from '@/api/adminSiteSettings'

const LOCATIONS: { location: MenuLocation; fallbackLabel: string; hint: string }[] = [
  { location: 'main', fallbackLabel: '主選單', hint: '網站頂端的主要導覽列。' },
  { location: 'mega', fallbackLabel: '大選單', hint: '主選單展開後的大型下拉選單（分組列出更多連結）。' },
  { location: 'footer', fallbackLabel: '頁尾選單', hint: '每一頁最下方的連結區。' },
]

const canUpdate = usePermission('site.menu.update')
const club = computed(() => activeClubId.value)

const loadState = ref<'loading' | 'ready' | 'error'>('loading')
const loadErrorMessage = ref('')
const activeTab = ref<MenuLocation>('main')
const labels = reactive<Record<MenuLocation, string>>({ main: '主選單', mega: '大選單', footer: '頁尾選單' })
const trees = reactive<Record<MenuLocation, MenuNode[]>>({ main: [], mega: [], footer: [] })
const baselines = reactive<Record<MenuLocation, string>>({ main: '[]', mega: '[]', footer: '[]' })
const savingLocation = ref<MenuLocation | null>(null)
const errors = reactive<Record<MenuLocation, string | null>>({ main: null, mega: null, footer: null })

/** 比對用快照：不含畫面 key。 */
function snap(nodes: MenuNode[]): string {
  return JSON.stringify(toRequest(nodes))
}

function isLocationDirty(location: MenuLocation): boolean {
  return snap(trees[location]) !== baselines[location]
}

const isAnyDirty = computed(() => loadState.value === 'ready' && LOCATIONS.some((l) => isLocationDirty(l.location)))
useUnsavedChanges(isAnyDirty)

async function load() {
  loadState.value = 'loading'
  try {
    const data = await getAdminMenus(club.value)
    for (const { location } of LOCATIONS) {
      const found = data.locations.find((l) => l.location === location)
      labels[location] = found?.label ?? labels[location]
      trees[location] = fromDto(found?.items ?? [])
      baselines[location] = snap(trees[location])
      errors[location] = null
    }
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}
onMounted(load)
watch(club, load)

function addRoot(location: MenuLocation) {
  trees[location].push({ key: newMenuKey(), id: null, labelZh: '', labelEn: '', url: '', isExternal: false, children: [] })
}

async function handleSave(location: MenuLocation) {
  if (!canUpdate.value) return
  const problem = validateTree(trees[location])
  if (problem) {
    errors[location] = problem
    return
  }
  // 刪掉既有項目是整棵取代的副作用，先講清楚
  const before = JSON.parse(baselines[location]) as { id?: string }[]
  const existingIds = new Set<string>()
  const collect = (items: { id?: string | null; children?: unknown[] }[]) => {
    for (const it of items) {
      if (it.id) existingIds.add(it.id)
      collect((it.children ?? []) as { id?: string | null; children?: unknown[] }[])
    }
  }
  collect(before)
  const keepIds = new Set<string>()
  const collectKeep = (nodes: MenuNode[]) => {
    for (const n of nodes) {
      if (n.id) keepIds.add(n.id)
      collectKeep(n.children)
    }
  }
  collectKeep(trees[location])
  const removed = [...existingIds].filter((id) => !keepIds.has(id)).length
  if (removed > 0) {
    try {
      await ElMessageBox.confirm(`這次儲存會刪除 ${removed} 個既有項目，前台選單會立即更新。確定要儲存嗎？`, '儲存選單', {
        confirmButtonText: '儲存',
        cancelButtonText: '取消',
        type: 'warning',
      })
    } catch {
      return
    }
  }
  savingLocation.value = location
  errors[location] = null
  try {
    await replaceAdminMenu(club.value, location, toRequest(trees[location]))
    // 重新讀取以取得新項目的 id（只更新這個位置，不動其他位置未儲存的編輯）
    const data = await getAdminMenus(club.value)
    const found = data.locations.find((l) => l.location === location)
    trees[location] = fromDto(found?.items ?? [])
    baselines[location] = snap(trees[location])
    ElMessage.success(`已儲存${labels[location]}`)
  } catch (error) {
    errors[location] = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    savingLocation.value = null
  }
}
</script>

<template>
  <div class="menu-settings">
    <PageHeader title="選單管理">
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="!canUpdate" title="你的帳號只能檢視選單，不能修改。" type="info" show-icon :closable="false" class="menu-settings__block" />
      <el-alert
        title="三個位置各自儲存。儲存時會以畫面上的整份選單為準：從畫面刪掉的項目，儲存後前台也會消失。站內連結不用加語言前綴，系統會依英文或中文版自動補上。"
        type="info"
        show-icon
        :closable="false"
        class="menu-settings__block"
      />
      <el-tabs v-model="activeTab" class="menu-settings__tabs">
        <el-tab-pane v-for="l in LOCATIONS" :key="l.location" :name="l.location">
          <template #label>
            {{ labels[l.location] }}
            <el-tag v-if="isLocationDirty(l.location)" size="small" type="warning" class="menu-settings__dirty">未儲存</el-tag>
          </template>
          <p class="menu-settings__hint">{{ l.hint }}共 {{ countNodes(trees[l.location]) }} 個項目，最多 3 層。</p>
          <el-alert v-if="errors[l.location]" :title="errors[l.location]!" type="warning" show-icon class="menu-settings__block" @close="errors[l.location] = null" />
          <el-empty v-if="trees[l.location].length === 0" description="這個位置還沒有任何項目（前台目前沿用預設選單）" :image-size="64" />
          <MenuNodeList :nodes="trees[l.location]" :depth="1" :max-depth="MENU_MAX_DEPTH" :disabled="!canUpdate" :new-key="newMenuKey" />
          <div v-if="canUpdate" class="menu-settings__footer">
            <el-button @click="addRoot(l.location)">+ 新增項目</el-button>
            <el-button type="primary" :loading="savingLocation === l.location" :disabled="!isLocationDirty(l.location)" @click="handleSave(l.location)">
              儲存{{ labels[l.location] }}
            </el-button>
          </div>
        </el-tab-pane>
      </el-tabs>
    </template>
  </div>
</template>

<style scoped>
.menu-settings { max-width: 1000px; margin: 0 auto 48px; min-width: 0; }
.menu-settings__block { margin-bottom: 16px; }
.menu-settings__hint { margin: 0 0 12px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.menu-settings__dirty { margin-left: 6px; }
.menu-settings__footer { display: flex; flex-wrap: wrap; gap: 8px; justify-content: space-between; margin-top: 16px; }
</style>
