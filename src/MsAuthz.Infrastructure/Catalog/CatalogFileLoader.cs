using System.Text.Json;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Infrastructure.Catalog;

/// <summary>Immutable, sorted catalog loaded from disk once at startup — see <see cref="CatalogFileLoader"/>.</summary>
public sealed record CatalogSnapshot(IReadOnlyList<CatalogRole> Roles, IReadOnlyList<CatalogPermission> Permissions);

/// <summary>
/// Loads and validates the catalog JSON file (roles, permissions, role→permission — MS-AUTHZ-SPEC.md
/// §5) into a <see cref="CatalogSnapshot"/>. Every failure — a missing file, malformed JSON, a
/// duplicate or invalid code, a role referencing a permission that doesn't exist — surfaces as an
/// <see cref="InvalidOperationException"/> naming the file and the problem. ms-authz must never start
/// with an invalid or missing catalog.
/// </summary>
public static class CatalogFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static CatalogSnapshot Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Catalog file not found: '{path}'.");

        CatalogFileDocument? document;
        try
        {
            var json = File.ReadAllText(path);
            document = JsonSerializer.Deserialize<CatalogFileDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Catalog file '{path}' is not valid JSON: {ex.Message}", ex);
        }

        if (document is null)
            throw new InvalidOperationException($"Catalog file '{path}' is empty or could not be parsed.");

        var permissions = ValidatePermissions(document.Permissions ?? [], path);
        var roles = ValidateRoles(document.Roles ?? [], permissions, path);

        return new CatalogSnapshot(roles, permissions);
    }

    private static IReadOnlyList<CatalogPermission> ValidatePermissions(List<CatalogFilePermission> rawPermissions, string path)
    {
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        var permissions = new List<CatalogPermission>();

        foreach (var permission in rawPermissions)
        {
            ValidateCode(permission.Code, "permission", path);

            if (!seenCodes.Add(permission.Code))
                throw new InvalidOperationException($"Catalog file '{path}': duplicate permission code '{permission.Code}'.");

            if (string.IsNullOrWhiteSpace(permission.Module))
                throw new InvalidOperationException($"Catalog file '{path}': permission '{permission.Code}' is missing a module.");

            permissions.Add(new CatalogPermission(permission.Code, permission.Module, permission.Description));
        }

        return permissions.OrderBy(p => p.Code, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<CatalogRole> ValidateRoles(
        List<CatalogFileRole> rawRoles, IReadOnlyList<CatalogPermission> permissions, string path)
    {
        var knownPermissionCodes = permissions.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var allPermissionCodesSorted = permissions.Select(p => p.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();

        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        var roles = new List<CatalogRole>();

        foreach (var role in rawRoles)
        {
            ValidateCode(role.Code, "role", path);

            if (!seenCodes.Add(role.Code))
                throw new InvalidOperationException($"Catalog file '{path}': duplicate role code '{role.Code}'.");

            if (string.IsNullOrWhiteSpace(role.Name))
                throw new InvalidOperationException($"Catalog file '{path}': role '{role.Code}' is missing a name.");

            var permissionCodes = ExpandRolePermissions(role, knownPermissionCodes, allPermissionCodesSorted, path);
            roles.Add(new CatalogRole(role.Code, role.Name, role.Description, permissionCodes));
        }

        return roles.OrderBy(r => r.Code, StringComparer.Ordinal).ToList();
    }

    private static List<string> ExpandRolePermissions(
        CatalogFileRole role, HashSet<string> knownPermissionCodes, List<string> allPermissionCodesSorted, string path)
    {
        var requestedCodes = role.Permissions ?? [];

        if (requestedCodes.Count == 1 && requestedCodes[0] == "*")
            return allPermissionCodesSorted;

        foreach (var code in requestedCodes)
        {
            if (code == "*")
                throw new InvalidOperationException(
                    $"Catalog file '{path}': role '{role.Code}' mixes '*' with explicit permission codes.");

            if (!knownPermissionCodes.Contains(code))
                throw new InvalidOperationException(
                    $"Catalog file '{path}': role '{role.Code}' references unknown permission code '{code}'.");
        }

        return requestedCodes.OrderBy(c => c, StringComparer.Ordinal).ToList();
    }

    private static void ValidateCode(string code, string kind, string path)
    {
        try
        {
            OpenFgaIdentifiers.EnsureValidCode(code, kind);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Catalog file '{path}': invalid {kind} code '{code}': {ex.Message}", ex);
        }
    }
}
