/** 站台設定、系統信樣板、金流與發票通道：`/api/v1/donation-platform/admin/…`。 */
import { ADMIN_ROOT, apiRequest } from './http'

export interface SiteSettings {
  homeIntroZh: string | null
  homeIntroEn: string | null
  thankYouTemplateZh: string | null
  thankYouTemplateEn: string | null
  noticeZh: string | null
  noticeEn: string | null
  privacyPolicyZh: string | null
  privacyPolicyEn: string | null
  clubSiteUrl: string | null
  defaultMinAmount: number
  defaultMaxAmount: number
  creditListEnabled: boolean
}

/** 局部更新：沒帶的欄位不變；文案與網址送空字串＝清空。 */
export interface SiteSettingsInput {
  homeIntroZh?: string
  homeIntroEn?: string
  thankYouTemplateZh?: string
  thankYouTemplateEn?: string
  noticeZh?: string
  noticeEn?: string
  privacyPolicyZh?: string
  privacyPolicyEn?: string
  clubSiteUrl?: string
  defaultMinAmount?: number
  defaultMaxAmount?: number
  creditListEnabled?: boolean
}

export interface EmailTemplate {
  code: string
  label: string
  isActive: boolean
  tokens: { token: string; description: string }[]
  zh: { subject: string; body: string } | null
  en: { subject: string; body: string } | null
}

export interface EmailTemplateInput {
  isActive: boolean
  subjectZh: string
  bodyZh: string
  subjectEn: string
  bodyEn: string
}

export type PaymentEnvironment = 'sandbox' | 'production'

export interface PaymentChannel {
  channelType: string
  label: string
  activeEnvironment: PaymentEnvironment
  environments: { environment: PaymentEnvironment; label: string; hasCredential: boolean; invoicePrefix: string | null; rotatedAt: string | null }[]
}

export const getSettings = () => apiRequest<SiteSettings>(`${ADMIN_ROOT}/settings`)
export const updateSettings = (input: SiteSettingsInput) => apiRequest<SiteSettings>(`${ADMIN_ROOT}/settings`, { method: 'PUT', body: input })

export const listEmailTemplates = () => apiRequest<EmailTemplate[]>(`${ADMIN_ROOT}/email-templates`)
export const updateEmailTemplate = (code: string, input: EmailTemplateInput) =>
  apiRequest<EmailTemplate>(`${ADMIN_ROOT}/email-templates/${encodeURIComponent(code)}`, { method: 'PUT', body: input })

export const listPaymentChannels = () => apiRequest<PaymentChannel[]>(`${ADMIN_ROOT}/payment-channels`)
/** 憑證只寫不讀：送出後伺服器加密保存，之後任何端點都不會再回傳內容。 */
export const setPaymentCredential = (channelType: string, input: { environment: PaymentEnvironment; credential: string; invoicePrefix?: string }) =>
  apiRequest<PaymentChannel>(`${ADMIN_ROOT}/payment-channels/${encodeURIComponent(channelType)}/credential`, { method: 'PUT', body: input })
export const switchPaymentEnvironment = (channelType: string, environment: PaymentEnvironment) =>
  apiRequest<PaymentChannel>(`${ADMIN_ROOT}/payment-channels/${encodeURIComponent(channelType)}/environment`, {
    method: 'PUT',
    body: { environment, confirm: true },
  })
