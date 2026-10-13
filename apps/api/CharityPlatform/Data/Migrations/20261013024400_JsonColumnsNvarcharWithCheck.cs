using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <summary>
    /// 資料庫不再使用 Azure SQL 原生 <c>json</c> 型別（2026-10-13 使用者決定，docs/17 §6 第 2 項、docs/14）：
    /// 2 個 JSON 內容欄位一律 <c>nvarchar(max)</c> ＋ 具名 CHECK <c>CK_&lt;表&gt;_&lt;欄&gt;_json</c>（<c>ISJSON(欄, OBJECT|ARRAY) = 1</c>）。
    /// <para>
    /// <b>兩種庫都要正確（冪等、先查後做）</b>：
    /// ① 正式庫（<c>db/charity-schema.sql</c> 建、原生 json、Azure SQL）：欄位型別是 <c>json</c> → 新增暫存欄＋<c>CONVERT</c>複製＋刪舊欄＋改名為 <c>nvarchar(max) NULL</c>（SQL Server 不允許 json→nvarchar 的 <c>ALTER COLUMN</c>）（這些欄位在 DDL 皆為可空，保留 NULL），再加 CHECK；
    /// ② 本機 <c>tcrfc_charity</c>（SQL Server 2022，本來就是 nvarchar(max)）與<b>DDL 新建的庫</b>：型別已是 nvarchar → 轉型跳過；CHECK 已存在 → 全部跳過。
    /// EF 的冪等腳本把整支 migration 放在同一個批次，所以對欄位呼叫 <c>ISJSON</c> 的 ② 包成 <c>EXEC(N'…')</c>，到執行時才編譯（① 之後欄位型別才變）。
    /// </para>
    /// <para>
    /// <b>既有資料不合規時：中止整支 migration，不默默刪改、也不用 <c>WITH NOCHECK</c></b>。理由：
    /// ① <c>WITH NOCHECK</c> 會留下「不受信任」的約束（<c>is_not_trusted = 1</c>），髒資料永遠留在表裡、之後任何更新到該列又會被擋，問題只是延後；
    /// ② 默默刪改是改使用者資料；③ 失敗是安全的——EF 每支 migration 一個交易，整支回滾、什麼都沒套用，且先用 <c>THROW</c> 點名「哪張表哪個欄位」。
    /// 正式庫的欄位原本是原生 json（資料庫層已保證是物件或陣列），只有「限物件／限陣列」的欄位可能撞到另一種形狀；
    /// 本機 2022 於 2026-10-13 實測 0 筆不合規。遇到時由人判斷怎麼修資料，再重跑。
    /// </para>
    /// <para>
    /// <b>Down</b>：只移除 CHECK（若存在）。欄位型別<b>不</b>改回 <c>json</c>（本機 2022 沒有該型別，且決定就是不再使用）；
    /// 這是刻意的非對稱，與 <c>AdminUserTeamsDropContract</c> 同類的「只還原能還原的」慣例。
    /// SQL 與 DDL 的欄位宣告同源；新增 JSON 內容欄位時 DDL、<c>ClubDbContextJsonChecks</c>／<c>CharityDbContextCustomizations</c>、migration 三處一起改。
    /// </para>
    /// </summary>
    public partial class JsonColumnsNvarcharWithCheck : Migration
    {
        // (表, 欄, 允許的根節點)：O = 只物件、A = 只陣列、B = 物件或陣列。與 DDL 的 CHECK 逐字對應。
        private static readonly (string Table, string Column, char Kind)[] JsonColumns =
        {
            ("donation_payments", "raw_response", 'B'),
            ("donation_projects_i18n", "description", 'B')
        };

        private static string Condition(string column, char kind) => kind switch
        {
            'O' => $"ISJSON({column}, OBJECT) = 1",
            'A' => $"ISJSON({column}, ARRAY) = 1",
            _ => $"ISJSON({column}, OBJECT) = 1 OR ISJSON({column}, ARRAY) = 1",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, kind) in JsonColumns)
            {
                var ck = $"CK_{table}_{column}_json";
                var cond = Condition(column, kind);

                // ① 原生 json → nvarchar(max)（沒有原生 json 型別的庫上 sys.types 查不到 'json'，條件為假，無操作）。
                // 🔴 不能 `ALTER COLUMN … nvarchar(max)`：SQL Server 不允許 json → nvarchar 的隱含轉換
                // （Msg 257「Implicit conversion from data type json to nvarchar(max) is not allowed」，2026-10-13 在 SQL Server 2025 實測），
                // 所以改為：新增暫存欄 → 以 CONVERT 複製 → 刪舊欄 → 改名。12 欄皆無索引／約束／預設值依附（DDL 沒有），可直接 DROP COLUMN。
                // 代價：欄位在資料表中的順序移到最後（只影響 SELECT * 的欄序與 DDL 新建庫的欄序不同，EF 與 schema-drift 都不看欄序）。
                // 整段在 EF 的交易內，任何一步失敗整支回滾；UPDATE 用 EXEC 延後編譯（暫存欄在批次開始時還不存在）。
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'{table}') AND c.name = N'{column}' AND ty.name = N'json')
BEGIN
    ALTER TABLE [{table}] ADD [{column}__nv] nvarchar(max) NULL;
    EXEC(N'UPDATE [{table}] SET [{column}__nv] = CONVERT(nvarchar(max), [{column}]) WHERE [{column}] IS NOT NULL');
    ALTER TABLE [{table}] DROP COLUMN [{column}];
    EXEC sp_rename N'{table}.{column}__nv', N'{column}', N'COLUMN';
END");

                // ② 先擋不合規資料（明確錯誤訊息），再加 CHECK（WITH CHECK＝驗證既有資料、約束受信任）。
                // 包成 EXEC：EF 的冪等腳本把整支 migration 放在同一個批次，①把欄位從 json 改成 nvarchar 之後，
                // 這段對該欄位呼叫 ISJSON 的陳述式必須在「執行時」才編譯（批次一開始編譯時欄位還是 json 型別）。
                var check = $@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'{table}') AND name = N'{ck}')
BEGIN
    IF EXISTS (SELECT 1 FROM [{table}] WHERE [{column}] IS NOT NULL AND NOT ({cond}))
        THROW 50001, N'{table}.{column} 含不是合法 JSON {(kind == 'O' ? "物件" : kind == 'A' ? "陣列" : "物件／陣列")}的資料，無法加上 {ck}。請先人工修正資料再重跑（不自動刪改）。', 1;
    ALTER TABLE [{table}] WITH CHECK ADD CONSTRAINT [{ck}] CHECK ({cond});
END";
                migrationBuilder.Sql($"EXEC(N'{check.Replace("'", "''")}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column, _) in JsonColumns)
            {
                var ck = $"CK_{table}_{column}_json";
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'{table}') AND name = N'{ck}')
    ALTER TABLE [{table}] DROP CONSTRAINT [{ck}];");
            }
        }
    }
}
