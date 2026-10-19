using Skylane.Api.Common;
using Skylane.Api.Data;
using Skylane.Api.Reservations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Events;

[Route("api/events")]
public sealed class EventsController(SkylaneDbContext db, ReservationService reservations) : ApiControllerBase
{
    /// <summary>Lista os eventos, do mais próximo para o mais distante.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<EventResponse>> List(CancellationToken ct) =>
        await db.Events
            .AsNoTracking()
            .OrderBy(e => e.StartsAt)
            .Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.StartsAt, e.Capacity, e.Capacity - e.Reserved))
            .ToListAsync(ct);

    /// <summary>Devolve um evento, incluindo os lugares disponíveis neste momento.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Get(Guid id, CancellationToken ct)
    {
        var ev = await db.Events
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.StartsAt, e.Capacity, e.Capacity - e.Reserved))
            .FirstOrDefaultAsync(ct);

        return ev is null ? ToProblem(Errors.EventNotFound) : ev;
    }

    /// <summary>Cria um evento com uma lotação.</summary>
    [HttpPost]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventResponse>> Create(CreateEventRequest request, CancellationToken ct)
    {
        var ev = new Event
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Venue = request.Venue.Trim(),
            StartsAt = request.StartsAt!.Value,
            Capacity = request.Capacity
        };

        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        var response = new EventResponse(ev.Id, ev.Name, ev.Venue, ev.StartsAt, ev.Capacity, ev.Available);
        return CreatedAtAction(nameof(Get), new { id = ev.Id }, response);
    }

    /// <summary>
    /// Reserva lugares. Ficam presos durante alguns minutos à espera de confirmação.
    /// </summary>
    [HttpPost("{id:guid}/reservations")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Reserve(
        Guid id, CreateReservationRequest request, CancellationToken ct)
    {
        var result = await reservations.CreateAsync(id, request.Quantity, ct);

        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        return Created($"/api/reservations/{result.Value!.Id}", result.Value);
    }
}
