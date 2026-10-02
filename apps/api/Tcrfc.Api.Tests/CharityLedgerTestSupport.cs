using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Tests.Fixtures;

namespace Tcrfc.Api.Tests;

/// <summary>CH-4／CH-5（結算、對帳、報表、憑證、N7）整合測試共用的資料建立輔助。直接寫資料庫建立「已付款」捐款（可指定付款時間與分潤快照），不走公開流程。</summary>
public static class CharityLedgerTestSupport
{
    /// <summary>建一筆已付款（或指定狀態）的捐款＋確認的金流交易＋一張憑證。回傳 (id, 單號, 交易識別碼)。</summary>
    public static async Task<(Guid Id, string OrderNo, string TransactionId)> SeedPaidDonationAsync(
        CharityApiFixture fx, Guid projectId, Guid? storeId, int amount, DateTime paidAtUtc,
        int storeAmount = 0, int projectAmount = 0, decimal storePct = 0m, decimal projectPct = 0m,
        string status = "paid", string donorName = "測試捐款人", bool anonymous = false, string? refundReason = null,
        string invoiceIssueStatus = "issued", string invoiceMode = "b2c_invoice", string? invoiceNo = null)
    {
        var id = Guid.NewGuid();
        var orderNo = $"CTL{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var tx = $"TEST-TX-{Guid.NewGuid():N}";
        var email = CharityTestSupport.Email("ledger");
        await fx.ExecuteAsync(
            """
            INSERT INTO donations (id, order_no, donation_project_id, donation_store_id, amount, status, created_at, paid_at, donor_name, donor_email,
                is_anonymous, store_share_pct_snapshot, project_share_pct_snapshot, store_amount, project_amount, association_amount, invoice_mode, refund_reason)
            VALUES (@id, @o, @p, @s, @a, @st, @cr, @paid, @n, @e, @anon, @sp, @pp, @sa, @pa, @aa, @mode, @rr);
            INSERT INTO donation_payments (donation_id, transaction_id, requested_at, confirmed_at, amount, status)
            VALUES (@id, @tx, @cr, @paid, @a, N'confirmed');
            INSERT INTO donation_invoices (donation_id, invoice_type, invoice_no, issued_at, carrier_type, issue_status, void_status)
            VALUES (@id, @mode, @ino, @issued, N'mobile_carrier', @istatus, N'none');
            """,
            ("@id", id), ("@o", orderNo), ("@p", projectId), ("@s", (object?)storeId ?? DBNull.Value), ("@a", amount), ("@st", status),
            ("@cr", paidAtUtc.AddMinutes(-5)), ("@paid", status == "created" ? DBNull.Value : (object)paidAtUtc), ("@n", donorName), ("@e", email),
            ("@anon", anonymous), ("@sp", storePct), ("@pp", projectPct), ("@sa", storeAmount), ("@pa", projectAmount),
            ("@aa", amount - storeAmount - projectAmount), ("@mode", invoiceMode), ("@rr", (object?)refundReason ?? DBNull.Value), ("@tx", tx),
            ("@ino", invoiceIssueStatus == "issued" ? (object)(invoiceNo ?? $"CT{Guid.NewGuid():N}"[..12].ToUpperInvariant()) : DBNull.Value),
            ("@issued", invoiceIssueStatus == "issued" ? (object)paidAtUtc : DBNull.Value), ("@istatus", invoiceIssueStatus));
        return (id, orderNo, tx);
    }

    /// <summary>把捐款改成已退款（模擬後台退款完成後的本站狀態）。</summary>
    public static Task RefundAsync(CharityApiFixture fx, Guid donationId, string reason = "測試退款")
        => fx.ExecuteAsync("UPDATE donations SET status = N'refunded', refund_reason = @r WHERE id = @id", ("@id", donationId), ("@r", reason));

    /// <summary>台灣日期當天的中午（轉成 UTC）：避開日界線，期間判斷不會因為時區換算而跨日。</summary>
    public static DateTime NoonUtc(DateOnly taiwanDate) => DateTime.SpecifyKind(taiwanDate.ToDateTime(new TimeOnly(12, 0)).AddHours(-8), DateTimeKind.Utc);

    public static DateTime NoonUtc(int year, int month, int day) => NoonUtc(new DateOnly(year, month, day));

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options));
}
