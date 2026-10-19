using System.Net;
using System.Net.Http.Json;
using Skylane.Api.Flights;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests.Flights;

/// <summary>Endpoints que alimentam a página inicial e a faixa de datas dos resultados.</summary>
[Collection(ApiCollection.Name)]
public sealed class HomePageDataTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Calendario_devolve_o_preco_mais_baixo_de_cada_dia()
    {
        // Rota só usada neste teste: Lisboa → Amesterdão, em três dias seguidos.
        await _client.CreateFlightAsync("LIS", "AMS", FlightClientExtensions.LisbonTime(40, 8), economy: 120m);
        await _client.CreateFlightAsync("LIS", "AMS", FlightClientExtensions.LisbonTime(40, 18), economy: 95m);
        await _client.CreateFlightAsync("LIS", "AMS", FlightClientExtensions.LisbonTime(42, 8), economy: 150m);

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);
        var days = await _client.GetFromJsonAsync<List<CalendarDay>>(
            $"/api/flights/calendar?from=LIS&to=AMS&start={start:yyyy-MM-dd}&days=3", ApiClientExtensions.Json);

        Assert.Equal(3, days!.Count);
        Assert.Equal(95m, days[0].MinPrice);   // o mais barato dos dois voos do dia
        Assert.Null(days[1].MinPrice);         // dia sem voos
        Assert.Equal(150m, days[2].MinPrice);
    }

    [Fact]
    public async Task Quadro_de_partidas_mostra_os_proximos_voos_por_ordem()
    {
        // Barcelona como origem só é usada neste teste.
        var later = await _client.CreateFlightAsync("BCN", "OPO", FlightClientExtensions.LisbonTime(5, 18));
        var sooner = await _client.CreateFlightAsync("BCN", "LIS", FlightClientExtensions.LisbonTime(5, 9));

        var departures = await _client.GetFromJsonAsync<List<DepartureInfo>>(
            "/api/airports/BCN/departures?limit=10", ApiClientExtensions.Json);

        var ids = departures!.Select(d => d.FlightId).ToList();
        Assert.True(ids.IndexOf(sooner.Id) < ids.IndexOf(later.Id));
        Assert.All(departures, d => Assert.Equal(DepartureStatus.OnTime, d.Status));
    }

    [Fact]
    public async Task Quadro_de_partidas_de_aeroporto_inexistente_devolve_404()
    {
        var response = await _client.GetAsync("/api/airports/XYZ/departures");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rotas_mais_baratas_vem_ordenadas_por_preco()
    {
        await _client.CreateFlightAsync("FAO", "LIS", FlightClientExtensions.LisbonTime(12, 9), durationMinutes: 50, economy: 19m);

        var deals = await _client.GetFromJsonAsync<List<RouteDeal>>("/api/flights/deals?limit=4", ApiClientExtensions.Json);

        Assert.NotEmpty(deals!);
        Assert.Equal(deals.OrderBy(d => d.MinPrice).Select(d => d.MinPrice), deals.Select(d => d.MinPrice));

        var cheapest = deals[0];
        Assert.Equal("FAO", cheapest.Origin.Code);
        Assert.Equal(19m, cheapest.MinPrice);
        Assert.Equal(50, cheapest.DurationMinutes);
    }
}
