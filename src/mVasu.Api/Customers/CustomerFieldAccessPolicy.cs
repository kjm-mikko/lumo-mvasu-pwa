using System.Collections.Frozen;
using DevExpress.Xpo;
using xVasu.Data.Asma;
using xVasu.Data.Security;

namespace mVasu.Api.Customers;

/// <summary>
/// Per-request snapshot of which Asma properties the caller is allowed
/// to read. Stored as XAF property names (PascalCase, matching the CLR
/// types) so the customer mappers can query it directly while
/// projecting wire DTOs.
/// </summary>
/// <remarks>
/// <para>The snapshot is the output of <see cref="CustomerFieldAccessPolicy"/>
/// — for admins it is the singleton <see cref="AllowAll"/>; for everyone
/// else it's a freshly-computed instance per request. It is intentionally
/// trivial to copy/pass since callers compute it once per call and apply
/// it across a few mapper invocations.</para>
///
/// <para>Property names are scoped per concrete type. <see cref="CanRead"/>
/// looks up a property in the named type's allowed set; XAF's
/// inheritance semantics — a grant on <c>Asiakas</c> applies to all four
/// concrete types — are handled at compute time by the policy itself
/// (the property is mirrored into all four sets).</para>
/// </remarks>
public sealed record CustomerFieldAccessSnapshot(
    bool AllAllowed,
    IReadOnlySet<string> AsiakasFields,
    IReadOnlySet<string> HenkiloFields,
    IReadOnlySet<string> YritysFields,
    IReadOnlySet<string> YhteyshenkiloFields)
{
    public static CustomerFieldAccessSnapshot AllowAll { get; } =
        new(true, FrozenSet<string>.Empty, FrozenSet<string>.Empty, FrozenSet<string>.Empty, FrozenSet<string>.Empty);

    public static CustomerFieldAccessSnapshot DenyAll { get; } =
        new(false, FrozenSet<string>.Empty, FrozenSet<string>.Empty, FrozenSet<string>.Empty, FrozenSet<string>.Empty);

    public bool CanRead(Type clrType, string xafProperty)
    {
        if (AllAllowed) return true;
        var set = SetFor(clrType);
        return set is not null && set.Contains(xafProperty);
    }

    private IReadOnlySet<string>? SetFor(Type clrType)
    {
        if (clrType == typeof(Asiakas))       return AsiakasFields;
        if (clrType == typeof(Henkilo))       return HenkiloFields;
        if (clrType == typeof(Yritys))        return YritysFields;
        if (clrType == typeof(Yhteyshenkilo)) return YhteyshenkiloFields;
        return null;
    }
}

/// <summary>
/// Defensive PermissionPolicy walk for the customer detail/list flow.
/// Reads the resolved <see cref="xVasuSecuritySystemUser"/>'s roles and
/// produces a <see cref="CustomerFieldAccessSnapshot"/> the customer
/// query service uses to nullify wire fields the caller cannot read.
/// </summary>
/// <remarks>
/// <para>This is the B2 cut from the original Vaihe B plan. The "right"
/// XAF approach would set up <c>SecurityStrategyComplex</c> +
/// <c>SecuredObjectSpaceProvider</c> and let the framework do this in a
/// SQL filter, but that change touches the auth pipeline and needs a
/// non-admin test user before it's verifiable. See memory note
/// <c>project_non_admin_test_user.md</c> — the dev DB is admin-only, so
/// the fast path here fires for every real call today and the walk is
/// covered by unit tests instead.</para>
///
/// <para>Semantic shortcut for ease of review: <c>user.IsAdmistrator()</c>
/// is the existing entity helper xVasu exposes (note the spelling — that
/// is the actual method name) and short-circuits to AllowAll. For
/// non-admins, each role contributes its allowed members; the final
/// snapshot is the union across roles (Allow wins over Deny across
/// roles, matching XAF's "most permissive role" rule).</para>
/// </remarks>
public sealed class CustomerFieldAccessPolicy(ILogger<CustomerFieldAccessPolicy> logger)
{
    /// <summary>
    /// Properties that the customer service projects from
    /// <c>xVasu.Data.Asma.Asiakas</c> (and its three subclasses). The
    /// policy walk only honors entries in this set — anything outside
    /// is "not relevant", treated as not-checked.
    /// </summary>
    /// <remarks>
    /// Mirrors what <c>XpoCustomerQueryService.LoadAsiakasBaseRows</c>
    /// pulls. Keep in sync.
    /// </remarks>
    private static readonly FrozenSet<string> AsiakasProperties = FrozenSet.ToFrozenSet(new[]
    {
        "AsiakasNumero",
        "OnHenkilo",
        "OnYritys",
        "SukuNimi",
        "KatuOsoite",
        "PostiNumero",
        "PostiToimiPaikka",
        "Email",
        "Gsm",
        "Puhelin",
        "Maa",
        "LangCode",
        "ToimiAla",
        "TyoPaikka",
        "BruttoTulot",
        "EmailKayttoSallittu",
        "PuhNoKayttoSallittu",
        "Suoramarkkinointikielto",
    });

    private static readonly FrozenSet<string> HenkiloProperties = FrozenSet.ToFrozenSet(new[]
    {
        "AsiakasNumero", "EtuNimi", "SukuNimi", "Ammatti",
    });

    private static readonly FrozenSet<string> YritysProperties = FrozenSet.ToFrozenSet(new[]
    {
        "AsiakasNumero", "CompanyID", "SukuNimi",
    });

    private static readonly FrozenSet<string> YhteyshenkiloProperties = FrozenSet.ToFrozenSet(new[]
    {
        "AsiakasNumero", "EtuNimi", "SukuNimi",
    });

    public CustomerFieldAccessSnapshot Evaluate(xVasuSecuritySystemUser? user)
    {
        if (user is null)
        {
            return CustomerFieldAccessSnapshot.DenyAll;
        }

        // Admin fast-path. xVasu's user model exposes both IsAdminUser
        // (a derived flag from role membership) and IsAdmistrator (the
        // class-level role marker xVasu uses). The latter is the one
        // the persistent layer evaluates as "skip permission checks";
        // anyone for whom either is true is treated as full-access here.
        try
        {
            // IsAdminUser is a static class-level admin probe (returns
            // true for the current logon's user); IsAdmistrator is the
            // instance-level role marker on the row. Either should
            // short-circuit to AllowAll in our flow.
            if (user.IsAdmistrator() || xVasuSecuritySystemUser.IsAdminUser())
            {
                return CustomerFieldAccessSnapshot.AllowAll;
            }
        }
        catch (Exception ex)
        {
            // The flag method walks XPO collections internally and can
            // raise CannotLoadObjects on dev DBs with broken FKs. Falling
            // through to the role walk is fine — non-admins simply hit
            // the slow path.
            logger.LogDebug(ex, "Admin flag check failed for {Email}; falling back to role walk", user.UserName);
        }

        try
        {
            var roles = ExtractRoles(user);
            return Compute(isAdmin: false, roles);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Field access policy walk failed for user {Email}; denying customer field access",
                user.UserName);
            return CustomerFieldAccessSnapshot.DenyAll;
        }
    }

    /// <summary>
    /// Pure evaluator — separated from the XPO walk so the union
    /// semantics across roles can be unit tested without an XPO Session.
    /// </summary>
    public static CustomerFieldAccessSnapshot Compute(bool isAdmin, IEnumerable<RoleAccessSpec> roles)
    {
        if (isAdmin) return CustomerFieldAccessSnapshot.AllowAll;

        var asiakas        = new HashSet<string>(StringComparer.Ordinal);
        var henkilo        = new HashSet<string>(StringComparer.Ordinal);
        var yritys         = new HashSet<string>(StringComparer.Ordinal);
        var yhteyshenkilo  = new HashSet<string>(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            if (role.IsAdministrative)
            {
                // An administrative role grants everything; we can shortcut
                // to AllowAll the moment we see one.
                return CustomerFieldAccessSnapshot.AllowAll;
            }

            ApplyRole(role, asiakas, henkilo, yritys, yhteyshenkilo);
        }

        return new CustomerFieldAccessSnapshot(
            AllAllowed:          false,
            AsiakasFields:       asiakas.ToFrozenSet(),
            HenkiloFields:       henkilo.ToFrozenSet(),
            YritysFields:        yritys.ToFrozenSet(),
            YhteyshenkiloFields: yhteyshenkilo.ToFrozenSet());
    }

    private static void ApplyRole(
        RoleAccessSpec role,
        HashSet<string> asiakas,
        HashSet<string> henkilo,
        HashSet<string> yritys,
        HashSet<string> yhteyshenkilo)
    {
        // Resolve the per-type permission entry the role declares for
        // each Asma type. Asiakas is the base — properties allowed there
        // mirror into all three subtype sets per XAF inheritance.
        var asiakasTp        = FindTypePermission(role, "Asiakas");
        var henkiloTp        = FindTypePermission(role, "Henkilo");
        var yritysTp         = FindTypePermission(role, "Yritys");
        var yhteyshenkiloTp  = FindTypePermission(role, "Yhteyshenkilo");

        // Asiakas-level grants apply to every concrete subtype.
        ApplyTypePermission(role, asiakasTp, AsiakasProperties, asiakas);
        ApplyTypePermission(role, asiakasTp, AsiakasProperties, henkilo);
        ApplyTypePermission(role, asiakasTp, AsiakasProperties, yritys);
        ApplyTypePermission(role, asiakasTp, AsiakasProperties, yhteyshenkilo);

        // Subtype-level grants only apply to that concrete subtype.
        ApplyTypePermission(role, henkiloTp,        HenkiloProperties,       henkilo);
        ApplyTypePermission(role, yritysTp,         YritysProperties,        yritys);
        ApplyTypePermission(role, yhteyshenkiloTp,  YhteyshenkiloProperties, yhteyshenkilo);
    }

    private static TypeAccessSpec? FindTypePermission(RoleAccessSpec role, string typeName) =>
        role.Types.FirstOrDefault(t => string.Equals(t.TypeName, typeName, StringComparison.Ordinal));

    private static void ApplyTypePermission(
        RoleAccessSpec role,
        TypeAccessSpec? tp,
        FrozenSet<string> candidateProperties,
        HashSet<string> sink)
    {
        // Default per-property state when no explicit member entry is
        // declared: the type-level ReadState if set, otherwise the role's
        // global PermissionPolicy default.
        var typeDefault = tp?.ReadState
            ?? (role.DefaultPolicy == SecurityPermissionPolicyKind.AllowAllByDefault
                ? SecurityPermissionStateKind.Allow
                : SecurityPermissionStateKind.Deny);

        foreach (var property in candidateProperties)
        {
            var memberOverride = tp?.Members
                .FirstOrDefault(m => m.Members.Contains(property, StringComparer.Ordinal));

            var effectiveState = memberOverride?.ReadState ?? typeDefault;
            if (effectiveState == SecurityPermissionStateKind.Allow)
            {
                sink.Add(property);
            }
        }
    }

    /// <summary>
    /// Walks the user's <c>Roles</c> XPCollection and projects each
    /// role's PermissionPolicy state into the framework-agnostic
    /// <see cref="RoleAccessSpec"/> shape <see cref="Compute"/> consumes.
    /// All XPO entity access happens here so the rest of the policy is
    /// pure and unit-testable.
    /// </summary>
    private static IEnumerable<RoleAccessSpec> ExtractRoles(xVasuSecuritySystemUser user)
    {
        // Roles is XPCollection<PermissionPolicyRole> on the base
        // PermissionPolicyUser. Read non-generically through reflection
        // so we don't need a hard reference to the role type at compile
        // time and don't have to chase the specific generic argument
        // xVasu picks.
        var rolesProp = user.GetType().GetProperty("Roles");
        if (rolesProp?.GetValue(user) is not System.Collections.IEnumerable rolesCollection)
        {
            yield break;
        }

        foreach (var roleObj in rolesCollection)
        {
            if (roleObj is null) continue;
            var spec = ProjectRole(roleObj);
            if (spec is not null) yield return spec;
        }
    }

    private static RoleAccessSpec? ProjectRole(object roleObj)
    {
        var roleType = roleObj.GetType();

        var isAdministrative = ReadBool(roleObj, roleType, "IsAdministrative");

        var defaultPolicyValue = roleType.GetProperty("PermissionPolicy")?.GetValue(roleObj);
        var defaultPolicy = string.Equals(defaultPolicyValue?.ToString(), "AllowAllByDefault", StringComparison.Ordinal)
            ? SecurityPermissionPolicyKind.AllowAllByDefault
            : SecurityPermissionPolicyKind.DenyAllByDefault;

        var typesList = new List<TypeAccessSpec>();

        if (roleType.GetProperty("TypePermissions")?.GetValue(roleObj)
                is System.Collections.IEnumerable typePermissions)
        {
            foreach (var tpObj in typePermissions)
            {
                if (tpObj is null) continue;
                var tp = ProjectTypePermission(tpObj);
                if (tp is not null) typesList.Add(tp);
            }
        }

        return new RoleAccessSpec(
            IsAdministrative: isAdministrative,
            DefaultPolicy:    defaultPolicy,
            Types:            typesList);
    }

    private static TypeAccessSpec? ProjectTypePermission(object tpObj)
    {
        var tpType = tpObj.GetType();
        var targetType = tpType.GetProperty("TargetType")?.GetValue(tpObj) as Type;
        var targetTypeName = targetType?.Name
            ?? tpType.GetProperty("TargetTypeFullName")?.GetValue(tpObj)?.ToString()?.Split('.').LastOrDefault()
            ?? string.Empty;
        if (string.IsNullOrEmpty(targetTypeName)) return null;

        var readState = ReadPermissionState(tpObj, tpType, "ReadState");

        var members = new List<MemberAccessSpec>();
        if (tpType.GetProperty("MemberPermissions")?.GetValue(tpObj)
                is System.Collections.IEnumerable memberCollection)
        {
            foreach (var mpObj in memberCollection)
            {
                if (mpObj is null) continue;
                var mp = ProjectMemberPermission(mpObj);
                if (mp is not null) members.Add(mp);
            }
        }

        return new TypeAccessSpec(
            TypeName:  targetTypeName,
            ReadState: readState,
            Members:   members);
    }

    private static MemberAccessSpec? ProjectMemberPermission(object mpObj)
    {
        var mpType = mpObj.GetType();
        var membersString = mpType.GetProperty("Members")?.GetValue(mpObj) as string;
        if (string.IsNullOrEmpty(membersString)) return null;

        var memberNames = membersString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (memberNames.Length == 0) return null;

        var readState = ReadPermissionState(mpObj, mpType, "ReadState");
        return new MemberAccessSpec(memberNames, readState);
    }

    private static bool ReadBool(object obj, Type type, string propertyName) =>
        type.GetProperty(propertyName)?.GetValue(obj) is true;

    private static SecurityPermissionStateKind? ReadPermissionState(object obj, Type type, string propertyName)
    {
        var raw = type.GetProperty(propertyName)?.GetValue(obj);
        if (raw is null) return null;

        // XAF returns Nullable<SecurityPermissionState>; reflection gives
        // us the boxed underlying enum value or null.
        return raw.ToString() switch
        {
            "Allow" => SecurityPermissionStateKind.Allow,
            "Deny"  => SecurityPermissionStateKind.Deny,
            _       => null,
        };
    }
}

/// <summary>
/// Framework-agnostic projection of a single PermissionPolicyRole — the
/// only fields <see cref="CustomerFieldAccessPolicy.Compute"/> needs to
/// run its union walk.
/// </summary>
public sealed record RoleAccessSpec(
    bool IsAdministrative,
    SecurityPermissionPolicyKind DefaultPolicy,
    IReadOnlyList<TypeAccessSpec> Types);

public sealed record TypeAccessSpec(
    string TypeName,
    SecurityPermissionStateKind? ReadState,
    IReadOnlyList<MemberAccessSpec> Members);

public sealed record MemberAccessSpec(
    IReadOnlyList<string> Members,
    SecurityPermissionStateKind? ReadState);

/// <summary>
/// Local mirror of <c>DevExpress.Persistent.Base.SecurityPermissionPolicy</c>.
/// We project to a local enum so callers and tests don't have to reference
/// the DevExpress assembly that owns the original enum.
/// </summary>
public enum SecurityPermissionPolicyKind
{
    DenyAllByDefault,
    AllowAllByDefault,
}

public enum SecurityPermissionStateKind
{
    Allow,
    Deny,
}
