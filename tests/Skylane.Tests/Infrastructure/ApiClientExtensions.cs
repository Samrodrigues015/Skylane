using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skylane.Api.Events;
using Skylane.Api.Payments;
using Skylane.Api.Reservations;

namespace Skylane.Tests.Infrastructure;

public static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<EventResponse> CreateEventAsync(this HttpClient client, int capacity)
    {
        var request = new CreateEventRequest(
            $"Evento de teste {Guid.NewGuid():N}",
            "Sala de testes",
            DateTimeOffset.UtcNow.AddDays(7),
            capacity,
            "OPO",
            25m);

        var response = await client.PostAsJsonAsync("/api/events", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    public static Task<HttpResponseMessage> ReserveAsync(this HttpClient client, Guid eventId, int quantity) =>
        client.PostAsJsonAsync($"/api/events/{eventId}/reservations", new CreateReservationRequest(quantity), Json);

    public static async Task<ReservationResponse> ReserveOkAsync(this HttpClient client, Guid eventId, int quantity)
    {
        var response = await client.ReserveAsync(eventId, quantity);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!;
    }

    /// <summary>Paga os bilhetes com o cartão de teste que é sempre aprovado.</summary>
    public static Task<HttpResponseMessage> ConfirmAsync(this HttpClient client, Guid reservationId) =>
        client.PostAsJsonAsync($"/api/reservations/{reservationId}/pay", TestCard, Json);

    public static readonly CardPaymentRequest TestCard = new("4242 4242 4242 4242", "Samara Rodrigues", "12/39", "123");

    public static async Task<EventResponse> GetEventAsync(this HttpClient client, Guid eventId) =>
        (await client.GetFromJsonAsync<EventResponse>($"/api/events/{eventId}", Json))!;

    public static Task<T?> ReadAsync<T>(this HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(Json);
}
