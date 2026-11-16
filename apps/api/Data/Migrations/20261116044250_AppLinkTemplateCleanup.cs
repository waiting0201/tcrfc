using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 資料遷移（不改結構）：App 使用者連結樣板清理：值含大括號的使用者連結設為 NULL。冪等、事後斷言（THROW）。
    /// SQL 與 <c>db/migrations/20261116_app-link-template-cleanup.sql</c> 同源：<c>Data/Migrations/Sql/20261116_app-link-template-cleanup.sql</c> 是逐位元組複本（MigrationSqlParityTests 比對），
    /// 以內嵌資源讀入，依 GO 切批、每批包 <c>EXEC</c>（見 <see cref="EmbeddedMigrationSql"/>）。經 db-migrate.yml 核准後套用到正式庫。
    /// </summary>
    public partial class AppLinkTemplateCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            EmbeddedMigrationSql.Run(migrationBuilder, "migration-sql/20261116_app-link-template-cleanup.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意不還原：資料遷移不可逆（舊值已被覆蓋，且遷移只動「仍等於舊值」的列，無從得知哪些是被它改的）。
            // 回滾程式碼時資料維持新值即可；救命索是 PITR。
        }
    }
}
