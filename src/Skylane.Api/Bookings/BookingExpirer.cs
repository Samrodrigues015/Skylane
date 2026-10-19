using Skylane.Api.Data;
using Skylane.Api.Flights;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Bookings;

/// <summary>
/// Expira as reservas de voo pendentes fora de prazo e devolve os lugares ao inventário.
/// É chamado pelo mesmo job em segundo plano que expira as reservas de eventos.
/// </summary>
public sealed class BookingExpirer(SkylaneDbContext db, TimeProvider time)
{
    private const int BatchSize = 100;

    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();

        var due = await db.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt <= now)
            .OrderBy(b => b.ExpiresAt)
            .Select(b => b.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        var expired = 0;

        foreach (var bookingId in due)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // Condicional: se o cliente confirmou entretanto, já não está Pending e não mexemos.
            var changed = await db.Bookings
                .Where(b => b.Id == bookingId && b.Status == BookingStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Expired), ct);

            if (changed == 1)
            {
                await db.FlightSeats
                    .Where(s => s.BookingId == bookingId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, SeatStatus.Available)
                        .SetProperty(x => x.BookingId, (Guid?)null)
                        .SetProperty(x => x.PassengerId, (Guid?)null), ct);

                expired++;
            }

            await transaction.CommitAsync(ct);
        }

        return expired;
    }
}
