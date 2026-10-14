using Bilheteira.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Bilheteira.Api.Reservations;

/// <summary>
/// Marca como expiradas as reservas pendentes fora de prazo e devolve os lugares ao evento.
/// Está separado do job para poder ser chamado diretamente nos testes.
/// </summary>
public sealed class ReservationExpirer(BilheteiraDbContext db, TimeProvider time)
{
    private const int BatchSize = 100;

    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();

        var due = await db.Reservations
            .AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Select(r => new { r.Id, r.EventId, r.Quantity })
            .Take(BatchSize)
            .ToListAsync(ct);

        var expired = 0;

        foreach (var reservation in due)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Condicional: se o cliente confirmou entretanto, já não está Pending
            // e não mexemos em nada.
            var changed = await db.Reservations
                .Where(r => r.Id == reservation.Id && r.Status == ReservationStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, ReservationStatus.Expired), ct);

            if (changed == 1)
            {
                await db.Events
                    .Where(e => e.Id == reservation.EventId)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Reserved, e => e.Reserved - reservation.Quantity), ct);

                expired++;
            }

            await transaction.CommitAsync(ct);
        }

        return expired;
    }
}
