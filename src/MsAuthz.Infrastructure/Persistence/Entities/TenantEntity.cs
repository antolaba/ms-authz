namespace MsAuthz.Infrastructure.Persistence.Entities;

/// <summary>
/// Maps to `tenants` (Liquibase: liquibase/changelog/004-create-tenants-table.sql).
/// Not part of MS-AUTHZ-SPEC.md §5's schema — see the rationale on
/// <see cref="Application.Interfaces.ITenantRegistry"/>. Just a code and a timestamp: ms-authz does
/// not own tenant metadata, it only needs to remember which tenant codes it has provisioned so that
/// POST /catalog/sync has something to iterate.
/// </summary>
public class TenantEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
