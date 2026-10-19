using Skylane.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Skylane.Api.Reservations;

[Route("api/reservations")]
public sealed class ReservationsController(ReservationService reservations) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Get(Guid id, CancellationToken ct)
    {
        var reservation = await reservations.GetAsync(id, ct);
        return reservation is null ? ToProblem(Errors.ReservationNotFound) : reservation;
    }

    /// <summary>
    /// Confirma a compra e emite os bilhetes. Pode ser repetido sem efeitos duplicados.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<ReservationResponse>> Confirm(Guid id, CancellationToken ct)
    {
        var result = await reservations.ConfirmAsync(id, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }
}
