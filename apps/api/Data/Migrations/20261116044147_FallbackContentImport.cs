using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 資料遷移（不改結構）：官網寫死後備轉後台資料（磐石國際夥伴、藍鯨建教合作學校、贊助方案名稱、成立首年榮譽、頁尾簡介與社群）。定義由 db/seed/generate-fallback-content-migration.py 產生，只補不存在或未被人改過的內容。冪等、事後斷言（THROW）。
    /// SQL 與 <c>db/migrations/20261113_fallback-content-import.sql</c> 同源：<c>Data/Migrations/Sql/20261113_fallback-content-import.sql</c> 是逐位元組複本（MigrationSqlParityTests 比對），
    /// 以內嵌資源讀入，依 GO 切批、每批包 <c>EXEC</c>（見 <see cref="EmbeddedMigrationSql"/>）。經 db-migrate.yml 核准後套用到正式庫。
    /// </summary>
    public partial class FallbackContentImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            EmbeddedMigrationSql.Run(migrationBuilder, "migration-sql/20261113_fallback-content-import.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意不還原：資料遷移不可逆（舊值已被覆蓋，且遷移只動「仍等於舊值」的列，無從得知哪些是被它改的）。
            // 回滾程式碼時資料維持新值即可；救命索是 PITR。
        }
    }
}
