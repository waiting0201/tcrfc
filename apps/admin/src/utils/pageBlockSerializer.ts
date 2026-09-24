/**
 * B1 頁面區塊的畫面狀態 ↔ 後端 JSON 互轉，對照 `Features/AdminPages/PageBlockContentProcessor.cs`。
 * 兩個方向：
 * - `parseBlockFromDto`：後端回傳的 `AdminPageBlockDto`（讀取既有頁面／版本快照）→ 畫面狀態。
 * - `serializeBlocksForSubmit`：畫面狀態（`PageBlockState[]`）→ 建立／更新請求的 `blocks` 陣列
 *   ＋ 待上傳的檔案（`Record<'file:{區塊索引}:{圖片路徑}', File>`），連同前端自己的驗證——
 *   跟後端 `PageBlockContentProcessor` 的規則對齊，但**後端仍是最終依據**，這裡的檢查只是
 *   減少一次不必要的往返，不是取代伺服器端驗證。
 */
import type { AdminPageBlockDto, AdminPageBlockInputDto } from '@/api/adminPages'
import {
  type AccordionFaqBlockContent,
  type BilingualText,
  type CtaBlockContent,
  type FileDownloadBlockContent,
  type GalleryBlockContent,
  type ImageSlotState,
  type PageBlockContent,
  type PageBlockState,
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

export class PageBlockValidationError extends Error {}

function randomLocalKey(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `local-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

// ───────────────────────────── 解析（後端 → 畫面） ─────────────────────────────

function parseBilingual(raw: unknown): BilingualText {
  const obj = (raw ?? {}) as { zh?: string | null; en?: string | null }
  return { zh: obj.zh ?? '', en: obj.en ?? '' }
}

function parseImageSlot(raw: unknown): ImageSlotState {
  const obj = (raw ?? {}) as { key?: string | null; width?: number | null; height?: number | null; altZh?: string | null; altEn?: string | null }
  return {
    existingKey: obj.key ?? null,
    existingWidth: obj.width ?? null,
    existingHeight: obj.height ?? null,
    file: null,
    cleared: false,
    altZh: obj.altZh ?? '',
    altEn: obj.altEn ?? '',
  }
}

function parseItemArray<T>(raw: unknown, parseItem: (item: unknown) => T): T[] {
  return Array.isArray(raw) ? raw.map(parseItem) : []
}

export function parseBlockContent(blockType: PageBlockType, raw: unknown): PageBlockContent {
  const obj = (raw ?? {}) as Record<string, unknown>
  switch (blockType) {
    case 'text':
      return { body: parseBilingual(obj.body) } satisfies TextBlockContent
    case 'text_image':
      return {
        body: parseBilingual(obj.body),
        imagePosition: obj.imagePosition === 'right' ? 'right' : 'left',
        image: parseImageSlot(obj.image),
      } satisfies TextImageBlockContent
    case 'gallery':
      return { images: parseItemArray(obj.images, parseImageSlot) } satisfies GalleryBlockContent
    case 'video_embed':
      return {
        provider: obj.provider === 'vimeo' ? 'vimeo' : 'youtube',
        videoId: typeof obj.videoId === 'string' ? obj.videoId : '',
        caption: parseBilingual(obj.caption),
      } satisfies VideoEmbedBlockContent
    case 'quote':
      return { text: parseBilingual(obj.text), attribution: parseBilingual(obj.attribution) } satisfies QuoteBlockContent
    case 'cta':
      return {
        text: parseBilingual(obj.text),
        buttonLabel: parseBilingual(obj.buttonLabel),
        buttonUrl: typeof obj.buttonUrl === 'string' ? obj.buttonUrl : '',
      } satisfies CtaBlockContent
    case 'accordion_faq':
      return {
        items: parseItemArray(obj.items, (item) => {
          const i = (item ?? {}) as Record<string, unknown>
          return { question: parseBilingual(i.question), answer: parseBilingual(i.answer) }
        }),
      } satisfies AccordionFaqBlockContent
    case 'timeline':
      return {
        items: parseItemArray(obj.items, (item) => {
          const i = (item ?? {}) as Record<string, unknown>
          return {
            date: typeof i.date === 'string' ? i.date : '',
            title: parseBilingual(i.title),
            description: parseBilingual(i.description),
          }
        }),
      } satisfies TimelineBlockContent
    case 'steps':
      return {
        items: parseItemArray(obj.items, (item) => {
          const i = (item ?? {}) as Record<string, unknown>
          return { title: parseBilingual(i.title), description: parseBilingual(i.description) }
        }),
      } satisfies StepsBlockContent
    case 'stat_cards':
      return {
        items: parseItemArray(obj.items, (item) => {
          const i = (item ?? {}) as Record<string, unknown>
          return { value: typeof i.value === 'string' ? i.value : '', label: parseBilingual(i.label) }
        }),
      } satisfies StatCardsBlockContent
    case 'table':
      return {
        headers: parseItemArray(obj.headers, parseBilingual),
        rows: Array.isArray(obj.rows) ? obj.rows.map((row) => (Array.isArray(row) ? row.map((cell) => String(cell ?? '')) : [])) : [],
      } satisfies TableBlockContent
    case 'file_download':
      return {
        label: parseBilingual(obj.label),
        fileUrl: typeof obj.fileUrl === 'string' ? obj.fileUrl : '',
      } satisfies FileDownloadBlockContent
  }
}

export function parseBlockFromDto(dto: AdminPageBlockDto): PageBlockState {
  return { localKey: randomLocalKey(), blockType: dto.blockType as PageBlockType, content: parseBlockContent(dto.blockType as PageBlockType, dto.content) }
}

// ───────────────────────────── 序列化（畫面 → 後端） ─────────────────────────────

function serializeBilingual(pair: BilingualText): { zh: string; en: string | null } {
  return { zh: pair.zh, en: pair.en.trim() ? pair.en : null }
}

/** 可省略的雙語欄位：兩個語言都留白就整個省略（後端「這個欄位可以整個不提供」），
 * 只填了其中一種就一律走必填規則（中文非空白），理由與 `RequireOptionalBilingualText` 一致。 */
function serializeOptionalBilingual(pair: BilingualText, blockIndex: number, fieldLabel: string): { zh: string; en: string | null } | undefined {
  if (!pair.zh.trim() && !pair.en.trim()) return undefined
  if (!pair.zh.trim()) {
    throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊的「${fieldLabel}」已填寫英文，請一併填寫中文（或把英文也清空）。`)
  }
  return serializeBilingual(pair)
}

function requireBilingual(pair: BilingualText, blockIndex: number, fieldLabel: string): { zh: string; en: string | null } {
  if (!pair.zh.trim()) {
    throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊的「${fieldLabel}」（中文）為必填欄位。`)
  }
  return serializeBilingual(pair)
}

function requireNonEmpty(value: string, blockIndex: number, fieldLabel: string): string {
  if (!value.trim()) {
    throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊的「${fieldLabel}」為必填欄位。`)
  }
  return value.trim()
}

/** 序列化單一圖片欄位。有新選的檔案就標記待上傳並記錄檔案（供呼叫端組進 multipart 請求）；
 * 沒有新檔案時必須還有既有圖片可沿用（`existingKey` 有值且沒被使用者按下「移除」）。 */
function serializeImageSlot(
  slot: ImageSlotState, blockIndex: number, uploadPath: string, files: Record<string, File>,
): Record<string, unknown> {
  const altZh = requireNonEmpty(slot.altZh, blockIndex, '圖片替代文字（中文）')
  const altEn = slot.altEn.trim() ? slot.altEn : null

  if (slot.file) {
    files[`file:${blockIndex}:${uploadPath}`] = slot.file
    return { pendingUpload: true, altZh, altEn }
  }
  if (slot.existingKey && !slot.cleared) {
    return {
      key: slot.existingKey,
      width: slot.existingWidth ?? undefined,
      height: slot.existingHeight ?? undefined,
      altZh,
      altEn,
    }
  }
  throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊尚未選擇圖片，請上傳一張圖片後再儲存。`)
}

function serializeBlockContent(block: PageBlockState, blockIndex: number, files: Record<string, File>): unknown {
  switch (block.blockType) {
    case 'text': {
      const c = block.content as TextBlockContent
      return { body: requireBilingual(c.body, blockIndex, '內文') }
    }
    case 'text_image': {
      const c = block.content as TextImageBlockContent
      return {
        body: requireBilingual(c.body, blockIndex, '內文'),
        imagePosition: c.imagePosition,
        image: serializeImageSlot(c.image, blockIndex, 'image', files),
      }
    }
    case 'gallery': {
      const c = block.content as GalleryBlockContent
      if (c.images.length === 0) {
        throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊（圖片藝廊）至少需要 1 張圖片。`)
      }
      return { images: c.images.map((slot, i) => serializeImageSlot(slot, blockIndex, `images:${i}`, files)) }
    }
    case 'video_embed': {
      const c = block.content as VideoEmbedBlockContent
      return {
        provider: c.provider,
        videoId: requireNonEmpty(c.videoId, blockIndex, '影片代碼'),
        caption: serializeOptionalBilingual(c.caption, blockIndex, '說明文字'),
      }
    }
    case 'quote': {
      const c = block.content as QuoteBlockContent
      return {
        text: requireBilingual(c.text, blockIndex, '引言文字'),
        attribution: serializeOptionalBilingual(c.attribution, blockIndex, '引言來源'),
      }
    }
    case 'cta': {
      const c = block.content as CtaBlockContent
      return {
        text: requireBilingual(c.text, blockIndex, '文字'),
        buttonLabel: requireBilingual(c.buttonLabel, blockIndex, '按鈕文字'),
        buttonUrl: requireNonEmpty(c.buttonUrl, blockIndex, '按鈕連結網址'),
      }
    }
    case 'accordion_faq': {
      const c = block.content as AccordionFaqBlockContent
      if (c.items.length === 0) throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊至少需要 1 筆項目。`)
      return {
        items: c.items.map((item, i) => ({
          question: requireBilingual(item.question, blockIndex, `第 ${i + 1} 筆的問題`),
          answer: requireBilingual(item.answer, blockIndex, `第 ${i + 1} 筆的答案`),
        })),
      }
    }
    case 'timeline': {
      const c = block.content as TimelineBlockContent
      if (c.items.length === 0) throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊至少需要 1 筆項目。`)
      return {
        items: c.items.map((item, i) => ({
          date: requireNonEmpty(item.date, blockIndex, `第 ${i + 1} 筆的日期`),
          title: requireBilingual(item.title, blockIndex, `第 ${i + 1} 筆的標題`),
          description: serializeOptionalBilingual(item.description, blockIndex, `第 ${i + 1} 筆的說明`),
        })),
      }
    }
    case 'steps': {
      const c = block.content as StepsBlockContent
      if (c.items.length === 0) throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊至少需要 1 筆項目。`)
      return {
        items: c.items.map((item, i) => ({
          title: requireBilingual(item.title, blockIndex, `第 ${i + 1} 筆的標題`),
          description: serializeOptionalBilingual(item.description, blockIndex, `第 ${i + 1} 筆的說明`),
        })),
      }
    }
    case 'stat_cards': {
      const c = block.content as StatCardsBlockContent
      if (c.items.length === 0) throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊至少需要 1 筆項目。`)
      return {
        items: c.items.map((item, i) => ({
          value: requireNonEmpty(item.value, blockIndex, `第 ${i + 1} 筆的數據值`),
          label: requireBilingual(item.label, blockIndex, `第 ${i + 1} 筆的說明文字`),
        })),
      }
    }
    case 'table': {
      const c = block.content as TableBlockContent
      if (c.headers.length === 0) throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊（表格）至少需要 1 個欄位標題。`)
      const headers = c.headers.map((h, i) => requireBilingual(h, blockIndex, `表格第 ${i + 1} 欄標題`))
      for (const [r, row] of c.rows.entries()) {
        if (row.length !== c.headers.length) {
          throw new PageBlockValidationError(`第 ${blockIndex + 1} 個區塊（表格）第 ${r + 1} 列的欄數與標題欄數不一致。`)
        }
      }
      return { headers, rows: c.rows }
    }
    case 'file_download': {
      const c = block.content as FileDownloadBlockContent
      return {
        label: requireBilingual(c.label, blockIndex, '檔案名稱'),
        fileUrl: requireNonEmpty(c.fileUrl, blockIndex, '檔案網址'),
      }
    }
  }
}

/**
 * 把畫面上的整份區塊清單序列化成建立／更新請求要用的 `blocks` 陣列，並收集待上傳的檔案。
 * 拋出 `PageBlockValidationError` 時，呼叫端應該把訊息顯示給使用者、不送出請求——
 * 這是前端先擋一次明顯錯誤，**後端仍會重新驗證一次**（不能只靠這裡）。
 */
export function serializeBlocksForSubmit(blocks: PageBlockState[]): { blocks: AdminPageBlockInputDto[]; files: Record<string, File> } {
  const files: Record<string, File> = {}
  const serialized = blocks.map((block, index) => ({
    blockType: block.blockType,
    content: serializeBlockContent(block, index, files),
  }))
  return { blocks: serialized, files }
}
