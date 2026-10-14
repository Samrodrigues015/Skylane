namespace Bilheteira.Api.Events;

public sealed class Event
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Venue { get; set; }
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Lotação total do evento.</summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Lugares presos (reservas pendentes) ou vendidos (reservas confirmadas).
    /// Só é alterado com UPDATEs atómicos na base de dados — ver ReservationService.
    /// </summary>
    public int Reserved { get; set; }

    public int Available => Capacity - Reserved;
}
