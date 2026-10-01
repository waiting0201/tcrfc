using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tcrfc.Api.CharityPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔴 冪等：用 IF OBJECT_ID 守住。db/charity-schema.sql（2026-10-01 起）新建的庫已經有這張表，
            // 只有 CH-1 時期建好的本機庫缺它；兩種庫跑 `dotnet ef database update` 都要成功。
            // 內容與 db/charity-schema.sql 逐項一致（欄位、約束、索引、外鍵）；EF 產生的 CreateTable 版本與此等價，
            // 這裡改寫成守衛過的 SQL 是為了冪等，ModelSnapshot／Designer 不受影響。
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.admin_refresh_tokens', N'U') IS NULL
BEGIN
    CREATE TABLE admin_refresh_tokens (
        seq             bigint IDENTITY(1,1) NOT NULL,
        id              uniqueidentifier NOT NULL DEFAULT NEWID(),
        admin_user_id   uniqueidentifier NOT NULL,
        token_hash      nvarchar(128)    NOT NULL,
        issued_at       datetime2(3)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        expires_at      datetime2(3)     NOT NULL,
        revoked_at      datetime2(3)     NULL,
        replaced_by_id  uniqueidentifier NULL,
        CONSTRAINT PK_admin_refresh_tokens PRIMARY KEY NONCLUSTERED (id),
        CONSTRAINT UQ_admin_refresh_tokens_seq UNIQUE CLUSTERED (seq),
        CONSTRAINT UQ_admin_refresh_tokens_token_hash UNIQUE (token_hash)
    );
    CREATE INDEX IX_admin_refresh_tokens_user ON admin_refresh_tokens (admin_user_id);
    ALTER TABLE admin_refresh_tokens ADD CONSTRAINT FK_admin_refresh_tokens_user
        FOREIGN KEY (admin_user_id) REFERENCES admin_users (id) ON DELETE CASCADE;
    ALTER TABLE admin_refresh_tokens ADD CONSTRAINT FK_admin_refresh_tokens_replaced
        FOREIGN KEY (replaced_by_id) REFERENCES admin_refresh_tokens (id);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'dbo.admin_refresh_tokens', N'U') IS NOT NULL DROP TABLE admin_refresh_tokens;");
        }
    }
}
