using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authz.Client;

public class AuthzClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<AuthzClientOptions> options,
    ILogger<AuthzClient> logger) : IAuthzClient
{
    private readonly AuthzClientOptions _options = options.Value;

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(
        string tenantCode, string subjectId, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(tenantCode, subjectId);

        if (cache.TryGetValue(cacheKey, out IReadOnlyCollection<string>? cached) && cached is not null)
        {
            logger.LogDebug(
                "Cache hit for effective permissions (tenant {TenantCode}, subject {SubjectId})",
                tenantCode, subjectId);
            return cached;
        }

        logger.LogDebug(
            "Cache miss for effective permissions (tenant {TenantCode}, subject {SubjectId}) — calling ms-authz",
            tenantCode, subjectId);

        var requestUri = $"me/permissions?tenant={Uri.EscapeDataString(tenantCode)}&subject={Uri.EscapeDataString(subjectId)}";
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var permissions = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken)
                           ?? [];

        cache.Set(cacheKey, (IReadOnlyCollection<string>)permissions, _options.CacheTtl);

        return permissions;
    }

    private sealed record CacheKey(string TenantCode, string SubjectId);

    private static CacheKey BuildCacheKey(string tenantCode, string subjectId)
        => new(tenantCode, subjectId);
}
