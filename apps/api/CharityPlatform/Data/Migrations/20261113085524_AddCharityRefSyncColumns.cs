using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <summary>
    /// K-2（2026-11-13）：公益團體與慈善計畫唯讀複本加上「從官網更新清單」需要的欄位——主站 id 對照鍵 <c>external_id</c>、
    /// 英文名稱 <c>name_en</c>、啟用狀態 <c>is_active</c>／<c>deactivated_at</c>、<c>last_synced_at</c>。
    /// <para><b>手寫 SQL 取代 EF 產出的 AddColumn</b>：EF 的 AddColumn 會建立<b>未命名</b>的預設值約束（<c>DF__表__欄__隨機碼</c>），
    /// 與 <c>db/charity-schema.sql</c> 的 <c>DF_&lt;表&gt;_&lt;欄&gt;</c> 分岔（E-322 結構要完全一樣）；欄位順序也要與 DDL 一致（附加在表尾）。
    /// Designer 與 snapshot 仍是 EF 產生的，模型描述不變。</para>
    /// <para><b>可重跑</b>：每一步先檢查欄位／索引是否已存在（以 DDL 建庫的環境，baseline 會把本 migration 標為已套用，不會執行；
    /// 但手動補跑時不會重複報錯）。用到新欄位的 CREATE INDEX 包在 EXEC 內（見該處說明）。</para>
    /// <para><b>Down</b>：不還原（會丟失已同步的英文名稱與停用狀態；展開—收縮慣例，欄位為可空或有預設值，舊程式不受影響）。</para>
    /// </summary>
    public partial class AddCharityRefSyncColumns : Migration
    {
        private static readonly string[] Tables = ["charity_refs", "charity_program_refs"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH(N'{table}', N'external_id') IS NULL
    ALTER TABLE {table} ADD external_id uniqueidentifier NULL;
IF COL_LENGTH(N'{table}', N'name_en') IS NULL
    ALTER TABLE {table} ADD name_en nvarchar(128) NULL;
IF COL_LENGTH(N'{table}', N'is_active') IS NULL
    ALTER TABLE {table} ADD is_active bit NOT NULL CONSTRAINT DF_{table}_is_active DEFAULT 1;
IF COL_LENGTH(N'{table}', N'deactivated_at') IS NULL
    ALTER TABLE {table} ADD deactivated_at datetime2(3) NULL;
IF COL_LENGTH(N'{table}', N'last_synced_at') IS NULL
    ALTER TABLE {table} ADD last_synced_at datetime2(3) NULL;");

                // 篩選唯一索引：僅非空的 external_id 唯一（同步機制之前的列為空，彼此不衝突）。
                // 🔴 包 EXEC：EF 產生的 sql 腳本（含 --idempotent）不會在每個 Sql() 之間放 GO，同一批次裡 ALTER ADD 之後直接
                // CREATE INDEX 會在編譯階段報「Invalid column name 'external_id'」（Msg 207，已實測）；EXEC 讓它延後到執行階段才綁定欄位。
                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'{table}') AND name = N'UX_{table}_external_id')
    EXEC(N'CREATE UNIQUE NONCLUSTERED INDEX UX_{table}_external_id ON {table} (external_id) WHERE external_id IS NOT NULL');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意不還原（見類別說明）。
        }
    }
}
