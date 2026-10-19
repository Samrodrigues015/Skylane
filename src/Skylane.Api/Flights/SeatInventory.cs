using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Flights;

/// <summary>
/// Cria as linhas de FlightSeats (uma por lugar) para os voos que ainda não as têm.
///
/// Porquê SQL em vez de EF? Com 30 dias de voos são ~250 000 lugares. Inserir isso
/// objeto a objeto pelo EF demoraria minutos; um único INSERT ... SELECT com
/// CROSS JOIN faz tudo dentro do SQL Server em poucos segundos.
/// </summary>
public static class SeatInventory
{
    /// <param name="flightId">Se indicado, cria só os lugares desse voo; senão, de todos os voos sem lugares.</param>
    public static Task<int> CreateMissingSeatsAsync(SkylaneDbContext db, Guid? flightId, CancellationToken ct)
    {
        // A lista de lugares vem do SeatLayout (constantes do código), não do utilizador,
        // por isso pode ir diretamente no SQL sem risco de injeção.
        var layout = string.Join(",\n        ", SeatLayout.Seats.Select(s =>
            $"('{s.SeatNumber}', {s.Row}, '{s.Letter}', '{s.Class}')"));

        var onlyThisFlight = flightId is null ? "" : "AND f.Id = {0}";

        var sql = $"""
            INSERT INTO FlightSeats (FlightId, SeatNumber, [Row], Letter, Class, Status)
            SELECT f.Id, l.SeatNumber, l.[Row], l.Letter, l.Class, 'Available'
            FROM Flights AS f
            CROSS JOIN (VALUES
                    {layout}
                ) AS l (SeatNumber, [Row], Letter, Class)
            WHERE NOT EXISTS (SELECT 1 FROM FlightSeats AS s WHERE s.FlightId = f.Id)
              {onlyThisFlight};
            """;

        // O {0} é convertido pelo EF num parâmetro SQL (@p0), nunca concatenado.
        return flightId is null
            ? db.Database.ExecuteSqlRawAsync(sql, ct)
            : db.Database.ExecuteSqlRawAsync(sql, new object[] { flightId.Value }, ct);
    }
}
