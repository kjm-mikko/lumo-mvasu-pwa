using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Layout;

namespace mVasu.Api.Data;

/// <summary>
/// Headless XafApplication used as the Setup pipeline driver for the Web API.
/// It is not visited as a UI host — only the metadata, modules and ObjectSpace
/// machinery are reused. CreateLayoutManagerCore is the single abstract member;
/// it throws because layout managers are a UI concern that does not apply.
/// </summary>
public sealed class MVasuApiApplication : XafApplication
{
    public MVasuApiApplication()
    {
        ApplicationName = "Lumo.mVasu.Api";
    }

    protected override LayoutManager CreateLayoutManagerCore(bool simple)
        => throw new NotSupportedException("Web API host does not render layouts");
}
