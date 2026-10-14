using System.Net;
using Bilheteira.Tests.Infrastructure;

namespace Bilheteira.Tests;

[Collection(ApiCollection.Name)]
public sealed class ReservationConcurrencyTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Pedidos_em_simultaneo_nunca_vendem_acima_da_lotacao()
    {
        // 200 pessoas tentam comprar 1 bilhete, ao mesmo tempo, para um evento com 50 lugares.
        const int capacity = 50;
        const int buyers = 200;

        var ev = await _client.CreateEventAsync(capacity);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, buyers).Select(_ => _client.ReserveAsync(ev.Id, quantity: 1)));

        // Exatamente 50 conseguem; os outros 150 recebem "esgotado". Nem um a mais, nem um a menos.
        Assert.Equal(capacity, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(buyers - capacity, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var after = await _client.GetEventAsync(ev.Id);
        Assert.Equal(0, after.Available);
    }

    [Fact]
    public async Task Pedidos_simultaneos_com_quantidades_diferentes_respeitam_a_lotacao()
    {
        // Quantidades variadas tornam o problema mais difícil: um pedido de 4 pode falhar
        // enquanto um de 1 ainda cabe. Verificamos que a soma vendida nunca passa a lotação.
        const int capacity = 37;
        var ev = await _client.CreateEventAsync(capacity);

        var quantities = Enumerable.Range(0, 120).Select(i => i % 4 + 1).ToArray();
        var responses = await Task.WhenAll(quantities.Select(q => _client.ReserveAsync(ev.Id, q)));

        var sold = quantities
            .Zip(responses)
            .Where(pair => pair.Second.StatusCode == HttpStatusCode.Created)
            .Sum(pair => pair.First);

        Assert.True(sold <= capacity, $"Foram vendidos {sold} lugares para uma lotação de {capacity}.");

        var after = await _client.GetEventAsync(ev.Id);
        Assert.Equal(capacity - sold, after.Available);
    }
}
