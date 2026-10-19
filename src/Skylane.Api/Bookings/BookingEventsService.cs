using Skylane.Api.Common;
using Skylane.Api.Data;
using Skylane.Api.Reservations;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Bookings;

/// <summary>
/// O "carrinho" da viagem: bilhetes para eventos juntos a uma reserva de voos ainda por pagar.
/// Os lugares do evento ficam presos até ao mesmo prazo que os do avião e paga-se tudo de uma vez.
/// </summary>
public sealed class BookingEventsService(SkylaneDbContext db, BookingService bookings, ReservationService reservations)
{
    public async Task<Result<BookingResponse>> AddAsync(string reference, AddEventRequest request, CancellationToken ct)
    {
        var (booking, error) = await PendingBookingAsync(reference, ct);
        if (error is not null)
        {
            return error;
        }

        var eventId = request.EventId!.Value;
        if (booking!.Events.Any(e => e.EventId == eventId))
        {
            return Errors.EventAlreadyInBooking;
        }

        var bookingId = await BookingIdAsync(booking.Reference, ct);

        // Mesmo prazo que os lugares do avião: expiram juntos se ninguém pagar.
        var reservation = await reservations.CreateAsync(eventId, request.Quantity, ct, bookingId, booking.ExpiresAt);
        if (!reservation.IsSuccess)
        {
            return reservation.Error!;
        }

        return (await bookings.GetAsync(booking.Reference, ct))!;
    }

    public async Task<Result<BookingResponse>> RemoveAsync(string reference, Guid reservationId, CancellationToken ct)
    {
        var (booking, error) = await PendingBookingAsync(reference, ct);
        if (error is not null)
        {
            return error;
        }

        if (booking!.Events.All(e => e.ReservationId != reservationId))
        {
            return Errors.ReservationNotFound;
        }

        await reservations.CancelAsync(reservationId, await BookingIdAsync(booking.Reference, ct), ct);
        return (await bookings.GetAsync(booking.Reference, ct))!;
    }

    private async Task<(BookingResponse? Booking, Error? Error)> PendingBookingAsync(string reference, CancellationToken ct)
    {
        var booking = await bookings.GetAsync(reference, ct);

        return booking?.Status switch
        {
            null => (null, Errors.BookingNotFound),
            BookingStatus.Confirmed => (null, Errors.BookingAlreadyPaid),
            BookingStatus.Expired => (null, Errors.BookingExpired),
            _ => (booking, null)
        };
    }

    private Task<Guid> BookingIdAsync(string reference, CancellationToken ct) =>
        db.Bookings.Where(b => b.Reference == reference).Select(b => b.Id).SingleAsync(ct);
}
