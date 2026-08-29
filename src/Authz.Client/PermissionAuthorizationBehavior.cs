using Authz.Client.Exceptions;
using Authz.Client.Extensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Authz.Client;

/// <summary>
/// MediatR pipeline behavior enforcing <see cref="RequirePermissionAttribute"/>. Copied in form from
/// EstudioContable.Application/Common/Behaviours/TenantUserAuthorizationBehavior.cs
/// (MS-AUTHZ-SPEC.md §8, §9): a request with no attribute passes straight through; one with the
/// attribute and a missing permission logs a warning and throws.
///
/// Registration order matters in the host's own pipeline: this must run AFTER whatever behavior
/// resolves tenant/user context (TenantUserAuthorizationBehavior in estudio-contable-backend), since
/// <see cref="IAuthzRequestContextAccessor"/> depends on that context already being set.
/// </summary>
public class PermissionAuthorizationBehavior<TRequest, TResponse>(
    IAuthzClient authzClient,
    IAuthzRequestContextAccessor requestContextAccessor,
    ILogger<PermissionAuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);

        if (!requestType.RequiresPermission())
        {
            return await next(cancellationToken);
        }

        var requiredCodes = requestType.GetRequiredPermissionCodes();

        var tenantCode = requestContextAccessor.TenantCode;
        var subjectId = requestContextAccessor.SubjectId;

        if (string.IsNullOrEmpty(tenantCode) || string.IsNullOrEmpty(subjectId))
        {
            logger.LogWarning(
                "Denying {RequestType}: no tenant/subject context available to evaluate required permission(s) {RequiredCodes}",
                requestType.Name, requiredCodes);
            throw new AuthzForbiddenAccessException(
                $"No authenticated tenant/subject context to evaluate required permissions for {requestType.Name}.");
        }

        var effectivePermissions = await authzClient.GetEffectivePermissionsAsync(tenantCode, subjectId, cancellationToken);
        var missingCodes = requiredCodes.Where(code => !effectivePermissions.Contains(code)).ToArray();

        if (missingCodes.Length > 0)
        {
            logger.LogWarning(
                "Denying {RequestType} for subject {SubjectId} in tenant {TenantCode}: missing permission(s) {MissingCodes}",
                requestType.Name, subjectId, tenantCode, missingCodes);
            throw new AuthzForbiddenAccessException(
                $"Subject is missing required permission(s): {string.Join(", ", missingCodes)}.");
        }

        logger.LogDebug(
            "Authorized {RequestType} for subject {SubjectId} in tenant {TenantCode}: required permission(s) {RequiredCodes} present",
            requestType.Name, subjectId, tenantCode, requiredCodes);

        return await next(cancellationToken);
    }
}
