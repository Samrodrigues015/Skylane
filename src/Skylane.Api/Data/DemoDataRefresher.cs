using Skylane.Api.Events;
using Skylane.Api.Flights;

namespace Skylane.Api.Data;

/// <summary>
/// Mantém a demo sempre "viva": uma vez por dia acrescenta os voos das rotas principais
/// e as sessões dos eventos que entram na janela dos próximos meses.
///
/// Sem isto, uma demo publicada hoje teria daqui a dois meses um quadro de partidas vazio
/// e eventos todos no passado. As outras rotas e datas já são criadas a pedido pelo <see cref="FlightSchedule"/>.
/// </summary>
public sealed class DemoDataRefresher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<DemoDataRefresher> logger) : BackgroundService
{
    private const int FlightDaysAhead = 30;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("SeedDemoData"))
        {
            return;
        }

        do
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Uma falha (ex.: base de dados a arrancar) não deve parar a aplicação: tenta no próximo ciclo.
                logger.LogError(ex, "Erro ao atualizar os dados de demonstração.");
            }
        }
        while (await WaitAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkylaneDbContext>();
        var schedule = scope.ServiceProvider.GetRequiredService<FlightSchedule>();

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        foreach (var (from, to) in FlightTimetable.MainRoutePairs)
        {
            await schedule.EnsureFlightsAsync(from, to, today, FlightDaysAhead, ct);
        }

        await EventCatalog.EnsureUpcomingAsync(db, time, ct);
    }

    private static async Task<bool> WaitAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Interval, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
