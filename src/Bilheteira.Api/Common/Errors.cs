namespace Bilheteira.Api.Common;

public static class Errors
{
    public static readonly Error EventNotFound =
        new(ErrorType.NotFound, "event_not_found", "O evento não existe.");

    public static readonly Error SoldOut =
        new(ErrorType.Conflict, "sold_out", "Não há lugares suficientes para esta reserva.");

    public static readonly Error ReservationNotFound =
        new(ErrorType.NotFound, "reservation_not_found", "A reserva não existe.");

    public static readonly Error ReservationExpired =
        new(ErrorType.Gone, "reservation_expired", "A reserva expirou e os lugares foram libertados.");

    public static readonly Error TicketNotFound =
        new(ErrorType.NotFound, "ticket_not_found", "O bilhete não existe.");

    public static readonly Error TicketAlreadyUsed =
        new(ErrorType.Conflict, "ticket_already_used", "Este bilhete já foi validado.");

    public static Error InvalidQuantity(int max) =>
        new(ErrorType.Validation, "invalid_quantity", $"A quantidade tem de estar entre 1 e {max}.");
}
