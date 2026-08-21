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
                new Servanda.Domain.Entities.Category { Id = Guid.NewGuid(), Name = "Rodzina", SortOrder = 2 }
            );
            await db.SaveChangesAsync();
        }

        // 1. Fetch categories
        var getResponse = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var categories = await getResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(categories);
        Assert.Equal(3, categories.Count);
        Assert.Equal("Prompty", categories[0].Name);
        Assert.Equal("Notatki", categories[1].Name);
        Assert.Equal("Rodzina", categories[2].Name);

        // 2. Reorder categories (move Rodzina to top)
        var newOrder = new List<Guid> { categories[2].Id, categories[0].Id, categories[1].Id };
        var putResponse = await _client.PutAsJsonAsync("/api/categories/reorder", new ReorderCategoriesRequest(newOrder));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var reordered = await putResponse.Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.NotNull(reordered);
        Assert.Equal("Rodzina", reordered[0].Name);
        Assert.Equal("Prompty", reordered[1].Name);
        Assert.Equal("Notatki", reordered[2].Name);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
