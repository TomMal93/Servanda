using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Areas;

namespace Servanda.Infrastructure.Data;

public sealed class ServandaDbContext(DbContextOptions<ServandaDbContext> options) : DbContext(options)
{
    public DbSet<Area> Areas => Set<Area>();

    internal DbSet<AppState> AppState => Set<AppState>();

    internal DbSet<OrderingScope> OrderingScopes => Set<OrderingScope>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppState>(entity =>
        {
            entity.ToTable("app_state", table => table.HasCheckConstraint("ck_app_state_singleton", "id = 1"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.ContentEpoch).HasColumnName("content_epoch").HasMaxLength(26).IsRequired();
        });

        modelBuilder.Entity<OrderingScope>(entity =>
        {
            entity.ToTable("ordering_scopes", table => table.HasCheckConstraint("ck_ordering_scopes_revision", "revision > 0"));
            entity.HasKey(item => item.ScopeKey);
            entity.Property(item => item.ScopeKey).HasColumnName("scope_key").HasMaxLength(220);
            entity.Property(item => item.Revision).HasColumnName("revision");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.ToTable("areas", table =>
            {
                table.HasCheckConstraint("ck_areas_availability", "availability IN ('active', 'planned')");
                table.HasCheckConstraint("ck_areas_sort_order", "sort_order >= 0");
                table.HasCheckConstraint("ck_areas_revision", "revision > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            entity.Property(item => item.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
            entity.Property(item => item.IconKey).HasColumnName("icon_key").HasMaxLength(60).IsRequired();
            entity.Property(item => item.AccentKey).HasColumnName("accent_key").HasMaxLength(40).IsRequired();
            entity.Property(item => item.ModuleKey).HasColumnName("module_key").HasMaxLength(60).IsRequired();
            entity.Property(item => item.Availability).HasColumnName("availability").HasMaxLength(16).IsRequired();
            entity.Property(item => item.SortOrder).HasColumnName("sort_order");
            entity.Property(item => item.IsHidden).HasColumnName("is_hidden");
            entity.Property(item => item.ArchivedAt).HasColumnName("archived_at");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            entity.HasIndex(item => item.SortOrder).IsUnique();
            entity.HasIndex(item => item.ModuleKey)
                .IsUnique()
                .HasFilter("availability = 'active' AND archived_at IS NULL");
        });
    }
}
