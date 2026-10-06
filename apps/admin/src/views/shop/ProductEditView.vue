<script setup lang="ts">
/**
 * 商品——新增／編輯，含商品圖片與規格（尺寸／顏色、售價、促銷價、成本、庫存狀態）。
 * - 新增商品時只能是「下架（草稿）」；上架前至少要有一個販售中規格。
 * - 規格的庫存量不能在這裡直接改，一律到「庫存」調整（新增規格時填的期初庫存會記成一筆進貨）。
 * - 成本只有持有成本權限的人看得到、改得了；沒有權限時這一欄不出現，也不會被送出。
 * - 商品圖片、規格的新增修改刪除立即生效；商品本身的欄位按「儲存」才送出。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import MobileCardList from '@/components/MobileCardList.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import GalleryManager from '@/components/GalleryManager.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import EditLayout from '@/components/EditLayout.vue'
import FormErrorStatus from '@/components/FormErrorStatus.vue'
import FormField from '@/components/FormField.vue'
import LangTabsBar from '@/components/LangTabsBar.vue'
import { provideFormErrors } from '@/composables/useFormErrors'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { enOrUndefined, nullIfBlank } from '@/api/adminCommon'
import {
  addProductImages,
  createProduct,
  createVariant,
  deleteProductImage,
  deleteVariant,
  getProduct,
  listCollections,
  reorderProductImages,
  reorderVariants,
  updateProduct,
  updateVariant,
  type CollectionListItemDto,
  type OutOfStockBehavior,
  type ProductDetailDto,
  type ShopPublishStatus,
  type VariantDto,
  type VariantStatus,
} from '@/api/adminShop'
import { formatMoney } from '@/utils/formatMoney'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const isCreate = computed(() => route.name === 'shop-product-new')
const productId = ref<string | undefined>(route.params.id as string | undefined)
const { canCreate, canUpdate } = useCrudPermissions('shop.product')
const variantPerm = useCrudPermissions('shop.variant')
const canEditCost = usePermission('shop.cost.update')
const club = computed(() => activeClubId.value)

const form = reactive({
  slug: '',
  collectionId: '',
  isNewArrival: false,
  sortOrder: 0,
  status: 'draft' as ShopPublishStatus,
  outOfStockBehavior: 'show_unavailable' as OutOfStockBehavior,
  sizeChartText: '',
  nameZh: '', nameEn: '', narrZh: '', narrEn: '',
  seoTitleZh: '', seoTitleEn: '', seoDescZh: '', seoDescEn: '',
  tagsZh: '', tagsEn: '',
})
const baselineJson = ref('')
const collections = ref<CollectionListItemDto[]>([])
const images = ref<ProductDetailDto['images']>([])
const variants = ref<VariantDto[]>([])
const canViewVariants = ref(false)
const canViewCost = ref(false)

const loadState = ref<'loading' | 'ready' | 'error' | 'not-found'>('loading')
const loadErrorMessage = ref('')
const saving = ref(false)
/** 頁首提示：只放沒有對到欄位的錯誤；欄位錯誤標在欄位上（formErrors）。 */
const formError = ref<string | null>(null)
const formErrors = provideFormErrors()
const readOnly = computed(() => (isCreate.value ? !canCreate.value : !canUpdate.value))
const pageTitle = computed(() => (isCreate.value ? '新增商品' : `編輯：${form.nameZh || '（未命名）'}`))

function apply(d: ProductDetailDto) {
  form.slug = d.slug ?? ''
  form.collectionId = d.collectionId ?? ''
  form.isNewArrival = d.isNewArrival
  form.sortOrder = d.sortOrder
  form.status = d.status
  form.outOfStockBehavior = d.outOfStockBehavior
  form.sizeChartText = d.sizeChart === null || d.sizeChart === undefined ? '' : JSON.stringify(d.sizeChart, null, 2)
  form.nameZh = d.zh?.name ?? ''
  form.nameEn = d.en?.name ?? ''
  form.narrZh = d.zh?.narrative ?? ''
  form.narrEn = d.en?.narrative ?? ''
  form.seoTitleZh = d.zh?.seoTitle ?? ''
  form.seoTitleEn = d.en?.seoTitle ?? ''
  form.seoDescZh = d.zh?.seoDescription ?? ''
  form.seoDescEn = d.en?.seoDescription ?? ''
  form.tagsZh = d.zh?.tags ?? ''
  form.tagsEn = d.en?.tags ?? ''
  images.value = d.images ?? []
  variants.value = d.variants ?? []
  canViewVariants.value = d.canViewVariants
  canViewCost.value = d.canViewCost
}

async function load() {
  loadState.value = 'loading'
  try {
    collections.value = await listCollections(club.value).catch(() => [] as CollectionListItemDto[])
    if (!isCreate.value && productId.value) apply(await getProduct(club.value, productId.value))
    baselineJson.value = JSON.stringify(form)
    loadState.value = 'ready'
  } catch (error) {
    if (error instanceof AdminApiError && error.kind === 'not-found') loadState.value = 'not-found'
    else {
      loadErrorMessage.value = error instanceof AdminApiError ? error.message : '資料載入失敗，請稍後再試'
      loadState.value = 'error'
    }
  }
}
onMounted(load)

const isDirty = computed(() => loadState.value === 'ready' && JSON.stringify(form) !== baselineJson.value)
useUnsavedChanges(isDirty)

function parseSizeChart(): { ok: boolean; value: unknown } {
  const text = form.sizeChartText.trim()
  if (!text) return { ok: true, value: null }
  try {
    return { ok: true, value: JSON.parse(text) }
  } catch {
    return { ok: false, value: null }
  }
}

/** 一次檢查全部，回傳 欄位鍵 → 訊息（鍵只在程式內對照，不顯示）。 */
function validate(): Record<string, string> {
  const errors: Record<string, string> = {}
  if (!form.nameZh.trim()) errors.nameZh = '請輸入中文商品名稱'
  if (isCreate.value && form.status === 'published') errors.status = '新增商品時請先存成下架（草稿），加入規格後再上架'
  else if (form.status === 'published' && !isCreate.value && canViewVariants.value && !variants.value.some((v) => v.status === 'active')) {
    errors.status = '上架前至少要有一個販售中的規格'
  }
  if (!parseSizeChart().ok) errors.sizeChart = '尺寸對照表的格式不正確，請檢查後再儲存，或清空這個欄位'
  return errors
}

async function handleSave() {
  if (readOnly.value) return
  formError.value = null
  if (formErrors.replaceAll(validate())) {
    await formErrors.focusFirst()
    return
  }
  saving.value = true
  const payload = {
    slug: nullIfBlank(form.slug) ?? undefined,
    collectionId: form.collectionId || null,
    isNewArrival: form.isNewArrival,
    sortOrder: form.sortOrder,
    status: form.status,
    outOfStockBehavior: form.outOfStockBehavior,
    // 更新時省略＝清除，所以一律明確帶出畫面上的內容
    sizeChart: parseSizeChart().value,
    content: {
      zh: {
        name: form.nameZh.trim(),
        narrative: nullIfBlank(form.narrZh),
        seoTitle: nullIfBlank(form.seoTitleZh),
        seoDescription: nullIfBlank(form.seoDescZh),
        tags: nullIfBlank(form.tagsZh),
      },
      en: enOrUndefined(
        {
          name: form.nameEn.trim(),
          narrative: nullIfBlank(form.narrEn) as string,
          seoTitle: nullIfBlank(form.seoTitleEn) as string,
          seoDescription: nullIfBlank(form.seoDescEn) as string,
          tags: nullIfBlank(form.tagsEn) as string,
        },
        'name', 'narrative', 'seoTitle', 'seoDescription', 'tags',
      ),
    },
  }
  try {
    const wasCreate = isCreate.value
    const saved = wasCreate ? await createProduct(club.value, payload) : await updateProduct(club.value, productId.value!, payload)
    if (wasCreate) {
      productId.value = saved.id
      router.replace(`/shop/products/${saved.id}/edit`)
    }
    apply(saved)
    baselineJson.value = JSON.stringify(form)
    ElMessage.success(wasCreate ? '已建立，接著可以加入商品圖片與規格' : '已儲存')
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放頁首
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    formError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}

function back() {
  router.push('/shop/products')
}

async function refreshDetail() {
  if (!productId.value) return
  const d = await getProduct(club.value, productId.value)
  images.value = d.images ?? []
  variants.value = d.variants ?? []
  canViewVariants.value = d.canViewVariants
  canViewCost.value = d.canViewCost
}
async function galleryUpload(file: File) {
  await addProductImages(club.value, productId.value!, [file])
  await refreshDetail()
}
async function galleryRemove(id: string) {
  await deleteProductImage(club.value, productId.value!, id)
  await refreshDetail()
}
async function galleryReorder(ids: string[]) {
  await reorderProductImages(club.value, productId.value!, ids)
  await refreshDetail()
}

// ── 規格 ──
const varDialog = ref(false)
const varSaving = ref(false)
const varFormError = ref<string | null>(null)
const varForm = reactive({
  id: null as string | null,
  sku: '', size: '', colour: '',
  price: 0, salePrice: null as number | null,
  cost: null as number | null, originalCost: null as number | null,
  status: 'active' as VariantStatus,
  lowStockThreshold: null as number | null,
  sortOrder: 0,
  initialStock: 0,
})
const varReordering = ref(false)

function openVariant(v: VariantDto | null) {
  varFormError.value = null
  formErrors.clearAll()
  Object.assign(varForm, v
    ? { id: v.id, sku: v.sku, size: v.size ?? '', colour: v.colour ?? '', price: v.price, salePrice: v.salePrice ?? null, cost: v.cost, originalCost: v.cost, status: v.status, lowStockThreshold: v.lowStockThreshold ?? null, sortOrder: v.sortOrder, initialStock: 0 }
    : { id: null, sku: '', size: '', colour: '', price: 0, salePrice: null, cost: null, originalCost: null, status: 'active', lowStockThreshold: null, sortOrder: variants.value.length, initialStock: 0 })
  varDialog.value = true
}

async function saveVariant() {
  varFormError.value = null
  const sku = varForm.sku.trim()
  // 規格由獨立端點儲存，錯誤鍵用該端點的邏輯欄位名（sku／price／salePrice），不加 variants[i] 前綴
  const errors: Record<string, string> = {}
  if (!sku) errors.sku = '請輸入商品規格編號'
  else if (/\s/.test(sku) || sku.length > 64) errors.sku = '商品規格編號不可含空白，最多 64 字'
  if (varForm.price === null || varForm.price < 0) errors.price = '售價不能是負數'
  else if (varForm.salePrice !== null && (varForm.salePrice < 0 || varForm.salePrice > varForm.price)) errors.salePrice = '促銷價必須介於 0 與售價之間'
  if (formErrors.replaceAll(errors)) {
    await formErrors.focusFirst()
    return
  }
  varSaving.value = true
  const payload: Parameters<typeof createVariant>[2] = {
    sku,
    size: nullIfBlank(varForm.size),
    colour: nullIfBlank(varForm.colour),
    price: varForm.price,
    salePrice: varForm.salePrice,
    status: varForm.status,
    lowStockThreshold: varForm.lowStockThreshold,
    sortOrder: varForm.sortOrder,
  }
  // 成本：沒有權限時完全不送；有權限時只在有變動才送（沒帶＝維持不變）
  if (canEditCost.value && canViewCost.value && varForm.cost !== varForm.originalCost) {
    if (varForm.cost === null) payload.clearCost = true
    else payload.cost = varForm.cost
  }
  if (!varForm.id && varForm.initialStock > 0) payload.initialStock = varForm.initialStock
  try {
    if (varForm.id) await updateVariant(club.value, productId.value!, varForm.id, payload)
    else await createVariant(club.value, productId.value!, payload)
    varDialog.value = false
    ElMessage.success('已儲存規格')
    await refreshDetail()
  } catch (error) {
    // 後端標到欄位的錯誤直接標在欄位上；對不到欄位的才放對話框頂部
    if (error instanceof AdminApiError && formErrors.applyApiError(error)) return
    varFormError.value = error instanceof AdminApiError ? error.message : '儲存失敗，請稍後再試'
  } finally {
    varSaving.value = false
  }
}

async function removeVariant(v: VariantDto) {
  try {
    await ElMessageBox.confirm(`確定要刪除規格「${v.label || v.sku}」嗎？有訂單或保留紀錄的規格不能刪除，請改為停售。`, '刪除規格', {
      confirmButtonText: '刪除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger', type: 'warning',
    })
  } catch {
    return
  }
  try {
    await deleteVariant(club.value, productId.value!, v.id)
    ElMessage.success('已刪除')
    await refreshDetail()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '刪除失敗，請稍後再試')
  }
}

async function moveVariant(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= variants.value.length) return
  const ids = variants.value.map((v) => v.id)
  ;[ids[index], ids[target]] = [ids[target], ids[index]]
  varReordering.value = true
  try {
    await reorderVariants(club.value, productId.value!, ids)
    await refreshDetail()
  } catch (error) {
    ElMessage.error(error instanceof AdminApiError ? error.message : '調整順序失敗，請稍後再試')
  } finally {
    varReordering.value = false
  }
}
</script>

<template>
  <div class="product-edit">
    <PageHeader :title="pageTitle">
      <template v-if="loadState === 'ready'" #back>
        <el-button text @click="back"><el-icon><ArrowLeft /></el-icon>返回列表</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="S1" /></template>
    </PageHeader>

    <el-card v-if="loadState === 'loading'" shadow="never"><el-skeleton :rows="8" animated /></el-card>
    <el-card v-else-if="loadState !== 'ready'" shadow="never">
      <el-empty :image-size="96">
        <template #description>
          <p v-if="loadState === 'not-found'">找不到這件商品，可能已被刪除，或不屬於目前選擇的俱樂部。</p>
          <p v-else>{{ loadErrorMessage }}</p>
        </template>
        <el-button v-if="loadState === 'error'" type="primary" @click="load">重新載入</el-button>
        <el-button v-else type="primary" @click="back">返回列表</el-button>
      </el-empty>
    </el-card>

    <template v-else>
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="product-edit__block" @close="formError = null" />
      <el-alert v-if="readOnly" title="你的帳號只能檢視商品，不能修改。" type="info" show-icon :closable="false" class="product-edit__block" />
      <LangTabsBar>
        <EditLayout>
          <template #main>
            <el-form label-position="top" :disabled="readOnly" class="product-edit__form">
              <el-card shadow="never" header="商品資料">
                <BilingualShortField field="name" label="商品名稱" :zh="form.nameZh" :en="form.nameEn" required @update:zh="(v) => (form.nameZh = v)" @update:en="(v) => (form.nameEn = v)" />
                <BilingualTextareaField field="narr" label="商品介紹" :zh="form.narrZh" :en="form.narrEn" :rows="4" @update:zh="(v) => (form.narrZh = v)" @update:en="(v) => (form.narrEn = v)" />
                <el-row :gutter="12">
                  <el-col :xs="24" :sm="12">
                    <FormField field="collectionId" label="所屬系列">
                      <el-select v-model="form.collectionId" clearable placeholder="不歸類" style="width: 100%">
                        <el-option v-for="c in collections" :key="c.id" :label="c.nameZh || '（未命名）'" :value="c.id" />
                      </el-select>
                    </FormField>
                  </el-col>
                  <el-col :xs="24" :sm="12"><FormField field="slug" label="網址名稱（選填）"><el-input v-model="form.slug" maxlength="128" placeholder="留空由系統自動產生" /></FormField></el-col>
                  <el-col :xs="24" :sm="8"><el-form-item label="標示為新品"><el-switch v-model="form.isNewArrival" /></el-form-item></el-col>
                  <el-col :xs="24" :sm="8"><FormField field="sortOrder" label="排序值"><el-input-number v-model="form.sortOrder" :min="0" /></FormField></el-col>
                  <el-col :xs="24" :sm="8">
                    <FormField field="outOfStockBehavior" label="缺貨時的前台顯示">
                      <el-select v-model="form.outOfStockBehavior" style="width: 100%">
                        <el-option label="顯示為缺貨" value="show_unavailable" />
                        <el-option label="從商店隱藏" value="hide" />
                      </el-select>
                    </FormField>
                  </el-col>
                </el-row>
                <FormField field="status" label="狀態">
                  <el-radio-group v-model="form.status" @change="formErrors.clear('status')">
                    <el-radio-button value="draft">下架（草稿）</el-radio-button>
                    <el-radio-button value="published">上架</el-radio-button>
                  </el-radio-group>
                </FormField>
                <p class="product-edit__hint">新增商品時只能先存成下架；上架前至少要有一個販售中的規格。「缺貨」由庫存自動判定，不需要手動設定。已有訂單的商品不能刪除，請改為下架。</p>
              </el-card>

              <el-card shadow="never" header="搜尋與分享設定">
                <BilingualShortField field="seoTitle" label="搜尋標題" :zh="form.seoTitleZh" :en="form.seoTitleEn" @update:zh="(v) => (form.seoTitleZh = v)" @update:en="(v) => (form.seoTitleEn = v)" />
                <BilingualTextareaField field="seoDesc" label="搜尋描述" :zh="form.seoDescZh" :en="form.seoDescEn" :rows="2" @update:zh="(v) => (form.seoDescZh = v)" @update:en="(v) => (form.seoDescEn = v)" />
                <BilingualShortField field="tags" label="標籤（以逗號分隔）" :zh="form.tagsZh" :en="form.tagsEn" @update:zh="(v) => (form.tagsZh = v)" @update:en="(v) => (form.tagsEn = v)" />
              </el-card>

              <el-card shadow="never" header="尺寸對照表（選填）">
                <FormField field="sizeChart">
                  <el-input v-model="form.sizeChartText" type="textarea" :rows="6" placeholder="尚無對照表。需要時貼上對照表資料（須為系統可讀的結構化格式）；清空並儲存即清除。" />
                </FormField>
                <p class="product-edit__hint">前台的呈現格式尚未定義，目前只能原樣存放。格式不正確時無法儲存。</p>
              </el-card>
            </el-form>

            <el-card shadow="never">
              <template #header>
                <div class="product-edit__head">
                  <span>規格與售價</span>
                  <el-button v-if="!isCreate && canViewVariants && variantPerm.canCreate.value" size="small" type="primary" @click="openVariant(null)">+ 新增規格</el-button>
                </div>
              </template>
              <p v-if="isCreate" class="product-edit__hint">請先按「儲存」建立商品，儲存後就能新增規格。</p>
              <p v-else-if="!canViewVariants" class="product-edit__hint">你的帳號沒有檢視規格與售價的權限。</p>
              <template v-else>
                <p class="product-edit__hint">庫存量不能在這裡修改，請到「庫存」調整；新增規格時填的期初庫存會記成一筆進貨。有訂單的規格不能刪除，請改為停售。</p>
                <el-empty v-if="variants.length === 0" description="還沒有規格" :image-size="64" />
                <el-table v-else-if="!isMobile" :data="variants" row-key="id">
                  <el-table-column label="商品規格編號" min-width="120" prop="sku" />
                  <el-table-column label="尺寸／顏色" min-width="110"><template #default="{ row }">{{ row.label || '—' }}</template></el-table-column>
                  <el-table-column label="售價" width="110">
                    <template #default="{ row }">
                      <div>{{ formatMoney(row.price) }}</div>
                      <div v-if="row.salePrice !== null && row.salePrice !== undefined" class="product-edit__muted">促銷 {{ formatMoney(row.salePrice) }}</div>
                    </template>
                  </el-table-column>
                  <el-table-column v-if="canViewCost" label="成本" width="100"><template #default="{ row }">{{ formatMoney(row.cost) }}</template></el-table-column>
                  <el-table-column label="庫存／保留／可售" width="140"><template #default="{ row }">{{ row.stockQty }} ／ {{ row.reservedQty }} ／ <strong :class="{ 'product-edit__low': row.isLowStock }">{{ row.availableQty }}</strong></template></el-table-column>
                  <el-table-column label="狀態" width="90">
                    <template #default="{ row }">
                      <el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
                      <el-tag v-if="row.isLowStock" type="warning" size="small" class="product-edit__tag">庫存偏低</el-tag>
                    </template>
                  </el-table-column>
                  <el-table-column v-if="variantPerm.canUpdate.value" label="順序" width="96">
                    <template #default="{ $index }">
                      <el-button size="small" text :disabled="varReordering || $index === 0" aria-label="上移" @click="moveVariant($index, -1)"><el-icon><ArrowUp /></el-icon></el-button>
                      <el-button size="small" text :disabled="varReordering || $index === variants.length - 1" aria-label="下移" @click="moveVariant($index, 1)"><el-icon><ArrowDown /></el-icon></el-button>
                    </template>
                  </el-table-column>
                  <el-table-column label="操作" width="130" fixed="right">
                    <template #default="{ row }">
                      <el-button size="small" text type="primary" @click="openVariant(row)">{{ variantPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                      <el-button v-if="variantPerm.canDelete.value" size="small" text type="danger" @click="removeVariant(row)">刪除</el-button>
                    </template>
                  </el-table-column>
                </el-table>
                <MobileCardList v-else :rows="variants" row-key="id">
                  <template #title="{ row }">{{ row.label || row.sku }}</template>
                  <template #meta="{ row }">
                    <el-tag :type="row.status === 'active' ? 'success' : 'info'" size="small">{{ row.statusLabel }}</el-tag>
                    <span>{{ formatMoney(row.effectivePrice) }}</span>
                    <span>可售 {{ row.availableQty }}</span>
                    <span v-if="canViewCost">成本 {{ formatMoney(row.cost) }}</span>
                  </template>
                  <template #actions="{ row }">
                    <el-button size="small" text type="primary" @click="openVariant(row)">{{ variantPerm.canUpdate.value ? '編輯' : '檢視' }}</el-button>
                    <el-button v-if="variantPerm.canDelete.value" size="small" text type="danger" @click="removeVariant(row)">刪除</el-button>
                  </template>
                </MobileCardList>
              </template>
            </el-card>
          </template>

          <template #aside>
            <el-card shadow="never" header="商品圖片">
              <p class="product-edit__hint">這裡的變更會立即儲存，不需要按下方的儲存</p>
              <p v-if="isCreate" class="product-edit__hint">請先儲存基本資料，才能管理相簿</p>
              <GalleryManager :images="images.map((i) => ({ id: i.id, thumbUrl: i.imageThumbUrl, imageUrl: i.imageUrl }))" :disabled="isCreate || !canUpdate" :on-upload="galleryUpload" :on-remove="galleryRemove" :on-reorder="galleryReorder" />
            </el-card>
          </template>
        </EditLayout>
      </LangTabsBar>
      <EditActionBar v-if="!readOnly">
        <template #status><FormErrorStatus /></template>
        <el-button type="primary" :loading="saving" @click="handleSave">儲存</el-button>
      </EditActionBar>
    </template>

    <el-dialog v-model="varDialog" :title="varForm.id ? '編輯規格' : '新增規格'" width="560px" :close-on-click-modal="false" class="product-edit__dialog">
      <el-alert v-if="varFormError" :title="varFormError" type="warning" show-icon class="product-edit__block" @close="varFormError = null" />
      <el-form label-position="top" :disabled="varForm.id ? !variantPerm.canUpdate.value : !variantPerm.canCreate.value">
        <FormField field="sku" label="商品規格編號" required>
          <el-input v-model="varForm.sku" maxlength="64" placeholder="不可含空白，全站不可重複（含另一個俱樂部）" />
        </FormField>
        <el-row :gutter="12">
          <el-col :xs="24" :sm="12"><FormField field="size" label="尺寸"><el-input v-model="varForm.size" maxlength="32" placeholder="例如 M" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><FormField field="colour" label="顏色"><el-input v-model="varForm.colour" maxlength="32" placeholder="例如 粉紅" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><FormField field="price" label="售價（元）" required><el-input-number v-model="varForm.price" :min="0" :controls="false" style="width: 100%" @change="formErrors.clear('price'); formErrors.clear('salePrice')" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><FormField field="salePrice" label="促銷價（元，選填）"><el-input-number v-model="varForm.salePrice" :min="0" :controls="false" style="width: 100%" @change="formErrors.clear('salePrice')" /></FormField></el-col>
          <el-col v-if="canViewCost && canEditCost" :xs="24" :sm="12"><FormField field="cost" label="成本（元，選填）"><el-input-number v-model="varForm.cost" :min="0" :controls="false" style="width: 100%" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><FormField field="lowStockThreshold" label="庫存偏低門檻（選填）"><el-input-number v-model="varForm.lowStockThreshold" :min="0" :controls="false" style="width: 100%" placeholder="沿用商店設定" /></FormField></el-col>
          <el-col v-if="!varForm.id" :xs="24" :sm="12"><FormField field="initialStock" label="期初庫存"><el-input-number v-model="varForm.initialStock" :min="0" :controls="false" style="width: 100%" /></FormField></el-col>
          <el-col :xs="24" :sm="12"><el-form-item label="排序值"><el-input-number v-model="varForm.sortOrder" :min="0" style="width: 100%" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="販售狀態">
          <el-radio-group v-model="varForm.status"><el-radio-button value="active">販售中</el-radio-button><el-radio-button value="inactive">停售</el-radio-button></el-radio-group>
        </el-form-item>
        <p class="product-edit__hint">促銷價不可高於售價。庫存量請到「庫存」調整。</p>
      </el-form>
      <template #footer>
        <el-button @click="varDialog = false">關閉</el-button>
        <el-button v-if="varForm.id ? variantPerm.canUpdate.value : variantPerm.canCreate.value" type="primary" :loading="varSaving" @click="saveVariant">儲存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.product-edit { max-width: 1200px; margin: 0 auto; }
.product-edit__form { display: flex; flex-direction: column; gap: var(--admin-space-4); }
.product-edit__block { margin-bottom: 16px; }
.product-edit__hint { margin: 4px 0 10px; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.product-edit__head { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.product-edit__muted { font-size: 12px; color: var(--admin-text-tertiary); }
.product-edit__tag { margin-left: 4px; }
.product-edit__low { color: var(--admin-warning-text); }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
