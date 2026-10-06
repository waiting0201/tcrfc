<script setup lang="ts">
/**
 * 商品與規格（對應前台「站內商店」）：商品與系列兩個分頁。
 * 商品只有「上架」「下架（草稿）」兩態，「缺貨」由庫存自動判定；價格與可售量只有持有「檢視規格與售價」權限才看得到。
 * 🔴 商店收款主體是俱樂部；前台商店與結帳流程已完成，正式線上付款與電子發票待取得商店號後啟用。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import {
  createCollection,
  deleteCollection,
  deleteProduct,
  getCollection,
  listCollections,
  listProducts,
  reorderCollections,
  updateCollection,
  type CollectionListItemDto,
  type ProductListItemDto,
  type ShopPublishStatus,
} from '@/api/adminShop'
import { formatMoney } from '@/utils/formatMoney'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const productPerm = useCrudPermissions('shop.product')
const collectionPerm = useCrudPermissions('shop.collection')
const canViewVariants = usePermission('shop.variant.view')
const club = computed(() => activeClubId.value)

type Tab = 'products' | 'collections'
const tab = ref<Tab>(route.query.tab === 'collections' || !productPerm.canView.value ? 'collections' : 'products')
watch(tab, (t) => router.replace({ query: { tab: t } }))
function errorText(error: unknown, fallback: string): string {
  return error instanceof AdminApiError ? error.message : fallback
}

// ══ 系列（也供商品篩選使用） ══
const collections = ref<CollectionListItemDto[]>([])
const colLoading = ref(true)
const colError = ref<string | null>(null)
const colReordering = ref(false)
async function loadCollections() {
  colLoading.value = true
  colError.value = null
  try {
    collections.value = await listCollections(club.value)
  } catch (error) {
    collections.value = []
    colError.value = errorText(error, '系列清單載入失敗，請稍後再試')
  } finally {
    colLoading.value = false
  }
}
async function moveCollection(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= collections.value.length) return
  const ids = collections.value.map((c) => c.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  colReordering.value = true
  try {
    await reorderCollections(club.value, ids)
    await loadCollections()
  } catch (error) {
    ElMessage.error(errorText(error, '調整順序失敗，請稍後再試'))
  } finally {
    colReordering.value = false
  }
}
async function removeCollection(c: CollectionListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除系列「${c.nameZh || '（未命名）'}」嗎？底下還有商品的系列不能刪除。`, '刪除系列', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteCollection(club.value, c.id)
    ElMessage.success('已刪除')
    await loadCollections()
  } catch (error) {
    ElMessage.error(errorText(error, '刪除失敗，請稍後再試'))
  }
}

const colDialog = ref(false)
const colSaving = ref(false)
/** 對話框頂部提示：只放沒有對到欄位的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const colFormError = ref<string | null>(null)
const formErrors = provideFormErrors()
const colForm = reactive({ id: null as string | null, slug: '', sortOrder: 0, status: 'draft' as ShopPublishStatus, nameZh: '', nameEn: '', narrZh: '', narrEn: '' })
async function openCollection(c: CollectionListItemDto | null) {
  colFormError.value = null
  formErrors.clearAll()
  if (!c) {
    Object.assign(colForm, { id: null, slug: '', sortOrder: collections.value.length, status: 'draft', nameZh: '', nameEn: '', narrZh: '', narrEn: '' })
    colDialog.value = true
    return
  }
  try {
    const d = await getCollection(club.value, c.id)
    Object.assign(colForm, {
      id: d.id, slug: d.slug ?? '', sortOrder: d.sortOrder, status: d.status,
      nameZh: d.zh?.name ?? '', nameEn: d.en?.name ?? '', narrZh: d.zh?.narrative ?? '', narrEn: d.en?.narrative ?? '',
    })
    colDialog.value = true
  } catch (error) {
    ElMessage.error(errorText(error, '系列資料載入失敗'))
  }
}
async function saveCollection() {
  colFormError.value = null
  const errors: Record<string, string> = {}
  if (!colForm.nameZh.trim()) errors.nameZh = '請輸入系列中文名稱'
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  colSaving.value = true
  const payload = {
    slug: nullIfBlank(colForm.slug) ?? undefined,
    sortOrder: colForm.sortOrder,
    status: colForm.status,
    content: {
      zh: { name: colForm.nameZh.trim(), narrative: nullIfBlank(colForm.narrZh) },
      en: enOrUndefined({ name: colForm.nameEn.trim(), narrative: nullIfBlank(colForm.narrEn) as string }, 'name', 'narrative'),
    },
  }
  try {
    if (colForm.id) await updateCollection(club.value, colForm.id, payload)
    else await createCollection(club.value, payload)
    colDialog.value = false
    ElMessage.success('已儲存')
    await loadCollections()
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放對話框頂部
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    colFormError.value = errorText(error, '儲存失敗，請稍後再試')
  } finally {
    colSaving.value = false
  }
}

// ══ 商品 ══
const emptyFilter = () => ({ keyword: '', status: '', collectionId: '' })
const filters = reactive(emptyFilter())
const rows = ref<ProductListItemDto[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await listProducts(club.value, {
      keyword: filters.keyword.trim() || undefined,
      status: filters.status || undefined,
      collectionId: filters.collectionId || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = result.items
    total.value = result.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorText(error, '商品清單載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function applyFilter() {
  page.value = 1
  load()
}
function clearFilter() {
  Object.assign(filters, emptyFilter())
  applyFilter()
}
onMounted(() => {
  load()
  loadCollections()
})
watch(club, () => {
  Object.assign(filters, emptyFilter())
  page.value = 1
  load()
  loadCollections()
})

async function removeProduct(row: ProductListItemDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除商品「${row.nameZh || '（未命名）'}」嗎？規格與圖片會一併刪除。已有訂單的商品不能刪除，請改為下架。`, '刪除商品', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteProduct(club.value, row.id)
    ElMessage.success('已刪除')
    await load()
  } catch (error) {
    ElMessage.error(errorText(error, '刪除失敗，請稍後再試'))
  }
}

function displayTag(status: string): 'success' | 'warning' | 'info' {
  if (status === 'published') return 'success'
  if (status === 'sold_out') return 'warning'
  return 'info'
}
function priceText(row: ProductListItemDto): string {
  if (row.priceMin === null) return '—'
  return row.priceMin === row.priceMax ? formatMoney(row.priceMin) : `${formatMoney(row.priceMin)} ～ ${formatMoney(row.priceMax)}`
}
const goEdit = (row: ProductListItemDto) => router.push(`/shop/products/${row.id}/edit`)
</script>

<template>
  <div class="products">
    <PageHeader title="商品與規格">
      <template #meta><FrontendUnitBanner module-code="S1" /></template>
    </PageHeader>

    <el-tabs v-model="tab">
      <el-tab-pane v-if="productPerm.canView.value" label="商品" name="products">
        <el-card shadow="never" class="products__bar">
          <div class="products__row">
            <el-input v-model="filters.keyword" placeholder="搜尋商品名稱、網址名稱或商品規格編號" clearable class="products__keyword" @keyup.enter="applyFilter" @clear="applyFilter">
              <template #prefix><el-icon><Search /></el-icon></template>
            </el-input>
            <el-select v-model="filters.status" placeholder="狀態" clearable class="products__select" @change="applyFilter">
              <el-option label="上架" value="published" />
              <el-option label="下架（草稿）" value="draft" />
            </el-select>
            <el-select v-model="filters.collectionId" placeholder="系列" clearable class="products__select" @change="applyFilter">
              <el-option v-for="c in collections" :key="c.id" :label="c.nameZh || '（未命名）'" :value="c.id" />
            </el-select>
            <el-button type="primary" @click="applyFilter">篩選</el-button>
            <el-button @click="clearFilter">清除</el-button>
            <span class="products__spacer" />
            <el-button v-if="productPerm.canCreate.value" type="primary" @click="router.push('/shop/products/new')">+ 新增商品</el-button>
          </div>
          <p class="products__hint">「缺貨」由庫存自動判定：已上架且所有販售中規格都沒有可售量。{{ canViewVariants ? '' : '你的帳號沒有檢視規格與售價的權限，價格與可售量不會顯示。' }}</p>
        </el-card>

        <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
        <el-card v-else-if="loadError" shadow="never">
          <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
        </el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="rows.length === 0" description="目前沒有符合條件的商品">
            <el-button v-if="productPerm.canCreate.value" type="primary" @click="router.push('/shop/products/new')">+ 新增第一件商品</el-button>
          </el-empty>
          <template v-else>
            <el-table v-if="!isMobile" :data="rows" row-key="id">
              <el-table-column label="圖片" width="84">
                <template #default="{ row }">
                  <img v-if="row.coverThumbUrl" :src="row.coverThumbUrl" alt="" class="products__thumb">
                  <span v-else class="products__muted">—</span>
                </template>
              </el-table-column>
              <el-table-column label="商品名稱" min-width="180">
                <template #default="{ row }">
                  <div>{{ row.nameZh || '（未命名）' }}</div>
                  <div class="products__muted">{{ row.collectionName || '未歸類系列' }}<el-tag v-if="row.isNewArrival" size="small" type="warning" class="products__tag">新品</el-tag></div>
                </template>
              </el-table-column>
              <el-table-column label="狀態" width="120"><template #default="{ row }"><el-tag :type="displayTag(row.displayStatus)" size="small">{{ row.displayStatusLabel }}</el-tag></template></el-table-column>
              <el-table-column label="規格數" width="80"><template #default="{ row }">{{ row.variantCount }}</template></el-table-column>
              <el-table-column label="售價" min-width="150"><template #default="{ row }">{{ priceText(row) }}</template></el-table-column>
              <el-table-column label="可售量" width="90"><template #default="{ row }">{{ row.availableTotal ?? '—' }}</template></el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="goEdit(row)">{{ productPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="productPerm.canDelete.value" size="small" text type="danger" @click="removeProduct(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="rows" row-key="id">
              <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
              <template #meta="{ row }">
                <el-tag :type="displayTag(row.displayStatus)" size="small">{{ row.displayStatusLabel }}</el-tag>
                <span>{{ row.collectionName || '未歸類系列' }}</span>
                <span>{{ row.variantCount }} 個規格</span>
                <span>{{ priceText(row) }}</span>
              </template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="goEdit(row)">{{ productPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="productPerm.canDelete.value" size="small" text type="danger" @click="removeProduct(row)">刪除</el-button>
              </template>
            </MobileCardList>
            <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total" :page-sizes="[20, 50, 100]" :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :small="isMobile" class="products__pager" @current-change="load" @size-change="applyFilter" />
          </template>
        </el-card>
      </el-tab-pane>

      <el-tab-pane v-if="collectionPerm.canView.value" label="系列" name="collections" lazy>
        <div class="products__row products__bar">
          <span class="products__hint">系列是商品的分類，前台依這裡的順序排列；底下還有商品的系列不能刪除。</span>
          <span class="products__spacer" />
          <el-button v-if="collectionPerm.canCreate.value" type="primary" @click="openCollection(null)">+ 新增系列</el-button>
        </div>
        <el-card v-if="colLoading" shadow="never"><el-skeleton :rows="4" animated /></el-card>
        <el-card v-else-if="colError" shadow="never">
          <el-empty :description="colError"><el-button type="primary" @click="loadCollections">重新載入</el-button></el-empty>
        </el-card>
        <el-card v-else shadow="never">
          <el-empty v-if="collections.length === 0" description="還沒有系列" />
          <template v-else>
            <el-table v-if="!isMobile" :data="collections" row-key="id">
              <el-table-column label="系列名稱" min-width="180"><template #default="{ row }">{{ row.nameZh || '（未命名）' }}</template></el-table-column>
              <el-table-column label="商品數" width="90"><template #default="{ row }">{{ row.productCount }}</template></el-table-column>
              <el-table-column label="狀態" width="90"><template #default="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag></template></el-table-column>
              <el-table-column v-if="collectionPerm.canUpdate.value" label="順序" width="96">
                <template #default="{ $index }">
                  <el-button size="small" text :disabled="colReordering || $index === 0" aria-label="上移" @click="moveCollection($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
                  <el-button size="small" text :disabled="colReordering || $index === collections.length - 1" aria-label="下移" @click="moveCollection($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="130" fixed="right">
                <template #default="{ row }">
                  <el-button size="small" text type="primary" @click="openCollection(row)">{{ collectionPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                  <el-button v-if="collectionPerm.canDelete.value" size="small" text type="danger" @click="removeCollection(row)">刪除</el-button>
                </template>
              </el-table-column>
            </el-table>
            <MobileCardList v-else :rows="collections" row-key="id">
              <template #title="{ row }">{{ row.nameZh || '（未命名）' }}</template>
              <template #meta="{ row }"><el-tag :type="row.status === 'published' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag><span>{{ row.productCount }} 件商品</span></template>
              <template #actions="{ row }">
                <el-button size="small" text type="primary" @click="openCollection(row)">{{ collectionPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                <el-button v-if="collectionPerm.canDelete.value" size="small" text type="danger" @click="removeCollection(row)">刪除</el-button>
              </template>
            </MobileCardList>
          </template>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="colDialog" :title="colForm.id ? '編輯系列' : '新增系列'" width="620px" :close-on-click-modal="false" class="products__dialog">
      <el-alert v-if="colFormError" :title="colFormError" type="warning" show-icon class="products__block" @close="colFormError = null" />
      <el-form label-position="top" :disabled="colForm.id ? !collectionPerm.canUpdate.value : !collectionPerm.canCreate.value">
        <LangTabsBar variant="bare">
          <BilingualShortField field="name" label="系列名稱" :zh="colForm.nameZh" :en="colForm.nameEn" required @update:zh="(v) => (colForm.nameZh = v)" @update:en="(v) => (colForm.nameEn = v)" />
          <BilingualTextareaField field="narr" label="系列介紹" :zh="colForm.narrZh" :en="colForm.narrEn" :rows="3" @update:zh="(v) => (colForm.narrZh = v)" @update:en="(v) => (colForm.narrEn = v)" />
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><FormField field="slug" label="網址名稱（選填）"><el-input v-model="colForm.slug" maxlength="128" placeholder="留空由系統自動產生" /></FormField></el-col>
            <el-col :xs="24" :sm="12">
              <FormField field="status" label="狀態">
                <el-radio-group v-model="colForm.status"><el-radio-button value="draft">草稿</el-radio-button><el-radio-button value="published">已發布</el-radio-button></el-radio-group>
              </FormField>
            </el-col>
          </el-row>
        </LangTabsBar>
      </el-form>
      <template #footer>
        <el-button @click="colDialog = false">關閉</el-button>
        <el-button v-if="colForm.id ? collectionPerm.canUpdate.value : collectionPerm.canCreate.value" type="primary" :loading="colSaving" @click="saveCollection">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.products__bar { margin-bottom: 12px; }
.products__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.products__keyword { width: 260px; max-width: 100%; }
.products__select { width: 150px; max-width: 100%; }
.products__spacer { flex: 1; }
.products__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.products__thumb { width: 56px; height: 56px; object-fit: cover; border-radius: 3px; background: var(--admin-lightbox-neutral); }
.products__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.products__tag { margin-left: 6px; }
.products__pager { margin-top: 12px; justify-content: flex-end; }
.products__block { margin-bottom: 12px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
