using Skylane.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Skylane.Api.Bookings;

[Route("api/bookings")]
public sealed class BookingsController(BookingService bookings) : ApiControllerBase
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

    /// <summary>Confirma a compra. Pode ser repetido sem efeitos duplicados.</summary>
    [HttpPost("{reference}/confirm")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<BookingResponse>> Confirm(string reference, CancellationToken ct)
    {
        var result = await bookings.ConfirmAsync(reference, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }
}
