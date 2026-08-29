namespace MsAuthz.Infrastructure.Persistence.Entities;

/// <summary>Maps to `roles` (Liquibase: liquibase/changelog/001-create-roles-table.sql).</summary>
public class RoleEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = new List<RolePermissionEntity>();
}
