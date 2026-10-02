using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditHiddenAndSettlementLineKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔴 冪等：db/charity-schema.sql（2026-10-02 起）新建的庫已經有這一欄與這個索引，只有更早建好的本機庫缺它們；
            // 兩種庫跑 `dotnet ef database update` 都要成功。內容與 db/charity-schema.sql 逐項一致。
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.donations', N'is_credit_hidden') IS NULL
    ALTER TABLE dbo.donations ADD is_credit_hidden bit NOT NULL DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_settlement_lines_settlement_donation_kind' AND object_id = OBJECT_ID(N'dbo.settlement_lines'))
    CREATE UNIQUE INDEX UQ_settlement_lines_settlement_donation_kind ON dbo.settlement_lines (settlement_id, donation_id, is_clawback);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_settlement_lines_settlement_donation_kind' AND object_id = OBJECT_ID(N'dbo.settlement_lines'))
    DROP INDEX UQ_settlement_lines_settlement_donation_kind ON dbo.settlement_lines;");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.donations', N'is_credit_hidden') IS NOT NULL
BEGIN
    DECLARE @df sysname = (SELECT dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.donations') AND c.name = N'is_credit_hidden');
    IF @df IS NOT NULL EXEC(N'ALTER TABLE dbo.donations DROP CONSTRAINT ' + @df);
    ALTER TABLE dbo.donations DROP COLUMN is_credit_hidden;
END");
        }
    }
}
