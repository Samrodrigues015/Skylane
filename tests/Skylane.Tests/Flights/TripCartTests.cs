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

    [Fact]
    public async Task Carrinho_so_com_eventos_paga_e_emite_os_bilhetes()
    {
        var concert = await _client.CreateEventAsync(capacity: 10);
        var fado = await _client.CreateEventAsync(capacity: 10);

        var response = await _client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest(
            "ana@exemplo.pt", null, null, null,
            [new EventSelection(concert.Id, 2), new EventSelection(fado.Id, 1)]), ApiClientExtensions.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cart = (await response.ReadAsync<BookingResponse>())!;
        Assert.Empty(cart.Legs);
        Assert.Equal(75m, cart.Total);

        var paid = (await (await _client.ConfirmBookingAsync(cart.Reference)).ReadAsync<BookingResponse>())!;

        Assert.Equal(BookingStatus.Confirmed, paid.Status);
        Assert.Equal(3, paid.Events.Sum(e => e.Tickets.Count));
    }

    [Fact]
    public async Task Voo_e_eventos_reservados_no_mesmo_pedido()
    {
        var flight = await _client.CreateFlightAsync(economy: 80m);
        var ev = await _client.CreateEventAsync(capacity: 10);

        var response = await _client.PostAsJsonAsync("/api/bookings", Request(flight.Id, "26C", ev.Id, 1), ApiClientExtensions.Json);
        var cart = (await response.ReadAsync<BookingResponse>())!;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(80m + 25m, cart.Total);
        Assert.Single(cart.Events);
    }

    [Fact]
    public async Task Evento_esgotado_no_carrinho_nao_deixa_nada_reservado()
    {
        var flight = await _client.CreateFlightAsync();
        var ev = await _client.CreateEventAsync(capacity: 1);

        var response = await _client.PostAsJsonAsync("/api/bookings", Request(flight.Id, "27C", ev.Id, 2), ApiClientExtensions.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(await _client.IsSeatAvailableAsync(flight.Id, "27C"));
        Assert.Equal(1, (await _client.GetEventAsync(ev.Id)).Available);
    }

    [Fact]
    public async Task Carrinho_vazio_e_recusado()
    {
        var response = await _client.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest("ana@exemplo.pt", null, null, null), ApiClientExtensions.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static CreateBookingRequest Request(Guid flightId, string seat, Guid eventId, int tickets) => new(
        "ana@exemplo.pt",
        [new PassengerRequest("Ana", "Costa", "DOC123")],
        new FlightSelection(flightId, [seat]),
        null,
        [new EventSelection(eventId, tickets)]);

    private Task<HttpResponseMessage> AddEventAsync(string reference, Guid eventId, int quantity) =>
        _client.PostAsJsonAsync($"/api/bookings/{reference}/events", new AddEventRequest(eventId, quantity), ApiClientExtensions.Json);
}
