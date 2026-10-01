#!/usr/bin/env bash
# infra/bootstrap.sh — 第一次部署前的一次性設定（infra/README.md §3 步驟 0–5 的合併版）
#
# 由訂閱 Owner 在自己的終端機執行：  bash infra/bootstrap.sh
# - 可重跑：每一步先檢查是否已存在，已存在就略過，中途失敗修好後直接再跑一次
# - 機密（SQL 密碼、SSH 來源 IP、告警 Email）只在執行時輸入並直接寫進 GitHub secret，
#   不落地、不進版控、不顯示在畫面上
# - 只建資源群組與部署身分，不建 VM／SQL 等計費資源（那些由 infra.yml 部署）
# - Fork PR 核准設定（README §3 步驟 5a）沒有穩定的 API，結尾會提示到網頁上設
set -euo pipefail

RG=rg-tcrfc-prod
LOCATION=japaneast
IDENTITY=id-tcrfc-deploy
FED_NAME=github-production
LOCK_ROLE="TCRFC Lock Manager"
REPO=waiting0201/tcrfc
ENV_NAME=production

step() { printf '\n== %s ==\n' "$1"; }
skip() { printf '   （%s，略過）\n' "$1"; }

# 重試直到成功（角色定義與身分在 Entra 的同步有延遲，固定 sleep 不可靠）
retry() {
  local tries=$1; shift
  for ((i = 1; i <= tries; i++)); do
    "$@" && return 0
    printf '   第 %d/%d 次失敗，15 秒後重試…\n' "${i}" "${tries}"; sleep 15
  done
  return 1
}

step "前置檢查"
command -v az >/dev/null || { echo "找不到 az CLI"; exit 1; }
command -v gh >/dev/null || { echo "找不到 gh CLI"; exit 1; }
gh auth status >/dev/null 2>&1 || { echo "請先 gh auth login"; exit 1; }
az account show >/dev/null 2>&1 || { echo "請先 az login"; exit 1; }

az account show --query '{subscription:name, subscriptionId:id, user:user.name}' -o table
read -rp "要部署到這個訂閱嗎？(y/N) " ok
[[ "${ok}" == [yY] ]] || { echo "請先 az account set --subscription <ID> 再重跑"; exit 1; }

SUB=$(az account show --query id -o tsv)
TENANT=$(az account show --query tenantId -o tsv)
SCOPE="/subscriptions/${SUB}/resourceGroups/${RG}"

step "步驟 0：SSH 金鑰"
if [[ -f ~/.ssh/id_ed25519.pub ]]; then
  skip "${HOME}/.ssh/id_ed25519 已存在"
else
  mkdir -p ~/.ssh && chmod 700 ~/.ssh
  ssh-keygen -t ed25519 -C "tcrfc-vm" -f ~/.ssh/id_ed25519
fi

step "步驟 1：註冊資源提供者"
failed=()
for ns in Microsoft.Network Microsoft.Compute Microsoft.Sql Microsoft.Storage \
          Microsoft.Insights Microsoft.Consumption Microsoft.ManagedIdentity; do
  state=$(az provider show -n "${ns}" --query registrationState -o tsv 2>/dev/null || echo Unknown)
  if [[ "${state}" == Registered ]]; then
    skip "${ns} 已註冊"
  elif az provider register -n "${ns}" --wait -o none; then
    echo "   ${ns} 已註冊"
  else
    failed+=("${ns}")
  fi
done
if ((${#failed[@]})); then
  echo "⚠️  以下提供者註冊失敗：${failed[*]}"
  echo "    CSP 訂閱常見於 Microsoft.Consumption（預算）——不擋後續步驟，但部署到預算那一步可能失敗。"
fi

step "步驟 2：資源群組 ${RG}"
if [[ $(az group exists -n "${RG}") == true ]]; then
  skip "已存在"
else
  az group create -n "${RG}" -l "${LOCATION}" --tags project=tcrfc env=prod -o none
fi

step "步驟 3：部署身分 ${IDENTITY} ＋ federated credential"
if az identity show -g "${RG}" -n "${IDENTITY}" -o none 2>/dev/null; then
  skip "身分已存在"
else
  az identity create -g "${RG}" -n "${IDENTITY}" -l "${LOCATION}" -o none
fi
CLIENT_ID=$(az identity show -g "${RG}" -n "${IDENTITY}" --query clientId -o tsv)
PRINCIPAL_ID=$(az identity show -g "${RG}" -n "${IDENTITY}" --query principalId -o tsv)

# subject 前綴向 GitHub 查，不自己拼：repo 啟用「不可變 subject」時格式是
# repo:<owner>@<owner_id>/<repo>@<repo_id>，與舊的 repo:<owner>/<repo> 不同，拼錯 Azure 登入會回 AADSTS700213
SUB_PREFIX=$(gh api "repos/${REPO}/actions/oidc/customization/sub" --jq '.sub_claim_prefix // empty' 2>/dev/null || true)
[[ -n "${SUB_PREFIX}" ]] || SUB_PREFIX="repo:${REPO}"
FED_SUBJECT="${SUB_PREFIX}:environment:${ENV_NAME}"
echo "   subject：${FED_SUBJECT}"

current=$(az identity federated-credential show -g "${RG}" --identity-name "${IDENTITY}" -n "${FED_NAME}" --query subject -o tsv 2>/dev/null || true)
if [[ "${current}" == "${FED_SUBJECT}" ]]; then
  skip "federated credential 已存在且 subject 正確"
elif [[ -n "${current}" ]]; then
  echo "   既有 subject 不符（${current}），更新"
  az identity federated-credential update -g "${RG}" --identity-name "${IDENTITY}" -n "${FED_NAME}" \
    --issuer https://token.actions.githubusercontent.com \
    --subject "${FED_SUBJECT}" \
    --audiences api://AzureADTokenExchange -o none
else
  az identity federated-credential create -g "${RG}" --identity-name "${IDENTITY}" -n "${FED_NAME}" \
    --issuer https://token.actions.githubusercontent.com \
    --subject "${FED_SUBJECT}" \
    --audiences api://AzureADTokenExchange -o none
fi

step "步驟 4：授權（範圍僅 ${RG}）"
assign() {
  local role=$1
  if [[ -n $(az role assignment list --assignee "${PRINCIPAL_ID}" --role "${role}" --scope "${SCOPE}" --query '[0].id' -o tsv 2>/dev/null) ]]; then
    skip "「${role}」已指派"
  else
    retry 8 az role assignment create --assignee-object-id "${PRINCIPAL_ID}" \
      --assignee-principal-type ServicePrincipal --role "${role}" --scope "${SCOPE}" -o none
    echo "   已指派「${role}」"
  fi
}
assign Contributor

if [[ -n $(az role definition list --custom-role-only true --name "${LOCK_ROLE}" --query '[0].id' -o tsv) ]]; then
  skip "自訂角色「${LOCK_ROLE}」已存在"
else
  ROLE_JSON=$(mktemp)
  trap 'rm -f "${ROLE_JSON}"' EXIT
  cat > "${ROLE_JSON}" <<JSON
{ "Name": "${LOCK_ROLE}", "IsCustom": true,
  "Description": "只能讀寫 resource lock，供 infra 部署管線建立 CanNotDelete 鎖。",
  "Actions": ["Microsoft.Authorization/locks/*"], "NotActions": [],
  "AssignableScopes": ["${SCOPE}"] }
JSON
  az role definition create --role-definition "${ROLE_JSON}" -o none
fi
assign "${LOCK_ROLE}"

step "步驟 5b：GitHub 環境 ${ENV_NAME} 只允許 master（先設限制，再放 secrets）"
gh api -X PUT "repos/${REPO}/environments/${ENV_NAME}" --input - >/dev/null <<'JSON'
{"deployment_branch_policy":{"protected_branches":false,"custom_branch_policies":true}}
JSON
if gh api "repos/${REPO}/environments/${ENV_NAME}/deployment-branch-policies" \
     --jq '.branch_policies[].name' | grep -qx master; then
  skip "master 規則已存在"
else
  gh api -X POST "repos/${REPO}/environments/${ENV_NAME}/deployment-branch-policies" \
    -f name=master -f type=branch >/dev/null
fi
policies=$(gh api "repos/${REPO}/environments/${ENV_NAME}/deployment-branch-policies" --jq '[.branch_policies[].name]|join(",")')
[[ "${policies}" == master ]] || { echo "🔴 分支限制不是只有 master（目前：${policies}），停止，不寫入 secrets"; exit 1; }
echo "   已確認：只有 master 能進入 ${ENV_NAME}"

step "步驟 5c：variables"
gh variable set AZURE_CLIENT_ID       --env "${ENV_NAME}" --repo "${REPO}" --body "${CLIENT_ID}"
gh variable set AZURE_TENANT_ID       --env "${ENV_NAME}" --repo "${REPO}" --body "${TENANT}"
gh variable set AZURE_SUBSCRIPTION_ID --env "${ENV_NAME}" --repo "${REPO}" --body "${SUB}"
gh variable set SSH_PUBLIC_KEY        --env "${ENV_NAME}" --repo "${REPO}" --body "$(cat ~/.ssh/id_ed25519.pub)"

step "步驟 5c：secrets（輸入內容不會顯示，也不會存到任何檔案）"
existing=$(gh secret list --env "${ENV_NAME}" --repo "${REPO}" --json name --jq '.[].name' || true)
want() {  # 已存在就問要不要覆寫
  grep -qx "$1" <<<"${existing}" || return 0
  local a; read -rp "   $1 已存在，要覆寫嗎？(y/N) " a; [[ "${a}" == [yY] ]]
}

if want SSH_ALLOWED_CIDR; then
  while :; do
    IFS= read -rp "   SSH 允許的來源 IPv4（例 1.2.3.4，不用加 /32）：" ip
    [[ "${ip}" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]] && break
    echo "   格式不對，重來"
  done
  gh secret set SSH_ALLOWED_CIDR --env "${ENV_NAME}" --repo "${REPO}" --body "${ip}/32"
  unset ip
fi

if want ALERT_EMAIL; then
  while :; do
    IFS= read -rp "   告警與預算通知 Email：" mail
    [[ "${mail}" =~ ^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$ ]] && break
    echo "   格式不對，重來"
  done
  gh secret set ALERT_EMAIL --env "${ENV_NAME}" --repo "${REPO}" --body "${mail}"
  unset mail
fi

if want SQL_ADMIN_PASSWORD; then
  echo "   SQL 管理員密碼：≥16 字元，大寫、小寫、數字、符號都要有。請同時存進密碼管理器（之後 api 連線字串要用）。"
  while :; do
    IFS= read -rsp "   密碼：" p1; echo
    IFS= read -rsp "   再輸入一次：" p2; echo
    if [[ "${p1}" != "${p2}" ]]; then echo "   兩次不一致"; continue; fi
    if (( ${#p1} < 16 )) || [[ ! "${p1}" =~ [A-Z] || ! "${p1}" =~ [a-z] || ! "${p1}" =~ [0-9] || ! "${p1}" =~ [^A-Za-z0-9] ]]; then
      echo "   強度不足"; continue
    fi
    break
  done
  printf '%s' "${p1}" | gh secret set SQL_ADMIN_PASSWORD --env "${ENV_NAME}" --repo "${REPO}"
  unset p1 p2
fi

step "完成"
echo "✅ Azure 端：${RG}、${IDENTITY}、federated credential、兩個角色指派"
echo "✅ GitHub 端：${ENV_NAME} 只允許 master、4 個 variables、3 個 secrets"
echo
echo "剩一項請到網頁設定（沒有穩定的 API）："
echo "   https://github.com/${REPO}/settings/actions"
echo "   → Fork pull request workflows from outside collaborators"
echo "   → Require approval for all outside collaborators"
echo
echo "⚠️  從現在起，push 到 master 且動到 infra/** 就會真的部署並開始計費。"
echo "    第一次請先手動跑 Infra Deploy 並勾「只跑 what-if」。"
