// GET /api/charity/projects/:slug — 項目詳情頁 `/{lang}/p/<project_slug>` 用。
export default defineEventHandler((event) => {
  const slug = getRouterParam(event, 'slug') ?? ''
  const project = findProjectBySlug(slug)
  if (!project) {
    throw createError({ statusCode: 404, statusMessage: 'Project not found' })
  }
  return project
})
