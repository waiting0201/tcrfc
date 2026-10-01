using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <summary>
    /// 🔴 慈善庫的「已套用空白基準」（docs/20-cicd.md §5 的一次性 handoff，比照主站 <c>InitialBaseline</c>）。
    /// 慈善庫的 29 張表是用 <c>db/charity-schema.sql</c> 手寫 DDL 建好的（CH-1），這個 migration 只是讓 EF Core 的
    /// <c>CharityDbContextModelSnapshot</c> 有一個「目前資料庫長什麼樣」的起點，<b>Up／Down 刻意清空</b>，不得重跑 DDL。
    /// 之後每一次綱要異動都是一支新的 migration（見 <c>AddAdminRefreshTokens</c>）。
    /// </summary>
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 刻意留空：資料庫已由 db/charity-schema.sql 建立。
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意留空：基準 migration 不得刪除任何表。
        }
    }
}
