using Microsoft.EntityFrameworkCore.Migrations;
using Tcrfc.Api.Data;

#nullable disable

namespace Tcrfc.Api.Data.Migrations
{
    /// <summary>
    /// 把系統自動命名的 DEFAULT／CHECK 約束（<c>DF__表__欄__隨機碼</c>、<c>CK__表__欄__隨機碼</c>）改成規則名稱
    /// <c>DF_&lt;表&gt;_&lt;欄&gt;</c>／<c>CK_&lt;表&gt;_&lt;欄&gt;</c>（2026-10-13 使用者要求本機與正式庫結構完全一樣，docs/14「約束命名」、E-322）。
    /// <para>
    /// 實作與失敗行為見 <see cref="SystemNamedConstraintRenamer"/>：以 sys.* 查詢產生、冪等、
    /// 目標名稱已存在／同欄重複／超過 128 字元／表層級系統命名 CHECK 一律 THROW 並點名（整支回滾，不默默跳過）。
    /// 正式庫形狀（DDL 建庫、migration 把欄位加在表尾）與本機形狀（部分 CHECK 旗標為 0 但名稱仍是自動名稱）都正確。
    /// </para>
    /// <para><b>Down</b>：不還原。隨機碼名稱無法重現，且還原只會讓各環境名稱再度分岔；刻意的非對稱。</para>
    /// </summary>
    public partial class NameSystemConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SystemNamedConstraintRenamer.Rename(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 刻意不還原（見類別說明）。
        }
    }
}
