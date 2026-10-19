using Skylane.Api.Flights;

namespace Skylane.Api.Events;

/// <summary>Tipo de evento. O front-end usa-o para a ilustração e para explicar o que é (ex.: o que é o fado).</summary>
public enum EventCategory
{
    Concert,
    Fado,
    Jazz,
    Classical,
    Opera,
    Flamenco,
    Guitar,
    Electronic,
    Festival,
    LightFestival,
    Comedy,
    Cinema,
    Food,
    Flowers,
    Fireworks
}

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
    public EventCategory Category { get; set; }

    /// <summary>Descrição em português.</summary>
    public string? Description { get; set; }

    /// <summary>Descrição em inglês; se faltar, usa-se a portuguesa.</summary>
    public string? DescriptionEn { get; set; }

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
