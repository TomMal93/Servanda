using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.DTOs;
using Servanda.Domain.Entities;
using Servanda.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Configure SQLite
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var resolvedConnectionString = DbLocationHelper.ResolveConnectionString(rawConnectionString, builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(resolvedConnectionString);
});

// Configure Problem Details
builder.Services.AddProblemDetails();

var app = builder.Build();

// Auto-migrate database in Development and seed initial data
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        await SeedInitialCategoriesAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply migrations or seed initial categories at startup");
    }
}

// Health check endpoint
app.MapGet("/api/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var count = canConnect ? await db.Notes.CountAsync(ct) : 0;

    var response = new HealthCheckDto(
        Status: canConnect ? "healthy" : "unhealthy",
        Database: canConnect ? "connected" : "disconnected",
        NoteCount: count,
        TimestampUtc: DateTime.UtcNow
    );

    return canConnect ? Results.Ok(response) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

// Categories endpoints
app.MapGet("/api/categories", async (AppDbContext db, CancellationToken ct) =>
{
    var categories = await db.Categories
        .AsNoTracking()
        .OrderBy(c => c.SortOrder)
        .Select(c => new CategoryDto(
            c.Id,
            c.Name,
            c.Color,
            c.SortOrder
        ))
        .ToListAsync(ct);

    return Results.Ok(categories);
});

app.MapPut("/api/categories/reorder", async ([FromBody] ReorderCategoriesRequest request, AppDbContext db, CancellationToken ct) =>
{
    if (request.OrderedIds == null || request.OrderedIds.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["OrderedIds"] = ["Lista identyfikatorów kategorii nie może być pusta."]
        });
    }

    var categories = await db.Categories.ToListAsync(ct);
    for (int i = 0; i < request.OrderedIds.Count; i++)
    {
        var id = request.OrderedIds[i];
        var category = categories.FirstOrDefault(c => c.Id == id);
        if (category != null)
        {
            category.SortOrder = i;
        }
    }

    await db.SaveChangesAsync(ct);

    var updated = await db.Categories
        .AsNoTracking()
        .OrderBy(c => c.SortOrder)
        .Select(c => new CategoryDto(
            c.Id,
            c.Name,
            c.Color,
            c.SortOrder
        ))
        .ToListAsync(ct);

    return Results.Ok(updated);
});

// Notes endpoints
app.MapGet("/api/notes", async (AppDbContext db, CancellationToken ct) =>
{
    var notes = await db.Notes
        .AsNoTracking()
        .OrderByDescending(n => n.CreatedAt)
        .Select(n => new NoteDto(
            n.Id,
            n.CategoryId,
            n.Title,
            n.Content,
            n.CreatedAt,
            n.UpdatedAt,
            n.IsPinned,
            n.IsArchived
        ))
        .ToListAsync(ct);

    return Results.Ok(notes);
});

app.MapPost("/api/notes", async ([FromBody] CreateNoteRequest request, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Title"] = ["Tytuł notatki nie może być pusty."]
        });
    }

    var now = DateTime.UtcNow;
    var note = new Note
    {
        Id = Guid.NewGuid(),
        Title = request.Title.Trim(),
        Content = request.Content ?? string.Empty,
        CategoryId = request.CategoryId,
        IsPinned = request.IsPinned,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Notes.Add(note);
    await db.SaveChangesAsync(ct);

    var dto = new NoteDto(
        note.Id,
        note.CategoryId,
        note.Title,
        note.Content,
        note.CreatedAt,
        note.UpdatedAt,
        note.IsPinned,
        note.IsArchived
    );

    return Results.Created($"/api/notes/{note.Id}", dto);
});

static async Task SeedInitialCategoriesAsync(AppDbContext db)
{
    if (!await db.Categories.AnyAsync())
    {
        db.Categories.AddRange(
            new Category { Id = Guid.NewGuid(), Name = "Prompty", Color = "#a855f7", SortOrder = 0 },
            new Category { Id = Guid.NewGuid(), Name = "Notatki", Color = "#38bdf8", SortOrder = 1 },
            new Category { Id = Guid.NewGuid(), Name = "Rodzina", Color = "#f59e0b", SortOrder = 2 },
            new Category { Id = Guid.NewGuid(), Name = "Narzędzia", Color = "#10b981", SortOrder = 3 }
        );
        await db.SaveChangesAsync();
    }
    else
    {
        var existing = await db.Categories.ToListAsync();
        bool changed = false;

        if (!existing.Any(c => c.Name.Equals("Narzędzia", StringComparison.OrdinalIgnoreCase)))
        {
            var maxSort = existing.Count > 0 ? existing.Max(c => c.SortOrder) + 1 : 0;
            db.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = "Narzędzia",
                Color = "#10b981",
                SortOrder = maxSort
            });
            changed = true;
        }

        foreach (var cat in existing)
        {
            if (string.IsNullOrEmpty(cat.Color))
            {
                cat.Color = cat.Name.ToLower() switch
                {
                    "prompty" => "#a855f7",
                    "notatki" => "#38bdf8",
                    "rodzina" => "#f59e0b",
                    "narzędzia" or "narzedzia" or "tools" => "#10b981",
                    _ => "#10b981"
                };
                changed = true;
            }
        }
        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}

app.Run();

// For integration tests
public partial class Program { }
