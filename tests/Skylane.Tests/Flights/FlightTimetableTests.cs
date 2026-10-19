using Skylane.Api.Flights;

namespace Skylane.Tests.Flights;

/// <summary>O horário de demonstração tem de cobrir todas as rotas e ser sempre igual.</summary>
public sealed class FlightTimetableTests
{
    private static readonly string[] Airports = ["OPO", "LIS", "FAO", "FNC", "PDL", "MAD", "BCN", "CDG", "LHR", "FCO", "AMS"];
    private static readonly DateOnly Day = new(2027, 3, 10);

    [Fact]
    public void Todas_as_combinacoes_de_aeroportos_tem_dois_voos_por_dia()
    {
        foreach (var from in Airports)
        foreach (var to in Airports.Where(a => a != from))
        {
            var flights = FlightTimetable.ForDay(from, to, Day, "Europe/Lisbon");

            Assert.Equal(2, flights.Count);
            Assert.All(flights, f =>
            {
                Assert.True(f.ArrivesAt > f.DepartsAt);
                Assert.True(f.Number.Length <= 8);
                Assert.All(f.Fares, fare => Assert.True(fare.Price > 0));
            });
        }
    }

    [Fact]
    public void O_mesmo_dia_gera_sempre_os_mesmos_voos_e_precos()
    {
        var first = FlightTimetable.ForDay("OPO", "AMS", Day, "Europe/Lisbon");
        var second = FlightTimetable.ForDay("OPO", "AMS", Day, "Europe/Lisbon");

        Assert.Equal(
            first.Select(f => (f.Number, f.DepartsAt, f.Fares[0].Price)),
            second.Select(f => (f.Number, f.DepartsAt, f.Fares[0].Price)));
    }

    [Fact]
    public void Rota_mais_longa_demora_mais_e_custa_mais()
    {
        var shortFlight = FlightTimetable.ForDay("OPO", "MAD", Day, "Europe/Lisbon")[0];
        var longFlight = FlightTimetable.ForDay("PDL", "FCO", Day, "Atlantic/Azores")[0];

        Assert.True(longFlight.ArrivesAt - longFlight.DepartsAt > shortFlight.ArrivesAt - shortFlight.DepartsAt);
    }

    [Fact]
    public void Aeroporto_desconhecido_nao_tem_voos()
    {
        Assert.Empty(FlightTimetable.ForDay("OPO", "XYZ", Day, "Europe/Lisbon"));
    }
}
