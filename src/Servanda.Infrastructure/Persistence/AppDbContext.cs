using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Entities;

namespace Servanda.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Content).IsRequired();
            entity.Property(n => n.CreatedAt).IsRequired();
            entity.Property(n => n.UpdatedAt).IsRequired();

            entity.HasOne(n => n.Category)
                  .WithMany(c => c.Notes)
                  .HasForeignKey(n => n.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Color).HasMaxLength(30);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<NoteTag>(entity =>
        {
            entity.HasKey(nt => new { nt.NoteId, nt.TagId });

            entity.HasOne(nt => nt.Note)
                  .WithMany(n => n.NoteTags)
                  .HasForeignKey(nt => nt.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(nt => nt.Tag)
                  .WithMany(t => t.NoteTags)
                  .HasForeignKey(nt => nt.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
