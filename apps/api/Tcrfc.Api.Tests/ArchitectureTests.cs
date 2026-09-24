using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴🔴🔴 2026-09-23（型別層強制授權，第二輪修正）：純掃原始碼的架構守門測試，
/// 不需要資料庫，不需要行程，跟一般單元測試一樣跑在 <c>dotnet test</c> 裡。
///
/// **第一版（逐行字串比對 `new AdminClubScope(`）被使用者實測兩次戳破**：
/// ① `new Tcrfc.Api.Security.AdminClubScope(...)`（完整命名空間寫法）——`IndexOf("new AdminClubScope(")`
/// 找不到，因為 `new ` 後面接的是 `Tcrfc`。
/// ② `=> default;`——`readonly struct` 的 `default`／`default(T)` 語言規範保證**完全不呼叫任何
/// 建構子**，直接產生零值實例，字串掃描從一開始就不可能抓到「沒有 `new` 這個字」的取值方式。
///
/// **這一版改用 Roslyn 語意模型**，不再比對文字：把 <c>apps/api</c> 底下（排除本測試專案）的
/// 每一個 <c>.cs</c> 檔組成一個真正的 <see cref="CSharpCompilation"/>，對每一個「建構物件」
/// 或「取預設值」的語法節點呼叫 <see cref="SemanticModel.GetTypeInfo"/> 問編譯器「這個運算式
/// 實際上的型別是什麼」——不管原始碼寫的是完整命名空間、using 別名、逐字識別碼
/// （<c>@AdminClubScope</c>）還是任何排版變形，語意模型看到的都是同一個已解析的型別符號，
/// 型別比對本身不會被語法表面形式繞過。
///
/// **`default`／`null` 這條路其實已經在型別本身解掉了，這支測試抓到它只是defense-in-depth**：
/// <see cref="Tcrfc.Api.Security.ClubScope"/>／<see cref="Tcrfc.Api.Security.AdminClubScope"/>
/// 2026-09-23 起改成 <c>sealed class</c> 不是 <c>readonly struct</c>——`class` 的 `default`
/// 是 <c>null</c>，不是一個「看起來合法」的零值物件，任何試圖真的使用偽造出來的 <c>null</c>
/// 會在第一次存取屬性時就丟 <see cref="NullReferenceException"/>，是**立刻爆炸**不是**悄悄繞過**。
/// 這支測試仍然掃 `default`／`null` 是因為語意模型讓這個檢查零誤判成本（不是文字比對，不會
/// 誤觸合法的無關程式碼），多一層總是好的，但**真正解掉這個洞的是型別從 struct 改成 class**，
/// 不是這支測試——這個區分很重要，見 apps/api/README.md「新增後台端點的必要形狀」的完整說明。
///
/// **這支測試真正在守的洞**：在**同一個組件（assembly）內**用任何引數（真實或偽造）呼叫
/// `internal` 建構子（`new AdminClubScope(club, identity)`）——C# 的 `internal` 是組件範圍不是
/// 命名空間範圍，這件事本身沒辦法用型別系統解決，只能靠拆成獨立組件（更大的結構異動，見 README
/// 該節的說明）或這支測試的掃描。**這支測試抓得到**：不管引數是不是真的授權過的值，只要出現
/// `new [任何寫法的] AdminClubScope(...)`／`ClubScope(...)`，就會被記錄下來（見下方八種實測形狀）
/// ——這是 CI／`dotnet test` 層級的防線，不是編譯期擋住，兩者的差別是「寫程式碼的當下有沒有紅色
/// 波浪線」，不是「擋不擋得住」。
///
/// **這支測試沒有涵蓋到的地方（誠實列出邊界，不誇大涵蓋範圍）**：
/// ① **反射繞過建構子的已知 API**（<see cref="System.Activator.CreateInstance(Type)"/>／
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/>／
/// <c>FormatterServices.GetUninitializedObject</c>）另外用「呼叫的方法是不是這三個之一 ＋
/// 引數裡有沒有 <c>typeof(ClubScope)</c>／<c>typeof(AdminClubScope)</c>」的語意比對抓，
/// 這是**有限枚舉**——只涵蓋 .NET 本身公開、文件記載的這幾個「序列化框架專用、故意繞過建構子」
/// 的 API，不是通用的反射防護。② **更底層的手法**（`unsafe` 指標轉型、`Marshal.PtrToStructure`、
/// 手刻 IL 產生器直接 emit 一個物件）完全不在涵蓋範圍內——這類手法在原始碼層級幾乎沒有可辨識的
/// 特徵，純靠原始碼靜態分析做不到，需要更底層的執行期防護（例如簽章驗證），本輪判斷投資報酬率
/// 不夠，沒有做。③ **這份反例清單不是窮舉出來的**，是刻意涵蓋幾個不同「類別」的語法變形
/// （完整命名空間、裸型別名稱、using 別名、逐字識別碼、目標型別 `new()`、`default`、`null`、
/// 反射）來證明「語意解析」這個機制本身對語法表面變形無感——涵蓋廣度的信心來自**解法的性質**
/// （比對已解析的型別符號，不是比對文字），不是來自「想到了所有可能的寫法」，見
/// apps/api/README.md「新增後台端點的必要形狀」對這一點的完整說明。
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenFullyQualifiedNames =
    [
        "Tcrfc.Api.Security.ClubScope",
        "Tcrfc.Api.Security.AdminClubScope",
        // 本輪新增（S1-3 J1／J2／J4）：AdminSystemScope 是同一套「型別層強制授權」的第三個型別，
        // 見 Security/AdminSystemScope.cs 上的說明——沒有理由讓它逃過同一道 Roslyn 語意掃描。
        "Tcrfc.Api.Security.AdminSystemScope",
        // S1-8 新增（列級授權強制）：TeamRowScope 是同一套「型別層強制授權」的第四個型別，
        // 見 Security/TeamRowScope.cs 檔頭「取捨」段——沒有理由讓它逃過同一道 Roslyn 語意掃描。
        "Tcrfc.Api.Security.TeamRowScope",
    ];

    private static string RepoRoot([CallerFilePath] string thisFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(thisFilePath)!;
        return Path.GetFullPath(Path.Combine(testsDir, "..", "..", ".."));
    }

    [Fact]
    public void 除了唯一產生者以外_沒有任何地方能產生ClubScope或AdminClubScope的實例()
    {
        var apiDir = Path.Combine(RepoRoot(), "apps", "api");
        Assert.True(Directory.Exists(apiDir), $"找不到 apps/api 目錄：{apiDir}");

        var allowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(apiDir, "Security", "AdminClubAuthorizer.cs"),  // AdminClubScope 的唯一產生者
            Path.Combine(apiDir, "Security", "ClubResolver.cs"),         // ClubScope 的唯一產生者
            Path.Combine(apiDir, "Security", "AdminSystemAuthorizer.cs"), // AdminSystemScope 的唯一產生者
            Path.Combine(apiDir, "Security", "AdminTeamRowScopeResolver.cs"), // TeamRowScope 的唯一產生者
        };

        var sourceFiles = Directory.EnumerateFiles(apiDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     // 只掃正式程式碼，不掃 Tcrfc.Api.Tests 自己——本測試檔的說明文字與其他測試檔
                     // 的驗證程式碼本身會合法出現 `new AdminClubScope(` 等字樣，跟「production 程式碼
                     // 有沒有繞過授權」無關。
                     && !f.Contains($"{Path.DirectorySeparatorChar}Tcrfc.Api.Tests{Path.DirectorySeparatorChar}"))
            .ToList();

        Assert.True(sourceFiles.Count > 50, $"只掃到 {sourceFiles.Count} 個檔案，遠低於預期——RepoRoot／apiDir 推導可能算錯。");

        var syntaxTrees = sourceFiles
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "ArchitectureScan",
            syntaxTrees: syntaxTrees,
            references: CollectReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var violations = new List<string>();

        foreach (var tree in syntaxTrees)
        {
            if (allowList.Contains(tree.FilePath))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            // ① 明確 `new T(...)`（含完整命名空間、using 別名、逐字識別碼——語意模型一律解析成
            //    同一個型別符號，不管原始碼表面長什麼樣）。對整個建構運算式問型別，不是對它的
            //    TypeSyntax 子節點問——後者在部分情況下拿不到正確的繫結結果（實測過，見下方
            //    「這支測試自己是怎麼驗證出來的」）。
            foreach (var node in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                CheckAndRecordByConvertedType(semanticModel, node, tree.FilePath, violations);
            }

            // ② C# 9 起的目標型別 `new(...)`（例如 `AdminClubScope x = new(club, identity);`）。
            foreach (var node in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                CheckAndRecordByConvertedType(semanticModel, node, tree.FilePath, violations);
            }

            // ③ `default`／`default(T)`——語言規範保證不經過建構子，class 化後雖然已經從「偽造出
            //    看似合法的值」降級成「一定會 NRE」，但語意模型抓這個幾乎零成本，多一層防線。
            foreach (var node in root.DescendantNodes().OfType<LiteralExpressionSyntax>()
                         .Where(n => n.IsKind(SyntaxKind.DefaultLiteralExpression) || n.IsKind(SyntaxKind.NullLiteralExpression)))
            {
                CheckAndRecordByConvertedType(semanticModel, node, tree.FilePath, violations, label: node.IsKind(SyntaxKind.NullLiteralExpression) ? "null" : "default");
            }

            foreach (var node in root.DescendantNodes().OfType<DefaultExpressionSyntax>())
            {
                CheckAndRecord(semanticModel, node, node.Type, tree.FilePath, violations, label: "default(...)");
            }

            // ④ 反射繞過建構子的已知 API——這幾個是 .NET 本身給序列化框架用的「不呼叫任何建構子
            //    就生出一個型別實例」管道，純語法／語意層級的檢查（①～③）天生看不到「這是在建構
            //    ClubScope」，因為呼叫端看起來只是一般的方法呼叫。這裡另外比對呼叫的方法是否為
            //    這三個已知 API 之一，且引數裡有 `typeof(ClubScope)`／`typeof(AdminClubScope)`。
            //    ⚠️ 這是**有限枚舉**，不是通用防護——見本測試類別上「這支測試沒有涵蓋到的地方」。
            foreach (var node in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                CheckReflectionBypass(semanticModel, node, tree.FilePath, violations);
            }
        }

        Assert.True(violations.Count == 0,
            "發現不在允許清單內的地方能產生 ClubScope／AdminClubScope 的實例，這會繞過授權：\n"
            + string.Join("\n", violations));
    }

    private static readonly (string Type, string Method)[] KnownConstructorBypassApis =
    [
        ("System.Activator", "CreateInstance"),
        ("System.Runtime.CompilerServices.RuntimeHelpers", "GetUninitializedObject"),
        ("System.Runtime.Serialization.FormatterServices", "GetUninitializedObject"),
    ];

    private static void CheckReflectionBypass(
        SemanticModel semanticModel, InvocationExpressionSyntax invocation, string filePath, List<string> violations)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        var containingType = method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);
        var isKnownBypassApi = KnownConstructorBypassApis
            .Any(api => api.Type == containingType && api.Method == method.Name);
        if (!isKnownBypassApi)
        {
            return;
        }

        // 找呼叫裡的 typeof(...) 引數，看目標型別是不是我們要擋的兩個之一。
        foreach (var typeOfExpr in invocation.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
            RecordIfForbidden(typeInfo.Type, invocation, filePath, violations, label: $"reflection:{method.Name}");
        }
    }

    private static void CheckAndRecord(
        SemanticModel semanticModel, SyntaxNode node, TypeSyntax typeSyntax, string filePath, List<string> violations, string? label = null)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
        var resolved = typeInfo.Type ?? typeInfo.ConvertedType;
        RecordIfForbidden(resolved, node, filePath, violations, label);
    }

    private static void CheckAndRecordByConvertedType(
        SemanticModel semanticModel, SyntaxNode node, string filePath, List<string> violations, string? label = null)
    {
        var typeInfo = semanticModel.GetTypeInfo(node);
        var resolved = typeInfo.ConvertedType ?? typeInfo.Type;
        RecordIfForbidden(resolved, node, filePath, violations, label);
    }

    private static void RecordIfForbidden(ITypeSymbol? resolved, SyntaxNode node, string filePath, List<string> violations, string? label)
    {
        if (resolved is null)
        {
            return;
        }

        // FullyQualifiedFormat 開頭會帶 "global::"，比對時去掉前綴，比對到我們關心的兩個型別名稱即可。
        var normalized = resolved.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

        if (!ForbiddenFullyQualifiedNames.Contains(normalized))
        {
            return;
        }

        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        violations.Add($"{filePath}:{lineNumber}: [{label ?? "new"}] {node.ToString().Trim()}");
    }

    /// <summary>
    /// 組一個「足以做語意分析」的參考組件清單——不是完整的 MSBuild 還原，用的是測試行程本身
    /// 已經載入的組件（xunit 行程本來就已經載入 Tcrfc.Api.dll 與它的全部相依組件，因為
    /// Tcrfc.Api.Tests 專案本來就參照 Tcrfc.Api.csproj），這是常見的「輕量組出可分析編譯」寫法，
    /// 不需要另外拉一份 MSBuildWorkspace。
    /// </summary>
    private static List<MetadataReference> CollectReferences()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
}
