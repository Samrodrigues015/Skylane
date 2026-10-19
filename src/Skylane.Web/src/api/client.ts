import type {
  Airport,
  BookingResponse,
  CalendarDay,
  CreateBookingRequest,
  DepartureInfo,
  EventResponse,
  FlightSummary,
  CardPayment,
  ReservationResponse,
  RouteDeal,
  SeatMapResponse,
} from "./types";

/**
 * Erro devolvido pela API no formato ProblemDetails.
 * O `code` (ex.: "seat_unavailable") é estável e é o que o front traduz para PT ou EN;
 * o `title` vem em português da API e só é usado se não houver tradução.
 */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    title: string,
  ) {
    super(title);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;

  try {
    response = await fetch(path, {
      ...init,
      headers: { "Content-Type": "application/json", ...init?.headers },
    });
  } catch {
    throw new ApiError(0, "network_error", "Network error");
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    // Erros de validação automáticos do ASP.NET não trazem "code".
    const code = body?.code ?? (response.status === 400 ? "validation" : "unexpected");
    throw new ApiError(response.status, code, body?.title ?? response.statusText);
  }

  return (await response.json()) as T;
}

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

export const api = {
  airports: () => request<Airport[]>("/api/airports"),
  departures: (code: string, limit = 6) =>
    request<DepartureInfo[]>(`/api/airports/${code}/departures?limit=${limit}`),
  deals: (limit = 4) => request<RouteDeal[]>(`/api/flights/deals?limit=${limit}`),

  searchFlights: (from: string, to: string, date: string, passengers: number) =>
    request<FlightSummary[]>(
      `/api/flights/search?from=${from}&to=${to}&date=${date}&passengers=${passengers}`,
    ),
  calendar: (from: string, to: string, start: string, days: number, passengers: number) =>
    request<CalendarDay[]>(
      `/api/flights/calendar?from=${from}&to=${to}&start=${start}&days=${days}&passengers=${passengers}`,
    ),
  seatMap: (flightId: string) => request<SeatMapResponse>(`/api/flights/${flightId}/seats`),

  createBooking: (body: CreateBookingRequest) => post<BookingResponse>("/api/bookings", body),
  getBooking: (reference: string) =>
    request<BookingResponse>(`/api/bookings/${encodeURIComponent(reference)}`),
  payBooking: (reference: string, body: CardPayment) =>
    post<BookingResponse>(`/api/bookings/${encodeURIComponent(reference)}/pay`, body),

  events: (filter: { city?: string; from?: string; to?: string; limit?: number } = {}) => {
    const q = new URLSearchParams();
    Object.entries(filter).forEach(([key, value]) => value !== undefined && q.set(key, String(value)));
    const query = q.toString();
    return request<EventResponse[]>(`/api/events${query ? `?${query}` : ""}`);
  },
  reserveEvent: (eventId: string, quantity: number) =>
    post<ReservationResponse>(`/api/events/${eventId}/reservations`, { quantity }),
  payReservation: (reservationId: string, card: CardPayment) =>
    post<ReservationResponse>(`/api/reservations/${reservationId}/pay`, card),
  addBookingEvent: (reference: string, eventId: string, quantity: number) =>
    post<BookingResponse>(`/api/bookings/${encodeURIComponent(reference)}/events`, { eventId, quantity }),
  removeBookingEvent: (reference: string, reservationId: string) =>
    request<BookingResponse>(`/api/bookings/${encodeURIComponent(reference)}/events/${reservationId}`, { method: "DELETE" }),
};
