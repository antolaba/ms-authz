namespace MsAuthz.Application.Interfaces;

/// <summary>
/// Tracks which tenant codes ms-authz has provisioned (POST /tenants).
///
/// Not part of the schema MS-AUTHZ-SPEC.md §5 lists (roles / permissions / role_permissions) — that
/// schema has no notion of "tenant" at all, on purpose (the catalog is global to the system). But
/// POST /catalog/sync ("re-expansión idempotente sobre todos los tenants") needs *some* durable list
/// of tenants to iterate, and ms-authz owns no other source of truth for that (tenant management is
/// the consuming system's job — see estudio-contable's `public.tenants`). This is the smallest
/// addition that makes §5's own re-expansion job possible: a table of tenant codes this ms-authz
/// instance has ever provisioned. See ms-authz's CLAUDE.md for the fuller rationale.
/// </summary>
public interface ITenantRegistry
{
    /// <summary>Idempotent: registering an already-known tenant code is a no-op.</summary>
    Task RegisterAsync(string tenantCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAllTenantCodesAsync(CancellationToken cancellationToken = default);
}
