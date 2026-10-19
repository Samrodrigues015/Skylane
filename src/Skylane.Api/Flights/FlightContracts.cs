using System.ComponentModel.DataAnnotations;

namespace Skylane.Api.Flights;

public sealed record AirportResponse(string Code, string Name, string City, string Country);

public sealed record AirportInfo(string Code, string City);

public sealed record FareInfo(CabinClass Class, decimal Price, int SeatsLeft);

/// <summary>
/// Um voo, como o cliente o vê. As horas vêm na hora local de cada aeroporto,
/// com o offset incluído (ex.: "2026-11-20T10:00:00+00:00" em Lisboa no inverno).
/// </summary>
public sealed record FlightSummary(
    Guid Id,
    string Number,
    string Airline,
    AirportInfo Origin,
    AirportInfo Destination,
    DateTimeOffset DepartsAt,
    DateTimeOffset ArrivesAt,
    int DurationMinutes,
    IReadOnlyList<FareInfo> Fares);

public sealed record SeatInfo(string SeatNumber, int Row, string Letter, CabinClass Class, bool Available);

public sealed record SeatMapResponse(FlightSummary Flight, IReadOnlyList<SeatInfo> Seats);

/// <summary>Cria um voo (uso de backoffice e dos testes).</summary>
/// <param name="DepartsAt">Instante de partida, com offset (ex.: "2026-11-20T10:00:00+00:00").</param>
public sealed record CreateFlightRequest(
    [Required, StringLength(8, MinimumLength = 3)] string Number,
    [Required, StringLength(2, MinimumLength = 2)] string AirlineCode,
    [Required, StringLength(3, MinimumLength = 3)] string Origin,
    [Required, StringLength(3, MinimumLength = 3)] string Destination,
    [Required] DateTimeOffset? DepartsAt,
    [Range(20, 1200)] int DurationMinutes,
    [Range(0.01, 100_000.0)] decimal EconomyPrice,
    [Range(0.01, 100_000.0)] decimal BusinessPrice);

/// <summary>Preço mais baixo de um dia, para a faixa de datas dos resultados. Null = sem voos com lugares.</summary>
public sealed record CalendarDay(DateOnly Date, decimal? MinPrice);

public enum DepartureStatus
{
    OnTime,
    Boarding,
    LastSeats
}

/// <summary>Uma linha do quadro de partidas de um aeroporto.</summary>
public sealed record DepartureInfo(
    Guid FlightId,
    string Number,
    string Airline,
    AirportInfo Destination,
    DateTimeOffset DepartsAt,
    int SeatsLeft,
    DepartureStatus Status);

/// <summary>Rota com o preço mais baixo disponível em Economy.</summary>
public sealed record RouteDeal(AirportInfo Origin, AirportInfo Destination, decimal MinPrice, int DurationMinutes);
