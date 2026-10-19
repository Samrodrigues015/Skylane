using Skylane.Api.Common;
using Skylane.Api.Payments;
using Microsoft.AspNetCore.Mvc;

namespace Skylane.Api.Reservations;

[Route("api/reservations")]
public sealed class ReservationsController(ReservationService reservations, PaymentService payments) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Get(Guid id, CancellationToken ct)
    {
        var reservation = await reservations.GetAsync(id, ct);
        return reservation is null ? ToProblem(Errors.ReservationNotFound) : reservation;
    }

    /// <summary>Paga os bilhetes (cartão fictício) e emite-os.</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<ReservationResponse>> Pay(Guid id, CardPaymentRequest request, CancellationToken ct)
    {
        var result = await payments.PayReservationAsync(id, request, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }
}
