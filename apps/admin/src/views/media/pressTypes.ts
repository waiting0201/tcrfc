import type { PressResourceType } from '@/api/adminPress'

/** 媒體資源類別的日常說法（後端字面值是英文代碼，畫面一律顯示這裡的中文）。 */
export const PRESS_TYPE_LABEL: Record<PressResourceType, string> = {
  press_release: '新聞稿',
  brand_kit: '品牌識別包',
  hires_image: '高解析圖',
}

export const PRESS_TYPE_ORDER: PressResourceType[] = ['press_release', 'brand_kit', 'hires_image']
