using Skylane.Api.Events;
using Skylane.Api.Flights;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Cria a base de dados se não existir e, se configurado, carrega eventos e voos de demonstração.
    /// </summary>
    /// <remarks>
    /// EnsureCreated é suficiente para um projeto de demonstração. Num sistema em produção
    /// usaria migrations do EF Core, para conseguir evoluir o esquema sem perder dados.
    /// </remarks>
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await db.Database.EnsureCreatedAsync();

        if (!app.Configuration.GetValue<bool>("SeedDemoData"))
        {
            return;
        }

        await FlightSeeder.SeedAsync(db, time);
        await EventCatalog.SeedAsync(db, time);
    }
}
