using System.ComponentModel.DataAnnotations;
using Skylane.Api.Bookings;
using Skylane.Api.Common;
using Skylane.Api.Data;
using Skylane.Api.Reservations;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Payments;

/// <param name="Expiry">Validade no formato MM/AA.</param>
public sealed record CardPaymentRequest(
    [Required, StringLength(23, MinimumLength = 12)] string CardNumber,
    [Required, StringLength(60, MinimumLength = 2)] string CardHolder,
    [Required, RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$")] string Expiry,
    [Required, RegularExpression(@"^\d{3,4}$")] string Cvc);

/// <summary>
/// Pagamentos com cartão: de uma viagem (voos e os eventos que lhe foram juntos)
/// ou só de bilhetes para um evento. Só depois de o pagamento ser aprovado é que
/// as reservas passam a confirmadas e os bilhetes são emitidos.
/// </summary>
public sealed class PaymentService(
    SkylaneDbContext db,
    BookingService bookings,
    ReservationService reservations,
    IPaymentGateway gateway,
    TimeProvider time)
{
    public async Task<Result<BookingResponse>> PayBookingAsync(string reference, CardPaymentRequest request, CancellationToken ct)
    {
        reference = BookingReference.Normalize(reference);

        // Dois pedidos de pagamento ao mesmo tempo (duplo clique, retry) não podem cobrar duas vezes:
        // o segundo espera pelo primeiro e depois já encontra a reserva paga.
        await using var _ = await DbLock.AcquireAsync(db, $"pay-booking:{reference}", ct);

        var booking = await bookings.GetAsync(reference, ct);

        if (booking is null)
        {
            return Errors.BookingNotFound;
        }

        // Já paga: devolvemos a reserva sem cobrar outra vez.
        if (booking.Status == BookingStatus.Confirmed)
        {
            return booking;
        }

        if (booking.Status == BookingStatus.Expired)
        {
            return Errors.BookingExpired;
        }

        var bookingId = await db.Bookings.Where(b => b.Reference == reference).Select(b => b.Id).SingleAsync(ct);

        var charge = await ChargeAsync(request, booking.Reference, booking.Total, booking.Currency,
            bookingId: bookingId, reservationId: null, ct);

        if (charge.Error is not null)
        {
            return charge.Error;
        }

        var confirmed = await bookings.ConfirmAsync(booking.Reference, ct);

        if (!confirmed.IsSuccess)
        {
            // A reserva expirou enquanto o banco respondia. Num prestador real, aqui pedíamos o reembolso.
            await MarkRefundedAsync(charge.Payment!, ct);
            return confirmed;
        }

        // Os eventos da viagem foram pagos juntos: emitimos agora os bilhetes.
        foreach (var ev in booking.Events.Where(e => e.Status == ReservationStatus.Pending))
        {
            await reservations.ConfirmAsync(ev.ReservationId, ct);
        }

        return (await bookings.GetAsync(booking.Reference, ct))!;
    }

    public async Task<Result<ReservationResponse>> PayReservationAsync(Guid reservationId, CardPaymentRequest request, CancellationToken ct)
    {
        await using var _ = await DbLock.AcquireAsync(db, $"pay-reservation:{reservationId}", ct);

        var reservation = await reservations.GetAsync(reservationId, ct);

        if (reservation is null)
        {
            return Errors.ReservationNotFound;
        }

        if (reservation.BookingReference is not null)
        {
            return Errors.ReservationPaidWithBooking;
        }

        if (reservation.Status == ReservationStatus.Confirmed)
        {
            return reservation;
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            return Errors.ReservationExpired;
        }

        var charge = await ChargeAsync(request, reservation.Id.ToString(), reservation.Total, reservation.Currency,
            bookingId: null, reservationId: reservation.Id, ct);

        if (charge.Error is not null)
        {
            return charge.Error;
        }

        var confirmed = await reservations.ConfirmAsync(reservation.Id, ct);

        if (!confirmed.IsSuccess)
        {
            await MarkRefundedAsync(charge.Payment!, ct);
        }

        return confirmed;
    }

    private sealed record Charge(Payment? Payment, Error? Error);

    /// <summary>Valida o cartão, cobra no gateway e regista o pagamento (aprovado ou recusado).</summary>
    private async Task<Charge> ChargeAsync(
        CardPaymentRequest request, string idempotencyKey, decimal amount, string currency,
        Guid? bookingId, Guid? reservationId, CancellationToken ct)
    {
        var cardNumber = new string(request.CardNumber.Where(char.IsAsciiDigit).ToArray());

        if (!FakePaymentGateway.IsValidCardNumber(cardNumber))
        {
            return new Charge(null, Errors.InvalidCard);
        }

        if (IsExpired(request.Expiry))
        {
            return new Charge(null, Errors.CardExpired);
        }

        var result = await gateway.ChargeAsync(
            new PaymentRequest(idempotencyKey, amount, currency, cardNumber, request.CardHolder.Trim()), ct);

        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            ReservationId = reservationId,
            Amount = amount,
            Currency = currency,
            Status = result.Approved ? PaymentStatus.Approved : PaymentStatus.Declined,
            CardLast4 = cardNumber[^4..],
            ProviderReference = result.ProviderReference,
            DeclineCode = result.DeclineCode,
            CreatedAt = time.GetUtcNow()
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        if (!result.Approved)
        {
            // A reserva continua pendente: a pessoa pode tentar outro cartão até ao fim do prazo.
            var error = result.DeclineCode == "insufficient_funds" ? Errors.InsufficientFunds : Errors.CardDeclined;
            return new Charge(payment, error);
        }

        return new Charge(payment, null);
    }

    private async Task MarkRefundedAsync(Payment payment, CancellationToken ct)
    {
        payment.Status = PaymentStatus.Refunded;
        await db.SaveChangesAsync(ct);
    }

    private bool IsExpired(string expiry)
    {
        var month = int.Parse(expiry[..2]);
        var year = 2000 + int.Parse(expiry[3..]);
        var firstDayAfter = new DateTime(year, month, 1).AddMonths(1);
        return time.GetUtcNow().UtcDateTime >= firstDayAfter;
    }
}
