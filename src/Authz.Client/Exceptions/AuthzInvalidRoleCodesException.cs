namespace Authz.Client.Exceptions;

/// <summary>
/// Thrown by <see cref="Admin.IAuthzAdminClient.ReplaceUserRolesAsync"/> when ms-authz rejects the
/// request because one or more of the given role codes are not in the catalog (the HTTP 400 ms-authz
/// returns for its own <c>InvalidCatalogRequestException</c> — see
/// MsAuthz.Api/Extensions/GlobalExceptionHandler.cs in the ms-authz repo).
///
/// Deliberately independent of any host system's exception hierarchy, same reasoning as
/// <see cref="AuthzForbiddenAccessException"/>: Authz.Client is a generic package and cannot depend on
/// a host's own exception types. Wiring this into a host's global exception handler is the host's job
/// — in estudio-contable-backend it is mapped next to <c>AppException</c>'s business-rule branch
/// (422), since "you asked to assign a role code that does not exist" is a business-rule failure, not
/// a structural one.
/// </summary>
public class AuthzInvalidRoleCodesException(string message) : Exception(message);
