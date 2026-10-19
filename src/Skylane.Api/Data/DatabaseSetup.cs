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

        // Na demo publicada a base de dados pode estar em pausa (Azure SQL serverless) e demorar
        // até um minuto a acordar: tentamos durante dois minutos antes de desistir.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                DatabaseWakeUp.MarkAwake();
                break;
            }
            catch (Microsoft.Data.SqlClient.SqlException) when (attempt < 40)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        if (!app.Configuration.GetValue<bool>("SeedDemoData"))
        {
            return;
        }

        await FlightSeeder.SeedAsync(db, time);
        await EventCatalog.EnsureUpcomingAsync(db, time);
    }
}
