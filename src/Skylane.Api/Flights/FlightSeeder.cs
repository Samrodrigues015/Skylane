using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Flights;

/// <summary>
/// Carrega no arranque os voos das rotas principais para os próximos dias, para a página inicial
/// ter logo um quadro de partidas e rotas mais baratas. As outras rotas e datas são criadas
/// a pedido pelo <see cref="FlightSchedule"/>, quando alguém as pesquisa.
/// </summary>
public static class FlightSeeder
{
    private const int DaysAhead = 30;

    public static async Task SeedAsync(SkylaneDbContext db, TimeProvider time, CancellationToken ct = default)
    {
        if (await db.Flights.AnyAsync(ct))
        {
            return;
        }

        var timeZones = await db.Airports.ToDictionaryAsync(a => a.Code, a => a.TimeZoneId, ct);
        var firstDay = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1);

        var flights = FlightTimetable.MainRoutePairs
            .SelectMany(route => Enumerable.Range(0, DaysAhead)
                .SelectMany(day => FlightTimetable.ForDay(route.From, route.To, firstDay.AddDays(day), timeZones[route.From])))
            .ToList();

        db.Flights.AddRange(flights);
        await db.SaveChangesAsync(ct);

        await SeatInventory.CreateMissingSeatsAsync(db, flightId: null, ct);
    }
}
