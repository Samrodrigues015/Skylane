using System.Net;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests.Flights;

[Collection(ApiCollection.Name)]
public sealed class SeatConcurrencyTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Quarenta_pessoas_a_escolher_o_mesmo_lugar_so_uma_consegue()
    {
        var flight = await _client.CreateFlightAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 40).Select(_ => _client.BookAsync(flight.Id, ["10C"])));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(39, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Reservas_que_pedem_os_mesmos_lugares_por_ordens_diferentes_nao_bloqueiam()
    {
        // Metade pede [16A, 16B] e a outra metade [16B, 16A]. Sem ordenar os lugares antes
        // de os prender, isto provocava deadlocks no SQL Server (erros 500).
        var flight = await _client.CreateFlightAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            _client.BookAsync(flight.Id, i % 2 == 0 ? new[] { "16A", "16B" } : new[] { "16B", "16A" })));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(29, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
