using Skylane.Api.Events;
using Skylane.Api.Tickets;

namespace Skylane.Api.Reservations;

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Expired,

    /// <summary>Retirada do carrinho antes de pagar; os lugares voltam ao evento.</summary>
    Cancelled
}

public sealed class Reservation
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Event Event { get; init; } = null!;

    public int Quantity { get; init; }

    /// <summary>Preço de cada bilhete no momento da reserva (o do evento pode mudar depois).</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>
    /// Se o evento foi juntado a uma viagem, a reserva de voos a que pertence:
    /// paga-se tudo de uma vez e expira ao mesmo tempo que os lugares do avião.
    /// </summary>
    public Guid? BookingId { get; init; }
    public ReservationStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Até quando os lugares ficam presos à espera de confirmação.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public List<Ticket> Tickets { get; init; } = [];
}
