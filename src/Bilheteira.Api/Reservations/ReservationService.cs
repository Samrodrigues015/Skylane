using Bilheteira.Api.Common;
using Bilheteira.Api.Data;
using Bilheteira.Api.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bilheteira.Api.Reservations;

/// <summary>
/// Regras de reserva e confirmação.
///
/// Ideia central: cada mudança de estado é um UPDATE condicional na base de dados
/// ("altera SE ainda for válido"). A base de dados executa cada UPDATE de forma atómica
/// e bloqueia a linha enquanto o faz, por isso dois pedidos ao mesmo tempo nunca
/// conseguem os dois o último lugar. Quem perde recebe "0 linhas afetadas" e sabe
/// que tem de responder "esgotado".
/// </summary>
public sealed class ReservationService(
    BilheteiraDbContext db,
    TimeProvider time,
    IOptions<ReservationOptions> options)
{
    private readonly ReservationOptions _options = options.Value;

    public async Task<Result<ReservationResponse>> CreateAsync(Guid eventId, int quantity, CancellationToken ct)
    {
        if (quantity < 1 || quantity > _options.MaxTicketsPerReservation)
        {
            return Errors.InvalidQuantity(_options.MaxTicketsPerReservation);
        }

        // A transação garante que o contador e a reserva mudam juntos:
        // se a inserção da reserva falhar, o incremento do contador é desfeito.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // UPDATE Events SET Reserved = Reserved + @q
        // WHERE Id = @id AND Reserved + @q <= Capacity
        var seatsTaken = await db.Events
            .Where(e => e.Id == eventId && e.Reserved + quantity <= e.Capacity)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Reserved, e => e.Reserved + quantity), ct);

        if (seatsTaken == 0)
        {
            // Nada foi alterado: ou o evento não existe, ou não há lugares que cheguem.
            var exists = await db.Events.AnyAsync(e => e.Id == eventId, ct);
            return exists ? Errors.SoldOut : Errors.EventNotFound;
        }

        var now = time.GetUtcNow();
        var reservation = new Reservation
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            Quantity = quantity,
            Status = ReservationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.HoldMinutes)
        };

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (await GetAsync(reservation.Id, ct))!;
    }

    /// <summary>
    /// Confirma a compra e emite os bilhetes. É idempotente: confirmar duas vezes
    /// a mesma reserva devolve os mesmos bilhetes, sem emitir novos.
    /// </summary>
    public async Task<Result<ReservationResponse>> ConfirmAsync(Guid reservationId, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            // Só passa de Pending para Confirmed se ainda estiver dentro do prazo.
            // Se o job de expiração tentar expirar a mesma reserva ao mesmo tempo,
            // só um dos dois UPDATEs encontra Status = Pending.
            var confirmed = await db.Reservations
                .Where(r => r.Id == reservationId
                            && r.Status == ReservationStatus.Pending
                            && r.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, ReservationStatus.Confirmed)
                    .SetProperty(r => r.ConfirmedAt, (DateTimeOffset?)now), ct);

            if (confirmed == 1)
            {
                var quantity = await db.Reservations
                    .Where(r => r.Id == reservationId)
                    .Select(r => r.Quantity)
                    .SingleAsync(ct);

                var tickets = Enumerable.Range(0, quantity).Select(_ => new Ticket
                {
                    Id = Guid.CreateVersion7(),
                    ReservationId = reservationId,
                    Code = TicketCode.Generate()
                });

                db.Tickets.AddRange(tickets);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
        }

        // Quer tenhamos confirmado agora, quer já estivesse confirmada antes,
        // o estado atual diz-nos o que responder.
        var current = await GetAsync(reservationId, ct);

        return current switch
        {
            null => Errors.ReservationNotFound,
            { Status: ReservationStatus.Confirmed } => current,
            _ => Errors.ReservationExpired
        };
    }

    public async Task<ReservationResponse?> GetAsync(Guid reservationId, CancellationToken ct)
    {
        var row = await db.Reservations
            .AsNoTracking()
            .Where(r => r.Id == reservationId)
            .Select(r => new
            {
                r.Id,
                r.EventId,
                EventName = r.Event.Name,
                r.Quantity,
                r.Status,
                r.ExpiresAt,
                Tickets = r.Tickets.OrderBy(t => t.Code).Select(t => t.Code).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        // O job corre de 30 em 30 segundos; entretanto, uma reserva pendente
        // fora de prazo já é, para quem pergunta, uma reserva expirada.
        var status = row.Status == ReservationStatus.Pending && row.ExpiresAt <= time.GetUtcNow()
            ? ReservationStatus.Expired
            : row.Status;

        return new ReservationResponse(
            row.Id, row.EventId, row.EventName, row.Quantity, status, row.ExpiresAt, row.Tickets);
    }
}
