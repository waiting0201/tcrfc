namespace Tcrfc.Api.Data;

/// <summary>
/// 把「系統自動命名」的 DEFAULT／CHECK 約束（<c>DF__表__欄__8碼十六進位</c>、<c>CK__表__欄__8碼十六進位</c>）改成決定性的規則名稱
/// （2026-10-13 使用者要求「本機與正式庫結構完全一樣」，docs/14「約束命名」、E-322）。
/// <para>
/// <b>為什麼需要</b>：SQL Server 在 DDL／migration 沒寫 <c>CONSTRAINT 名稱</c> 時會產生帶隨機碼的名稱，每個資料庫都不同——
/// 同一份 DDL 建兩次、或 EF 的 <c>AddColumn(defaultValue)</c>（它從不命名預設值）各跑一次，名稱就不一樣，
/// SSMS／Azure Data Studio 的 Schema Compare 會把它們全報成差異。
/// </para>
/// <para>
/// <b>規則</b>：欄位層級預設值 <c>DF_&lt;表&gt;_&lt;欄&gt;</c>；欄位層級 CHECK <c>CK_&lt;表&gt;_&lt;欄&gt;</c>。
/// 表層級（不屬於單一欄位）的系統命名 CHECK 無法決定名稱，<b>失敗並點名</b>（DDL 的表層級 CHECK 一律手動具名，如 <c>CK_banners_video_key</c>）。
/// 超過 128 字元、同一欄有兩個自動命名 CHECK（目標名稱重複）、目標名稱已被別的物件占用——一律失敗並點名，不默默跳過。
/// </para>
/// <para>
/// <b>冪等、以 sys.* 查詢產生</b>（沒有手抄清單）：已是規則名稱或手動具名的約束不碰；全部改完後再跑一次等於無操作。
/// 辨識「系統命名」= <c>is_system_named = 1</c> 或名稱符合 <c>DF__*__*_8碼十六進位</c> 的樣式（EF 的 AlterColumn 重建過的 CHECK
/// 旗標會變 0 但名稱仍是自動名稱）。
/// </para>
/// <para>
/// <b>以後的 migration</b>：用 <c>AddColumn(… defaultValue)</c> 或 <c>migrationBuilder.Sql("… CHECK …")</c> 新增預設值／欄位 CHECK 的 migration，
/// 最後一行呼叫 <c>SystemNamedConstraintRenamer.Rename(migrationBuilder)</c>（或在 SQL 內直接寫 <c>CONSTRAINT DF_表_欄 DEFAULT …</c>），
/// DDL 也要寫同名 <c>CONSTRAINT</c>；漏做會被 <c>schema-drift-check.sh --mode local-vs-ddl</c> 與正式庫快照比對抓到。
/// </para>
/// </summary>
public static class SystemNamedConstraintRenamer
{
    public static void Rename(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(Script);

    // 單一批次、全程只讀 sys.*，最後才用 sp_rename 改名；任何檢查失敗都在改名之前 THROW（EF 每支 migration 一個交易，整支回滾）。
    public const string Script = @"
DECLARE @plan TABLE (kind char(2) NOT NULL, sch sysname NOT NULL, tbl sysname NOT NULL, col sysname NULL, old_name sysname NOT NULL, new_name nvarchar(400) NULL);
INSERT INTO @plan (kind, sch, tbl, col, old_name)
SELECT 'DF', s.name, t.name, c.name, dc.name
FROM sys.default_constraints dc
JOIN sys.tables t ON t.object_id = dc.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> N'__EFMigrationsHistory'
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.is_system_named = 1
   OR dc.name LIKE N'DF[_][_]%[_][_][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'
UNION ALL
SELECT 'CK', s.name, t.name, c.name, ck.name
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> N'__EFMigrationsHistory'
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.columns c ON c.object_id = ck.parent_object_id AND c.column_id = ck.parent_column_id
WHERE ck.is_system_named = 1
   OR ck.name LIKE N'CK[_][_]%[_][_][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]';

UPDATE @plan SET new_name = CONCAT(kind, N'_', tbl, N'_', col) WHERE col IS NOT NULL;

DECLARE @msg nvarchar(2000);

SELECT @msg = LEFT(STRING_AGG(CONCAT(tbl, N'.', old_name), N'、'), 1500) FROM @plan WHERE new_name IS NULL;
IF @msg IS NOT NULL
BEGIN
    SET @msg = CONCAT(N'下列系統自動命名的表層級約束無法決定規則名稱，請在 DDL 與 migration 手動具名（CK_<表>_<說明>）後重跑：', @msg);
    THROW 50010, @msg, 1;
END;

SELECT @msg = LEFT(STRING_AGG(CONCAT(tbl, N'.', old_name, N' → ', new_name), N'、'), 1500) FROM @plan WHERE LEN(new_name) > 128;
IF @msg IS NOT NULL
BEGIN
    SET @msg = CONCAT(N'目標約束名稱超過 128 字元，請手動具名後重跑：', @msg);
    THROW 50011, @msg, 1;
END;

SELECT @msg = LEFT(STRING_AGG(new_name, N'、'), 1500) FROM (SELECT new_name FROM @plan GROUP BY new_name HAVING COUNT(*) > 1) d;
IF @msg IS NOT NULL
BEGIN
    SET @msg = CONCAT(N'多個系統命名約束的目標名稱相同（同一欄有兩個以上自動命名的同類約束），請手動具名後重跑：', @msg);
    THROW 50012, @msg, 1;
END;

SELECT @msg = LEFT(STRING_AGG(CONCAT(p.tbl, N'.', p.old_name, N' → ', p.new_name), N'、'), 1500)
FROM @plan p
JOIN sys.objects o ON o.name = p.new_name COLLATE DATABASE_DEFAULT AND o.schema_id = SCHEMA_ID(p.sch);
IF @msg IS NOT NULL
BEGIN
    SET @msg = CONCAT(N'目標約束名稱已被其他物件占用，不默默跳過，請人工處理後重跑：', @msg);
    THROW 50013, @msg, 1;
END;

DECLARE @old nvarchar(520), @new sysname;
DECLARE rename_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT CONCAT(QUOTENAME(sch), N'.', QUOTENAME(old_name)), new_name FROM @plan ORDER BY sch, tbl, kind, col;
OPEN rename_cur;
FETCH NEXT FROM rename_cur INTO @old, @new;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sp_rename @old, @new, N'OBJECT';
    FETCH NEXT FROM rename_cur INTO @old, @new;
END;
CLOSE rename_cur;
DEALLOCATE rename_cur;
";
}
