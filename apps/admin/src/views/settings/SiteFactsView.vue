<script setup lang="ts">
/**
 * I 網站設定（對應主站規劃書 §7 `GEO-03`／`GEO-04`；apps/api/README.md「S1-12d」）。單一設定
 * 表單，逐俱樂部各自一份，跟著站台切換器切換（`activeClubId`），比照 H1「全站設定」
 * （`SeoSettingsView.vue`）與 H5「AI 爬蟲授權」（`AiCrawlerView.vue`）既有寫法：
 * 單筆載入／整份取代／`useUnsavedChanges` 攔離開。
 *
 * 承載成立年份與日期、首季頭銜、所屬聯賽、梯隊組成、主場場地、聯絡方式、**台中藍鯨官網網址**——
 * 全站只有這一處維護，網站頁面與提供給搜尋引擎／AI 服務的摘要資料都讀同一份值，不會兩邊各寫
 * 一次而對不起來。
 *
 * ℹ️ H 批起後台另有獨立的「場地管理」畫面（`VenueListView`／`VenueEditView`，可新增、修改、刪除、上傳照片）；以下是 S1-12d 當時
 *   「還沒有場地管理」的設計背景，這頁的主場挑選與建立新場地能力保留不變。
 *
 * 🔵 **主場場地改為「從既有場地挑選並排序」為主，保留「建立新場地」為輔**（S1-12d 後續補完，
 * 2026-09-29，`GET /api/v1/admin/{club}/venues` 上線後）：
 * - 主要動線是上方「選擇既有場地加入」下拉選單（讀全站場地清單，排除已在清單內的），選了就以
 *   該筆既有 `Venue` 的 `id` 加入 `homeVenues`，不會另外建立一筆重複資料。
 * - **保留**「建立新場地資料」的能力（清單裡確實還沒有這座場地時使用）——後端 PUT 省略 `id`
 *   即新增一筆（`AdminSiteFactsRepository.UpsertHomeVenuesAsync`），這是目前**唯一**的場地建檔
 *   管道（`Features/AdminVenues` 明文只做唯讀清單，不做新增／刪除）,拿掉這個能力會讓「清單裡
 *   還沒有的場地」完全無法登記。為避免與既有場地重名重複建檔，新建列的名稱若與既有場地名稱
 *   （去頭尾空白、不分大小寫比對）重複，前端顯示提示（不擋存檔，後端也沒有唯一性限制）。
 * - 🔴 **帶 `id` 的既有列名稱／地址欄位刻意維持可編輯**（沒有改成唯讀）：後端邏輯是「`id` 有值＝
 *   更新既有 `Venue` 列」，也就是說編輯這裡的欄位會**寫回共用場地主檔本身**，可能影響其他俱樂部
 *   或賽事對同一筆場地的引用——但目前系統唯一能修正既有場地資料的入口就是這裡（沒有獨立的場地
 *   管理畫面），拿掉可編輯能力會讓「既有場地地址打錯字」永遠無法修正。因此選擇保留可編輯、
 *   在每一列加上提示文案說明「修改會同步套用到所有引用這座場地的資料」，讓管理員知情後再改，
 *   不是靜默寫穿。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { activeClubId } from '@/auth/clubAccess'
import { getAdminSiteFacts, updateAdminSiteFacts, type AdminSiteFactsDto } from '@/api/adminSiteFacts'
import { listAdminVenues, type AdminVenueListItemDto } from '@/api/adminVenues'
import { AdminApiError } from '@/api/http'

const club = computed(() => activeClubId.value)
// 台中藍鯨官網網址概念上只屬於台中磐石（主站規劃書 §3.6「06 女子足球」入口頁），藍鯨官網本身
// 沒有 06 單元，因此只在 tcrfc 顯示這個欄位（見 `AdminSiteFactsDto.BlueWhaleSiteUrl` 檔頭）。
const showBlueWhaleField = computed(() => club.value === 'tcrfc')

interface SiteFactsForm {
  foundedYear: string
  foundingDateIso: string | null
  foundingDateDisplayZh: string
  foundingDateDisplayEn: string
  foundingTitleZh: string
  foundingTitleEn: string
  leagueNameZh: string
  leagueNameEn: string
  leagueShortNameZh: string
  leagueShortNameEn: string
  squadStructureZh: string
  squadStructureEn: string
  contactPhone: string
  contactHoursZh: string
  contactHoursEn: string
  blueWhaleSiteUrl: string
}

interface SquadCodeRow {
  value: string
}

interface HomeVenueRow {
  id: string | null
  nameZh: string
  nameEn: string
  address: string
}

function emptyForm(): SiteFactsForm {
  return {
    foundedYear: '',
    foundingDateIso: null,
    foundingDateDisplayZh: '',
    foundingDateDisplayEn: '',
    foundingTitleZh: '',
    foundingTitleEn: '',
    leagueNameZh: '',
    leagueNameEn: '',
    leagueShortNameZh: '',
    leagueShortNameEn: '',
    squadStructureZh: '',
    squadStructureEn: '',
    contactPhone: '',
    contactHoursZh: '',
    contactHoursEn: '',
    blueWhaleSiteUrl: '',
  }
}

const loadState = ref<'loading' | 'error' | 'ready'>('loading')
const loadErrorMessage = ref('')
const form = reactive<SiteFactsForm>(emptyForm())
const squadCodes = reactive<SquadCodeRow[]>([])
const homeVenues = reactive<HomeVenueRow[]>([])
const baselineJson = ref('')

// 全站場地清單（S1-12d 後續補完新增的唯讀端點），供「選擇既有場地加入」下拉選單與新建列的
// 重名提示使用。與俱樂部無關，切換站台切換器不需要重新載入。
const allVenues = ref<AdminVenueListItemDto[]>([])
const venuePickerId = ref<string | null>(null)
const availableVenueOptions = computed(() =>
  allVenues.value.filter((v) => !homeVenues.some((hv) => hv.id === v.id)),
)

const saving = ref(false)
/** 頁首提示：只放「沒有對到欄位」的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()

function snapshot() {
  return JSON.stringify({ form, squadCodes, homeVenues })
}

function applyLoaded(dto: AdminSiteFactsDto) {
  form.foundedYear = dto.foundedYear ?? ''
  form.foundingDateIso = dto.foundingDateIso ?? null
  form.foundingDateDisplayZh = dto.foundingDateDisplayZh ?? ''
  form.foundingDateDisplayEn = dto.foundingDateDisplayEn ?? ''
  form.foundingTitleZh = dto.foundingTitleZh ?? ''
  form.foundingTitleEn = dto.foundingTitleEn ?? ''
  form.leagueNameZh = dto.leagueNameZh ?? ''
  form.leagueNameEn = dto.leagueNameEn ?? ''
  form.leagueShortNameZh = dto.leagueShortNameZh ?? ''
  form.leagueShortNameEn = dto.leagueShortNameEn ?? ''
  form.squadStructureZh = dto.squadStructureZh ?? ''
  form.squadStructureEn = dto.squadStructureEn ?? ''
  form.contactPhone = dto.contactPhone ?? ''
  form.contactHoursZh = dto.contactHoursZh ?? ''
  form.contactHoursEn = dto.contactHoursEn ?? ''
  form.blueWhaleSiteUrl = dto.blueWhaleSiteUrl ?? ''

  squadCodes.splice(0, squadCodes.length, ...dto.squadCodes.map((c) => ({ value: c })))
  homeVenues.splice(
    0,
    homeVenues.length,
    ...dto.homeVenues.map((v) => ({
      id: v.id,
      nameZh: v.nameZh,
      nameEn: v.nameEn ?? '',
      address: v.address ?? '',
    })),
  )

  baselineJson.value = snapshot()
}

async function loadAllVenues() {
  try {
    allVenues.value = await listAdminVenues(club.value)
  } catch {
    // 場地清單載入失敗不影響主表單其餘欄位的檢視與儲存——只是「選擇既有場地加入」暫時沒有
    // 選項可挑，仍可用「建立新場地資料」繼續操作，比照既有 `loadPlayersForTeams` 的靜默降級寫法。
    allVenues.value = []
  }
}

async function loadFacts() {
  loadState.value = 'loading'
  try {
    const [dto] = await Promise.all([getAdminSiteFacts(club.value), loadAllVenues()])
    applyLoaded(dto)
    loadState.value = 'ready'
  } catch (error) {
    loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
    loadState.value = 'error'
  }
}

onMounted(loadFacts)
watch(club, loadFacts)

const isDirty = computed(() => loadState.value === 'ready' && snapshot() !== baselineJson.value)
useUnsavedChanges(isDirty)

function moveItem<T>(list: T[], index: number, delta: number) {
  const target = index + delta
  if (target < 0 || target >= list.length) return
  const [item] = list.splice(index, 1)
  list.splice(target, 0, item)
}

function addSquadCode() {
  squadCodes.push({ value: '' })
  formErrors.clear('squadCodes')
}

function removeSquadCode(index: number) {
  squadCodes.splice(index, 1)
  formErrors.clear('squadCodes')
}

function addHomeVenue() {
  homeVenues.push({ id: null, nameZh: '', nameEn: '', address: '' })
  formErrors.clear('homeVenues')
}

function addVenueFromPicker() {
  if (!venuePickerId.value) return
  const picked = allVenues.value.find((v) => v.id === venuePickerId.value)
  if (!picked) return
  homeVenues.push({ id: picked.id, nameZh: picked.nameZh, nameEn: picked.nameEn ?? '', address: picked.address ?? '' })
  formErrors.clear('homeVenues')
  venuePickerId.value = null
}

function removeHomeVenue(index: number) {
  homeVenues.splice(index, 1)
  // 列刪除後索引會位移，舊的逐列錯誤已不準，整批清掉讓使用者重新儲存取得最新結果
  formErrors.clearAll()
}

/** 新建列（`id` 為空）的名稱若與既有場地重複，前端提示（不擋存檔）——避免重複建檔，
 * 見檔頭「主場場地」說明。去頭尾空白、不分大小寫比對。 */
function venueNameDuplicate(nameZh: string): boolean {
  const trimmed = nameZh.trim().toLowerCase()
  if (!trimmed) return false
  return allVenues.value.some((v) => v.nameZh.trim().toLowerCase() === trimmed)
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}

  if (!form.foundedYear.trim()) errors.foundedYear = '請輸入成立年份'
  if (!form.foundingDateDisplayZh.trim()) errors.foundingDateDisplayZh = '請輸入成立年份／日期顯示文字（中文）'
  if (!form.leagueNameZh.trim()) errors.leagueNameZh = '請輸入所屬聯賽名稱（中文）'
  if (!form.squadStructureZh.trim()) errors.squadStructureZh = '請輸入梯隊組成敘述（中文）'

  if (squadCodes.some((row) => !row.value.trim())) {
    errors.squadCodes = '梯隊年齡層代碼不能留空，請刪除空白列或填入代碼'
  }

  homeVenues.forEach((venue, index) => {
    if (!venue.nameZh.trim()) errors[`homeVenues[${index}].nameZh`] = '請填寫這筆主場的中文名稱，或刪除這一列'
  })

  if (showBlueWhaleField.value && form.blueWhaleSiteUrl.trim() && !isValidHttpsUrl(form.blueWhaleSiteUrl.trim())) {
    errors.blueWhaleSiteUrl = '台中藍鯨官網網址格式不正確，須為 https:// 開頭的完整網址。'
  }

  return errors
}

/** 前端預檢，錯誤訊息逐字對照 `AdminSiteFactsRepository.ValidateBlueWhaleSiteUrl`——後端才是
 * 唯一真實的把關，這裡只是提前擋下明顯錯誤，減少一次往返。 */
function isValidHttpsUrl(value: string): boolean {
  try {
    return new URL(value).protocol === 'https:'
  } catch {
    return false
  }
}

async function handleSave() {
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  try {
    const saved = await updateAdminSiteFacts(club.value, {
      foundedYear: form.foundedYear.trim(),
      foundingDateIso: form.foundingDateIso || null,
      foundingDateDisplayZh: form.foundingDateDisplayZh.trim(),
      foundingDateDisplayEn: form.foundingDateDisplayEn.trim() || null,
      foundingTitleZh: form.foundingTitleZh.trim() || null,
      foundingTitleEn: form.foundingTitleEn.trim() || null,
      leagueNameZh: form.leagueNameZh.trim(),
      leagueNameEn: form.leagueNameEn.trim() || null,
      leagueShortNameZh: form.leagueShortNameZh.trim() || null,
      leagueShortNameEn: form.leagueShortNameEn.trim() || null,
      squadStructureZh: form.squadStructureZh.trim(),
      squadStructureEn: form.squadStructureEn.trim() || null,
      squadCodes: squadCodes.map((r) => r.value.trim()),
      homeVenues: homeVenues.map((v) => ({
        id: v.id || undefined,
        nameZh: v.nameZh.trim(),
        nameEn: v.nameEn.trim() || null,
        address: v.address.trim() || null,
      })),
      contactPhone: form.contactPhone.trim() || null,
      contactHoursZh: form.contactHoursZh.trim() || null,
      contactHoursEn: form.contactHoursEn.trim() || null,
      blueWhaleSiteUrl: form.blueWhaleSiteUrl.trim() || null,
    })
    applyLoaded(saved)
    ElMessage.success('已儲存')
  } catch (error) {
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="site-facts">
    <PageHeader title="網站設定">
      <template #meta>
        <FrontendUnitBanner module-code="I" />
      </template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never">
      <el-skeleton :rows="10" animated />
    </el-card>

    <el-card v-else-if="loadState === 'error'" shadow="never">
      <el-empty :image-size="96" :description="loadErrorMessage">
        <el-button type="primary" @click="loadFacts">重新載入</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert type="info" :closable="false" show-icon class="site-facts__notice">
        這裡填寫的資料，除了會顯示在網站上對應的頁面，也會提供給 Google 這類搜尋引擎，以及 ChatGPT、Claude 這類讀取網站內容的 AI 服務——這裡是唯一的維護處，網站頁面與提供給這些服務的資料一律讀同一份，請確保正確性。
      </el-alert>

      <el-alert
        v-if="formError"
        :title="formError"
        type="warning"
        show-icon
        class="site-facts__form-error"
        @close="formError = null"
      />

      <LangTabsBar>
        <EditLayout>
          <template #main>
            <el-card shadow="never" header="成立沿革">
              <el-form label-position="top">
                <FormField field="foundedYear" label="成立年份" required>
                  <el-input v-model="form.foundedYear" placeholder="例如：2024" class="site-facts__short-input" />
                </FormField>
                <el-form-item label="確切成立日期（選填）">
                  <el-date-picker
                    v-model="form.foundingDateIso"
                    type="date"
                    value-format="YYYY-MM-DD"
                    placeholder="尚未核實確切日期時可留空"
                    class="site-facts__short-input"
                  />
                </el-form-item>
                <BilingualShortField
                  field="foundingDateDisplay"
                  label="成立年份／日期顯示文字"
                  :zh="form.foundingDateDisplayZh"
                  :en="form.foundingDateDisplayEn"
                  required
                  placeholder="例如：2024 年創立"
                  @update:zh="(v) => (form.foundingDateDisplayZh = v)"
                  @update:en="(v) => (form.foundingDateDisplayEn = v)"
                />
                <BilingualShortField
                  field="foundingTitle"
                  label="首季頭銜（選填）"
                  :zh="form.foundingTitleZh"
                  :en="form.foundingTitleEn"
                  placeholder="成立當年若有奪冠等頭銜才填寫，沒有可留空"
                  @update:zh="(v) => (form.foundingTitleZh = v)"
                  @update:en="(v) => (form.foundingTitleEn = v)"
                />
              </el-form>
            </el-card>

            <el-card shadow="never" header="所屬聯賽">
              <el-form label-position="top">
                <BilingualShortField
                  field="leagueName"
                  label="聯賽全名"
                  :zh="form.leagueNameZh"
                  :en="form.leagueNameEn"
                  required
                  placeholder="例如：企業甲級聯賽"
                  @update:zh="(v) => (form.leagueNameZh = v)"
                  @update:en="(v) => (form.leagueNameEn = v)"
                />
                <BilingualShortField
                  field="leagueShortName"
                  label="聯賽簡稱（選填）"
                  :zh="form.leagueShortNameZh"
                  :en="form.leagueShortNameEn"
                  placeholder="有常用簡稱才填寫，沒有可留空"
                  @update:zh="(v) => (form.leagueShortNameZh = v)"
                  @update:en="(v) => (form.leagueShortNameEn = v)"
                />
              </el-form>
            </el-card>

            <el-card shadow="never" header="梯隊組成">
              <el-form label-position="top">
                <BilingualTextareaField
                  field="squadStructure"
                  label="梯隊組成敘述"
                  :zh="form.squadStructureZh"
                  :en="form.squadStructureEn"
                  required
                  :rows="3"
                  placeholder="例如：一線隊與足球學院（U15／U14／U12）三個梯隊並行的發展體系"
                  @update:zh="(v) => (form.squadStructureZh = v)"
                  @update:en="(v) => (form.squadStructureEn = v)"
                />
              </el-form>
              <p class="site-facts__hint">梯隊年齡層代碼（依顯示順序，例如 U15、U14、U12）：</p>
              <FormField field="squadCodes" label="">
                <div v-if="squadCodes.length > 0" class="site-facts__list">
                  <div v-for="(row, index) in squadCodes" :key="index" class="site-facts__row">
                    <el-input v-model="row.value" placeholder="例如：U15" class="site-facts__code-input" />
                    <el-button-group class="site-facts__order-buttons">
                      <el-button :disabled="index === 0" @click="moveItem(squadCodes, index, -1)">
                        <el-icon><ArrowUp /></el-icon>
                      </el-button>
                      <el-button :disabled="index === squadCodes.length - 1" @click="moveItem(squadCodes, index, 1)">
                        <el-icon><ArrowDown /></el-icon>
                      </el-button>
                    </el-button-group>
                    <el-button text type="danger" @click="removeSquadCode(index)">刪除</el-button>
                  </div>
                </div>
                <el-empty v-else description="目前沒有設定任何梯隊年齡層" :image-size="64" />
              </FormField>
              <el-button class="site-facts__add-button" @click="addSquadCode">+ 新增梯隊年齡層</el-button>
            </el-card>

            <el-card shadow="never" header="主場與場地">
              <p class="site-facts__hint">
                依顯示順序排列，第一筆是主要主場（聯絡地址會自動取自這一筆場地，不需要另外填寫）。從清單移除不會刪除場地資料本身，只是不再視為這個俱樂部的主場。
              </p>

              <div class="site-facts__venue-picker">
                <el-select
                  v-model="venuePickerId"
                  placeholder="選擇既有場地加入主場清單"
                  filterable
                  clearable
                  class="site-facts__venue-picker-select"
                >
                  <el-option v-for="v in availableVenueOptions" :key="v.id" :label="v.nameZh" :value="v.id" />
                </el-select>
                <el-button type="primary" :disabled="!venuePickerId" @click="addVenueFromPicker">加入</el-button>
              </div>

              <FormField field="homeVenues" label="">
                <div v-if="homeVenues.length > 0" class="site-facts__venue-list">
                  <el-card v-for="(venue, index) in homeVenues" :key="index" shadow="never" class="site-facts__venue-card">
                    <div class="site-facts__venue-head">
                      <span class="site-facts__venue-index">{{ index === 0 ? '主要主場' : `第 ${index + 1} 順位` }}</span>
                      <div class="site-facts__venue-head-actions">
                        <el-button-group class="site-facts__order-buttons">
                          <el-button :disabled="index === 0" @click="moveItem(homeVenues, index, -1)">
                            <el-icon><ArrowUp /></el-icon>
                          </el-button>
                          <el-button :disabled="index === homeVenues.length - 1" @click="moveItem(homeVenues, index, 1)">
                            <el-icon><ArrowDown /></el-icon>
                          </el-button>
                        </el-button-group>
                        <el-button text type="danger" @click="removeHomeVenue(index)">移除</el-button>
                      </div>
                    </div>
                    <p v-if="venue.id" class="site-facts__venue-shared-hint">
                      這是既有共用場地資料，修改名稱或地址會同步套用到所有引用這座場地的資料（例如其他俱樂部的賽事），請確認後再修改。
                    </p>
                    <el-form label-position="top">
                      <BilingualShortField
                        :field-zh="`homeVenues[${index}].nameZh`"
                        :field-en="`homeVenues[${index}].nameEn`"
                        label="場地名稱"
                        :zh="venue.nameZh"
                        :en="venue.nameEn"
                        required
                        placeholder="例如：西屯足球場"
                        @update:zh="(v) => (venue.nameZh = v)"
                        @update:en="(v) => (venue.nameEn = v)"
                      />
                      <el-form-item label="地址（選填）">
                        <el-input v-model="venue.address" placeholder="例如：台中市北屯區崇平路二段景谷巷 11 弄 41 號" />
                      </el-form-item>
                      <p v-if="!venue.id && venueNameDuplicate(venue.nameZh)" class="site-facts__venue-duplicate-hint">
                        已有相同名稱的既有場地，建議改用上方「選擇既有場地加入」，避免建立重複資料。
                      </p>
                    </el-form>
                  </el-card>
                </div>
                <el-empty v-else description="目前沒有設定任何主場場地" :image-size="64" />
              </FormField>
              <el-button class="site-facts__add-button" @click="addHomeVenue">+ 建立新場地資料（清單裡沒有這座場地時才使用）</el-button>
            </el-card>

            <el-card v-if="showBlueWhaleField" shadow="never" header="台中藍鯨官網連結">
              <el-alert type="info" :closable="false" show-icon class="site-facts__hint-alert">
                這個網址會用在「女子足球」入口頁「前往台中藍鯨官網」按鈕的連結目標。台中藍鯨官網有自己獨立的網站設定，這裡填寫的網址只影響主站這一個按鈕。
              </el-alert>
              <el-form label-position="top">
                <FormField field="blueWhaleSiteUrl" label="台中藍鯨官網網址（選填，須為 https:// 開頭的完整網址）">
                  <el-input v-model="form.blueWhaleSiteUrl" placeholder="例如：https://bluewhale.tcrfc.tw" />
                </FormField>
              </el-form>
            </el-card>

            <el-card shadow="never" header="聯絡方式">
              <el-form label-position="top">
                <FormField field="contactPhone" label="聯絡電話（選填，目前尚未核實可留空）">
                  <el-input v-model="form.contactPhone" placeholder="選填" class="site-facts__short-input" />
                </FormField>
                <BilingualShortField
                  field="contactHours"
                  label="營業時間（選填）"
                  :zh="form.contactHoursZh"
                  :en="form.contactHoursEn"
                  placeholder="例如：平日 09:00–18:00"
                  @update:zh="(v) => (form.contactHoursZh = v)"
                  @update:en="(v) => (form.contactHoursEn = v)"
                />
              </el-form>
            </el-card>
          </template>
        </EditLayout>
      </LangTabsBar>

      <EditActionBar>
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.site-facts {
  max-width: 780px;
  margin: 0 auto;
}

.site-facts__notice,
.site-facts__form-error {
  margin-bottom: 12px;
}

.site-facts__hint {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin: 4px 0 12px;
}

.site-facts__short-input {
  max-width: 280px;
}

.site-facts__list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 12px;
}

.site-facts__row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.site-facts__code-input {
  max-width: 200px;
}

.site-facts__order-buttons {
  flex-shrink: 0;
}

.site-facts__add-button {
  margin-top: 4px;
}

.site-facts__venue-picker {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.site-facts__venue-picker-select {
  max-width: 320px;
  flex: 1;
}

.site-facts__venue-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 12px;
}

.site-facts__venue-shared-hint {
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin: 0 0 8px;
}

.site-facts__venue-duplicate-hint {
  font-size: 12px;
  color: var(--el-color-warning);
  margin: -4px 0 8px;
}

.site-facts__hint-alert {
  margin-bottom: 12px;
}

.site-facts__venue-card {
  background: var(--admin-bg-surface-2);
}

.site-facts__venue-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.site-facts__venue-index {
  font-size: 13px;
  font-weight: 600;
  color: var(--admin-text-secondary);
}

.site-facts__venue-head-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

</style>
