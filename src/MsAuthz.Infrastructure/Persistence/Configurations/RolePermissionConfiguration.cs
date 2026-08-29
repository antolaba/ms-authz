using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MsAuthz.Infrastructure.Persistence.Entities;

namespace MsAuthz.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<RolePermissionEntity> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        builder.Property(rp => rp.RoleId).HasColumnName("role_id");
        builder.Property(rp => rp.PermissionId).HasColumnName("permission_id");

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
