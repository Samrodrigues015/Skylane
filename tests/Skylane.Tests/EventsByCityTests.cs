using System.Net;
using System.Net.Http.Json;
using Skylane.Api.Events;
using Skylane.Api.Flights;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests;

/// <summary>Eventos filtrados pela cidade de destino e pelas datas da viagem.</summary>
[Collection(ApiCollection.Name)]
public sealed class EventsByCityTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Devolve_so_os_eventos_da_cidade_durante_a_viagem()
    {
        // Roma, daqui a 300 dias: só este teste usa estas datas.
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(300);

        var during = await CreateAsync("FCO", day.AddDays(1), 21);
        var before = await CreateAsync("FCO", day.AddDays(-1), 21);
        var otherCity = await CreateAsync("AMS", day.AddDays(1), 21);

        var events = await _client.GetFromJsonAsync<List<EventResponse>>(
            $"/api/events?city=FCO&from={day:yyyy-MM-dd}&to={day.AddDays(3):yyyy-MM-dd}", ApiClientExtensions.Json);

        var ids = events!.Select(e => e.Id).ToList();
        Assert.Contains(during.Id, ids);
        Assert.DoesNotContain(before.Id, ids);
        Assert.DoesNotContain(otherCity.Id, ids);
    }

    [Fact]
    public async Task Hora_do_evento_vem_na_hora_local_da_cidade()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(310);
        var ev = await CreateAsync("FCO", day, 21);

        Assert.Equal("Roma", ev.City);
        Assert.Equal(21, ev.StartsAt.Hour);
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome").GetUtcOffset(ev.StartsAt.UtcDateTime), ev.StartsAt.Offset);
    }

    [Fact]
    public async Task Evento_num_aeroporto_inexistente_e_recusado()
    {
        var request = new CreateEventRequest("Evento", "Sala", DateTimeOffset.UtcNow.AddDays(5), 10, "XYZ", 20m);

        var response = await _client.PostAsJsonAsync("/api/events", request, ApiClientExtensions.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<EventResponse> CreateAsync(string airport, DateOnly day, int hour)
    {
        var timeZone = airport == "FCO" ? "Europe/Rome" : "Europe/Amsterdam";
        var startsAt = AirportTime.FromLocal(day.ToDateTime(new TimeOnly(hour, 0)), timeZone);

        var response = await _client.PostAsJsonAsync("/api/events",
            new CreateEventRequest($"Evento {Guid.NewGuid():N}", "Sala", startsAt, 50, airport, 20m), ApiClientExtensions.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>(ApiClientExtensions.Json))!;
    }
}
