<script setup lang="ts">
/** 「付款紀錄」分頁：人工登錄的付款紀錄，供對帳（系統不經手金流）。 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import MobileCardList from '@/components/MobileCardList.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import { activeClubId } from '@/auth/clubAccess'
import { listMembershipPayments, type MembershipPaymentRowDto } from '@/api/adminMemberships'
import { clubNameOf, errorMessage, formatMoney } from './membershipHelpers'

const route = useRoute()
const router = useRouter()
const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'mobile')
const club = computed(() => activeClubId.value)

const range = ref<[string, string] | null>(null)
const membershipId = ref(typeof route.query.membershipId === 'string' ? route.query.membershipId : '')
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
type PaymentRow = MembershipPaymentRowDto & { rowId: string }
const rows = ref<PaymentRow[]>([])
const loading = ref(true)
const loadError = ref<string | null>(null)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const res = await listMembershipPayments(club.value, {
      membershipId: membershipId.value || undefined,
      from: range.value?.[0],
      to: range.value?.[1],
      page: page.value,
      pageSize: pageSize.value,
    })
    rows.value = res.items.map((r) => ({ ...r, rowId: r.payment.id }))
    total.value = res.totalCount
  } catch (error) {
    rows.value = []
    total.value = 0
    loadError.value = errorMessage(error, '付款紀錄載入失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}
function search() {
  page.value = 1
  load()
}
function clearMembership() {
  membershipId.value = ''
  router.replace({ query: { tab: 'payments' } })
  search()
}
function clearAll() {
  range.value = null
  clearMembership()
}
onMounted(load)
watch(club, () => {
  range.value = null
  membershipId.value = ''
  search()
})
const total$ = computed(() => rows.value.reduce((sum, r) => sum + r.payment.amount, 0))
const detail = (row: PaymentRow) => router.push(`/members/plans/memberships/${row.membershipId}`)
</script>

<template>
  <div class="pay-tab">
    <el-card shadow="never" class="pay-tab__bar">
      <div class="pay-tab__row">
        <el-date-picker v-model="range" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="付款日起" end-placeholder="付款日迄" class="pay-tab__range" @change="search" />
        <el-tag v-if="membershipId" closable @close="clearMembership">只看單一會籍的付款紀錄</el-tag>
        <el-button @click="clearAll">清除</el-button>
      </div>
      <p class="pay-tab__hint">這裡是客服人工登錄的付款紀錄（系統不經手金流），用於對帳。收款主體是實際收款的俱樂部，受益俱樂部是會籍歸屬的俱樂部。</p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="6" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else shadow="never">
      <template v-if="rows.length > 0">
        <el-table v-if="!isMobile" :data="rows" row-key="rowId">
          <el-table-column label="付款日" width="110"><template #default="{ row }">{{ row.payment.paidOn }}</template></el-table-column>
          <el-table-column label="會員編號" width="120"><template #default="{ row }">{{ row.memberNo || '—' }}</template></el-table-column>
          <el-table-column label="球季" width="90"><template #default="{ row }">{{ row.seasonCode || '—' }}</template></el-table-column>
          <el-table-column label="方案" min-width="130"><template #default="{ row }">{{ row.payment.planName || '—' }}</template></el-table-column>
          <el-table-column label="方式" width="100"><template #default="{ row }">{{ row.payment.methodLabel || row.payment.method }}</template></el-table-column>
          <el-table-column label="金額" width="100"><template #default="{ row }">{{ formatMoney(row.payment.amount) }}</template></el-table-column>
          <el-table-column label="收款主體" width="120"><template #default="{ row }">{{ clubNameOf(row.payment.collectingClubCode) }}</template></el-table-column>
          <el-table-column label="受益俱樂部" width="120"><template #default="{ row }">{{ clubNameOf(row.payment.beneficiaryClubCode) }}</template></el-table-column>
          <el-table-column label="經手人" width="110"><template #default="{ row }">{{ row.payment.handledByName || '—' }}</template></el-table-column>
          <el-table-column label="備註" min-width="140"><template #default="{ row }">{{ row.payment.note || '—' }}</template></el-table-column>
          <el-table-column label="操作" width="90" fixed="right"><template #default="{ row }"><el-button size="small" text type="primary" @click="detail(row)">會籍</el-button></template></el-table-column>
        </el-table>
        <MobileCardList v-else :rows="rows" row-key="rowId">
          <template #title="{ row }">{{ row.payment.paidOn }}｜{{ formatMoney(row.payment.amount) }}</template>
          <template #meta="{ row }">
            <span>{{ row.memberNo || '—' }}</span>
            <span>{{ row.payment.planName || '' }}</span>
            <span>{{ row.payment.methodLabel || row.payment.method }}</span>
            <span>收款：{{ clubNameOf(row.payment.collectingClubCode) }}</span>
            <span>受益：{{ clubNameOf(row.payment.beneficiaryClubCode) }}</span>
          </template>
          <template #actions="{ row }"><el-button size="small" text type="primary" @click="detail(row)">會籍</el-button></template>
        </MobileCardList>
        <p class="pay-tab__sum">本頁合計 {{ formatMoney(total$) }}</p>
        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" class="pay-tab__pager" background :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next'" :page-sizes="[20, 50, 100]" :total="total" @current-change="load" @size-change="search" />
      </template>
      <el-empty v-else description="這個條件下沒有付款紀錄" />
    </el-card>
  </div>
</template>

<style scoped>
.pay-tab { min-width: 0; }
.pay-tab__bar { margin-bottom: 12px; }
.pay-tab__row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.pay-tab__range { max-width: 100%; }
.pay-tab__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.pay-tab__sum { margin: 10px 0 0; font-size: 13px; color: var(--admin-text-secondary); text-align: right; }
.pay-tab__pager { margin-top: 12px; justify-content: flex-end; flex-wrap: wrap; }
</style>
