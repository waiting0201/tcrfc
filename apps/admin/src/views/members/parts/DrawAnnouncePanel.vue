<script setup lang="ts">
/**
 * 公布：把中獎名單（姓名遮罩）交給新聞編輯，最後標為已公布。
 * - 「產生公告草稿」會在「新聞與故事」建立一篇草稿（分類：俱樂部新聞，標籤：球迷會員抽獎），
 *   內文只有活動名稱、獎品、基準時間、合格人數與遮罩後的中獎名單；接著到新聞編輯調整、發布。
 * - 也可以連結一篇已經寫好的既有文章。標為「已公布」前，連結的文章必須已發布。
 * - 公關／媒體只能取得遮罩名單，不因此取得會員模組的權限。
 */
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useViewUpdatePermissions, usePermission } from '@/composables/useCrudPermissions'
import { activeClubId } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import { lookupNews, type NewsLookupItemDto } from '@/api/adminNews'
import { createAnnouncementDraft, getAnnouncementPreview, linkAnnouncementArticle, markAnnounced, type AnnouncementPreviewDto, type DrawDetailDto } from '@/api/adminDraws'
import { formatDateTime } from '@/utils/dateTime'

const props = defineProps<{ draw: DrawDetailDto }>()
const emit = defineEmits<{ (e: 'changed'): void }>()

const router = useRouter()
const { canUpdate } = useViewUpdatePermissions('member.draw')
const canAnnounce = usePermission('member.draw.announce')
const club = computed(() => activeClubId.value)
const errorText = (e: unknown, f: string) => (e instanceof AdminApiError ? e.message : f)
const can = (a: string) => props.draw.availableActions.includes(a as never)

const preview = ref<AnnouncementPreviewDto | null>(null)
const previewing = ref(false)
async function loadPreview() {
  previewing.value = true
  try {
    preview.value = await getAnnouncementPreview(club.value, props.draw.id)
  } catch (error) {
    preview.value = null
    ElMessage.error(errorText(error, '公布稿預覽載入失敗，請稍後再試'))
  } finally {
    previewing.value = false
  }
}

const creating = ref(false)
async function createDraft() {
  try {
    await ElMessageBox.confirm('系統會在「新聞與故事」建立一篇草稿，內文是遮罩後的中獎名單。確定要產生嗎？', '產生公告草稿', { confirmButtonText: '產生', cancelButtonText: '取消', type: 'info' })
  } catch {
    return
  }
  creating.value = true
  try {
    const result = await createAnnouncementDraft(club.value, props.draw.id)
    ElMessage.success('已建立草稿，請到新聞編輯調整後發布')
    emit('changed')
    if (result?.articleId) router.push(`/content/news/${result.articleId}/edit`)
  } catch (error) {
    ElMessage.error(errorText(error, '產生失敗，請稍後再試'))
  } finally {
    creating.value = false
  }
}

// ── 連結既有文章 ──
const linkOpen = ref(false)
const linkKeyword = ref('')
const linkOptions = ref<NewsLookupItemDto[]>([])
const linkPicked = ref('')
const linkSearching = ref(false)
const linkSaving = ref(false)
async function searchArticles(keyword: string) {
  linkSearching.value = true
  try {
    linkOptions.value = (await lookupNews(club.value, { keyword: keyword.trim() || undefined, pageSize: 20 })).items
  } catch {
    linkOptions.value = []
  } finally {
    linkSearching.value = false
  }
}
function openLink() {
  linkPicked.value = ''
  linkKeyword.value = ''
  linkOpen.value = true
  searchArticles('')
}
async function saveLink() {
  if (!linkPicked.value) return ElMessage.warning('請先選擇一篇文章')
  linkSaving.value = true
  try {
    await linkAnnouncementArticle(club.value, props.draw.id, linkPicked.value)
    linkOpen.value = false
    ElMessage.success('已連結')
    emit('changed')
  } catch (error) {
    ElMessage.error(errorText(error, '連結失敗，請稍後再試'))
  } finally {
    linkSaving.value = false
  }
}

const marking = ref(false)
async function doMark() {
  try {
    await ElMessageBox.confirm('確定要把這個活動標為「已公布」嗎？連結的文章必須已經發布。公布後再修改中獎名單，必須填寫修改原因。', '標為已公布', { confirmButtonText: '確定', cancelButtonText: '取消', type: 'warning' })
  } catch {
    return
  }
  marking.value = true
  try {
    await markAnnounced(club.value, props.draw.id)
    ElMessage.success('已標為已公布')
    emit('changed')
  } catch (error) {
    ElMessage.error(errorText(error, '操作失敗，請稍後再試'))
  } finally {
    marking.value = false
  }
}
</script>

<template>
  <div class="announce">
    <el-card shadow="never" header="公布文章" class="announce__block">
      <dl class="announce__dl">
        <div>
          <dt>連結的文章</dt>
          <dd>
            <template v-if="draw.announcementArticleId">
              <el-button text type="primary" class="announce__link" @click="router.push(`/content/news/${draw.announcementArticleId}/edit`)">到新聞編輯開啟</el-button>
              <el-tag size="small" :type="draw.announcementStatus === 'published' ? 'success' : 'info'">{{ draw.announcementStatusLabel || '—' }}</el-tag>
            </template>
            <span v-else class="announce__muted">尚未連結</span>
          </dd>
        </div>
        <div><dt>活動狀態</dt><dd>{{ draw.statusLabel }}</dd></div>
      </dl>
      <div class="announce__row">
        <el-button v-if="canAnnounce && can('announce')" type="primary" :loading="creating" @click="createDraft">產生公告草稿</el-button>
        <el-button v-if="canAnnounce && can('announce')" @click="openLink">連結既有文章</el-button>
        <el-button v-if="canUpdate && can('mark_announced')" type="success" :loading="marking" @click="doMark">標為已公布</el-button>
      </div>
      <p class="announce__hint">流程：產生公告草稿 → 在新聞編輯調整並發布 → 回到這裡標為「已公布」。系統不會寄送中獎通知；如需聯繫中獎人請由客服電話處理。</p>
      <p v-if="!can('announce') && canAnnounce" class="announce__hint">目前活動狀態不能產生公布稿（需要先回填中獎人）。</p>
    </el-card>

    <el-card v-if="canAnnounce" shadow="never" header="公布稿預覽（姓名已遮罩）" class="announce__block">
      <el-button :loading="previewing" @click="loadPreview">{{ preview ? '重新整理預覽' : '載入預覽' }}</el-button>
      <template v-if="preview">
        <dl class="announce__dl announce__preview">
          <div><dt>活動</dt><dd>{{ preview.drawName }}</dd></div>
          <div v-if="preview.prizeDescription"><dt>獎品</dt><dd>{{ preview.prizeDescription }}</dd></div>
          <div><dt>資格基準時間</dt><dd>{{ formatDateTime(preview.snapshotAt) }}</dd></div>
          <div><dt>合格人數</dt><dd>{{ preview.eligibleCount }} 人</dd></div>
        </dl>
        <el-empty v-if="preview.winners.length === 0" description="還沒有中獎人" :image-size="48" />
        <el-table v-else :data="preview.winners" row-key="serialNo" size="small">
          <el-table-column label="序號" width="80" prop="serialNo" />
          <el-table-column label="會員編號" width="130" prop="memberNo" />
          <el-table-column label="姓名（遮罩）" min-width="100" prop="maskedName" />
          <el-table-column label="獎項" min-width="140" prop="prizeName" />
        </el-table>
      </template>
    </el-card>

    <el-dialog v-model="linkOpen" title="連結既有文章" width="520px" :close-on-click-modal="false" class="announce__dialog">
      <p class="announce__hint">挑選作為公布稿的文章（本俱樂部或兩隊共用）。標為已公布前，這篇文章必須已發布。</p>
      <el-select v-model="linkPicked" filterable remote :remote-method="searchArticles" :loading="linkSearching" placeholder="輸入標題關鍵字搜尋" style="width: 100%">
        <el-option v-for="a in linkOptions" :key="a.id" :label="`${a.titleZh || a.titleEn || a.slug}（${a.statusLabel}）`" :value="a.id" />
      </el-select>
      <template #footer>
        <el-button @click="linkOpen = false">取消</el-button>
        <el-button type="primary" :loading="linkSaving" @click="saveLink">確定連結</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.announce__block { margin-bottom: 16px; }
.announce__row { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
.announce__hint { margin: 8px 0 0; font-size: 12px; color: var(--admin-text-tertiary); line-height: 1.6; }
.announce__muted { color: var(--admin-text-tertiary); }
.announce__dl { margin: 0; display: flex; flex-direction: column; gap: 6px; font-size: 14px; }
.announce__dl > div { display: flex; gap: 8px; align-items: baseline; }
.announce__dl dt { width: 96px; flex-shrink: 0; color: var(--admin-text-secondary); }
.announce__dl dd { margin: 0; min-width: 0; word-break: break-word; }
.announce__preview { margin: 12px 0; }
.announce__link { padding: 0; margin-right: 8px; }
@media (max-width: 767px) { :deep(.el-dialog) { width: 94% !important; } }
</style>
