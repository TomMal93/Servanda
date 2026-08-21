using Servanda.Application.DTOs;
using Xunit;

namespace Servanda.Application.Tests;

public class DtoTests
{
    [Fact]
    public void CreateNoteRequest_ShouldInitializeCorrectly()
    {
        var request = new CreateNoteRequest("Test Title", "Test Content");

        Assert.Equal("Test Title", request.Title);
        Assert.Equal("Test Content", request.Content);
        Assert.Null(request.CategoryId);
        Assert.False(request.IsPinned);
    }

    [Fact]
    public void HealthCheckDto_ShouldContainExpectedProperties()
    {
        var now = DateTime.UtcNow;
        var dto = new HealthCheckDto("healthy", "connected", 5, now);

        Assert.Equal("healthy", dto.Status);
        Assert.Equal("connected", dto.Database);
        Assert.Equal(5, dto.NoteCount);
        Assert.Equal(now, dto.TimestampUtc);
    }
}
