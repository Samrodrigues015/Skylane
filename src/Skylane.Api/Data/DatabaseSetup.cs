using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Cria a base de dados se não existir. Os dados de demonstração (voos e eventos) são carregados
    /// depois, em segundo plano, pelo <see cref="DemoDataRefresher"/>.
    /// </summary>
    /// <remarks>
    /// O arranque tem de ser rápido: no IIS (ex.: MonsterASP.NET) a aplicação tem 120 segundos para arrancar,
    /// e gerar centenas de milhares de lugares num servidor partilhado pode demorar mais do que isso.
    /// Se a base de dados não responder a tempo, a aplicação arranca na mesma e o
    /// <see cref="DemoDataRefresher"/> volta a tentar.
    ///
    /// EnsureCreated é suficiente para um projeto de demonstração. Num sistema em produção
    /// usaria migrations do EF Core, para conseguir evoluir o esquema sem perder dados.
    /// </remarks>
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SkylaneDbContext>>();

        // Para o diagnóstico: o servidor e a base de dados (nunca a password) a que a aplicação se vai ligar.
        var target = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.Database.GetConnectionString());
        logger.LogInformation("Base de dados: servidor {Server}, base de dados {Database}.", target.DataSource, target.InitialCatalog);

        // Na demo publicada a base de dados pode estar em pausa e demorar a acordar. Tentamos durante
        // 45 segundos no total (contando o tempo de cada ligação), bem abaixo do limite de arranque do IIS.
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (true)
        {
            try
            {
                await EnsureCreatedAsync(db);
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                if (DateTime.UtcNow.AddSeconds(3) >= deadline)
                {
                    logger.LogError(ex, "A base de dados não respondeu no arranque; vai ser tentado de novo em segundo plano.");
                    return;
                }

                logger.LogWarning("A base de dados ainda não respondeu ({Message}). A tentar outra vez.", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    public static async Task EnsureCreatedAsync(SkylaneDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        DatabaseWakeUp.MarkAwake();
    }
}
