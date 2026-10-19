using System.ComponentModel.DataAnnotations;

namespace Skylane.Api.Events;

/// <param name="AirportCode">Aeroporto da cidade do evento (ex.: "OPO").</param>
public sealed record CreateEventRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, StringLength(120, MinimumLength = 2)] string Venue,
    [Required] DateTimeOffset? StartsAt,
    [Range(1, 100_000)] int Capacity,
    [Required, StringLength(3, MinimumLength = 3)] string AirportCode,
    [Range(typeof(decimal), "0.01", "10000")] decimal Price,
    EventCategory Category = EventCategory.Concert,
    [StringLength(600)] string? Description = null,
    [StringLength(600)] string? DescriptionEn = null);

/// <param name="StartsAt">Na hora local da cidade do evento (como nos voos).</param>
/// <param name="Description">No idioma pedido em Accept-Language (pt ou en).</param>
public sealed record EventResponse(
    Guid Id,
    string Name,
    EventCategory Category,
    string? Description,
    string Venue,
    string AirportCode,
    string City,
    DateTimeOffset StartsAt,
    int Capacity,
    int Available,
    decimal Price,
    string Currency);
