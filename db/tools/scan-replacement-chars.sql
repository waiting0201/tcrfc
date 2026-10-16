-- db/tools/scan-replacement-chars.sql — 唯讀：掃全庫含 U+FFFD（NCHAR(65533)，亂碼）的列（E-296／E-330）
--
-- 用法（對要檢查的庫執行；不寫入任何資料）：
--   sqlcmd -S <server> -d <db> -U <user> -N -b -i db/tools/scan-replacement-chars.sql
--   本機：docker cp 進容器後用 -i（不得用 stdin）。
-- 輸出：每個受損欄位一列「表|欄|主鍵(JSON)|U+FFFD 個數」。無輸出（只有表頭）＝乾淨。
-- 涵蓋 nvarchar／nchar／json（json 先轉 nvarchar(max)）。主鍵由 sys.indexes 動態取得，複合主鍵也完整輸出。
SET NOCOUNT ON;
DECLARE @sql nvarchar(max) = N'';
DECLARE @t sysname, @c sysname, @ty sysname, @expr nvarchar(400), @pk nvarchar(max), @obj int;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
  SELECT t.name, c.name, ty.name, t.object_id
  FROM sys.columns c
  JOIN sys.tables t ON t.object_id = c.object_id
  JOIN sys.types ty ON ty.user_type_id = c.user_type_id
  WHERE ty.name IN (N'nvarchar', N'nchar', N'json') AND c.max_length <> 0
  ORDER BY t.name, c.column_id;
OPEN cur;
FETCH NEXT FROM cur INTO @t, @c, @ty, @obj;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @expr = CASE WHEN @ty = N'json' THEN N'CAST([' + @c + N'] AS nvarchar(max))' ELSE N'[' + @c + N']' END;
  -- 主鍵欄位 → 'col', [col], ...（供 JSON_OBJECT 之類以外的相容寫法：用字串串接，避免依賴 2025 才有的函式）
  SELECT @pk = STRING_AGG(CAST(N'N''' + pc.name + N'=''+CAST([' + pc.name + N'] AS nvarchar(100))' AS nvarchar(max)), N'+N'';''+')
         WITHIN GROUP (ORDER BY ic.key_ordinal)
  FROM sys.indexes i
  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  JOIN sys.columns pc ON pc.object_id = ic.object_id AND pc.column_id = ic.column_id
  WHERE i.object_id = @obj AND i.is_primary_key = 1;
  SET @sql += N'SELECT N''' + @t + N''' AS tbl, N''' + @c + N''' AS col, '
            + ISNULL(@pk, N'N''(no pk)''') + N' AS pk, '
            + N'(LEN(' + @expr + N') - LEN(REPLACE(' + @expr + N' COLLATE Latin1_General_BIN2, NCHAR(65533), N''''))) AS fffd_count '
            + N'FROM [' + @t + N'] WHERE CHARINDEX(NCHAR(65533) COLLATE Latin1_General_BIN2, ' + @expr + N' COLLATE Latin1_General_BIN2) > 0 UNION ALL ';
  FETCH NEXT FROM cur INTO @t, @c, @ty, @obj;
END
CLOSE cur; DEALLOCATE cur;
SET @sql += N'SELECT NULL, NULL, NULL, NULL WHERE 1 = 0';
EXEC sp_executesql @sql;
