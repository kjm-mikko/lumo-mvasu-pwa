using System.Reflection;
using DevExpress.Xpo;

// Reflection dump for xVasu persistent types. Run once per phase change to
// confirm property names against NAVIGATION.md / Model.xafml before wiring
// up XPO queries. Lives under tools/ so it's not in the runtime build graph.
//
// Usage:
//   dotnet run --project tools/xVasuReflect -- xVasu.Data.Security.xVasuSecuritySystemUserTask
//   dotnet run --project tools/xVasuReflect -- --list xVasu.Data.Security
//   dotnet run --project tools/xVasuReflect            (defaults to the task entity)

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
