using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.CharityPlatform.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 慈善平台的架構守門測試（純掃原始碼＋反射，不需要資料庫）。守住的是「改錯了不會被任何功能測試發現」的結構性約束：
/// 授權不可繞過、公開寫入端點一律限流、慈善與主站互不相依（獨立後台、獨立資料庫）、慈善完全不碰快取、稽核 append-only、
/// 公開與後台回應不洩漏不該出現的欄位。
/// </summary>
public sealed class CharityArchitectureTests
{
    /// <summary>repo 根目錄（與 <c>ArchitectureTests.RepoRoot</c> 同一個推導）；下面都以 <c>apps/api/...</c> 往下接。</summary>
    private static string ApiDir([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    private static string CharityDir() => Path.Combine(ApiDir(), "apps", "api", "CharityPlatform");

    private static IEnumerable<string> CharitySources()
        => Directory.EnumerateFiles(CharityDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Entities{Path.DirectorySeparatorChar}")
                        && !f.EndsWith("CharityDbContext.cs", StringComparison.Ordinal));

    // ═══════════════════════════════════════════════════════════════════════
    // 授權
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly HashSet<string> MapMethods = new(StringComparer.Ordinal) { "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch" };

    private static IEnumerable<(InvocationExpressionSyntax Call, string Route, SyntaxTree Tree)> RouteCalls(string file)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
        foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: var name } && MapMethods.Contains(name)
                && invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal)
            {
                yield return (invocation, literal.Token.ValueText, tree);
            }
        }
    }

    private static bool LambdaCalls(InvocationExpressionSyntax mapCall, params string[] methodNames)
    {
        var lambda = mapCall.ArgumentList.Arguments.Select(a => a.Expression).OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault();
        return lambda is not null && lambda.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(i => i.Expression is MemberAccessExpressionSyntax m && methodNames.Contains(m.Name.Identifier.Text));
    }

    [Fact]
    public void 慈善後台每一支端點都必須先通過授權器_沒有任何端點能略過()
    {
        var file = Path.Combine(CharityDir(), "Admin", "CharityAdminEndpoints.cs");
        var routes = RouteCalls(file).ToList();
        Assert.True(routes.Count >= 25, $"只掃到 {routes.Count} 支後台端點，遠低於預期——檔案結構可能已改變，需要同步更新這支測試。");

        var violations = routes
            .Where(r => !LambdaCalls(r.Call, "AuthorizeAsync", "AuthorizeAnyAsync"))
            .Select(r => $"{r.Tree.FilePath}:{r.Tree.GetLineSpan(r.Call.Span).StartLinePosition.Line + 1}: 路由 \"{r.Route}\" 沒有呼叫 AuthorizeAsync／AuthorizeAnyAsync")
            .ToList();

        Assert.True(violations.Count == 0, "慈善後台端點沒有經過授權器：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void 慈善後台登入檔案_除了login_refresh_logout三支以外都必須確認已登入_login與refresh必須掛限流()
    {
        var file = Path.Combine(CharityDir(), "Auth", "CharityAdminAuthEndpoints.cs");
        var routes = RouteCalls(file).ToList();
        var unauthenticated = new HashSet<string> { "/login", "/refresh", "/logout" };
        Assert.Equal(8, routes.Count);

        var violations = new List<string>();
        foreach (var (call, route, tree) in routes)
        {
            if (!unauthenticated.Contains(route) && !LambdaCalls(call, "RequireSignedInAsync"))
            {
                violations.Add($"路由 \"{route}\" 沒有呼叫 RequireSignedInAsync");
            }

            if (route is "/login" or "/refresh" && !HasRateLimit(call))
            {
                violations.Add($"路由 \"{route}\" 沒有掛 RequireRateLimiting（呼叫當下不需要先登入，必須依 IP 限流）");
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void 慈善公開端點的寫入呼叫一律掛限流政策_結果頁輪詢也要限流()
    {
        var file = Path.Combine(CharityDir(), "Public", "CharityPublicEndpoints.cs");
        var routes = RouteCalls(file).ToList();
        Assert.True(routes.Count >= 9);
        var writeCalls = new HashSet<string> { "MapPost", "MapPut", "MapDelete", "MapPatch" };

        var violations = routes
            .Where(r => writeCalls.Contains(((MemberAccessExpressionSyntax)r.Call.Expression).Name.Identifier.Text) && !HasRateLimit(r.Call))
            .Select(r => $"POST/PUT/DELETE \"{r.Route}\" 沒有掛 RequireRateLimiting")
            .ToList();
        // 單筆查詢（結果頁）雖然是 GET，但單號是公開端點上唯一的「憑證」，必須擋大量枚舉。
        violations.AddRange(routes
            .Where(r => r.Route.StartsWith("/donations/{orderNo}", StringComparison.Ordinal) && r.Route == "/donations/{orderNo}" && !HasRateLimit(r.Call))
            .Select(r => $"GET \"{r.Route}\" 沒有掛 RequireRateLimiting"));

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    private static bool HasRateLimit(InvocationExpressionSyntax mapCall)
    {
        SyntaxNode current = mapCall;
        while (current.Parent is MemberAccessExpressionSyntax member && member.Expression == current
               && member.Parent is InvocationExpressionSyntax outer && outer.Expression == member)
        {
            if (member.Name.Identifier.Text == "RequireRateLimiting")
            {
                return true;
            }

            current = outer;
        }

        return false;
    }

    [Fact]
    public void 慈善後台三個service的公開方法第一個參數都是CharityAdminScope_型別層強制授權()
    {
        // 方法清單用反射抓：新增公開方法忘了要求 scope，這支測試會變紅。
        // 例外：純計算的 BuildQrTargetUrl（不碰資料）。
        var exempt = new HashSet<string> { "BuildQrTargetUrl" };
        var violations = new List<string>();
        foreach (var type in new[] { typeof(CharityStoresAdminService), typeof(CharityProjectsAdminService), typeof(CharityDonationsAdminService) })
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (exempt.Contains(method.Name) || method.IsSpecialName)
                {
                    continue;
                }

                if (method.GetParameters().FirstOrDefault()?.ParameterType != typeof(CharityAdminScope))
                {
                    violations.Add($"{type.Name}.{method.Name} 的第一個參數不是 CharityAdminScope");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void CharityAdminScope只有授權器能建立_全專案掃描()
    {
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(ApiDir(), "apps", "api"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}Tcrfc.Api.Tests{Path.DirectorySeparatorChar}")))
        {
            if (file.EndsWith($"CharityPlatform{Path.DirectorySeparatorChar}Security{Path.DirectorySeparatorChar}CharityAdminAuthorizer.cs", StringComparison.Ordinal)
                || file.EndsWith("CharityAdminScope.cs", StringComparison.Ordinal))
            {
                continue;
            }

            // 任何寫法的 new CharityAdminScope(...)／default(CharityAdminScope)——語意層級的完整掃描由 ArchitectureTests 負責，
            // 這裡是最後一道便宜的文字防線（含完整命名空間與 using 別名寫法）。
            if (Regex.IsMatch(File.ReadAllText(file), @"new\s+(?:[\w.]+\.)?CharityAdminScope\s*\(|default\s*\(\s*(?:[\w.]+\.)?CharityAdminScope\s*\)"))
            {
                violations.Add(file);
            }
        }

        Assert.True(violations.Count == 0, "這些檔案建立了 CharityAdminScope：\n" + string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 獨立性
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void 慈善平台不碰主站資料庫_不讀快取_只借用兩個無狀態的主站共用型別()
    {
        string[] forbidden =
        [
            "ClubDbContext", "Tcrfc.Api.Data.EfEntities", "IClubSqlConnectionFactory", "CLUB_SQL_CONNECTION_STRING",
            "IQueryCache", "IConnectionMultiplexer", "StackExchange.Redis", "IClubResolver", "ClubScope",
        ];
        // 允許借用的主站型別：登入請求／回應的純 DTO 與驗證碼（Features.AdminAuth）、multipart 解析（Features.Uploads）。
        var allowedFeatureNamespaces = new[] { "Tcrfc.Api.Features.AdminAuth", "Tcrfc.Api.Features.Uploads" };
        var violations = new List<string>();

        foreach (var file in CharitySources())
        {
            var text = File.ReadAllText(file);
            foreach (var word in forbidden.Where(text.Contains))
            {
                // 說明文字（註解）可以提到這些名字；只有程式碼裡出現才算違規。
                var code = string.Join("\n", text.Split('\n').Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///") && !l.TrimStart().StartsWith("*")));
                if (code.Contains(word))
                {
                    violations.Add($"{Path.GetFileName(file)} 使用了 {word}");
                }
            }

            foreach (Match m in Regex.Matches(text, @"using\s+(Tcrfc\.Api\.Features\.[\w.]+);"))
            {
                if (!allowedFeatureNamespaces.Contains(m.Groups[1].Value))
                {
                    violations.Add($"{Path.GetFileName(file)} 使用了主站的 {m.Groups[1].Value}");
                }
            }
        }

        Assert.True(violations.Count == 0, "慈善平台與主站互相依賴（慈善是獨立後台、獨立資料庫）：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void 主站程式碼不依賴慈善平台_只有Program與例外處理器與健康檢查接縫()
    {
        var violations = new List<string>();
        foreach (var dir in new[] { "Features", "Security", "Data", "Caching" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(ApiDir(), "apps", "api", dir), "*.cs", SearchOption.AllDirectories)
                         .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            {
                if (File.ReadAllText(file).Contains("CharityPlatform", StringComparison.Ordinal))
                {
                    violations.Add(file);
                }
            }
        }

        Assert.True(violations.Count == 0, "主站程式碼引用了慈善平台：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void 慈善命名空間不得與實體同名_避免遮蔽主站的Charity實體()
    {
        // docs/18：功能命名空間不得等於實體名稱。主站有 Tcrfc.Api.Data.EfEntities.Charity；慈善的命名空間是 CharityPlatform，不是 Charity。
        foreach (var file in CharitySources())
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(@"namespace\s+Tcrfc\.Api\.Charity\s*[;{.]", text);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 稽核 append-only
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void 稽核紀錄append_only_沒有更新與刪除路徑_實體沒有updated欄位()
    {
        Assert.Null(typeof(AuditLog).GetProperty("UpdatedAt"));
        Assert.Null(typeof(AuditLog).GetProperty("UpdatedBy"));
        Assert.Null(typeof(AuditLog).GetProperty("CreatedAt"));
        Assert.Null(typeof(AuditLog).GetProperty("CreatedBy"));

        var publicMethods = typeof(CharityAuditLogger).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).ToList();
        Assert.Equal(["Stage"], publicMethods);

        var violations = CharitySources()
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"AuditLogs\s*\.\s*(Remove|RemoveRange|Update|UpdateRange|ExecuteDelete|ExecuteUpdate)|AuditLogs[^;]*\.\s*(ExecuteDelete|ExecuteUpdate)"))
            .ToList();
        Assert.True(violations.Count == 0, "有程式碼更新或刪除稽核紀錄：\n" + string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 回應形狀
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void 公開回應的型別沒有分潤_加密欄位_金流交易識別碼_原始回應_物件鍵()
    {
        var banned = new[] { "Pct", "Share", "Encrypted", "TransactionId", "RawResponse", "PasswordHash", "Secret" };
        var violations = new List<string>();
        foreach (var type in typeof(PublicSettingsDto).Assembly.GetTypes()
                     .Where(t => t.Namespace == typeof(PublicSettingsDto).Namespace && t.IsPublic && (t.Name.StartsWith("Public") || t.Name is "CreateDonationResponse" or "StartPaymentResponse")))
        {
            foreach (var property in type.GetProperties())
            {
                if (banned.Any(b => property.Name.Contains(b, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{type.Name}.{property.Name}");
                }

                if (property.Name.EndsWith("Key", StringComparison.Ordinal) && property.PropertyType == typeof(string))
                {
                    violations.Add($"{type.Name}.{property.Name}（物件鍵一律換成完整網址）");
                }
            }
        }

        Assert.True(violations.Count == 0, "公開回應含有不該出現的欄位：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void 後台回應的型別沒有密文欄位_金流原始回應_密碼雜湊()
    {
        var banned = new[] { "Encrypted", "RawResponse", "PasswordHash" };
        var violations = new List<string>();
        foreach (var type in typeof(AdminDonationDetailDto).Assembly.GetTypes()
                     .Where(t => t.Namespace == typeof(AdminDonationDetailDto).Namespace && t.IsPublic && t.Name.StartsWith("Admin")))
        {
            violations.AddRange(type.GetProperties()
                .Where(p => banned.Any(b => p.Name.Contains(b, StringComparison.OrdinalIgnoreCase)))
                .Select(p => $"{type.Name}.{p.Name}"));
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 假實作的環境防線
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Config(string? allowFake = null)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [CharityFakeGuard.AllowFakeConfigKey] = allowFake }).Build();

    [Fact]
    public async Task 假金流與假發票與假寄信_在正式環境一律拒絕運作_不會憑空確認收款或開立憑證()
    {
        var production = new FakeEnv("Production");
        var config = Config();

        await Assert.ThrowsAsync<PaymentGatewayNotConfiguredException>(() =>
            new FakePaymentGateway(production, config).RequestPaymentAsync(new PaymentRequest("CHX", 1, 100, "p", "zh", "u", "u"), default));
        await Assert.ThrowsAsync<PaymentGatewayNotConfiguredException>(() =>
            new FakePaymentGateway(production, config).ConfirmPaymentAsync(new PaymentConfirmRequest("CHX", "FAKE-1", 100), default));
        await Assert.ThrowsAsync<PaymentGatewayNotConfiguredException>(() =>
            new FakePaymentGateway(production, config).RefundPaymentAsync(new PaymentRefundRequest("CHX", "FAKE-1", 100), default));
        await Assert.ThrowsAsync<InvoiceIssuerNotConfiguredException>(() =>
            new FakeInvoiceIssuer(production, config).IssueAsync(new InvoiceIssueRequest(InvoiceKind.B2cInvoice, "CHX", 100, "TEST", "n", null, null, null, null, null, null, false), default));
        await Assert.ThrowsAsync<EmailSendException>(() =>
            new FakeEmailSender(production, config, Microsoft.Extensions.Logging.Abstractions.NullLogger<FakeEmailSender>.Instance)
                .SendAsync(new EmailMessage("donation_thanks", "a@b.c", "s", "b"), default));
    }

    [Fact]
    public async Task 假實作在Development或明確允許時可用_FAKE_DECLINE開頭的交易會被拒絕()
    {
        var dev = new FakePaymentGateway(new FakeEnv("Development"), Config());
        var allowedInStaging = new FakePaymentGateway(new FakeEnv("Staging"), Config("true"));

        var requested = await dev.RequestPaymentAsync(new PaymentRequest("CHX", 1, 100, "p", "en", "u", "u"), default);
        Assert.Contains("/en/pay/CHX?transactionId=", requested.PaymentUrl);
        Assert.Equal(PaymentConfirmOutcome.Confirmed, (await dev.ConfirmPaymentAsync(new PaymentConfirmRequest("CHX", requested.TransactionId, 100), default)).Outcome);
        Assert.Equal(PaymentConfirmOutcome.Declined, (await dev.ConfirmPaymentAsync(new PaymentConfirmRequest("CHX", "FAKE-DECLINE-1", 100), default)).Outcome);
        Assert.Equal(PaymentConfirmOutcome.Declined, (await dev.ConfirmPaymentAsync(new PaymentConfirmRequest("CHX", "REAL-LOOKING-TX", 100), default)).Outcome);
        Assert.True((await allowedInStaging.RefundPaymentAsync(new PaymentRefundRequest("CHX", "FAKE-1", 100), default)).Succeeded);

        var invoice = await new FakeInvoiceIssuer(new FakeEnv("Development"), Config())
            .IssueAsync(new InvoiceIssueRequest(InvoiceKind.B2cInvoice, "CHABC", 100, "TEST", "n", null, null, null, null, null, null, false), default);
        Assert.Equal("TEST-CHABC", invoice.InvoiceNo); // 同單號同憑證：天然冪等
    }
}
