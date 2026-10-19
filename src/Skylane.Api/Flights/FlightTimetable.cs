namespace Skylane.Api.Flights;

/// <summary>
/// Horário de demonstração: para qualquer par de aeroportos e qualquer dia, diz que voos existem.
///
/// É determinístico: o mesmo dia gera sempre os mesmos voos, às mesmas horas e com os mesmos preços.
/// Por isso tanto o seeder como o <see cref="FlightSchedule"/> (que cria voos a pedido) podem usá-lo
/// sem que a demo mude de um arranque para o outro.
/// </summary>
public static class FlightTimetable
{
    /// <summary>Partidas por dia em cada sentido: manhã e fim de tarde.</summary>
    private static readonly TimeOnly[] Slots = [new(7, 0), new(17, 0)];

    private sealed record MainRoute(string From, string To, int Minutes, decimal BasePrice, string Airline);

    /// <summary>
    /// Rotas principais, com duração e preço "à mão". São as que o seeder carrega logo no arranque
    /// (alimentam o quadro de partidas e as rotas mais baratas da página inicial).
    /// </summary>
    private static readonly MainRoute[] MainRoutes =
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

    /// <summary>Coordenadas dos aeroportos, para estimar duração e preço das outras rotas.</summary>
    private static readonly Dictionary<string, (double Lat, double Lon)> Coordinates = new()
    {
        ["OPO"] = (41.2481, -8.6814),
        ["LIS"] = (38.7742, -9.1342),
        ["FAO"] = (37.0144, -7.9659),
        ["FNC"] = (32.6979, -16.7745),
        ["PDL"] = (37.7412, -25.6979),
        ["MAD"] = (40.4719, -3.5626),
        ["BCN"] = (41.2971, 2.0785),
        ["CDG"] = (49.0097, 2.5479),
        ["LHR"] = (51.4700, -0.4543),
        ["FCO"] = (41.8003, 12.2389),
        ["AMS"] = (52.3105, 4.7683)
    };

    private static readonly string[] Codes = Coordinates.Keys.Order(StringComparer.Ordinal).ToArray();

    /// <summary>Os dois sentidos de cada rota principal.</summary>
    public static IEnumerable<(string From, string To)> MainRoutePairs =>
        MainRoutes.SelectMany(r => new[] { (r.From, r.To), (r.To, r.From) });

    private sealed record RouteInfo(int Minutes, decimal BasePrice, string Airline, int NumberBase, int OffsetMinutes);

    /// <summary>
    /// Voos de <paramref name="from"/> para <paramref name="to"/> no dia <paramref name="date"/>
    /// (dia local da origem). Lista vazia se a rota não for conhecida.
    /// </summary>
    public static List<Flight> ForDay(string from, string to, DateOnly date, string originTimeZone)
    {
        var route = Find(from, to);
        if (route is null)
        {
            return [];
        }

        var flights = new List<Flight>();

        for (var slot = 0; slot < Slots.Length; slot++)
        {
            var departsAt = AirportTime.FromLocal(date.ToDateTime(Slots[slot]).AddMinutes(route.OffsetMinutes), originTimeZone);
            var economy = EconomyPrice(route.BasePrice, date, new Random(StableHash($"{from}{to}{date:yyyyMMdd}{slot}")));

            flights.Add(new Flight
            {
                Id = Guid.CreateVersion7(),
                // O mesmo número todos os dias, como nas companhias reais.
                Number = $"{route.Airline}{route.NumberBase + slot}",
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

        return flights;
    }

    private static RouteInfo? Find(string from, string to)
    {
        for (var i = 0; i < MainRoutes.Length; i++)
        {
            var r = MainRoutes[i];
            var direction = r.From == from && r.To == to ? 0 : r.From == to && r.To == from ? 1 : -1;
            if (direction >= 0)
            {
                // Horas diferentes de rota para rota, para o quadro de partidas não ter tudo à mesma hora.
                return new RouteInfo(r.Minutes, r.BasePrice, r.Airline,
                    NumberBase: 1000 + i * 10 + direction * 2,
                    OffsetMinutes: (i * 35 + direction * 50) % 180 / 5 * 5);
            }
        }

        // Outra combinação qualquer: duração e preço estimados pela distância.
        var fromIndex = Array.IndexOf(Codes, from);
        var toIndex = Array.IndexOf(Codes, to);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            return null;
        }

        var pair = fromIndex * Codes.Length + toIndex;
        var km = DistanceKm(Coordinates[from], Coordinates[to]);

        return new RouteInfo(
            // ~780 km/h em cruzeiro, mais 25 minutos de descolagem e aterragem; arredondado a 5 minutos.
            Minutes: (int)Math.Round((km / 780 * 60 + 25) / 5) * 5,
            BasePrice: Math.Round(35m + (decimal)km * 0.075m),
            Airline: pair % 2 == 0 ? "AT" : "LW",
            NumberBase: 2000 + pair * 2,
            OffsetMinutes: pair * 25 % 180 / 5 * 5);
    }

    /// <summary>Preço base, mais caro ao fim de semana, com variação de ±15%. Acaba sempre em ,99.</summary>
    private static decimal EconomyPrice(decimal basePrice, DateOnly date, Random random)
    {
        var weekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday ? 1.2m : 1m;
        var variation = 0.85m + (decimal)random.NextDouble() * 0.30m;
        return Math.Round(basePrice * weekend * variation) - 0.01m;
    }

    /// <summary>Distância em linha reta (fórmula de Haversine).</summary>
    private static double DistanceKm((double Lat, double Lon) a, (double Lat, double Lon) b)
    {
        const double earthRadiusKm = 6371;
        static double Rad(double degrees) => degrees * Math.PI / 180;

        var dLat = Rad(b.Lat - a.Lat);
        var dLon = Rad(b.Lon - a.Lon);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Rad(a.Lat)) * Math.Cos(Rad(b.Lat)) * Math.Pow(Math.Sin(dLon / 2), 2);

        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(h));
    }

    /// <summary>
    /// Hash estável (FNV-1a). O string.GetHashCode do .NET muda a cada arranque,
    /// por isso não serve para gerar sempre os mesmos preços.
    /// </summary>
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 16777619;
            }

            return (int)hash;
        }
    }
}
