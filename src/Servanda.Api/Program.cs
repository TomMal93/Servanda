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

// Auto-migrate database in Development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply migrations at startup");
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

app.Run();

// For integration tests
public partial class Program { }
