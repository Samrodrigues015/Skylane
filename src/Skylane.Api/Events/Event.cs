using Skylane.Api.Flights;

namespace Skylane.Api.Events;

public sealed class Event
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Venue { get; set; }

    /// <summary>
    /// Aeroporto que serve a cidade do evento (ex.: "AMS" para Amesterdão). É o que liga os eventos
    /// aos voos: quem pesquisa um voo para Amesterdão vê os eventos de Amesterdão.
    /// </summary>
    public required string AirportCode { get; set; }
    public Airport Airport { get; init; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Lotação total do evento.</summary>
    /// <summary>Preço de cada bilhete, em euros.</summary>
    public decimal Price { get; set; }

    public int Capacity { get; set; }

    /// <summary>
    /// Lugares presos (reservas pendentes) ou vendidos (reservas confirmadas).
    /// Só é alterado com UPDATEs atómicos na base de dados — ver ReservationService.
    /// </summary>
    public int Reserved { get; set; }

    public int Available => Capacity - Reserved;
}
