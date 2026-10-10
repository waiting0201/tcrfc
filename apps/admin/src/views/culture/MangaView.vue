<script setup lang="ts">
/**
 * 漫畫（對應前台「台中磐石文化」的漫畫閱讀）：企劃介紹、角色、集數三個分頁。
 * 🔴 台中藍鯨不設漫畫：切到藍鯨站台時整頁只顯示說明，不呼叫任何漫畫端點。
 * 「最新一集」由系統依已發布且發布日已到的最大集數自動判定，不是人工勾選。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import ImageAltField from '@/components/ImageAltField.vue'
import ImageUploader from '@/components/ImageUploader.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions } from '@/composables/useCrudPermissions'
import { useClubFeatures } from '@/composables/useClubFeatures'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import { listAdminPlayers, type AdminPlayerListItemDto } from '@/api/adminPlayers'
import {
  createMangaCharacter,
  deleteMangaCharacter,
  deleteMangaEpisode,
  getMangaAbout,
  listMangaCharacters,
  listMangaEpisodes,
  reorderMangaCharacters,
  saveMangaAbout,
  updateMangaCharacter,
  type MangaCharacterDto,
  type MangaEpisodeListItemDto,
} from '@/api/adminManga'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const { comicAvailable } = useClubFeatures()
const { canCreate, canUpdate, canDelete } = useCrudPermissions('culture.comic')
const club = computed(() => activeClubId.value)

type Tab = 'about' | 'characters' | 'episodes'
const validTabs: Tab[] = ['about', 'characters', 'episodes']
const readTab = (): Tab => (validTabs.includes(route.query.tab as Tab) ? (route.query.tab as Tab) : 'about')
const tab = ref<Tab>(readTab())
watch(tab, (t) => router.replace({ query: { tab: t } }))

// 企劃介紹與角色對話框共用一份欄位錯誤（兩者欄位鍵不重疊；同一時間只會驗證其中一個）
const formErrors = provideFormErrors()

function errorText(error: unknown, fallback: string): string {
  return error instanceof AdminApiError ? error.message : fallback
}

// ══ 企劃介紹 ══
const about = reactive({ titleZh: '', bodyZh: '', titleEn: '', bodyEn: '' })
const aboutBaseline = ref('')
const aboutLoading = ref(true)
const aboutError = ref<string | null>(null)
const aboutSaving = ref(false)
const aboutFormError = ref<string | null>(null)
const aboutDirty = computed(() => JSON.stringify(about) !== aboutBaseline.value)

async function loadAbout() {
  aboutLoading.value = true
  aboutError.value = null
  try {
    const d = await getMangaAbout(club.value)
    about.titleZh = d.zh?.title ?? ''
    about.bodyZh = d.zh?.body ?? ''
    about.titleEn = d.en?.title ?? ''
    about.bodyEn = d.en?.body ?? ''
    aboutBaseline.value = JSON.stringify(about)
  } catch (error) {
    aboutError.value = errorText(error, '企劃介紹載入失敗，請稍後再試')
  } finally {
    aboutLoading.value = false
  }
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validateAbout(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!about.titleZh.trim()) errors.titleZh = '請輸入中文標題'
  else if (about.titleZh.trim().length > 200) errors.titleZh = '標題最多 200 字'
  if (about.titleEn.trim().length > 200) errors.titleEn = '標題最多 200 字'
  if (about.bodyZh.length > 20000) errors.bodyZh = '內文最多 20000 字'
  if (about.bodyEn.length > 20000) errors.bodyEn = '內文最多 20000 字'
  return errors
}

async function saveAbout() {
  aboutFormError.value = null
  if (formErrors.replaceAll(validateAbout())) {
    await formErrors.focusFirst()
    return
  }
  aboutSaving.value = true
  try {
    const en = enOrUndefined({ title: about.titleEn.trim(), body: about.bodyEn }, 'title', 'body')
    await saveMangaAbout(club.value, { zh: { title: about.titleZh.trim(), body: about.bodyZh }, en })
    aboutBaseline.value = JSON.stringify(about)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    aboutFormError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    aboutSaving.value = false
  }
}

// ══ 角色 ══
const characters = ref<MangaCharacterDto[]>([])
const charLoading = ref(true)
const charError = ref<string | null>(null)
const reordering = ref(false)
const players = ref<AdminPlayerListItemDto[]>([])

async function loadCharacters() {
  charLoading.value = true
  charError.value = null
  try {
    characters.value = await listMangaCharacters(club.value)
  } catch (error) {
    characters.value = []
    charError.value = errorText(error, '角色清單載入失敗，請稍後再試')
  } finally {
    charLoading.value = false
  }
}
async function loadPlayers() {
  try {
    players.value = await listAdminPlayers(club.value)
  } catch {
    players.value = []
  }
}
async function moveCharacter(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= characters.value.length) return
  const ids = characters.value.map((c) => c.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  reordering.value = true
  try {
    await reorderMangaCharacters(club.value, ids)
    await loadCharacters()
  } catch (error) {
    ElMessage.error(errorText(error, '調整順序失敗，請稍後再試'))
  } finally {
    reordering.value = false
  }
}

const charDialog = ref(false)
const charSaving = ref(false)
const charFormError = ref<string | null>(null)
const charForm = reactive({ id: null as string | null, playerId: '', nameZh: '', nameEn: '', descZh: '', descEn: '', imageAltZh: '', imageAltEn: '' })
const charFile = ref<File | null>(null)
const charRemoveImage = ref(false)
const charHasImage = ref(false)
const charImageUrl = ref<string | null>(null)
const charImageWidth = ref<number | null>(null)
const charImageHeight = ref<number | null>(null)
watch([charFile, charRemoveImage], () => formErrors.clear('image'))

function openCharacter(c: MangaCharacterDto | null) {
  charFormError.value = null
  formErrors.clearAll()
  Object.assign(charForm, {
    id: c?.id ?? null,
    playerId: c?.playerId ?? '',
    nameZh: c?.zh.name ?? '', nameEn: c?.en?.name ?? '',
    descZh: c?.zh.description ?? '', descEn: c?.en?.description ?? '',
    imageAltZh: c?.zh.imageAlt ?? '', imageAltEn: c?.en?.imageAlt ?? '',
  })
  charFile.value = null
  charRemoveImage.value = false
  charHasImage.value = !!c?.imageKey
  charImageUrl.value = c?.imageThumbUrl ?? c?.imageUrl ?? null
  charImageWidth.value = c?.imageWidth ?? null
  charImageHeight.value = c?.imageHeight ?? null
  charDialog.value = true
}

function validateCharacter(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!charForm.nameZh.trim()) errors.nameZh = '請輸入角色中文名稱'
  else if (charForm.nameZh.trim().length > 64) errors.nameZh = '角色名稱最多 64 字'
  return errors
}

async function saveCharacter() {
  charFormError.value = null
  if (formErrors.replaceAll(validateCharacter())) {
    await formErrors.focusFirst()
    return
  }
  charSaving.value = true
  const payload = {
    playerId: charForm.playerId || null,
    removeImage: charFile.value ? false : charRemoveImage.value,
    content: {
      zh: { name: charForm.nameZh.trim(), description: nullIfBlank(charForm.descZh), imageAlt: nullIfBlank(charForm.imageAltZh) },
      en: enOrUndefined(
        { name: charForm.nameEn.trim(), description: nullIfBlank(charForm.descEn) as string, imageAlt: nullIfBlank(charForm.imageAltEn) as string },
        'name', 'description', 'imageAlt',
      ),
    },
  }
  try {
    if (charForm.id) await updateMangaCharacter(club.value, charForm.id, payload, charFile.value)
    else await createMangaCharacter(club.value, payload, charFile.value)
    charDialog.value = false
    ElMessage.success('已儲存')
    await loadCharacters()
  } catch (error) {
    // 送出失敗不關對話框；對得到欄位的標在欄位上
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    charFormError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    charSaving.value = false
  }
}

async function removeCharacter(c: MangaCharacterDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除角色「${c.zh.name}」嗎？角色圖片會一併刪除，無法復原。`, '刪除角色', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteMangaCharacter(club.value, c.id)
    ElMessage.success('已刪除')
    await loadCharacters()
  } catch (error) {
    ElMessage.error(errorText(error, '刪除失敗，請稍後再試'))
  }
}

// ══ 集數 ══
const episodes = ref<MangaEpisodeListItemDto[]>([])
const epLoading = ref(true)
const epError = ref<string | null>(null)
const epStatus = ref('')

async function loadEpisodes() {
  epLoading.value = true
  epError.value = null
  try {
    episodes.value = await listMangaEpisodes(club.value, { status: epStatus.value || undefined })
  } catch (error) {
    episodes.value = []
    epError.value = errorText(error, '集數清單載入失敗，請稍後再試')
  } finally {
    epLoading.value = false
  }
}
async function removeEpisode(e: MangaEpisodeListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除第 ${e.episodeNo} 集嗎？封面與全部內頁都會一併刪除，無法復原。`, '刪除集數', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteMangaEpisode(club.value, e.id)
    ElMessage.success('已刪除')
    await loadEpisodes()
  } catch (error) {
    ElMessage.error(errorText(error, '刪除失敗，請稍後再試'))
  }
}

function loadAll() {
  if (!comicAvailable.value) return
  loadAbout()
  loadCharacters()
  loadEpisodes()
  loadPlayers()
}
onMounted(loadAll)
watch(club, () => {
  epStatus.value = ''
  loadAll()
})
</script>

<template>
  <div class="manga">
    <PageHeader title="漫畫">
      <template #meta><FrontendUnitBanner module-code="F1" /></template>
    </PageHeader>

    <el-card v-if="!comicAvailable" shadow="never">
      <el-empty description="台中藍鯨不設漫畫，這個功能只在台中磐石使用。請從左上角的站台切換器切回台中磐石。" />
    </el-card>

    <el-tabs v-else v-model="tab">
      <!-- 企劃介紹 -->
      <el-tab-pane label="企劃介紹" name="about" lazy>
        <el-card v-if="aboutLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="aboutError" shadow="never">
          <el-empty :description="aboutError"><el-button type="primary" @click="loadAbout">重新載入</el-button></el-empty>
        </el-card>
        <LangTabsBar v-else>
          <el-card shadow="never" class="manga__about">
            <p class="manga__hint">這是前台漫畫入口的「世界觀說明」頁。中文為必填，英文留空代表前台沒有英文版。</p>
            <el-alert v-if="aboutFormError" :title="aboutFormError" type="warning" show-icon class="manga__block" @close="aboutFormError = null" />
            <el-form label-position="top" :disabled="!canUpdate">
              <BilingualShortField field="title" label="標題" :zh="about.titleZh" :en="about.titleEn" required @update:zh="(v) => (about.titleZh = v)" @update:en="(v) => (about.titleEn = v)" />
              <BilingualTextareaField field="body" label="內文" :zh="about.bodyZh" :en="about.bodyEn" :rows="10" @update:zh="(v) => (about.bodyZh = v)" @update:en="(v) => (about.bodyEn = v)" />
            </el-form>
            <el-button v-if="canUpdate" type="primary" :loading="aboutSaving" :disabled="!aboutDirty" @click="saveAbout">儲存企劃介紹</el-button>
          </el-card>
        </LangTabsBar>
      </el-tab-pane>

      <!-- 角色 -->
      <el-tab-pane label="角色" name="characters" lazy>
        <div class="manga__bar">
          <span class="manga__hint">角色可以關聯真實球員；前台依這裡的順序排列。</span>
          <span class="manga__spacer" />
          <el-button v-if="canCreate" type="primary" @click="openCharacter(null)">+ 新增角色</el-button>
        </div>
        <el-card v-if="charLoading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
        <el-card v-else-if="charError" shadow="never">
          <el-empty :description="charError"><el-button type="primary" @click="loadCharacters">重新載入</el-button></el-empty>
        </el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="characters.length === 0" description="還沒有角色">
            <el-button v-if="canCreate" type="primary" @click="openCharacter(null)">+ 新增第一個角色</el-button>
          </el-empty>
          <template v-else>
            <el-table v-if="!isMobile" :data="characters" row-key="id">
              <el-table-column label="圖片" width="84">
                <template #default="{ row }">
                  <img v-if="row.imageThumbUrl || row.imageUrl" :src="(row.imageThumbUrl || row.imageUrl)!" alt="" class="manga__thumb">
                  <span v-else class="manga__muted">—</span>
                </template>
              </el-table-column>
              <el-table-column label="角色名稱" min-width="150"><template #default="{ row }">{{ row.zh.name }}</template></el-table-column>
              <el-table-column label="關聯球員" min-width="120"><template #default="{ row }">{{ row.playerName || '—' }}</template></el-table-column>
              <el-table-column label="簡介" min-width="200"><template #default="{ row }"><span class="manga__clip">{{ row.zh.description || '—' }}</span></template></el-table-column>
              <el-table-column v-if="canUpdate" label="順序" width="96">
                <template #default="{ $index }">
                  <el-button size="small" text :disabled="reordering || $index === 0" aria-label="上移" @click="moveCharacter($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
                  <el-button size="small" text :disabled="reordering || $index === characters.length - 1" aria-label="下移" @click="moveCharacter($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="openCharacter(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="canDelete" size="small" text type="danger" @click="removeCharacter(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="characters" row-key="id">
              <template #title="{ row }">{{ row.zh.name }}</template>
              <template #meta="{ row }"><span>{{ row.playerName ? `關聯球員：${row.playerName}` : '未關聯球員' }}</span></template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="openCharacter(row)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="canDelete" size="small" text type="danger" @click="removeCharacter(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
        </el-card>
      </el-tab-pane>

      <!-- 集數 -->
      <el-tab-pane label="集數" name="episodes" lazy>
        <div class="manga__bar">
          <el-select v-model="epStatus" placeholder="全部狀態" clearable class="manga__select" @change="loadEpisodes">
            <el-option label="已發布" value="published" />
            <el-option label="草稿" value="draft" />
          </el-select>
          <span class="manga__hint">「最新一集」由系統自動判定（已發布且發布日已到的最大集數）。全部集數免費公開閱讀。</span>
          <span class="manga__spacer" />
          <el-button v-if="canCreate" type="primary" @click="router.push('/culture/manga/episodes/new')">+ 新增集數</el-button>
        </div>
        <el-card v-if="epLoading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
        <el-card v-else-if="epError" shadow="never">
          <el-empty :description="epError"><el-button type="primary" @click="loadEpisodes">重新載入</el-button></el-empty>
        </el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="episodes.length === 0" description="目前沒有符合條件的集數">
            <el-button v-if="canCreate" type="primary" @click="router.push('/culture/manga/episodes/new')">+ 新增第一集</el-button>
          </el-empty>
          <template v-else>
            <el-table v-if="!isMobile" :data="episodes" row-key="id">
              <el-table-column label="集數" width="80"><template #default="{ row }">第 {{ row.episodeNo }} 集</template></el-table-column>
              <el-table-column label="封面" width="84">
                <template #default="{ row }">
                  <img v-if="row.coverThumbUrl || row.coverUrl" :src="(row.coverThumbUrl || row.coverUrl)!" alt="" class="manga__thumb">
                  <span v-else class="manga__muted">—</span>
                </template>
              </el-table-column>
              <el-table-column label="標題" min-width="180"><template #default="{ row }">{{ row.titleZh || '（未命名）' }}</template></el-table-column>
              <el-table-column label="狀態" width="130">
                <template #default="{ row }">
                  <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
                  <el-tag v-if="row.isLatest" size="small" type="warning" class="manga__tag">最新一集</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="發布日" width="120"><template #default="{ row }">{{ row.publishedOn || '—' }}</template></el-table-column>
              <el-table-column label="頁數" width="80"><template #default="{ row }">{{ row.pageCount }}</template></el-table-column>
              <el-table-column label="閱讀數" width="90"><template #default="{ row }">{{ row.viewCount }}</template></el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="router.push(`/culture/manga/episodes/${row.id}/edit`)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="canDelete" size="small" text type="danger" @click="removeEpisode(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="episodes" row-key="id">
              <template #title="{ row }">第 {{ row.episodeNo }} 集・{{ row.titleZh || '（未命名）' }}</template>
              <template #meta="{ row }">
                <el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
                <el-tag v-if="row.isLatest" size="small" type="warning">最新一集</el-tag>
                <span>{{ row.pageCount }} 頁・閱讀 {{ row.viewCount }}</span>
              </template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="router.push(`/culture/manga/episodes/${row.id}/edit`)">{{ canUpdate ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="canDelete" size="small" text type="danger" @click="removeEpisode(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="charDialog" :title="charForm.id ? '編輯角色' : '新增角色'" width="600px" :close-on-click-modal="false" class="manga__dialog">
      <el-alert v-if="charFormError" :title="charFormError" type="warning" show-icon class="manga__block" @close="charFormError = null" />
      <el-form label-position="top" :disabled="!canUpdate && !!charForm.id">
        <LangTabsBar variant="bare">
          <BilingualShortField field="name" label="角色名稱" :zh="charForm.nameZh" :en="charForm.nameEn" required @update:zh="(v) => (charForm.nameZh = v)" @update:en="(v) => (charForm.nameEn = v)" />
          <BilingualTextareaField field="desc" label="角色簡介" :zh="charForm.descZh" :en="charForm.descEn" :rows="3" @update:zh="(v) => (charForm.descZh = v)" @update:en="(v) => (charForm.descEn = v)" />
          <FormField field="playerId" label="關聯真實球員（選填）">
            <el-select v-model="charForm.playerId" clearable filterable placeholder="不關聯球員" style="width: 100%" @change="formErrors.clear('playerId')">
              <el-option v-for="p in players" :key="p.id" :label="`${p.shirtNo ? p.shirtNo + ' 號 ' : ''}${p.nameZh || p.nameEn || '（未命名）'}`" :value="p.id" />
            </el-select>
          </FormField>
          <FormField field="image" label="角色圖片">
            <ImageUploader v-model:file="charFile" v-model:remove-cover="charRemoveImage" :min-width="0" :min-height="0" :has-existing-image="charHasImage" :existing-preview-url="charImageUrl" :disabled="charSaving" />
          </FormField>
          <ImageAltField
            v-model:zh="charForm.imageAltZh"
            v-model:en="charForm.imageAltEn"
            field="imageAlt"
            :has-image="(charHasImage && !charRemoveImage) || charFile !== null"
            :width="charImageWidth"
            :height="charImageHeight"
            fallback="角色名稱"
          />
        </LangTabsBar>
      </el-form>
      <template #footer>
        <el-button @click="charDialog = false">關閉</el-button>
        <el-button v-if="charForm.id ? canUpdate : canCreate" type="primary" :loading="charSaving" @click="saveCharacter">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.manga__block { margin-bottom: 12px; }
.manga__hint { margin: 0 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.manga__bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 12px; }
.manga__bar .manga__hint { margin: 0; }
.manga__spacer { flex: 1; }
.manga__select { width: 150px; max-width: 100%; }
.manga__thumb { width: 56px; height: 56px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.manga__muted { color: var(--admin-text-tertiary); }
.manga__tag { margin-left: 4px; }
.manga__clip { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.manga__about { max-width: 820px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
