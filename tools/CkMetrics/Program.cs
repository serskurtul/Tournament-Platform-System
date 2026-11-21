// ЛР-8 (Інженерія якості): CK & MOOD object-design metrics for the C# backend.
//
// Usage:
//   dotnet run --project tools/CkMetrics -- <source-dir> [--out metrics.json] [--refs <dir-with-dlls>]...
//
// The whole source tree is compiled into one Roslyn compilation, so every project type resolves
// semantically. Framework/package references are optional (they only make DIT/RFC exact for
// classes that derive from external types such as AutoMapper.Profile or ControllerBase).
//
// Metrics per class (Chidamber & Kemerer + Lorenz & Kidd):
//   WMC  – Weighted Methods per Class: sum of McCabe cyclomatic complexity of its methods/accessors.
//   CBO  – Coupling Between Objects: distinct OTHER classes/interfaces/records/structs *of this system*
//          the class depends on (fields, parameters, locals, return types, base types, generics,
//          invocations, object creation). Framework types are reported separately as CBO_ext.
//   LCOM – Henderson-Sellers LCOM* in [0..1]: (mean_A(mu(A)) - m) / (1 - m), m = instance methods,
//          mu(A) = methods that use instance field A. 0 = perfectly cohesive, 1 = no cohesion.
//   LCOM4 – connected components of methods linked by shared fields or calls (SonarQube `lcom4`).
//   DIT  – Depth of Inheritance Tree, counting System.Object (a plain class has DIT = 1).
//   NOC  – Number Of Children inside the system.
//   RFC  – Response For a Class: own methods + distinct methods it invokes.
//   PIM  – Public Instance Methods (Lorenz & Kidd public interface size).
//   NOO  – Number Of Overridden methods (Lorenz & Kidd).
// System-level MOOD (Abreu):
//   MHF/AHF – method/attribute hiding factors (share of non-public methods/fields).
//   COF     – coupling factor: actual client→supplier pairs (excluding inheritance) / (TC² − TC).
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var argList = args.ToList();
if (argList.Count == 0)
{
    Console.Error.WriteLine("usage: CkMetrics <source-dir> [--out file.json] [--refs dir]...");
    return 2;
}

var sourceDir = Path.GetFullPath(argList[0]);
string? outFile = null;
var refDirs = new List<string>();
for (var i = 1; i < argList.Count; i++)
{
    if (argList[i] == "--out") outFile = argList[++i];
    else if (argList[i] == "--refs") refDirs.Add(argList[++i]);
}

string[] excluded = ["/bin/", "/obj/", "/Migrations/", "/Tests/", "/IntegrationTests/"];
var files = Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
    .Where(f => !excluded.Any(x => f.Replace('\\', '/').Contains(x)))
    .OrderBy(f => f)
    .ToList();

var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parseOptions, path: f)).ToList();

// References: the running runtime (System.*) plus any extra directories (ASP.NET shared framework, bin/).
var refPaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
    .Concat(refDirs.SelectMany(d => Directory.Exists(d) ? Directory.EnumerateFiles(d, "*.dll") : []))
    .GroupBy(Path.GetFileName)
    .Select(g => g.First())
    .ToList();
var references = new List<MetadataReference>();
foreach (var p in refPaths)
{
    try { references.Add(MetadataReference.CreateFromFile(p)); } catch { /* not a managed assembly */ }
}

var compilation = CSharpCompilation.Create("Analysis", trees, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

// Collect system types (declared in the analysed sources).
var declared = new Dictionary<INamedTypeSymbol, (TypeDeclarationSyntax Syntax, SemanticModel Model)>(SymbolEqualityComparer.Default);
foreach (var tree in trees)
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var decl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
    {
        if (model.GetDeclaredSymbol(decl) is INamedTypeSymbol symbol && !declared.ContainsKey(symbol))
            declared[symbol] = (decl, model);
    }
}
var systemTypes = new HashSet<INamedTypeSymbol>(declared.Keys, SymbolEqualityComparer.Default);

INamedTypeSymbol? Normalize(ITypeSymbol? t) => t switch
{
    INamedTypeSymbol n => n.OriginalDefinition,
    IArrayTypeSymbol a => Normalize(a.ElementType),
    _ => null,
};

void AddType(ITypeSymbol? t, HashSet<INamedTypeSymbol> into)
{
    if (t is null) return;
    if (t is IArrayTypeSymbol arr) { AddType(arr.ElementType, into); return; }
    if (t is not INamedTypeSymbol named) return;
    var def = Normalize(named);
    if (def is not null && def.SpecialType == SpecialType.None) into.Add(def);
    foreach (var arg in named.TypeArguments) AddType(arg, into);
}

int Cyclomatic(SyntaxNode body) => 1 + body.DescendantNodes().Count(n => n switch
{
    IfStatementSyntax or WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
        or DoStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax
        or CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax or SwitchExpressionArmSyntax
        or ConditionalAccessExpressionSyntax => true,
    BinaryExpressionSyntax b => b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression)
                                || b.IsKind(SyntaxKind.CoalesceExpression),
    AssignmentExpressionSyntax a => a.IsKind(SyntaxKind.CoalesceAssignmentExpression),
    _ => false,
});

var results = new List<Dictionary<string, object?>>();
var couplingPairs = 0;
int totalMethods = 0, hiddenMethods = 0, totalFields = 0, hiddenFields = 0;

foreach (var (symbol, (syntax, model)) in declared.OrderBy(kv => kv.Key.ToDisplayString()))
{
    if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct)) continue;

    var deps = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    var nonInheritanceDeps = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    var invoked = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

    // Base type + interfaces.
    var inheritance = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    if (symbol.BaseType is { } bt) AddType(bt, inheritance);
    foreach (var itf in symbol.Interfaces) AddType(itf, inheritance);
    deps.UnionWith(inheritance);

    // Every type mentioned or used anywhere in the class body.
    foreach (var node in syntax.DescendantNodes())
    {
        switch (node)
        {
            case TypeSyntax ts when ts.Parent is not QualifiedNameSyntax:
                AddType(model.GetTypeInfo(ts).Type, nonInheritanceDeps);
                break;
            case InvocationExpressionSyntax inv when model.GetSymbolInfo(inv).Symbol is IMethodSymbol m:
                invoked.Add(m.OriginalDefinition);
                AddType(m.ContainingType, nonInheritanceDeps);
                break;
            case ObjectCreationExpressionSyntax oc:
                AddType(model.GetTypeInfo(oc).Type, nonInheritanceDeps);
                break;
            case MemberAccessExpressionSyntax ma when model.GetSymbolInfo(ma).Symbol is { ContainingType: { } ct }:
                AddType(ct, nonInheritanceDeps);
                break;
        }
    }
    nonInheritanceDeps.RemoveWhere(t => SymbolEqualityComparer.Default.Equals(t, symbol.OriginalDefinition));
    deps.UnionWith(nonInheritanceDeps);
    deps.RemoveWhere(t => SymbolEqualityComparer.Default.Equals(t, symbol.OriginalDefinition));

    var systemDeps = deps.Where(systemTypes.Contains).ToList();
    var externalDeps = deps.Where(t => !systemTypes.Contains(t)).ToList();
    couplingPairs += nonInheritanceDeps.Count(t => systemTypes.Contains(t) && !inheritance.Contains(t));

    // Methods (ordinary methods, constructors excluded from LCOM/PIM, accessors counted for WMC).
    var methodSyntax = syntax.Members.OfType<BaseMethodDeclarationSyntax>().ToList();
    var ordinary = methodSyntax.OfType<MethodDeclarationSyntax>().ToList();
    var wmc = methodSyntax.Sum(m => Cyclomatic(m))
              + syntax.Members.OfType<PropertyDeclarationSyntax>().Sum(p =>
                    p.ExpressionBody is not null ? Cyclomatic(p.ExpressionBody)
                    : p.AccessorList?.Accessors.Where(a => a.Body is not null || a.ExpressionBody is not null)
                        .Sum(a => Cyclomatic(a)) ?? 0);

    var methodSymbols = ordinary.Select(m => model.GetDeclaredSymbol(m)).OfType<IMethodSymbol>().ToList();
    totalMethods += methodSymbols.Count;
    hiddenMethods += methodSymbols.Count(m => m.DeclaredAccessibility != Accessibility.Public);
    var pim = methodSymbols.Count(m => !m.IsStatic && m.DeclaredAccessibility == Accessibility.Public);
    var noo = methodSymbols.Count(m => m.IsOverride);

    // Fields (explicit fields + auto-properties' backing state).
    var fieldSymbols = symbol.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsImplicitlyDeclared && !f.IsConst).ToList();
    totalFields += fieldSymbols.Count;
    hiddenFields += fieldSymbols.Count(f => f.DeclaredAccessibility != Accessibility.Public);
    var instanceFields = fieldSymbols.Where(f => !f.IsStatic).ToList<ISymbol>();
    instanceFields.AddRange(symbol.GetMembers().OfType<IPropertySymbol>()
        .Where(p => !p.IsStatic && p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is PropertyDeclarationSyntax pd
                    && pd.AccessorList?.Accessors.All(a => a.Body is null && a.ExpressionBody is null) == true));

    // LCOM* (Henderson-Sellers) over instance methods.
    var instanceMethods = ordinary.Where(m => !m.Modifiers.Any(SyntaxKind.StaticKeyword)).ToList();
    double lcom = 0;
    if (instanceMethods.Count > 1 && instanceFields.Count > 0)
    {
        var mu = instanceFields.Select(f => instanceMethods.Count(m =>
            m.DescendantNodes().OfType<IdentifierNameSyntax>()
             .Any(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol?.OriginalDefinition, f.OriginalDefinition))));
        var mean = mu.Average();
        var m = instanceMethods.Count;
        lcom = Math.Round(Math.Clamp((mean - m) / (1.0 - m), 0, 1), 3);
    }

    // LCOM4 (Hitz & Montazeri, the metric SonarQube exposed as `lcom4`): number of connected
    // components in the graph of ordinary methods, where two methods are linked if they use a
    // common instance field or one calls the other. 1 = cohesive; >1 = the class does several jobs.
    var lcom4 = 0;
    if (ordinary.Count > 0)
    {
        var methodSet = ordinary.Select(m => model.GetDeclaredSymbol(m)).ToList();
        var usesField = ordinary.Select(m => instanceFields.Where(f =>
            m.DescendantNodes().OfType<IdentifierNameSyntax>()
             .Any(id => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(id).Symbol?.OriginalDefinition, f.OriginalDefinition)))
            .ToHashSet<ISymbol>(SymbolEqualityComparer.Default)).ToList();
        var calls = ordinary.Select(m => m.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(inv => model.GetSymbolInfo(inv).Symbol?.OriginalDefinition)
            .Where(s => s is not null && methodSet.Contains(s, SymbolEqualityComparer.Default))
            .ToHashSet(SymbolEqualityComparer.Default)).ToList();
        var parent = Enumerable.Range(0, ordinary.Count).ToArray();
        int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
        for (var a = 0; a < ordinary.Count; a++)
        for (var b2 = a + 1; b2 < ordinary.Count; b2++)
        {
            var linked = usesField[a].Overlaps(usesField[b2])
                         || calls[a].Contains(methodSet[b2]) || calls[b2].Contains(methodSet[a]);
            if (linked) parent[Find(a)] = Find(b2);
        }
        lcom4 = Enumerable.Range(0, ordinary.Count).Select(Find).Distinct().Count();
    }

    var dit = 0;
    for (var b = symbol.BaseType; b is not null; b = b.BaseType) dit++;
    var noc = systemTypes.Count(t => SymbolEqualityComparer.Default.Equals(t.BaseType?.OriginalDefinition, symbol.OriginalDefinition));

    results.Add(new Dictionary<string, object?>
    {
        ["class"] = symbol.ToDisplayString(),
        ["file"] = Path.GetRelativePath(sourceDir, syntax.SyntaxTree.FilePath).Replace('\\', '/'),
        ["WMC"] = wmc,
        ["CBO"] = systemDeps.Count,
        ["CBO_ext"] = externalDeps.Count,
        ["LCOM"] = lcom,
        ["LCOM4"] = lcom4,
        ["DIT"] = dit,
        ["NOC"] = noc,
        ["RFC"] = methodSymbols.Count + invoked.Count(m => !methodSymbols.Contains(m, SymbolEqualityComparer.Default)),
        ["PIM"] = pim,
        ["NOO"] = noo,
        ["methods"] = methodSymbols.Count,
        ["fields"] = instanceFields.Count,
        ["base"] = symbol.BaseType?.ToDisplayString(),
        ["coupledTo"] = systemDeps.Select(t => t.Name).OrderBy(n => n).ToList(),
    });
}

var tc = results.Count;
var mood = new Dictionary<string, object>
{
    ["classes"] = tc,
    ["MHF"] = totalMethods == 0 ? 0 : Math.Round((double)hiddenMethods / totalMethods, 3),
    ["AHF"] = totalFields == 0 ? 0 : Math.Round((double)hiddenFields / totalFields, 3),
    ["COF"] = tc < 2 ? 0 : Math.Round((double)couplingPairs / (tc * tc - tc), 4),
};

var json = JsonSerializer.Serialize(new { source = sourceDir, mood, classes = results },
    new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
if (outFile is null) Console.WriteLine(json);
else
{
    File.WriteAllText(outFile, json);
    Console.WriteLine($"{tc} classes analysed; MOOD: MHF={mood["MHF"]} AHF={mood["AHF"]} COF={mood["COF"]} -> {outFile}");
}
return 0;
