namespace MsAuthz.Application.Common.Identifiers;

/// <summary>
/// Single place that builds and parses every OpenFGA object identifier used by ms-authz. Nothing
/// outside this class should concatenate "user:" or "role:" strings by hand.
///
/// Object shape: exactly one ':' (type separator, reserved by OpenFGA) and exactly one '|' (tenant
/// separator; '#' is OpenFGA's reserved userset separator and cannot be used). Both types carry the
/// tenant, the subject included: a subject only ever exists inside a tenant (one Keycloak realm per
/// tenant), and scoping the user object keeps every read bounded to that tenant's tuples.
///
///   user:&lt;tenant&gt;|&lt;subject id&gt;
///   role:&lt;tenant&gt;|&lt;role code&gt;
/// </summary>
public static class OpenFgaIdentifiers
{
    public const string UserType = "user";
    public const string RoleType = "role";
    public const string AssigneeRelation = "assignee";
    public const char TenantSeparator = '|';

    public static void EnsureValidTenantCode(string tenantCode)
        => EnsureValidCode(tenantCode, nameof(tenantCode));

    public static string User(string tenantCode, string subjectId)
    {
        EnsureValidCode(tenantCode, nameof(tenantCode));
        EnsureValidCode(subjectId, nameof(subjectId));
        return $"{UserType}:{tenantCode}{TenantSeparator}{subjectId}";
    }

    public static string Role(string tenantCode, string roleCode)
    {
        EnsureValidCode(tenantCode, nameof(tenantCode));
        EnsureValidCode(roleCode, nameof(roleCode));
        return $"{RoleType}:{tenantCode}{TenantSeparator}{roleCode}";
    }

    /// <summary>
    /// Attempts to strip the "role:&lt;tenant&gt;|" prefix from an OpenFGA object string, returning
    /// just the role code when it belongs to <paramref name="tenantCode"/>. Anything else — another
    /// tenant, another type, a malformed object — is rejected.
    /// </summary>
    public static bool TryStripTenantRolePrefix(string roleObject, string tenantCode, out string roleCode)
    {
        ArgumentNullException.ThrowIfNull(roleObject);
        EnsureValidCode(tenantCode, nameof(tenantCode));

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
    /// A tenant/role/permission/subject code that itself contains ':', '#', '|' or whitespace would
    /// produce a malformed or ambiguous OpenFGA object. Fail fast instead of writing it.
    /// </summary>
    public static void EnsureValidCode(string value, string paramName)
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
