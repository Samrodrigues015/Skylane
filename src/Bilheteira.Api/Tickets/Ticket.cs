using Bilheteira.Api.Reservations;

namespace Bilheteira.Api.Tickets;

public sealed class Ticket
{
    public Guid Id { get; init; }
    public Guid ReservationId { get; init; }
    public Reservation Reservation { get; init; } = null!;

    /// <summary>Código legível que vai no bilhete (ex.: "K7M2Q-9XH4P").</summary>
    public required string Code { get; init; }

    /// <summary>Quando o bilhete foi validado à entrada. Null = ainda não usado.</summary>
    public DateTimeOffset? CheckedInAt { get; set; }
}
