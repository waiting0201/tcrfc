using System.Text;

namespace Tcrfc.Api.Common;

/// <summary>
/// 最小可行的 CSV 編碼／解析（RFC 4180 精神：逗號分欄、雙引號包住含特殊字元的欄位、
/// 欄位內雙引號用兩個雙引號跳脫）。S1-6（B4 常見問題）第一個接上 CSV 匯入／匯出的模組，
/// 規劃書與 <c>docs/04-data-model.md</c>／<c>docs/12b-database-tables.md</c> §10 只確認
/// 「FAQ 題目要支援 CSV 匯入＋匯出」這件事本身，沒有逐欄定義格式，這是本輪依「最小可行」
/// 原則新增的共用工具，放在 <c>Common/</c> 是因為 docs/12b §10.1 另外列了整季賽程／積分榜／
/// 301 轉址／物流單號四項也要支援 CSV，屆時應該重用這裡而不是各自另刻一份剖析器。
///
/// ⚠️ **`Encoding.UTF8` 這個靜態成員本身就會發出 BOM**（`GetPreamble()` 回傳 3 bytes）——
/// 本檔特別把「產生 BOM 開頭的位元組」與「編碼字串」分開兩個方法，是為了讓呼叫端看得到
/// BOM 是刻意加的（規劃書與 docs 都沒有明文要求 BOM，是任務指示「沒定義就採最小可行」的
/// 具體選擇：Excel 開啟不含 BOM 的 UTF-8 CSV 在部分作業系統會誤判編碼，中文欄位變亂碼）。
/// </summary>
public static class CsvUtils
{
    /// <summary>單一欄位編碼：含逗號、雙引號、換行才需要用雙引號包住，欄位內雙引號跳脫成兩個。</summary>
    public static string EncodeField(string? value)
    {
        value ??= string.Empty;
        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    public static string EncodeRow(IEnumerable<string?> fields) => string.Join(",", fields.Select(EncodeField));

    /// <summary>把多行（含表頭）組成完整 CSV 文字，每列用 <c>\r\n</c> 結尾
    /// （Excel／多數試算表軟體的既有慣例，即使來源系統本身用 <c>\n</c>）。</summary>
    public static string BuildCsv(IEnumerable<IEnumerable<string?>> rows)
        => string.Concat(rows.Select(row => EncodeRow(row) + "\r\n"));

    /// <summary>轉成帶 UTF-8 BOM 的位元組——見本類別檔頭「為什麼要 BOM」的說明。</summary>
    public static byte[] ToUtf8BytesWithBom(string csvText)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return encoding.GetPreamble().Concat(encoding.GetBytes(csvText)).ToArray();
    }

    /// <summary>
    /// 解析完整 CSV 文字為列×欄的字串陣列。容錯處理：
    /// - 開頭若有 BOM 字元（<c>﻿</c>）先移除（呼叫端可能忘記先用對應編碼讀取）。
    /// - 換行一律先正規化成 <c>\n</c>（含引號內的換行）——CSV 資料本身是否用 <c>\r\n</c>
    ///   對「這是不是一列的分隔」這件事沒有語意差異，正規化能大幅簡化狀態機。
    /// - 完全空白的列（例如檔案結尾多一個空行）會被忽略，不當成一列空資料。
    /// </summary>
    public static List<List<string>> Parse(string content)
    {
        if (content.Length > 0 && content[0] == '﻿')
        {
            content = content[1..];
        }

        content = content.Replace("\r\n", "\n").Replace("\r", "\n");

        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var rowHasAnyContent = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    rowHasAnyContent = true;
                    break;
                case ',':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rowHasAnyContent = true;
                    break;
                case '\n':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    if (rowHasAnyContent || currentRow.Any(f => f.Length > 0))
                    {
                        rows.Add(currentRow);
                    }
                    currentRow = [];
                    rowHasAnyContent = false;
                    break;
                default:
                    field.Append(c);
                    if (!char.IsWhiteSpace(c))
                    {
                        rowHasAnyContent = true;
                    }
                    break;
            }
        }

        // 檔案結尾沒有換行符的最後一列。
        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString());
            if (rowHasAnyContent || currentRow.Any(f => f.Length > 0))
            {
                rows.Add(currentRow);
            }
        }

        return rows;
    }
}
