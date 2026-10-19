namespace Skylane.Api.Payments;

/// <summary>Pedido de cobrança enviado ao prestador de pagamentos.</summary>
/// <param name="IdempotencyKey">
/// Chave que identifica a tentativa. Se o mesmo pedido chegar duas vezes (retry de rede),
/// um prestador real usa-a para não cobrar a dobrar.
/// </param>
public sealed record PaymentRequest(
    string IdempotencyKey,
    decimal Amount,
    string Currency,
    string CardNumber,
    string CardHolder);

public sealed record PaymentResult(bool Approved, string? ProviderReference, string? DeclineCode)
{
    public static PaymentResult Success(string providerReference) => new(true, providerReference, null);
    public static PaymentResult Declined(string code) => new(false, null, code);
}

/// <summary>
/// Abstração do prestador de pagamentos (Stripe, Adyen, SIBS...).
/// O resto da aplicação só conhece esta interface: trocar o prestador falso por um real
/// é registar outra implementação no Program.cs, sem mexer na lógica das reservas.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);
}
