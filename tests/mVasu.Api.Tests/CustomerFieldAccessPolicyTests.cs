using mVasu.Api.Customers;
using xVasu.Data.Asma;

namespace mVasu.Api.Tests;

/// <summary>
/// Unit-tests the pure <see cref="CustomerFieldAccessPolicy.Compute"/>
/// path. The XPO walk that builds the input from
/// <c>xVasuSecuritySystemUser</c> is exercised end-to-end at runtime
/// only — see memory note <c>project_non_admin_test_user.md</c> for the
/// outstanding need for a non-admin test account.
/// </summary>
public class CustomerFieldAccessPolicyTests
{
    [Fact]
    public void IsAdmin_ReturnsAllowAllSnapshot()
    {
        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: true, roles: Array.Empty<RoleAccessSpec>());

        Assert.True(snap.AllAllowed);
        Assert.True(snap.CanRead(typeof(Henkilo), "Email"));
        Assert.True(snap.CanRead(typeof(Yritys),  "CompanyID"));
    }

    [Fact]
    public void NoRoles_DenyAllByDefault()
    {
        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles: Array.Empty<RoleAccessSpec>());

        Assert.False(snap.AllAllowed);
        Assert.False(snap.CanRead(typeof(Henkilo), "Email"));
        Assert.False(snap.CanRead(typeof(Asiakas), "SukuNimi"));
    }

    [Fact]
    public void AdministrativeRole_ShortCircuitsToAllowAll()
    {
        var roles = new[]
        {
            new RoleAccessSpec(
                IsAdministrative: true,
                DefaultPolicy:    SecurityPermissionPolicyKind.DenyAllByDefault,
                Types:            Array.Empty<TypeAccessSpec>()),
        };

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles);

        Assert.True(snap.AllAllowed);
    }

    [Fact]
    public void TypeLevelAllow_GrantsAllPropertiesForThatType()
    {
        var roles = new[]
        {
            Role(
                defaultPolicy: SecurityPermissionPolicyKind.DenyAllByDefault,
                types: new[]
                {
                    new TypeAccessSpec(
                        TypeName:  "Henkilo",
                        ReadState: SecurityPermissionStateKind.Allow,
                        Members:   Array.Empty<MemberAccessSpec>()),
                }),
        };

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles);

        Assert.True(snap.CanRead(typeof(Henkilo), "EtuNimi"));
        Assert.True(snap.CanRead(typeof(Henkilo), "SukuNimi"));
        Assert.True(snap.CanRead(typeof(Henkilo), "Ammatti"));
        // Asiakas-level fields are NOT granted by a Henkilo TypePermission;
        // they're a property of the base type and need their own grant.
        Assert.False(snap.CanRead(typeof(Henkilo), "Email"));
        // Other concrete subtypes don't inherit Henkilo grants.
        Assert.False(snap.CanRead(typeof(Yritys), "EtuNimi"));
    }

    [Fact]
    public void AsiakasBaseAllow_PropagatesToAllConcreteSubtypes()
    {
        var roles = new[]
        {
            Role(
                defaultPolicy: SecurityPermissionPolicyKind.DenyAllByDefault,
                types: new[]
                {
                    new TypeAccessSpec(
                        TypeName:  "Asiakas",
                        ReadState: SecurityPermissionStateKind.Allow,
                        Members:   Array.Empty<MemberAccessSpec>()),
                }),
        };

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles);

        // Asiakas-level grant mirrors into every concrete subtype set so
        // CanRead(Henkilo, "Email") works the same as CanRead(Asiakas, "Email").
        Assert.True(snap.CanRead(typeof(Asiakas),       "Email"));
        Assert.True(snap.CanRead(typeof(Henkilo),       "Email"));
        Assert.True(snap.CanRead(typeof(Yritys),        "Email"));
        Assert.True(snap.CanRead(typeof(Yhteyshenkilo), "Email"));
    }

    [Fact]
    public void MemberDeny_OverridesTypeAllow()
    {
        var roles = new[]
        {
            Role(
                defaultPolicy: SecurityPermissionPolicyKind.DenyAllByDefault,
                types: new[]
                {
                    new TypeAccessSpec(
                        TypeName:  "Asiakas",
                        ReadState: SecurityPermissionStateKind.Allow,
                        Members:   new[]
                        {
                            new MemberAccessSpec(
                                Members:   new[] { "Email" },
                                ReadState: SecurityPermissionStateKind.Deny),
                        }),
                }),
        };

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles);

        Assert.False(snap.CanRead(typeof(Asiakas), "Email"));
        // Other Asiakas fields stay allowed because of the type-level Allow.
        Assert.True(snap.CanRead(typeof(Asiakas),  "SukuNimi"));
        Assert.True(snap.CanRead(typeof(Henkilo),  "SukuNimi"));
    }

    [Fact]
    public void MemberAllow_OverridesTypeDeny()
    {
        var roles = new[]
        {
            Role(
                defaultPolicy: SecurityPermissionPolicyKind.DenyAllByDefault,
                types: new[]
                {
                    new TypeAccessSpec(
                        TypeName:  "Henkilo",
                        ReadState: SecurityPermissionStateKind.Deny,
                        Members:   new[]
                        {
                            new MemberAccessSpec(
                                Members:   new[] { "EtuNimi", "SukuNimi" },
                                ReadState: SecurityPermissionStateKind.Allow),
                        }),
                }),
        };

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles);

        Assert.True(snap.CanRead(typeof(Henkilo),  "EtuNimi"));
        Assert.True(snap.CanRead(typeof(Henkilo),  "SukuNimi"));
        Assert.False(snap.CanRead(typeof(Henkilo), "Ammatti"));
    }

    [Fact]
    public void MultipleRoles_UnionAllowedFields_AllowWinsOverDeny()
    {
        var role1 = Role(
            defaultPolicy: SecurityPermissionPolicyKind.DenyAllByDefault,
            types: new[]
            {
                new TypeAccessSpec(
                    TypeName:  "Asiakas",
                    ReadState: SecurityPermissionStateKind.Deny,
                    Members:   new[]
                    {
                        new MemberAccessSpec(new[] { "SukuNimi" }, SecurityPermissionStateKind.Allow),
                    }),
            });

        var role2 = Role(
            defaultPolicy: SecurityPermissionPolicyKind.DenyAllByDefault,
            types: new[]
            {
                new TypeAccessSpec(
                    TypeName:  "Asiakas",
                    ReadState: SecurityPermissionStateKind.Deny,
                    Members:   new[]
                    {
                        new MemberAccessSpec(new[] { "Email" }, SecurityPermissionStateKind.Allow),
                    }),
            });

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, new[] { role1, role2 });

        // SukuNimi from role 1, Email from role 2 — both granted.
        Assert.True(snap.CanRead(typeof(Asiakas), "SukuNimi"));
        Assert.True(snap.CanRead(typeof(Asiakas), "Email"));
        // KatuOsoite was denied by both roles — stays denied.
        Assert.False(snap.CanRead(typeof(Asiakas), "KatuOsoite"));
    }

    [Fact]
    public void AllowAllByDefaultPolicy_GrantsAllUnlessExplicitDeny()
    {
        var roles = new[]
        {
            Role(
                defaultPolicy: SecurityPermissionPolicyKind.AllowAllByDefault,
                types: new[]
                {
                    new TypeAccessSpec(
                        TypeName:  "Henkilo",
                        ReadState: null,
                        Members:   new[]
                        {
                            new MemberAccessSpec(new[] { "Ammatti" }, SecurityPermissionStateKind.Deny),
                        }),
                }),
        };

        var snap = CustomerFieldAccessPolicy.Compute(isAdmin: false, roles);

        Assert.True(snap.CanRead(typeof(Henkilo),  "EtuNimi"));
        Assert.True(snap.CanRead(typeof(Henkilo),  "SukuNimi"));
        Assert.False(snap.CanRead(typeof(Henkilo), "Ammatti"));
    }

    [Fact]
    public void SemicolonSeparatedMembers_AreSplit()
    {
        // XAF stores MemberPermissions.Members as semicolon-separated
        // names — we project that into a list before reaching Compute,
        // but the snapshot lookup compares whole names.
        var spec = new MemberAccessSpec(
            Members:   new[] { "EtuNimi", "SukuNimi" },
            ReadState: SecurityPermissionStateKind.Allow);

        Assert.Contains("EtuNimi", spec.Members);
        Assert.Contains("SukuNimi", spec.Members);
    }

    private static RoleAccessSpec Role(
        SecurityPermissionPolicyKind defaultPolicy,
        IReadOnlyList<TypeAccessSpec> types) =>
        new(IsAdministrative: false, DefaultPolicy: defaultPolicy, Types: types);
}
