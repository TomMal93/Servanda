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
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
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
    // Auto-seed subcategories if missing
    if (!await db.Categories.AnyAsync(c => c.ParentCategoryId != null, ct))
    {
        await SeedInitialCategoriesAsync(db);
    }

    var categories = await db.Categories
        .AsNoTracking()
        .OrderBy(c => c.SortOrder)
        .Select(c => new CategoryDto(
            c.Id,
            c.Name,
            c.Color,
            c.SortOrder,
            c.ParentCategoryId
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
            c.SortOrder,
            c.ParentCategoryId
        ))
        .ToListAsync(ct);

    return Results.Ok(updated);
});

app.MapPut("/api/categories/{id:guid}", async (Guid id, [FromBody] UpdateCategoryRequest request, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Name"] = ["Nazwa kategorii nie może być pusta."]
        });
    }

    var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
    if (category == null)
    {
        return Results.NotFound();
    }

    category.Name = request.Name.Trim();
    if (request.Color != null)
    {
        category.Color = request.Color;
    }

    await db.SaveChangesAsync(ct);

    var dto = new CategoryDto(
        category.Id,
        category.Name,
        category.Color,
        category.SortOrder,
        category.ParentCategoryId
    );

    return Results.Ok(dto);
});

// Notes endpoints
app.MapGet("/api/notes", async (AppDbContext db, CancellationToken ct) =>
{
    var notes = await db.Notes
        .AsNoTracking()
        .OrderBy(n => n.SortOrder)
        .ThenByDescending(n => n.CreatedAt)
        .Select(n => new NoteDto(
            n.Id,
            n.CategoryId,
            n.Title,
            n.Content,
            n.CreatedAt,
            n.UpdatedAt,
            n.SortOrder,
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
        SortOrder = 0,
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
        note.SortOrder,
        note.IsPinned,
        note.IsArchived
    );

    return Results.Created($"/api/notes/{note.Id}", dto);
});

app.MapPut("/api/notes/reorder", async ([FromBody] ReorderNotesRequest request, AppDbContext db, CancellationToken ct) =>
{
    if (request.OrderedNoteIds == null || request.OrderedNoteIds.Count == 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["OrderedNoteIds"] = ["Lista identyfikatorów notatek nie może być pusta."]
        });
    }

    if (request.TargetCategoryId.HasValue)
    {
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.TargetCategoryId.Value, ct);
        if (!categoryExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["TargetCategoryId"] = [$"Kategoria o ID {request.TargetCategoryId.Value} nie istnieje."]
            });
        }
    }

    var noteIds = request.OrderedNoteIds;
    var notes = await db.Notes.Where(n => noteIds.Contains(n.Id)).ToListAsync(ct);

    var now = DateTime.UtcNow;
    for (int i = 0; i < noteIds.Count; i++)
    {
        var id = noteIds[i];
        var note = notes.FirstOrDefault(n => n.Id == id);
        if (note != null)
        {
            note.CategoryId = request.TargetCategoryId;
            note.SortOrder = i;
            note.UpdatedAt = now;
        }
    }

    await db.SaveChangesAsync(ct);

    var allNotes = await db.Notes
        .AsNoTracking()
        .OrderBy(n => n.SortOrder)
        .ThenByDescending(n => n.CreatedAt)
        .Select(n => new NoteDto(
            n.Id,
            n.CategoryId,
            n.Title,
            n.Content,
            n.CreatedAt,
            n.UpdatedAt,
            n.SortOrder,
            n.IsPinned,
            n.IsArchived
        ))
        .ToListAsync(ct);

    return Results.Ok(allNotes);
});

app.MapPut("/api/notes/{id:guid}/move", async (Guid id, [FromBody] MoveNoteRequest request, AppDbContext db, CancellationToken ct) =>
{
    var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id, ct);
    if (note == null)
    {
        return Results.NotFound(new ProblemDetails
        {
            Title = "Nie znaleziono notatki",
            Detail = $"Notatka o ID {id} nie istnieje."
        });
    }

    if (request.TargetCategoryId.HasValue)
    {
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.TargetCategoryId.Value, ct);
        if (!categoryExists)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["TargetCategoryId"] = [$"Kategoria o ID {request.TargetCategoryId.Value} nie istnieje."]
            });
        }
    }

    note.CategoryId = request.TargetCategoryId;
    if (request.NewSortOrder.HasValue)
    {
        note.SortOrder = request.NewSortOrder.Value;
    }
    else
    {
        var maxSortOrder = await db.Notes
            .Where(n => n.CategoryId == request.TargetCategoryId && n.Id != id)
            .Select(n => (int?)n.SortOrder)
            .MaxAsync(ct) ?? -1;
        note.SortOrder = maxSortOrder + 1;
    }
    note.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync(ct);

    var allNotes = await db.Notes
        .AsNoTracking()
        .OrderBy(n => n.SortOrder)
        .ThenByDescending(n => n.CreatedAt)
        .Select(n => new NoteDto(
            n.Id,
            n.CategoryId,
            n.Title,
            n.Content,
            n.CreatedAt,
            n.UpdatedAt,
            n.SortOrder,
            n.IsPinned,
            n.IsArchived
        ))
        .ToListAsync(ct);

    return Results.Ok(allNotes);
});

static async Task SeedInitialCategoriesAsync(AppDbContext db)
{
    if (!await db.Categories.AnyAsync())
    {
        var promptyId = Guid.NewGuid();
        var notatkiId = Guid.NewGuid();
        var rodzinaId = Guid.NewGuid();
        var narzedziaId = Guid.NewGuid();
        var subKodId = Guid.NewGuid();
        var subPracaId = Guid.NewGuid();
        var subOsobisteId = Guid.NewGuid();

        db.Categories.AddRange(
            new Category { Id = promptyId, Name = "Prompty", Color = "#a855f7", SortOrder = 0 },
            new Category { Id = subKodId, ParentCategoryId = promptyId, Name = "Generowanie kodu", Color = "#ec4899", SortOrder = 0 },
            new Category { Id = notatkiId, Name = "Notatki", Color = "#38bdf8", SortOrder = 1 },
            new Category { Id = subPracaId, ParentCategoryId = notatkiId, Name = "Praca", Color = "#06b6d4", SortOrder = 0 },
            new Category { Id = subOsobisteId, ParentCategoryId = notatkiId, Name = "Osobiste", Color = "#14b8a6", SortOrder = 1 },
            new Category { Id = narzedziaId, Name = "Narzędzia", Color = "#10b981", SortOrder = 2 },
            new Category { Id = rodzinaId, Name = "Rodzina", Color = "#f59e0b", SortOrder = 3 }
        );

        db.Notes.AddRange(
            new Note
            {
                Id = Guid.NewGuid(),
                CategoryId = promptyId,
                Title = "Architektura aplikacji React",
                Content = "Przewodnik po strukturze katalogów, podziale na komponenty i zarządzaniu stanem w nowym projekcie.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Note
            {
                Id = Guid.NewGuid(),
                CategoryId = subKodId,
                Title = "Refaktoryzacja komponentu",
                Content = "Napisz zoptymalizowany hook useMemo i useCallback dla listy elementów z wirtualizacją.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Note
            {
                Id = Guid.NewGuid(),
                CategoryId = subPracaId,
                Title = "Plan wdrożenia v1.0",
                Content = "Sprawdzić integrację z bazą SQLite, migracje EF Core oraz testy Playwright.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Note
            {
                Id = Guid.NewGuid(),
                CategoryId = subOsobisteId,
                Title = "Lista zakupów i książek",
                Content = "Książki do przeczytania w tym kwartale: Clean Code, Designing Data-Intensive Applications.",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
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

        // Add subcategories if none exist in existing database
        var hasSubcategories = existing.Any(c => c.ParentCategoryId != null);
        Category? subKod = null;
        Category? subPraca = null;
        Category? subOsobiste = null;

        if (!hasSubcategories)
        {
            var notatki = existing.FirstOrDefault(c => c.Name.Equals("Notatki", StringComparison.OrdinalIgnoreCase));
            if (notatki != null)
            {
                subPraca = new Category { Id = Guid.NewGuid(), ParentCategoryId = notatki.Id, Name = "Praca", Color = "#06b6d4", SortOrder = 0 };
                subOsobiste = new Category { Id = Guid.NewGuid(), ParentCategoryId = notatki.Id, Name = "Osobiste", Color = "#14b8a6", SortOrder = 1 };
                db.Categories.AddRange(subPraca, subOsobiste);
                changed = true;
            }

            var prompty = existing.FirstOrDefault(c => c.Name.Equals("Prompty", StringComparison.OrdinalIgnoreCase));
            if (prompty != null)
            {
                subKod = new Category { Id = Guid.NewGuid(), ParentCategoryId = prompty.Id, Name = "Generowanie kodu", Color = "#ec4899", SortOrder = 0 };
                db.Categories.Add(subKod);
                changed = true;
            }
        }

        foreach (var cat in existing)
        {
            if (string.IsNullOrEmpty(cat.Color))
            {
                cat.Color = cat.Name.ToLower() switch
                {
                    "prompty" => "#a855f7",
                    "generowanie kodu" => "#ec4899",
                    "analiza danych" => "#8b5cf6",
                    "notatki" => "#38bdf8",
                    "praca" => "#06b6d4",
                    "osobiste" => "#14b8a6",
                    "rodzina" => "#f59e0b",
                    "narzędzia" or "narzedzia" or "tools" => "#10b981",
                    _ => "#6366f1"
                };
                changed = true;
            }
        }

        // Assign unassigned demo notes to categories so each category has notes
        var unassignedNotes = await db.Notes.Where(n => n.CategoryId == null).ToListAsync();
        if (unassignedNotes.Count > 0)
        {
            var notatkiCat = existing.FirstOrDefault(c => c.Name.Equals("Notatki", StringComparison.OrdinalIgnoreCase));
            var promptyCat = existing.FirstOrDefault(c => c.Name.Equals("Prompty", StringComparison.OrdinalIgnoreCase));
            var rodzinaCat = existing.FirstOrDefault(c => c.Name.Equals("Rodzina", StringComparison.OrdinalIgnoreCase));
            var narzedziaCat = existing.FirstOrDefault(c => c.Name.Equals("Narzędzia", StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < unassignedNotes.Count; i++)
            {
                var note = unassignedNotes[i];
                if (i % 4 == 0 && notatkiCat != null) note.CategoryId = subPraca?.Id ?? notatkiCat.Id;
                else if (i % 4 == 1 && promptyCat != null) note.CategoryId = subKod?.Id ?? promptyCat.Id;
                else if (i % 4 == 2 && narzedziaCat != null) note.CategoryId = narzedziaCat.Id;
                else if (rodzinaCat != null) note.CategoryId = rodzinaCat.Id;
            }
            changed = true;
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
