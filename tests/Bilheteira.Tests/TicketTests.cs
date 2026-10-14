using System.Net;
using System.Text.RegularExpressions;
using Bilheteira.Api.Reservations;
using Bilheteira.Api.Tickets;
using Bilheteira.Tests.Infrastructure;

namespace Bilheteira.Tests;

[Collection(ApiCollection.Name)]
public sealed partial class TicketTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Bilhete_so_entra_uma_vez()
    {
        var code = await BuyOneTicketAsync();

        var first = await _client.PostAsync($"/api/tickets/{code}/check-in", content: null);
        var second = await _client.PostAsync($"/api/tickets/{code}/check-in", content: null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Check_in_aceita_o_codigo_em_minusculas()
    {
        var code = await BuyOneTicketAsync();

        var response = await _client.PostAsync($"/api/tickets/{code.ToLowerInvariant()}/check-in", content: null);
        var ticket = await response.ReadAsync<TicketResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(ticket!.CheckedInAt);
    }

    [Fact]
    public async Task Codigo_inexistente_devolve_404()
    {
        var response = await _client.PostAsync("/api/tickets/AAAAA-BBBBB/check-in", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Codigos_gerados_tem_o_formato_esperado_e_nao_se_repetem()
    {
        var codes = Enumerable.Range(0, 10_000).Select(_ => TicketCode.Generate()).ToList();

        Assert.All(codes, c => Assert.Matches(CodeFormat(), c));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    private async Task<string> BuyOneTicketAsync()
    {
        var ev = await _client.CreateEventAsync(capacity: 10);
        var reservation = await _client.ReserveOkAsync(ev.Id, quantity: 1);
        var confirmed = await (await _client.ConfirmAsync(reservation.Id)).ReadAsync<ReservationResponse>();
        return confirmed!.Tickets.Single();
    }

    [GeneratedRegex("^[A-HJKMNP-Z2-9]{5}-[A-HJKMNP-Z2-9]{5}$")]
    private static partial Regex CodeFormat();
}
