namespace Skylane.Api.Common;

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

    // ---------- Voos ----------

    public static readonly Error FlightNotFound =
        new(ErrorType.NotFound, "flight_not_found", "O voo não existe.");

    public static readonly Error FlightNotBookable =
        new(ErrorType.Conflict, "flight_not_bookable", "Este voo já não aceita reservas.");

    public static readonly Error SameAirports =
        new(ErrorType.Validation, "same_airports", "A origem e o destino têm de ser aeroportos diferentes.");

    public static readonly Error AirlineNotFound =
        new(ErrorType.Validation, "airline_not_found", "A companhia aérea não existe.");

    public static Error AirportNotFound(string code) =>
        new(ErrorType.Validation, "airport_not_found", $"O aeroporto '{code}' não existe.");

    public static Error InvalidPassengerCount(int max) =>
        new(ErrorType.Validation, "invalid_passenger_count", $"O número de passageiros tem de estar entre 1 e {max}.");

    // ---------- Reservas de voo ----------

    public static readonly Error BookingNotFound =
        new(ErrorType.NotFound, "booking_not_found", "A reserva não existe.");

    public static readonly Error BookingExpired =
        new(ErrorType.Gone, "booking_expired", "A reserva expirou e os lugares foram libertados.");

    public static readonly Error SeatsMustMatchPassengers =
        new(ErrorType.Validation, "seats_must_match_passengers", "Escolhe um lugar por passageiro em cada voo.");

    public static readonly Error DuplicateSeats =
        new(ErrorType.Validation, "duplicate_seats", "O mesmo lugar foi escolhido mais do que uma vez.");

    public static readonly Error InvalidReturnFlight =
        new(ErrorType.Validation, "invalid_return_flight",
            "O voo de regresso tem de fazer o percurso inverso e partir depois da chegada do voo de ida.");

    public static Error InvalidSeat(string seat) =>
        new(ErrorType.Validation, "invalid_seat", $"O lugar '{seat}' não existe neste avião.");

    public static Error SeatUnavailable(string seat) =>
        new(ErrorType.Conflict, "seat_unavailable", $"O lugar {seat} já não está disponível. Escolhe outro.");
}
