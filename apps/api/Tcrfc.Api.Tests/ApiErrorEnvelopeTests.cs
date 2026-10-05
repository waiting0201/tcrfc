using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 統一錯誤結構（App 規劃書 §9.5「代碼、雙語訊息、是否可重試」）：所有錯誤本文都有 <c>code</c>／<c>messageZh</c>／<c>messageEn</c>／<c>retryable</c>，
/// 既有欄位（status／title／detail／instance、會員一族的 code）不變。涵蓋三條產生路徑：例外處理器、空本文（UseStatusCodePages）、
/// 自組 ProblemDetails；429 見 <see cref="AdminAuthRateLimitingTests"/>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class ApiErrorEnvelopeTests(AdminWriteApiFixture fixture)
{
    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        // 例外處理器沿用既有的 application/json，UseStatusCodePages 為 application/problem+json；兩者本文結構相同。
        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.Ordinal);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertEnvelope(JsonElement p, int status, string code, bool retryable)
    {
        Assert.Equal(status, p.GetProperty("status").GetInt32());
        Assert.Equal(code, p.GetProperty("code").GetString());
        Assert.Equal(p.GetProperty("detail").GetString(), p.GetProperty("messageZh").GetString()); // messageZh 恆等於 detail
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("messageEn").GetString()));
        Assert.Equal(retryable, p.GetProperty("retryable").GetBoolean());
        Assert.True(p.TryGetProperty("title", out _)); // 既有欄位仍在
        Assert.True(p.TryGetProperty("instance", out _));
    }

    [Fact]
    public async Task 例外路徑_未知俱樂部404_沒有專屬代碼就用狀態通用代碼()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/nope/players");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertEnvelope(await ProblemAsync(response), 404, "not_found", retryable: false);
    }

    [Fact]
    public async Task 空本文路徑_Results_NotFound也補成統一結構()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/tcrfc/news/zz-no-such-article-slug");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertEnvelope(await ProblemAsync(response), 404, "not_found", retryable: false);

        // 路由根本不存在（框架直接回 404，沒有任何端點處理）
        var unrouted = await client.GetAsync("/api/v1/tcrfc/zz-unrouted-path");
        Assert.Equal(HttpStatusCode.NotFound, unrouted.StatusCode);
        AssertEnvelope(await ProblemAsync(unrouted), 404, "not_found", retryable: false);
    }

    [Fact]
    public async Task 會員一族_專屬代碼保留_並補上雙語與不可重試()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/member/auth/login", new { email = "nobody@example.test", password = "Wrong-pass1" }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var p = await ProblemAsync(response);
        AssertEnvelope(p, 401, "invalid_credentials", retryable: false);
        Assert.Equal("The email or password is incorrect.", p.GetProperty("messageEn").GetString());
    }

    [Fact]
    public async Task 後台一族_沒有專屬代碼_401用通用代碼()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/players");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertEnvelope(await ProblemAsync(response), 401, "unauthenticated", retryable: false);
    }

    [Theory]
    [InlineData(400, "validation_failed", false)]
    [InlineData(403, "forbidden", false)]
    [InlineData(404, "not_found", false)]
    [InlineData(409, "conflict", false)]
    [InlineData(423, "account_locked", false)]
    [InlineData(429, "rate_limited", false)]
    [InlineData(500, "server_error", true)]
    [InlineData(502, "server_error", true)]
    [InlineData(503, "service_unavailable", true)]
    public void Fill_各狀態的代碼與可重試_依規劃書9_5(int status, string code, bool retryable)
    {
        var problem = new ProblemDetails { Status = status };
        ApiErrorEnvelope.Fill(problem, status);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.Equal(retryable, problem.Extensions["retryable"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)problem.Extensions["messageZh"]));
        Assert.False(string.IsNullOrWhiteSpace((string?)problem.Extensions["messageEn"]));
    }

    [Fact]
    public void Fill_已有專屬代碼不覆蓋_尚未設定的503不可重試()
    {
        var problem = new ProblemDetails { Status = 503, Detail = "LINE 登入尚未啟用，請先使用 Email 登入。" };
        problem.Extensions["code"] = "line_not_configured";
        ApiErrorEnvelope.Fill(problem, 503);
        Assert.Equal("line_not_configured", problem.Extensions["code"]);
        Assert.Equal(false, problem.Extensions["retryable"]);
        Assert.Equal("LINE 登入尚未啟用，請先使用 Email 登入。", problem.Extensions["messageZh"]);
        Assert.Equal("LINE sign-in is not enabled. Please sign in with your email first.", problem.Extensions["messageEn"]);

        // 暫時性的 503（地址定位服務暫時不可用）可重試
        var transient = new ProblemDetails { Status = 503, Detail = "x" };
        transient.Extensions["code"] = "geocoder_unavailable";
        ApiErrorEnvelope.Fill(transient, 503);
        Assert.Equal(true, transient.Extensions["retryable"]);
    }

    [Fact]
    public void Fill_沒登記英文的代碼退回該狀態的通用英文()
    {
        var problem = new ProblemDetails { Status = 409, Detail = "某訊息" };
        problem.Extensions["code"] = "zz_unregistered_code";
        ApiErrorEnvelope.Fill(problem, 409);
        Assert.Equal(ApiErrorMessages.GenericEnglish(409), problem.Extensions["messageEn"]);
    }
}
