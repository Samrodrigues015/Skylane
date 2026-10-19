namespace Skylane.Api.Flights;

public sealed class Airport
{
    /// <summary>Código IATA de 3 letras (ex.: "OPO"). É a chave primária: é estável e é o que toda a gente usa.</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }

    /// <summary>
    /// Fuso horário IANA (ex.: "Europe/Lisbon", "Atlantic/Madeira").
    /// As horas dos voos são guardadas em UTC e convertidas para a hora local de cada aeroporto.
    /// </summary>
    public required string TimeZoneId { get; init; }
}
