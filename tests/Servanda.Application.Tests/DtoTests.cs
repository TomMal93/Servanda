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

    [Fact]
    public void NoteDto_And_ReorderNotesRequest_ShouldInitializeCorrectly()
    {
        var noteId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var noteDto = new NoteDto(noteId, catId, "Title", "Content", now, now, 2, false, false);

        Assert.Equal(2, noteDto.SortOrder);
        Assert.Equal("Title", noteDto.Title);

        var reorderReq = new ReorderNotesRequest(catId, new List<Guid> { noteId });
        Assert.Equal(catId, reorderReq.TargetCategoryId);
        Assert.Single(reorderReq.OrderedNoteIds);

        var moveReq = new MoveNoteRequest(catId, 3);
        Assert.Equal(catId, moveReq.TargetCategoryId);
        Assert.Equal(3, moveReq.NewSortOrder);
    }
}
