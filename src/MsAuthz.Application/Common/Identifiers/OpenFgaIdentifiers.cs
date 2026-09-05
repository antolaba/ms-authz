namespace MsAuthz.Application.Common.Identifiers;

/// <summary>
/// Single place that builds and parses every OpenFGA object identifier used by ms-authz.
/// Nothing outside this class should concatenate "user:", "role:" or "permission:" strings by hand
/// (MS-AUTHZ-SPEC.md §4).
///
/// Object shape: exactly one ':' (type separator, reserved by OpenFGA) and exactly one '|' (tenant
/// separator, chosen here because '#' is OpenFGA's reserved userset separator — see the warning at
/// the top of MS-AUTHZ-SPEC.md §4 about the previous, invalid DSL). Every type carries the tenant,
/// the subject included: a subject only ever exists inside a tenant (one Keycloak realm per tenant),
/// and scoping the user object is what keeps ListObjects/Read bounded to that tenant's tuples
/// instead of every tenant the same subject id appears in (MS-AUTHZ-SPEC.md §15).
///
///   user:&lt;tenant&gt;|&lt;subject id&gt;
///   role:&lt;tenant&gt;|&lt;role code&gt;
///   permission:&lt;tenant&gt;|&lt;permission code&gt;
/// </summary>
public static class OpenFgaIdentifiers
{
    public const string UserType = "user";
    public const string RoleType = "role";
    public const string PermissionType = "permission";
    public const string AssigneeRelation = "assignee";
    public const string GrantedRelation = "granted";
    public const char TenantSeparator = '|';

    /// <summary>Validates a tenant code without building any object from it.</summary>
    public static void EnsureValidTenantCode(string tenantCode)
        => EnsureValidComponent(tenantCode, nameof(tenantCode));

    /// <summary>
    /// Public entry point for validating a catalog role/permission code against the same rules as
    /// every other identifier component here — used when loading the catalog file so it can't
    /// smuggle in a code that would later produce a malformed OpenFGA object.
    /// </summary>
    public static void EnsureValidCode(string value, string paramName)
        => EnsureValidComponent(value, paramName);

    /// <summary>Builds the "user:&lt;tenant&gt;|&lt;subjectId&gt;" object.</summary>
    public static string User(string tenantCode, string subjectId)
    {
        EnsureValidComponent(tenantCode, nameof(tenantCode));
        EnsureValidComponent(subjectId, nameof(subjectId));
        return $"{UserType}:{tenantCode}{TenantSeparator}{subjectId}";
    }

    /// <summary>Builds the "role:&lt;tenant&gt;|&lt;roleCode&gt;" object.</summary>
    public static string Role(string tenantCode, string roleCode)
    {
        EnsureValidComponent(tenantCode, nameof(tenantCode));
        EnsureValidComponent(roleCode, nameof(roleCode));
        return $"{RoleType}:{tenantCode}{TenantSeparator}{roleCode}";
    }

    /// <summary>
    /// Builds the "role:&lt;tenant&gt;|&lt;roleCode&gt;#assignee" userset reference — the "User" side
    /// of a role→permission tuple (MS-AUTHZ-SPEC.md §4, "Catálogo materializado").
    /// </summary>
    public static string RoleAssigneeUserset(string tenantCode, string roleCode)
        => $"{Role(tenantCode, roleCode)}#{AssigneeRelation}";

    /// <summary>Builds the "permission:&lt;tenant&gt;|&lt;permissionCode&gt;" object.</summary>
    public static string Permission(string tenantCode, string permissionCode)
    {
        EnsureValidComponent(tenantCode, nameof(tenantCode));
        EnsureValidComponent(permissionCode, nameof(permissionCode));
        return $"{PermissionType}:{tenantCode}{TenantSeparator}{permissionCode}";
    }

    /// <summary>
    /// Attempts to strip the "permission:&lt;tenant&gt;|" prefix from an OpenFGA object string,
    /// returning just the permission code when it belongs to <paramref name="tenantCode"/>.
    ///
    /// With the tenant inside the user object, <c>ListObjects</c> can only reach this tenant's
    /// tuples; this filter is the defence in depth for anything malformed that still comes back
    /// (an assignment written across tenants, an object of the wrong shape).
    /// </summary>
    public static bool TryStripTenantPrefix(string permissionObject, string tenantCode, out string permissionCode)
    {
        ArgumentNullException.ThrowIfNull(permissionObject);
        EnsureValidComponent(tenantCode, nameof(tenantCode));

        permissionCode = string.Empty;

        const string typePrefix = $"{PermissionType}:";
        if (!permissionObject.StartsWith(typePrefix, StringComparison.Ordinal))
            return false;

        var withoutType = permissionObject[typePrefix.Length..];
        var tenantPrefix = $"{tenantCode}{TenantSeparator}";
        if (!withoutType.StartsWith(tenantPrefix, StringComparison.Ordinal))
            return false;

        var code = withoutType[tenantPrefix.Length..];
        if (string.IsNullOrEmpty(code))
            return false;

        permissionCode = code;
        return true;
    }

    /// <summary>
    /// Attempts to strip the "role:&lt;tenant&gt;|" prefix from an OpenFGA object string, the same
    /// way <see cref="TryStripTenantPrefix"/> does for permissions. Used when reading a user's role
    /// assignments.
    /// </summary>
    public static bool TryStripTenantRolePrefix(string roleObject, string tenantCode, out string roleCode)
    {
        ArgumentNullException.ThrowIfNull(roleObject);
        EnsureValidComponent(tenantCode, nameof(tenantCode));

        roleCode = string.Empty;

        const string typePrefix = $"{RoleType}:";
        if (!roleObject.StartsWith(typePrefix, StringComparison.Ordinal))
            return false;

        var withoutType = roleObject[typePrefix.Length..];
        var tenantPrefix = $"{tenantCode}{TenantSeparator}";
        if (!withoutType.StartsWith(tenantPrefix, StringComparison.Ordinal))
            return false;

        var code = withoutType[tenantPrefix.Length..];
        if (string.IsNullOrEmpty(code))
            return false;

        roleCode = code;
        return true;
    }

    /// <summary>
    /// Guards against the failure mode documented in MS-AUTHZ-SPEC.md §4: a tenant/role/permission
    /// code that itself contains ':', '#', '|' or whitespace would produce a malformed or ambiguous
    /// OpenFGA object. Fail fast here instead of writing a tuple OpenFGA will reject (or, worse, one
    /// it silently parses differently than intended).
    /// </summary>
    private static void EnsureValidComponent(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier component cannot be null or empty.", paramName);

        if (value.Contains(':'))
            throw new ArgumentException(
                "':' is OpenFGA's reserved type separator and cannot appear inside an identifier component.",
                paramName);

        if (value.Contains('#'))
            throw new ArgumentException(
                "'#' is OpenFGA's reserved userset separator and cannot appear inside an identifier component.",
                paramName);

        if (value.Contains(TenantSeparator))
            throw new ArgumentException(
                $"'{TenantSeparator}' is the tenant separator and cannot appear inside an identifier component.",
                paramName);

        if (value.Any(char.IsWhiteSpace))
            throw new ArgumentException("Identifier component cannot contain whitespace.", paramName);
    }
}
