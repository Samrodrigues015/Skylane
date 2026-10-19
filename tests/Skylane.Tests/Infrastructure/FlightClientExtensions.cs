using System.Net.Http.Json;
using Skylane.Api.Bookings;
using Skylane.Api.Flights;

namespace Skylane.Tests.Infrastructure;

public static class FlightClientExtensions
{
    private static int _flightCounter;

    /// <summary>Hora de partida "dia N a partir de hoje, às HH:MM em Lisboa".</summary>
    public static DateTimeOffset LisbonTime(int daysFromToday, int hour, int minute = 0)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromToday);
        return AirportTime.FromLocal(date.ToDateTime(new TimeOnly(hour, minute)), "Europe/Lisbon");
    }

    public static async Task<FlightSummary> CreateFlightAsync(
        this HttpClient client,
        string from = "OPO",
        string to = "FNC",
        DateTimeOffset? departsAt = null,
        int durationMinutes = 105,
        decimal economy = 80m,
        decimal business = 220m)
    {
        var number = $"AT{1000 + Interlocked.Increment(ref _flightCounter) % 9000}";

        var request = new CreateFlightRequest(
            number, "AT", from, to,
            departsAt ?? LisbonTime(10, 9),
            durationMinutes, economy, business);

        var response = await client.PostAsJsonAsync("/api/flights", request, ApiClientExtensions.Json);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<FlightSummary>())!;
    }

    public static async Task<SeatMapResponse> GetSeatMapAsync(this HttpClient client, Guid flightId) =>
        (await client.GetFromJsonAsync<SeatMapResponse>($"/api/flights/{flightId}/seats", ApiClientExtensions.Json))!;

    public static async Task<bool> IsSeatAvailableAsync(this HttpClient client, Guid flightId, string seat) =>
        (await client.GetSeatMapAsync(flightId)).Seats.Single(s => s.SeatNumber == seat).Available;

    /// <summary>Reserva com um passageiro por lugar (o número de passageiros vem do voo de ida).</summary>
    public static Task<HttpResponseMessage> BookAsync(
        this HttpClient client,
        Guid outboundFlightId,
        string[] outboundSeats,
        Guid? returnFlightId = null,
        string[]? returnSeats = null,
        int? passengerCount = null)
    {
        var passengers = Enumerable.Range(1, passengerCount ?? outboundSeats.Length)
            .Select(i => new PassengerRequest("Passageiro", $"Teste{i}", $"DOC{i:000}"))
            .ToList();

        var request = new CreateBookingRequest(
            "ana@exemplo.pt",
            passengers,
            new FlightSelection(outboundFlightId, outboundSeats.ToList()),
            returnFlightId is null ? null : new FlightSelection(returnFlightId, (returnSeats ?? Array.Empty<string>()).ToList()));

        return client.PostAsJsonAsync("/api/bookings", request, ApiClientExtensions.Json);
    }

    public static async Task<BookingResponse> BookOkAsync(
        this HttpClient client,
        Guid outboundFlightId,
        string[] outboundSeats,
        Guid? returnFlightId = null,
        string[]? returnSeats = null)
    {
        var response = await client.BookAsync(outboundFlightId, outboundSeats, returnFlightId, returnSeats);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<BookingResponse>())!;
    }

    /// <summary>Paga a reserva com o cartão de teste que é sempre aprovado.</summary>
    public static Task<HttpResponseMessage> ConfirmBookingAsync(this HttpClient client, string reference) =>
        client.PostAsJsonAsync($"/api/bookings/{reference}/pay", ApiClientExtensions.TestCard, ApiClientExtensions.Json);
}
