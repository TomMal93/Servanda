using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Areas;
using Servanda.Domain.Catalog;
using Servanda.Domain.Prompts;
using Servanda.Domain.Tools;

namespace Servanda.Infrastructure.Data;

public sealed class ServandaDbContext(DbContextOptions<ServandaDbContext> options) : DbContext(options)
{
    public DbSet<Area> Areas => Set<Area>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Tool> Tools => Set<Tool>();

    public DbSet<Prompt> Prompts => Set<Prompt>();

    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();

    public DbSet<PromptUsageEntry> PromptUsage => Set<PromptUsageEntry>();

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
            entity.HasIndex(item => new { item.ArchivedAt, item.IsHidden, item.SortOrder });
            entity.HasIndex(item => item.ModuleKey)
                .IsUnique()
                .HasFilter("availability = 'active' AND archived_at IS NULL");
        });

        ConfigureCatalog(modelBuilder);
        ConfigureTools(modelBuilder);
        ConfigurePrompts(modelBuilder);
    }

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories", table =>
            {
                table.HasCheckConstraint("ck_categories_sort_order", "sort_order >= 0");
                table.HasCheckConstraint("ck_categories_revision", "revision > 0");
                table.HasCheckConstraint("ck_categories_parent", "parent_id IS NULL OR parent_id <> id");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.AreaId).HasColumnName("area_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.ParentId).HasColumnName("parent_id").HasMaxLength(26);
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(Category.MaxNameLength).IsRequired();
            entity.Property(item => item.Description)
                .HasColumnName("description")
                .HasMaxLength(Category.MaxDescriptionLength)
                .IsRequired();
            entity.Property(item => item.SortOrder).HasColumnName("sort_order");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            entity.HasAlternateKey(item => new { item.Id, item.AreaId });
            entity.HasOne<Area>()
                .WithMany()
                .HasForeignKey(item => item.AreaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Category>()
                .WithMany()
                .HasForeignKey(item => new { item.ParentId, item.AreaId })
                .HasPrincipalKey(item => new { item.Id, item.AreaId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.AreaId, item.SortOrder })
                .IsUnique()
                .HasFilter("parent_id IS NULL");
            entity.HasIndex(item => new { item.AreaId, item.ParentId, item.SortOrder })
                .IsUnique()
                .HasFilter("parent_id IS NOT NULL");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("tags", table => table.HasCheckConstraint("ck_tags_revision", "revision > 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.AreaId).HasColumnName("area_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(Tag.MaxNameLength).IsRequired();
            entity.Property(item => item.NormalizedName)
                .HasColumnName("normalized_name")
                .HasMaxLength(Tag.MaxNameLength)
                .IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            entity.HasOne<Area>()
                .WithMany()
                .HasForeignKey(item => item.AreaId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.AreaId, item.NormalizedName }).IsUnique();
        });
    }

    private static void ConfigureTools(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tool>(entity =>
        {
            entity.ToTable("tools", table =>
            {
                table.HasCheckConstraint("ck_tools_group_key", "group_key IN ('featured', 'regular')");
                table.HasCheckConstraint("ck_tools_sort_order", "sort_order >= 0");
                table.HasCheckConstraint("ck_tools_revision", "revision > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.AreaId).HasColumnName("area_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.CategoryId).HasColumnName("category_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(Tool.MaxNameLength).IsRequired();
            entity.Property(item => item.Description)
                .HasColumnName("description")
                .HasMaxLength(Tool.MaxDescriptionLength)
                .IsRequired();
            entity.Property(item => item.Url).HasColumnName("url").HasMaxLength(Tool.MaxUrlLength).IsRequired();
            entity.Property(item => item.GroupKey).HasColumnName("group_key").HasMaxLength(16).IsRequired();
            entity.Property(item => item.SortOrder).HasColumnName("sort_order");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            entity.HasOne<Category>()
                .WithMany()
                .HasForeignKey(item => new { item.CategoryId, item.AreaId })
                .HasPrincipalKey(item => new { item.Id, item.AreaId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.CategoryId, item.GroupKey, item.SortOrder }).IsUnique();
            entity.HasMany(item => item.Tags)
                .WithOne()
                .HasForeignKey(item => item.ToolId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Metadata.FindNavigation(nameof(Tool.Tags))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ToolTag>(entity =>
        {
            entity.ToTable("tool_tags");
            entity.HasKey(item => new { item.ToolId, item.TagId });
            entity.Property(item => item.ToolId).HasColumnName("tool_id").HasMaxLength(26);
            entity.Property(item => item.TagId).HasColumnName("tag_id").HasMaxLength(26);
            entity.HasOne<Tag>()
                .WithMany()
                .HasForeignKey(item => item.TagId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.TagId);
        });
    }

    private static void ConfigurePrompts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Prompt>(entity =>
        {
            entity.ToTable("prompts", table =>
            {
                table.HasCheckConstraint("ck_prompts_sort_order", "sort_order >= 0");
                table.HasCheckConstraint("ck_prompts_revision", "revision > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.AreaId).HasColumnName("area_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.CategoryId).HasColumnName("category_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.Title).HasColumnName("title").HasMaxLength(Prompt.MaxTitleLength).IsRequired();
            entity.Property(item => item.Description)
                .HasColumnName("description")
                .HasMaxLength(Prompt.MaxDescriptionLength)
                .IsRequired();
            entity.Property(item => item.IsFavorite).HasColumnName("is_favorite");
            entity.Property(item => item.SortOrder).HasColumnName("sort_order");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            entity.HasOne<Category>()
                .WithMany()
                .HasForeignKey(item => new { item.CategoryId, item.AreaId })
                .HasPrincipalKey(item => new { item.Id, item.AreaId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.CategoryId, item.SortOrder }).IsUnique();
            entity.HasIndex(item => item.IsFavorite);
            entity.HasMany(item => item.Tags)
                .WithOne()
                .HasForeignKey(item => item.PromptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Variants)
                .WithOne()
                .HasForeignKey(item => item.PromptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Variables)
                .WithOne()
                .HasForeignKey(item => item.PromptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Metadata.FindNavigation(nameof(Prompt.Tags))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            entity.Metadata.FindNavigation(nameof(Prompt.Variants))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            entity.Metadata.FindNavigation(nameof(Prompt.Variables))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PromptTag>(entity =>
        {
            entity.ToTable("prompt_tags");
            entity.HasKey(item => new { item.PromptId, item.TagId });
            entity.Property(item => item.PromptId).HasColumnName("prompt_id").HasMaxLength(26);
            entity.Property(item => item.TagId).HasColumnName("tag_id").HasMaxLength(26);
            entity.HasOne<Tag>()
                .WithMany()
                .HasForeignKey(item => item.TagId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.TagId);
        });

        modelBuilder.Entity<PromptVariant>(entity =>
        {
            entity.ToTable("prompt_variants", table =>
                table.HasCheckConstraint("ck_prompt_variants_sort_order", "sort_order >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.PromptId).HasColumnName("prompt_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.Name)
                .HasColumnName("name")
                .HasMaxLength(PromptVariant.MaxNameLength)
                .IsRequired();
            entity.Property(item => item.Target).HasColumnName("target").HasMaxLength(PromptVariant.MaxTargetLength);
            entity.Property(item => item.Content)
                .HasColumnName("content")
                .HasMaxLength(PromptVariant.MaxContentLength)
                .IsRequired();
            entity.Property(item => item.SortOrder).HasColumnName("sort_order");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => new { item.PromptId, item.SortOrder }).IsUnique();
        });

        modelBuilder.Entity<PromptVariable>(entity =>
        {
            entity.ToTable("prompt_variables", table =>
                table.HasCheckConstraint("ck_prompt_variables_sort_order", "sort_order >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.PromptId).HasColumnName("prompt_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.Name)
                .HasColumnName("name")
                .HasMaxLength(PromptVariable.MaxNameLength)
                .IsRequired();
            entity.Property(item => item.Label)
                .HasColumnName("label")
                .HasMaxLength(PromptVariable.MaxLabelLength)
                .IsRequired();
            entity.Property(item => item.DefaultValue)
                .HasColumnName("default_value")
                .HasMaxLength(PromptVariable.MaxDefaultValueLength)
                .IsRequired();
            entity.Property(item => item.IsRequired).HasColumnName("is_required");
            entity.Property(item => item.IsMultiline).HasColumnName("is_multiline");
            entity.Property(item => item.SortOrder).HasColumnName("sort_order");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => new { item.PromptId, item.SortOrder }).IsUnique();
            entity.HasIndex(item => new { item.PromptId, item.Name }).IsUnique();
        });

        modelBuilder.Entity<PromptVersion>(entity =>
        {
            entity.ToTable("prompt_versions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.PromptId).HasColumnName("prompt_id").HasMaxLength(26).IsRequired();
            entity.Property(item => item.SnapshotJson).HasColumnName("snapshot_json").IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.HasOne<Prompt>()
                .WithMany()
                .HasForeignKey(item => item.PromptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.PromptId, item.CreatedAt });
        });

        modelBuilder.Entity<PromptUsageEntry>(entity =>
        {
            entity.ToTable("prompt_usage");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(26).ValueGeneratedNever();
            entity.Property(item => item.PromptId).HasColumnName("prompt_id").HasMaxLength(26);
            entity.Property(item => item.VariantId).HasColumnName("variant_id").HasMaxLength(26);
            entity.Property(item => item.PromptTitle)
                .HasColumnName("prompt_title")
                .HasMaxLength(Prompt.MaxTitleLength)
                .IsRequired();
            entity.Property(item => item.VariantName)
                .HasColumnName("variant_name")
                .HasMaxLength(PromptVariant.MaxNameLength)
                .IsRequired();
            entity.Property(item => item.UsedAt).HasColumnName("used_at");
            entity.HasOne<Prompt>()
                .WithMany()
                .HasForeignKey(item => item.PromptId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<PromptVariant>()
                .WithMany()
                .HasForeignKey(item => item.VariantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => item.UsedAt);
            entity.HasIndex(item => item.PromptId);
        });
    }
}
