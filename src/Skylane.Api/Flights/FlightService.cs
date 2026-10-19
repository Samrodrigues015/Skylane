using Skylane.Api.Common;
using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Flights;

public sealed class FlightService(SkylaneDbContext db, TimeProvider time)
{
    public const int MaxPassengersPerSearch = 9;

    /// <summary>
    /// Voos de <paramref name="from"/> para <paramref name="to"/> num dia, com lugares
    /// suficientes para <paramref name="passengers"/> pessoas em pelo menos uma classe.
    /// </summary>
    /// <param name="date">Dia na hora local do aeroporto de origem (é assim que o cliente pensa).</param>
    public async Task<Result<IReadOnlyList<FlightSummary>>> SearchAsync(
        string from, string to, DateOnly date, int passengers, CancellationToken ct)
    {
        from = from.Trim().ToUpperInvariant();
        to = to.Trim().ToUpperInvariant();

        if (from == to)
        {
            return Errors.SameAirports;
        }

        if (passengers is < 1 or > MaxPassengersPerSearch)
        {
            return Errors.InvalidPassengerCount(MaxPassengersPerSearch);
        }

        var origin = await db.Airports.AsNoTracking().FirstOrDefaultAsync(a => a.Code == from, ct);
        if (origin is null)
        {
            return Errors.AirportNotFound(from);
        }

        if (!await db.Airports.AnyAsync(a => a.Code == to, ct))
        {
            return Errors.AirportNotFound(to);
        }

        // "Dia 20 no Porto" vai da meia-noite de dia 20 à meia-noite de dia 21, hora de Lisboa.
        // Convertemos esses dois limites para UTC, porque é assim que as partidas estão guardadas.
        var dayStart = AirportTime.FromLocal(date.ToDateTime(TimeOnly.MinValue), origin.TimeZoneId);
        var dayEnd = AirportTime.FromLocal(date.AddDays(1).ToDateTime(TimeOnly.MinValue), origin.TimeZoneId);
        var now = time.GetUtcNow();

        var query = db.Flights.Where(f =>
            f.OriginCode == from &&
            f.DestinationCode == to &&
            f.Status == FlightStatus.Scheduled &&
            f.DepartsAt >= dayStart &&
            f.DepartsAt < dayEnd &&
            f.DepartsAt > now);

        var flights = await LoadSummariesAsync(query, ct);

        var available = flights
            .Where(f => f.Fares.Any(fare => fare.SeatsLeft >= passengers))
            .ToList();

        return Result<IReadOnlyList<FlightSummary>>.Success(available);
    }

    public async Task<FlightSummary?> GetAsync(Guid id, CancellationToken ct) =>
        (await LoadSummariesAsync(db.Flights.Where(f => f.Id == id), ct)).SingleOrDefault();

    /// <summary>Todos os lugares do voo, para o front desenhar o mapa do avião.</summary>
    public async Task<SeatMapResponse?> GetSeatMapAsync(Guid id, CancellationToken ct)
    {
        var flight = await GetAsync(id, ct);
        if (flight is null)
        {
            return null;
        }

        var seats = await db.FlightSeats
            .AsNoTracking()
            .Where(s => s.FlightId == id)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Letter)
            .Select(s => new SeatInfo(s.SeatNumber, s.Row, s.Letter, s.Class, s.Status == SeatStatus.Available))
            .ToListAsync(ct);

        return new SeatMapResponse(flight, seats);
    }

    public async Task<Result<FlightSummary>> CreateAsync(CreateFlightRequest request, CancellationToken ct)
    {
        var origin = request.Origin.Trim().ToUpperInvariant();
        var destination = request.Destination.Trim().ToUpperInvariant();
        var airline = request.AirlineCode.Trim().ToUpperInvariant();

        if (origin == destination)
        {
            return Errors.SameAirports;
        }

        foreach (var code in new[] { origin, destination })
        {
            if (!await db.Airports.AnyAsync(a => a.Code == code, ct))
            {
                return Errors.AirportNotFound(code);
            }
        }

        if (!await db.Airlines.AnyAsync(a => a.Code == airline, ct))
        {
            return Errors.AirlineNotFound;
        }

        var departsAt = request.DepartsAt!.Value.ToUniversalTime();
        var flight = new Flight
        {
            Id = Guid.CreateVersion7(),
            Number = request.Number.Trim().ToUpperInvariant(),
            AirlineCode = airline,
            OriginCode = origin,
            DestinationCode = destination,
            DepartsAt = departsAt,
            ArrivesAt = departsAt.AddMinutes(request.DurationMinutes),
            Status = FlightStatus.Scheduled,
            Fares =
            [
                new FlightFare { Class = CabinClass.Economy, Price = request.EconomyPrice },
                new FlightFare { Class = CabinClass.Business, Price = request.BusinessPrice }
            ]
        };

        // O voo e os seus lugares são criados juntos: ou tudo, ou nada.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Flights.Add(flight);
        await db.SaveChangesAsync(ct);
        await SeatInventory.CreateMissingSeatsAsync(db, flight.Id, ct);

        await transaction.CommitAsync(ct);

        return (await GetAsync(flight.Id, ct))!;
    }

    /// <summary>
    /// Carrega voos com os preços e os lugares livres por classe, numa só query,
    /// e converte as horas para a hora local de cada aeroporto.
    /// </summary>
    private static async Task<List<FlightSummary>> LoadSummariesAsync(IQueryable<Flight> query, CancellationToken ct)
    {
        var rows = await query
            .AsNoTracking()
            .OrderBy(f => f.DepartsAt)
            .Select(f => new
            {
                f.Id,
                f.Number,
                Airline = f.Airline.Name,
                OriginCode = f.Origin.Code,
                OriginCity = f.Origin.City,
                OriginTimeZone = f.Origin.TimeZoneId,
                DestinationCode = f.Destination.Code,
                DestinationCity = f.Destination.City,
                DestinationTimeZone = f.Destination.TimeZoneId,
                f.DepartsAt,
                f.ArrivesAt,
                Fares = f.Fares.Select(fare => new
                {
                    fare.Class,
                    fare.Price,
                    // Subquery correlacionada: o SQL Server conta os lugares livres de cada classe.
                    SeatsLeft = f.Seats.Count(s => s.Class == fare.Class && s.Status == SeatStatus.Available)
                }).ToList()
            })
            .ToListAsync(ct);

        // A conversão de fuso horário é feita em C#, porque não tem tradução para SQL.
        return rows.Select(r => new FlightSummary(
                r.Id,
                r.Number,
                r.Airline,
                new AirportInfo(r.OriginCode, r.OriginCity),
                new AirportInfo(r.DestinationCode, r.DestinationCity),
                AirportTime.ToLocal(r.DepartsAt, r.OriginTimeZone),
                AirportTime.ToLocal(r.ArrivesAt, r.DestinationTimeZone),
                (int)(r.ArrivesAt - r.DepartsAt).TotalMinutes,
                r.Fares
                    .OrderBy(fare => fare.Class)
                    .Select(fare => new FareInfo(fare.Class, fare.Price, fare.SeatsLeft))
                    .ToList()))
            .ToList();
    }
}
