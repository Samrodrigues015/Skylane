using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Skylane.Api.Data;
using Skylane.Api.Events;
using Skylane.Tests.Infrastructure;

namespace Skylane.Tests;

/// <summary>Os eventos de demonstração vão sendo criados à medida que os dias passam.</summary>
[Collection(ApiCollection.Name)]
public sealed class EventCatalogTests(ApiFactory factory)
{
    [Fact]
    public async Task Ha_eventos_em_todas_as_cidades_e_correr_duas_vezes_nao_os_duplica()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();

        await EventCatalog.EnsureUpcomingAsync(db, factory.Clock);
        var first = await db.Events.CountAsync();

        await EventCatalog.EnsureUpcomingAsync(db, factory.Clock);
        var second = await db.Events.CountAsync();

        Assert.Equal(first, second);

        var now = factory.Clock.GetUtcNow();
        var limit = now.AddDays(EventCatalog.DaysAhead);
        var cities = await db.Events
            .Where(e => e.StartsAt > now && e.StartsAt <= limit)
            .Select(e => e.AirportCode)
            .Distinct()
            .CountAsync();

        Assert.Equal(await db.Airports.CountAsync(), cities);
    }
}
