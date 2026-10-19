using System.Net;
using Skylane.Api.Reservations;
using Skylane.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Skylane.Tests;

[Collection(ApiCollection.Name)]
public sealed class ReservationFlowTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reservar_prende_os_lugares()
    {
        var ev = await _client.CreateEventAsync(capacity: 10);

        var reservation = await _client.ReserveOkAsync(ev.Id, quantity: 3);

        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Empty(reservation.Tickets);
        Assert.Equal(7, (await _client.GetEventAsync(ev.Id)).Available);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task Quantidade_invalida_devolve_400(int quantity)
    {
        var ev = await _client.CreateEventAsync(capacity: 100);

        var response = await _client.ReserveAsync(ev.Id, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pedir_mais_lugares_do_que_os_disponiveis_devolve_409()
    {
        var ev = await _client.CreateEventAsync(capacity: 2);

        var response = await _client.ReserveAsync(ev.Id, quantity: 3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, (await _client.GetEventAsync(ev.Id)).Available);
    }

    [Fact]
    public async Task Reservar_num_evento_inexistente_devolve_404()
    {
        var response = await _client.ReserveAsync(Guid.NewGuid(), quantity: 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirmar_emite_um_bilhete_por_lugar()
    {
        var ev = await _client.CreateEventAsync(capacity: 10);
        var reservation = await _client.ReserveOkAsync(ev.Id, quantity: 4);

        var response = await _client.ConfirmAsync(reservation.Id);
        var confirmed = await response.ReadAsync<ReservationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReservationStatus.Confirmed, confirmed!.Status);
        Assert.Equal(4, confirmed.Tickets.Count);
        Assert.Equal(4, confirmed.Tickets.Distinct().Count());
    }

    [Fact]
    public async Task Confirmar_duas_vezes_nao_emite_bilhetes_a_dobrar()
    {
        var ev = await _client.CreateEventAsync(capacity: 10);
        var reservation = await _client.ReserveOkAsync(ev.Id, quantity: 2);

        // Simula o cliente a carregar duas vezes em "Pagar" (ou um retry de rede).
        var responses = await Task.WhenAll(
            _client.ConfirmAsync(reservation.Id),
            _client.ConfirmAsync(reservation.Id));

        var first = await responses[0].ReadAsync<ReservationResponse>();
        var second = await responses[1].ReadAsync<ReservationResponse>();

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(2, first!.Tickets.Count);
        Assert.Equal(first.Tickets, second!.Tickets);
    }

    [Fact]
    public async Task Reserva_fora_de_prazo_nao_pode_ser_confirmada_e_liberta_os_lugares()
    {
        var ev = await _client.CreateEventAsync(capacity: 5);
        var reservation = await _client.ReserveOkAsync(ev.Id, quantity: 5);
        Assert.Equal(0, (await _client.GetEventAsync(ev.Id)).Available);

        factory.Clock.Advance(TimeSpan.FromMinutes(11));

        var confirm = await _client.ConfirmAsync(reservation.Id);
        Assert.Equal(HttpStatusCode.Gone, confirm.StatusCode);

        await RunExpiryAsync();

        Assert.Equal(5, (await _client.GetEventAsync(ev.Id)).Available);
    }

    [Fact]
    public async Task Reserva_confirmada_nao_e_expirada_pelo_job()
    {
        var ev = await _client.CreateEventAsync(capacity: 5);
        var reservation = await _client.ReserveOkAsync(ev.Id, quantity: 2);
        await _client.ConfirmAsync(reservation.Id);

        factory.Clock.Advance(TimeSpan.FromMinutes(11));
        await RunExpiryAsync();

        // Os lugares vendidos continuam vendidos.
        Assert.Equal(3, (await _client.GetEventAsync(ev.Id)).Available);
    }

    private async Task RunExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var expirer = scope.ServiceProvider.GetRequiredService<ReservationExpirer>();
        await expirer.ExpireDueAsync(CancellationToken.None);
    }
}
