using DevExpress.ExpressApp;
using DevExpress.Xpo;
using xVasu.Data.Security;

namespace mVasu.Api.Domain;

/// <summary>
/// XAF module that exports the persistent types this Web API uses. Adding the
/// types to <see cref="ModuleBase.AdditionalExportedTypes"/> here is the only
/// reliable way to register them with XAF's business model — calling
/// XafTypesInfo.Instance.RegisterEntity directly does not produce the same
/// metadata, so CompositeObjectSpace.FindObject would fail without this module
/// being loaded into a XafApplication.Setup pipeline.
/// </summary>
public sealed class MVasuApiModule : ModuleBase
{
    public MVasuApiModule()
    {
        AdditionalExportedTypes.Add(typeof(MVasuUserSettings));

        // Scan xVasu.Module assembly for every persistent type under
        // xVasu.Data.* and xVasu.Modules.* so any class the resolver or
        // future endpoints touch (associations of xVasuSecuritySystemUser,
        // domain entities) is exported. Mirrors the manual list maintained
        // in VasuCommonBlazorModule on the legacy side.
        var assembly = typeof(xVasuSecuritySystemUser).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !type.IsPublic) continue;
            if (type.Namespace is null) continue;
            if (!type.Namespace.StartsWith("xVasu.", StringComparison.Ordinal)) continue;
            if (!typeof(PersistentBase).IsAssignableFrom(type)) continue;

            if (!AdditionalExportedTypes.Contains(type))
            {
                AdditionalExportedTypes.Add(type);
            }
        }
    }
}
