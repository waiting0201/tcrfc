using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignIndexesWithDdl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔴 S0-7l（2026-09-24，docs/18-work-errors.md E-45／docs/20-cicd.md §5）：刻意留空，
            // 比照 InitialBaseline 的作法（見該檔案）。
            //
            // scaffold 出來的原始內容是 281 個 DropIndex（本檔 git 歷史可查），對應
            // ClubDbContextCustomizations.cs 移除 ForeignKeyIndexConvention 之後，模型不再宣告的
            // 281 個索引（created_by 85、updated_by 85、club_id 33、其餘業務外鍵約 78）。
            // 這 281 個索引**從未真的建到任何資料庫**——InitialBaseline 的 Up() 是空的，這些索引
            // 只存在於 EF 的記憶體模型／migration snapshot，是 InitialBaseline scaffold 時舊慣例
            // 自動產生、從未跟 db/club-schema.sql 逐欄核對過的落差（S0-7k 已處理其中 1 筆
            // admin_refresh_tokens.replaced_by_id，S0-7l 處理其餘 280 筆＋改用整條移除慣例）。
            //
            // 對任何實際資料庫執行這裡的 DropIndex 都會失敗（找不到要刪的索引），所以刻意清空——
            // 這支 migration 存在的唯一目的是讓 ClubDbContextModelSnapshot.cs 更新為正確模型
            // （不再宣告這 281 個索引），使 `dotnet ef migrations has-pending-model-changes` 恢復綠燈。
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 同上：這支 migration 不做任何事，Down() 沒有東西可還原（原始內容是把上述 281 個
            // 索引用 CreateIndex 建回去，同樣因為它們從未真的存在於任何資料庫而刻意清空）。
        }
    }
}
