using System.Net;
using System.Net.Http.Json;
using Skylane.Api.Bookings;
using Skylane.Api.Payments;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests.Flights;

[Collection(ApiCollection.Name)]
public sealed class PaymentTests(ApiFactory factory)
{
    private const string ApprovedCard = "4242 4242 4242 4242";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Pagamento_aprovado_confirma_a_reserva_e_vende_os_lugares()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["21A"]);

        var response = await PayAsync(booking.Reference, ApprovedCard);
        var paid = await response.ReadAsync<BookingResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, paid!.Status);
        Assert.False(await _client.IsSeatAvailableAsync(flight.Id, "21A"));
    }

    [Fact]
    public async Task Cartao_recusado_devolve_402_e_a_reserva_continua_pendente()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["22A"]);

        var response = await PayAsync(booking.Reference, "4000 0000 0000 0002");
        var current = await _client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{booking.Reference}", ApiClientExtensions.Json);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(BookingStatus.Pending, current!.Status);
        Assert.False(await _client.IsSeatAvailableAsync(flight.Id, "22A"));
    }

    [Fact]
    public async Task Depois_de_um_cartao_recusado_pode_pagar_com_outro()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["23A"]);

        await PayAsync(booking.Reference, "4000 0000 0000 9995");
        var second = await PayAsync(booking.Reference, ApprovedCard);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Pagar_uma_reserva_ja_paga_nao_falha()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["24A"]);

        await PayAsync(booking.Reference, ApprovedCard);
        var again = await PayAsync(booking.Reference, ApprovedCard);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Theory]
    [InlineData("4242 4242 4242 4241", "12/40")] // falha o algoritmo de Luhn
    [InlineData("4242 4242 4242 4242", "01/20")] // fora de validade
    public async Task Cartao_invalido_devolve_400(string card, string expiry)
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["25A"]);

        var response = await PayAsync(booking.Reference, card, expiry);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reserva_fora_de_prazo_nao_e_cobrada()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["26A"]);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        var response = await PayAsync(booking.Reference, ApprovedCard);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public void Algoritmo_de_Luhn_valida_cartoes_de_teste()
    {
        Assert.True(FakePaymentGateway.IsValidCardNumber("4242424242424242"));
        Assert.True(FakePaymentGateway.IsValidCardNumber("4000000000000002"));
        Assert.False(FakePaymentGateway.IsValidCardNumber("4242424242424241"));
        Assert.False(FakePaymentGateway.IsValidCardNumber("12345"));
    }

    private Task<HttpResponseMessage> PayAsync(string reference, string card, string expiry = "12/40") =>
        _client.PostAsJsonAsync(
            $"/api/bookings/{reference}/pay",
            new PayBookingRequest(card, "Samara Rodrigues", expiry, "123"),
            ApiClientExtensions.Json);
}
