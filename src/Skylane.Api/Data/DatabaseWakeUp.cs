using Microsoft.Data.SqlClient;

namespace Skylane.Api.Data;

/// <summary>
/// Espera que a base de dados esteja disponível antes de atender pedidos à API.
///
/// Na demo publicada, a base de dados é um Azure SQL "serverless" no escalão gratuito: quando
/// ninguém a usa durante algum tempo entra em pausa, e a primeira ligação a seguir falha enquanto
/// ela acorda (pode demorar até um minuto). Em vez de devolver erro a essa primeira pessoa,
/// o pedido espera e vai tentando. Localmente (Docker) a base de dados está sempre ligada
/// e isto passa sem esperar.
/// </summary>
public sealed class DatabaseWakeUp(RequestDelegate next, IConfiguration configuration, ILogger<DatabaseWakeUp> logger)
{
    /// <summary>Se houve uma ligação bem-sucedida há menos disto, não vale a pena verificar.</summary>
    private static readonly TimeSpan RecentlyAwake = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(90);

    private static long _lastAwakeTicks;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api") && !IsRecentlyAwake())
        {
            await WaitUntilAwakeAsync(context.RequestAborted);
        }

        await next(context);
    }

    private static bool IsRecentlyAwake() =>
        DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastAwakeTicks) < RecentlyAwake.Ticks;

    /// <summary>Marca a base de dados como acordada (também usado no arranque da aplicação).</summary>
    public static void MarkAwake() => Interlocked.Exchange(ref _lastAwakeTicks, DateTime.UtcNow.Ticks);

    private async Task WaitUntilAwakeAsync(CancellationToken ct)
    {
        // Só um pedido de cada vez acorda a base de dados; os outros esperam por ele.
        await Gate.WaitAsync(ct);
        try
        {
            if (IsRecentlyAwake())
            {
                return;
            }

            var connectionString = configuration.GetConnectionString("Default");
            var deadline = DateTime.UtcNow + MaxWait;

            while (true)
            {
                try
                {
                    await using var connection = new SqlConnection(connectionString);
                    await connection.OpenAsync(ct);
                    MarkAwake();
                    return;
                }
                catch (SqlException ex) when (DateTime.UtcNow < deadline)
                {
                    logger.LogInformation("Base de dados ainda indisponível ({Number}); a tentar outra vez.", ex.Number);
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
