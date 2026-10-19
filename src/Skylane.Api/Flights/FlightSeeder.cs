using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Flights;

/// <summary>
/// Gera voos de demonstração para os próximos dias: rotas reais entre aeroportos reais,
/// com companhias fictícias, dois voos por dia em cada sentido e preços variados.
/// </summary>
public static class FlightSeeder
{
    private const int DaysAhead = 30;

    private sealed record Route(string From, string To, int Minutes, decimal BasePrice, string Airline);

    private static readonly Route[] Routes =
    [
        new("OPO", "LIS", 55, 49m, "AT"),
        new("OPO", "FNC", 105, 89m, "AT"),
        new("OPO", "PDL", 135, 99m, "LW"),
        new("LIS", "FNC", 100, 79m, "LW"),
        new("LIS", "PDL", 130, 89m, "AT"),
        new("LIS", "FAO", 50, 45m, "LW"),
        new("OPO", "MAD", 80, 69m, "AT"),
        new("LIS", "MAD", 75, 65m, "LW"),
        new("OPO", "BCN", 115, 99m, "LW"),
        new("OPO", "CDG", 130, 119m, "AT"),
        new("LIS", "LHR", 160, 129m, "AT"),
        new("LIS", "FCO", 180, 139m, "LW"),
        new("LIS", "AMS", 170, 135m, "AT")
    ];

    public static async Task SeedAsync(SkylaneDbContext db, TimeProvider time, CancellationToken ct = default)
    {
        if (await db.Flights.AnyAsync(ct))
        {
            return;
        }

        var timeZones = await db.Airports.ToDictionaryAsync(a => a.Code, a => a.TimeZoneId, ct);

        // Semente fixa: os "aleatórios" são sempre os mesmos, por isso a demo é reproduzível.
        var random = new Random(42);
        var firstDay = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1);
        var flights = new List<Flight>();

        for (var routeIndex = 0; routeIndex < Routes.Length; routeIndex++)
        {
            var route = Routes[routeIndex];

            // Ida (direction 0) e volta (direction 1).
            for (var direction = 0; direction < 2; direction++)
            {
                var from = direction == 0 ? route.From : route.To;
                var to = direction == 0 ? route.To : route.From;

                // Manhã e fim de tarde, com horas diferentes de rota para rota.
                var offsetMinutes = (routeIndex * 35 + direction * 50) % 180 / 5 * 5;
                TimeOnly[] departures =
                [
                    new TimeOnly(7, 0).AddMinutes(offsetMinutes),
                    new TimeOnly(17, 0).AddMinutes(offsetMinutes)
                ];

                for (var day = 0; day < DaysAhead; day++)
                {
                    var date = firstDay.AddDays(day);

                    for (var slot = 0; slot < departures.Length; slot++)
                    {
                        var departsAt = AirportTime.FromLocal(date.ToDateTime(departures[slot]), timeZones[from]);
                        var economy = EconomyPrice(route.BasePrice, date, random);

                        flights.Add(new Flight
                        {
                            Id = Guid.CreateVersion7(),
                            // O mesmo número todos os dias, como nas companhias reais.
                            Number = $"{route.Airline}{1000 + routeIndex * 10 + direction * 2 + slot}",
                            AirlineCode = route.Airline,
                            OriginCode = from,
                            DestinationCode = to,
                            DepartsAt = departsAt,
                            ArrivesAt = departsAt.AddMinutes(route.Minutes),
                            Status = FlightStatus.Scheduled,
                            Fares =
                            [
                                new FlightFare { Class = CabinClass.Economy, Price = economy },
                                new FlightFare { Class = CabinClass.Business, Price = Math.Round(economy * 2.8m) - 0.01m }
                            ]
                        });
                    }
                }
            }
        }

        db.Flights.AddRange(flights);
        await db.SaveChangesAsync(ct);

        await SeatInventory.CreateMissingSeatsAsync(db, flightId: null, ct);
    }

    /// <summary>Preço base, mais caro ao fim de semana, com variação de ±15%. Acaba sempre em ,99.</summary>
    private static decimal EconomyPrice(decimal basePrice, DateOnly date, Random random)
    {
        var weekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday ? 1.2m : 1m;
        var variation = 0.85m + (decimal)random.NextDouble() * 0.30m;
        return Math.Round(basePrice * weekend * variation) - 0.01m;
    }
}
