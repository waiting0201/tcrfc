// GET /api/charity/projects — 已上架項目卡片牆（掃碼落地頁與一般入口共用同一份資料）。
export default defineEventHandler(() => listPublishedProjects())
