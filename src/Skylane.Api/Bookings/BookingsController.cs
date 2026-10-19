using Skylane.Api.Common;
using Skylane.Api.Payments;
using Microsoft.AspNetCore.Mvc;

namespace Skylane.Api.Bookings;

[Route("api/bookings")]
public sealed class BookingsController(BookingService bookings, BookingEventsService events, PaymentService payments)
    : ApiControllerBase
{
    /// <summary>
    /// Cria uma reserva (só ida ou ida e volta) e prende os lugares escolhidos durante alguns minutos.
    /// Se algum lugar já estiver ocupado, nada fica reservado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var result = await bookings.CreateAsync(request, ct);

        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        return CreatedAtAction(nameof(Get), new { reference = result.Value!.Reference }, result.Value);
    }

    [HttpGet("{reference}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Get(string reference, CancellationToken ct)
    {
        var booking = await bookings.GetAsync(reference, ct);
        return booking is null ? ToProblem(Errors.BookingNotFound) : booking;
    }

    /// <summary>Junta bilhetes de um evento à viagem, antes de pagar.</summary>
    [HttpPost("{reference}/events")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<BookingResponse>> AddEvent(string reference, AddEventRequest request, CancellationToken ct)
    {
        var result = await events.AddAsync(reference, request, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }

    /// <summary>Retira da viagem os bilhetes de um evento (ainda por pagar).</summary>
    [HttpDelete("{reference}/events/{reservationId:guid}")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> RemoveEvent(string reference, Guid reservationId, CancellationToken ct)
    {
        var result = await events.RemoveAsync(reference, reservationId, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }

    /// <summary>Paga a viagem (voos e eventos) e emite os cartões de embarque e os bilhetes.</summary>
    [HttpPost("{reference}/pay")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<BookingResponse>> Pay(string reference, CardPaymentRequest request, CancellationToken ct)
    {
        var result = await payments.PayBookingAsync(reference, request, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }
}
