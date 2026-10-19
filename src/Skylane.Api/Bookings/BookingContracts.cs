using System.ComponentModel.DataAnnotations;
using Skylane.Api.Flights;
using Skylane.Api.Reservations;

namespace Skylane.Api.Bookings;

public sealed record PassengerRequest(
    [Required, StringLength(60, MinimumLength = 1)] string FirstName,
    [Required, StringLength(60, MinimumLength = 1)] string LastName,
    [Required, StringLength(20, MinimumLength = 3)] string DocumentNumber);

/// <summary>Um voo e os lugares escolhidos nele, pela mesma ordem dos passageiros.</summary>
public sealed record FlightSelection(
    [Required] Guid? FlightId,
    [Required] List<string> Seats);

/// <summary>
/// Pedido de reserva. O primeiro lugar de cada voo é do primeiro passageiro, o segundo do segundo, etc.
/// </summary>
/// <example>
/// {
///   "contactEmail": "ana@exemplo.pt",
///   "passengers": [ { "firstName": "Ana", "lastName": "Silva", "documentNumber": "12345678" } ],
///   "outbound": { "flightId": "...", "seats": [ "12A" ] },
///   "return":   { "flightId": "...", "seats": [ "14C" ] }
/// }
/// </example>
public sealed record CreateBookingRequest(
    [Required, EmailAddress, StringLength(254)] string ContactEmail,
    [Required] List<PassengerRequest> Passengers,
    [Required] FlightSelection Outbound,
    FlightSelection? Return);

public enum LegDirection
{
    Outbound,
    Return
}

public sealed record BookedSeat(string Passenger, string SeatNumber, CabinClass Class, decimal Price);

public sealed record BookingLeg(
    LegDirection Direction,
    Guid FlightId,
    string FlightNumber,
    AirportInfo Origin,
    AirportInfo Destination,
    DateTimeOffset DepartsAt,
    DateTimeOffset ArrivesAt,
    IReadOnlyList<BookedSeat> Seats);

/// <summary>Bilhetes de um evento juntos à viagem.</summary>
/// <param name="StartsAt">Na hora local da cidade do evento.</param>
public sealed record BookedEvent(
    Guid ReservationId,
    Guid EventId,
    string Name,
    string Venue,
    string City,
    DateTimeOffset StartsAt,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    ReservationStatus Status,
    IReadOnlyList<string> Tickets);

public sealed record AddEventRequest([Required] Guid? EventId, [Range(1, 10)] int Quantity);

/// <param name="Total">Voos mais os eventos juntos à viagem.</param>
public sealed record BookingResponse(
    string Reference,
    BookingStatus Status,
    string ContactEmail,
    decimal Total,
    string Currency,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> Passengers,
    IReadOnlyList<BookingLeg> Legs,
    IReadOnlyList<BookedEvent> Events);

public sealed class BookingOptions
{
    public const string Section = "FlightBookings";

    /// <summary>Minutos durante os quais os lugares ficam presos à espera de confirmação.</summary>
    public int HoldMinutes { get; set; } = 15;

    public int MaxPassengers { get; set; } = 9;
}
