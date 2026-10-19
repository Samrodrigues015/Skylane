using System.Net;
using System.Net.Http.Json;
using Skylane.Api.Bookings;
using Skylane.Api.Flights;
using Skylane.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Skylane.Tests.Flights;

[Collection(ApiCollection.Name)]
public sealed class BookingFlowTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reserva_so_de_ida_prende_os_lugares_e_calcula_o_total()
    {
        var flight = await _client.CreateFlightAsync(economy: 80m, business: 220m);

        var booking = await _client.BookOkAsync(flight.Id, ["12A", "12B"]);

        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Matches("^[A-Z2-9]{6}$", booking.Reference);
        Assert.Equal(160m, booking.Total);

        var leg = Assert.Single(booking.Legs);
        Assert.Equal(LegDirection.Outbound, leg.Direction);
        Assert.Equal(new[] { "12A", "12B" }, leg.Seats.Select(s => s.SeatNumber).ToArray());

        Assert.False(await _client.IsSeatAvailableAsync(flight.Id, "12A"));
        Assert.False(await _client.IsSeatAvailableAsync(flight.Id, "12B"));
        Assert.True(await _client.IsSeatAvailableAsync(flight.Id, "12C"));
    }

    [Fact]
    public async Task Reserva_de_ida_e_volta_com_classes_diferentes()
    {
        var outbound = await _client.CreateFlightAsync("OPO", "FNC", FlightClientExtensions.LisbonTime(30, 9), economy: 80m, business: 220m);
        var inbound = await _client.CreateFlightAsync("FNC", "OPO", FlightClientExtensions.LisbonTime(37, 18), economy: 90m, business: 250m);

        // Ida em Business (fila 1), regresso em Economy.
        var booking = await _client.BookOkAsync(outbound.Id, ["1A"], inbound.Id, ["20C"]);

        Assert.Equal(220m + 90m, booking.Total);
        Assert.Equal(2, booking.Legs.Count);
        Assert.Equal(CabinClass.Business, booking.Legs.Single(l => l.Direction == LegDirection.Outbound).Seats.Single().Class);
        Assert.Equal(CabinClass.Economy, booking.Legs.Single(l => l.Direction == LegDirection.Return).Seats.Single().Class);
    }

    [Fact]
    public async Task Voo_de_regresso_com_percurso_errado_devolve_400()
    {
        var outbound = await _client.CreateFlightAsync("OPO", "FNC", FlightClientExtensions.LisbonTime(30, 9));
        var wrongReturn = await _client.CreateFlightAsync("FNC", "LIS", FlightClientExtensions.LisbonTime(35, 9));

        var response = await _client.BookAsync(outbound.Id, ["10A"], wrongReturn.Id, ["10A"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Voo_de_regresso_antes_da_ida_devolve_400()
    {
        var outbound = await _client.CreateFlightAsync("OPO", "FNC", FlightClientExtensions.LisbonTime(30, 9));
        var earlierReturn = await _client.CreateFlightAsync("FNC", "OPO", FlightClientExtensions.LisbonTime(29, 9));

        var response = await _client.BookAsync(outbound.Id, ["10A"], earlierReturn.Id, ["10A"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Numero_de_lugares_diferente_do_numero_de_passageiros_devolve_400()
    {
        var flight = await _client.CreateFlightAsync();

        var response = await _client.BookAsync(flight.Id, ["10A"], passengerCount: 2);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("99Z")]   // não existe
    [InlineData("1B")]    // a fila 1 é Business e não tem lugar B
    public async Task Lugar_que_nao_existe_no_aviao_devolve_400(string seat)
    {
        var flight = await _client.CreateFlightAsync();

        var response = await _client.BookAsync(flight.Id, [seat]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Se_um_lugar_estiver_ocupado_nada_fica_reservado()
    {
        var flight = await _client.CreateFlightAsync();
        await _client.BookOkAsync(flight.Id, ["7A"]);

        // Pede 6A (livre) e 7A (ocupado). Tem de falhar tudo: o 6A não pode ficar preso.
        var response = await _client.BookAsync(flight.Id, ["6A", "7A"]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await _client.IsSeatAvailableAsync(flight.Id, "6A"));
    }

    [Fact]
    public async Task Confirmar_vende_os_lugares_e_pode_ser_repetido()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["14D"]);

        var first = await _client.ConfirmBookingAsync(booking.Reference);
        var second = await _client.ConfirmBookingAsync(booking.Reference.ToLowerInvariant());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, (await second.ReadAsync<BookingResponse>())!.Status);
        Assert.False(await _client.IsSeatAvailableAsync(flight.Id, "14D"));
    }

    [Fact]
    public async Task Reserva_fora_de_prazo_nao_confirma_e_liberta_os_lugares()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["18E", "18F"]);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));

        var confirm = await _client.ConfirmBookingAsync(booking.Reference);
        Assert.Equal(HttpStatusCode.Gone, confirm.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingExpirer>().ExpireDueAsync(CancellationToken.None);
        }

        Assert.True(await _client.IsSeatAvailableAsync(flight.Id, "18E"));
        Assert.True(await _client.IsSeatAvailableAsync(flight.Id, "18F"));

        var expired = await _client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{booking.Reference}", ApiClientExtensions.Json);
        Assert.Equal(BookingStatus.Expired, expired!.Status);
        Assert.Empty(expired.Legs.Single().Seats);
    }

    [Fact]
    public async Task Referencia_inexistente_devolve_404()
    {
        var response = await _client.GetAsync("/api/bookings/ZZZZZZ");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
