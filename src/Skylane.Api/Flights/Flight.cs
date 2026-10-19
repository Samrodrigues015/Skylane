namespace Skylane.Api.Flights;

public enum FlightStatus
{
    Scheduled,
    Cancelled
}

public enum CabinClass
{
    Economy,
    Business
}

public sealed class Flight
{
    public Guid Id { get; init; }

    /// <summary>Número do voo (ex.: "AT1947").</summary>
    public required string Number { get; init; }

    public required string AirlineCode { get; init; }
    public Airline Airline { get; init; } = null!;

    // Duas chaves estrangeiras para a mesma tabela (Airports): origem e destino.
    public required string OriginCode { get; init; }
    public Airport Origin { get; init; } = null!;

    public required string DestinationCode { get; init; }
    public Airport Destination { get; init; } = null!;

    /// <summary>Partida, sempre em UTC. A hora local calcula-se com o fuso do aeroporto de origem.</summary>
    public DateTimeOffset DepartsAt { get; init; }

    /// <summary>Chegada, sempre em UTC. A hora local calcula-se com o fuso do aeroporto de destino.</summary>
    public DateTimeOffset ArrivesAt { get; init; }

    public FlightStatus Status { get; set; }

    public List<FlightFare> Fares { get; init; } = [];
    public List<FlightSeat> Seats { get; init; } = [];
}

/// <summary>Preço de uma classe num voo. Chave primária composta: (FlightId, Class).</summary>
public sealed class FlightFare
{
    public Guid FlightId { get; init; }
    public CabinClass Class { get; init; }
    public decimal Price { get; init; }
}
