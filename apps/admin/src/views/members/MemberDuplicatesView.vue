<script setup lang="ts">
/**
 * 重複帳號比對：依電話或 Email 找出疑似同一人的帳號（顯示遮罩後的資料）。
 * 合併不可逆，只有系統管理員能做；沒有合併權限的人只看比對結果。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { formatDateTime } from '@/utils/formatDateTime'
import { listMemberDuplicates, mergeMembers, type DuplicateGroupDto, type DuplicateMemberDto } from '@/api/adminMembers'

const router = useRouter()
const canMerge = usePermission('member.account.merge')
const club = computed(() => activeClubId.value)

const groups = ref<DuplicateGroupDto[]>([])
const crossClub = ref(false)
const loading = ref(true)
const loadError = ref<string | null>(null)
/** 每一組選定要保留的帳號（key＝組別索引）。 */
const keepIds = ref<Record<number, string>>({})

async function load() {
  loading.value = true
  loadError.value = null
  try {
    groups.value = await listMemberDuplicates(club.value, crossClub.value)
    keepIds.value = {}
    groups.value.forEach((g, i) => {
      // 預設保留最早建立的帳號
      const oldest = [...g.members].sort((a, b) => a.createdAt.localeCompare(b.createdAt))[0]
      if (oldest) keepIds.value[i] = oldest.id
    })
  } catch (error) {
    groups.value = []
    loadError.value = error instanceof AdminApiError ? error.message : '比對結果載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(club, load)

// ── 合併 ──
const dialogOpen = ref(false)
const target = ref<DuplicateMemberDto | null>(null)
const source = ref<DuplicateMemberDto | null>(null)
const confirmText = ref('')
const merging = ref(false)
const mergeError = ref<string | null>(null)
const confirmMatches = computed(() => !!target.value && confirmText.value.trim().toUpperCase() === target.value.memberNo.toUpperCase())

function openMerge(group: DuplicateGroupDto, index: number, sourceMember: DuplicateMemberDto) {
  const keep = group.members.find((m) => m.id === keepIds.value[index])
  if (!keep || keep.id === sourceMember.id) return
  target.value = keep
  source.value = sourceMember
  confirmText.value = ''
  mergeError.value = null
  dialogOpen.value = true
}

async function handleMerge() {
  if (!target.value || !source.value || !confirmMatches.value) return
  merging.value = true
  mergeError.value = null
  try {
    const r = await mergeMembers(club.value, target.value.id, source.value.id)
    dialogOpen.value = false
    ElMessage.success(
      `已合併：${r.sourceMemberNo} 併入 ${r.targetMemberNo}。轉移會籍 ${r.movedMemberships} 筆、報名 ${r.movedRegistrations} 筆、訂單 ${r.movedOrders} 筆、球衣 ${r.movedJerseys} 筆`,
    )
    await load()
  } catch (error) {
    mergeError.value = error instanceof AdminApiError ? error.message : '合併失敗，請稍後再試'
  } finally {
    merging.value = false
  }
}
</script>

<template>
  <div class="dup">
    <PageHeader title="重複帳號比對">
      <template #back>
        <el-button text @click="router.push('/members/list')"><el-icon><ArrowLeft /></el-icon>返回名單</el-button>
      </template>
      <template #meta><FrontendUnitBanner module-code="K1" /></template>
    </PageHeader>

    <el-card shadow="never" class="dup__block">
      <el-checkbox v-model="crossClub" @change="load">顯示我有權限的所有俱樂部的會員</el-checkbox>
      <p class="dup__hint">
        比對規則：同一支電話，或同一個 Email（大小寫、Gmail 的點號與加號標籤視為相同）。顯示的是遮罩後的資料，可用會員編號與註冊時間判斷。
        <template v-if="!canMerge">合併帳號只有系統管理員能操作，你的帳號僅能查看比對結果。</template>
      </p>
    </el-card>

    <el-card v-if="loading" shadow="never"><el-skeleton :rows="5" animated /></el-card>
    <el-card v-else-if="loadError" shadow="never">
      <el-empty :description="loadError"><el-button type="primary" @click="load">重新載入</el-button></el-empty>
    </el-card>
    <el-card v-else-if="groups.length === 0" shadow="never"><el-empty description="沒有發現疑似重複的帳號" /></el-card>
    <template v-else>
      <el-card v-for="(group, gi) in groups" :key="gi" shadow="never" class="dup__block">
        <template #header>
          <span>{{ group.matchKindLabel }}相同・{{ group.members.length }} 個帳號</span>
        </template>
        <el-radio-group v-model="keepIds[gi]" class="dup__group">
          <div v-for="m in group.members" :key="m.id" class="dup__member">
            <el-radio v-if="canMerge" :value="m.id" class="dup__radio">保留這個</el-radio>
            <div class="dup__info">
              <router-link :to="`/members/list/${m.id}`" class="dup__link">{{ m.memberNo }}</router-link>
              <span>{{ m.name || '—' }}</span>
              <span class="dup__muted">{{ m.email || '—' }}</span>
              <span class="dup__muted">{{ m.phone || '—' }}</span>
              <span class="dup__muted">註冊 {{ formatDateTime(m.createdAt) }}・會籍 {{ m.membershipCount }} 份</span>
            </div>
            <el-button v-if="canMerge && keepIds[gi] && keepIds[gi] !== m.id" size="small" type="danger" plain @click="openMerge(group, gi, m)">併入保留的帳號</el-button>
          </div>
        </el-radio-group>
      </el-card>
    </template>

    <el-dialog v-model="dialogOpen" title="合併重複帳號" width="520px" class="dup__dialog" :close-on-click-modal="false">
      <el-alert type="error" show-icon :closable="false" title="合併無法復原">
        被併入的帳號 {{ source?.memberNo }} 的會籍、付款、報名、訂單與球衣會全部轉給 {{ target?.memberNo }}，
        被併入的帳號會變成「已刪除」且無法登入。如果兩個帳號在同一俱樂部同一球季都有會籍，系統會拒絕合併，需先處理掉其中一份。
      </el-alert>
      <el-alert v-if="mergeError" type="warning" show-icon :title="mergeError" class="dup__gap" @close="mergeError = null" />
      <p class="dup__gap">請輸入要保留的會員編號 <strong>{{ target?.memberNo }}</strong> 以確認：</p>
      <el-input v-model="confirmText" placeholder="輸入保留帳號的會員編號" @keyup.enter="handleMerge" />
      <template #footer>
        <el-button :disabled="merging" @click="dialogOpen = false">取消</el-button>
        <el-button type="danger" :disabled="!confirmMatches" :loading="merging" @click="handleMerge">確認合併</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.dup { max-width: 980px; margin: 0 auto 40px; }
.dup__block { margin-bottom: 16px; }
.dup__hint { margin: 8px 0 0; font-size: 12px; line-height: 1.6; color: var(--admin-text-tertiary); }
.dup__group { display: flex; flex-direction: column; align-items: stretch; width: 100%; gap: 8px; }
.dup__member { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; padding: 8px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
.dup__member:last-child { border-bottom: 0; }
.dup__radio { margin-right: 0; }
.dup__info { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 12px; flex: 1; min-width: 0; word-break: break-all; }
.dup__link { font-weight: 500; color: var(--el-color-primary); text-decoration: none; }
.dup__muted { color: var(--admin-text-tertiary); font-size: 12px; }
.dup__gap { margin-top: 12px; }
</style>
