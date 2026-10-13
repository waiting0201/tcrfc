/* ============================================================================
   deploy/schema-snapshot.sql — 把「目前連線的資料庫」抽成可比對的正規化文字快照（唯讀）
   對應 docs/20-cicd.md §3「schema-drift」、docs/18 E-321、E-322。

   只讀 sys.* 目錄檢視與內建函式，不寫入任何東西（deploy/prod-schema-snapshot.sh 會以關鍵字掃描驗證這一點，
   所以本檔的程式碼部分不得出現寫入／執行類的詞，連欄位別名也不行）。
   每一行是一個「結構事實」，格式 `類別|…`，每行獨立、不依賴排序（schema-diff.py 以集合比對）。

   類別一覽（2026-10-13 嚴格化後；舊版沒有的標 ★）：
     TABLE|表                          TBLOPT|表|lock_escalation=…|ansi_nulls=…                      ★
     COL|表|欄|型別|可空|identity|計算欄位|coll=字串欄定序                                          （coll 為 ★）
     COLORDER|表|欄1,欄2,…              欄位的實際順序（依 column_id 排名；DROP COLUMN 留下的空號不算）    ★
     DEFAULT|表|欄|定義                 NAME|DEFAULT|表|欄|約束名稱                                   （NAME|DEFAULT 為 ★）
     PK|…／IDX|…（含 |opts=…）          索引選項：fill factor、padding、ignore_dup_key、鎖定、停用、壓縮、no_recompute   （opts 為 ★）
     NAME|PK|…／NAME|IDX|…              主鍵、唯一鍵與索引名稱（★ 系統自動命名的也輸出，不再豁免）
     FK|…（含 |trusted=…）              NAME|FK|表|名稱（★ 系統自動命名的也輸出）
     CK|表|定義                         NAME|CK|表|名稱（★ 系統自動命名的也輸出）  CKSTATE|表|名稱|disabled=…|not_trusted=…（★）
     STAT|表|名稱|欄位                  使用者建立的統計                                              ★
     VIEW|視圖  VIEWCOL|視圖|欄  VIEWDEF|視圖|ansi_nulls|quoted_identifier|正規化本體                （VIEWDEF 為 ★）
     OTHER|觸發程序／預存程序／函式／序列／同義字     SCHEMA|非 dbo 的使用者結構描述                    ★
     EXTPROP|表或欄|名稱|值             使用者的延伸屬性                                              ★
     DB|屬性=值                         資料庫層級屬性（定序、ANSI 設定）——嚴格比對                      ★
     ENV|屬性=值                        平台相關的環境資訊（相容性層級、RCSI、引擎版本、scoped 設定…）——只列出、不計差異  ★

   正規化規則（兩邊用同一份腳本，差異只會來自真的結構不同）：
     - 型別：照實輸出，不做任何等價正規化（2026-10-13 起資料庫不使用原生 json）。
     - 預設值／CHECK／計算欄位／視圖本體：去掉所有空白、換行、定位字元，轉小寫，拿掉方括號。
     - 唯一約束（ALTER TABLE ADD CONSTRAINT … UNIQUE）與唯一索引（CREATE UNIQUE INDEX）視為等價（都歸 IDX|）。
     - 約束與索引：比「結構」，名稱另列 NAME| 類別。🔴 2026-10-13 起 DEFAULT／CHECK／外鍵／主鍵／索引的名稱一律比對，
       不再豁免「系統自動命名」——命名規則見 docs/14「約束命名」，系統命名的約束已由 migration NameSystemConstraints 改為規則名稱。
     - 不抽：__EFMigrationsHistory、系統物件。
   視圖：EF 模型只能把視圖當 keyless entity 對映（ToView），不會建立視圖；EF 模式（schema-diff.py --mode ef）排除 VIEW*。
   ============================================================================ */
SET NOCOUNT ON;

/* ---- TABLE／表選項 ---- */
SELECT CONCAT('TABLE|', t.name)
FROM sys.tables t
WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
SELECT CONCAT('TBLOPT|', t.name, '|lock_escalation=', t.lock_escalation_desc COLLATE DATABASE_DEFAULT, '|ansi_nulls=', CAST(t.uses_ansi_nulls AS varchar(1)))
FROM sys.tables t
WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
/* ---- COLUMN：型別、可空、identity、計算欄位、字串欄定序 ---- */
SELECT CONCAT('COL|', t.name, '|', c.name, '|',
    CASE
      WHEN ty.name IN ('nvarchar','nchar') THEN CONCAT(ty.name, '(', CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length/2 AS varchar(10)) END, ')')
      WHEN ty.name IN ('varchar','char','varbinary','binary') THEN CONCAT(ty.name, '(', CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length AS varchar(10)) END, ')')
      WHEN ty.name IN ('decimal','numeric') THEN CONCAT(ty.name, '(', c.precision, ',', c.scale, ')')
      WHEN ty.name IN ('datetime2','datetimeoffset','time') THEN CONCAT(ty.name, '(', c.scale, ')')
      ELSE ty.name
    END,
    '|', CASE WHEN c.is_nullable = 1 THEN 'NULL' ELSE 'NOT NULL' END,
    '|', CASE WHEN c.is_identity = 1 THEN CONCAT('identity(', CAST(ic.seed_value AS varchar(30)), ',', CAST(ic.increment_value AS varchar(30)), ')') ELSE '' END,
    '|', CASE WHEN c.is_computed = 1 THEN CONCAT('computed:', LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(cc.definition,' ',''),CHAR(13),''),CHAR(10),''),CHAR(9),''),'[',''),']','')), ':persisted=', CAST(cc.is_persisted AS varchar(1))) ELSE '' END,
    '|coll=', ISNULL(c.collation_name COLLATE DATABASE_DEFAULT, ''))
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id

UNION ALL
/* ---- 欄位順序：依 column_id 排名（DROP COLUMN 會留下 column_id 空號，排名後空號不算） ---- */
SELECT CONCAT('COLORDER|', t.name, '|',
    (SELECT STRING_AGG(r.name, ',') WITHIN GROUP (ORDER BY r.rn)
       FROM (SELECT c.name, ROW_NUMBER() OVER (ORDER BY c.column_id) AS rn FROM sys.columns c WHERE c.object_id = t.object_id) r))
FROM sys.tables t
WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
/* ---- DEFAULT（定義 ＋ 名稱） ---- */
SELECT CONCAT('DEFAULT|', t.name, '|', c.name, '|', LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(dc.definition,' ',''),CHAR(13),''),CHAR(10),''),CHAR(9),''),'[',''),']','')))
FROM sys.default_constraints dc
JOIN sys.tables t ON t.object_id = dc.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id

UNION ALL
SELECT CONCAT('NAME|DEFAULT|', t.name, '|', c.name, '|', dc.name)
FROM sys.default_constraints dc
JOIN sys.tables t ON t.object_id = dc.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id

UNION ALL
/* ---- PK／UNIQUE 約束與所有索引（含唯一、篩選、含入欄位、遞減、叢集與否、索引選項） ---- */
SELECT CONCAT(
    CASE WHEN i.is_primary_key = 1 THEN 'PK|' ELSE 'IDX|' END,   -- 唯一約束（ALTER TABLE ADD CONSTRAINT UNIQUE）≡ 唯一索引，見檔頭說明
    t.name, '|',
    (SELECT STRING_AGG(CONCAT(c.name, CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END), ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
       FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0),
    '|unique=', CAST(i.is_unique AS varchar(1)),
    '|', LOWER(i.type_desc COLLATE DATABASE_DEFAULT),
    '|include=', ISNULL((SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY c.name)
       FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1), ''),
    '|where=', ISNULL(LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition,' ',''),CHAR(13),''),CHAR(10),''),CHAR(9),''),'[',''),']','')), ''),
    '|opts=ff', CAST(i.fill_factor AS varchar(3)),
    ',pad', CAST(i.is_padded AS varchar(1)),
    ',ign', CAST(i.ignore_dup_key AS varchar(1)),
    ',rowlock', CAST(i.allow_row_locks AS varchar(1)),
    ',pagelock', CAST(i.allow_page_locks AS varchar(1)),
    ',disabled', CAST(i.is_disabled AS varchar(1)),
    ',cmp=', ISNULL((SELECT MIN(p.data_compression_desc COLLATE DATABASE_DEFAULT) FROM sys.partitions p WHERE p.object_id = i.object_id AND p.index_id = i.index_id), ''),
    ',norecompute', ISNULL((SELECT CAST(MAX(CAST(s.no_recompute AS int)) AS varchar(1)) FROM sys.stats s WHERE s.object_id = i.object_id AND s.stats_id = i.index_id), '0'))
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
WHERE i.index_id > 0 AND i.is_hypothetical = 0

UNION ALL
/* ---- 索引／約束「名稱」：結構相同但名稱不同也算差異；系統自動命名的也輸出（不再豁免） ---- */
SELECT CONCAT('NAME|',
    CASE WHEN i.is_primary_key = 1 THEN 'PK' ELSE 'IDX' END,
    '|', t.name, '|', i.name, '|',
    (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
       FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0))
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
WHERE i.index_id > 0 AND i.is_hypothetical = 0

UNION ALL
/* ---- FOREIGN KEY（欄位對應、參照表、刪除／更新動作、是否啟用、是否受信任） ---- */
SELECT CONCAT('FK|', t.name, '|',
    (SELECT STRING_AGG(CONCAT(pc.name, '->', rc.name), ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
       FROM sys.foreign_key_columns fkc
       JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
       JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
      WHERE fkc.constraint_object_id = fk.object_id),
    '|ref=', rt.name, '|del=', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, '|upd=', fk.update_referential_action_desc COLLATE DATABASE_DEFAULT,
    '|disabled=', CAST(fk.is_disabled AS varchar(1)), '|trusted=', CAST(1 - fk.is_not_trusted AS varchar(1)))
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id

UNION ALL
SELECT CONCAT('NAME|FK|', t.name, '|', fk.name)
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
/* ---- CHECK（定義；名稱另列 NAME|CK；啟用／受信任狀態另列 CKSTATE） ---- */
SELECT CONCAT('CK|', t.name, '|', LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ck.definition,' ',''),CHAR(13),''),CHAR(10),''),CHAR(9),''),'[',''),']','')))
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
SELECT CONCAT('NAME|CK|', t.name, '|', ck.name)
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
SELECT CONCAT('CKSTATE|', t.name, '|', ck.name, '|disabled=', CAST(ck.is_disabled AS varchar(1)), '|not_trusted=', CAST(ck.is_not_trusted AS varchar(1)))
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
/* ---- 使用者建立的統計 ---- */
SELECT CONCAT('STAT|', t.name, '|', s.name, '|',
    (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY sc.stats_column_id)
       FROM sys.stats_columns sc JOIN sys.columns c ON c.object_id = sc.object_id AND c.column_id = sc.column_id
      WHERE sc.object_id = s.object_id AND sc.stats_id = s.stats_id))
FROM sys.stats s
JOIN sys.tables t ON t.object_id = s.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
WHERE s.user_created = 1

UNION ALL
/* ---- VIEW：名稱、輸出欄位、本體（正規化後）與建立時的 ANSI 設定 ---- */
SELECT CONCAT('VIEW|', v.name)
FROM sys.views v WHERE v.is_ms_shipped = 0

UNION ALL
SELECT CONCAT('VIEWCOL|', v.name, '|', c.name)
FROM sys.views v JOIN sys.columns c ON c.object_id = v.object_id WHERE v.is_ms_shipped = 0

UNION ALL
SELECT CONCAT('VIEWDEF|', v.name, '|ansi_nulls=', CAST(m.uses_ansi_nulls AS varchar(1)), '|quoted_identifier=', CAST(m.uses_quoted_identifier AS varchar(1)), '|', LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(m.definition,' ',''),CHAR(13),''),CHAR(10),''),CHAR(9),''),'[',''),']','')))
FROM sys.views v JOIN sys.sql_modules m ON m.object_id = v.object_id WHERE v.is_ms_shipped = 0

UNION ALL
/* ---- 其他我們不預期存在的物件類型：觸發程序、預存程序、函式、序列、同義字 ---- */
SELECT CONCAT('OTHER|', o.type_desc COLLATE DATABASE_DEFAULT, '|', o.name)
FROM sys.objects o
WHERE o.is_ms_shipped = 0
  AND o.type IN ('TR','P','FN','IF','TF','SO','SN','PC','FS','FT')

UNION ALL
/* ---- 使用者建立的結構描述（預設的 dbo 以外） ---- */
SELECT CONCAT('SCHEMA|', sc.name)
FROM sys.schemas sc
WHERE sc.schema_id > 4 AND sc.schema_id < 16384 AND sc.name <> 'dbo'

UNION ALL
/* ---- 使用者的延伸屬性（表與欄位層級） ---- */
SELECT CONCAT('EXTPROP|', t.name, '|', ISNULL(c.name, ''), '|', CAST(ep.name AS nvarchar(200)), '|', CAST(ep.value AS nvarchar(1000)))
FROM sys.extended_properties ep
JOIN sys.tables t ON t.object_id = ep.major_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
LEFT JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id AND ep.minor_id > 0
WHERE ep.class = 1

UNION ALL
/* ---- DB：資料庫層級屬性，嚴格比對（定序與影響物件語意的 ANSI 設定） ---- */
SELECT CONCAT('DB|', v.k, '=', v.v) COLLATE DATABASE_DEFAULT
FROM sys.databases d
CROSS APPLY (VALUES
    ('collation', CAST(d.collation_name AS nvarchar(200))),
    ('ansi_nulls', CAST(d.is_ansi_nulls_on AS nvarchar(5))),
    ('ansi_padding', CAST(d.is_ansi_padding_on AS nvarchar(5))),
    ('ansi_warnings', CAST(d.is_ansi_warnings_on AS nvarchar(5))),
    ('arithabort', CAST(d.is_arithabort_on AS nvarchar(5))),
    ('concat_null_yields_null', CAST(d.is_concat_null_yields_null_on AS nvarchar(5))),
    ('numeric_roundabort', CAST(d.is_numeric_roundabort_on AS nvarchar(5))),
    ('quoted_identifier', CAST(d.is_quoted_identifier_on AS nvarchar(5))),
    ('recursive_triggers', CAST(d.is_recursive_triggers_on AS nvarchar(5))),
    ('auto_create_stats', CAST(d.is_auto_create_stats_on AS nvarchar(5))),
    ('auto_update_stats', CAST(d.is_auto_update_stats_on AS nvarchar(5))),
    ('page_verify', CAST(d.page_verify_option_desc AS nvarchar(60))),
    ('parameterization_forced', CAST(d.is_parameterization_forced AS nvarchar(5))),
    ('trustworthy', CAST(d.is_trustworthy_on AS nvarchar(5))),
    ('db_chaining', CAST(d.is_db_chaining_on AS nvarchar(5))),
    ('cursor_close_on_commit', CAST(d.is_cursor_close_on_commit_on AS nvarchar(5))),
    ('local_cursor_default', CAST(d.is_local_cursor_default AS nvarchar(5)))
) v(k, v)
WHERE d.name = DB_NAME()

UNION ALL
/* ---- ENV：平台相關的環境資訊，只列出、不計差異（本機 SQL Server 容器與 Azure SQL 本來就不同，無法也不該對齊） ---- */
SELECT CONCAT('ENV|', v.k, '=', v.v) COLLATE DATABASE_DEFAULT
FROM sys.databases d
CROSS APPLY (VALUES
    ('compatibility_level', CAST(d.compatibility_level AS nvarchar(10))),
    ('read_committed_snapshot', CAST(d.is_read_committed_snapshot_on AS nvarchar(5))),
    ('snapshot_isolation', CAST(d.snapshot_isolation_state_desc AS nvarchar(60))),
    ('auto_update_stats_async', CAST(d.is_auto_update_stats_async_on AS nvarchar(5))),
    ('recovery_model', CAST(d.recovery_model_desc AS nvarchar(60))),
    ('containment', CAST(d.containment_desc AS nvarchar(60))),
    ('delayed_durability', CAST(d.delayed_durability_desc AS nvarchar(60))),
    ('target_recovery_seconds', CAST(d.target_recovery_time_in_seconds AS nvarchar(10))),
    ('query_store', CAST(d.is_query_store_on AS nvarchar(5))),
    ('accelerated_database_recovery', CAST(d.is_accelerated_database_recovery_on AS nvarchar(5))),
    ('encrypted', CAST(d.is_encrypted AS nvarchar(5))),
    ('engine_edition', CAST(SERVERPROPERTY('EngineEdition') AS nvarchar(10))),
    ('product_major_version', CAST(SERVERPROPERTY('ProductMajorVersion') AS nvarchar(10)))
) v(k, v)
WHERE d.name = DB_NAME()

UNION ALL
SELECT CONCAT('ENV|scoped:', sc.name, '=', CAST(sc.value AS nvarchar(100))) COLLATE DATABASE_DEFAULT
FROM sys.database_scoped_configurations sc
;
