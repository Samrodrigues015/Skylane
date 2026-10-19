using Bilheteira.Api.Events;
using Bilheteira.Api.Flights;
using Microsoft.EntityFrameworkCore;

namespace Bilheteira.Api.Data;

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
        var db = scope.ServiceProvider.GetRequiredService<BilheteiraDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await db.Database.EnsureCreatedAsync();

        if (!app.Configuration.GetValue<bool>("SeedDemoData"))
        {
            return;
        }

        await FlightSeeder.SeedAsync(db, time);

        if (!await db.Events.AnyAsync())
        {
            var today = time.GetUtcNow().Date;

            db.Events.AddRange(
                NewEvent("Noite de Fado no Cais", "Armazém do Rio, Porto", today.AddDays(9).AddHours(21), 120),
                NewEvent("Festival de Jazz da Ribeira", "Jardim do Morro, Gaia", today.AddDays(23).AddHours(18), 800),
                NewEvent("Stand-up: Sessão Esgotável", "Sala Pequena, Porto", today.AddDays(4).AddHours(22), 8),
                NewEvent("Concerto de Ano Novo", "Auditório Central, Porto", today.AddDays(80).AddHours(17), 450));

            await db.SaveChangesAsync();
        }
    }

    private static Event NewEvent(string name, string venue, DateTime startsAtUtc, int capacity) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Venue = venue,
        StartsAt = new DateTimeOffset(startsAtUtc, TimeSpan.Zero),
        Capacity = capacity
    };
}
