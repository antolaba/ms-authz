using Microsoft.EntityFrameworkCore;
using MsAuthz.Application.Interfaces;
using MsAuthz.Infrastructure.Persistence.Entities;

namespace MsAuthz.Infrastructure.Persistence.Repositories;

public class TenantRegistry(AuthzDbContext context) : ITenantRegistry
{
    public async Task RegisterAsync(string tenantCode, CancellationToken cancellationToken = default)
    {
        var exists = await context.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Code == tenantCode, cancellationToken);

        if (exists)
            return;

        // Race-safe against a concurrent registration of the same tenant: the unique index on
        // `code` (TenantConfiguration) turns a lost race into a constraint violation, not a
        // duplicate row. Registration is meant to be idempotent, so that violation is swallowed.
        context.Tenants.Add(new TenantEntity { Code = tenantCode, CreatedAt = DateTimeOffset.UtcNow });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request may have registered it first — confirm before deciding this was a
            // real failure rather than a lost idempotency race.
            var registeredConcurrently = await context.Tenants
                .AsNoTracking()
                .AnyAsync(t => t.Code == tenantCode, cancellationToken);

            if (!registeredConcurrently)
                throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetAllTenantCodesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);
    }
}
