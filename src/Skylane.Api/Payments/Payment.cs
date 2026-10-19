namespace Skylane.Api.Payments;

public enum PaymentStatus
{
    Approved,
    Declined,

    /// <summary>Cobrado, mas a reserva expirou entretanto: o valor é devolvido.</summary>
    Refunded
}

/// <summary>
/// Uma tentativa de pagamento de uma reserva de voo. Guardamos todas, aprovadas ou não,
/// porque é o histórico que o apoio ao cliente consulta ("o meu cartão foi recusado?").
/// </summary>
public sealed class Payment
{
    public Guid Id { get; init; }
    /// <summary>O que foi pago: uma reserva de voos (com os eventos juntos) ou só bilhetes de um evento.</summary>
    public Guid? BookingId { get; init; }
    public Guid? ReservationId { get; init; }

    public decimal Amount { get; init; }
    public required string Currency { get; init; }
    public PaymentStatus Status { get; set; }

    /// <summary>Só os últimos 4 dígitos. Guardar o número completo obrigaria a cumprir a norma PCI DSS.</summary>
    public required string CardLast4 { get; init; }

    public string? ProviderReference { get; init; }
    public string? DeclineCode { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
