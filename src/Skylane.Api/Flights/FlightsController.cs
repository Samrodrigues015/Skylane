using Skylane.Api.Common;
using Skylane.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Flights;

[Route("api/airports")]
public sealed class AirportsController(SkylaneDbContext db) : ApiControllerBase
{
    /// <summary>Lista os aeroportos, para preencher os campos de origem e destino.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<AirportResponse>> List(CancellationToken ct) =>
        await db.Airports
            .AsNoTracking()
            .OrderBy(a => a.City)
            .Select(a => new AirportResponse(a.Code, a.Name, a.City, a.Country))
            .ToListAsync(ct);
}

[Route("api/flights")]
public sealed class FlightsController(FlightService flights) : ApiControllerBase
{
    /// <summary>
    /// Pesquisa voos diretos num dia. Para ida e volta, o front faz duas pesquisas
    /// (a segunda com origem e destino trocados e a data de regresso).
    /// </summary>
    /// <param name="from">Código do aeroporto de origem (ex.: OPO).</param>
    /// <param name="to">Código do aeroporto de destino (ex.: FNC).</param>
    /// <param name="date">Dia da partida, na hora local da origem (ex.: 2026-11-20).</param>
    /// <param name="passengers">Número de passageiros (1 a 9).</param>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<FlightSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FlightSummary>>> Search(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery, BindRequired] DateOnly date,
        CancellationToken ct,
        [FromQuery] int passengers = 1)
    {
        var result = await flights.SearchAsync(from, to, date, passengers, ct);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FlightSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlightSummary>> Get(Guid id, CancellationToken ct)
    {
        var flight = await flights.GetAsync(id, ct);
        return flight is null ? ToProblem(Errors.FlightNotFound) : flight;
    }

    /// <summary>Mapa de lugares do voo: todos os lugares, com a classe e se estão livres.</summary>
    [HttpGet("{id:guid}/seats")]
    [ProducesResponseType<SeatMapResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeatMapResponse>> Seats(Guid id, CancellationToken ct)
    {
        var seatMap = await flights.GetSeatMapAsync(id, ct);
        return seatMap is null ? ToProblem(Errors.FlightNotFound) : seatMap;
    }

    /// <summary>Cria um voo com os seus preços e todos os lugares do avião.</summary>
    [HttpPost]
    [ProducesResponseType<FlightSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FlightSummary>> Create(CreateFlightRequest request, CancellationToken ct)
    {
        var result = await flights.CreateAsync(request, ct);

        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }
}
