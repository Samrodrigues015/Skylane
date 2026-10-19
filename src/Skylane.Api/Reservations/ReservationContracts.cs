namespace Skylane.Api.Reservations;

public sealed record CreateReservationRequest(int Quantity);

/// <param name="StartsAt">Na hora local da cidade do evento.</param>
/// <param name="BookingReference">Se os bilhetes fazem parte de uma viagem, a referência dessa reserva de voos.</param>
public sealed record ReservationResponse(
    Guid Id,
    Guid EventId,
    string EventName,
    string Venue,
    string City,
    DateTimeOffset StartsAt,
    int Quantity,
    decimal UnitPrice,
    decimal Total,
    string Currency,
    ReservationStatus Status,
    DateTimeOffset ExpiresAt,
    string? BookingReference,
    IReadOnlyList<string> Tickets);

public sealed class ReservationOptions
{
    public const string Section = "Reservations";

    /// <summary>Minutos durante os quais os lugares ficam presos antes de expirar.</summary>
    public int HoldMinutes { get; set; } = 10;

    public int MaxTicketsPerReservation { get; set; } = 10;

    /// <summary>De quantos em quantos segundos o job procura reservas expiradas.</summary>
    public int ExpiryCheckSeconds { get; set; } = 30;

    public bool ExpiryJobEnabled { get; set; } = true;
}
