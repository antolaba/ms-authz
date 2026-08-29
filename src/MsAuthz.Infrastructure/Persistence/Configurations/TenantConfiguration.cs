using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MsAuthz.Infrastructure.Persistence.Entities;

namespace MsAuthz.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(EntityTypeBuilder<TenantEntity> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
    }
}
