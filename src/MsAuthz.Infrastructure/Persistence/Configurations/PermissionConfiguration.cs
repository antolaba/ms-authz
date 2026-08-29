using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MsAuthz.Infrastructure.Persistence.Entities;

namespace MsAuthz.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<PermissionEntity>
{
    public void Configure(EntityTypeBuilder<PermissionEntity> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(150).IsRequired();
        builder.Property(p => p.Module).HasColumnName("module").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(p => p.Code).IsUnique();
    }
}
