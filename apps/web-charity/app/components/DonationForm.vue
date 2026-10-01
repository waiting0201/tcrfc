<script setup lang="ts">
// DonationForm.vue — 捐款表單（docs/22-charity-ui.md §2.5，規劃書 §3.3／§5.2）。
//
// 送出流程（apps/api 契約，CH-2）：
//   POST /donations（標頭 Idempotency-Key）→ POST /donations/{單號}/pay → 整頁導向 paymentUrl（LINE Pay）。
//   付款返回由 /{lang}/result/{單號} 接手（confirm／cancel／輪詢）。建單成功但發起付款失敗時，使用者再按一次送出，
//   因為內容沒變會沿用同一個冪等鍵，後端回原單（created:false）而不是重複建單。
//
// 無障礙要求（docs/22 §1.8，WCAG 1.4.1）：錯誤狀態一律「邊框變 2px 危險色＋文字說明＋警示符號
// ＋ aria-invalid／aria-describedby」四件事一起做，不單靠顏色。送出時捲動並置焦到第一個錯誤欄位。
import { useLang } from '../composables/useLang'
import { useCheckoutDraft, resolveIdempotencyKey } from '../composables/useCheckoutDraft'
import { toApiProblem } from '../composables/useCharityApi'
import { formatTwd } from '../utils/currency'
import {
  isValidEmail,
  isValidMobileCarrier,
  isValidTaxId,
  isValidLoveCode,
  isValidNationalId,
  isAmountInRange,
} from '../utils/validators'
import type {
  CreateDonationRequest,
  CreateDonationResponse,
  PublicProjectDetail,
  StartPaymentResponse,
} from '../types/charity'

const props = defineProps<{
  project: PublicProjectDetail
  storeSlug: string | null
}>()

const { lang, tr, tt } = useLang()
const api = useCharityApi()
const config = useRuntimeConfig()
const draft = useCheckoutDraft(props.project.slug, props.storeSlug)

const turnstileKey = config.public.turnstileSiteKey
const turnstileToken = ref<string | null>(null)
const turnstile = ref<{ reset: () => void } | null>(null)

const customAmountInput = ref<string>('')
const isCustomAmountActive = computed(() => draft.value.amount !== null && !props.project.amountOptions.includes(draft.value.amount))

function selectAmount(amount: number) {
  draft.value.amount = amount
  customAmountInput.value = ''
}

function onCustomAmountInput() {
  const digits = customAmountInput.value.replace(/[^0-9]/g, '')
  customAmountInput.value = digits
  draft.value.amount = digits === '' ? null : Number(digits)
}

const submitting = ref(false)
const formError = ref('')

const submitLabel = computed(() => {
  if (submitting.value) return tr.value.form.submitting
  if (draft.value.amount === null || draft.value.amount <= 0) return tr.value.form.submitNoAmount
  return tt(tr.value.form.submit, { amount: draft.value.amount.toLocaleString('en-US') })
})

// ---- 驗證狀態 ----
interface Errors {
  amount?: string
  donorName?: string
  donorEmail?: string
  invoiceType?: string
  mobileCarrier?: string
  loveCode?: string
  taxId?: string
  invoiceTitle?: string
  nationalId?: string
  consent?: string
}

const errors = ref<Errors>({})
const fieldRefs: Record<string, HTMLElement | null> = {}

function setFieldRef(key: string) {
  return (el: Element | ComponentPublicInstance | null) => {
    fieldRefs[key] = el as HTMLElement | null
  }
}

function validate(): boolean {
  const e: Errors = {}
  const d = draft.value
  const p = props.project

  if (d.amount === null || !isAmountInRange(d.amount, p.minAmount, p.maxAmount)) {
    e.amount = tt(tr.value.form.errorAmountRange, { min: p.minAmount.toLocaleString('en-US'), max: p.maxAmount.toLocaleString('en-US') })
  }
  if (!d.donorName.trim()) {
    e.donorName = tr.value.form.errorRequired
  }
  if (!d.donorEmail.trim()) {
    e.donorEmail = tr.value.form.errorRequired
  } else if (!isValidEmail(d.donorEmail)) {
    e.donorEmail = tr.value.form.errorEmail
  }

  if (p.invoiceMode === 'b2c_invoice') {
    if (!d.invoiceType) {
      e.invoiceType = tr.value.form.errorRequired
    } else if (d.invoiceType === 'mobile_carrier' && !isValidMobileCarrier(d.mobileCarrier)) {
      e.mobileCarrier = tr.value.form.errorMobileCarrier
    } else if (d.invoiceType === 'love_code' && !isValidLoveCode(d.loveCode)) {
      e.loveCode = tr.value.form.errorLoveCode
    } else if (d.invoiceType === 'tax_id') {
      if (!isValidTaxId(d.taxId)) e.taxId = tr.value.form.errorTaxId
      if (!d.invoiceTitle.trim()) e.invoiceTitle = tr.value.form.errorRequired
    }
  } else if (d.nationalId.trim() && !isValidNationalId(d.nationalId)) {
    e.nationalId = tr.value.form.errorNationalId
  }

  if (!d.agreedToPrivacy) {
    e.consent = tr.value.form.errorConsent
  }

  errors.value = e

  const order: (keyof Errors)[] = ['amount', 'donorName', 'donorEmail', 'invoiceType', 'mobileCarrier', 'loveCode', 'taxId', 'invoiceTitle', 'nationalId', 'consent']
  const firstErrorKey = order.find((key) => e[key])
  if (firstErrorKey) {
    const el = fieldRefs[firstErrorKey]
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    el?.focus?.()
  }

  return Object.keys(e).length === 0
}

function buildRequest(): CreateDonationRequest {
  const d = draft.value
  const body: CreateDonationRequest = {
    projectSlug: props.project.slug,
    amount: d.amount as number,
    donorName: d.donorName.trim(),
    donorEmail: d.donorEmail.trim(),
    isAnonymous: d.isAnonymous,
    consentPrivacy: d.agreedToPrivacy,
    invoice: {},
    lang: lang.value,
  }
  if (props.storeSlug) body.storeSlug = props.storeSlug

  if (props.project.invoiceMode === 'b2c_invoice') {
    if (d.invoiceType === 'mobile_carrier') {
      body.invoice = { type: 'mobile_carrier', mobileCarrier: d.mobileCarrier.trim().toUpperCase() }
    } else if (d.invoiceType === 'love_code') {
      body.invoice = { type: 'love_code', loveCode: d.loveCode.trim() }
    } else if (d.invoiceType === 'tax_id') {
      body.invoice = { type: 'tax_id', taxId: d.taxId.trim(), invoiceTitle: d.invoiceTitle.trim() }
    }
  } else {
    body.invoice = {
      receiptTitle: d.receiptTitle.trim() || undefined,
      nationalId: d.nationalId.trim() ? d.nationalId.trim().toUpperCase() : undefined,
      address: d.address.trim() || undefined,
      isAnnualSummary: d.isAnnualSummary,
    }
  }
  return body
}

function describeSubmitError(error: unknown): string {
  const problem = toApiProblem(error)
  if (problem.status === 0) return tr.value.form.errorNetwork
  if (problem.status === 429) return tr.value.form.errorRateLimit
  return problem.detail ?? tr.value.form.errorSubmitFallback
}

async function onSubmit() {
  if (submitting.value) return
  formError.value = ''
  if (!validate()) return
  if (turnstileKey && !turnstileToken.value) {
    formError.value = tr.value.form.errorTurnstile
    return
  }

  const body = buildRequest()
  // 指紋不含 Turnstile 權杖（每次都不同），只含會影響建單內容的欄位。
  const key = resolveIdempotencyKey(draft.value, JSON.stringify(body))
  if (turnstileKey) body.turnstileToken = turnstileToken.value ?? undefined

  submitting.value = true
  try {
    const created = await api.request<CreateDonationResponse>('/donations', {
      method: 'POST',
      body,
      headers: { 'Idempotency-Key': key },
    })
    const payment = await api.request<StartPaymentResponse>(`/donations/${encodeURIComponent(created.orderNo)}/pay`, {
      method: 'POST',
      body: { lang: lang.value },
    })
    if (!/^https?:\/\//i.test(payment.paymentUrl)) {
      throw new Error('invalid payment url')
    }
    // 整頁導向付款網址；導向期間維持 submitting，避免連點。
    window.location.assign(payment.paymentUrl)
  } catch (error) {
    formError.value = describeSubmitError(error)
    submitting.value = false
    // Turnstile 權杖只能用一次，失敗後要換新的。
    turnstile.value?.reset()
  }
}
</script>

<template>
  <section class="card" aria-labelledby="donation-form-heading">
    <h2 id="donation-form-heading">{{ tr.form.heading }}</h2>

    <form novalidate @submit.prevent="onSubmit">
      <div class="field">
        <span id="amount-label" class="field-label field-required">{{ tr.form.amountHeading }}</span>
        <div
          :ref="setFieldRef('amount')"
          class="amount-grid"
          role="group"
          aria-labelledby="amount-label"
          tabindex="-1"
        >
          <button
            v-for="option in project.amountOptions"
            :key="option"
            type="button"
            class="amount-option"
            :aria-pressed="draft.amount === option"
            @click="selectAmount(option)"
          >
            {{ formatTwd(option) }}
          </button>
        </div>
        <div class="field" style="margin-bottom: 0;">
          <label class="field-label" :for="`custom-amount-${project.slug}`">{{ tr.form.customAmountLabel }}</label>
          <div style="display:flex; align-items:center; gap: 8px;">
            <input
              :id="`custom-amount-${project.slug}`"
              v-model="customAmountInput"
              type="text"
              inputmode="decimal"
              class="field-input"
              :style="isCustomAmountActive ? { borderColor: 'var(--charity-primary)', borderWidth: '2px' } : undefined"
              @input="onCustomAmountInput"
            >
            <span aria-hidden="true">{{ tr.form.customAmountSuffix }}</span>
          </div>
        </div>
        <p v-if="errors.amount" class="field-error" role="alert">
          <span class="icon" aria-hidden="true">⚠</span>{{ errors.amount }}
        </p>
        <p class="field-hint">
          {{ tt(tr.project.minMaxHint, { min: project.minAmount.toLocaleString('en-US'), max: project.maxAmount.toLocaleString('en-US') }) }}
        </p>
      </div>

      <div class="field">
        <label class="field-label field-required" :for="`donor-name-${project.slug}`">{{ tr.form.donorName }}</label>
        <input
          :id="`donor-name-${project.slug}`"
          :ref="setFieldRef('donorName')"
          v-model="draft.donorName"
          type="text"
          maxlength="128"
          autocomplete="name"
          class="field-input"
          :aria-invalid="!!errors.donorName"
          :aria-describedby="errors.donorName ? `donor-name-error-${project.slug}` : undefined"
        >
        <p v-if="errors.donorName" :id="`donor-name-error-${project.slug}`" class="field-error" role="alert">
          <span class="icon" aria-hidden="true">⚠</span>{{ errors.donorName }}
        </p>
      </div>

      <div class="field">
        <label class="field-label field-required" :for="`donor-email-${project.slug}`">{{ tr.form.donorEmail }}</label>
        <input
          :id="`donor-email-${project.slug}`"
          :ref="setFieldRef('donorEmail')"
          v-model="draft.donorEmail"
          type="email"
          inputmode="email"
          maxlength="255"
          autocomplete="email"
          class="field-input"
          :aria-invalid="!!errors.donorEmail"
          :aria-describedby="errors.donorEmail ? `donor-email-error-${project.slug}` : 'donor-email-hint'"
        >
        <p :id="'donor-email-hint'" class="field-hint">{{ tr.form.emailHint }}</p>
        <p v-if="errors.donorEmail" :id="`donor-email-error-${project.slug}`" class="field-error" role="alert">
          <span class="icon" aria-hidden="true">⚠</span>{{ errors.donorEmail }}
        </p>
      </div>

      <fieldset class="field" style="border: none; padding: 0; margin: 0 0 var(--sp-5);">
        <legend class="field-label">{{ tr.form.identityHeading }}</legend>
        <div class="radio-group">
          <label class="radio-option">
            <input v-model="draft.isAnonymous" type="radio" :value="false" :name="`anon-${project.slug}`">
            {{ tr.form.named }}
          </label>
          <label class="radio-option">
            <input v-model="draft.isAnonymous" type="radio" :value="true" :name="`anon-${project.slug}`">
            {{ tr.form.anonymous }}
          </label>
        </div>
      </fieldset>

      <fieldset v-if="project.invoiceMode === 'b2c_invoice'" :ref="setFieldRef('invoiceType')" class="field" style="border: none; padding: 0;" tabindex="-1">
        <legend class="field-label field-required">{{ tr.form.voucherHeading }} — {{ tr.form.invoiceTypeLabel }}</legend>
        <div class="radio-group">
          <label class="radio-option">
            <input v-model="draft.invoiceType" type="radio" value="mobile_carrier" :name="`invoice-type-${project.slug}`">
            {{ tr.form.invoiceTypeMobileCarrier }}
          </label>
          <label class="radio-option">
            <input v-model="draft.invoiceType" type="radio" value="love_code" :name="`invoice-type-${project.slug}`">
            {{ tr.form.invoiceTypeDonation }}
          </label>
          <label class="radio-option">
            <input v-model="draft.invoiceType" type="radio" value="tax_id" :name="`invoice-type-${project.slug}`">
            {{ tr.form.invoiceTypeTaxId }}
          </label>
        </div>
        <p v-if="errors.invoiceType" class="field-error" role="alert">
          <span class="icon" aria-hidden="true">⚠</span>{{ errors.invoiceType }}
        </p>

        <div v-if="draft.invoiceType === 'mobile_carrier'" class="field">
          <label class="field-label field-required" :for="`mobile-carrier-${project.slug}`">{{ tr.form.mobileCarrierLabel }}</label>
          <input
            :id="`mobile-carrier-${project.slug}`"
            :ref="setFieldRef('mobileCarrier')"
            v-model="draft.mobileCarrier"
            type="text"
            class="field-input"
            :aria-invalid="!!errors.mobileCarrier"
            :aria-describedby="`mobile-carrier-hint-${project.slug}`"
          >
          <p :id="`mobile-carrier-hint-${project.slug}`" class="field-hint">{{ tr.form.mobileCarrierHint }}</p>
          <p v-if="errors.mobileCarrier" class="field-error" role="alert">
            <span class="icon" aria-hidden="true">⚠</span>{{ errors.mobileCarrier }}
          </p>
        </div>

        <div v-else-if="draft.invoiceType === 'love_code'" class="field">
          <label class="field-label field-required" :for="`love-code-${project.slug}`">{{ tr.form.loveCodeLabel }}</label>
          <input
            :id="`love-code-${project.slug}`"
            :ref="setFieldRef('loveCode')"
            v-model="draft.loveCode"
            type="text"
            inputmode="numeric"
            maxlength="7"
            class="field-input"
            :aria-invalid="!!errors.loveCode"
            :aria-describedby="`love-code-hint-${project.slug}`"
          >
          <p :id="`love-code-hint-${project.slug}`" class="field-hint">{{ tr.form.loveCodeHint }}</p>
          <p v-if="errors.loveCode" class="field-error" role="alert">
            <span class="icon" aria-hidden="true">⚠</span>{{ errors.loveCode }}
          </p>
        </div>

        <div v-else-if="draft.invoiceType === 'tax_id'" class="field">
          <label class="field-label field-required" :for="`tax-id-${project.slug}`">{{ tr.form.taxIdLabel }}</label>
          <input
            :id="`tax-id-${project.slug}`"
            :ref="setFieldRef('taxId')"
            v-model="draft.taxId"
            type="text"
            inputmode="numeric"
            class="field-input"
            :aria-invalid="!!errors.taxId"
          >
          <p v-if="errors.taxId" class="field-error" role="alert">
            <span class="icon" aria-hidden="true">⚠</span>{{ errors.taxId }}
          </p>

          <label class="field-label field-required" style="margin-top: var(--sp-3);" :for="`invoice-title-${project.slug}`">{{ tr.form.invoiceTitleLabel }}</label>
          <input
            :id="`invoice-title-${project.slug}`"
            :ref="setFieldRef('invoiceTitle')"
            v-model="draft.invoiceTitle"
            type="text"
            maxlength="128"
            class="field-input"
            :aria-invalid="!!errors.invoiceTitle"
          >
          <p v-if="errors.invoiceTitle" class="field-error" role="alert">
            <span class="icon" aria-hidden="true">⚠</span>{{ errors.invoiceTitle }}
          </p>
        </div>
      </fieldset>

      <fieldset v-else class="field" style="border: none; padding: 0;">
        <legend class="field-label">{{ tr.form.voucherHeading }}</legend>
        <label class="field-label" :for="`receipt-title-${project.slug}`">{{ tr.form.receiptTitleLabel }}</label>
        <input :id="`receipt-title-${project.slug}`" v-model="draft.receiptTitle" type="text" maxlength="128" class="field-input" :placeholder="draft.donorName">

        <label class="field-label" style="margin-top: var(--sp-3);" :for="`national-id-${project.slug}`">{{ tr.form.nationalIdLabel }}</label>
        <input
          :id="`national-id-${project.slug}`"
          :ref="setFieldRef('nationalId')"
          v-model="draft.nationalId"
          type="text"
          maxlength="10"
          autocomplete="off"
          class="field-input"
          :aria-invalid="!!errors.nationalId"
          :aria-describedby="`national-id-hint-${project.slug}`"
        >
        <p :id="`national-id-hint-${project.slug}`" class="field-hint">{{ tr.form.nationalIdHint }}</p>
        <p v-if="errors.nationalId" class="field-error" role="alert">
          <span class="icon" aria-hidden="true">⚠</span>{{ errors.nationalId }}
        </p>

        <label class="field-label" style="margin-top: var(--sp-3);" :for="`address-${project.slug}`">{{ tr.form.addressLabel }}</label>
        <input :id="`address-${project.slug}`" v-model="draft.address" type="text" maxlength="500" class="field-input" :aria-describedby="`address-hint-${project.slug}`">
        <p :id="`address-hint-${project.slug}`" class="field-hint">{{ tr.form.addressHint }}</p>

        <span class="field-label" style="margin-top: var(--sp-3); display: block;">{{ tr.form.annualSummaryLabel }}</span>
        <div class="radio-group">
          <label class="radio-option">
            <input v-model="draft.isAnnualSummary" type="radio" :value="false" :name="`annual-${project.slug}`">
            {{ tr.form.annualSummarySingle }}
          </label>
          <label class="radio-option">
            <input v-model="draft.isAnnualSummary" type="radio" :value="true" :name="`annual-${project.slug}`">
            {{ tr.form.annualSummaryYear }}
          </label>
        </div>
      </fieldset>

      <div class="field">
        <div :ref="setFieldRef('consent')" class="checkbox-row" :class="{ 'is-invalid': errors.consent }" tabindex="-1">
          <input :id="`consent-${project.slug}`" v-model="draft.agreedToPrivacy" type="checkbox" :aria-invalid="!!errors.consent">
          <label :for="`consent-${project.slug}`">
            {{ tr.form.consentLabel }}
            <NuxtLink :to="`/${lang}/privacy/`" target="_blank">{{ tr.form.privacyPolicyLink }}</NuxtLink>
          </label>
        </div>
        <p v-if="errors.consent" class="field-error" role="alert">
          <span class="icon" aria-hidden="true">⚠</span>{{ errors.consent }}
        </p>
      </div>

      <TurnstileWidget
        v-if="turnstileKey"
        ref="turnstile"
        :site-key="turnstileKey"
        :lang="lang"
        @token="turnstileToken = $event"
      />

      <p class="field-hint">{{ tt(tr.form.recipientNotice) }}</p>

      <div v-if="formError" class="form-error-banner" role="alert">
        <span class="icon" aria-hidden="true">⚠</span> {{ formError }}
      </div>

      <button type="submit" class="btn btn-primary btn-block" :disabled="submitting" :aria-busy="submitting">{{ submitLabel }}</button>
    </form>
  </section>
</template>
