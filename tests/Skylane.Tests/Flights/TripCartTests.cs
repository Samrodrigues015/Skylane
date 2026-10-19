using System.Net;
using System.Net.Http.Json;
using Skylane.Api.Bookings;
using Skylane.Api.Payments;
using Skylane.Api.Reservations;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests.Flights;

/// <summary>Eventos juntos à viagem: presos com os lugares do avião e pagos de uma vez.</summary>
[Collection(ApiCollection.Name)]
public sealed class TripCartTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Evento_junto_a_viagem_entra_no_total_e_e_emitido_ao_pagar()
    {
        var flight = await _client.CreateFlightAsync(economy: 80m);
        var booking = await _client.BookOkAsync(flight.Id, ["22A", "22B"]);
        var ev = await _client.CreateEventAsync(capacity: 10); // 25 € por bilhete

        var withEvent = await AddEventAsync(booking.Reference, ev.Id, 2);

        Assert.Equal(HttpStatusCode.OK, withEvent.StatusCode);
        var cart = (await withEvent.ReadAsync<BookingResponse>())!;
        Assert.Equal(160m + 50m, cart.Total);
        Assert.Equal(8, (await _client.GetEventAsync(ev.Id)).Available);

        // O evento expira com a viagem, não com o prazo normal das reservas de eventos.
        Assert.Single(cart.Events);

        var paid = (await (await _client.ConfirmBookingAsync(booking.Reference)).ReadAsync<BookingResponse>())!;

        Assert.Equal(BookingStatus.Confirmed, paid.Status);
        var tickets = Assert.Single(paid.Events);
        Assert.Equal(ReservationStatus.Confirmed, tickets.Status);
        Assert.Equal(2, tickets.Tickets.Count);
    }

    [Fact]
    public async Task Retirar_evento_devolve_os_lugares_e_baixa_o_total()
    {
        var flight = await _client.CreateFlightAsync(economy: 80m);
        var booking = await _client.BookOkAsync(flight.Id, ["23A"]);
        var ev = await _client.CreateEventAsync(capacity: 10);

        var cart = (await (await AddEventAsync(booking.Reference, ev.Id, 3)).ReadAsync<BookingResponse>())!;
        var reservationId = cart.Events.Single().ReservationId;

        var response = await _client.DeleteAsync($"/api/bookings/{booking.Reference}/events/{reservationId}");
        var after = (await response.ReadAsync<BookingResponse>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(after.Events);
        Assert.Equal(80m, after.Total);
        Assert.Equal(10, (await _client.GetEventAsync(ev.Id)).Available);
    }

    [Fact]
    public async Task Bilhetes_de_uma_viagem_nao_se_pagam_a_parte()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["24A"]);
        var ev = await _client.CreateEventAsync(capacity: 10);
        var cart = (await (await AddEventAsync(booking.Reference, ev.Id, 1)).ReadAsync<BookingResponse>())!;

        var response = await _client.ConfirmAsync(cart.Events.Single().ReservationId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Viagem_ja_paga_nao_aceita_mais_eventos()
    {
        var flight = await _client.CreateFlightAsync();
        var booking = await _client.BookOkAsync(flight.Id, ["25A"]);
        await _client.ConfirmBookingAsync(booking.Reference);
        var ev = await _client.CreateEventAsync(capacity: 10);

        var response = await AddEventAsync(booking.Reference, ev.Id, 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cartao_recusado_nao_emite_bilhetes_do_evento()
    {
        var ev = await _client.CreateEventAsync(capacity: 10);
        var reservation = await _client.ReserveOkAsync(ev.Id, 2);

        var response = await _client.PostAsJsonAsync($"/api/reservations/{reservation.Id}/pay",
            ApiClientExtensions.TestCard with { CardNumber = "4000 0000 0000 0002" }, ApiClientExtensions.Json);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var current = await _client.GetFromJsonAsync<ReservationResponse>($"/api/reservations/{reservation.Id}", ApiClientExtensions.Json);
        Assert.Equal(ReservationStatus.Pending, current!.Status);
        Assert.Empty(current.Tickets);
    }

    private Task<HttpResponseMessage> AddEventAsync(string reference, Guid eventId, int quantity) =>
        _client.PostAsJsonAsync($"/api/bookings/{reference}/events", new AddEventRequest(eventId, quantity), ApiClientExtensions.Json);
}
