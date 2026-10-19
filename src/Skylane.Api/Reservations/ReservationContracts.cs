namespace Skylane.Api.Reservations;

public sealed record CreateReservationRequest(int Quantity);

public sealed record ReservationResponse(
    Guid Id,
    Guid EventId,
    string EventName,
    int Quantity,
    ReservationStatus Status,
    DateTimeOffset ExpiresAt,
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
