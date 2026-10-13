/* ============================================================================
   deploy/schema-snapshot.sql — 把「目前連線的資料庫」抽成可比對的正規化文字快照（唯讀）
   對應 docs/20-cicd.md §3「schema-drift」、docs/18 E-321。

   只讀 sys.* 目錄檢視，不寫入任何東西。每一行是一個「結構事實」，格式 `類別|表|細節…`，
   由 deploy/schema-drift-check.sh 對兩個庫各抽一份後 sort 再 diff。

   正規化規則（兩邊用同一份腳本，差異只會來自真的結構不同）：
     - 型別：照實輸出，不做任何等價正規化。2026-10-13 起資料庫不使用原生 json 型別（JSON 內容欄位是
       nvarchar(max) ＋ 具名 ISJSON CHECK），所以 json 與 nvarchar 不再有「視為等價」的理由；
       DDL 或資料庫又出現 json 欄位時，它會以 `json` 型別出現在快照裡並與 EF 模型（nvarchar(max)）形成差異，
       另外 deploy/schema-drift-check.sh 會先於建庫直接拒絕。
     - 預設值／CHECK／計算欄位定義：去掉所有空白與最外層括號、轉小寫、拿掉方括號
       （[col] 與 col 視為相同），讓 EF 與手寫 DDL 的排版差異不算差異。
     - 唯一約束（ALTER TABLE ADD CONSTRAINT … UNIQUE）與唯一索引（CREATE UNIQUE INDEX）視為等價（都歸 IDX|）：
       EF 的 HasIndex().IsUnique() 只會建唯一索引，無法表達「約束」；兩者的儲存結構與查詢行為相同。
       取捨：此檢查看不出『約束 vs 索引』的差別（影響只在手寫 migration 要用 DROP CONSTRAINT 還是 DROP INDEX，
       docs/20 §5 migration 注意事項 2 已要求先查 sys.indexes／sys.key_constraints）。
     - 約束與索引：比「結構」（欄位順序、唯一性、篩選條件、含入欄位、遞減）；名稱另列 NAME| 類別，
       腳本預設把 NAME| 當成必須一致（索引／約束名稱以 DDL 為準，docs/20 §5 migration 注意事項 2）。
     - 系統自動命名的約束（DDL 沒寫 CONSTRAINT 名稱時，SQL Server 產生 CK__表__欄__隨機碼）每個庫都不同，NAME| 不比；
       其「結構」（CK|/IDX|/FK|）仍照比。
     - 不抽：__EFMigrationsHistory、系統物件、視圖本體（見下方 VIEW 說明）。
   視圖：EF 模型只能把視圖當 keyless entity 對映（ToView），不會建立視圖，所以 EF 側沒有視圖可比。
         這裡只輸出視圖的名稱與輸出欄位（VIEW|、VIEWCOL|），由腳本只對「DDL 庫」留存並檢查不得為空；
         不拿來和 EF 庫 diff（見腳本的排除清單）。
   ============================================================================ */
SET NOCOUNT ON;

/* ---- TABLE ---- */
SELECT CONCAT('TABLE|', t.name)
FROM sys.tables t
WHERE t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
/* ---- COLUMN：型別、可空、identity、計算欄位 ---- */
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
    '|', CASE WHEN c.is_computed = 1 THEN CONCAT('computed:', LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(cc.definition,' ',''),CHAR(13),''),CHAR(10),''),'[',''),']','')), ':persisted=', CAST(cc.is_persisted AS varchar(1))) ELSE '' END)
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id

UNION ALL
/* ---- DEFAULT（約束名稱不比，只比欄位與定義） ---- */
SELECT CONCAT('DEFAULT|', t.name, '|', c.name, '|',
    LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(dc.definition,' ',''),CHAR(13),''),CHAR(10),''),'[',''),']','')))
FROM sys.default_constraints dc
JOIN sys.tables t ON t.object_id = dc.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id

UNION ALL
/* ---- PK／UNIQUE 約束與所有索引（含唯一、篩選、含入欄位、遞減、叢集與否） ---- */
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
    '|where=', ISNULL(LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(i.filter_definition,' ',''),CHAR(13),''),CHAR(10),''),'[',''),']','')), ''))
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
WHERE i.index_id > 0 AND i.is_hypothetical = 0

UNION ALL
/* ---- 索引／約束「名稱」：結構相同但名稱不同也算差異（名稱以 DDL 為準） ---- */
SELECT CONCAT('NAME|',
    CASE WHEN i.is_primary_key = 1 THEN 'PK' ELSE 'IDX' END,
    '|', t.name, '|', i.name, '|',
    (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
       FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0))
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = i.object_id AND kc.unique_index_id = i.index_id
WHERE i.index_id > 0 AND i.is_hypothetical = 0 AND ISNULL(kc.is_system_named, 0) = 0

UNION ALL
/* ---- FOREIGN KEY（欄位對應、參照表、刪除／更新動作、是否啟用） ---- */
SELECT CONCAT('FK|', t.name, '|',
    (SELECT STRING_AGG(CONCAT(pc.name, '->', rc.name), ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
       FROM sys.foreign_key_columns fkc
       JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
       JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
      WHERE fkc.constraint_object_id = fk.object_id),
    '|ref=', rt.name, '|del=', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, '|upd=', fk.update_referential_action_desc COLLATE DATABASE_DEFAULT,
    '|disabled=', CAST(fk.is_disabled AS varchar(1)))
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id

UNION ALL
SELECT CONCAT('NAME|FK|', t.name, '|', fk.name)
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
WHERE fk.is_system_named = 0

UNION ALL
/* ---- CHECK（定義正規化；名稱另列 NAME|CK） ---- */
SELECT CONCAT('CK|', t.name, '|',
    LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ck.definition,' ',''),CHAR(13),''),CHAR(10),''),'[',''),']','')))
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'

UNION ALL
SELECT CONCAT('NAME|CK|', t.name, '|', ck.name)
FROM sys.check_constraints ck
JOIN sys.tables t ON t.object_id = ck.parent_object_id AND t.is_ms_shipped = 0 AND t.name <> '__EFMigrationsHistory'
WHERE ck.is_system_named = 0   -- 系統自動命名（CK__table__col__隨機碼）每個庫都不同，不比；只比有明確名稱的
  -- 本機庫的行內 CHECK 經 EF AlterColumn 重建過，旗標會變 0 但名字仍是 CK__…__8碼十六進位的自動名稱，一併視為自動命名
  AND NOT (ck.name LIKE 'CK[_][_]%' AND ck.name LIKE '%[_][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]')

UNION ALL
/* ---- VIEW（只給 DDL 庫的存在性檢查用，不與 EF 庫 diff） ---- */
SELECT CONCAT('VIEW|', v.name)
FROM sys.views v WHERE v.is_ms_shipped = 0

UNION ALL
SELECT CONCAT('VIEWCOL|', v.name, '|', c.name)
FROM sys.views v JOIN sys.columns c ON c.object_id = v.object_id WHERE v.is_ms_shipped = 0

UNION ALL
/* ---- 其他我們不預期存在的物件類型：觸發程序、預存程序、函式、序列、同義字 ---- */
SELECT CONCAT('OTHER|', o.type_desc COLLATE DATABASE_DEFAULT, '|', o.name)
FROM sys.objects o
WHERE o.is_ms_shipped = 0
  AND o.type IN ('TR','P','FN','IF','TF','SO','SN','PC','FS','FT')
;
