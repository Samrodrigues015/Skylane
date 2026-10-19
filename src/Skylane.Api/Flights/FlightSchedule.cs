using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Flights;

/// <summary>
/// Garante que há voos para a rota e os dias que alguém pesquisa, criando-os a pedido
/// a partir do <see cref="FlightTimetable"/>.
///
/// Porquê a pedido? Voos para todas as combinações de aeroportos durante um ano seriam
/// ~80 000 voos e 14 milhões de lugares. Assim só existem os voos que alguém procurou.
/// Só está ativo com dados de demonstração (SeedDemoData); nos testes cada teste cria os seus voos.
/// </summary>
public sealed class FlightSchedule(SkylaneDbContext db, TimeProvider time, IConfiguration configuration)
{
    /// <summary>Até quantos dias no futuro se podem pesquisar voos.</summary>
    public const int DaysAhead = 365;

    private bool Enabled => configuration.GetValue<bool>("SeedDemoData");

    /// <param name="firstDay">Primeiro dia, na hora local do aeroporto de origem.</param>
    public async Task EnsureFlightsAsync(string from, string to, DateOnly firstDay, int days, CancellationToken ct)
    {
        if (!Enabled || from == to)
        {
            return;
        }

        var timeZone = await db.Airports
            .Where(a => a.Code == from)
            .Select(a => a.TimeZoneId)
            .FirstOrDefaultAsync(ct);

        if (timeZone is null || !await db.Airports.AnyAsync(a => a.Code == to, ct))
        {
            return;
        }

        var today = DateOnly.FromDateTime(AirportTime.ToLocal(time.GetUtcNow(), timeZone).DateTime);
        var start = firstDay < today ? today : firstDay;
        var end = Min(firstDay.AddDays(days), today.AddDays(DaysAhead + 1));
        if (start >= end)
        {
            return;
        }

        // Verificação rápida, sem bloqueios: no caso normal os voos já existem.
        if ((await MissingDaysAsync(from, to, start, end, timeZone, ct)).Count == 0)
        {
            return;
        }

        // Dois pedidos para a mesma rota ao mesmo tempo não podem criar os voos duas vezes.
        // O sp_getapplock serializa só quem gera esta rota; o resto da aplicação não espera.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var resource = $"flight-schedule:{from}-{to}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 50000, 'Não foi possível obter o lock do horário de voos.', 1;
            """, ct);

        // Pode ter sido outro pedido a criá-los enquanto esperávamos pelo lock: voltamos a ver.
        var missing = await MissingDaysAsync(from, to, start, end, timeZone, ct);
        var flights = missing.SelectMany(day => FlightTimetable.ForDay(from, to, day, timeZone)).ToList();

        if (flights.Count > 0)
        {
            db.Flights.AddRange(flights);
            await db.SaveChangesAsync(ct);

            foreach (var flight in flights)
            {
                await SeatInventory.CreateMissingSeatsAsync(db, flight.Id, ct);
            }
        }

        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>Dias do intervalo [start, end) em que a rota ainda não tem nenhum voo.</summary>
    private async Task<List<DateOnly>> MissingDaysAsync(
        string from, string to, DateOnly start, DateOnly end, string timeZone, CancellationToken ct)
    {
        var rangeStart = AirportTime.FromLocal(start.ToDateTime(TimeOnly.MinValue), timeZone);
        var rangeEnd = AirportTime.FromLocal(end.ToDateTime(TimeOnly.MinValue), timeZone);

        var departures = await db.Flights
            .AsNoTracking()
            .Where(f => f.OriginCode == from && f.DestinationCode == to && f.DepartsAt >= rangeStart && f.DepartsAt < rangeEnd)
            .Select(f => f.DepartsAt)
            .ToListAsync(ct);

        var daysWithFlights = departures
            .Select(d => DateOnly.FromDateTime(AirportTime.ToLocal(d, timeZone).DateTime))
            .ToHashSet();

        var missing = new List<DateOnly>();
        for (var day = start; day < end; day = day.AddDays(1))
        {
            if (!daysWithFlights.Contains(day))
            {
                missing.Add(day);
            }
        }

        return missing;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
