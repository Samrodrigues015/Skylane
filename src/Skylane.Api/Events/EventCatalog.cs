using Skylane.Api.Data;
using Skylane.Api.Flights;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Events;

/// <summary>
/// Eventos de demonstração em todas as cidades com aeroporto, que se repetem ao longo do ano.
/// Nomes e salas são fictícios.
/// </summary>
public static class EventCatalog
{
    /// <param name="FirstDay">Dias depois de <see cref="Anchor"/> em que acontece pela primeira vez.</param>
    /// <param name="RepeatDays">De quantos em quantos dias se repete.</param>
    private sealed record Sample(
        string Airport, string Name, string Venue, int FirstDay, int Hour, int Capacity, decimal Price, int RepeatDays);

    /// <summary>
    /// Data fixa a partir da qual os eventos se repetem. Por ser fixa, o calendário é sempre o mesmo,
    /// seja qual for o dia em que a aplicação arranca.
    /// </summary>
    private static readonly DateOnly Anchor = new(2026, 1, 5);

    /// <summary>Até quantos dias à frente há sempre eventos.</summary>
    public const int DaysAhead = 120;

    private static readonly Sample[] Samples =
    [
        new("OPO", "Noite de Fado no Cais", "Armazém do Rio, Porto", 9, 21, 120, 22m, 21),
        new("OPO", "Festival de Jazz da Ribeira", "Jardim do Morro, Gaia", 23, 18, 800, 35m, 28),
        new("OPO", "Stand-up: Sessão Esgotável", "Sala Pequena, Porto", 4, 22, 8, 12m, 7),
        new("OPO", "Concerto de Gala", "Auditório Central, Porto", 80, 17, 450, 45m, 35),

        new("LIS", "Fado na Mouraria", "Casa da Guitarra, Lisboa", 6, 21, 90, 25m, 21),
        new("LIS", "Festival de Luz do Tejo", "Ribeira das Naus, Lisboa", 19, 20, 2000, 15m, 28),
        new("LIS", "Concerto ao Pôr do Sol", "Miradouro da Graça, Lisboa", 41, 18, 300, 18m, 14),
        new("LIS", "Lisboa Electrónica", "Armazém 7, Lisboa", 67, 23, 1200, 30m, 35),

        new("FAO", "Noites de Música na Marina", "Marina de Faro", 12, 21, 400, 15m, 21),
        new("FAO", "Festival de Marisco do Algarve", "Largo da Sé, Faro", 34, 13, 1500, 20m, 28),
        new("FAO", "Guitarra ao Luar", "Cidade Velha, Faro", 58, 21, 150, 12m, 14),

        new("FNC", "Mostra de Flores do Atlântico", "Praça do Município, Funchal", 15, 11, 3000, 8m, 21),
        new("FNC", "Noite de Música na Zona Velha", "Rua de Santa Maria, Funchal", 29, 21, 250, 15m, 28),
        new("FNC", "Fogo de Artifício na Baía", "Marina do Funchal", 74, 23, 5000, 10m, 14),

        new("PDL", "Concerto nas Portas da Cidade", "Portas da Cidade, Ponta Delgada", 17, 21, 800, 18m, 21),
        new("PDL", "Festival de Música dos Açores", "Auditório do Mar, Ponta Delgada", 46, 20, 600, 28m, 28),

        new("MAD", "Noche de Flamenco", "Tablao del Centro, Madrid", 8, 22, 120, 35m, 21),
        new("MAD", "Jazz en el Parque", "Parque del Retiro, Madrid", 27, 19, 900, 25m, 28),
        new("MAD", "Madrid Indie Fest", "Nave 4, Madrid", 53, 20, 1500, 30m, 14),

        new("BCN", "Concert a la Platja", "Platja de la Barceloneta, Barcelona", 11, 21, 2500, 20m, 21),
        new("BCN", "Nit de Guitarra", "Sala Gòtica, Barcelona", 32, 21, 140, 28m, 28),
        new("BCN", "Festival de Música Mediterrània", "Port Vell, Barcelona", 61, 19, 1800, 32m, 14),

        new("CDG", "Soirée Jazz à Montmartre", "Cave Saint-Pierre, Paris", 7, 21, 110, 30m, 21),
        new("CDG", "Concert sur la Seine", "Quai de la Tournelle, Paris", 25, 20, 700, 45m, 28),
        new("CDG", "Nuit Électro", "Hangar 12, Paris", 49, 23, 1500, 35m, 14),

        new("LHR", "Late Night Comedy", "Camden Basement, London", 5, 21, 90, 18m, 21),
        new("LHR", "Thames Riverside Concert", "South Bank Lawn, London", 22, 19, 1200, 40m, 28),
        new("LHR", "Winter Lights Festival", "Canal Side, London", 71, 18, 3000, 25m, 14),

        new("FCO", "Notte d'Opera", "Teatro dei Colli, Roma", 13, 20, 500, 55m, 21),
        new("FCO", "Jazz a Trastevere", "Piazza del Vicolo, Roma", 36, 21, 300, 25m, 28),
        new("FCO", "Cinema sotto le Stelle", "Giardino Aurelio, Roma", 64, 21, 400, 12m, 14),

        new("AMS", "Canal Lights Concert", "Prinsengracht, Amsterdam", 10, 20, 600, 35m, 21),
        new("AMS", "Jazz in de Jordaan", "Huiskamer Jordaan, Amsterdam", 30, 21, 80, 22m, 28),
        new("AMS", "Amsterdam Dance Night", "Westerpark Hal 3, Amsterdam", 55, 23, 2000, 38m, 14)
    ];

    /// <summary>
    /// Garante que cada evento tem as suas sessões dos próximos <see cref="DaysAhead"/> dias.
    /// Corre no arranque e uma vez por dia (<see cref="Data.DemoDataRefresher"/>), por isso a demo
    /// tem sempre eventos à venda, seja qual for o dia em que alguém a abre.
    /// </summary>
    public static async Task EnsureUpcomingAsync(SkylaneDbContext db, TimeProvider time, CancellationToken ct = default)
    {
        var timeZones = await db.Airports.ToDictionaryAsync(a => a.Code, a => a.TimeZoneId, ct);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var first = today.AddDays(1);
        var last = today.AddDays(DaysAhead);

        var wanted = Samples
            .SelectMany(s => Occurrences(s, first, last).Select(day => new
            {
                Sample = s,
                // A hora é a da cidade do evento (21:00 em Roma), guardada em UTC.
                StartsAt = AirportTime.FromLocal(day.ToDateTime(new TimeOnly(s.Hour, 0)), timeZones[s.Airport])
            }))
            .ToList();

        var from = AirportTime.FromLocal(first.ToDateTime(TimeOnly.MinValue), "UTC").AddDays(-1);
        var existing = (await db.Events
                .Where(e => e.StartsAt >= from)
                .Select(e => new { e.Name, e.StartsAt })
                .ToListAsync(ct))
            .Select(e => (e.Name, e.StartsAt.UtcDateTime))
            .ToHashSet();

        var missing = wanted
            .Where(w => !existing.Contains((w.Sample.Name, w.StartsAt.UtcDateTime)))
            .Select(w => new Event
            {
                Id = Guid.CreateVersion7(),
                Name = w.Sample.Name,
                Venue = w.Sample.Venue,
                AirportCode = w.Sample.Airport,
                StartsAt = w.StartsAt,
                Capacity = w.Sample.Capacity,
                Price = w.Sample.Price
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.Events.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Dias entre <paramref name="first"/> e <paramref name="last"/> em que o evento acontece.</summary>
    private static IEnumerable<DateOnly> Occurrences(Sample sample, DateOnly first, DateOnly last)
    {
        var day = Anchor.AddDays(sample.FirstDay);
        if (day < first)
        {
            var periods = (first.DayNumber - day.DayNumber + sample.RepeatDays - 1) / sample.RepeatDays;
            day = day.AddDays(periods * sample.RepeatDays);
        }

        for (; day <= last; day = day.AddDays(sample.RepeatDays))
        {
            yield return day;
        }
    }
}
