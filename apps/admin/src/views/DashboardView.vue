<script setup lang="ts">
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import {
  TODO_REMINDERS,
  UPCOMING_EVENTS,
  QUICK_ENTRIES,
  CONTENT_OVERVIEW,
  MEMBERSHIP_OVERVIEW,
} from '@/data/dashboard'

const router = useRouter()
</script>

<template>
  <div class="dashboard">
    <PageHeader title="儀表板">
      <template #meta>
        <FrontendUnitBanner module-code="A" />
      </template>
    </PageHeader>

    <el-row :gutter="16">
      <el-col :xs="24" :md="12" :lg="6">
        <el-card shadow="never" class="dashboard__stat-card">
          <div class="dashboard__stat-label">本月新註冊會員</div>
          <div class="dashboard__stat-value">{{ MEMBERSHIP_OVERVIEW.newMembersThisMonth }}</div>
        </el-card>
      </el-col>
      <el-col :xs="24" :md="12" :lg="6">
        <el-card shadow="never" class="dashboard__stat-card">
          <div class="dashboard__stat-label">新加入付費會籍</div>
          <div class="dashboard__stat-value">{{ MEMBERSHIP_OVERVIEW.newPaidMemberships }}</div>
        </el-card>
      </el-col>
      <el-col :xs="24" :md="12" :lg="6">
        <el-card shadow="never" class="dashboard__stat-card">
          <div class="dashboard__stat-label">即將到期會籍</div>
          <div class="dashboard__stat-value dashboard__stat-value--warning">
            {{ MEMBERSHIP_OVERVIEW.expiringSoon }}
          </div>
        </el-card>
      </el-col>
      <el-col :xs="24" :md="12" :lg="6">
        <el-card shadow="never" class="dashboard__stat-card">
          <div class="dashboard__stat-label">未翻譯內容數</div>
          <div class="dashboard__stat-value">{{ CONTENT_OVERVIEW.untranslatedCount }}</div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="16" class="dashboard__row">
      <el-col :xs="24" :lg="8">
        <el-card shadow="never" header="待辦提醒">
          <ul class="dashboard__list">
            <li v-for="item in TODO_REMINDERS" :key="item.id">
              <a class="dashboard__list-link" @click="router.push(item.to)">
                {{ item.label }}
                <el-tag size="small" type="warning">{{ item.count }}</el-tag>
              </a>
            </li>
          </ul>
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="8" class="dashboard__col-spacing">
        <el-card shadow="never" header="未來 14 天行程">
          <ul class="dashboard__list">
            <li v-for="event in UPCOMING_EVENTS" :key="event.id">
              <span class="dashboard__event-date">{{ event.date }}</span>
              <span class="dashboard__event-title">{{ event.title }}</span>
              <el-tag size="small">{{ event.type }}</el-tag>
            </li>
          </ul>
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="8" class="dashboard__col-spacing">
        <el-card shadow="never" header="快速入口">
          <div class="dashboard__quick-entries">
            <el-button
              v-for="entry in QUICK_ENTRIES"
              :key="entry.id"
              @click="router.push(entry.to)"
            >
              {{ entry.label }}
            </el-button>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<style scoped>
.dashboard__stat-card {
  margin-bottom: 16px;
}

.dashboard__stat-label {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.dashboard__stat-value {
  font-size: 28px;
  font-weight: 600;
  margin-top: 4px;
}

.dashboard__stat-value--warning {
  color: var(--el-color-warning);
}

.dashboard__row {
  margin-top: 4px;
}

.dashboard__col-spacing {
  margin-top: 16px;
}

@media (min-width: 1200px) {
  .dashboard__col-spacing {
    margin-top: 0;
  }
}

.dashboard__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.dashboard__list-link {
  display: flex;
  align-items: center;
  justify-content: space-between;
  cursor: pointer;
  color: var(--el-text-color-primary);
}

.dashboard__list-link:hover {
  color: var(--el-color-primary);
}

.dashboard__event-date {
  color: var(--el-text-color-secondary);
  margin-right: 8px;
  font-size: 12px;
}

.dashboard__event-title {
  flex: 1;
  margin-right: 8px;
}

.dashboard__list li {
  display: flex;
  align-items: center;
}

.dashboard__quick-entries {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
