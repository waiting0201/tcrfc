using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴🔴🔴 2026-09-24（`S1-8` 續作，回應 <c>docs/18-work-errors.md</c> <c>E-52</c>）：
/// <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 把 400／403／409 例外的 <c>.Message</c>
/// 原樣塞進 <c>ProblemDetails.Detail</c> 回給呼叫端——這段文字**就是後台介面文字**（畫面上
/// `ElMessageBox.alert` 或表單錯誤提示會原樣顯示），必須遵守規劃書 §4.0／<c>docs/06-conventions.md</c>
/// §1「介面上不得出現……模組代號、權限碼、隊別代號、英文技術詞」這條全站不變量。
/// <c>E-52</c> 的實際案例：<c>AdminClubAuthorizer</c> 曾經把訊息寫成
/// <c>你的角色沒有「{permissionCode}」這項操作的權限。</c>，畫面上直接印出
/// <c>team.competition.view</c> 這種字串——那一處已經修好，但**修好一個已知案例不等於這一類錯不會
/// 再發生**，這支測試要做的是「以後同一形狀的錯，不需要有人先在畫面上親眼看到才會被抓到」。
///
/// ## 涵蓋範圍怎麼確認夠廣
///
/// 這支測試是**靜態掃描**，跟 <see cref="ArchitectureTests"/> 用同一套手法（把 <c>apps/api</c>
/// 組成一個 Roslyn <see cref="CSharpCompilation"/>，用語意模型而不是文字比對）——好處是**掃描範圍
/// 不受目前有沒有測試命中那條程式碼路徑影響**：即使某個 403／400／409 的分支從來沒有被任何整合測試
/// 打到過，這支測試一樣看得到它的原始碼。步驟：
///
/// 1. **先問 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 本人「哪些例外型別對應
///    400／403／409」**——不是自己另外維護一份型別清單（那份清單會跟例外處理器的 switch
///    脫鉤，見 <c>docs/18-work-errors.md</c> <c>E-28</c>／<c>E-34</c> 一再出現的「衍生清單與來源
///    分處兩份檔案」根因）。做法是對例外處理器的原始碼文字跑一個小 regex，抓出
///    <c>XxxException xxx =&gt; (StatusCodes.StatusYYY, ...)</c> 這個既有、穩定的排版慣例，
///    取出每一對（型別名稱、狀態碼常數）。新增一個 400／403／409 的例外分支不需要回來改這支測試。
/// 2. **這個 codebase 的例外訊息只有兩種形狀，各自對應一種掃描目標**：
///    - **呼叫端組訊息**（<c>AdminForbiddenException(string reason) : Exception(reason)</c>
///      這種「建構子原封不動轉呈訊息」的型別，訊息內容完全由 <c>throw new X($"...")</c> 那一行決定
///      ——<c>E-52</c> 的真實案例就是這種）。這支測試稱之為「**直通型**」，判斷方式：類別自己的
///      基底建構式呼叫，引數是不是原封不動轉呈自己收到的那個參數。**只對直通型掃呼叫端**——對非
///      直通型（見下一類）掃呼叫端引數，測過會製造假警報（見下方「寫這支測試時走過的彎路」）。
///    - **類別自己烤訊息模板**（<c>ArticleSlugConflictException(string slug) : AdminArticleException(
///      $"網址名稱「{slug}」...")</c> 這種型別，呼叫端只傳原始資料，實際的訊息文字寫死在類別宣告
///      裡）。這支測試稱之為「**烤製型**」，只掃類別宣告自己的基底建構式引數，不看呼叫端傳了什麼
///      ——類別內部想怎麼用那份資料是類別自己的責任，呼叫端傳的原始資料本身不是訊息。
/// 3. **掃描內容做三件事**（見 <c>ExpressionMightLeak</c>）：
///    - 字面文字（含插值字串的純文字片段）比對「權限碼形狀」正則與 docs/06 §1 禁用技術詞清單。
///    - 插值洞（<c>{expr}</c>）裡的識別字，名稱本身像「權限碼」（<c>(?i)permission.*code</c>）
///      就直接判定——這是 <c>E-52</c> 原始寫法 <c>{permissionCode}</c> 的字面重現。
///    - 對插值洞裡的區域變數，**向上追它自己的宣告式再遞迴分析一次**（有跳躍次數上限，預設 6 層），
///      並額外檢查 <c>x.Code</c>／<c>x.PermissionCode</c> 這種成員存取（只要 <c>x</c> 的語意型別
///      名稱含有 <c>Permission</c> 就判定，不看識別字名稱）——這一條讓掃描抓得到「變數名字完全看
///      不出來是權限碼」的案例（見下方「意外抓到的舊洞」）。**唯一的例外**：只用來取筆數的
///      <c>x.Count</c>／<c>x.Length</c> 不算外洩（顯示「有 3 個」不是列出那 3 個是什麼），所以不
///      往回追、也不算識別字名稱命中。
///    - 對「烤製型」類別自己收下的**字串集合型參數**（<c>IReadOnlyList&lt;string&gt;</c> 這一類），
///      只要基底建構式呼叫裡有任何一處不是單純取 <c>.Count</c>／<c>.Length</c>，一律判定——這一條
///      **不靠參數名稱**，靠的是「這個參數是一批字串、而且訊息模板把它的內容而不是筆數印出來」這個
///      結構性特徵，理由與下方「意外抓到的舊洞」的重現方式有關。
///
/// ### 意外抓到的舊洞（不是虛構的示範）
///
/// 這支測試在撰寫過程中，真的用上面的邏輯抓到兩個先前沒被任何人發現的既有違規：
/// <c>AdminRoleSysadminOnlyPermissionException</c>（烤製型，<c>IReadOnlyList&lt;string&gt; codes</c>
/// 参数）把整份被拒絕的權限碼清單逐字接進訊息（<c>AdminRoleExceptions.cs</c>），以及
/// <c>AdminRoleValidationException</c>（直通型）在 <c>AdminRolesRepository.ReplacePermissionsAsync</c>
/// 找不到權限碼時，呼叫端把查無資料的代碼清單逐字接進訊息（<c>AdminRolesRepository.cs:166</c>）——
/// 兩處都不是本輪新增的程式碼，是 <c>S1-3</c>／<c>S1-8</c> 就存在的舊洞，這支測試通過之前已經在
/// 同一次交付內修掉（兩處都改成只回報筆數）。這兩個變數（<c>codes</c>／<c>missing</c>）的名稱本身
/// 完全不含「permission」字樣，是靠「語意型別是 <c>Permission</c>」與「參數是字串集合」這兩條
/// **不靠命名**的規則抓到的。
///
/// ### 寫這支測試時走過的彎路（誠實記錄，供下一個要改這支測試的人參考）
///
/// 第一版沒有分「直通型」／「烤製型」，對所有目標型別的呼叫端引數一律遞迴分析，結果修好
/// <c>AdminRoleSysadminOnlyPermissionException</c>（改成只印 <c>codes.Count</c>）之後，**呼叫端**
/// <c>new AdminRoleSysadminOnlyPermissionException(sysadminOnlyCodes)</c> 仍然被判定「洩漏」——
/// 因為 <c>sysadminOnlyCodes</c> 這個區域變數本來就是從 <c>Permission.Code</c> 算出來的，把「這個
/// 引數的內容源頭是不是權限碼」跟「這個型別最終有沒有把內容而不是筆數印進訊息」混為一談，對「烤製型」
/// 而言是過度保守——**傳一份權限碼清單給建構子本身不是問題，類別內部只印筆數就是安全的**。改成
/// 「直通型看呼叫端、烤製型看類別自己的模板」之後才同時消除誤判又保留抓得到兩個舊洞的能力。
///
/// ## 誠實列出做不到的邊界
///
/// - **只能追蹤同一個方法內的區域變數宣告，不追蹤跨方法呼叫的資料流**——如果某個方法把權限碼字串
///   當參數傳進另一個 helper 方法、helper 內部才組訊息，這支測試在 helper 內部一樣看得到（helper
///   本身如果符合上面的其中一種掃描目標就會被掃到），但**如果權限碼是透過方法回傳值間接取得、又
///   經過好幾層方法呼叫才到達訊息組裝處**，向上追蹤只會停在「呼叫了某個方法」這一步，不會真的進去
///   分析被呼叫方法的內部邏輯（那需要完整的 call graph／dataflow 分析，超出這支測試的合理成本）。
/// - **看不到執行期才決定內容的字串**（例如從資料庫欄位值、設定檔、外部 API 回應組出來的訊息）——
///   這支測試是原始碼靜態分析，只能看到編譯時就存在的字面文字與可追蹤的語法結構。
/// - **「識別字名稱像權限碼」這條判準本身是啟發式，不是形式證明**——理論上可以取一個完全不含
///   「permission」「code」字樣、型別也不是 <c>Permission</c> 相關、也不是字串集合的變數名稱來
///   刻意繞過。**這正是為什麼本任務同時要求「以代表性端點實打」**（見
///   <see cref="UserFacingMessageHttpContentTests"/>）——靜態掃描負責「盡量涵蓋所有原始碼路徑」，
///   HTTP 整合測試負責「證明幾個已知高風險端點的實際輸出，經過完整的 JSON 序列化管線之後，確實
///   不含權限碼形狀」，兩者互補，不是其中一種就足夠。
/// - **「字串集合參數」的判斷是文字比對參數型別的寫法**（<c>IReadOnlyList&lt;string&gt;</c> 這種
///   固定寫法），不是完整解析泛型型別的語意——如果有人把型別寫成完整命名空間
///   （<c>System.Collections.Generic.IReadOnlyList&lt;string&gt;</c>）或用型別別名，這條規則會漏抓，
///   屬於已知限制（目前 <c>apps/api</c> 全部用簡短寫法，見 <c>.editorconfig</c>／既有程式碼慣例）。
/// - **「權限碼形狀」正則（三段小寫、以句點分隔）理論上可能誤判**成其他也長這樣的字串（例如某個
///   網域名稱）——目前 <c>apps/api</c> 的 400／403／409 訊息裡沒有這種內容，屬於低機率的已知限制，
///   不是「保證零誤判」。
/// </summary>
public sealed class UserFacingMessageContentTests
{
    private static readonly Regex PermissionCodeShape = new(
        @"\b[a-z][a-z0-9]*(?:_[a-z0-9]+)*(?:\.[a-z][a-z0-9]*(?:_[a-z0-9]+)*){2,}\b",
        RegexOptions.Compiled);

    // docs/06-conventions.md §1「後台介面用語對照表」明文禁止在介面出現的英文技術詞。
    private static readonly Regex ForbiddenTechnicalWord = new(
        @"\b(slug|canonical|noindex|hreflang|sku|token|blob|schema|club_id)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 模組代號（B1／K4／S3……）與隊別代號（D1／BW1）——docs/06 §1、docs/14-invariants.md。
    private static readonly Regex ModuleCodeShape = new(@"\b[A-Z][1-9]\b", RegexOptions.Compiled);
    private static readonly Regex TeamCodeLiteral = new(@"\b(D1|BW1)\b", RegexOptions.Compiled);

    // E-52 原始寫法 {permissionCode} 的識別字名稱重現；不分大小寫涵蓋 PermissionCode／permCode 等變形。
    private static readonly Regex PermissionCodeIdentifierName = new(
        @"permission.*code|perm.*code", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 「烤製型」類別自己收下的字串集合型參數——這一類參數如果被整份印進訊息（而不是只印筆數），
    // 一律判定風險，不管參數叫什麼名字（見類別上方「涵蓋範圍」第 3 點最後一項）。
    private static readonly Regex StringCollectionParameterType = new(
        @"^(IReadOnlyList|IReadOnlyCollection|IEnumerable|List|ICollection)<\s*string\s*>$|^string\[\]$",
        RegexOptions.Compiled);

    private static string RepoRoot([CallerFilePath] string thisFilePath = "")
    {
        var testsDir = Path.GetDirectoryName(thisFilePath)!;
        return Path.GetFullPath(Path.Combine(testsDir, "..", "..", ".."));
    }

    [Fact]
    public void _400_403_409的例外訊息不含權限碼形狀或docs06禁用技術詞()
    {
        var apiDir = Path.Combine(RepoRoot(), "apps", "api");
        Assert.True(Directory.Exists(apiDir), $"找不到 apps/api 目錄：{apiDir}");

        var handlerPath = Path.Combine(apiDir, "Common", "ApiExceptionHandler.cs");
        Assert.True(File.Exists(handlerPath), $"找不到例外處理器：{handlerPath}");
        var handlerSource = File.ReadAllText(handlerPath);

        var targetTypeNames = ExtractTargetExceptionTypeNames(handlerSource);
        Assert.True(targetTypeNames.Count >= 20,
            $"只從 ApiExceptionHandler.cs 解析到 {targetTypeNames.Count} 個 400/403/409 例外型別，" +
            "遠低於預期——regex 抽取邏輯可能跟不上例外處理器目前的排版，請人工核對。");

        var sourceFiles = Directory.EnumerateFiles(apiDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}Tcrfc.Api.Tests{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.True(sourceFiles.Count > 50, $"只掃到 {sourceFiles.Count} 個檔案，遠低於預期——apiDir 推導可能算錯。");

        var syntaxTrees = sourceFiles
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "UserFacingMessageScan",
            syntaxTrees: syntaxTrees,
            references: CollectReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // 先找出所有「烤製型」類別宣告（訊息模板刻在類別裡），順便建出「直通型」集合——
        // 直通型才需要掃呼叫端，理由見類別上方「涵蓋範圍」第 2 點與「寫這支測試時走過的彎路」。
        var passthroughTypes = new HashSet<string>();
        var classDeclarationsByFile = new List<(SyntaxTree Tree, SemanticModel Model, ClassDeclarationSyntax Class)>();

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            foreach (var classDecl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!targetTypeNames.Contains(classDecl.Identifier.Text))
                {
                    continue;
                }

                classDeclarationsByFile.Add((tree, semanticModel, classDecl));
                if (IsMessagePassthrough(classDecl))
                {
                    passthroughTypes.Add(classDecl.Identifier.Text);
                }
            }
        }

        var violations = new List<string>();

        // ① 直通型的呼叫端：throw new X($"...") ——訊息完全由這一行決定。
        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var node in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                ScanConstructorCallIfTargeted(node, node.ArgumentList, semanticModel, targetTypeNames, passthroughTypes, tree.FilePath, violations);
            }
            foreach (var node in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                ScanConstructorCallIfTargeted(node, node.ArgumentList, semanticModel, targetTypeNames, passthroughTypes, tree.FilePath, violations);
            }
        }

        // ② 烤製型的類別宣告本身：基底建構式呼叫的引數運算式，以及「字串集合參數有沒有被整份印出」。
        foreach (var (tree, semanticModel, classDecl) in classDeclarationsByFile)
        {
            foreach (var arg in GetBaseInitializerArguments(classDecl))
            {
                if (ExpressionMightLeak(arg, semanticModel, new HashSet<ISymbol>(SymbolEqualityComparer.Default), depth: 0))
                {
                    violations.Add(Format(tree, arg, $"[類別宣告 {classDecl.Identifier.Text} 的基底建構式呼叫]"));
                }
            }

            if (ClassBakesStringCollectionParameterRaw(classDecl, out var offendingParam, out var offendingExpr))
            {
                violations.Add(Format(tree, offendingExpr!,
                    $"[類別宣告 {classDecl.Identifier.Text} 把字串集合參數「{offendingParam}」的內容而非筆數印進訊息]"));
            }
        }

        Assert.True(violations.Count == 0,
            "發現 400/403/409 的例外訊息可能包含權限碼形狀或 docs/06 §1 禁用的技術詞（介面不得顯示）：\n"
            + string.Join("\n", violations));
    }

    /// <summary>「直通型」：基底建構式呼叫的唯一引數，是不是原封不動轉呈自己某個建構子參數。</summary>
    private static bool IsMessagePassthrough(ClassDeclarationSyntax classDecl)
    {
        var ownParamNames = new HashSet<string>(
            classDecl.ParameterList?.Parameters.Select(p => p.Identifier.Text) ?? []);

        foreach (var ctor in classDecl.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            foreach (var p in ctor.ParameterList.Parameters)
            {
                ownParamNames.Add(p.Identifier.Text);
            }
        }

        foreach (var argExpr in GetBaseInitializerArguments(classDecl))
        {
            if (argExpr is IdentifierNameSyntax id && ownParamNames.Contains(id.Identifier.Text))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<ExpressionSyntax> GetBaseInitializerArguments(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList?.Types.FirstOrDefault() is PrimaryConstructorBaseTypeSyntax primaryBase)
        {
            foreach (var arg in primaryBase.ArgumentList.Arguments)
            {
                yield return arg.Expression;
            }
        }

        foreach (var ctor in classDecl.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Initializer is not { } initializer)
            {
                continue;
            }
            foreach (var arg in initializer.ArgumentList.Arguments)
            {
                yield return arg.Expression;
            }
        }
    }

    /// <summary>
    /// 烤製型類別自己收下的字串集合參數，只要基底建構式呼叫裡有任何一處不是單純取
    /// <c>.Count</c>／<c>.Length</c>，就代表訊息模板把內容（而不是筆數）印了出來。
    /// </summary>
    private static bool ClassBakesStringCollectionParameterRaw(
        ClassDeclarationSyntax classDecl, out string? offendingParam, out ExpressionSyntax? offendingExpr)
    {
        offendingParam = null;
        offendingExpr = null;

        var collectionParamNames = new HashSet<string>();
        foreach (var p in classDecl.ParameterList?.Parameters ?? default)
        {
            if (p.Type is not null && StringCollectionParameterType.IsMatch(p.Type.ToString()))
            {
                collectionParamNames.Add(p.Identifier.Text);
            }
        }
        foreach (var ctor in classDecl.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            foreach (var p in ctor.ParameterList.Parameters)
            {
                if (p.Type is not null && StringCollectionParameterType.IsMatch(p.Type.ToString()))
                {
                    collectionParamNames.Add(p.Identifier.Text);
                }
            }
        }

        if (collectionParamNames.Count == 0)
        {
            return false;
        }

        foreach (var argExpr in GetBaseInitializerArguments(classDecl))
        {
            foreach (var id in argExpr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                if (!collectionParamNames.Contains(id.Identifier.Text) || IsSolelyCountOrLengthReceiver(id))
                {
                    continue;
                }

                offendingParam = id.Identifier.Text;
                offendingExpr = argExpr;
                return true;
            }
        }

        return false;
    }

    private static bool IsSolelyCountOrLengthReceiver(IdentifierNameSyntax id)
        => id.Parent is MemberAccessExpressionSyntax memberAccess
        && memberAccess.Expression == id
        && memberAccess.Name.Identifier.Text is "Count" or "Length" or "LongLength";

    private static void ScanConstructorCallIfTargeted(
        SyntaxNode creationNode, ArgumentListSyntax? argumentList, SemanticModel semanticModel,
        HashSet<string> targetTypeNames, HashSet<string> passthroughTypes, string filePath, List<string> violations)
    {
        if (argumentList is null)
        {
            return;
        }

        // 🔴 這裡刻意用 `.Type ?? .ConvertedType`，順序跟 ArchitectureTests 的 `.ConvertedType ?? .Type`
        // 相反——實測發現（見本檔頭「涵蓋範圍」說明）`throw new X(...)`／`return new X(...)` 這種
        // 語法位置，Roslyn 回報的 `ConvertedType` 是「這個位置要求的型別」（throw 是 `System.Exception`，
        // 某個 async 方法的 return 甚至回報 `Task`），不是「這個運算式實際建構出來的型別」；
        // 這支測試的掃描目標「throw new AdminForbiddenException(...)」剛好就落在這個語法位置，
        // 用 `ConvertedType` 優先會把每一個目標例外都誤判成基底型別，永遠命中不了 targetTypeNames。
        // `.Type`（未轉換的自然型別）在這個語法節點上就是建構運算式本身宣告的型別，才是我們要問的問題。
        var typeInfo = semanticModel.GetTypeInfo(creationNode);
        var resolved = typeInfo.Type ?? typeInfo.ConvertedType;
        if (resolved is null || !targetTypeNames.Contains(resolved.Name) || !passthroughTypes.Contains(resolved.Name))
        {
            return; // 非直通型不掃呼叫端——理由見類別上方「寫這支測試時走過的彎路」。
        }

        foreach (var arg in argumentList.Arguments)
        {
            if (ExpressionMightLeak(arg.Expression, semanticModel, new HashSet<ISymbol>(SymbolEqualityComparer.Default), depth: 0))
            {
                var lineSpan = creationNode.SyntaxTree.GetLineSpan(creationNode.Span);
                violations.Add($"{filePath}:{lineSpan.StartLinePosition.Line + 1}: [呼叫端 new {resolved.Name}(...)] {creationNode.ToString().Trim()}");
            }
        }
    }

    private static string Format(SyntaxTree tree, SyntaxNode node, string label)
    {
        var lineSpan = tree.GetLineSpan(node.Span);
        return $"{tree.FilePath}:{lineSpan.StartLinePosition.Line + 1}: {label} {node.ToString().Trim()}";
    }

    /// <summary>
    /// 從 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 的原始碼文字，抽出所有對應
    /// 400／403／409 的例外型別短名稱。刻意用文字 regex 而不是完整解析整個 switch expression——
    /// 這支測試要問的是「例外處理器現在實際怎麼寫」，直接讀它的排版慣例（<c>XxxException xxx =&gt;</c>
    /// 後面接 <c>(StatusCodes.StatusYYY, ...)</c>）比重新組一份 switch-arm 語法樹解析更不容易跟原始碼
    /// 的實際寫法脫節；新增一個 arm 只要遵循既有排版慣例就會被自動抓到，不需要回來改這支測試。
    /// </summary>
    private static HashSet<string> ExtractTargetExceptionTypeNames(string handlerSource)
    {
        var armPattern = new Regex(
            @"(?<type>\w+Exception)\s+\w+\s*=>\s*\r?\n\s*\(StatusCodes\.(?<status>Status\d{3}\w+)",
            RegexOptions.Compiled);

        var targetStatuses = new HashSet<string> { "Status400BadRequest", "Status403Forbidden", "Status409Conflict" };
        var result = new HashSet<string>();

        foreach (Match match in armPattern.Matches(handlerSource))
        {
            if (targetStatuses.Contains(match.Groups["status"].Value))
            {
                result.Add(match.Groups["type"].Value);
            }
        }

        return result;
    }

    /// <summary>
    /// 遞迴判斷一個運算式（訊息本身，或訊息插值洞裡的運算式）有沒有可能把權限碼形狀或禁用技術詞
    /// 帶進使用者看得到的文字。見類別上方「涵蓋範圍」與「邊界」的完整說明。
    /// </summary>
    private static bool ExpressionMightLeak(
        ExpressionSyntax expr, SemanticModel semanticModel, HashSet<ISymbol> visitedLocals, int depth)
    {
        if (depth > 6)
        {
            return false;
        }

        // ① 字面文字（含插值字串的純文字片段）。
        foreach (var literal in expr.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
        {
            if (literal.IsKind(SyntaxKind.StringLiteralExpression) && ContentLeaksForbiddenTerm(literal.Token.ValueText))
            {
                return true;
            }
        }
        foreach (var text in expr.DescendantNodesAndSelf().OfType<InterpolatedStringTextSyntax>())
        {
            if (ContentLeaksForbiddenTerm(text.TextToken.ValueText))
            {
                return true;
            }
        }

        // ② x.Code／x.PermissionCode 成員存取，且 x 的語意型別名稱含有 "Permission"——
        //    不看識別字名稱，看型別，抓得到本輪實測發現的 missing／sysadminOnlyCodes 這種案例。
        foreach (var member in expr.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            var memberName = member.Name.Identifier.Text;
            if (memberName is not ("Code" or "PermissionCode"))
            {
                continue;
            }

            var receiverType = semanticModel.GetTypeInfo(member.Expression).Type;
            if (receiverType is not null
                && receiverType.Name.Contains("Permission", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // ③ 插值洞（或整個運算式本身）裡任何識別字：名稱本身像「權限碼」就直接判定；否則若是區域
        //    變數，向上追它自己的宣告式再遞迴分析一次。**只取 .Count／.Length 的識別字跳過**——
        //    顯示筆數不算列出內容，見類別上方「涵蓋範圍」第 3 點。
        foreach (var id in expr.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (IsSolelyCountOrLengthReceiver(id))
            {
                continue;
            }

            if (PermissionCodeIdentifierName.IsMatch(id.Identifier.Text))
            {
                return true;
            }

            var symbol = semanticModel.GetSymbolInfo(id).Symbol;
            if (symbol is not null && visitedLocals.Add(symbol)
                && symbol is ILocalSymbol local
                && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: { } initExpr }
                && ExpressionMightLeak(initExpr, semanticModel, visitedLocals, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContentLeaksForbiddenTerm(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return PermissionCodeShape.IsMatch(text)
            || ForbiddenTechnicalWord.IsMatch(text)
            || ModuleCodeShape.IsMatch(text)
            || TeamCodeLiteral.IsMatch(text);
    }

    private static List<MetadataReference> CollectReferences()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
}
