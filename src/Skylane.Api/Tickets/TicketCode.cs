using System.Security.Cryptography;

namespace Skylane.Api.Tickets;

/// <summary>
/// Gera códigos de bilhete legíveis, no formato "XXXXX-XXXXX".
/// </summary>
public static class TicketCode
{
    // Sem 0/O, 1/I/L: letras que se confundem quando alguém lê o código em voz alta
    // ou o escreve à mão na entrada.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int GroupLength = 5;

    public static string Generate()
    {
        // RandomNumberGenerator e não Random: os códigos não podem ser adivinháveis.
        var chars = RandomNumberGenerator.GetItems<char>(Alphabet, GroupLength * 2);
        return $"{new string(chars, 0, GroupLength)}-{new string(chars, GroupLength, GroupLength)}";
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
