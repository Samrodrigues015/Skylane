using Skylane.Api.Common;
using Skylane.Api.Data;
using Skylane.Api.Flights;
using Skylane.Api.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Skylane.Api.Bookings;

/// <summary>
/// Reservas de voo com lugares marcados, só ida ou ida e volta.
///
/// Usa o mesmo padrão dos eventos: cada lugar é preso com um UPDATE condicional
/// ("muda para Held SE ainda estiver Available"). A diferença é que aqui uma reserva
/// prende vários lugares, possivelmente em dois voos, e tem de ser tudo ou nada.
/// </summary>
public sealed class BookingService(
    SkylaneDbContext db,
    TimeProvider time,
    ReservationService reservations,
    IOptions<BookingOptions> options)
{
    private const string Currency = "EUR";

    private readonly BookingOptions _options = options.Value;

    private sealed record Leg(LegDirection Direction, Guid FlightId, List<string> Seats);

    public async Task<Result<BookingResponse>> CreateAsync(CreateBookingRequest request, CancellationToken ct)
    {
        // ---------- 1. Validar o pedido (sem tocar na base de dados) ----------

        var events = request.Events ?? new List<EventSelection>();
        var passengers = request.Passengers ?? new List<PassengerRequest>();

        if (request.Outbound is null && events.Count == 0)
        {
            return Errors.EmptyBooking;
        }

        if (request.Outbound is null && request.Return is not null)
        {
            return Errors.InvalidReturnFlight;
        }

        if (events.Select(e => e.EventId).Distinct().Count() != events.Count)
        {
            return Errors.EventAlreadyInBooking;
        }

        var legs = new List<Leg>();
        if (request.Outbound is not null)
        {
            legs.Add(ToLeg(LegDirection.Outbound, request.Outbound));
        }

        if (request.Return is not null)
        {
            legs.Add(ToLeg(LegDirection.Return, request.Return));
        }

        var passengerCount = passengers.Count;
        if (legs.Count > 0 && (passengerCount < 1 || passengerCount > _options.MaxPassengers))
        {
            return Errors.InvalidPassengerCount(_options.MaxPassengers);
        }

        foreach (var leg in legs)
        {
            if (leg.Seats.Count != passengerCount)
            {
                return Errors.SeatsMustMatchPassengers;
            }

            if (leg.Seats.Distinct().Count() != leg.Seats.Count)
            {
                return Errors.DuplicateSeats;
            }

            var invalidSeat = leg.Seats.FirstOrDefault(seat => !SeatLayout.TryGet(seat, out _));
            if (invalidSeat is not null)
            {
                return Errors.InvalidSeat(invalidSeat);
            }
        }

        // ---------- 2. Validar os voos ----------

        var now = time.GetUtcNow();
        var flightIds = legs.Select(l => l.FlightId).Distinct().ToList();

        if (flightIds.Count != legs.Count)
        {
            return Errors.InvalidReturnFlight;
        }

        var flights = await db.Flights
            .AsNoTracking()
            .Where(f => flightIds.Contains(f.Id))
            .Select(f => new { f.Id, f.OriginCode, f.DestinationCode, f.DepartsAt, f.ArrivesAt, f.Status })
            .ToDictionaryAsync(f => f.Id, ct);

        foreach (var leg in legs)
        {
            if (!flights.TryGetValue(leg.FlightId, out var flight))
            {
                return Errors.FlightNotFound;
            }

            if (flight.Status != FlightStatus.Scheduled || flight.DepartsAt <= now)
            {
                return Errors.FlightNotBookable;
            }
        }

        if (legs.Count == 2)
        {
            var outbound = flights[legs[0].FlightId];
            var inbound = flights[legs[1].FlightId];

            var isReverseRoute = inbound.OriginCode == outbound.DestinationCode
                                 && inbound.DestinationCode == outbound.OriginCode;

            if (!isReverseRoute || inbound.DepartsAt <= outbound.ArrivesAt)
            {
                return Errors.InvalidReturnFlight;
            }
        }

        // ---------- 3. Calcular o preço ----------

        // Os preços são lidos ANTES da transação. A classe de cada lugar vem do layout do
        // avião (o 2A é sempre Business), por isso não precisamos de ler a tabela FlightSeats
        // para saber quanto custa. Ler lugares dentro da transação podia bloquear em lugares
        // que outra reserva está a prender ao mesmo tempo.
        var fares = await db.FlightFares
            .AsNoTracking()
            .Where(f => flightIds.Contains(f.FlightId))
            .ToDictionaryAsync(f => (f.FlightId, f.Class), f => f.Price, ct);

        var total = legs.Sum(leg => leg.Seats.Sum(seat =>
        {
            SeatLayout.TryGet(seat, out var definition);
            return fares[(leg.FlightId, definition.Class)];
        }));

        // ---------- 4. Criar a reserva e prender os lugares (tudo ou nada) ----------

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var booking = new Booking
        {
            Id = Guid.CreateVersion7(),
            Reference = BookingReference.Generate(),
            Status = BookingStatus.Pending,
            ContactEmail = request.ContactEmail.Trim().ToLowerInvariant(),
            OutboundFlightId = legs.Count > 0 ? legs[0].FlightId : null,
            ReturnFlightId = legs.Count == 2 ? legs[1].FlightId : null,
            Total = total,
            Currency = Currency,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.HoldMinutes),
            // Numa compra só de eventos não há passageiros.
            Passengers = (legs.Count > 0 ? passengers : new List<PassengerRequest>()).Select(p => new Passenger
            {
                Id = Guid.CreateVersion7(),
                FirstName = p.FirstName.Trim(),
                LastName = p.LastName.Trim(),
                DocumentNumber = p.DocumentNumber.Trim().ToUpperInvariant()
            }).ToList()
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        // Cada lugar fica com o passageiro na mesma posição da lista.
        // Ordenamos por (voo, lugar) antes de prender: se duas reservas querem os mesmos
        // lugares, ambas tentam-nos pela mesma ordem e nunca ficam à espera uma da outra
        // em círculo (deadlock). Uma passa, a outra recebe "lugar indisponível".
        var assignments = legs
            .SelectMany(leg => leg.Seats.Select((seat, index) => new
            {
                leg.FlightId,
                Seat = seat,
                PassengerId = booking.Passengers[index].Id
            }))
            .OrderBy(a => a.FlightId)
            .ThenBy(a => a.Seat, StringComparer.Ordinal)
            .ToList();

        foreach (var assignment in assignments)
        {
            // UPDATE FlightSeats SET Status = 'Held', BookingId = @b, PassengerId = @p
            // WHERE FlightId = @f AND SeatNumber = @s AND Status = 'Available'
            var held = await db.FlightSeats
                .Where(s => s.FlightId == assignment.FlightId
                            && s.SeatNumber == assignment.Seat
                            && s.Status == SeatStatus.Available)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SeatStatus.Held)
                    .SetProperty(x => x.BookingId, (Guid?)booking.Id)
                    .SetProperty(x => x.PassengerId, (Guid?)assignment.PassengerId), ct);

            if (held == 0)
            {
                // Basta um lugar falhar para desfazer tudo: a reserva e os lugares já presos.
                await transaction.RollbackAsync(ct);
                return Errors.SeatUnavailable(assignment.Seat);
            }
        }

        // Bilhetes para eventos que vinham no carrinho: presos na mesma transação e com o mesmo
        // prazo que os lugares do avião. Se um evento esgotar, nada fica reservado.
        // Também por ordem fixa (do Id), pela mesma razão dos lugares: evitar deadlocks.
        foreach (var selection in events.OrderBy(e => e.EventId))
        {
            var held = await reservations.HoldAsync(
                selection.EventId!.Value, selection.Quantity, booking.Id, booking.ExpiresAt, ct);

            if (held is not null)
            {
                await transaction.RollbackAsync(ct);
                return held;
            }
        }

        await transaction.CommitAsync(ct);

        return (await GetAsync(booking.Reference, ct))!;
    }

    /// <summary>
    /// Confirma a reserva: os lugares passam de Held para Sold. Idempotente: confirmar duas
    /// vezes devolve o mesmo resultado. (O pagamento entra aqui numa fase seguinte.)
    /// </summary>
    public async Task<Result<BookingResponse>> ConfirmAsync(string reference, CancellationToken ct)
    {
        reference = BookingReference.Normalize(reference);
        var now = time.GetUtcNow();

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var bookingId = await db.Bookings
                .Where(b => b.Reference == reference)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct);

            if (bookingId is null)
            {
                return Errors.BookingNotFound;
            }

            // Mesmo padrão dos eventos: só confirma se ainda estiver pendente E dentro do prazo.
            // Se o job de expiração correr ao mesmo tempo, só um dos dois UPDATEs encontra "Pending".
            var confirmed = await db.Bookings
                .Where(b => b.Id == bookingId.Value
                            && b.Status == BookingStatus.Pending
                            && b.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, BookingStatus.Confirmed)
                    .SetProperty(b => b.ConfirmedAt, (DateTimeOffset?)now), ct);

            if (confirmed == 1)
            {
                await db.FlightSeats
                    .Where(s => s.BookingId == bookingId.Value)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SeatStatus.Sold), ct);
            }

            await transaction.CommitAsync(ct);
        }

        var current = await GetAsync(reference, ct);

        return current switch
        {
            null => Errors.BookingNotFound,
            { Status: BookingStatus.Confirmed } => current,
            _ => Errors.BookingExpired
        };
    }

    public async Task<BookingResponse?> GetAsync(string reference, CancellationToken ct)
    {
        reference = BookingReference.Normalize(reference);

        var booking = await db.Bookings
            .AsNoTracking()
            .Where(b => b.Reference == reference)
            .Select(b => new
            {
                b.Id,
                b.Reference,
                b.Status,
                b.ContactEmail,
                b.Total,
                b.Currency,
                b.ExpiresAt,
                b.OutboundFlightId,
                b.ReturnFlightId,
                Passengers = b.Passengers.Select(p => p.FirstName + " " + p.LastName).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (booking is null)
        {
            return null;
        }

        var flightIds = new List<Guid>();
        if (booking.OutboundFlightId is { } outboundId)
        {
            flightIds.Add(outboundId);
        }

        if (booking.ReturnFlightId is { } returnId)
        {
            flightIds.Add(returnId);
        }

        var flights = await db.Flights
            .AsNoTracking()
            .Where(f => flightIds.Contains(f.Id))
            .Select(f => new
            {
                f.Id,
                f.Number,
                OriginCode = f.Origin.Code,
                OriginCity = f.Origin.City,
                OriginTimeZone = f.Origin.TimeZoneId,
                DestinationCode = f.Destination.Code,
                DestinationCity = f.Destination.City,
                DestinationTimeZone = f.Destination.TimeZoneId,
                f.DepartsAt,
                f.ArrivesAt
            })
            .ToDictionaryAsync(f => f.Id, ct);

        // Os lugares só existem enquanto a reserva os tem. Numa reserva expirada a lista vem vazia.
        var seats = await db.FlightSeats
            .AsNoTracking()
            .Where(s => s.BookingId == booking.Id)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Letter)
            .Select(s => new
            {
                s.FlightId,
                s.SeatNumber,
                s.Class,
                Passenger = s.Passenger!.FirstName + " " + s.Passenger.LastName,
                Price = db.FlightFares
                    .Where(f => f.FlightId == s.FlightId && f.Class == s.Class)
                    .Select(f => f.Price)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var legs = flightIds.Select(id =>
        {
            var flight = flights[id];

            return new BookingLeg(
                id == booking.OutboundFlightId ? LegDirection.Outbound : LegDirection.Return,
                flight.Id,
                flight.Number,
                new AirportInfo(flight.OriginCode, flight.OriginCity),
                new AirportInfo(flight.DestinationCode, flight.DestinationCity),
                AirportTime.ToLocal(flight.DepartsAt, flight.OriginTimeZone),
                AirportTime.ToLocal(flight.ArrivesAt, flight.DestinationTimeZone),
                seats
                    .Where(s => s.FlightId == id)
                    .Select(s => new BookedSeat(s.Passenger, s.SeatNumber, s.Class, s.Price))
                    .ToList());
        }).ToList();

        // O job corre de tempos a tempos; entretanto, uma reserva pendente fora de prazo
        // já é, para quem pergunta, uma reserva expirada.
        var status = booking.Status == BookingStatus.Pending && booking.ExpiresAt <= time.GetUtcNow()
            ? BookingStatus.Expired
            : booking.Status;

        // Eventos juntos à viagem (os retirados do carrinho ou expirados já não contam).
        var eventRows = await db.Reservations
            .AsNoTracking()
            .Where(r => r.BookingId == booking.Id &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .OrderBy(r => r.Event.StartsAt)
            .Select(r => new
            {
                r.Id,
                r.EventId,
                r.Event.Name,
                r.Event.Venue,
                r.Event.Airport.City,
                r.Event.Airport.TimeZoneId,
                r.Event.StartsAt,
                r.Quantity,
                r.UnitPrice,
                r.Status,
                Tickets = r.Tickets.OrderBy(t => t.Code).Select(t => t.Code).ToList()
            })
            .ToListAsync(ct);

        var events = eventRows
            .Select(r => new BookedEvent(
                r.Id, r.EventId, r.Name, r.Venue, r.City,
                AirportTime.ToLocal(r.StartsAt, r.TimeZoneId),
                r.Quantity, r.UnitPrice, r.UnitPrice * r.Quantity, r.Status, r.Tickets))
            .ToList();

        return new BookingResponse(
            booking.Reference,
            status,
            booking.ContactEmail,
            booking.Total + events.Sum(e => e.Total),
            booking.Currency,
            booking.ExpiresAt,
            booking.Passengers,
            legs,
            events);
    }

    private static Leg ToLeg(LegDirection direction, FlightSelection selection) => new(
        direction,
        selection.FlightId!.Value,
        selection.Seats.Select(s => s.Trim().ToUpperInvariant()).ToList());
}
