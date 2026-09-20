# apps/api — api（單一 .NET 行程，雙 DbContext）

🔴 **本目錄目前是空的。** 待 `backend-engineer` 在此建立 .NET 專案。

## 這個目錄之後要放什麼

- 標準 .NET 專案結構（`*.csproj`、`Program.cs`、`Migrations/`…）。
- **單一 instance，持有兩個 `DbContext`**（俱樂部庫 `sqldb-club`、慈善庫 `sqldb-charity`），
  見 [`docs/17-deployment.md`](../../docs/17-deployment.md) §0／§5。
- 🔴 **慈善的 `DbContext` 不得註冊任何俱樂部實體型別**——這是維持「不得跨庫 join」邊界的補償措施之一。
- 兩個健康檢查端點：`/readyz`（真的檢查兩個 `DbContext` 能連線；Redis 連線失敗算警告不算失敗）與
  一般存活探針，見 [`docs/20-cicd.md`](../../docs/20-cicd.md) §6。
- **EF Core Migrations**：建庫（`db/club-schema.sql`／`db/charity-schema.sql`）之後的結構變更一律用 migration，
  第一次建立本專案時要對著已建好的資料庫跑 `dotnet ef dbcontext scaffold` 並建立 `InitialBaseline` 基準 migration
  （[`docs/20-cicd.md`](../../docs/20-cicd.md) §5，這是一次性 handoff，交給 `backend-engineer` 做）。
- NuGet 相依鎖定：需開 `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`（`--use-lock-file`），
  CI 的 NuGet 快取依 `packages.lock.json`。

## 相關文件

- [`docs/12-database-schema.md`](../../docs/12-database-schema.md)／[`12a`](../../docs/12a-database-erd.md)／[`12b`](../../docs/12b-database-tables.md) — 資料表設計
- [`docs/17-deployment.md`](../../docs/17-deployment.md) §4／§6 — 快取策略、DBMS 連帶決定（主鍵策略、型別對照）
- [`docs/20-cicd.md`](../../docs/20-cicd.md) §5 — 資料庫遷移關卡
