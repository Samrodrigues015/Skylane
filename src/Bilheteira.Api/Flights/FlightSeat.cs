using Bilheteira.Api.Bookings;

namespace Bilheteira.Api.Flights;

public enum SeatStatus
{
    Available,

    /// <summary>Preso a uma reserva pendente, à espera de confirmação.</summary>
    Held,

    Sold
}

/// <summary>
/// Um lugar concreto num voo concreto (ex.: o 12A do voo AT1947 de dia 20).
/// Há uma linha por lugar e por voo: é o "inventário" que se vende.
///
/// Chave primária composta (FlightId, SeatNumber). Como é clustered, os lugares de um voo
/// ficam fisicamente juntos no disco, e "dá-me os lugares do voo X" lê só essa zona.
/// </summary>
public sealed class FlightSeat
{
    public Guid FlightId { get; init; }

    /// <summary>Ex.: "12A".</summary>
    public required string SeatNumber { get; init; }

    public int Row { get; init; }
    public required string Letter { get; init; }
    public CabinClass Class { get; init; }

    public SeatStatus Status { get; set; }

    /// <summary>Reserva que tem o lugar (null quando está livre).</summary>
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Passageiro que vai sentado neste lugar (null quando está livre).</summary>
    public Guid? PassengerId { get; set; }
    public Passenger? Passenger { get; set; }
}
