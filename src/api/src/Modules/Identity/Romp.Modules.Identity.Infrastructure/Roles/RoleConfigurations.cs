using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Identity.Domain.Roles;

namespace Romp.Modules.Identity.Infrastructure.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("ROLE_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.Property(r => r.HasAllPermissions).HasColumnName("ALL_PERM_IND").IsRequired();

        builder.HasData(RoleSeed.Roles.Select(r => (object)new
        {
            r.Id, r.Code, r.Name, Description = (string?)r.Description, SortSeq = r.Id, IsActive = true, HasAllPermissions = r.AllPermissions,
        }).ToArray());
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<PermissionLookup>
{
    public void Configure(EntityTypeBuilder<PermissionLookup> builder)
    {
        builder.ToTable("PERM_LKP");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(p => p.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(p => p.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("ACT_IND").IsRequired();

        builder.HasData(RoleSeed.Permissions.Select(p => (object)new
        {
            p.Id, p.Code, p.Name, Description = (string?)p.Description, SortSeq = p.Id, IsActive = true,
        }).ToArray());
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RoleGrant>
{
    public void Configure(EntityTypeBuilder<RoleGrant> builder)
    {
        builder.ToTable("ROLE_PERM_MAP");
        builder.HasKey(m => new { m.RoleId, m.PermissionId });
        builder.Property(m => m.RoleId).HasColumnName("ROLE_ID");
        builder.Property(m => m.PermissionId).HasColumnName("PERM_ID");
        builder.HasOne(m => m.Role).WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Permission).WithMany().HasForeignKey(m => m.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.PermissionId); // the role side is covered by the primary key

        builder.HasData(RoleSeed.Mappings.Select(m => (object)new { m.RoleId, m.PermissionId }).ToArray());
    }
}
