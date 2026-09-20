# apps/ — 六個容器、五個應用專案

> 對應 [`docs/17-deployment.md`](../docs/17-deployment.md) §1（八個容器）與
> [`docs/20-cicd.md`](../docs/20-cicd.md) §3（五個映像檔）。
> 🔴 **本目錄下目前沒有任何應用程式碼**——S0-7a 只建骨架（Dockerfile、目錄結構），
> 專案本體（`package.json`／`nuxt.config.ts`／`*.csproj`…）待對應 agent 之後建立。

| 目錄 | 映像檔名稱 | 對應容器 | 技術棧 | 誰建立 |
|---|---|---|---|---|
| [`web/`](web/) | `nuxt-club` | `nuxt-tcrfc`、`nuxt-bw`（**同一個映像檔，兩個容器**） | Nuxt 4 SSR（Vue 3） | `frontend-architect` |
| [`web-charity/`](web-charity/) | `nuxt-charity` | `nuxt-charity` | Nuxt 4 SSR（Vue 3） | `frontend-architect` |
| [`admin/`](admin/) | `admin-web` | `admin-web` | Vue 3 SPA | `frontend-architect` |
| [`admin-charity/`](admin-charity/) | `admin-charity` | `admin-charity` | Vue 3 SPA | `frontend-architect` |
| [`api/`](api/) | `api` | `api` | .NET／C#，EF Core ＋ Dapper，兩個 `DbContext` | `backend-engineer` |

**命名說明**：[`docs/20-cicd.md`](../docs/20-cicd.md) §3 原本示意的路徑是 `apps/nuxt-club`／`apps/admin-web`，
該節明文「實際建立專案骨架時可調整」。S0-7a 實際採用上表這組更短的名稱（`web`／`web-charity`／`admin`／`admin-charity`／`api`），
`docs/20` §3 的 `paths-filter` 對照表已同步改寫，**這是本次任務的執行層決定，不是規格變更**。

`proxy`（Caddy）與 `redis` 沒有對應的 `apps/` 目錄——它們是官方映像檔 ＋ 設定檔（[`../deploy/Caddyfile`](../deploy/Caddyfile)），不是自建應用。
