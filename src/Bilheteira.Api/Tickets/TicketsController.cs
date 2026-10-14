using Bilheteira.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Bilheteira.Api.Tickets;

[Route("api/tickets")]
public sealed class TicketsController(TicketService tickets) : ApiControllerBase
{
    [HttpGet("{code}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Get(string code, CancellationToken ct)
    {
        var ticket = await tickets.GetAsync(code, ct);
        return ticket is null ? ToProblem(Errors.TicketNotFound) : ticket;
    }

    /// <summary>Valida o bilhete à entrada do evento. Cada bilhete só entra uma vez.</summary>
    [HttpPost("{code}/check-in")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketResponse>> CheckIn(string code, CancellationToken ct)
    {
        var result = await tickets.CheckInAsync(code, ct);
        return result.IsSuccess ? result.Value! : ToProblem(result.Error!);
    }
}
