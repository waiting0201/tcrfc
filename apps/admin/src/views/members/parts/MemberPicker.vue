<script setup lang="ts">
/**
 * 挑選會員：遠端搜尋（會員名單端點），結果姓名為遮罩值。
 * 沒有「解除遮罩」權限的人，關鍵字只會比對會員編號，所以提示以會員編號為主。
 */
import { ref, watch } from 'vue'
import { activeClubId } from '@/auth/clubAccess'
import { searchMembers, type MemberSearchItemDto } from '@/api/adminMemberships'
import { errorMessage } from './membershipHelpers'

const model = defineModel<string>({ default: '' })
const emit = defineEmits<{ (e: 'picked', member: MemberSearchItemDto | null): void }>()

/** 預先帶入的會員（例如從待確認申請開通），讓下拉框一開始就顯示會員而不是一串識別碼。 */
const props = defineProps<{ seed?: MemberSearchItemDto | null }>()
const options = ref<MemberSearchItemDto[]>([])
watch(
  () => props.seed,
  (seed) => {
    if (seed && !options.value.some((o) => o.id === seed.id)) options.value = [seed, ...options.value]
  },
  { immediate: true },
)
const loading = ref(false)
const searchError = ref<string | null>(null)

async function remote(keyword: string) {
  const k = keyword.trim()
  if (!k) {
    options.value = []
    return
  }
  loading.value = true
  searchError.value = null
  try {
    options.value = await searchMembers(activeClubId.value, k)
  } catch (error) {
    options.value = []
    searchError.value = errorMessage(error, '搜尋失敗，請稍後再試')
  } finally {
    loading.value = false
  }
}

function handleChange(id: string) {
  emit('picked', options.value.find((o) => o.id === id) ?? null)
}
</script>

<template>
  <div class="member-picker">
    <el-select
      v-model="model"
      filterable
      remote
      reserve-keyword
      clearable
      :remote-method="remote"
      :loading="loading"
      placeholder="輸入會員編號搜尋"
      no-data-text="沒有符合的會員"
      style="width: 100%"
      @change="handleChange"
    >
      <el-option v-for="m in options" :key="m.id" :value="m.id" :label="`${m.memberNo}｜${m.name}`" />
    </el-select>
    <p class="member-picker__hint">
      以會員編號搜尋；有權限查看完整資料的人也可以輸入姓名、Email 或電話。搜尋結果的姓名為遮罩顯示。
    </p>
    <p v-if="searchError" class="member-picker__error">{{ searchError }}</p>
  </div>
</template>

<style scoped>
.member-picker { width: 100%; min-width: 0; }
.member-picker__hint { margin: 4px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.5; }
.member-picker__error { margin: 4px 0 0; font-size: 12px; color: var(--el-color-danger); }
</style>
