<script setup lang="ts">
/**
 * 簽到表（課程梯次與試訓場次共用）：畫面上顯示、按「列印」直接輸出。
 * 只顯示契約給的欄位（編號、姓名、電話、家長、狀態），不含健康聲明與備註（資料最小化）。
 * 列印時外殼（側欄、頂欄、頁首）與 `.no-print` 元素都會被隱藏，只留這張表——
 * 外殼元件用 scoped 樣式，所以這裡的列印規則放在非 scoped 的 `<style>`，並用 `!important` 蓋過它們。
 */
import { formatDateTime } from '@/utils/formatDateTime'
import type { SignInRowDto } from '@/api/adminTrials'

defineProps<{
  title: string
  /** 表頭下方的資訊列，例如「日期」「場地」。沒有值的項目請先濾掉。 */
  infos: { label: string; value: string }[]
  rows: SignInRowDto[]
  generatedAt: string
}>()
</script>

<template>
  <section class="sign-in-sheet">
    <h2 class="sign-in-sheet__title">{{ title }}</h2>
    <dl class="sign-in-sheet__infos">
      <div v-for="info in infos" :key="info.label" class="sign-in-sheet__info">
        <dt>{{ info.label }}</dt>
        <dd>{{ info.value }}</dd>
      </div>
      <div class="sign-in-sheet__info">
        <dt>應到人數</dt>
        <dd>{{ rows.length }} 人</dd>
      </div>
    </dl>

    <div v-if="rows.length > 0" class="sign-in-sheet__scroll">
      <table class="sign-in-sheet__table">
        <thead>
          <tr>
            <th class="sign-in-sheet__narrow">序號</th>
            <th>報名編號</th>
            <th>姓名</th>
            <th>電話</th>
            <th>家長姓名</th>
            <th>家長電話</th>
            <th class="sign-in-sheet__narrow">狀態</th>
            <th class="sign-in-sheet__sign">簽到</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.registrationNo">
            <td>{{ row.no }}</td>
            <td>{{ row.registrationNo }}</td>
            <td>{{ row.applicantName }}</td>
            <td>{{ row.phone || '—' }}</td>
            <td>{{ row.guardianName || '—' }}</td>
            <td>{{ row.guardianPhone || '—' }}</td>
            <td>{{ row.status }}</td>
            <td class="sign-in-sheet__sign" />
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="sign-in-sheet__empty">目前沒有需要到場的報名。</p>
    <p class="sign-in-sheet__generated">產生時間：{{ formatDateTime(generatedAt) }}（候補與取消的報名不列入）</p>
  </section>
</template>

<style scoped>
.sign-in-sheet {
  background: var(--admin-bg-surface);
  border: 1px solid var(--admin-border);
  border-radius: 6px;
  padding: 16px;
  min-width: 0;
}

.sign-in-sheet__title {
  margin: 0 0 10px;
  font-size: 18px;
}

.sign-in-sheet__infos {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 24px;
  margin: 0 0 14px;
  font-size: 14px;
}

.sign-in-sheet__info {
  display: flex;
  gap: 6px;
}

.sign-in-sheet__info dt {
  color: var(--admin-text-secondary);
}

.sign-in-sheet__info dd {
  margin: 0;
}

.sign-in-sheet__scroll {
  overflow-x: auto;
}

.sign-in-sheet__table {
  width: 100%;
  min-width: 640px;
  border-collapse: collapse;
  font-size: 14px;
}

.sign-in-sheet__table th,
.sign-in-sheet__table td {
  border: 1px solid var(--admin-border);
  padding: 8px 10px;
  text-align: left;
  vertical-align: middle;
}

.sign-in-sheet__narrow {
  width: 64px;
}

.sign-in-sheet__sign {
  width: 120px;
  min-width: 100px;
  height: 40px;
}

.sign-in-sheet__empty {
  color: var(--admin-text-secondary);
}

.sign-in-sheet__generated {
  margin: 10px 0 0;
  font-size: 12px;
  color: var(--admin-text-tertiary);
}
</style>

<style>
@media print {
  .admin-layout__aside,
  .admin-layout__header,
  .page-header,
  .no-print,
  .el-drawer,
  .el-overlay {
    display: none !important;
  }

  html,
  body {
    background: #fff !important;
    height: auto !important;
  }

  .admin-layout {
    display: block !important;
    height: auto !important;
    background: #fff !important;
  }

  .admin-layout__body,
  .admin-layout__main {
    display: block !important;
    height: auto !important;
    overflow: visible !important;
    padding: 0 !important;
    background: #fff !important;
  }

  .sign-in-sheet {
    border: none !important;
    padding: 0 !important;
    background: #fff !important;
    color: #000 !important;
  }

  .sign-in-sheet * {
    color: #000 !important;
  }

  .sign-in-sheet__scroll {
    overflow: visible !important;
  }

  .sign-in-sheet__table {
    min-width: 0 !important;
  }

  .sign-in-sheet__table th,
  .sign-in-sheet__table td {
    border-color: #000 !important;
  }

  .sign-in-sheet__sign {
    height: 44px;
  }
}
</style>
