namespace Authz.Client.Exceptions;

/// <summary>
/// Thrown by <see cref="Admin.IAuthzAdminClient.SyncCatalogAsync"/> when ms-authz rejects the request with a
/// 400 (an invalid tenant code, or an empty tenant list — see MsAuthz.Api/Extensions/
/// GlobalExceptionHandler.cs in the ms-authz repo). Deliberately independent of any host system's
/// exception hierarchy, same reasoning as <see cref="AuthzInvalidRoleCodesException"/>.
/// </summary>
public class AuthzInvalidRequestException(string message) : Exception(message);
