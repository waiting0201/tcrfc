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
        // Type 與 ConvertedType 兩個都要看：`object s = new AdminClubScope(...)` 的 ConvertedType 是
        // object、Type 才是 AdminClubScope；`default`／`null` 字面值則反過來只有 ConvertedType。
        // 只看其中一個曾讓「指派給 object 的偽造建構」整個漏掉（2026-09-24 主 session 以實際違規複驗抓到）。
        var typeInfo = semanticModel.GetTypeInfo(node);
        var resolved = IsForbidden(typeInfo.Type) ? typeInfo.Type : typeInfo.ConvertedType;
        RecordIfForbidden(resolved, node, filePath, violations, label);
    }

    private static bool IsForbidden(ITypeSymbol? type) =>
        type is not null
        && ForbiddenFullyQualifiedNames.Contains(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty));

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

    /// <summary>
    /// 🔴 E-64（2026-09-29）：純掃原始碼的語法守門測試，不需要資料庫。<c>docs/18-work-errors.md</c>
    /// E-64 記過 <c>Features/Home/HomeRepository.ListBannersAsync</c> 把 <c>banners.image_key</c>
    /// 這個 Blob 物件鍵原封不動塞進公開 DTO，前台拿到手完全無法組出可用網址——跟
    /// <c>Features/Staff</c>／<c>Features/Players</c> 等既有端點「<c>XxxKey</c> 原始鍵 ＋
    /// <c>XxxUrl</c> 經 <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/>／
    /// <see cref="Tcrfc.Api.Videos.IVideoPublicUrlResolver"/> 解析出的完整網址」成對出現的慣例
    /// 不一致。這支測試把「成對出現」這件事變成可以自動驗證的形狀約束，不必每次新增欄位都要
    /// 靠人工比對既有寫法：只掃**公開**端點（<c>Features/*</c> 排除 <c>Features/Admin*</c>）用來
    /// 對外輸出的 DTO（型別名稱以 <c>Dto</c> 結尾的 <c>public</c> record），找每一個名稱以
    /// <c>Key</c> 結尾、型別是 <c>string</c>／<c>string?</c> 的屬性，要求同一個 record 裡
    /// 一定有一個 <c>{去掉 Key 的字首}Url</c> 屬性存在——不驗證那個 <c>Url</c> 屬性實際上有沒有
    /// 正確呼叫解析器（那需要語意層級追蹤資料流，投資報酬率不夠，見下方「涵蓋邊界」），只驗證
    /// 「這個形狀存在」，跟 <c>SchemaEligible</c> 一類欄位靠命名慣例互相對照的既有寫法同一種精神。
    ///
    /// **只掃公開端點，不含後台**：<c>Features/Admin*</c>（例如 <c>AdminBannerListItemDto.ImageKey</c>）
    /// 目前也是同一種「只有原始鍵、沒有解析網址」的形狀，但那是後台畫面的既有落差、不是這次
    /// E-64 的範圍（E-64 只點名公開讀取端點），刻意不在這支測試裡一併擋——見
    /// <c>apps/api/README.md</c>「E-64」節與 <c>docs/18-work-errors.md</c> 的範圍說明，日後若要
    /// 把後台一併納管，是新的一輪工作、不是這支測試該默默擴大範圍去做的事。
    ///
    /// **涵蓋邊界（誠實列出，不誇大）**：① 只驗證「有沒有同名的 <c>Url</c> 屬性」，不驗證
    /// repository 的 <c>Map</c> 方法有沒有真的把它接上解析器、也不驗證接的是不是正確的解析器
    /// （例如誤把 <c>VideoKey</c> 接去 <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/>）——
    /// 這件事的正確性目前只靠 <c>PublicSchemaEligibilityTests</c> 一類的整合測試個案驗證，
    /// 不是這支測試的職責。② 命名比對是純字串規則（去掉字尾 <c>Key</c>、接上 <c>Url</c>），
    /// 不是語意分析，跟本檔上方那支測試刻意用 Roslyn 語意模型抓型別身分不同——這裡要抓的是
    /// 「這一組欄位命名有沒有成對」，字串規則本來就足夠、也更容易讀懂哪裡不成對。
    /// </summary>
    [Fact]
    public void 公開DTO的物件鍵欄位都必須有對應的完整網址欄位()
    {
        var apiDir = Path.Combine(RepoRoot(), "apps", "api");
        Assert.True(Directory.Exists(apiDir), $"找不到 apps/api 目錄：{apiDir}");

        var featuresDir = Path.Combine(apiDir, "Features");
        Assert.True(Directory.Exists(featuresDir), $"找不到 apps/api/Features 目錄：{featuresDir}");

        // 已核對過的既有例外，逐項寫清楚理由，鍵是「record 型別名稱.屬性名稱」（不是只比對屬性
        // 名稱本身）——避免日後某個新 DTO 剛好也叫這個屬性名稱時被誤放行，見下方 violations 比對。
        var knownExceptions = new HashSet<string>(StringComparer.Ordinal)
        {
            // Features/Forms/FormDtos.cs：表單自訂欄位的識別碼（對應 form_fields.field_key），
            // 是「這一題的代號」，不是 Blob 物件鍵，沒有網址可以解析。
            "PublicFormFieldDto.FieldKey",
            // Features/Clubs/ClubDto.cs：LogoLightKey 確實有解析成網址，只是既有欄位刻意命名為
            // LogoUrl 不是 LogoLightUrl（clubs 目前只有一種要顯示的隊徽變體，見
            // ClubsRepository.Map／ClubDto.LogoUrl 檔頭說明）——是命名不對稱，不是漏解析。
            "ClubDto.LogoLightKey",
        };

        var sourceFiles = Directory.EnumerateFiles(featuresDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !IsUnderAdminFeature(f, featuresDir))
            .ToList();

        Assert.True(sourceFiles.Count > 5, $"只掃到 {sourceFiles.Count} 個公開端點檔案，遠低於預期——路徑篩選可能算錯。");

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
            var root = tree.GetRoot();

            foreach (var record in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                var isPublic = record.Modifiers.Any(SyntaxKind.PublicKeyword);
                var isDto = record.Identifier.Text.EndsWith("Dto", StringComparison.Ordinal);
                if (!isPublic || !isDto)
                {
                    continue;
                }

                var propertyNames = record.Members.OfType<PropertyDeclarationSyntax>()
                    .Select(p => p.Identifier.Text)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var property in record.Members.OfType<PropertyDeclarationSyntax>())
                {
                    var name = property.Identifier.Text;
                    if (!name.EndsWith("Key", StringComparison.Ordinal)
                        || knownExceptions.Contains($"{record.Identifier.Text}.{name}"))
                    {
                        continue;
                    }

                    var typeText = property.Type.ToString();
                    if (typeText != "string" && typeText != "string?")
                    {
                        continue;
                    }

                    var expectedUrlProperty = name[..^"Key".Length] + "Url";
                    if (!propertyNames.Contains(expectedUrlProperty))
                    {
                        var lineNumber = tree.GetLineSpan(property.Span).StartLinePosition.Line + 1;
                        violations.Add(
                            $"{file}:{lineNumber}: {record.Identifier.Text}.{name} 沒有對應的 {expectedUrlProperty} 屬性——" +
                            "公開 DTO 不應該只回傳未解析的物件鍵（E-64）。");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            "發現公開 DTO 直接輸出未解析的物件鍵（E-64 同類缺口），前台拿到手無法組出可用網址：\n"
            + string.Join("\n", violations));
    }

    /// <summary>判斷檔案是否位於 <c>Features/AdminXxx/</c> 之下——只看 <c>Features</c> 底下的
    /// 第一層目錄名稱是否以 <c>Admin</c> 開頭，比對邏輯跟 <c>docs/18-work-errors.md</c> E-27
    /// 記過的「萬用字元只擋單層」是同一個坑，這裡改用逐層目錄名稱比對，不用字串前綴湊。</summary>
    private static bool IsUnderAdminFeature(string filePath, string featuresDir)
    {
        var relative = Path.GetRelativePath(featuresDir, filePath);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, 2)[0];
        return firstSegment.StartsWith("Admin", StringComparison.Ordinal);
    }

    /// <summary>
    /// S1-18c（2026-09-29，補齊公開寫入端點的限流缺口）：純掃原始碼的架構守門測試，跟本檔前兩支
    /// 測試同一種精神——掃描 <c>Features/*</c>（排除 <c>Features/AdminXxx</c>，理由同上一支測試
    /// 共用的 <see cref="IsUnderAdminFeature"/> 判斷：公開端點全部「不需要登入」，後台端點另外走
    /// <c>IAdminClubAuthorizer</c>／<c>IAdminSystemAuthorizer</c> 一組完全不同的防護，不是這支
    /// 測試要守的洞），找每一個 <c>MapPost</c>／<c>MapPut</c>／<c>MapDelete</c>／<c>MapPatch</c>
    /// 呼叫，要求它所在的 fluent chain（<c>.WithName(...)</c>／<c>.Produces(...)</c> 那一長串）
    /// 裡一定要有 <c>.RequireRateLimiting(...)</c>，否則記一筆違規——這是
    /// docs/14-invariants.md「公開寫入端點一律限流」這條不變量的自動化防呆：往後任何人在
    /// <c>Features/*</c>（非 Admin）新增一個公開非 GET 端點卻忘記掛限流，<c>dotnet test</c>
    /// 會直接失敗，不必等到真的被灌爆才發現。
    ///
    /// **判斷「公開」的依據是檔案所在資料夾，不是逐一檢查 handler 內有沒有呼叫授權方法**：
    /// 本專案目前每一個 <c>Features/*Endpoints.cs</c>（非 Admin）檔案開頭的 XML 文件註解都明文
    /// 「全部不需要登入」，是刻意的架構切分（後台一律在 <c>Features/AdminXxx</c> 底下、一律經過
    /// 型別層強制授權，見本檔第一支測試）；用資料夾判斷比較穩定，不會因為某個 handler 剛好呼叫了
    /// 什麼方法名稱而誤判。
    ///
    /// **涵蓋邊界**：只驗證「chain 裡有沒有出現 <c>RequireRateLimiting</c> 呼叫」，不驗證掛的是
    /// 哪一個政策、政策的額度是否合理——額度合理性是
    /// <c>Tcrfc.Api.Tests.PublicRateLimitPoliciesTests</c> 的職責，這支測試只守「有沒有掛」這個
    /// 最低門檻。
    /// </summary>
    [Fact]
    public void 公開端點的非GET寫入呼叫都必須掛限流政策()
    {
        var apiDir = Path.Combine(RepoRoot(), "apps", "api");
        var featuresDir = Path.Combine(apiDir, "Features");
        Assert.True(Directory.Exists(featuresDir), $"找不到 apps/api/Features 目錄：{featuresDir}");

        var sourceFiles = Directory.EnumerateFiles(featuresDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !IsUnderAdminFeature(f, featuresDir))
            .ToList();

        Assert.True(sourceFiles.Count > 5, $"只掃到 {sourceFiles.Count} 個公開端點檔案，遠低於預期——路徑篩選可能算錯。");

        var syntaxTrees = sourceFiles
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "RateLimitScan",
            syntaxTrees: syntaxTrees,
            references: CollectReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var writeMethodNames = new HashSet<string>(StringComparer.Ordinal) { "MapPost", "MapPut", "MapDelete", "MapPatch" };
        var violations = new List<string>();

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is not IMethodSymbol method || !writeMethodNames.Contains(method.Name))
                {
                    continue;
                }

                // 確認這是 ASP.NET Core Minimal API 路由對映方法，不是巧合同名的其他方法——
                // 這幾個方法一律定義在 Microsoft.AspNetCore 底下的擴充方法類別。
                var containingNamespace = method.ContainingType?.ContainingNamespace?.ToDisplayString();
                if (containingNamespace is null || !containingNamespace.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ChainHasRequireRateLimiting(invocation))
                {
                    var lineNumber = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                    violations.Add(
                        $"{tree.FilePath}:{lineNumber}: {method.Name} 呼叫沒有掛 .RequireRateLimiting(...)——" +
                        "公開非 GET 端點一律要限流（docs/14-invariants.md「公開寫入端點一律限流」）。");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "發現公開端點的寫入呼叫沒有掛限流政策，任何人都能無限次呼叫、灌票或塞爆資料庫：\n"
            + string.Join("\n", violations));
    }

    /// <summary>從 <c>MapPost</c>（或其他寫入方法）呼叫節點沿著 fluent chain 往外層走
    /// （<c>.WithName(...).WithTags(...).RequireRateLimiting(...).Produces(...)</c> 這種一路
    /// 串下去的寫法，語法樹上是一層層互相巢狀的 <c>MemberAccessExpression</c>／
    /// <c>InvocationExpression</c>），檢查沿路有沒有任何一段是 <c>.RequireRateLimiting(...)</c>。
    /// 只比對成員名稱字串（不追語意符號）——<c>RequireRateLimiting</c> 這個名稱在本專案裡不會
    /// 跟其他無關方法撞名，字串比對已經足夠，不需要為此再多一層語意解析成本。</summary>
    private static bool ChainHasRequireRateLimiting(InvocationExpressionSyntax mapInvocation)
    {
        SyntaxNode current = mapInvocation;

        while (current.Parent is MemberAccessExpressionSyntax memberAccess
               && memberAccess.Expression == current
               && memberAccess.Parent is InvocationExpressionSyntax outerInvocation
               && outerInvocation.Expression == memberAccess)
        {
            if (memberAccess.Name.Identifier.Text == "RequireRateLimiting")
            {
                return true;
            }

            current = outerInvocation;
        }

        return false;
    }
}
