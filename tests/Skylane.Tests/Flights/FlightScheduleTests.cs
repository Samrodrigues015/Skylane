using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skylane.Api.Data;
using Skylane.Api.Flights;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests.Flights;

/// <summary>
/// Voos criados a pedido quando alguém pesquisa uma rota e um dia sem voos.
/// Nos outros testes está desligado (SeedDemoData = false), por isso aqui o FlightSchedule é criado à mão.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FlightScheduleTests(ApiFactory factory)
{
    private static readonly IConfiguration DemoData = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["SeedDemoData"] = "true" })
        .Build();

    [Fact]
    public async Task Cria_os_voos_e_os_lugares_do_dia_pesquisado()
    {
        // Paris → Roma, daqui a 200 dias: rota e dia só usados neste teste.
        var day = DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime).AddDays(200);

        await EnsureAsync("CDG", "FCO", day);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();
        var flights = await FlightsOnRoute(db, "CDG", "FCO");

        Assert.Equal(2, flights.Count);
        foreach (var id in flights)
        {
            Assert.Equal(SeatLayout.Seats.Count, await db.Set<FlightSeat>().CountAsync(s => s.FlightId == id));
        }
    }

    [Fact]
    public async Task Pesquisas_ao_mesmo_tempo_nao_criam_voos_repetidos()
    {
        var day = DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime).AddDays(250);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => EnsureAsync("AMS", "MAD", day)));
        await EnsureAsync("AMS", "MAD", day);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();
        Assert.Equal(2, (await FlightsOnRoute(db, "AMS", "MAD")).Count);
    }

    private async Task EnsureAsync(string from, string to, DateOnly day)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();
        var schedule = new FlightSchedule(db, factory.Clock, DemoData);
        await schedule.EnsureFlightsAsync(from, to, day, days: 1, CancellationToken.None);
    }

    private static Task<List<Guid>> FlightsOnRoute(SkylaneDbContext db, string from, string to) =>
        db.Flights.Where(f => f.OriginCode == from && f.DestinationCode == to).Select(f => f.Id).ToListAsync();
}
