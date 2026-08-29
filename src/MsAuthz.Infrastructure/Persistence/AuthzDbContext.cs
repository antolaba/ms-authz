using Microsoft.EntityFrameworkCore;
using MsAuthz.Infrastructure.Persistence.Entities;

namespace MsAuthz.Infrastructure.Persistence;

/// <summary>
/// EF Core context over the `authz` database. Schema is owned by Liquibase (ms-authz/liquibase/), not
/// EF migrations — same convention as estudio-contable-backend. EF here is a query/write tool over an
/// already-migrated schema, nothing more.
/// </summary>
public class AuthzDbContext(DbContextOptions<AuthzDbContext> options) : DbContext(options)
{
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthzDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
