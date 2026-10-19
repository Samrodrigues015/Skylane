using Skylane.Api.Common;
using Skylane.Api.Data;
using Skylane.Api.Flights;
using Skylane.Api.Reservations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Events;

[Route("api/events")]
public sealed class EventsController(SkylaneDbContext db, ReservationService reservations, TimeProvider time)
    : ApiControllerBase
{
    /// <summary>
    /// Próximos eventos, opcionalmente numa cidade e entre duas datas
    /// (ex.: os eventos de Amesterdão durante a viagem: <c>?city=AMS&amp;from=2026-11-20&amp;to=2026-11-25</c>).
    /// </summary>
    /// <param name="city">Código do aeroporto da cidade.</param>
    /// <param name="from">Primeiro dia (hora local da cidade).</param>
    /// <param name="to">Último dia, inclusive.</param>
    [HttpGet]
    public async Task<IReadOnlyList<EventResponse>> List(
        [FromQuery] string? city, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var query = db.Events.AsNoTracking().Where(e => e.StartsAt > now);

        // Sem cidade, os dias são contados em UTC; com cidade, no fuso dessa cidade.
        var timeZone = "UTC";
        if (!string.IsNullOrWhiteSpace(city))
        {
            city = city.Trim().ToUpperInvariant();
            query = query.Where(e => e.AirportCode == city);
            timeZone = await db.Airports.Where(a => a.Code == city).Select(a => a.TimeZoneId).FirstOrDefaultAsync(ct) ?? "UTC";
        }

        if (from is not null)
        {
            var start = AirportTime.FromLocal(from.Value.ToDateTime(TimeOnly.MinValue), timeZone);
            query = query.Where(e => e.StartsAt >= start);
        }

        if (to is not null)
        {
            var end = AirportTime.FromLocal(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);
            query = query.Where(e => e.StartsAt < end);
        }

        var rows = await query
            .OrderBy(e => e.StartsAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(e => new
            {
                Event = e,
                e.Airport.City,
                e.Airport.TimeZoneId
            })
            .ToListAsync(ct);

        return rows.Select(r => ToResponse(r.Event, r.City, r.TimeZoneId)).ToList();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Get(Guid id, CancellationToken ct)
    {
        var row = await db.Events
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { Event = e, e.Airport.City, e.Airport.TimeZoneId })
            .FirstOrDefaultAsync(ct);

        return row is null ? ToProblem(Errors.EventNotFound) : ToResponse(row.Event, row.City, row.TimeZoneId);
    }

    [HttpPost]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventResponse>> Create(CreateEventRequest request, CancellationToken ct)
    {
        var code = request.AirportCode.Trim().ToUpperInvariant();
        var airport = await db.Airports.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code, ct);
        if (airport is null)
        {
            return ToProblem(Errors.AirportNotFound(code));
        }

        var ev = new Event
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Venue = request.Venue.Trim(),
            AirportCode = code,
            StartsAt = request.StartsAt!.Value.ToUniversalTime(),
            Capacity = request.Capacity,
            Price = request.Price
        };

        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = ev.Id }, ToResponse(ev, airport.City, airport.TimeZoneId));
    }

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

    private static EventResponse ToResponse(Event e, string city, string timeZoneId) => new(
        e.Id, e.Name, e.Venue, e.AirportCode, city,
        AirportTime.ToLocal(e.StartsAt, timeZoneId),
        e.Capacity, e.Capacity - e.Reserved, e.Price, "EUR");
}
