namespace MsAuthz.Infrastructure.Persistence.Entities;

/// <summary>Maps to `role_permissions` (Liquibase: liquibase/changelog/003-create-role-permissions-table.sql).</summary>
public class RolePermissionEntity
{
    public int RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public PermissionEntity Permission { get; set; } = null!;
}
