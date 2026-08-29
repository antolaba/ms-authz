namespace MsAuthz.Infrastructure.Persistence.Entities;

/// <summary>Maps to `permissions` (Liquibase: liquibase/changelog/002-create-permissions-table.sql).</summary>
public class PermissionEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = new List<RolePermissionEntity>();
}
