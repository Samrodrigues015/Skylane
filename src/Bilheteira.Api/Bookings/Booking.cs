using Bilheteira.Api.Flights;

namespace Bilheteira.Api.Bookings;

public enum BookingStatus
{
    Pending,
    Confirmed,
    Expired
}

/// <summary>
/// Reserva de voo: só ida ou ida e volta, para um ou mais passageiros.
/// Os lugares ficam presos (Held) até <see cref="ExpiresAt"/>; se não for confirmada a tempo, expira.
/// </summary>
public sealed class Booking
{
    public Guid Id { get; init; }

    /// <summary>Código de 6 caracteres que o cliente usa para consultar a reserva (ex.: "K7M2Q9").</summary>
    public required string Reference { get; init; }

    public BookingStatus Status { get; set; }

    public required string ContactEmail { get; init; }

    public Guid OutboundFlightId { get; init; }
    public Flight OutboundFlight { get; init; } = null!;

    /// <summary>Voo de regresso. Null numa reserva só de ida.</summary>
    public Guid? ReturnFlightId { get; init; }
    public Flight? ReturnFlight { get; init; }

    public decimal Total { get; set; }
    public required string Currency { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public List<Passenger> Passengers { get; init; } = [];
}

public sealed class Passenger
{
    public Guid Id { get; init; }
    public Guid BookingId { get; init; }

    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    /// <summary>Número do documento de identificação (cartão de cidadão ou passaporte).</summary>
    public required string DocumentNumber { get; init; }
}
