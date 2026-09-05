using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Authz.Client.Exceptions;
using Microsoft.Extensions.Logging;

namespace Authz.Client.Admin;

/// <summary>
/// HTTP implementation of <see cref="IAuthzAdminClient"/> against a system's own ms-authz instance.
/// No caching — see the interface doc comment for why.
/// </summary>
public class AuthzAdminClient(HttpClient httpClient, ILogger<AuthzAdminClient> logger) : IAuthzAdminClient
{
    // JsonSerializerDefaults.Web => camelCase naming policy + case-insensitive matching on read,
    // matching ms-authz's ASP.NET Core controllers (which serialize with the same defaults).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AuthzRoleDto>> GetRoleCatalogAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching role catalog from ms-authz");

        var response = await httpClient.GetAsync("roles", cancellationToken);
        response.EnsureSuccessStatusCode();

        var roles = await response.Content.ReadFromJsonAsync<List<AuthzRoleDto>>(JsonOptions, cancellationToken);
        return roles ?? [];
    }

    public async Task<IReadOnlyList<AuthzAssignedRoleDto>> GetUserRolesAsync(
        string tenantCode, string userId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Fetching role assignments from ms-authz for user {UserId} in tenant {TenantCode}", userId, tenantCode);

        var response = await httpClient.GetAsync(BuildUserRolesUri(tenantCode, userId), cancellationToken);
        response.EnsureSuccessStatusCode();

        var roles = await response.Content.ReadFromJsonAsync<List<AuthzAssignedRoleDto>>(JsonOptions, cancellationToken);
        return roles ?? [];
    }

    public async Task<IReadOnlyList<AuthzAssignedRoleDto>> ReplaceUserRolesAsync(
        string tenantCode, string userId, IReadOnlyList<string> roleCodes, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Replacing role assignments in ms-authz for user {UserId} in tenant {TenantCode}: {RoleCodes}",
            userId, tenantCode, roleCodes);

        var response = await httpClient.PutAsJsonAsync(
            BuildUserRolesUri(tenantCode, userId), new { roleCodes }, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var detail = await TryReadProblemDetailAsync(response, cancellationToken);
            logger.LogWarning(
                "ms-authz rejected role replacement for user {UserId} in tenant {TenantCode}: {Detail}",
                userId, tenantCode, detail);
            throw new AuthzInvalidRoleCodesException(detail ?? "One or more role codes are not in the catalog.");
        }

        response.EnsureSuccessStatusCode();

        var roles = await response.Content.ReadFromJsonAsync<List<AuthzAssignedRoleDto>>(JsonOptions, cancellationToken);
        return roles ?? [];
    }

    public async Task<IReadOnlyList<string>> SyncCatalogAsync(
        IReadOnlyList<string> tenantCodes, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Syncing catalog in ms-authz for tenants {TenantCodes}", tenantCodes);

        var response = await httpClient.PostAsJsonAsync(
            "catalog/sync", new { tenantCodes }, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var detail = await TryReadProblemDetailAsync(response, cancellationToken);
            logger.LogWarning("ms-authz rejected catalog sync for tenants {TenantCodes}: {Detail}", tenantCodes, detail);
            throw new AuthzInvalidRequestException(detail ?? "'tenantCodes' is required.");
        }

        response.EnsureSuccessStatusCode();

        var syncedTenants = await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions, cancellationToken);
        return syncedTenants ?? [];
    }

    private static string BuildUserRolesUri(string tenantCode, string userId)
        => $"users/{Uri.EscapeDataString(userId)}/roles?tenant={Uri.EscapeDataString(tenantCode)}";

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailPayload>(JsonOptions, cancellationToken);
            return problem?.Detail;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Minimal slice of RFC 7807 — only the field this client actually uses.
    private sealed record ProblemDetailPayload(string? Detail);
}
