namespace Authz.Client.Exceptions;

/// <summary>
/// Thrown by <see cref="PermissionAuthorizationBehavior{TRequest,TResponse}"/> when the current
/// subject is missing a permission required by <see cref="RequirePermissionAttribute"/>.
///
/// Deliberately independent of any host system's exception hierarchy (Authz.Client cannot depend on
/// estudio-contable-backend's ForbiddenAccessException — it is a generic package). Per
/// MS-AUTHZ-SPEC.md §9, wiring this into a host's global exception handler as a 403 is the host's job:
/// in estudio-contable-backend that means catching it (or its base type) next to
/// ForbiddenAccessException so both map to the same RFC 7807 403, keeping the frontend error contract
/// unchanged.
/// </summary>
public class AuthzForbiddenAccessException(string message) : Exception(message);
