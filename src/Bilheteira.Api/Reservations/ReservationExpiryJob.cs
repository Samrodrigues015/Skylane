using Microsoft.Extensions.Options;

namespace Bilheteira.Api.Reservations;

/// <summary>
/// Serviço em segundo plano que, periodicamente, liberta os lugares das reservas expiradas.
/// </summary>
public sealed class ReservationExpiryJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationOptions> options,
    ILogger<ReservationExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ExpiryJobEnabled)
        {
            logger.LogInformation("Job de expiração de reservas desativado por configuração.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ExpiryCheckSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // O DbContext é "scoped": criamos um scope novo em cada ciclo.
                using var scope = scopeFactory.CreateScope();
                var expirer = scope.ServiceProvider.GetRequiredService<ReservationExpirer>();

                var expired = await expirer.ExpireDueAsync(stoppingToken);

                if (expired > 0)
                {
                    logger.LogInformation("{Count} reserva(s) expirada(s); lugares libertados.", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Uma falha num ciclo (ex.: base de dados indisponível) não deve matar o job.
                logger.LogError(ex, "Erro ao expirar reservas.");
            }
        }
    }
}
