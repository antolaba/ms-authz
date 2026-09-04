namespace MsAuthz.Application.Common.Identifiers;

/// <summary>
/// Single place that builds and parses every OpenFGA object identifier used by ms-authz.
/// Nothing outside this class should concatenate "user:", "role:" or "permission:" strings by hand
/// (MS-AUTHZ-SPEC.md §4).
///
/// Object shape: exactly one ':' (type separator, reserved by OpenFGA) and, for tenant-scoped types,
/// exactly one '|' (tenant separator, chosen here because '#' is OpenFGA's reserved userset
/// separator — see the warning at the top of MS-AUTHZ-SPEC.md §4 about the previous, invalid DSL).
///
///   user:&lt;keycloak user id&gt;
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

    /// <summary>Builds the "user:&lt;id&gt;" object for a Keycloak user id.</summary>
    public static string User(string keycloakUserId)
    {
        EnsureValidComponent(keycloakUserId, nameof(keycloakUserId));
        return $"{UserType}:{keycloakUserId}";
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
    /// This is the filter required by MS-AUTHZ-SPEC.md §4: <c>ListObjects</c> returns permissions
    /// from every tenant the subject has a role in, not just the one being asked about. Every
    /// object that does not match "permission:&lt;tenantCode&gt;|" — including ones for other
    /// tenants — is rejected here.
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
    /// assignments, which are just as cross-tenant-leaky as ListObjects for the same reason.
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
