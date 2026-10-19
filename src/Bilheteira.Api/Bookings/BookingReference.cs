using System.Security.Cryptography;

namespace Bilheteira.Api.Bookings;

/// <summary>
/// Gera a referência da reserva, ao estilo das companhias aéreas: 6 caracteres (ex.: "K7M2Q9").
/// </summary>
public static class BookingReference
{
    // Sem 0/O e 1/I/L, para não haver confusões ao ditar o código ao telefone.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <remarks>
    /// 31^6 ≈ 887 milhões de combinações. Uma colisão é muito improvável e, se acontecer,
    /// o índice único na base de dados impede que duas reservas fiquem com o mesmo código.
    /// </remarks>
    public static string Generate() =>
        new(RandomNumberGenerator.GetItems<char>(Alphabet, 6));

    public static string Normalize(string reference) => reference.Trim().ToUpperInvariant();
}
