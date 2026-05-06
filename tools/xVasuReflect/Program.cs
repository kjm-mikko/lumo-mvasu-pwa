using System.Reflection;
using DevExpress.Xpo;

// Reflection dump for xVasu persistent types. Run once per phase change to
// confirm property names against NAVIGATION.md / Model.xafml before wiring
// up XPO queries. Lives under tools/ so it's not in the runtime build graph.
//
// Usage:
//   dotnet run --project tools/xVasuReflect -- xVasu.Data.Security.xVasuSecuritySystemUserTask
//   dotnet run --project tools/xVasuReflect -- --list xVasu.Data.Security
//   dotnet run --project tools/xVasuReflect -- --find Priority
//   dotnet run --project tools/xVasuReflect -- --methods DevExpress.Xpo.Session SelectData
//   dotnet run --project tools/xVasuReflect -- --xaf-rules xVasu.Data.Asma.Henkilo
//   dotnet run --project tools/xVasuReflect            (defaults to the task entity)
//
// `--xaf-rules <type>` produces a categorised dump of validation /
// read-only / size / mask / appearance metadata, matching what the XAF
// DetailView would render. Used as the canonical spec for PWA detail
// views.

var typeNames = args.Length == 0
    ? new[] { "xVasu.Data.Security.xVasuSecuritySystemUserTask" }
    : args;

if (typeNames[0] == "--list")
{
    DumpNamespace(typeNames.Length > 1 ? typeNames[1] : "xVasu.Data.Security");
    return 0;
}

if (typeNames[0] == "--find")
{
    FindByName(typeNames.Length > 1 ? typeNames[1] : "Priority");
    return 0;
}

if (typeNames[0] == "--methods")
{
    DumpMethods(
        typeNames.Length > 1 ? typeNames[1] : "DevExpress.Xpo.XPDataView",
        typeNames.Length > 2 ? typeNames[2] : null);
    return 0;
}

if (typeNames[0] == "--xaf-rules")
{
    if (typeNames.Length < 2)
    {
        Console.Error.WriteLine("--xaf-rules requires a type name (e.g. xVasu.Data.Asma.Henkilo)");
        return 1;
    }
    DumpXafRules(typeNames[1]);
    return 0;
}

foreach (var name in typeNames)
{
    DumpType(name);
    Console.WriteLine();
}

return 0;

static void DumpType(string fullName)
{
    var type = ResolveType(fullName);
    if (type is null)
    {
        Console.WriteLine($"# {fullName} — NOT FOUND");
        return;
    }

    Console.WriteLine($"# {type.FullName}");
    Console.WriteLine($"  base: {type.BaseType?.FullName}");
    Console.WriteLine($"  assembly: {type.Assembly.GetName().Name}");

    if (type.IsEnum)
    {
        Console.WriteLine($"  enum values:");
        foreach (var name in Enum.GetNames(type))
        {
            var value = Convert.ToInt32(Enum.Parse(type, name));
            Console.WriteLine($"    {name} = {value}");
        }
        Console.WriteLine();
        return;
    }
    Console.WriteLine();

    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.FlattenHierarchy)
        .OrderBy(p => p.DeclaringType == type ? 0 : 1)
        .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"  {"Property",-40} {"Type",-30} {"Declared",-30} {"Notes"}");
    Console.WriteLine($"  {new string('-', 40)} {new string('-', 30)} {new string('-', 30)} {new string('-', 30)}");

    foreach (var prop in props)
    {
        var notes = new List<string>();
        if (prop.GetCustomAttribute<KeyAttribute>() is not null) notes.Add("key");
        if (prop.GetCustomAttribute<PersistentAliasAttribute>() is { } pa) notes.Add($"alias:{pa.AliasExpression}");
        if (prop.GetCustomAttribute<AssociationAttribute>() is { } a) notes.Add($"assoc:{a.Name}");
        if (prop.GetCustomAttribute<NonPersistentAttribute>() is not null) notes.Add("nonpersistent");
        if (prop.SetMethod is null || !prop.SetMethod.IsPublic) notes.Add("readonly");

        Console.WriteLine(
            $"  {prop.Name,-40} {RenderType(prop.PropertyType),-30} {Truncate(prop.DeclaringType?.FullName ?? "?", 30),-30} {string.Join(", ", notes)}");
    }
}

static void DumpNamespace(string ns)
{
    var sample = ResolveType("xVasu.Data.Security.xVasuSecuritySystemUser")
        ?? throw new InvalidOperationException("xVasu.Module assembly not loaded");
    var assembly = sample.Assembly;

    var types = assembly.GetTypes()
        .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract)
        .Where(t => t.Namespace is not null && t.Namespace.StartsWith(ns, StringComparison.Ordinal))
        .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"# Types under {ns} ({types.Length})");
    foreach (var t in types)
    {
        Console.WriteLine($"  {t.FullName}");
    }
}

static void FindByName(string simpleName)
{
    _ = Type.GetType("xVasu.Data.Security.xVasuSecuritySystemUser, xVasu.Module", throwOnError: false);

    var matches = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a =>
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
        })
        .Where(t => t!.Name == simpleName)
        .ToArray();

    Console.WriteLine($"# Types named {simpleName} ({matches.Length})");
    foreach (var t in matches)
    {
        Console.WriteLine($"  {t!.FullName} — {t.Assembly.GetName().Name}");
    }
}

static Type? ResolveType(string fullName)
{
    // Force-load xVasu.Module first so dependent DevExpress assemblies fault-load.
    _ = Type.GetType("xVasu.Data.Security.xVasuSecuritySystemUser, xVasu.Module", throwOnError: false);

    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        var found = assembly.GetType(fullName, throwOnError: false);
        if (found is not null) return found;
    }

    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        try
        {
            var found = assembly.GetTypes().FirstOrDefault(t => t.FullName == fullName);
            if (found is not null) return found;
        }
        catch (ReflectionTypeLoadException) { /* skip partially loaded assemblies */ }
    }

    return null;
}

static string RenderType(Type t)
{
    if (!t.IsGenericType) return Truncate(t.Name, 30);
    var generic = t.GetGenericTypeDefinition().Name;
    var args = string.Join(", ", t.GetGenericArguments().Select(a => a.Name));
    return Truncate($"{generic.Split('`')[0]}<{args}>", 30);
}

static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

static void DumpMethods(string fullName, string? methodFilter)
{
    var type = ResolveType(fullName);
    if (type is null)
    {
        Console.WriteLine($"# {fullName} — NOT FOUND");
        return;
    }
    Console.WriteLine($"# {type.FullName} methods" + (methodFilter is null ? "" : $" (filter: {methodFilter})"));
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        .Where(m => methodFilter is null || string.Equals(m.Name, methodFilter, StringComparison.OrdinalIgnoreCase))
        .OrderBy(m => m.Name)
        .ThenBy(m => m.GetParameters().Length);
    foreach (var m in methods)
    {
        var ps = string.Join(", ", m.GetParameters().Select(p => $"{RenderType(p.ParameterType)} {p.Name}"));
        Console.WriteLine($"  {m.Name}({ps}) -> {RenderType(m.ReturnType)}");
    }
}

// -- --xaf-rules ----------------------------------------------------------
//
// Categorised dump of XAF DetailView-relevant attributes:
//   * Required fields        (RuleRequiredField)
//   * Read-only fields       (ModelDefault AllowEdit=False)
//   * Size constraints       (Size, with mask if ModelDefault EditMask is set)
//   * Range / regex / unique (RuleRange, RuleRegularExpression, RuleUniqueValue)
//   * Hidden by default      (VisibleInDetailView=False)
//   * Class-level Appearance rules (TargetItems / Visibility / BackColor /
//     FontColor / Enabled / Criteria / Context / Priority)
//
// Attributes are read via CustomAttributeData.GetCustomAttributes so the
// tool needs no hard reference to DevExpress.Persistent.Base — names
// are matched textually.

static void DumpXafRules(string fullName)
{
    var type = ResolveType(fullName);
    if (type is null)
    {
        Console.WriteLine($"# {fullName} — NOT FOUND");
        return;
    }

    Console.WriteLine($"# === XAF rules for {type.FullName} ===");
    Console.WriteLine($"  base: {type.BaseType?.FullName}");
    Console.WriteLine();

    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var required        = new List<string>();
    var readOnly        = new List<string>();
    var hiddenInDetail  = new List<string>();
    var sizeRows        = new List<(string Name, int Size, string? Mask, string? MaskType)>();
    var rangeRows       = new List<(string Name, string Args)>();
    var regexRows       = new List<(string Name, string Args)>();
    var uniqueRows      = new List<(string Name, string Args)>();

    foreach (var prop in props)
    {
        var attrs = prop.GetCustomAttributesData();

        if (HasAttribute(attrs, "RuleRequiredFieldAttribute"))
            required.Add(prop.Name);

        var allowEdit = ReadModelDefault(attrs, "AllowEdit");
        if (string.Equals(allowEdit, "False", StringComparison.OrdinalIgnoreCase))
            readOnly.Add(prop.Name);

        var visibleInDetail = GetSingleBoolArg(attrs, "VisibleInDetailViewAttribute");
        if (visibleInDetail == false)
            hiddenInDetail.Add(prop.Name);

        var sizeAttr = attrs.FirstOrDefault(a => a.AttributeType.Name is "SizeAttribute" or "FieldSizeAttribute");
        if (sizeAttr is not null)
        {
            var size = (sizeAttr.ConstructorArguments.Count > 0 && sizeAttr.ConstructorArguments[0].Value is int si)
                ? si : 0;
            var mask     = ReadModelDefault(attrs, "EditMask");
            var maskType = ReadModelDefault(attrs, "EditMaskType");
            if (size > 0 || mask is not null) sizeRows.Add((prop.Name, size, mask, maskType));
        }

        var rangeAttr = attrs.FirstOrDefault(a => a.AttributeType.Name == "RuleRangeAttribute");
        if (rangeAttr is not null)
            rangeRows.Add((prop.Name, RenderArgs(rangeAttr)));

        var regexAttr = attrs.FirstOrDefault(a => a.AttributeType.Name == "RuleRegularExpressionAttribute");
        if (regexAttr is not null)
            regexRows.Add((prop.Name, RenderArgs(regexAttr)));

        var uniqAttr = attrs.FirstOrDefault(a => a.AttributeType.Name == "RuleUniqueValueAttribute");
        if (uniqAttr is not null)
            uniqueRows.Add((prop.Name, RenderArgs(uniqAttr)));
    }

    PrintSection("Required fields (RuleRequiredField)", required.Select(n => $"  - {n}"));
    PrintSection("Read-only fields (ModelDefault AllowEdit=False)", readOnly.Select(n => $"  - {n}"));
    PrintSection("Hidden in DetailView", hiddenInDetail.Select(n => $"  - {n}"));
    PrintSection(
        "Size / mask",
        sizeRows.Select(r =>
        {
            var sizeText = r.Size > 0 ? r.Size.ToString().PadRight(5) : "  -  ";
            var maskText = r.Mask is null
                ? string.Empty
                : $"  (mask: {r.Mask}{(r.MaskType is null ? string.Empty : $", {r.MaskType}")})";
            return $"  - {r.Name,-32} {sizeText}{maskText}";
        }));
    PrintSection("Range validations (RuleRange)", rangeRows.Select(r => $"  - {r.Name,-32} {r.Args}"));
    PrintSection("Regex validations (RuleRegularExpression)", regexRows.Select(r => $"  - {r.Name,-32} {r.Args}"));
    PrintSection("Unique value rules (DB-level, not enforced UI-side)",
        uniqueRows.Select(r => $"  - {r.Name,-32} {r.Args}"));

    DumpAppearanceRules(type);
}

static void DumpAppearanceRules(Type type)
{
    // Class-level Appearance attributes — walk the inheritance chain so
    // base-class rules (e.g. Asiakas.RuleRequiredField.Highlight) show up
    // alongside concrete-type rules (e.g. Henkilo.ShowEdunvalvonta).
    var rules = new List<string>();
    var t = type;
    while (t is not null && t != typeof(object))
    {
        foreach (var attr in t.GetCustomAttributesData())
        {
            if (attr.AttributeType.Name != "AppearanceAttribute") continue;
            rules.Add(FormatAppearance(attr, t));
        }
        t = t.BaseType;
    }

    PrintSection("Class-level Appearance rules", rules);
}

static string FormatAppearance(CustomAttributeData attr, Type declaringType)
{
    string? id           = null;
    string? targetItems  = null;
    string? criteria     = null;
    string? context      = null;
    int?    priority     = null;
    string? backColor    = null;
    string? fontColor    = null;
    int?    visibility   = null;
    bool?   enabled      = null;
    string? appearanceItemType = null;

    // First positional arg is usually the rule id.
    if (attr.ConstructorArguments.Count > 0 && attr.ConstructorArguments[0].Value is string rid)
        id = rid;

    foreach (var n in attr.NamedArguments)
    {
        var v = n.TypedValue.Value;
        switch (n.MemberName)
        {
            case "TargetItems":         targetItems = v?.ToString(); break;
            case "Criteria":            criteria    = v?.ToString(); break;
            case "Context":             context     = v?.ToString(); break;
            case "Priority":            priority    = v as int?;     break;
            case "BackColor":           backColor   = v?.ToString(); break;
            case "FontColor":           fontColor   = v?.ToString(); break;
            case "Visibility":          visibility  = v as int?;     break;
            case "Enabled":             enabled     = v as bool?;    break;
            case "AppearanceItemType":  appearanceItemType = v?.ToString(); break;
        }
    }

    var effects = new List<string>();
    if (visibility.HasValue) effects.Add($"Visibility={visibility}");
    if (enabled.HasValue)    effects.Add($"Enabled={enabled}");
    if (backColor is not null) effects.Add($"BackColor={backColor}");
    if (fontColor is not null) effects.Add($"FontColor={fontColor}");

    var effectText = effects.Count > 0 ? string.Join(", ", effects) : "(no effect)";
    var targetText = targetItems is null ? "*" : targetItems;
    var contextText = context is null ? "Any" : context;
    var idText = id is null ? "(unnamed)" : id;
    var origin = declaringType.Name;
    var criteriaText = criteria is null ? "always" : criteria;
    var itemTypeText = appearanceItemType is null ? "" : $" [item={appearanceItemType}]";
    var prioText = priority.HasValue ? $" prio={priority}" : "";

    return $"  - [{origin}] {idText}\n" +
           $"      target: {targetText}{itemTypeText}\n" +
           $"      effect: {effectText}\n" +
           $"      when:   {criteriaText}\n" +
           $"      ctx:    {contextText}{prioText}";
}

static void PrintSection(string title, IEnumerable<string> items)
{
    var list = items.ToList();
    if (list.Count == 0) return;
    Console.WriteLine($"## {title}");
    foreach (var line in list) Console.WriteLine(line);
    Console.WriteLine();
}

static bool HasAttribute(IList<CustomAttributeData> attrs, string name) =>
    attrs.Any(a => a.AttributeType.Name == name);

static bool? GetSingleBoolArg(IList<CustomAttributeData> attrs, string name)
{
    var attr = attrs.FirstOrDefault(a => a.AttributeType.Name == name);
    if (attr is null) return null;
    var arg = attr.ConstructorArguments.FirstOrDefault();
    return arg.Value is bool b ? b : null;
}

/// <summary>
/// Reads a ModelDefault("key", "value") attribute for a given key from a
/// member's attribute set. Returns null when the key isn't set.
/// </summary>
static string? ReadModelDefault(IList<CustomAttributeData> attrs, string key)
{
    foreach (var a in attrs)
    {
        if (a.AttributeType.Name != "ModelDefaultAttribute") continue;
        if (a.ConstructorArguments.Count < 2) continue;
        if (a.ConstructorArguments[0].Value is string k
            && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)
            && a.ConstructorArguments[1].Value is string v)
        {
            return v;
        }
    }
    return null;
}

static string RenderArgs(CustomAttributeData attr)
{
    var positional = attr.ConstructorArguments.Select(RenderTypedArg);
    var named = attr.NamedArguments.Select(n => $"{n.MemberName}={RenderTypedArg(n.TypedValue)}");
    return string.Join(", ", positional.Concat(named));
}

static string RenderTypedArg(CustomAttributeTypedArgument arg)
{
    if (arg.Value is null) return "null";
    if (arg.Value is string s) return $"\"{s}\"";
    if (arg.Value is Type t) return $"typeof({t.Name})";
    return arg.Value.ToString() ?? "?";
}
