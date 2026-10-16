<script setup lang="ts">
// 權益對照表（K4）：`GET /{club}/membership/benefits`，一份資料多處使用（加入頁、8.2 球迷會頁、會員中心方案頁）。
// v3.29（客戶 8.2）：**以方案為欄**——每個目前可購買的方案一欄（磐石＝磐石會員、尊尚會員；藍鯨由藍鯨自訂方案），
// 欄位名稱、年費與每格的值全來自 API，前台不硬編任何權益文字或方案名稱。沒有可購買方案時顯示空狀態。
const { lp, isEn, tx } = useLocale()
const { benefits, failed } = await useMembershipBenefits()
const plans = computed(() => benefits.value?.plans ?? [])
const groups = computed(() => (benefits.value?.groups ?? []).filter(g => g.items.length > 0))
const hasData = computed(() => plans.value.length > 0 && groups.value.length > 0)
const feeText = (n: number) => `NT$ ${n.toLocaleString(isEn.value ? 'en-US' : 'zh-TW')}`

/** 第 i 個方案在某條權益上的值；API 保證同序，缺格時視為「沒有這一條」。 */
function valueAt(item: { values: Array<{ planCode: string, value: string | null }> }, planCode: string, i: number): string {
  const v = item.values[i]?.planCode === planCode ? item.values[i] : item.values.find(x => x.planCode === planCode)
  return v?.value?.trim() ?? ''
}
</script>

<template>
<!-- 會籍權益對照表 —— 規劃書 3.14 要求「一份資料、多處使用」（加入會員頁／8.2 球迷會頁／會員中心方案頁），
     且必須是可被搜尋引擎索引、可被螢幕閱讀器朗讀的 HTML 文字，**不得做成圖片**。修改請改這一支，勿在各頁另抄一份。 -->
<div class="benefits">
  <p class="kicker">MEMBER BENEFITS</p>
  <h2 class="section-title" id="benefits-title">{{ tx('會員權益對照', 'Membership benefits compared') }}</h2>
  <p class="section-lede">{{ tx('各方案的權益逐條對照。權益以會籍所屬俱樂部為準，未登入亦可查看。', 'The benefits of each plan, side by side. Benefits apply to the club that issues the membership, and can be viewed without signing in.') }}</p>

  <div v-if="hasData" class="table-scroll">
    <table class="benefits-table">
      <caption class="visually-hidden">{{ tx('各會籍方案的權益對照表', 'Benefits comparison of the membership plans') }}</caption>
      <thead>
        <tr>
          <th scope="col">{{ tx('權益項目', 'Benefit') }}</th>
          <th v-for="p in plans" :key="p.code" scope="col">
            {{ p.name ?? p.tierLabel }}
            <span class="benefits-table__price">{{ feeText(p.fee) }}{{ tx('／年', ' / year') }}</span>
          </th>
        </tr>
      </thead>
      <tbody v-for="g in groups" :key="g.group">
        <tr class="benefits-table__group"><th :colspan="plans.length + 1" scope="colgroup">{{ g.groupLabel }}</th></tr>
        <tr v-for="item in g.items" :key="`${g.group}-${item.code}`">
          <th scope="row">
            {{ item.name }}
            <span v-if="item.description" class="benefits-table__desc">{{ item.description }}</span>
          </th>
          <td v-for="(p, i) in plans" :key="p.code">
            <template v-if="valueAt(item, p.code, i)">{{ valueAt(item, p.code, i) }}</template>
            <template v-else><span class="cross" aria-hidden="true">—</span><span class="visually-hidden">{{ tx('無', 'No') }}</span></template>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <div v-else class="pending-note">
    <template v-if="failed">{{ tx('權益對照表暫時無法載入，請稍後再試。', 'The benefits comparison cannot be loaded right now. Please try again later.') }}</template>
    <template v-else>{{ tx('目前沒有開放購買的會籍方案，方案與權益公布後會在這裡逐條對照。', 'There are no membership plans open for purchase at the moment. Once the plans and benefits are announced, they will be compared here.') }}</template>
  </div>

  <p class="benefits__note">
    <template v-if="isEn">Just <strong>show your digital membership card</strong> at the store to get the discount, with no extra steps.</template>
    <template v-else>到店<strong>出示電子會員卡</strong>即可享折扣，不需額外手續。</template>
    <a :href="lp('/zh/perks/')">{{ tx('查看特約店家清單 →', 'See the list of partner stores →') }}</a>
  </p>
</div>
</template>

<style>
/* benefits：會籍權益對照表（隨元件一起帶走的樣式，勿在頁面另抄）
   ⛔ 不加 scoped——tcrfc.css 與頁面樣式一字不改是全站紀律（docs/13-blue-whale-site.md §6 紀律 10），
   這裡的 class 命名本來就靠既有 .benefits 前綴隔離，不需要 scoped。 */
.benefits .section-lede{ margin-top:.5rem; }
.benefits .table-scroll{ overflow-x:auto; margin-top:1.75rem; }
.benefits-table{ width:100%; min-width:560px; border-collapse:collapse; background:var(--paper); }
.benefits-table th, .benefits-table td{
  padding:.9rem 1.1rem; border:1px solid var(--rule); font-size:.88rem; text-align:left; vertical-align:top;
}
.benefits-table thead th{ background:var(--paper-2); font-weight:800; color:var(--heading); line-height:1.5; }
.benefits-table thead th .en{ display:block; font-size:.72rem; color:var(--muted); font-weight:700; }
.benefits-table__price{ display:block; margin-top:.35rem; font-size:.75rem; font-weight:700; color:var(--brand-aa); }
.benefits-table tbody th{ font-weight:700; color:var(--heading); }
.benefits-table td{ color:var(--text); }
.benefits-table .tick{ color:var(--brand-aa); font-weight:800; }
.benefits-table .cross{ color:var(--muted); }
.benefits-table__group th{ background:var(--paper-2); font-size:.78rem; letter-spacing:.06em; color:var(--muted); font-weight:800; }
.benefits-table__desc{ display:block; margin-top:.25rem; font-size:.78rem; font-weight:400; color:var(--muted); line-height:1.6; }
.benefits__note{ margin-top:1.25rem; font-size:.88rem; color:var(--text); }
.benefits__note a{ color:var(--brand-aa); font-weight:700; text-decoration:underline; }
.pending-cell{ color:var(--muted); font-style:italic; }
.pending-inline{ font-size:.78em; font-weight:700; opacity:.85; }
.pending-note{
  margin-top:1.25rem; padding:.9rem 1.1rem; border-left:3px solid var(--brand);
  background:var(--paper-2); font-size:.82rem; line-height:1.7; color:var(--muted);
}
</style>
