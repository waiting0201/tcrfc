using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>慈善捐款的純業務規則（分潤、單號、憑證欄位格式）。不需要資料庫與行程。</summary>
public sealed class CharityDonationRulesTests
{
    [Theory]
    [InlineData(1000, 5.00, 8.00, 50, 80, 870)]
    [InlineData(333, 5.00, 8.00, 16, 26, 291)]      // 16.65 → 16、26.64 → 26，尾差歸協會
    [InlineData(100, 0.00, 12.00, 0, 12, 88)]       // 無店家歸屬：店家分潤 0，項目分潤照算
    [InlineData(99, 33.33, 66.67, 32, 66, 1)]       // 合計 100%：捨去後的尾差仍歸協會，不會是負數
    [InlineData(1, 5.00, 5.00, 0, 0, 1)]
    public void 分潤無條件捨去至整數元_尾差歸協會留存_三者相加必等於捐款金額(
        int amount, decimal storePct, decimal projectPct, int store, int project, int association)
    {
        var (s, p, a) = CharityDonationRules.ComputeSplit(amount, storePct, projectPct);

        Assert.Equal((store, project, association), (s, p, a));
        Assert.Equal(amount, s + p + a);
    }

    [Theory]
    [InlineData(60.0, 41.0)]
    [InlineData(-1.0, 5.0)]
    [InlineData(5.0, -0.01)]
    public void 分潤率合計超過100或為負數_直接拒絕_不會算出負的協會留存(double storePct, double projectPct)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CharityDonationRules.ComputeSplit(1000, (decimal)storePct, (decimal)projectPct));
    }

    [Fact]
    public void 隨機金額下三者永遠加總等於金額()
    {
        var random = new Random(20261001);
        for (var i = 0; i < 2000; i++)
        {
            var amount = random.Next(1, 2_000_000);
            var storePct = Math.Round((decimal)random.NextDouble() * 50m, 2);
            var projectPct = Math.Round((decimal)random.NextDouble() * 50m, 2);
            var (s, p, a) = CharityDonationRules.ComputeSplit(amount, storePct, projectPct);
            Assert.Equal(amount, s + p + a);
            Assert.True(a >= 0 && s >= 0 && p >= 0);
        }
    }

    [Fact]
    public void 單號由冪等鍵決定性推導_同鍵同號_不同鍵或不同秘密不同號()
    {
        const string secret = "unit-test-secret-unit-test-secret-32";
        var a1 = CharityDonationRules.DeriveOrderNo(secret, "key-0000000000000001");
        var a2 = CharityDonationRules.DeriveOrderNo(secret, "key-0000000000000001");
        var b = CharityDonationRules.DeriveOrderNo(secret, "key-0000000000000002");
        var c = CharityDonationRules.DeriveOrderNo(secret + "x", "key-0000000000000001");

        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b);
        Assert.NotEqual(a1, c); // 沒有秘密就算不出單號：單號不能由冪等鍵單獨推出
        Assert.Matches("^CH[0-9A-Z]{16}$", a1);
    }

    [Theory]
    [InlineData("0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("short", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("contains space 0123456789", false)]
    public void 冪等鍵格式(string? key, bool valid) => Assert.Equal(valid, CharityDonationRules.IsValidIdempotencyKey(key));

    [Theory]
    [InlineData("04595257", true)]   // 通過檢核碼
    [InlineData("04595258", false)]  // 末碼錯
    [InlineData("1234567", false)]   // 7 碼
    [InlineData("1234567a", false)]
    [InlineData("00000001", false)]  // 種子裡的測試統編本身不是合法統編
    public void 統一編號檢核碼(string value, bool valid) => Assert.Equal(valid, CharityDonationRules.IsValidTaxId(value));

    [Fact]
    public void 統一編號第七碼為7的例外規則()
    {
        // 10431201：第 7 碼是 0；改用財政部說明中的例外範例 10458575（第 7 碼為 5）無法示範。這裡直接驗證規則：
        // 找一個第 7 碼 = 7 且 sum % 10 == 9 的號碼，必須判為合法。
        var found = false;
        for (var n = 10_000_000; n < 10_000_000 + 2_000_000 && !found; n++)
        {
            var s = n.ToString();
            if (s[6] != '7')
            {
                continue;
            }

            ReadOnlySpan<int> w = [1, 2, 1, 2, 1, 2, 4, 1];
            var sum = 0;
            for (var i = 0; i < 8; i++)
            {
                var prod = (s[i] - '0') * w[i];
                sum += prod / 10 + prod % 10;
            }

            if (sum % 10 == 9)
            {
                Assert.True(CharityDonationRules.IsValidTaxId(s));
                found = true;
            }
        }

        Assert.True(found);
    }

    [Theory]
    [InlineData("A123456789", true)]
    [InlineData("A123456788", false)]
    [InlineData("a123456789", false)]
    [InlineData("A323456789", false)]   // 第二碼只能是 1 2 8 9
    [InlineData("A12345678", false)]
    [InlineData("", false)]
    public void 身分證字號檢核碼(string value, bool valid) => Assert.Equal(valid, CharityDonationRules.IsValidNationalId(value));

    [Theory]
    [InlineData("/ABC+123", true)]
    [InlineData("/ABC.123", true)]
    [InlineData("ABC+1234", false)]  // 缺斜線
    [InlineData("/abc+123", false)]  // 小寫（呼叫端先轉大寫）
    [InlineData("/ABC+12", false)]
    public void 手機條碼格式(string value, bool valid) => Assert.Equal(valid, CharityDonationRules.IsValidMobileCarrier(value));

    [Theory]
    [InlineData("168", true)]
    [InlineData("1234567", true)]
    [InlineData("12", false)]
    [InlineData("12345678", false)]
    [InlineData("12a", false)]
    public void 捐贈碼格式(string value, bool valid) => Assert.Equal(valid, CharityDonationRules.IsValidLoveCode(value));

    [Theory]
    [InlineData("2026-09-05T00:00:00Z", "2026-10-31T00:00:00Z", true)]    // 同為 9–10 月期（以台灣時間算）
    [InlineData("2026-09-05T00:00:00Z", "2026-11-02T00:00:00Z", false)]   // 跨到 11–12 月期
    [InlineData("2026-08-31T20:00:00Z", "2026-09-30T00:00:00Z", true)]    // 台灣時間 9/1 04:00 起算，仍在 9–10 月期
    [InlineData("2026-12-31T20:00:00Z", "2027-01-15T00:00:00Z", true)]    // 跨年：台灣 2027/1/1 起算，與 1/15 同期
    public void 憑證當期判斷以台灣時間的雙月期計算(string issued, string now, bool samePeriod)
    {
        var a = DateTime.Parse(issued, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        var b = DateTime.Parse(now, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(samePeriod, CharityDonationRules.IsSameInvoicePeriod(a, b));
    }

    [Fact]
    public void 載具類型的舊中文標籤正規化回代碼()
    {
        Assert.Equal("mobile_carrier", CarrierTypes.Normalize("手機條碼載具"));
        Assert.Equal("love_code", CarrierTypes.Normalize("捐贈發票"));
        Assert.Equal("tax_id", CarrierTypes.Normalize("統一編號"));
        Assert.Equal("tax_id", CarrierTypes.Normalize("tax_id"));
        Assert.Null(CarrierTypes.Normalize(null));
    }

    [Theory]
    [InlineData("A123456789", "A******789")]
    [InlineData("ab", "***")]
    [InlineData(null, "***")]
    public void 身分證字號遮罩只留首字母與末三碼(string? plain, string expected)
        => Assert.Equal(expected, CharityDataProtector.MaskNationalId(plain));
}
