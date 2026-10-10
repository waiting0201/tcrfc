using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 測試專用的「可調整欄位」表單（<c>form_code = zz_t_&lt;隨機&gt;</c>，tcrfc；每次呼叫各自一個代碼，平行執行的測試互不干擾）。
/// 捐助洽詢（<c>donation_enquiry</c>）整個拿掉後（2026-10-09），目錄內八種表單的欄位全部被系統鎖定（<c>FormCatalog.FieldLockedCodes</c>），
/// 但表單設計器仍保留「不在鎖定名單的表單可增刪改欄位」的程式路徑與測試需求（使用中欄位不可刪、內容摘要唯一、題目雙語回退、導向頁驗證、通知信設定等），
/// 因此測試時臨時建一張同形狀的表單（name／contact／message＝摘要／privacy_consent），測完連同收件資料一併刪除，不留在共用資料庫。
/// 🔴 只能在測試裡建立：生產與種子都沒有這個代碼，公開端點對不在資料庫裡的代碼一律 404。
/// </summary>
public sealed class UnlockedTestForm : IAsyncDisposable
{
    public string Code { get; }

    public Guid Id { get; }

    private UnlockedTestForm(Guid id, string code) => (Id, Code) = (id, code);

    public static async Task<UnlockedTestForm> CreateAsync()
    {
        await using var connection = new SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
            INSERT INTO forms (id, club_id, form_code, captcha_enabled) VALUES (@Form, @club, @Code, 1);
            INSERT INTO form_fields (id, form_id, field_key, field_type, is_required, validation_rule, options_json, is_summary, sort_order) VALUES
              (NEWID(), @Form, N'name', N'text', 1, NULL, NULL, 0, 0),
              (NEWID(), @Form, N'contact', N'text', 1, NULL, NULL, 0, 1),
              (NEWID(), @Form, N'message', N'textarea', 0, NULL, NULL, 1, 2),
              (NEWID(), @Form, N'privacy_consent', N'consent', 1, NULL, NULL, 0, 3);
            INSERT INTO form_fields_i18n (form_field_id, locale, label)
              SELECT ff.id, N'zh-Hant', CASE ff.field_key WHEN N'name' THEN N'姓名' WHEN N'contact' THEN N'聯絡方式' WHEN N'message' THEN N'洽詢內容' ELSE N'同意隱私權政策' END
              FROM form_fields ff WHERE ff.form_id = @Form;
            INSERT INTO form_fields_i18n (form_field_id, locale, label)
              SELECT ff.id, N'en', CASE ff.field_key WHEN N'name' THEN N'Name' WHEN N'contact' THEN N'Contact Info' WHEN N'message' THEN N'Enquiry Message' ELSE N'Agree to the Privacy Policy' END
              FROM form_fields ff WHERE ff.form_id = @Form;
            """;
        var id = Guid.NewGuid();
        var code = "zz_t_" + id.ToString("N")[..12];
        command.Parameters.AddWithValue("@Code", code);
        command.Parameters.AddWithValue("@Form", id);
        await command.ExecuteNonQueryAsync();
        return new UnlockedTestForm(id, code);
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new SqlConnection(Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")!);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE a FROM enquiry_answers a JOIN enquiries e ON e.id = a.enquiry_id WHERE e.form_id = @Form;
            DELETE FROM enquiries WHERE form_id = @Form;
            DELETE FROM forms WHERE id = @Form;
            """;
        command.Parameters.AddWithValue("@Form", Id);
        await command.ExecuteNonQueryAsync();
    }
}
