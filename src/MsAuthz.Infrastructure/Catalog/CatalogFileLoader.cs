using System.Text.Json;
using MsAuthz.Application.Common.Identifiers;
using MsAuthz.Application.Interfaces;

namespace MsAuthz.Infrastructure.Catalog;

/// <summary>Immutable, sorted catalog loaded from disk once at startup — see <see cref="CatalogFileLoader"/>.</summary>
public sealed record CatalogSnapshot(IReadOnlyList<CatalogRole> Roles, IReadOnlyList<CatalogPermission> Permissions);

/// <summary>
/// Loads and validates the catalog JSON file (roles, permissions, role→permission, role→role
/// inclusion) into a <see cref="CatalogSnapshot"/>. Included roles are expanded transitively, so a
/// role's <see cref="CatalogRole.PermissionCodes"/> is its effective set. Every failure — a missing
/// file, malformed JSON, a duplicate or invalid code, a role referencing a permission or role that
/// doesn't exist, an inclusion cycle — surfaces as an <see cref="InvalidOperationException"/> naming
/// the file and the problem. ms-authz must never start with an invalid or missing catalog.
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

        var byCode = new Dictionary<string, CatalogFileRole>(StringComparer.Ordinal);

        foreach (var role in rawRoles)
        {
            ValidateCode(role.Code, "role", path);

            if (!byCode.TryAdd(role.Code, role))
                throw new InvalidOperationException($"Catalog file '{path}': duplicate role code '{role.Code}'.");

            if (string.IsNullOrWhiteSpace(role.Name))
                throw new InvalidOperationException($"Catalog file '{path}': role '{role.Code}' is missing a name.");

            foreach (var included in role.Includes ?? [])
            {
                if (included == role.Code)
                    throw new InvalidOperationException($"Catalog file '{path}': role '{role.Code}' includes itself.");
            }
        }

        foreach (var role in byCode.Values)
        {
            foreach (var included in role.Includes ?? [])
            {
                if (!byCode.ContainsKey(included))
                    throw new InvalidOperationException(
                        $"Catalog file '{path}': role '{role.Code}' includes unknown role '{included}'.");
            }
        }

        var directPermissions = byCode.Values.ToDictionary(
            r => r.Code,
            r => ExpandRolePermissions(r, knownPermissionCodes, allPermissionCodesSorted, path),
            StringComparer.Ordinal);

        return byCode.Values
            .Select(r => new CatalogRole(
                r.Code, r.Name, r.Description,
                ResolveEffectivePermissions(r.Code, byCode, directPermissions, [], path)
                    .OrderBy(c => c, StringComparer.Ordinal)
                    .ToList()))
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static HashSet<string> ResolveEffectivePermissions(
        string roleCode,
        Dictionary<string, CatalogFileRole> byCode,
        Dictionary<string, List<string>> directPermissions,
        Stack<string> chain,
        string path)
    {
        if (chain.Contains(roleCode))
            throw new InvalidOperationException(
                $"Catalog file '{path}': role inclusion cycle {string.Join(" -> ", chain.Reverse().Append(roleCode))}.");

        chain.Push(roleCode);
        var effective = new HashSet<string>(directPermissions[roleCode], StringComparer.Ordinal);

        foreach (var included in byCode[roleCode].Includes ?? [])
            effective.UnionWith(ResolveEffectivePermissions(included, byCode, directPermissions, chain, path));

        chain.Pop();
        return effective;
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
