namespace Bilheteira.Api.Flights;

/// <summary>
/// Configuração dos lugares de um avião típico de corredor único (estilo A320).
/// Todos os voos do projeto usam este layout.
///
///   Business: filas 1–3, lugares A C | D F  (4 por fila, mais largos)
///   Economy:  filas 4–30, lugares A B C | D E F
/// </summary>
public static class SeatLayout
{
    public const int BusinessRows = 3;
    public const int LastRow = 30;

    private static readonly string[] BusinessLetters = ["A", "C", "D", "F"];
    private static readonly string[] EconomyLetters = ["A", "B", "C", "D", "E", "F"];

    public sealed record SeatDefinition(string SeatNumber, int Row, string Letter, CabinClass Class);

    public static IReadOnlyList<SeatDefinition> Seats { get; } = Build();

    // Declarado depois de Seats: os inicializadores estáticos correm pela ordem do ficheiro.
    private static readonly Dictionary<string, SeatDefinition> BySeatNumber =
        Seats.ToDictionary(s => s.SeatNumber);

    public static int CountOf(CabinClass cabin) => Seats.Count(s => s.Class == cabin);

    /// <summary>Procura um lugar pelo número (ex.: "12A"). Falso se não existir neste avião.</summary>
    public static bool TryGet(string seatNumber, out SeatDefinition seat) =>
        BySeatNumber.TryGetValue(seatNumber, out seat!);

    private static List<SeatDefinition> Build()
    {
        var seats = new List<SeatDefinition>();

        for (var row = 1; row <= LastRow; row++)
        {
            var isBusiness = row <= BusinessRows;
            var letters = isBusiness ? BusinessLetters : EconomyLetters;
            var cabin = isBusiness ? CabinClass.Business : CabinClass.Economy;

            seats.AddRange(letters.Select(letter => new SeatDefinition($"{row}{letter}", row, letter, cabin)));
        }

        return seats;
    }
}
