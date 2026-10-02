/**
 * I 網站設定其餘子模組（H 批，apps/api/README.md「H 批」§5）：選單、全域設定、多語系、介面字串、
 * 電子報平台設定。場地見 `adminVenues.ts`。權限碼（`site.menu.*`／`site.global.*`／`site.locale.*`／
 * `site.string.*`／`site.edm.*`）只決定畫面顯示，真正把關在後端。
 */
import { apiRequest, apiUploadRequest, buildMultipart, buildQuery, type PagedResult } from './adminCommon'

const club$ = (club: string) => `/api/v1/admin/${club}`

// ───────────── 選單 ─────────────

export type MenuLocation = 'main' | 'mega' | 'footer'

export interface AdminMenuItem {
  id: string
  labelZh: string
  labelEn?: string | null
  url?: string | null
  isExternal: boolean
  children: AdminMenuItem[]
}

export interface AdminMenuLocationDto {
  location: MenuLocation
  label: string
  items: AdminMenuItem[]
}

export interface UpsertMenuItem {
  id?: string | null
  labelZh: string
  labelEn?: string | null
  url?: string | null
  isExternal: boolean
  children?: UpsertMenuItem[]
}

export function getAdminMenus(club: string): Promise<{ locations: AdminMenuLocationDto[] }> {
  return apiRequest(`${club$(club)}/menus`)
}

/** 整棵樹取代：有 id 沿用、沒有新增、既有不在請求裡就刪除。 */
export function replaceAdminMenu(club: string, location: MenuLocation, items: UpsertMenuItem[]): Promise<void> {
  return apiRequest<void>(`${club$(club)}/menus/${location}`, { method: 'PUT', body: { items } })
}

// ───────────── 全域設定 ─────────────

export interface AdminPolicy {
  /** cookie／privacy／member-terms 之類的內部代碼，不顯示。 */
  code: string
  title: string
  bodyZh?: string | null
  bodyEn?: string | null
  updatedAt?: string | null
}

export interface AdminGlobalSettingsDto {
  brand: {
    logoLightUrl?: string | null
    logoDarkUrl?: string | null
    faviconUrl?: string | null
    brandColor?: string | null
    brandSecondaryColor?: string | null
  }
  policies: AdminPolicy[]
  maintenance: { enabled: boolean; messageZh?: string | null; messageEn?: string | null; updatedAt?: string | null }
}

export interface PolicyInput {
  bodyZh: string
  bodyEn: string
}

export interface UpdateGlobalSettingsPayload {
  brandColor: string | null
  brandSecondaryColor: string | null
  removeLogoLight: boolean
  removeLogoDark: boolean
  removeFavicon: boolean
  cookiePolicy: PolicyInput
  privacyPolicy: PolicyInput
  memberTerms: PolicyInput
  maintenanceEnabled: boolean
  maintenanceMessageZh: string
  maintenanceMessageEn: string
}

export function getAdminGlobalSettings(club: string): Promise<AdminGlobalSettingsDto> {
  return apiRequest(`${club$(club)}/global-settings`)
}

export function updateAdminGlobalSettings(
  club: string,
  payload: UpdateGlobalSettingsPayload,
  files: { logoLight: File | null; logoDark: File | null; favicon: File | null },
): Promise<AdminGlobalSettingsDto> {
  return apiUploadRequest<AdminGlobalSettingsDto>(`${club$(club)}/global-settings`, buildMultipart(payload, files), { method: 'PUT' })
}

// ───────────── 多語系 ─────────────

export interface AdminLocale {
  code: string
  name: string
  isDefault: boolean
  fallbackCode?: string | null
  isEnabled: boolean
  sortOrder: number
}

export interface AdminI18nSettings {
  fallbackMode: 'show_default' | 'hide'
  dateFormatZh?: string | null
  dateFormatEn?: string | null
  numberFormatZh?: string | null
  numberFormatEn?: string | null
}

export interface TranslationRow {
  type: string
  typeLabel: string
  id: string
  label: string
  isShared: boolean
  done: Record<string, boolean>
}

export interface TranslationSummary {
  type: string
  typeLabel: string
  total: number
  missing: Record<string, number>
}

export interface TranslationOverview {
  locales: string[]
  summary: TranslationSummary[]
  items: TranslationRow[]
  page: number
  pageSize: number
  totalCount: number
}

export function listAdminLocales(club: string): Promise<AdminLocale[]> {
  return apiRequest(`${club$(club)}/i18n/locales`)
}

export function updateAdminLocale(
  club: string,
  code: string,
  body: { name: string; isEnabled: boolean; fallbackCode: string | null; sortOrder: number },
): Promise<AdminLocale> {
  return apiRequest(`${club$(club)}/i18n/locales/${encodeURIComponent(code)}`, { method: 'PUT', body })
}

export function getAdminI18nSettings(club: string): Promise<AdminI18nSettings> {
  return apiRequest(`${club$(club)}/i18n/settings`)
}

export function updateAdminI18nSettings(club: string, body: AdminI18nSettings): Promise<AdminI18nSettings> {
  return apiRequest(`${club$(club)}/i18n/settings`, { method: 'PUT', body })
}

export function getTranslationOverview(
  club: string,
  query: { type?: string; missing?: string; keyword?: string; page?: number; pageSize?: number },
): Promise<TranslationOverview> {
  return apiRequest(`${club$(club)}/i18n/overview${buildQuery(query)}`)
}

// ───────────── 介面字串翻譯表 ─────────────

export interface AdminUiString {
  id: string
  key: string
  group?: string | null
  values: Record<string, string>
  updatedAt: string
}

export function listAdminUiStrings(
  club: string,
  query: { group?: string; keyword?: string; missing?: string; page?: number; pageSize?: number },
): Promise<PagedResult<AdminUiString>> {
  return apiRequest(`${club$(club)}/i18n/strings${buildQuery(query)}`)
}

export function listAdminUiStringGroups(club: string): Promise<string[]> {
  return apiRequest(`${club$(club)}/i18n/strings/groups`)
}

export function createAdminUiString(
  club: string,
  body: { key: string; group: string | null; values: Record<string, string | null> },
): Promise<AdminUiString> {
  return apiRequest(`${club$(club)}/i18n/strings`, { method: 'POST', body })
}

/** 只處理有出現的語系，空白＝刪除該語系翻譯（繁中不能清空）。 */
export function updateAdminUiString(
  club: string,
  id: string,
  body: { group?: string | null; values: Record<string, string | null> },
): Promise<AdminUiString> {
  return apiRequest(`${club$(club)}/i18n/strings/${id}`, { method: 'PUT', body })
}

export function deleteAdminUiString(club: string, id: string): Promise<void> {
  return apiRequest<void>(`${club$(club)}/i18n/strings/${id}`, { method: 'DELETE' })
}

// ───────────── 電子報平台設定（金鑰只寫不讀） ─────────────

export interface AdminEdmSettings {
  enabled: boolean
  provider?: string | null
  listId?: string | null
  senderEmail?: string | null
  apiKeyConfigured: boolean
  integrationAvailable: boolean
  updatedAt?: string | null
}

export function getAdminEdmSettings(club: string): Promise<AdminEdmSettings> {
  return apiRequest(`${club$(club)}/edm-settings`)
}

export function updateAdminEdmSettings(
  club: string,
  body: { enabled: boolean; provider: string | null; listId: string | null; senderEmail: string | null; apiKey: string | null; clearApiKey: boolean },
): Promise<AdminEdmSettings> {
  return apiRequest(`${club$(club)}/edm-settings`, { method: 'PUT', body })
}
