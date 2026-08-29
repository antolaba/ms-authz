using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MsAuthz.Infrastructure.Persistence.Entities;

namespace MsAuthz.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => r.Code).IsUnique();
    }
}
