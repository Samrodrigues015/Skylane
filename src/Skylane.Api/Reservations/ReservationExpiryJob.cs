using Skylane.Api.Bookings;
using Microsoft.Extensions.Options;

namespace Skylane.Api.Reservations;

/// <summary>
/// Serviço em segundo plano que, periodicamente, liberta os lugares das reservas expiradas
/// (de eventos e de voos).
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

                var expiredReservations = await scope.ServiceProvider
                    .GetRequiredService<ReservationExpirer>()
                    .ExpireDueAsync(stoppingToken);

                var expiredBookings = await scope.ServiceProvider
                    .GetRequiredService<BookingExpirer>()
                    .ExpireDueAsync(stoppingToken);

                if (expiredReservations + expiredBookings > 0)
                {
                    logger.LogInformation(
                        "Expiradas: {Reservations} reserva(s) de eventos e {Bookings} reserva(s) de voos; lugares libertados.",
                        expiredReservations, expiredBookings);
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
