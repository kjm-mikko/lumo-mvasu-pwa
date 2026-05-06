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
//   dotnet run --project tools/xVasuReflect -- --xaf-fields xVasu.Data.Asma.Henkilo
//   dotnet run --project tools/xVasuReflect -- --xaf-controllers xVasu.Data.Asma.Henkilo
//   dotnet run --project tools/xVasuReflect -- --xaf-rules xVasu.Data.Asma.Henkilo
//   dotnet run --project tools/xVasuReflect            (defaults to the task entity)
//
// The --xaf-fields and --xaf-controllers commands work for any XAF-managed
// persistent type — Tiskilista, Hakemus, Sopimus, … — and are how we
// reverse-engineer XAF DetailView layouts and ViewController action
// inventories without having Model.xafml on disk.
//
// `--xaf-rules <type>` is a sibling of `--xaf-fields` that produces a
// categorised dump (required / read-only / size / mask / range / regex /
// appearance) — used as the canonical spec for PWA detail views.

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

if (typeNames[0] == "--xaf-fields")
{
    if (typeNames.Length < 2)
    {
        Console.Error.WriteLine("--xaf-fields requires a type name (e.g. xVasu.Data.Asma.Henkilo)");
        return 1;
    }
    DumpXafFields(typeNames[1]);
    return 0;
}

if (typeNames[0] == "--xaf-controllers")
{
    if (typeNames.Length < 2)
    {
        Console.Error.WriteLine("--xaf-controllers requires a type name (e.g. xVasu.Data.Asma.Henkilo)");
        return 1;
    }
    DumpXafControllers(typeNames[1]);
    return 0;
}

if (typeNames[0] == "--xaf-all-controllers")
{
    // Diagnostic listing — shows every ViewController-derived class loaded
    // in the AppDomain plus the type each one targets (when discoverable).
    // Useful when --xaf-controllers <type> returns 0 to confirm whether the
    // controller assemblies are present at all.
    var nameFilter = typeNames.Length > 1 ? typeNames[1] : null;
    DumpAllXafControllers(nameFilter);
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

// -- XAF-aware reflection ---------------------------------------------------
//
// XAF reads display metadata (caption, ordering, validation) from a mix of
// (a) attributes on the persistent class and (b) Model.xafml diffs. We only
// have (a) without the running XAF host, but in practice attributes carry
// most of the layout intent — IndexAttribute, VisibleInDetailView,
// ModelDefault("Caption"), RuleRequiredField, Appearance, ImmediatePostData
// — so the dump is enough to reconstruct DetailView ordering and required
// fields for the PWA detail view.
//
// We use CustomAttributeData.GetCustomAttributes so we don't need a hard
// reference to DevExpress.Persistent.Base; attributes are matched by name.

// Attributes that influence DetailView/ListView rendering and validation.
// Kept as a method-local list because top-level program files can't host
// `static readonly` fields directly.
static string[] InterestingFieldAttributeNames() => new[]
{
    "BrowsableAttribute",
    "IndexAttribute",
    "VisibleInDetailViewAttribute",
    "VisibleInListViewAttribute",
    "VisibleInLookupListViewAttribute",
    "ModelDefaultAttribute",
    "ImmediatePostDataAttribute",
    "ToolTipAttribute",
    "DisplayNameAttribute",
    "RuleRequiredFieldAttribute",
    "RuleRangeAttribute",
    "RuleRegularExpressionAttribute",
    "RuleStringComparisonAttribute",
    "RuleUniqueValueAttribute",
    "AppearanceAttribute",
    "EditorAliasAttribute",
    "FieldSizeAttribute",
    "SizeAttribute",
    "DisplayFormatAttribute",
    "DataTypeAttribute",
};

static void DumpXafFields(string fullName)
{
    var type = ResolveType(fullName);
    if (type is null)
    {
        Console.WriteLine($"# {fullName} — NOT FOUND");
        return;
    }

    Console.WriteLine($"# XAF field metadata for {type.FullName}");
    Console.WriteLine($"  base: {type.BaseType?.FullName}");
    Console.WriteLine();

    // Class-level attributes (DefaultProperty, ImageName, NavigationItem, …)
    var classAttrs = CollectInterestingAttributes(type.GetCustomAttributesData());
    if (classAttrs.Count > 0)
    {
        Console.WriteLine("  Class-level attributes:");
        foreach (var a in classAttrs)
        {
            Console.WriteLine($"    {a}");
        }
        Console.WriteLine();
    }

    // Walk the inheritance chain; XAF DetailView merges attributes from base
    // classes, so dumping each level keeps ordering meaningful.
    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
        .OrderBy(p => GetIndexValue(p) ?? int.MaxValue)
        .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"  {"Property",-32} {"Type",-22} {"Index",-6} {"Browsable",-10} {"InDetail",-9} {"Required",-9} {"Notes"}");
    Console.WriteLine($"  {new string('-', 32)} {new string('-', 22)} {new string('-', 6)} {new string('-', 10)} {new string('-', 9)} {new string('-', 9)} {new string('-', 30)}");

    foreach (var prop in props)
    {
        var attrs = prop.GetCustomAttributesData();
        var index     = GetIndexValue(prop);
        var browsable = GetSingleBoolArg(attrs, "BrowsableAttribute");
        var inDetail  = GetSingleBoolArg(attrs, "VisibleInDetailViewAttribute");
        var required  = HasAttribute(attrs, "RuleRequiredFieldAttribute");
        var notes     = SummariseFieldAttributes(attrs);

        Console.WriteLine(
            $"  {Truncate(prop.Name, 32),-32} " +
            $"{Truncate(RenderType(prop.PropertyType), 22),-22} " +
            $"{(index?.ToString() ?? "-"),-6} " +
            $"{(browsable?.ToString() ?? "-"),-10} " +
            $"{(inDetail?.ToString() ?? "-"),-9} " +
            $"{(required ? "yes" : "-"),-9} " +
            $"{notes}");
    }
}

static int? GetIndexValue(PropertyInfo prop)
{
    var attr = prop.GetCustomAttributesData()
        .FirstOrDefault(a => a.AttributeType.Name == "IndexAttribute");
    if (attr is null) return null;
    var arg = attr.ConstructorArguments.FirstOrDefault();
    if (arg.Value is int i) return i;
    return null;
}

static bool? GetSingleBoolArg(IList<CustomAttributeData> attrs, string attributeName)
{
    var attr = attrs.FirstOrDefault(a => a.AttributeType.Name == attributeName);
    if (attr is null) return null;
    var arg = attr.ConstructorArguments.FirstOrDefault();
    return arg.Value is bool b ? b : null;
}

static bool HasAttribute(IList<CustomAttributeData> attrs, string attributeName) =>
    attrs.Any(a => a.AttributeType.Name == attributeName);

static string SummariseFieldAttributes(IList<CustomAttributeData> attrs)
{
    var interesting = InterestingFieldAttributeNames();
    var notes = new List<string>();
    foreach (var attr in attrs)
    {
        if (!interesting.Contains(attr.AttributeType.Name)) continue;

        // Already surfaced in dedicated columns.
        if (attr.AttributeType.Name is "BrowsableAttribute"
            or "VisibleInDetailViewAttribute"
            or "RuleRequiredFieldAttribute"
            or "IndexAttribute") continue;

        var args = string.Join(", ",
            attr.ConstructorArguments.Select(RenderArg)
                .Concat(attr.NamedArguments.Select(n => $"{n.MemberName}={RenderArg(n.TypedValue)}")));
        var tag = attr.AttributeType.Name.Replace("Attribute", "");
        notes.Add(args.Length > 0 ? $"{tag}({Truncate(args, 40)})" : tag);
    }
    return string.Join(" · ", notes);
}

static List<string> CollectInterestingAttributes(IList<CustomAttributeData> attrs)
{
    return attrs
        .Where(a => a.AttributeType.Name is "DefaultPropertyAttribute"
                                          or "DefaultClassOptionsAttribute"
                                          or "ImageNameAttribute"
                                          or "NavigationItemAttribute"
                                          or "VisibleInReportsAttribute"
                                          or "ModelDefaultAttribute"
                                          or "AppearanceAttribute")
        .Select(a =>
        {
            var args = string.Join(", ",
                a.ConstructorArguments.Select(RenderArg)
                    .Concat(a.NamedArguments.Select(n => $"{n.MemberName}={RenderArg(n.TypedValue)}")));
            var tag = a.AttributeType.Name.Replace("Attribute", "");
            return args.Length > 0 ? $"{tag}({args})" : tag;
        })
        .ToList();
}

static string RenderArg(CustomAttributeTypedArgument arg)
{
    if (arg.Value is null) return "null";
    if (arg.Value is string s) return $"\"{s}\"";
    if (arg.Value is Type t) return $"typeof({t.Name})";
    return arg.Value.ToString() ?? "?";
}

static void DumpXafControllers(string targetTypeFullName)
{
    // ViewControllers usually live in xVasu.Services or sibling assemblies
    // that aren't probed until something references their types — so
    // force-load every xVasu.*.dll and DevExpress.ExpressApp.*.dll sitting
    // next to this tool's binaries before we walk the AppDomain.
    PreloadXafAssemblies();

    var target = ResolveType(targetTypeFullName);
    if (target is null)
    {
        Console.WriteLine($"# {targetTypeFullName} — NOT FOUND");
        return;
    }

    var viewControllerType = ResolveType("DevExpress.ExpressApp.ViewController")
        ?? ResolveType("DevExpress.ExpressApp.Controller");
    if (viewControllerType is null)
    {
        Console.WriteLine("# DevExpress.ExpressApp.ViewController not loadable — is xVasu.Module referenced?");
        return;
    }
    var actionBaseType = ResolveType("DevExpress.ExpressApp.Actions.ActionBase");

    // Walk every controller in the loaded assemblies and keep the ones whose
    // declared target object type assignable from `target`. That covers both
    // `ObjectViewController<TView, Henkilo>` (generic arg) and the older
    // `controller.TargetObjectType = typeof(Henkilo)` runtime assignment —
    // for the latter we look at the constructor IL by reading
    // CustomAttributesData on the property setter, which is not always
    // available, so we fall back to instantiating the controller in a
    // try/catch and reading TargetObjectType.
    var controllers = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a =>
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
        })
        .Where(t => t is not null
                 && !t.IsAbstract
                 && viewControllerType.IsAssignableFrom(t))
        .Where(t => ControllerTargetsType(t!, target))
        .OrderBy(t => t!.FullName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"# ViewControllers targeting {target.FullName} ({controllers.Length})");
    if (controllers.Length == 0)
    {
        Console.WriteLine("  (no matches)");
        Console.WriteLine();
        Console.WriteLine("  Note: XAF controllers for xVasu.Data.* types live in a Web/Win/Blazor");
        Console.WriteLine("  module assembly (e.g. xVasu.Module.Web) that is NOT included in the");
        Console.WriteLine("  xVasu.Module NuGet package this tool references. To inventory actions,");
        Console.WriteLine("  cross-reference --xaf-fields output (look for Appearance attributes with");
        Console.WriteLine("  AppearanceItemType=\"Action\" — TargetItems names the action ID) or load");
        Console.WriteLine("  the deployed module DLL directly via Assembly.LoadFrom in this tool.");
        return;
    }

    foreach (var ctrl in controllers)
    {
        Console.WriteLine($"  {ctrl!.FullName}");
        Console.WriteLine($"    assembly: {ctrl.Assembly.GetName().Name}");

        // Action members declared on the controller — usually fields like
        // `private SimpleAction MyAction` or properties exposing them.
        if (actionBaseType is not null)
        {
            var actionMembers = ctrl
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => actionBaseType.IsAssignableFrom(f.FieldType))
                .Select(f => (Name: f.Name, Type: f.FieldType, Source: "field"))
                .Concat(ctrl
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => actionBaseType.IsAssignableFrom(p.PropertyType))
                    .Select(p => (Name: p.Name, Type: p.PropertyType, Source: "property")))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (actionMembers.Length > 0)
            {
                Console.WriteLine($"    actions:");
                foreach (var a in actionMembers)
                {
                    Console.WriteLine($"      {a.Name,-32} {RenderType(a.Type),-30} ({a.Source})");
                }
            }
        }
        Console.WriteLine();
    }
}

static void DumpAllXafControllers(string? nameFilter)
{
    PreloadXafAssemblies();

    var viewControllerType = ResolveType("DevExpress.ExpressApp.ViewController")
        ?? ResolveType("DevExpress.ExpressApp.Controller");
    if (viewControllerType is null)
    {
        Console.WriteLine("# DevExpress.ExpressApp.ViewController not loadable.");
        return;
    }

    var controllers = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a =>
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
        })
        .Where(t => t is not null
                 && !t.IsAbstract
                 && viewControllerType.IsAssignableFrom(t))
        .Where(t => nameFilter is null
                 || (t!.FullName ?? "").Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
        .OrderBy(t => t!.FullName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"# All loaded ViewController-derived classes" +
                      (nameFilter is null ? "" : $" (filter: {nameFilter})") +
                      $" ({controllers.Length})");

    foreach (var ctrl in controllers)
    {
        var targetGuess = TryGuessControllerTarget(ctrl!);
        Console.WriteLine($"  {ctrl!.FullName}");
        if (!string.IsNullOrEmpty(targetGuess))
        {
            Console.WriteLine($"    target: {targetGuess}");
        }
    }
}

static string TryGuessControllerTarget(Type controllerType)
{
    // Walk inheritance for generic args first — handles ObjectViewController<TView, T>.
    var t = controllerType;
    while (t is not null && t != typeof(object))
    {
        if (t.IsGenericType)
        {
            var args = t.GetGenericArguments()
                .Where(g => !g.IsGenericParameter)
                .Select(g => g.FullName ?? g.Name)
                .ToArray();
            if (args.Length > 0)
            {
                return $"generic args: {string.Join(", ", args)}";
            }
        }
        t = t.BaseType;
    }

    // Fall back to instantiation if the constructor allows it.
    try
    {
        var instance = Activator.CreateInstance(controllerType);
        var prop = controllerType.GetProperty("TargetObjectType");
        if (prop?.GetValue(instance) is Type runtimeTarget)
        {
            return $"TargetObjectType: {runtimeTarget.FullName}";
        }
    }
    catch (Exception ex)
    {
        return $"instantiation failed: {ex.GetType().Name}";
    }
    return string.Empty;
}

static void PreloadXafAssemblies()
{
    var binDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
    if (string.IsNullOrEmpty(binDir) || !Directory.Exists(binDir)) return;

    foreach (var dll in Directory.EnumerateFiles(binDir, "*.dll"))
    {
        var name = Path.GetFileNameWithoutExtension(dll);
        if (!name.StartsWith("xVasu", StringComparison.OrdinalIgnoreCase)
         && !name.StartsWith("DevExpress.ExpressApp", StringComparison.OrdinalIgnoreCase)
         && !name.StartsWith("Llamachant", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == name)) continue;
        try
        {
            Assembly.LoadFrom(dll);
        }
        catch
        {
            // Some satellite DLLs are resource-only or have native deps;
            // skipping them keeps the controller scan moving.
        }
    }
}

static bool ControllerTargetsType(Type controllerType, Type target)
{
    // 1. Generic ObjectViewController<TView, T> — match T against `target`.
    var t = controllerType;
    while (t is not null && t != typeof(object))
    {
        if (t.IsGenericType)
        {
            foreach (var arg in t.GetGenericArguments())
            {
                if (arg.IsAssignableFrom(target) || target.IsAssignableFrom(arg))
                {
                    return true;
                }
            }
        }
        t = t.BaseType;
    }

    // 2. Runtime-set TargetObjectType — try to instantiate. Many controllers
    // need a parameterless constructor and don't touch the security system,
    // so this works for the action-listing controllers we care about.
    try
    {
        var instance = Activator.CreateInstance(controllerType);
        var prop = controllerType.GetProperty("TargetObjectType");
        if (prop?.GetValue(instance) is Type t2)
        {
            return t2.IsAssignableFrom(target) || target.IsAssignableFrom(t2);
        }
    }
    catch
    {
        // Constructor needs a host or dependencies we don't have.
        // Accept that those controllers won't show up in the list.
    }

    return false;
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
// Re-uses the HasAttribute / GetSingleBoolArg / RenderArg helpers
// already defined for --xaf-fields.

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
            rangeRows.Add((prop.Name, RenderAttrArgs(rangeAttr)));

        var regexAttr = attrs.FirstOrDefault(a => a.AttributeType.Name == "RuleRegularExpressionAttribute");
        if (regexAttr is not null)
            regexRows.Add((prop.Name, RenderAttrArgs(regexAttr)));

        var uniqAttr = attrs.FirstOrDefault(a => a.AttributeType.Name == "RuleUniqueValueAttribute");
        if (uniqAttr is not null)
            uniqueRows.Add((prop.Name, RenderAttrArgs(uniqAttr)));
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

    var effectText   = effects.Count > 0 ? string.Join(", ", effects) : "(no effect)";
    var targetText   = targetItems ?? "*";
    var contextText  = context ?? "Any";
    var idText       = id ?? "(unnamed)";
    var origin       = declaringType.Name;
    var criteriaText = criteria ?? "always";
    var itemTypeText = appearanceItemType is null ? "" : $" [item={appearanceItemType}]";
    var prioText     = priority.HasValue ? $" prio={priority}" : "";

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

/// <summary>Reads ModelDefault("key", "value") for a given key from a member's attributes.</summary>
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

/// <summary>Renders all positional + named args of an attribute as a single comma-joined string.</summary>
static string RenderAttrArgs(CustomAttributeData attr)
{
    var positional = attr.ConstructorArguments.Select(RenderArg);
    var named = attr.NamedArguments.Select(n => $"{n.MemberName}={RenderArg(n.TypedValue)}");
    return string.Join(", ", positional.Concat(named));
}
