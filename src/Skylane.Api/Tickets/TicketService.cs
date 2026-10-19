using Skylane.Api.Common;
using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Tickets;

public sealed class TicketService(SkylaneDbContext db, TimeProvider time)
{
    public async Task<TicketResponse?> GetAsync(string code, CancellationToken ct)
    {
        var normalized = TicketCode.Normalize(code);

        return await db.Tickets
            .AsNoTracking()
            .Where(t => t.Code == normalized)
            .Select(t => new TicketResponse(
                t.Code,
                t.Reservation.Event.Name,
                t.Reservation.Event.Venue,
                t.Reservation.Event.StartsAt,
                t.CheckedInAt))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Valida o bilhete à entrada. Usa o mesmo padrão das reservas: um UPDATE condicional
    /// garante que, se o mesmo código for lido em duas portas ao mesmo tempo, só uma deixa entrar.
    /// </summary>
    public async Task<Result<TicketResponse>> CheckInAsync(string code, CancellationToken ct)
    {
        var normalized = TicketCode.Normalize(code);
        var now = time.GetUtcNow();

        var checkedIn = await db.Tickets
            .Where(t => t.Code == normalized && t.CheckedInAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CheckedInAt, (DateTimeOffset?)now), ct);

        var ticket = await GetAsync(normalized, ct);

        if (ticket is null)
        {
            return Errors.TicketNotFound;
        }

        return checkedIn == 1 ? ticket : Errors.TicketAlreadyUsed;
    }
}
