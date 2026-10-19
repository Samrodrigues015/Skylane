namespace Bilheteira.Api.Flights;

/// <summary>Companhia aérea. No projeto usamos companhias fictícias.</summary>
public sealed class Airline
{
    /// <summary>Código de 2 letras (ex.: "AT"), usado no número do voo ("AT1947").</summary>
    public required string Code { get; init; }

    public required string Name { get; init; }
}
