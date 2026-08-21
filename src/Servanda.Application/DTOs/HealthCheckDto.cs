namespace Servanda.Application.DTOs;

public record HealthCheckDto(
    string Status,
    string Database,
    int NoteCount,
    DateTime TimestampUtc
);
