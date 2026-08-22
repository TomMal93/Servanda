using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.DTOs;
using Servanda.Infrastructure.Persistence;
using Xunit;

namespace Servanda.Api.Tests;

public class ApiIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests()
    {
        // Open an in-memory SQLite connection for isolated testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory SQLite DbContext
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Ensure schema is created for test run
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthCheckDto>();
        Assert.NotNull(content);
        Assert.Equal("healthy", content.Status);
        Assert.Equal("connected", content.Database);
    }

    [Fact]
    public async Task NotesEndpoint_CanCreateAndRetrieveNotes()
    {
        // 1. Create a note
        var newNote = new CreateNoteRequest("Test Integration Note", "Checking SQLite integration");
        var postResponse = await _client.PostAsJsonAsync("/api/notes", newNote);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var createdNote = await postResponse.Content.ReadFromJsonAsync<NoteDto>();
        Assert.NotNull(createdNote);
        Assert.Equal("Test Integration Note", createdNote.Title);

        // 2. Fetch list of notes
        var getResponse = await _client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var notes = await getResponse.Content.ReadFromJsonAsync<List<NoteDto>>();
        Assert.NotNull(notes);
        Assert.Contains(notes, n => n.Title == "Test Integration Note");
    }

    [Fact]
    public async Task CategoriesEndpoint_CanListAndReorderCategories()
    {
        // Seed categories directly into the test database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Categories.AddRange(
                new Servanda.Domain.Entities.Category { Id = Guid.NewGuid(), Name = "Prompty", SortOrder = 0 },
                new Servanda.Domain.Entities.Category { Id = Guid.NewGuid(), Name = "Notatki", SortOrder = 1 },
                new Servanda.Domain.Entities.Category { Id = Guid.NewGuid(), Name = "Rodzina", SortOrder = 2 },
                new Servanda.Domain.Entities.Category { Id = Guid.NewGuid(), Name = "Narzędzia", SortOrder = 3 }
            );
            await db.SaveChangesAsync();
        }

        // 1. Fetch categories
        var getResponse = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var categories = await getResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(categories);
        Assert.True(categories.Count >= 4);

        var rootCategories = categories.Where(c => c.ParentCategoryId == null).ToList();
        Assert.True(rootCategories.Count >= 4);

        // 2. Reorder categories (move last root category to top)
        var lastRoot = rootCategories.Last();
        var reorderedIds = new List<Guid> { lastRoot.Id };
        reorderedIds.AddRange(rootCategories.Where(c => c.Id != lastRoot.Id).Select(c => c.Id));

        var putResponse = await _client.PutAsJsonAsync("/api/categories/reorder", new ReorderCategoriesRequest(reorderedIds));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var reordered = await putResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(reordered);
        var reorderedRoots = reordered.Where(c => c.ParentCategoryId == null).OrderBy(c => c.SortOrder).ToList();
        Assert.Equal(lastRoot.Name, reorderedRoots[0].Name);
    }

    [Fact]
    public async Task CategoriesEndpoint_CanUpdateCategoryName()
    {
        Guid catId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Categories.Add(new Servanda.Domain.Entities.Category
            {
                Id = catId,
                Name = "Stara nazwa",
                SortOrder = 0
            });
            await db.SaveChangesAsync();
        }

        var updateResponse = await _client.PutAsJsonAsync($"/api/categories/{catId}", new UpdateCategoryRequest("Nowa nazwa kategorii", "#38bdf8"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedCategory = await updateResponse.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(updatedCategory);
        Assert.Equal("Nowa nazwa kategorii", updatedCategory.Name);
        Assert.Equal("#38bdf8", updatedCategory.Color);

        // Verify update persisted
        var getResponse = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var list = await getResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(list);
        var found = list.FirstOrDefault(c => c.Id == catId);
        Assert.NotNull(found);
        Assert.Equal("Nowa nazwa kategorii", found.Name);
    }

    [Fact]
    public async Task NotesEndpoint_CanReorderAndMoveNotesBetweenCategories()
    {
        Guid catA = Guid.NewGuid();
        Guid catB = Guid.NewGuid();
        Guid note1Id = Guid.NewGuid();
        Guid note2Id = Guid.NewGuid();
        Guid note3Id = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Categories.AddRange(
                new Servanda.Domain.Entities.Category { Id = catA, Name = "Kategoria A", SortOrder = 0 },
                new Servanda.Domain.Entities.Category { Id = catB, Name = "Kategoria B", SortOrder = 1 }
            );

            db.Notes.AddRange(
                new Servanda.Domain.Entities.Note { Id = note1Id, CategoryId = catA, Title = "Note 1", Content = "C1", SortOrder = 0 },
                new Servanda.Domain.Entities.Note { Id = note2Id, CategoryId = catA, Title = "Note 2", Content = "C2", SortOrder = 1 },
                new Servanda.Domain.Entities.Note { Id = note3Id, CategoryId = catB, Title = "Note 3", Content = "C3", SortOrder = 0 }
            );

            await db.SaveChangesAsync();
        }

        // 1. Reorder within Category A (reverse note1 and note2)
        var reorderResponse = await _client.PutAsJsonAsync("/api/notes/reorder", new ReorderNotesRequest(catA, new List<Guid> { note2Id, note1Id }));
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        var notesAfterReorder = await reorderResponse.Content.ReadFromJsonAsync<List<NoteDto>>();
        Assert.NotNull(notesAfterReorder);
        var catANotes = notesAfterReorder.Where(n => n.CategoryId == catA).OrderBy(n => n.SortOrder).ToList();
        Assert.Equal(note2Id, catANotes[0].Id);
        Assert.Equal(0, catANotes[0].SortOrder);
        Assert.Equal(note1Id, catANotes[1].Id);
        Assert.Equal(1, catANotes[1].SortOrder);

        // 2. Move note1 to Category B via reorder (inserting before note3)
        var moveToBResponse = await _client.PutAsJsonAsync("/api/notes/reorder", new ReorderNotesRequest(catB, new List<Guid> { note1Id, note3Id }));
        Assert.Equal(HttpStatusCode.OK, moveToBResponse.StatusCode);

        var notesAfterMove = await moveToBResponse.Content.ReadFromJsonAsync<List<NoteDto>>();
        Assert.NotNull(notesAfterMove);
        var catBNotes = notesAfterMove.Where(n => n.CategoryId == catB).OrderBy(n => n.SortOrder).ToList();
        Assert.Equal(2, catBNotes.Count);
        Assert.Equal(note1Id, catBNotes[0].Id);
        Assert.Equal(catB, catBNotes[0].CategoryId);
        Assert.Equal(note3Id, catBNotes[1].Id);

        // 3. Move note2 to uncategorized (null category) via move endpoint
        var moveSingleResponse = await _client.PutAsJsonAsync($"/api/notes/{note2Id}/move", new MoveNoteRequest(null, 0));
        Assert.Equal(HttpStatusCode.OK, moveSingleResponse.StatusCode);

        var notesAfterSingleMove = await moveSingleResponse.Content.ReadFromJsonAsync<List<NoteDto>>();
        Assert.NotNull(notesAfterSingleMove);
        var movedNote2 = notesAfterSingleMove.FirstOrDefault(n => n.Id == note2Id);
        Assert.NotNull(movedNote2);
        Assert.Null(movedNote2.CategoryId);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
