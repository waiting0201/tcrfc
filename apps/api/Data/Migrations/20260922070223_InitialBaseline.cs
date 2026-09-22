using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔴 基準 migration（docs/20-cicd.md §5）：資料庫已用 db/club-schema.sql 手動建好，
            // 這裡刻意留空，不重跑 DDL——dotnet ef database update 只會在 __EFMigrationsHistory
            // 插入這一筆紀錄，把現有資料庫標記為「已套用到這個基準」。之後的每次結構變更改回
            // 正常流程：先改 db/club-schema.sql 與 docs/12，再 dotnet ef migrations add 產生
            // 真正會執行 DDL 的新 migration（backend-engineer 交接，見 apps/api/README.md）。
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 同上：基準 migration 不做任何事，Down() 沒有東西可還原。
        }
    }
}
