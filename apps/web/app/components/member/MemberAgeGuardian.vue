<script setup lang="ts">
// app/components/member/MemberAgeGuardian.vue — 註冊的生日欄位＋未滿 18 歲的監護人同意（Email 註冊與 LINE 完成頁共用）。
//
// 主站規劃書「會員資料安全要求」：未滿 18 歲須經監護人同意方得註冊。後端（`MemberAgeGate`）要求生日必填；
// 依台北當地日期算足歲，未滿 18 歲再要求 `guardianConsent`（同意旗標、監護人姓名、與當事人關係）。
// 前端用同一套台北日期算法（`taipeiAge`）決定要不要顯示監護人區塊，真正的判斷仍在後端。
// 🔴 同意文案待法務（B-9）：本元件只放「待法務定稿」的佔位說明，**不得自擬法律條文**；
// 送出的文案版本號固定為 `pending-legal`（見 `GUARDIAN_CONSENT_VERSION_PENDING`），法務定稿後才替換。
import { GUARDIAN_RELATIONSHIP_LABEL, GUARDIAN_RELATIONSHIP_LABEL_EN, isMinorBirth, taipeiToday } from '#shared/utils/member'
import type { GuardianRelationship } from '#shared/utils/member'

// `en`：LINE 導回頁的網址固定是 /zh/，英文使用者回來時語系要由呼叫端指定；沒給就看網址語系。
const props = defineProps<{ idPrefix: string, en?: boolean }>()
const { isEn } = useLocale()
const en = computed(() => props.en ?? isEn.value)
const tx = (zh: string, e: string): string => (en.value ? e : zh)
const birthOn = defineModel<string>('birthOn', { required: true })
const guardian = defineModel<{ consented: boolean, name: string, relationship: GuardianRelationship | '' }>('guardian', { required: true })

const isMinor = computed(() => isMinorBirth(birthOn.value))
const maxDate = taipeiToday()
</script>

<template>
<div class="form-field">
  <label :for="`${props.idPrefix}-birth`">{{ tx('生日', 'Date of birth') }}<span class="req" aria-hidden="true">*</span></label>
  <input :id="`${props.idPrefix}-birth`" v-model="birthOn" type="date" name="birthOn" required autocomplete="bday" :max="maxDate" :aria-describedby="`${props.idPrefix}-birth-hint`">
  <p :id="`${props.idPrefix}-birth-hint`" class="field-hint">{{ tx('用於確認是否年滿 18 歲；未滿 18 歲須經監護人同意才能註冊。', 'Used to confirm whether you are 18 or over. Members under 18 need their guardian\'s consent to register.') }}</p>
</div>

<fieldset v-if="isMinor" class="guardian-consent" :aria-describedby="`${props.idPrefix}-guardian-note`">
  <legend>{{ tx('監護人同意（未滿 18 歲）', 'Guardian consent (under 18)') }}</legend>
  <p :id="`${props.idPrefix}-guardian-note`" class="mc-alert mc-alert--info" role="note">
    <template v-if="en"><strong>The consent wording is pending legal review.</strong> Until the final wording is confirmed, this section only records that the guardian has consented, together with the guardian's name and relationship. It does not constitute the final consent terms. Please have the guardian complete it.</template>
    <template v-else><strong>同意條款文字待法務定稿。</strong>正式條文確認前，本區塊僅記錄「監護人已同意」、監護人姓名與關係，不構成最終的同意條款。請由監護人本人填寫。</template>
  </p>
  <div class="form-field">
    <label :for="`${props.idPrefix}-g-name`">{{ tx('監護人姓名', 'Guardian name') }}<span class="req" aria-hidden="true">*</span></label>
    <input :id="`${props.idPrefix}-g-name`" v-model="guardian.name" type="text" name="guardianName" maxlength="64" autocomplete="off">
  </div>
  <div class="form-field">
    <label :for="`${props.idPrefix}-g-rel`">{{ tx('與會員的關係', 'Relationship to the member') }}<span class="req" aria-hidden="true">*</span></label>
    <select :id="`${props.idPrefix}-g-rel`" v-model="guardian.relationship" name="guardianRelationship">
      <option value="">{{ tx('請選擇', 'Please choose') }}</option>
      <option v-for="(label, value) in (en ? GUARDIAN_RELATIONSHIP_LABEL_EN : GUARDIAN_RELATIONSHIP_LABEL)" :key="value" :value="value">{{ label }}</option>
    </select>
  </div>
  <div class="checkbox-field">
    <input :id="`${props.idPrefix}-g-consent`" v-model="guardian.consented" type="checkbox" name="guardianConsented">
    <label :for="`${props.idPrefix}-g-consent`">{{ tx('我是上述會員的監護人，同意其註冊成為會員。', 'I am the guardian of the member above and consent to their registering as a member.') }}<span class="req" aria-hidden="true">*</span></label>
  </div>
</fieldset>
</template>

<style>
.guardian-consent{ grid-column:1 / -1; margin-top:1rem; }
</style>
