using System.ComponentModel.DataAnnotations;

namespace Skylane.Api.Events;

public sealed record CreateEventRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, StringLength(120, MinimumLength = 2)] string Venue,
    [Required] DateTimeOffset? StartsAt,
    [Range(1, 100_000)] int Capacity);

public sealed record EventResponse(
    Guid Id,
    string Name,
    string Venue,
    DateTimeOffset StartsAt,
    int Capacity,
    int Available);
