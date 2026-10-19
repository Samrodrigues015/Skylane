using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Skylane.Api.Payments;

public sealed class PaymentOptions
{
    public const string Section = "Payments";

    /// <summary>Atraso simulado da resposta do "banco", para o front mostrar o estado de processamento.</summary>
    public int SimulatedDelayMs { get; set; } = 800;
}

/// <summary>
/// Prestador de pagamentos falso, com cartões de teste ao estilo do Stripe:
///
///   4242 4242 4242 4242  → aprovado
///   4000 0000 0000 0002  → recusado pelo banco
///   4000 0000 0000 9995  → saldo insuficiente
///
/// Qualquer outro número válido (algoritmo de Luhn) é aprovado.
/// Nenhum dinheiro é movimentado e o número do cartão nunca é guardado.
/// </summary>
public sealed class FakePaymentGateway(IOptions<PaymentOptions> options) : IPaymentGateway
{
    public const string DeclinedCard = "4000000000000002";
    public const string InsufficientFundsCard = "4000000000009995";

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct)
    {
        if (options.Value.SimulatedDelayMs > 0)
        {
            await Task.Delay(options.Value.SimulatedDelayMs, ct);
        }

        return request.CardNumber switch
        {
            DeclinedCard => PaymentResult.Declined("card_declined"),
            InsufficientFundsCard => PaymentResult.Declined("insufficient_funds"),
            _ => PaymentResult.Success($"pay_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}")
        };
    }

    /// <summary>
    /// Algoritmo de Luhn: o último dígito de um cartão é um dígito de controlo.
    /// Apanha erros de digitação antes de chegar ao "banco".
    /// </summary>
    public static bool IsValidCardNumber(string digits)
    {
        if (digits.Length is < 12 or > 19 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        var doubleIt = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }
}
