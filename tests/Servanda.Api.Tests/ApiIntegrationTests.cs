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

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
