using MsAuthz.Application.Dtos;

namespace MsAuthz.Application.Services;

/// <summary>Read-only, human-readable catalog — backs GET /roles (MS-AUTHZ-SPEC.md §1, §5, §7).</summary>
public interface ICatalogService
{
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
}
