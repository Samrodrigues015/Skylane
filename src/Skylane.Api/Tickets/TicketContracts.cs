namespace Skylane.Api.Tickets;

public sealed record TicketResponse(
    string Code,
    string EventName,
    string Venue,
    DateTimeOffset StartsAt,
    DateTimeOffset? CheckedInAt);
