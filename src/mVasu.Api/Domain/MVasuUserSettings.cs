using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using xVasu.Data.Security;

namespace mVasu.Api.Domain;

[NavigationItem(false)]
[DefaultClassOptions]
public class MVasuUserSettings : BaseObject
{
    public MVasuUserSettings(Session session) : base(session) { }

    // One-way XPO reference (no [Association]): the FK lives only on this side,
    // xVasuSecuritySystemUser is not modified to expose a settings collection.
    // Keeping the relation one-way means we own all schema and code changes
    // on the mVasu.Api side and never have to republish xVasu.Module.
    public xVasuSecuritySystemUser User
    {
        get => GetPropertyValue<xVasuSecuritySystemUser>(nameof(User));
        set => SetPropertyValue(nameof(User), value);
    }

    [Size(100)]
    public string? PreferredName
    {
        get => GetPropertyValue<string?>(nameof(PreferredName));
        set => SetPropertyValue(nameof(PreferredName), value);
    }

    [Size(20)]
    public string Theme
    {
        get => GetPropertyValue<string>(nameof(Theme)) ?? "light";
        set => SetPropertyValue(nameof(Theme), value);
    }

    [Size(10)]
    public string Language
    {
        get => GetPropertyValue<string>(nameof(Language)) ?? "fi";
        set => SetPropertyValue(nameof(Language), value);
    }

    public bool LocationConsent
    {
        get => GetPropertyValue<bool>(nameof(LocationConsent));
        set => SetPropertyValue(nameof(LocationConsent), value);
    }

    [Size(SizeAttribute.Unlimited)]
    public string? SettingsJson
    {
        get => GetPropertyValue<string?>(nameof(SettingsJson));
        set => SetPropertyValue(nameof(SettingsJson), value);
    }
}
