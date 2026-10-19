using System.ComponentModel.DataAnnotations;
using Skylane.Api.Bookings;
using Skylane.Api.Common;
using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Payments;

public sealed record PayBookingRequest(
    [Required, StringLength(23, MinimumLength = 12)] string CardNumber,
    [Required, StringLength(60, MinimumLength = 2)] string CardHolder,
    [Required, RegularExpression(@"^(0[1-9]|1[0-2])/\d{2}$")] string Expiry,
    [Required, RegularExpression(@"^\d{3,4}$")] string Cvc);

/// <summary>
/// Paga uma reserva de voo: cobra o total no prestador de pagamentos e, se for aprovado,
/// confirma a reserva (os lugares passam de Held para Sold).
/// </summary>
public sealed class BookingPaymentService(
    SkylaneDbContext db,
    BookingService bookings,
    IPaymentGateway gateway,
    TimeProvider time)
{
    public async Task<Result<BookingResponse>> PayAsync(string reference, PayBookingRequest request, CancellationToken ct)
    {
        var booking = await bookings.GetAsync(reference, ct);

        if (booking is null)
        {
            return Errors.BookingNotFound;
        }

        // Já paga: devolvemos a reserva sem cobrar outra vez (duplo clique, retry de rede).
        if (booking.Status == BookingStatus.Confirmed)
        {
            return booking;
        }

        if (booking.Status == BookingStatus.Expired)
        {
            return Errors.BookingExpired;
        }

        var cardNumber = new string(request.CardNumber.Where(char.IsAsciiDigit).ToArray());

        if (!FakePaymentGateway.IsValidCardNumber(cardNumber))
        {
            return Errors.InvalidCard;
        }

        if (IsExpired(request.Expiry))
        {
            return Errors.CardExpired;
        }

        var bookingId = await db.Bookings
            .Where(b => b.Reference == booking.Reference)
            .Select(b => b.Id)
            .SingleAsync(ct);

        var result = await gateway.ChargeAsync(
            new PaymentRequest(booking.Reference, booking.Total, booking.Currency, cardNumber, request.CardHolder.Trim()),
            ct);

        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            BookingId = bookingId,
            Amount = booking.Total,
            Currency = booking.Currency,
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
            return result.DeclineCode == "insufficient_funds" ? Errors.InsufficientFunds : Errors.CardDeclined;
        }

        var confirmed = await bookings.ConfirmAsync(booking.Reference, ct);

        if (!confirmed.IsSuccess)
        {
            // A reserva expirou enquanto o banco respondia. Num prestador real, aqui pedíamos o reembolso.
            payment.Status = PaymentStatus.Refunded;
            await db.SaveChangesAsync(ct);
        }

        return confirmed;
    }

    /// <summary>"MM/AA": o cartão é válido até ao último dia desse mês.</summary>
    private bool IsExpired(string expiry)
    {
        var month = int.Parse(expiry[..2]);
        var year = 2000 + int.Parse(expiry[3..]);
        var firstDayAfter = new DateTime(year, month, 1).AddMonths(1);
        return time.GetUtcNow().UtcDateTime >= firstDayAfter;
    }
}
