<script setup lang="ts">
/**
 * H6 結構化資料完整性檢查（GEO-05，S1-12c，對應主站規劃書 §4.8 H／§7 `GEO-05`）。唯讀報表，
 * 接上 `GET /api/v1/admin/{club}/seo/schema-completeness`（apps/api/README.md「S1-12c」）。
 *
 * 後端逐型別掃描本俱樂部（含俱樂部＋共同）資料，**只回傳有缺漏的列**——資料完整的不會出現在
 * 這裡（比照既有 H3 孤立頁面偵測同一種「只列有問題的」報表設計）。必填欄位定義的唯一來源是
 * 後端 `Features/Seo/SchemaCompleteness.cs`，這裡不重新判斷一次「哪些欄位算必填」，只負責把
 * 後端已經算好的缺漏清單，換成日常中文陳列、依型別分組，並連到對應的編輯頁補資料。
 *
 * 🔴 型別名稱（`Organization`／`SportsTeam`……）是 schema.org 本身的英文字面值，介面上一律換成
 * 日常中文（`SCHEMA_TYPE_INFO`，對照 docs/06-conventions.md §1 新增的九筆型別對照），不直接
 * 顯示英文——比照 CLAUDE.md 全域規定第 9 條、既有「模組代號不顯示」的同一個理由。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import FrontendUnitBanner from '@/components/FrontendUnitBanner.vue'
import PageHeader from '@/components/PageHeader.vue'
import { activeClubId } from '@/auth/clubAccess'
import {
  getAdminSchemaCompleteness,
  type SchemaCompletenessEntityType,
  type SchemaCompletenessIssueDto,
} from '@/api/adminSeo'
import { AdminApiError } from '@/api/http'

const router = useRouter()
const club = computed(() => activeClubId.value)

const items = ref<SchemaCompletenessIssueDto[]>([])
const loading = ref(true)
const errorMessage = ref<string | null>(null)

async function loadReport() {
  loading.value = true
  errorMessage.value = null
  try {
    const result = await getAdminSchemaCompleteness(club.value)
    items.value = result.items
  } catch (error) {
    items.value = []
    errorMessage.value = error instanceof AdminApiError ? error.message : '報表載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}

onMounted(loadReport)
watch(club, loadReport)

/**
 * schema.org 型別 → 日常中文說明。`liveOnFrontend` 依 apps/api/README.md「S1-12c」逐字核對：
 * 目前只有 `Article`／`SportsEvent` 兩種真的已經接上前台輸出，其餘七種後台已經看得到資料缺漏
 * 現況，但網站還沒有把這些型別的摘要資料放進網頁裡（留給 `S1-12f` 等後續任務）。
 */
const SCHEMA_TYPE_INFO: Record<string, { label: string; hint: string; liveOnFrontend: boolean }> = {
  Organization: { label: '俱樂部基本資料', hint: '俱樂部名稱、官方網址、隊徽', liveOnFrontend: false },
  SportsTeam: { label: '球隊基本資料', hint: '球隊名稱、官方網址、隊徽', liveOnFrontend: false },
  Event: { label: '行事曆活動', hint: '活動名稱、開始時間、地點', liveOnFrontend: false },
  SportsEvent: { label: '賽程賽事', hint: '比賽日期、開球時間、主客場、對手、場地、賽事名稱', liveOnFrontend: true },
  Person: { label: '球員資料', hint: '球員姓名', liveOnFrontend: false },
  Article: { label: '新聞與故事文章', hint: '標題、發布時間、分享圖片', liveOnFrontend: true },
  Course: { label: '課程與活動項目', hint: '項目名稱、項目說明', liveOnFrontend: false },
  BreadcrumbList: { label: '頁面路徑導覽', hint: '頁面標題、頁面網址', liveOnFrontend: false },
  FAQPage: { label: '常見問題', hint: '問題、答案', liveOnFrontend: false },
}

/** 固定顯示順序，跟後端 `SchemaType` 列舉宣告順序一致，不用資料出現的順序（避免每次重新整理
 * 分組順序跳來跳去）。 */
const SCHEMA_TYPE_ORDER = [
  'Organization', 'SportsTeam', 'Event', 'SportsEvent', 'Person', 'Article', 'Course', 'BreadcrumbList', 'FAQPage',
]

function schemaTypeInfo(name: string) {
  return SCHEMA_TYPE_INFO[name] ?? { label: name, hint: '', liveOnFrontend: false }
}

/** 內部型別詞彙（`entityType`）→ 日常中文，只用來讓「資料名稱」欄多一個可辨識的分類，
 * 不單獨顯示英文詞。沿用既有 H3 `OrphanPageDto.entityType` 的翻譯慣例。 */
const ENTITY_TYPE_LABEL: Record<SchemaCompletenessEntityType, string> = {
  club: '俱樂部',
  team: '球隊',
  event: '行事曆活動',
  match: '賽事',
  player: '球員',
  article: '新聞與故事',
  program: '課程項目',
  page: '頁面',
  faq: '常見問題',
}

/** 依 `entityType` 連到對應的編輯頁（現有九種內部型別全部都有編輯頁，`null` 只是防呆，
 * 避免日後新增型別忘記補連結時整頁壞掉）。 */
function editRouteFor(row: SchemaCompletenessIssueDto): { name: string; params: Record<string, string> } | null {
  switch (row.entityType) {
    case 'club':
      return { name: 'system-club-edit', params: { id: row.id } }
    case 'team':
      return { name: 'team-edit', params: { id: row.id } }
    case 'event':
      return { name: 'calendar-event-edit', params: { id: row.id } }
    case 'match':
      return { name: 'match-edit', params: { id: row.id } }
    case 'player':
      return { name: 'player-edit', params: { id: row.id } }
    case 'article':
      return { name: 'news-edit', params: { id: row.id } }
    case 'program':
      return { name: 'program-item-edit', params: { id: row.id } }
    case 'page':
      return { name: 'page-edit', params: { id: row.id } }
    case 'faq':
      return { name: 'faq-edit', params: { id: row.id } }
    default:
      return null
  }
}

function handleEdit(row: SchemaCompletenessIssueDto) {
  const target = editRouteFor(row)
  if (!target) return
  router.push(target)
}

/** 依型別分組，只保留有缺漏的型別，順序固定（見 `SCHEMA_TYPE_ORDER`）。 */
const groups = computed(() => {
  const byType = new Map<string, SchemaCompletenessIssueDto[]>()
  for (const item of items.value) {
    const list = byType.get(item.schemaTypeName)
    if (list) {
      list.push(item)
    } else {
      byType.set(item.schemaTypeName, [item])
    }
  }
  return SCHEMA_TYPE_ORDER.filter((type) => byType.has(type)).map((type) => ({
    type,
    info: schemaTypeInfo(type),
    rows: byType.get(type) ?? [],
  }))
})

const totalCount = computed(() => items.value.length)
const liveTypeLabels = computed(() =>
  SCHEMA_TYPE_ORDER.filter((type) => SCHEMA_TYPE_INFO[type]?.liveOnFrontend).map((type) => SCHEMA_TYPE_INFO[type].label),
)
const notLiveTypeLabels = computed(() =>
  SCHEMA_TYPE_ORDER.filter((type) => !SCHEMA_TYPE_INFO[type]?.liveOnFrontend).map((type) => SCHEMA_TYPE_INFO[type].label),
)
</script>

<template>
  <div class="schema-report">
    <PageHeader title="結構化資料完整性檢查">
      <template #meta>
        <FrontendUnitBanner module-code="H" />
      </template>
    </PageHeader>

    <el-alert type="info" :closable="false" show-icon class="schema-report__notice">
      <p class="schema-report__notice-text">
        「搜尋引擎摘要資料」是網頁在原本的文字畫面之外，額外提供給搜尋引擎與 AI
        服務讀取的一份結構化資訊（球隊、賽事、文章……逐類型各有一組固定要填的欄位）。<strong>只要缺其中任何一項必填欄位，網站就完全不會輸出這一筆的摘要資料</strong>——不會輸出缺東缺西的半成品，寧可完全不出現，避免搜尋引擎讀到不完整或誤導的資訊。
      </p>
      <p class="schema-report__notice-text">
        下面依類型列出目前缺漏必填欄位的資料筆，以及各筆缺的是哪些欄位。點「編輯」可以直接跳到那一筆資料的編輯頁補齊。
      </p>
      <p class="schema-report__notice-text">
        <strong>目前網站前台真的會輸出的類型只有「{{ liveTypeLabels.join('」「') }}」</strong>；「{{ notLiveTypeLabels.join('」「') }}」這幾種類型，網站目前還沒有把摘要資料放進網頁——這裡照樣列出資料缺漏，是先幫忙把資料本身準備好，等日後這些類型也接上輸出時就不用重新盤點一次。
      </p>
    </el-alert>

    <el-skeleton v-if="loading" :rows="6" animated />
    <el-empty v-else-if="errorMessage" :description="errorMessage">
      <el-button type="primary" @click="loadReport">重新載入</el-button>
    </el-empty>
    <el-empty v-else-if="totalCount === 0" description="目前沒有偵測到必填欄位缺漏" />

    <template v-else>
      <el-card v-for="group in groups" :key="group.type" shadow="never" class="schema-report__group">
        <template #header>
          <div class="schema-report__group-header">
            <span class="schema-report__group-title">{{ group.info.label }}</span>
            <el-tag :type="group.info.liveOnFrontend ? 'warning' : 'info'" size="small" effect="plain">
              {{ group.info.liveOnFrontend ? '網站已對外輸出此類型' : '網站尚未輸出此類型' }}
            </el-tag>
            <span class="schema-report__group-count">缺漏 {{ group.rows.length }} 筆</span>
          </div>
          <p v-if="group.info.hint" class="schema-report__group-hint">必填欄位：{{ group.info.hint }}</p>
        </template>

        <el-table :data="group.rows" row-key="id">
          <el-table-column label="所屬資料" width="120">
            <template #default="{ row }">{{ ENTITY_TYPE_LABEL[row.entityType as SchemaCompletenessEntityType] }}</template>
          </el-table-column>
          <el-table-column label="資料名稱" min-width="200">
            <template #default="{ row }">{{ row.label || '（未命名）' }}</template>
          </el-table-column>
          <el-table-column label="缺少的欄位" min-width="260">
            <template #default="{ row }">
              <el-tag
                v-for="field in row.missingFields"
                :key="field.labelZh"
                type="danger"
                size="small"
                effect="plain"
                class="schema-report__field-tag"
              >
                {{ field.labelZh }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="100" fixed="right">
            <template #default="{ row }">
              <el-button v-if="editRouteFor(row)" size="small" text type="primary" @click="handleEdit(row)">編輯</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.schema-report__notice {
  margin-bottom: 12px;
}

.schema-report__notice-text {
  margin: 0;
  line-height: 1.7;
}

.schema-report__notice-text + .schema-report__notice-text {
  margin-top: 8px;
}

.schema-report__group {
  margin-bottom: 16px;
}

.schema-report__group-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.schema-report__group-title {
  font-weight: 600;
  font-size: 15px;
}

.schema-report__group-count {
  color: var(--admin-text-secondary);
  font-size: 13px;
}

.schema-report__group-hint {
  margin: 6px 0 0;
  color: var(--admin-text-secondary);
  font-size: 13px;
}

.schema-report__field-tag {
  margin: 2px 4px 2px 0;
}
</style>
