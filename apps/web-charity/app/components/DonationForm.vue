<script setup lang="ts">
// DonationForm.vue — 捐款表單（docs/22-charity-ui.md §2.5，規劃書 §3.3／§5.2）。
//
// 無障礙要求（docs/22 §1.8，WCAG 1.4.1）：錯誤狀態一律「邊框變 2px 危險色＋文字說明＋警示符號
// ＋ aria-invalid／aria-describedby」四件事一起做，不單靠顏色。送出時捲動並置焦到第一個錯誤欄位。
import { useLang } from '../composables/useLang'
import { useCheckoutDraft, useMockOrder, generateMockOrderNo } from '../composables/useCheckoutDraft'
import { formatTwd } from '../utils/currency'
import { isValidEmail, isValidMobileCarrier, isValidTaxId, isAmountInRange } from '../utils/validators'

const props = defineProps<{
  project: {
    slug: string
    name_zh: string
    name_en: string
    min_amount: number
    max_amount: number
    invoice_mode: 'b2c_invoice' | 'donation_receipt'
    amountOptions: number[]
  }
  storeSlug: string | null
}>()

const { lang, tr, tt } = useLang()
const draft = useCheckoutDraft(props.project.slug, props.storeSlug)

const customAmountInput = ref<string>('')
const isCustomAmountActive = computed(() => draft.value.amount !== null && !props.project.amountOptions.includes(draft.value.amount))

function selectAmount(amount: number) {
  draft.value.amount = amount
  customAmountInput.value = ''
}

function onCustomAmountInput() {
  const parsed = Number(customAmountInput.value.replace(/[^0-9]/g, ''))
  draft.value.amount = customAmountInput.value.trim() === '' ? null : parsed
}

const submitLabel = computed(() => {
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
  taxId?: string
  invoiceTitle?: string
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

  if (d.amount === null || !isAmountInRange(d.amount, props.project.min_amount, props.project.max_amount)) {
    e.amount = tt(tr.value.form.errorAmountRange, { min: props.project.min_amount.toLocaleString('en-US'), max: props.project.max_amount.toLocaleString('en-US') })
  }
  if (!d.donorName.trim()) {
    e.donorName = tr.value.form.errorRequired
  }
  if (!d.donorEmail.trim()) {
    e.donorEmail = tr.value.form.errorRequired
  } else if (!isValidEmail(d.donorEmail)) {
    e.donorEmail = tr.value.form.errorEmail
  }

  if (props.project.invoice_mode === 'b2c_invoice') {
    if (!d.invoiceType) {
      e.invoiceType = tr.value.form.errorRequired
    } else if (d.invoiceType === 'mobile_carrier' && !isValidMobileCarrier(d.mobileCarrier)) {
      e.mobileCarrier = tr.value.form.errorMobileCarrier
    } else if (d.invoiceType === 'tax_id') {
      if (!isValidTaxId(d.taxId)) e.taxId = tr.value.form.errorTaxId
      if (!d.invoiceTitle.trim()) e.invoiceTitle = tr.value.form.errorRequired
    }
  }

  if (!d.agreedToPrivacy) {
    e.consent = tr.value.form.errorConsent
  }

  errors.value = e

  const order: (keyof Errors)[] = ['amount', 'donorName', 'donorEmail', 'invoiceType', 'mobileCarrier', 'taxId', 'invoiceTitle', 'consent']
  const firstErrorKey = order.find((key) => e[key])
  if (firstErrorKey) {
    const el = fieldRefs[firstErrorKey]
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    el?.focus?.()
  }

  return Object.keys(e).length === 0
}

async function onSubmit() {
  if (!validate()) return

  const orderNo = generateMockOrderNo()
  const order = useMockOrder(orderNo)
  order.value = {
    orderNo,
    projectSlug: props.project.slug,
    projectNameZh: props.project.name_zh,
    projectNameEn: props.project.name_en,
    amount: draft.value.amount as number,
    createdAt: new Date().toISOString(),
  }

  await navigateTo(`/${lang.value}/pay/${orderNo}`)
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
          {{ tt(tr.project.minMaxHint, { min: project.min_amount.toLocaleString('en-US'), max: project.max_amount.toLocaleString('en-US') }) }}
        </p>
      </div>

      <div class="field">
        <label class="field-label field-required" :for="`donor-name-${project.slug}`">{{ tr.form.donorName }}</label>
        <input
          :id="`donor-name-${project.slug}`"
          :ref="setFieldRef('donorName')"
          v-model="draft.donorName"
          type="text"
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

      <fieldset v-if="project.invoice_mode === 'b2c_invoice'" :ref="setFieldRef('invoiceType')" class="field" style="border: none; padding: 0;" tabindex="-1">
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
          <label class="field-label" :for="`love-code-${project.slug}`">{{ tr.form.loveCodeLabel }}</label>
          <input :id="`love-code-${project.slug}`" v-model="draft.loveCode" type="text" inputmode="numeric" class="field-input">
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
        <input :id="`receipt-title-${project.slug}`" v-model="draft.receiptTitle" type="text" class="field-input" :placeholder="draft.donorName">

        <label class="field-label" style="margin-top: var(--sp-3);" :for="`national-id-${project.slug}`">{{ tr.form.nationalIdLabel }}</label>
        <input :id="`national-id-${project.slug}`" v-model="draft.nationalId" type="text" class="field-input" :aria-describedby="`national-id-hint-${project.slug}`">
        <p :id="`national-id-hint-${project.slug}`" class="field-hint">{{ tr.form.nationalIdHint }}</p>

        <label class="field-label" style="margin-top: var(--sp-3);" :for="`address-${project.slug}`">{{ tr.form.addressLabel }}</label>
        <input :id="`address-${project.slug}`" v-model="draft.address" type="text" class="field-input" :aria-describedby="`address-hint-${project.slug}`">
        <p :id="`address-hint-${project.slug}`" class="field-hint">{{ tr.form.addressHint }}</p>
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

      <button type="submit" class="btn btn-primary btn-block">{{ submitLabel }}</button>
    </form>
  </section>
</template>
