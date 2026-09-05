namespace Authz.Client;

/// <summary>
/// Supplies the current request's tenant code and subject id to
/// <see cref="PermissionAuthorizationBehavior{TRequest,TResponse}"/>.
///
/// Authz.Client is a generic package (MS-AUTHZ-SPEC.md §8) — it cannot depend on any host system's
/// own request-context type (e.g. estudio-contable-backend's IRequestContext /
/// RequestContext.CurrentUserTenantInfo). Each consuming system implements this interface once,
/// wrapping whatever it already resolves tenant/user identity from, and registers it in DI alongside
/// <c>AddAuthzClient</c>.
///
/// In estudio-contable-backend (MS-AUTHZ-SPEC.md §9) this behavior runs after
/// TenantUserAuthorizationBehavior, so an implementation there can safely read
/// RequestContext.CurrentTenant / CurrentTokenUserInfo — both are guaranteed set by that point.
/// </summary>
public interface IAuthzRequestContextAccessor
{
    /// <summary>
    /// Tenant code for the current request, or null if none is resolved. A null falls back to
    /// <see cref="AuthzClientOptions.DefaultTenantCode"/>, which is how a single-tenant host avoids
    /// resolving a tenant per request; if that is null too, the request is denied.
    /// </summary>
    string? TenantCode { get; }

    /// <summary>Keycloak user id for the current request's subject, or null if none is resolved.</summary>
    string? SubjectId { get; }
}
