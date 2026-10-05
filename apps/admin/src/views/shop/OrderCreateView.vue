<script setup lang="ts">
/**
 * 手動建單（現場收款、賽事日擺攤、現場補登）。
 * 付款方式固定「現場收款」，建立後視為已付款並立即扣庫存；價格取當下售價（有促銷價用促銷價）。
 * 購物車不得跨俱樂部混買：只能選目前俱樂部的規格。可售量不足時整張訂單不成立。
 * 🔴 這不是線上結帳：前台結帳流程已完成，但正式線上付款與電子發票待取得商店號後才啟用，現場收款一律走這裡。
 */
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import EditActionBar from '@/components/EditActionBar.vue'
import { useUnsavedChanges } from '@/composables/useUnsavedChanges'
import { useCrudPermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { nullIfBlank } from '@/api/adminCommon'
import { listMembers, type MemberListItemDto } from '@/api/adminMembers'
import { createOrder, DELIVERY_METHOD_OPTIONS, listInventory, type InventoryItemDto } from '@/api/adminShop'

const router = useRouter()
const { canCreate } = useCrudPermissions('shop.order')
const canSearchInventory = usePermission('shop.inventory.view')
const canSearchMembers = usePermission('member.account.view')
const club = computed(() => activeClubId.value)

interface Line {
  variantId: string
  label: string
  sku: string
  availableQty: number
  quantity: number
}
const lines = ref<Line[]>([])
const form = reactive({
  deliveryMethod: 'onsite_pickup',
  recipientName: '', recipientPhone: '', recipientAddress: '',
  memberId: '',
  customerNote: '', internalNote: '',
  overrideFee: false, shippingFee: 0,
  completeImmediately: true,
})
const dirty = ref(false)
useUnsavedChanges(computed(() => dirty.value && !saving.value))
const saving = ref(false)
const formError = ref<string | null>(null)

// ── 規格搜尋 ──
const variantOptions = ref<InventoryItemDto[]>([])
const searching = ref(false)
const picked = ref('')
async function searchVariants(keyword: string) {
  const k = keyword.trim()
  if (!k) {
    variantOptions.value = []
    return
  }
  searching.value = true
  try {
    variantOptions.value = (await listInventory(club.value, { keyword: k, status: 'active', pageSize: 20 })).items
  } catch {
    variantOptions.value = []
  } finally {
    searching.value = false
  }
}
function addVariant(id: string) {
  const v = variantOptions.value.find((o) => o.variantId === id)
  picked.value = ''
  if (!v) return
  const existing = lines.value.find((l) => l.variantId === v.variantId)
  if (existing) existing.quantity += 1
  else lines.value.push({ variantId: v.variantId, label: `${v.productName} ${v.label || ''}`.trim(), sku: v.sku, availableQty: v.availableQty, quantity: 1 })
  dirty.value = true
}
function removeLine(index: number) {
  lines.value.splice(index, 1)
  dirty.value = true
}

// ── 會員（選填） ──
const memberOptions = ref<MemberListItemDto[]>([])
const memberSearching = ref(false)
async function searchMembers(keyword: string) {
  const k = keyword.trim()
  if (!k) {
    memberOptions.value = []
    return
  }
  memberSearching.value = true
  try {
    memberOptions.value = (await listMembers(club.value, { keyword: k }, 1, 10)).items
  } catch {
    memberOptions.value = []
  } finally {
    memberSearching.value = false
  }
}

const needsPhone = computed(() => form.deliveryMethod !== 'onsite_pickup')
const needsAddress = computed(() => form.deliveryMethod === 'home_delivery')
const overStock = computed(() => lines.value.filter((l) => l.quantity > l.availableQty))

function validate(): string | null {
  if (lines.value.length === 0) return '請至少加入一個商品規格'
  if (lines.value.length > 50) return '一張訂單最多 50 個品項'
  if (lines.value.some((l) => !Number.isInteger(l.quantity) || l.quantity < 1)) return '數量必須是 1 以上的整數'
  if (needsPhone.value && (!form.recipientName.trim() || !form.recipientPhone.trim())) return '宅配與超商取貨必須填寫收件人姓名與電話'
  if (needsAddress.value && !form.recipientAddress.trim()) return '宅配必須填寫收件地址'
  return null
}

async function handleSave() {
  formError.value = validate()
  if (formError.value) return
  saving.value = true
  try {
    const order = await createOrder(club.value, {
      items: lines.value.map((l) => ({ variantId: l.variantId, quantity: l.quantity })),
      deliveryMethod: form.deliveryMethod,
      recipientName: nullIfBlank(form.recipientName),
      recipientPhone: nullIfBlank(form.recipientPhone),
      recipientAddress: nullIfBlank(form.recipientAddress),
      memberId: form.memberId || null,
      customerNote: nullIfBlank(form.customerNote),
      internalNote: nullIfBlank(form.internalNote),
      shippingFee: form.overrideFee ? form.shippingFee : null,
      completeImmediately: form.deliveryMethod === 'onsite_pickup' ? form.completeImmediately : false,
    })
    dirty.value = false
    ElMessage.success('訂單已建立，庫存已扣減')
    router.replace(order?.id ? `/shop/orders/${order.id}` : '/shop/orders')
  } catch (error) {
    formError.value = error instanceof AdminApiError ? error.message : '建立失敗，請稍後再試'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="order-create">
    <PageHeader title="手動建單">
      <template #back><el-button text @click="router.push('/shop/orders')"><el-icon><ArrowLeft /></el-icon>返回訂單</el-button></template>
      <template #meta><FrontendUnitBanner module-code="S3" /></template>
    </PageHeader>

    <el-alert v-if="!canCreate" title="你的帳號沒有建立訂單的權限。" type="info" show-icon :closable="false" class="order-create__block" />
    <template v-else>
      <el-alert type="info" show-icon :closable="false" class="order-create__block" title="付款方式固定為「現場收款」，建立後視為已付款並立即扣庫存。金流與電子發票尚未串接。可售量不足時整張訂單不會成立。" />
      <el-alert v-if="formError" :title="formError" type="warning" show-icon class="order-create__block" @close="formError = null" />

      <el-card shadow="never" header="訂購品項" class="order-create__block">
        <p v-if="!canSearchInventory" class="order-create__hint">你的帳號沒有檢視庫存的權限，無法搜尋商品規格。</p>
        <el-select v-else v-model="picked" filterable remote clearable :remote-method="searchVariants" :loading="searching" placeholder="輸入商品名稱或商品規格編號搜尋，選取後加入" style="width: 100%" @change="addVariant">
          <el-option v-for="o in variantOptions" :key="o.variantId" :label="`${o.productName} ${o.label || ''}（${o.sku}）可售 ${o.availableQty}`" :value="o.variantId" />
        </el-select>
        <el-empty v-if="lines.length === 0" description="還沒有加入品項" :image-size="56" />
        <div v-else class="order-create__lines">
          <div v-for="(l, i) in lines" :key="l.variantId" class="order-create__line">
            <div class="order-create__line-main">
              <strong>{{ l.label }}</strong>
              <span class="order-create__hint">{{ l.sku }}・目前可售 {{ l.availableQty }}</span>
              <span v-if="l.quantity > l.availableQty" class="order-create__warn">數量超過可售量，送出時會被擋下</span>
            </div>
            <el-input-number v-model="l.quantity" :min="1" :step="1" size="small" @change="dirty = true" />
            <el-button size="small" text type="danger" aria-label="移除品項" @click="removeLine(i)"><el-icon><Delete /></el-icon></el-button>
          </div>
        </div>
        <p v-if="overStock.length > 0" class="order-create__warn">有 {{ overStock.length }} 個品項的數量超過可售量。</p>
      </el-card>

      <el-card shadow="never" header="配送與收件人" class="order-create__block">
        <el-form label-position="top" @change="dirty = true">
          <el-form-item label="配送方式">
            <el-radio-group v-model="form.deliveryMethod" @change="dirty = true">
              <el-radio-button v-for="o in DELIVERY_METHOD_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</el-radio-button>
            </el-radio-group>
          </el-form-item>
          <el-row :gutter="12">
            <el-col :xs="24" :sm="12"><el-form-item label="收件人姓名" :required="needsPhone"><el-input v-model="form.recipientName" maxlength="64" /></el-form-item></el-col>
            <el-col :xs="24" :sm="12"><el-form-item label="收件人電話" :required="needsPhone"><el-input v-model="form.recipientPhone" maxlength="32" /></el-form-item></el-col>
          </el-row>
          <el-form-item v-if="form.deliveryMethod === 'home_delivery'" label="收件地址" required><el-input v-model="form.recipientAddress" maxlength="200" /></el-form-item>
          <el-form-item v-if="form.deliveryMethod === 'cvs_pickup'"><span class="order-create__hint">超商取貨的門市代碼在出貨時填寫。</span></el-form-item>
          <el-form-item v-if="form.deliveryMethod === 'onsite_pickup'">
            <el-checkbox v-model="form.completeImmediately" @change="dirty = true">現場當場取貨，訂單直接標為已完成</el-checkbox>
          </el-form-item>
          <el-form-item v-if="canSearchMembers" label="關聯會員（選填）">
            <el-select v-model="form.memberId" filterable remote clearable :remote-method="searchMembers" :loading="memberSearching" placeholder="輸入會員編號搜尋" style="width: 100%" @change="dirty = true">
              <el-option v-for="m in memberOptions" :key="m.id" :label="`${m.memberNo} ${m.name || ''}`" :value="m.id" />
            </el-select>
          </el-form-item>
        </el-form>
      </el-card>

      <el-card shadow="never" header="運費與備註" class="order-create__block">
        <el-form label-position="top" @change="dirty = true">
          <el-form-item label="運費">
            <el-checkbox v-model="form.overrideFee" @change="dirty = true">手動指定運費（不勾選則依商店設定：現場自取免運、達免運門檻免運）</el-checkbox>
            <el-input-number v-if="form.overrideFee" v-model="form.shippingFee" :min="0" :controls="false" style="margin-top: 8px" @change="dirty = true" />
          </el-form-item>
          <el-form-item label="顧客備註（選填）"><el-input v-model="form.customerNote" type="textarea" :rows="2" maxlength="200" show-word-limit /></el-form-item>
          <el-form-item label="內部備註（選填，只有後台看得到）"><el-input v-model="form.internalNote" type="textarea" :rows="2" maxlength="500" show-word-limit /></el-form-item>
        </el-form>
      </el-card>
      <EditActionBar><el-button type="primary" :loading="saving" @click="handleSave">建立訂單（現場收款）</el-button></EditActionBar>
    </template>
  </div>
</template>

<style scoped>
.order-create { max-width: 820px; margin: 0 auto 88px; }
.order-create__block { margin-bottom: 16px; }
.order-create__hint { font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.order-create__warn { font-size: 12px; color: var(--admin-warning-text); }
.order-create__lines { display: flex; flex-direction: column; gap: 8px; margin-top: 12px; }
.order-create__line { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border: 1px solid var(--admin-border); border-radius: 4px; flex-wrap: wrap; }
.order-create__line-main { display: flex; flex-direction: column; flex: 1; min-width: 160px; }
</style>
