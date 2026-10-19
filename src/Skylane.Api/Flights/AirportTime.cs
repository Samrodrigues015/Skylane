using System.Collections.Concurrent;

namespace Skylane.Api.Flights;

/// <summary>
/// Conversões entre UTC e a hora local de um aeroporto.
///
/// Regra do projeto: na base de dados tudo é UTC. A hora local só existe na entrada
/// (quem cria um voo pensa em "10:00 em Lisboa") e na saída (o cliente quer ver
/// "chega às 12:15, hora de Madrid").
/// </summary>
public static class AirportTime
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new();

    private static TimeZoneInfo Zone(string timeZoneId) =>
        Zones.GetOrAdd(timeZoneId, TimeZoneInfo.FindSystemTimeZoneById);

    /// <summary>Instante UTC → hora local no aeroporto (com o offset certo, ex.: +01:00).</summary>
    public static DateTimeOffset ToLocal(DateTimeOffset instant, string timeZoneId) =>
        TimeZoneInfo.ConvertTime(instant, Zone(timeZoneId));

    /// <summary>Hora "de parede" num aeroporto (ex.: 10:00 em Lisboa) → instante UTC.</summary>
    public static DateTimeOffset FromLocal(DateTime localTime, string timeZoneId)
    {
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone(timeZoneId));
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
