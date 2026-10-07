<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NAV_GROUPS } from '@/data/nav'
import type { NavGroup } from '@/types/nav'
import { authUser } from '@/auth/session'
import { useProgramPermissions } from '@/composables/useProgramPermissions'
import { useFormsPermissions } from '@/composables/useFormsPermissions'
import { useCalendarPermissions } from '@/composables/useCalendarPermissions'
import { useCrudPermissions, usePermission, useViewUpdatePermissions } from '@/composables/useCrudPermissions'
import { useClubFeatures } from '@/composables/useClubFeatures'
import { activeClubId } from '@/auth/clubAccess'

const props = defineProps<{
  collapse: boolean
}>()

const emit = defineEmits<{
  (e: 'navigate'): void
}>()

const route = useRoute()
const router = useRouter()

/**
 * `J 系統管理`、`H 搜尋與 AI 能見度` 整組只有系統管理員看得到（`I 網站設定` 自 H 批起改依子項目權限碼顯示，見下方 `CHILD_VISIBILITY` 的 I1–I6）（`J`：規劃書 §6
 * 權限矩陣「系統」欄只有系統管理員打勾；`H`：`seo.setting.*`／`seo.redirect.*`／`seo.report.view`／
 * `seo.llms.*`／`seo.crawler.*`／`seo.schema.view` 六段權限碼全部 `sysadmin_only=1`，見
 * apps/api/README.md「S1-12」「S1-12a」「S1-12b」「S1-12c」各節「權限碼」；`I`
 * **不在此列（H 批起）**：`site.fact.*`／`site.global.*`／`site.locale.*`／`site.venue.*`／
 * `site.edm.*` 雖皆為 `sysadmin_only=1`，但字串翻譯表 `site.string.*` 開放翻譯人員，所以 `I` 改成
 * 依子項目權限碼顯示（`CHILD_VISIBILITY` 的 I1–I6），翻譯人員只會看到「多語系」）。
 * 這裡只是選單可見度，不是安全邊界——真正的把關在後端每一個 `sysadmin_only` 權限碼與
 * `router/index.ts` 的第二層路由守衛。
 */
const SYSADMIN_ONLY_MODULE_CODES = new Set(['J', 'H'])

/**
 * P1／P2／P3（課程與活動）：不是每個角色都看得到，見 `useProgramPermissions` 檔頭的完整角色
 * 對照表。`customer_service_admin` 只看得到「報名」；`pr_media`／`translator` 三個都看不到
 * ——這兩個角色一旦把 P1／P2／P3 都濾掉，`children` 會變成空陣列，下面的 `.filter` 會連「課程
 * 與活動」這個父層一併拿掉，不會留下一個點進去卻沒有任何子項目的空選單。
 */
const programPermissions = useProgramPermissions()
const formsPermissions = useFormsPermissions()
const calendarPermissions = useCalendarPermissions()
const partnerPerm = useCrudPermissions('business.partner')
const sponsorPerm = useCrudPermissions('business.sponsor')
const proposalPerm = useCrudPermissions('business.proposal')
const leadPerm = useViewUpdatePermissions('business.lead')
const charityPerm = useCrudPermissions('charity.content')
const pressPerm = useCrudPermissions('content.press')
const achievementPerm = useCrudPermissions('team.achievement')
const milestonePerm = useCrudPermissions('team.milestone')
const trialPerm = useCrudPermissions('program.trial')
const memberAccountPerm = useCrudPermissions('member.account')
const membershipPerm = useCrudPermissions('member.membership')
const planPerm = useCrudPermissions('member.plan')
const memberSettingPerm = useViewUpdatePermissions('member.setting')
const jerseyPerm = useCrudPermissions('member.jersey')
const storePerm = useCrudPermissions('member.store')
const benefitPerm = useCrudPermissions('member.benefit')
const calendarSettingPerm = useViewUpdatePermissions('calendar.setting')
const calendarSubscriptionView = usePermission('calendar.subscription.view')
const calendarExport = usePermission('calendar.export')
const comicPerm = useCrudPermissions('culture.comic')
const fanEventPerm = useCrudPermissions('culture.fan_event')
const shopProductPerm = useCrudPermissions('shop.product')
const shopCollectionPerm = useCrudPermissions('shop.collection')
const shopInventoryPerm = useViewUpdatePermissions('shop.inventory')
const shopOrderPerm = useCrudPermissions('shop.order')
const shopShipmentPerm = useViewUpdatePermissions('shop.shipment')
const shopRefundPerm = useViewUpdatePermissions('shop.refund')
const shopSettingPerm = useViewUpdatePermissions('shop.setting')
const shopReportView = usePermission('shop.report.view')
const shopCredentialView = usePermission('shop.credential.view')
const shopDonationCodePerm = useCrudPermissions('shop.donation_code')
const drawPerm = useViewUpdatePermissions('member.draw')
const drawAnnounce = usePermission('member.draw.announce')
// D 批：電子報、App 廣告、行動 App 後台各模組——沒有對應權限碼的角色不顯示（例如合作球隊管理沒有任何廣告與 App 權限）
const newsletterView = usePermission('form.newsletter.view')
const adAdvertiserView = usePermission('ad.advertiser.view')
const adSlotView = usePermission('ad.slot.view')
const adCampaignView = usePermission('ad.campaign.view')
const adReportView = usePermission('ad.report.view')
const appReleaseView = usePermission('app.release.view')
const appLayoutView = usePermission('app.layout.view')
const appPushView = usePermission('app.push.view')
const appDeviceView = usePermission('app.device.view')
const appConfigView = usePermission('app.config.view')
const appCredentialView = usePermission('app.credential.view')
const appDiagnosticView = usePermission('app.diagnostic.view')
// I 網站設定（H 批）：子項目各看各的權限碼
const siteFactView = usePermission('site.fact.view')
const siteGlobalView = usePermission('site.global.view')
const siteLocaleView = usePermission('site.locale.view')
const siteStringView = usePermission('site.string.view')
const siteStringTranslate = usePermission('site.string.translate')
const siteVenueView = usePermission('site.venue.view')
const siteEdmView = usePermission('site.edm.view')
// B 內容管理、C 球隊與賽事：先前沒有登記可見度，唯讀等角色會看到整組點進去全是 403 的選單
const pageView = usePermission('content.page.view')
const articleView = usePermission('content.article.view')
const homeSectionView = usePermission('content.home_section.view')
const bannerView = usePermission('content.banner.view')
const faqView = usePermission('content.faq.view')
const faqCategoryView = usePermission('content.faq_category.view')
const teamView = usePermission('team.team.view')
const playerView = usePermission('team.player.view')
const staffView = usePermission('team.staff.view')
const matchView = usePermission('team.match.view')
const standingView = usePermission('team.standing.view')
const competitionView = usePermission('team.competition.view')
const { comicAvailable } = useClubFeatures()
/** 同一個模組代號底下有多個頁面（C4 賽程／積分榜／賽事系列）時，依路徑各看各的權限。 */
const PATH_VISIBILITY: Record<string, () => boolean> = {
  '/teams/standings': () => standingView.value,
  '/teams/competitions': () => competitionView.value,
  // 賽季清單後端接受 team.match.view 或 team.competition.view 任一
  '/teams/seasons': () => matchView.value || competitionView.value,
}
const CHILD_VISIBILITY: Record<string, () => boolean> = {
  B1: () => pageView.value,
  B2: () => articleView.value,
  B3: () => homeSectionView.value || bannerView.value,
  B4: () => faqView.value || faqCategoryView.value,
  C1: () => teamView.value,
  C2: () => playerView.value,
  C3: () => staffView.value,
  C4: () => matchView.value,
  I1: () => siteFactView.value,
  I3: () => siteGlobalView.value,
  I4: () => siteLocaleView.value || siteStringView.value || siteStringTranslate.value,
  I5: () => siteVenueView.value,
  I6: () => siteEdmView.value,
  // 藍鯨不設漫畫：切到藍鯨時側欄不顯示（後端也會回 403）
  F1: () => comicAvailable.value && comicPerm.canView.value,
  F2: () => fanEventPerm.canView.value,
  G3: () => newsletterView.value,
  E4: () => adAdvertiserView.value || adSlotView.value,
  E5: () => adCampaignView.value,
  E6: () => adReportView.value,
  M1: () => appReleaseView.value,
  M2: () => appLayoutView.value,
  M3: () => appPushView.value,
  M4: () => appDeviceView.value,
  M5: () => appConfigView.value || appCredentialView.value || appDiagnosticView.value,
  S1: () => shopProductPerm.canView.value || shopCollectionPerm.canView.value,
  S2: () => shopInventoryPerm.canView.value,
  S3: () => shopOrderPerm.canView.value,
  S4: () => shopShipmentPerm.canView.value,
  S5: () => shopRefundPerm.canView.value,
  S6: () => shopSettingPerm.canView.value || shopReportView.value || shopCredentialView.value || shopDonationCodePerm.canView.value,
  K5: () => drawPerm.canView.value || drawAnnounce.value,
  P4: () => trialPerm.canView.value,
  K1: () => memberAccountPerm.canView.value,
  K2: () => membershipPerm.canView.value || planPerm.canView.value || memberSettingPerm.canView.value,
  K3: () => jerseyPerm.canView.value,
  K4: () => storePerm.canView.value || benefitPerm.canView.value,
  L3: () => calendarSettingPerm.canView.value,
  L4: () => calendarSubscriptionView.value || calendarExport.value,
  P1: () => programPermissions.canViewItems.value,
  P2: () => programPermissions.canViewItems.value,
  P3: () => programPermissions.canViewRegistrations.value,
  G1: () => formsPermissions.canViewForms.value,
  G2: () => formsPermissions.canViewInbox.value,
  L1: () => calendarPermissions.canViewOverview.value,
  L2: () => calendarPermissions.canViewCustomEvents.value,
  E1: () => partnerPerm.canView.value,
  E2: () => sponsorPerm.canView.value,
  E3: () => proposalPerm.canView.value || leadPerm.canView.value,
  B5: () => charityPerm.canView.value,
  B6: () => pressPerm.canView.value,
  C5: () => achievementPerm.canView.value || milestonePerm.canView.value,
}

const visibleGroups = computed<NavGroup[]>(() => {
  // 依賴目前站台，切換俱樂部時重新計算（藍鯨隱藏漫畫）
  void activeClubId.value
  const isSuperAdmin = authUser.value?.isSuperAdmin ?? false
  return NAV_GROUPS.map((group) => ({
    ...group,
    modules: group.modules
      .filter((mod) => isSuperAdmin || !SYSADMIN_ONLY_MODULE_CODES.has(mod.code))
      .map((mod) => {
        if (!mod.children) return mod
        const children = mod.children.filter((child) => {
          if (child.hidden) return false
          const check = PATH_VISIBILITY[child.path] ?? CHILD_VISIBILITY[child.code]
          return isSuperAdmin || !check || check()
        })
        return { ...mod, children }
      })
      .filter((mod) => !mod.children || mod.children.length > 0),
  })).filter((group) => group.modules.length > 0)
})

function handleSelect(path: string) {
  if (route.path !== path) router.push(path)
  emit('navigate')
}
</script>

<template>
  <div class="app-sidebar">
    <el-menu
      :default-active="route.path"
      :collapse="props.collapse"
      :collapse-transition="false"
      unique-opened
      class="app-sidebar__menu"
      @select="handleSelect"
    >
      <template v-for="group in visibleGroups" :key="group.groupLabel">
        <el-menu-item-group :title="props.collapse ? undefined : group.groupLabel">
          <template v-for="mod in group.modules" :key="mod.code">
            <el-sub-menu v-if="mod.children" :index="mod.code">
              <template #title>
                <el-icon><Folder /></el-icon>
                <span>{{ mod.label }}</span>
              </template>
              <el-menu-item v-for="child in mod.children" :key="child.path" :index="child.path">
                {{ child.label }}
              </el-menu-item>
            </el-sub-menu>
            <el-menu-item v-else :index="mod.path">
              <el-icon><Odometer /></el-icon>
              <template #title>{{ mod.label }}</template>
            </el-menu-item>
          </template>
        </el-menu-item-group>
      </template>
    </el-menu>
  </div>
</template>

<style scoped>
.app-sidebar {
  height: 100%;
  overflow-y: auto;
  overflow-x: hidden;
  background: var(--admin-bg-surface);
}

.app-sidebar__menu {
  /* 選中態改用左側 accent bar＋色階，不用大面積填色塊（docs/21 §1.3：長時間盯著一大塊高飽和藍
     比淺色底更容易視覺疲勞，一條 accent bar 加粗體文字就足夠標示「你在這裡」） */
  --el-menu-bg-color: var(--admin-bg-surface);
  --el-menu-text-color: var(--admin-text-secondary);
  --el-menu-hover-bg-color: var(--admin-bg-surface-2);
  --el-menu-hover-text-color: var(--admin-text-primary);
  --el-menu-active-color: var(--admin-primary);
  border-right: none;
  height: 100%;
}

.app-sidebar__menu:not(.el-menu--collapse) {
  width: var(--admin-sidebar-width-expanded);
}

/* 一般 hover：只變色階，不介入色相（§1.3） */
.app-sidebar__menu :deep(.el-menu-item:hover),
.app-sidebar__menu :deep(.el-sub-menu__title:hover) {
  background-color: var(--admin-bg-surface-2);
}

/* 選中（當前路由）：左側 3px accent bar ＋ surface-2 底 ＋ primary 粗體文字 */
.app-sidebar__menu :deep(.el-menu-item.is-active) {
  background-color: var(--admin-bg-surface-2);
  color: var(--admin-primary);
  font-weight: 600;
  box-shadow: inset 3px 0 0 0 var(--admin-primary);
}

.app-sidebar__menu :deep(.el-menu-item-group__title) {
  color: var(--admin-text-tertiary);
}
</style>
