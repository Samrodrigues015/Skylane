using System.Net;
using System.Net.Http.Json;
using Skylane.Api.Flights;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests.Flights;

[Collection(ApiCollection.Name)]
public sealed class FlightSearchTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Um_voo_novo_tem_todos_os_lugares_livres()
    {
        var flight = await _client.CreateFlightAsync();

        var seatMap = await _client.GetSeatMapAsync(flight.Id);

        // 3 filas de Business com 4 lugares + 27 filas de Economy com 6 lugares.
        Assert.Equal(12 + 162, seatMap.Seats.Count);
        Assert.All(seatMap.Seats, s => Assert.True(s.Available));
        Assert.Equal(162, seatMap.Flight.Fares.Single(f => f.Class == CabinClass.Economy).SeatsLeft);
        Assert.Equal(12, seatMap.Flight.Fares.Single(f => f.Class == CabinClass.Business).SeatsLeft);
    }

    [Fact]
    public async Task Pesquisa_encontra_o_voo_no_dia_certo_e_so_nesse_dia()
    {
        var flight = await _client.CreateFlightAsync("OPO", "PDL", FlightClientExtensions.LisbonTime(15, 7));
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(15);

        var sameDay = await SearchAsync("OPO", "PDL", day);
        var nextDay = await SearchAsync("OPO", "PDL", day.AddDays(1));
        var otherDirection = await SearchAsync("PDL", "OPO", day);

        Assert.Contains(sameDay, f => f.Id == flight.Id);
        Assert.DoesNotContain(nextDay, f => f.Id == flight.Id);
        Assert.DoesNotContain(otherDirection, f => f.Id == flight.Id);
    }

    [Fact]
    public async Task Horas_sao_devolvidas_na_hora_local_de_cada_aeroporto()
    {
        // Lisboa → Madrid: parte às 10:00 em Lisboa, voa 75 minutos.
        // Madrid está sempre uma hora à frente, por isso chega às 12:15 hora de Madrid.
        var flight = await _client.CreateFlightAsync("LIS", "MAD", FlightClientExtensions.LisbonTime(20, 10), durationMinutes: 75);

        Assert.Equal(new TimeSpan(10, 0, 0), flight.DepartsAt.TimeOfDay);
        Assert.Equal(new TimeSpan(12, 15, 0), flight.ArrivesAt.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(1), flight.ArrivesAt.Offset - flight.DepartsAt.Offset);
        Assert.Equal(75, flight.DurationMinutes);
    }

    [Fact]
    public async Task Acores_estao_uma_hora_atras_de_Lisboa()
    {
        // Porto → Ponta Delgada: parte às 07:00, voa 2h15, chega às 08:15 hora dos Açores.
        var flight = await _client.CreateFlightAsync("OPO", "PDL", FlightClientExtensions.LisbonTime(21, 7), durationMinutes: 135);

        Assert.Equal(new TimeSpan(8, 15, 0), flight.ArrivesAt.TimeOfDay);
    }

    [Fact]
    public async Task Pesquisa_com_aeroporto_inexistente_devolve_400()
    {
        var response = await _client.GetAsync($"/api/flights/search?from=OPO&to=XYZ&date={Tomorrow():yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pesquisa_com_origem_igual_ao_destino_devolve_400()
    {
        var response = await _client.GetAsync($"/api/flights/search?from=OPO&to=OPO&date={Tomorrow():yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static DateOnly Tomorrow() => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    private async Task<List<FlightSummary>> SearchAsync(string from, string to, DateOnly date) =>
        (await _client.GetFromJsonAsync<List<FlightSummary>>(
            $"/api/flights/search?from={from}&to={to}&date={date:yyyy-MM-dd}&passengers=1",
            ApiClientExtensions.Json))!;
}
