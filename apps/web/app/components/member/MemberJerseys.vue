<script setup lang="ts">
// app/components/member/MemberJerseys.vue — 球衣登記（主站 §3.14「球衣發放」）
//
// 付費會籍開通後，會員填寫尺寸與領取方式（寄送／到場領取；寄送須填收件人、電話與地址），家庭方案依
// `jersey_quota` 逐件登記。發放狀態（待處理／已寄出／已領取）會員端可見；**只有「待處理」能修改**。
// 件數上限由後端強制（並行也不會超收），前端只負責呈現與預先擋掉明顯錯誤。
// 尺寸是自由文字（後端只限 16 字元內）：尺碼表尚未提供，故不做固定選單，避免寫死一份未經客戶確認的尺碼。
import { formatPlainDate, toMemberApiError } from '#shared/utils/member'
import type { MemberJerseyGroup, MemberJerseyItem } from '#shared/utils/member'

const { authedFetch } = useMemberSession()
const { locale, isEn, tx } = useLocale()

const groups = ref<MemberJerseyGroup[] | null>(null)
const loadError = ref('')

async function load() {
  loadError.value = ''
  try {
    groups.value = await authedFetch<MemberJerseyGroup[]>('/api/backend/member/jerseys', { query: { lang: locale.value } })
  }
  catch (err) {
    groups.value = []
    loadError.value = toMemberApiError(err, undefined, isEn.value).detail
  }
}
onMounted(load)

interface Draft { id: string | null, membershipId: string, clubCode: string, recipientName: string, size: string, deliveryMethod: 'ship' | 'pickup', phone: string, address: string }
const draft = ref<Draft | null>(null)
const busy = ref(false)
const error = ref('')
const done = ref('')

function startNew(g: MemberJerseyGroup) {
  done.value = ''
  error.value = ''
  draft.value = { id: null, membershipId: g.membershipId, clubCode: g.clubCode, recipientName: '', size: '', deliveryMethod: 'pickup', phone: '', address: '' }
}
function startEdit(g: MemberJerseyGroup, j: MemberJerseyItem) {
  done.value = ''
  error.value = ''
  draft.value = {
    id: j.id,
    membershipId: g.membershipId,
    clubCode: g.clubCode,
    recipientName: j.recipientName,
    size: j.size ?? '',
    deliveryMethod: j.deliveryMethod ?? 'pickup',
    phone: j.phone ?? '',
    address: j.address ?? '',
  }
}

async function save() {
  const d = draft.value
  if (!d) return
  error.value = ''
  if (!d.recipientName.trim()) { error.value = tx('請輸入穿著人或收件人姓名。', 'Please enter the name of the wearer or recipient.'); return }
  if (!d.size.trim()) { error.value = tx('請輸入尺寸。', 'Please enter a size.'); return }
  if (d.deliveryMethod === 'ship' && (!d.phone.trim() || !d.address.trim())) { error.value = tx('選擇寄送時，需填寫電話與地址。', 'If you choose delivery, please enter a phone number and address.'); return }
  busy.value = true
  try {
    const body = {
      recipientName: d.recipientName.trim(),
      size: d.size.trim(),
      deliveryMethod: d.deliveryMethod,
      phone: d.phone.trim() || undefined,
      address: d.deliveryMethod === 'ship' ? d.address.trim() : undefined,
    }
    if (d.id) await authedFetch(`/api/backend/${d.clubCode}/member/jerseys/${d.id}`, { method: 'PUT', body })
    else await authedFetch(`/api/backend/${d.clubCode}/member/jerseys`, { method: 'POST', body: { ...body, membershipId: d.membershipId } })
    done.value = d.id ? tx('已更新球衣登記。', 'Your jersey registration has been updated.') : tx('已登記球衣。', 'Your jersey has been registered.')
    draft.value = null
    await load()
  }
  catch (err) { error.value = toMemberApiError(err, undefined, isEn.value).detail }
  finally { busy.value = false }
}
</script>

<template>
  <div class="mc-jerseys">
    <p v-if="groups === null" class="mc-empty">{{ tx('載入中…', 'Loading…') }}</p>
    <p v-else-if="loadError" class="mc-alert mc-alert--error" role="alert">{{ loadError }}</p>
    <p v-else-if="groups.length === 0" class="mc-empty">
      <template v-if="isEn">You do not have a membership eligible for jersey registration. The welcome jersey is a benefit of <strong>Paid Fan Club</strong> membership. Once your membership is activated you can enter your size and how you will receive it here.</template>
      <template v-else>目前沒有可登記球衣的會籍。入會球衣是<strong>付費球迷會員</strong>的權益，會籍開通後即可在此填寫尺寸與領取方式。</template>
    </p>

    <section v-for="g in groups ?? []" :key="g.membershipId" class="mc-jersey-group">
      <h3>{{ g.clubName }}{{ isEn ? ' · ' : '　' }}{{ g.seasonCode }}{{ tx(' 球季', ' season') }}</h3>
      <p class="mc-note">{{ tx(`方案含 ${g.quota} 件，已登記 ${g.used} 件。`, `Your plan includes ${g.quota} ${g.quota === 1 ? 'jersey' : 'jerseys'}; ${g.used} registered.`) }}</p>

      <ul v-if="g.items.length" class="mc-jersey-list">
        <li v-for="j in g.items" :key="j.id" class="mc-jersey">
          <dl class="mc-dl">
            <div><dt>{{ tx('穿著人／收件人', 'Wearer / recipient') }}</dt><dd>{{ j.recipientName }}</dd></div>
            <div><dt>{{ tx('尺寸', 'Size') }}</dt><dd>{{ j.size ?? '—' }}</dd></div>
            <div><dt>{{ tx('領取方式', 'Collection method') }}</dt><dd>{{ j.deliveryMethodLabel ?? '—' }}</dd></div>
            <div v-if="j.address"><dt>{{ tx('地址', 'Address') }}</dt><dd>{{ j.address }}</dd></div>
            <div><dt>{{ tx('發放狀態', 'Status') }}</dt><dd><span class="mc-badge">{{ j.statusLabel }}</span></dd></div>
            <div v-if="j.shippedOn"><dt>{{ tx('寄出日', 'Shipped on') }}</dt><dd>{{ formatPlainDate(j.shippedOn) }}</dd></div>
            <div v-if="j.receivedOn"><dt>{{ tx('領取日', 'Collected on') }}</dt><dd>{{ formatPlainDate(j.receivedOn) }}</dd></div>
          </dl>
          <button v-if="j.editable" type="button" class="mc-link" @click="startEdit(g, j)">{{ tx('修改', 'Edit') }}</button>
          <p v-else class="mc-note mc-note--small">{{ tx('已進入發放流程，無法再修改；如需更正請聯繫客服。', 'This jersey is already being processed and can no longer be edited. To correct it, please contact customer support.') }}</p>
        </li>
      </ul>

      <button v-if="g.canRegister && !draft" type="button" class="btn btn--dark btn--sm" @click="startNew(g)">{{ tx('登記一件球衣', 'Register a jersey') }}</button>
      <p v-else-if="!g.canRegister && g.used >= g.quota && g.quota > 0" class="mc-note mc-note--small">{{ tx('已達方案件數上限。', 'You have reached the number of jerseys included in your plan.') }}</p>
    </section>

    <form v-if="draft" class="tcrfc-form mc-jersey-form" novalidate @submit.prevent="save">
      <fieldset>
        <legend>{{ draft.id ? tx('修改球衣登記', 'Edit jersey registration') : tx('登記球衣', 'Register a jersey') }}</legend>
        <div class="form-grid">
          <div class="form-field">
            <label for="mj-name">{{ tx('穿著人／收件人姓名', 'Wearer / recipient name') }}<span class="req" aria-hidden="true">*</span></label>
            <input id="mj-name" v-model="draft.recipientName" type="text" maxlength="60" required>
          </div>
          <div class="form-field">
            <label for="mj-size">{{ tx('尺寸', 'Size') }}<span class="req" aria-hidden="true">*</span></label>
            <input id="mj-size" v-model="draft.size" type="text" maxlength="16" required aria-describedby="mj-size-hint">
            <p id="mj-size-hint" class="field-hint">{{ tx('請填寫尺寸（例如 M、L、童 140）。', 'Please enter a size (for example M, L, or Kids 140).') }}</p>
          </div>
          <div class="form-field form-field--full">
            <span id="mj-method" class="mc-legend">{{ tx('領取方式', 'Collection method') }}<span class="req" aria-hidden="true">*</span></span>
            <div class="mc-radios" role="radiogroup" aria-labelledby="mj-method">
              <label class="mc-radio"><input v-model="draft.deliveryMethod" type="radio" value="pickup"> {{ tx('到場領取', 'Collect in person') }}</label>
              <label class="mc-radio"><input v-model="draft.deliveryMethod" type="radio" value="ship"> {{ tx('寄送', 'Delivery') }}</label>
            </div>
          </div>
          <template v-if="draft.deliveryMethod === 'ship'">
            <div class="form-field">
              <label for="mj-phone">{{ tx('收件電話', 'Recipient phone') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="mj-phone" v-model="draft.phone" type="tel" maxlength="30" autocomplete="tel">
            </div>
            <div class="form-field form-field--full">
              <label for="mj-address">{{ tx('收件地址', 'Delivery address') }}<span class="req" aria-hidden="true">*</span></label>
              <input id="mj-address" v-model="draft.address" type="text" maxlength="200" autocomplete="street-address">
            </div>
          </template>
        </div>
      </fieldset>
      <p v-if="error" class="mc-alert mc-alert--error" role="alert">{{ error }}</p>
      <div class="mc-actions">
        <button type="submit" class="btn btn--primary btn--sm" :disabled="busy">{{ busy ? tx('送出中…', 'Submitting…') : tx('儲存', 'Save') }}</button>
        <button type="button" class="btn btn--dark btn--sm" :disabled="busy" @click="draft = null">{{ tx('取消', 'Cancel') }}</button>
      </div>
    </form>
    <p v-if="done" class="mc-alert mc-alert--ok" role="status">{{ done }}</p>
  </div>
</template>
