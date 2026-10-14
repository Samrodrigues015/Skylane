using Bilheteira.Api.Events;
using Bilheteira.Api.Tickets;

namespace Bilheteira.Api.Reservations;

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Expired
}

public sealed class Reservation
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Event Event { get; init; } = null!;

    public int Quantity { get; init; }
    public ReservationStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Até quando os lugares ficam presos à espera de confirmação.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public List<Ticket> Tickets { get; init; } = [];
}
