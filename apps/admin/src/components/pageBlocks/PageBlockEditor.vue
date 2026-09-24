<script setup lang="ts">
/**
 * B1 頁面管理的區塊化編輯器——單一區塊的內容編輯（12 種型別依 `blockType` 切換要顯示的欄位）。
 * 對照 `apps/api/README.md`「12 種區塊的欄位與驗證摘要」與 `@/types/pageBlocks.ts`。
 *
 * `content` 是父層（`PageEditView.vue` 的 `blocks` 陣列元素）持有的 reactive 物件，這裡直接
 * 用型別轉型後的 computed 存取並就地修改巢狀屬性——這不是「修改 prop 本身」（Vue 只警告
 * 重新賦值 prop 變數，不警告修改物件型別 prop 的巢狀屬性），是這個專案既有的慣例
 * （`CompetitionEditView.vue` 直接綁 `form.xxx` 也是同一種模式），比起每一層都寫一組
 * `emit`／`v-model` 省下大量樣板碼，對 12 種區塊、部分還帶陣列欄位的表單尤其明顯。
 *
 * 圖片欄位直接重用既有的 `ImageUploader.vue`（選檔不上傳、儲存才上傳，見該元件檔頭），
 * `v-model:file` 對到 `ImageSlotState.file`、`v-model:remove-cover` 對到 `ImageSlotState.cleared`，
 * 命名雖然沿用「cover」的既有慣例但語意通用（任何一張圖片欄位都適用）。
 */
import { computed } from 'vue'
import ImageUploader from '@/components/ImageUploader.vue'
import BilingualShortField from '@/components/BilingualShortField.vue'
import BilingualTextareaField from '@/components/BilingualTextareaField.vue'
import {
  emptyBilingualText,
  emptyImageSlot,
  type AccordionFaqBlockContent,
  type CtaBlockContent,
  type FileDownloadBlockContent,
  type GalleryBlockContent,
  type PageBlockContent,
  type PageBlockType,
  type QuoteBlockContent,
  type StatCardsBlockContent,
  type StepsBlockContent,
  type TableBlockContent,
  type TextBlockContent,
  type TextImageBlockContent,
  type TimelineBlockContent,
  type VideoEmbedBlockContent,
} from '@/types/pageBlocks'

const props = defineProps<{
  blockType: PageBlockType
  content: PageBlockContent
}>()

// ── 各型別的型別轉型（同一個物件參照，只是換個型別視角） ─────────────────────
const textC = computed(() => props.content as TextBlockContent)
const textImageC = computed(() => props.content as TextImageBlockContent)
const galleryC = computed(() => props.content as GalleryBlockContent)
const videoC = computed(() => props.content as VideoEmbedBlockContent)
const quoteC = computed(() => props.content as QuoteBlockContent)
const ctaC = computed(() => props.content as CtaBlockContent)
const faqC = computed(() => props.content as AccordionFaqBlockContent)
const timelineC = computed(() => props.content as TimelineBlockContent)
const stepsC = computed(() => props.content as StepsBlockContent)
const statCardsC = computed(() => props.content as StatCardsBlockContent)
const tableC = computed(() => props.content as TableBlockContent)
const fileDownloadC = computed(() => props.content as FileDownloadBlockContent)

function moveItem<T>(list: T[], index: number, delta: number) {
  const target = index + delta
  if (target < 0 || target >= list.length) return
  const [item] = list.splice(index, 1)
  list.splice(target, 0, item)
}

// 圖片藝廊
function addGalleryImage() {
  galleryC.value.images.push(emptyImageSlot())
}
function removeGalleryImage(index: number) {
  galleryC.value.images.splice(index, 1)
}

// 手風琴 FAQ
function addFaqItem() {
  faqC.value.items.push({ question: emptyBilingualText(), answer: emptyBilingualText() })
}
function removeFaqItem(index: number) {
  faqC.value.items.splice(index, 1)
}

// 時間軸
function addTimelineItem() {
  timelineC.value.items.push({ date: '', title: emptyBilingualText(), description: emptyBilingualText() })
}
function removeTimelineItem(index: number) {
  timelineC.value.items.splice(index, 1)
}

// 步驟條
function addStepsItem() {
  stepsC.value.items.push({ title: emptyBilingualText(), description: emptyBilingualText() })
}
function removeStepsItem(index: number) {
  stepsC.value.items.splice(index, 1)
}

// 數據卡
function addStatCard() {
  statCardsC.value.items.push({ value: '', label: emptyBilingualText() })
}
function removeStatCard(index: number) {
  statCardsC.value.items.splice(index, 1)
}

// 表格：新增／刪除欄位時，每一列都要跟著補一格／刪一格，欄數才會一直等於標題數
// （後端驗證每列欄數必須等於標題欄數，見 apps/api/README.md）。
function addTableColumn() {
  tableC.value.headers.push(emptyBilingualText())
  for (const row of tableC.value.rows) row.push('')
}
function removeTableColumn(colIndex: number) {
  tableC.value.headers.splice(colIndex, 1)
  for (const row of tableC.value.rows) row.splice(colIndex, 1)
}
function addTableRow() {
  tableC.value.rows.push(tableC.value.headers.map(() => ''))
}
function removeTableRow(rowIndex: number) {
  tableC.value.rows.splice(rowIndex, 1)
}
</script>

<template>
  <div class="page-block-editor">
    <!-- 文字 -->
    <template v-if="blockType === 'text'">
      <el-form-item label="內文（中文）" required>
        <el-input v-model="textC.body.zh" type="textarea" :rows="6" placeholder="請輸入內文" />
      </el-form-item>
      <el-form-item label="內文（英文）">
        <el-input v-model="textC.body.en" type="textarea" :rows="6" placeholder="Enter English content" />
      </el-form-item>
    </template>

    <!-- 圖文左右 -->
    <template v-else-if="blockType === 'text_image'">
      <el-form-item label="圖片位置" required>
        <el-radio-group v-model="textImageC.imagePosition">
          <el-radio value="left">圖片在左</el-radio>
          <el-radio value="right">圖片在右</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="內文（中文）" required>
        <el-input v-model="textImageC.body.zh" type="textarea" :rows="5" placeholder="請輸入內文" />
      </el-form-item>
      <el-form-item label="內文（英文）">
        <el-input v-model="textImageC.body.en" type="textarea" :rows="5" placeholder="Enter English content" />
      </el-form-item>
      <el-form-item label="圖片" required>
        <div class="page-block-editor__image-slot">
          <ImageUploader
            v-model:file="textImageC.image.file"
            v-model:remove-cover="textImageC.image.cleared"
            :has-existing-image="!!textImageC.image.existingKey"
          />
          <BilingualShortField
            label="圖片替代文字"
            :zh="textImageC.image.altZh"
            :en="textImageC.image.altEn"
            required
            placeholder="描述圖片內容，供螢幕閱讀器與搜尋引擎使用"
            @update:zh="(v) => (textImageC.image.altZh = v)"
            @update:en="(v) => (textImageC.image.altEn = v)"
          />
        </div>
      </el-form-item>
    </template>

    <!-- 圖片藝廊 -->
    <template v-else-if="blockType === 'gallery'">
      <div v-for="(slot, i) in galleryC.images" :key="i" class="page-block-editor__item-card">
        <div class="page-block-editor__item-toolbar">
          <span class="page-block-editor__item-index">第 {{ i + 1 }} 張</span>
          <el-button
            size="small"
            text
            type="danger"
            :disabled="galleryC.images.length <= 1"
            @click="removeGalleryImage(i)"
          >
            刪除這張圖片
          </el-button>
        </div>
        <ImageUploader
          v-model:file="slot.file"
          v-model:remove-cover="slot.cleared"
          :has-existing-image="!!slot.existingKey"
        />
        <BilingualShortField
          label="圖片替代文字"
          :zh="slot.altZh"
          :en="slot.altEn"
          required
          placeholder="描述圖片內容"
          @update:zh="(v) => (slot.altZh = v)"
          @update:en="(v) => (slot.altEn = v)"
        />
      </div>
      <el-button @click="addGalleryImage">+ 新增圖片</el-button>
    </template>

    <!-- 影音嵌入 -->
    <template v-else-if="blockType === 'video_embed'">
      <el-form-item label="影音來源" required>
        <el-radio-group v-model="videoC.provider">
          <el-radio value="youtube">YouTube</el-radio>
          <el-radio value="vimeo">Vimeo</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="影片代碼" required>
        <el-input v-model="videoC.videoId" placeholder="影片網址裡代表這支影片的那一段代碼，不是整段網址" />
      </el-form-item>
      <BilingualShortField
        label="說明文字"
        :zh="videoC.caption.zh"
        :en="videoC.caption.en"
        placeholder="選填"
        @update:zh="(v) => (videoC.caption.zh = v)"
        @update:en="(v) => (videoC.caption.en = v)"
      />
    </template>

    <!-- 引言 -->
    <template v-else-if="blockType === 'quote'">
      <BilingualTextareaField
        label="引言文字"
        :zh="quoteC.text.zh"
        :en="quoteC.text.en"
        required
        :rows="3"
        @update:zh="(v) => (quoteC.text.zh = v)"
        @update:en="(v) => (quoteC.text.en = v)"
      />
      <BilingualShortField
        label="引言來源"
        :zh="quoteC.attribution.zh"
        :en="quoteC.attribution.en"
        placeholder="選填，例如：講者姓名／出處"
        @update:zh="(v) => (quoteC.attribution.zh = v)"
        @update:en="(v) => (quoteC.attribution.en = v)"
      />
    </template>

    <!-- CTA -->
    <template v-else-if="blockType === 'cta'">
      <BilingualShortField
        label="文字"
        :zh="ctaC.text.zh"
        :en="ctaC.text.en"
        required
        @update:zh="(v) => (ctaC.text.zh = v)"
        @update:en="(v) => (ctaC.text.en = v)"
      />
      <BilingualShortField
        label="按鈕文字"
        :zh="ctaC.buttonLabel.zh"
        :en="ctaC.buttonLabel.en"
        required
        @update:zh="(v) => (ctaC.buttonLabel.zh = v)"
        @update:en="(v) => (ctaC.buttonLabel.en = v)"
      />
      <el-form-item label="按鈕連結網址" required>
        <el-input v-model="ctaC.buttonUrl" placeholder="例如：/zh/programs/ 或完整網址" />
      </el-form-item>
    </template>

    <!-- 手風琴 FAQ -->
    <template v-else-if="blockType === 'accordion_faq'">
      <div v-for="(item, i) in faqC.items" :key="i" class="page-block-editor__item-card">
        <div class="page-block-editor__item-toolbar">
          <span class="page-block-editor__item-index">第 {{ i + 1 }} 筆</span>
          <div>
            <el-button size="small" text :disabled="i === 0" @click="moveItem(faqC.items, i, -1)">上移</el-button>
            <el-button size="small" text :disabled="i === faqC.items.length - 1" @click="moveItem(faqC.items, i, 1)">下移</el-button>
            <el-button size="small" text type="danger" :disabled="faqC.items.length <= 1" @click="removeFaqItem(i)">刪除</el-button>
          </div>
        </div>
        <BilingualShortField
          label="問題"
          :zh="item.question.zh"
          :en="item.question.en"
          required
          @update:zh="(v) => (item.question.zh = v)"
          @update:en="(v) => (item.question.en = v)"
        />
        <BilingualTextareaField
          label="答案"
          :zh="item.answer.zh"
          :en="item.answer.en"
          required
          :rows="3"
          @update:zh="(v) => (item.answer.zh = v)"
          @update:en="(v) => (item.answer.en = v)"
        />
      </div>
      <el-button @click="addFaqItem">+ 新增一筆</el-button>
    </template>

    <!-- 時間軸 -->
    <template v-else-if="blockType === 'timeline'">
      <div v-for="(item, i) in timelineC.items" :key="i" class="page-block-editor__item-card">
        <div class="page-block-editor__item-toolbar">
          <span class="page-block-editor__item-index">第 {{ i + 1 }} 筆</span>
          <div>
            <el-button size="small" text :disabled="i === 0" @click="moveItem(timelineC.items, i, -1)">上移</el-button>
            <el-button size="small" text :disabled="i === timelineC.items.length - 1" @click="moveItem(timelineC.items, i, 1)">下移</el-button>
            <el-button size="small" text type="danger" :disabled="timelineC.items.length <= 1" @click="removeTimelineItem(i)">刪除</el-button>
          </div>
        </div>
        <el-form-item label="日期" required>
          <el-input v-model="item.date" placeholder="例如：2024 年 3 月，文字自由填寫，不限定日期格式" />
        </el-form-item>
        <BilingualShortField
          label="標題"
          :zh="item.title.zh"
          :en="item.title.en"
          required
          @update:zh="(v) => (item.title.zh = v)"
          @update:en="(v) => (item.title.en = v)"
        />
        <BilingualTextareaField
          label="說明"
          :zh="item.description.zh"
          :en="item.description.en"
          placeholder="選填"
          :rows="2"
          @update:zh="(v) => (item.description.zh = v)"
          @update:en="(v) => (item.description.en = v)"
        />
      </div>
      <el-button @click="addTimelineItem">+ 新增一筆</el-button>
    </template>

    <!-- 步驟條 -->
    <template v-else-if="blockType === 'steps'">
      <div v-for="(item, i) in stepsC.items" :key="i" class="page-block-editor__item-card">
        <div class="page-block-editor__item-toolbar">
          <span class="page-block-editor__item-index">步驟 {{ i + 1 }}</span>
          <div>
            <el-button size="small" text :disabled="i === 0" @click="moveItem(stepsC.items, i, -1)">上移</el-button>
            <el-button size="small" text :disabled="i === stepsC.items.length - 1" @click="moveItem(stepsC.items, i, 1)">下移</el-button>
            <el-button size="small" text type="danger" :disabled="stepsC.items.length <= 1" @click="removeStepsItem(i)">刪除</el-button>
          </div>
        </div>
        <BilingualShortField
          label="標題"
          :zh="item.title.zh"
          :en="item.title.en"
          required
          @update:zh="(v) => (item.title.zh = v)"
          @update:en="(v) => (item.title.en = v)"
        />
        <BilingualTextareaField
          label="說明"
          :zh="item.description.zh"
          :en="item.description.en"
          placeholder="選填"
          :rows="2"
          @update:zh="(v) => (item.description.zh = v)"
          @update:en="(v) => (item.description.en = v)"
        />
      </div>
      <el-button @click="addStepsItem">+ 新增步驟</el-button>
    </template>

    <!-- 數據卡 -->
    <template v-else-if="blockType === 'stat_cards'">
      <div v-for="(item, i) in statCardsC.items" :key="i" class="page-block-editor__item-card">
        <div class="page-block-editor__item-toolbar">
          <span class="page-block-editor__item-index">第 {{ i + 1 }} 張</span>
          <el-button size="small" text type="danger" :disabled="statCardsC.items.length <= 1" @click="removeStatCard(i)">刪除</el-button>
        </div>
        <el-form-item label="數據值" required>
          <el-input v-model="item.value" placeholder="例如：120＋、98%" />
        </el-form-item>
        <BilingualShortField
          label="說明文字"
          :zh="item.label.zh"
          :en="item.label.en"
          required
          @update:zh="(v) => (item.label.zh = v)"
          @update:en="(v) => (item.label.en = v)"
        />
      </div>
      <el-button @click="addStatCard">+ 新增數據卡</el-button>
    </template>

    <!-- 表格 -->
    <template v-else-if="blockType === 'table'">
      <p class="page-block-editor__hint">欄位標題需要雙語，儲存格內容自行決定要不要雙語混用。</p>
      <div class="page-block-editor__table-headers">
        <div v-for="(header, colIndex) in tableC.headers" :key="colIndex" class="page-block-editor__table-header-cell">
          <BilingualShortField
            :label="`第 ${colIndex + 1} 欄標題`"
            :zh="header.zh"
            :en="header.en"
            required
            @update:zh="(v) => (header.zh = v)"
            @update:en="(v) => (header.en = v)"
          />
          <el-button size="small" text type="danger" :disabled="tableC.headers.length <= 1" @click="removeTableColumn(colIndex)">
            刪除這一欄
          </el-button>
        </div>
        <el-button @click="addTableColumn">+ 新增欄位</el-button>
      </div>

      <el-table :data="tableC.rows.map((row, rowIndex) => ({ row, rowIndex }))" size="small" class="page-block-editor__table-rows">
        <el-table-column v-for="(header, colIndex) in tableC.headers" :key="colIndex" :label="header.zh || `第 ${colIndex + 1} 欄`">
          <template #default="{ row: entry }">
            <el-input v-model="entry.row[colIndex]" size="small" />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="80">
          <template #default="{ row: entry }">
            <el-button size="small" text type="danger" :disabled="tableC.rows.length <= 1" @click="removeTableRow(entry.rowIndex)">
              刪除
            </el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-button @click="addTableRow">+ 新增一列</el-button>
    </template>

    <!-- 檔案下載 -->
    <template v-else-if="blockType === 'file_download'">
      <BilingualShortField
        label="檔案名稱"
        :zh="fileDownloadC.label.zh"
        :en="fileDownloadC.label.en"
        required
        @update:zh="(v) => (fileDownloadC.label.zh = v)"
        @update:en="(v) => (fileDownloadC.label.en = v)"
      />
      <el-form-item label="檔案網址" required>
        <el-input v-model="fileDownloadC.fileUrl" placeholder="請貼上已有的外部網址或既有物件鍵，本區塊不支援直接上傳新檔案" />
        <span class="page-block-editor__hint">
          本區塊不支援直接上傳新檔案（圖片上傳服務只處理圖片，PDF 等檔案會被損毀），請貼上已經放好的檔案網址。
        </span>
      </el-form-item>
    </template>
  </div>
</template>

<style scoped>
.page-block-editor__hint {
  display: block;
  font-size: 12px;
  color: var(--admin-text-tertiary);
  margin-top: 4px;
}

.page-block-editor__image-slot {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.page-block-editor__item-card {
  border: 1px solid var(--admin-border);
  border-radius: 4px;
  padding: 12px;
  margin-bottom: 12px;
  background: var(--admin-bg-surface-2);
}

.page-block-editor__item-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.page-block-editor__item-index {
  font-size: 12px;
  color: var(--admin-text-tertiary);
}

.page-block-editor__table-headers {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 12px;
}

.page-block-editor__table-header-cell {
  width: 240px;
}

.page-block-editor__table-rows {
  margin-bottom: 8px;
}
</style>
