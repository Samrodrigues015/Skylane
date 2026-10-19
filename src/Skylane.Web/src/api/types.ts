// Tipos que espelham os contratos da API (ficheiros *Contracts.cs).

export type CabinClass = "Economy" | "Business";

export interface Airport {
  code: string;
  name: string;
  city: string;
  country: string;
}

export interface AirportInfo {
  code: string;
  city: string;
}

export interface FareInfo {
  class: CabinClass;
  price: number;
  seatsLeft: number;
}

/** Horas em hora local de cada aeroporto, com offset (ex.: "2026-10-24T07:35:00+01:00"). */
export interface FlightSummary {
  id: string;
  number: string;
  airline: string;
  origin: AirportInfo;
  destination: AirportInfo;
  departsAt: string;
  arrivesAt: string;
  durationMinutes: number;
  fares: FareInfo[];
}

export interface SeatInfo {
  seatNumber: string;
  row: number;
  letter: string;
  class: CabinClass;
  available: boolean;
}

export interface SeatMapResponse {
  flight: FlightSummary;
  seats: SeatInfo[];
}

export interface CalendarDay {
  date: string;
  minPrice: number | null;
}

export type DepartureStatus = "OnTime" | "Boarding" | "LastSeats";

export interface DepartureInfo {
  flightId: string;
  number: string;
  airline: string;
  destination: AirportInfo;
  departsAt: string;
  seatsLeft: number;
  status: DepartureStatus;
}

export interface RouteDeal {
  origin: AirportInfo;
  destination: AirportInfo;
  minPrice: number;
  durationMinutes: number;
}

export type BookingStatus = "Pending" | "Confirmed" | "Expired";
export type LegDirection = "Outbound" | "Return";

export interface BookedSeat {
  passenger: string;
  seatNumber: string;
  class: CabinClass;
  price: number;
}

export interface BookingLeg {
  direction: LegDirection;
  flightId: string;
  flightNumber: string;
  origin: AirportInfo;
  destination: AirportInfo;
  departsAt: string;
  arrivesAt: string;
  seats: BookedSeat[];
}

export interface BookingResponse {
  reference: string;
  status: BookingStatus;
  contactEmail: string;
  total: number;
  currency: string;
  expiresAt: string;
  passengers: string[];
  legs: BookingLeg[];
  /** Eventos juntos à viagem (já incluídos no total). */
  events: BookedEvent[];
}

export interface PassengerRequest {
  firstName: string;
  lastName: string;
  documentNumber: string;
}

/** O carrinho: voos, bilhetes para eventos ou os dois. */
export interface CreateBookingRequest {
  contactEmail: string;
  passengers?: PassengerRequest[];
  outbound?: { flightId: string; seats: string[] };
  return?: { flightId: string; seats: string[] };
  events?: { eventId: string; quantity: number }[];
}

export interface CardPayment {
  cardNumber: string;
  cardHolder: string;
  expiry: string;
  cvc: string;
}

// ---------- Eventos ----------

export type EventCategory =
  | "Concert"
  | "Fado"
  | "Jazz"
  | "Classical"
  | "Opera"
  | "Flamenco"
  | "Guitar"
  | "Electronic"
  | "Festival"
  | "LightFestival"
  | "Comedy"
  | "Cinema"
  | "Food"
  | "Flowers"
  | "Fireworks";

export interface EventResponse {
  id: string;
  name: string;
  category: EventCategory;
  /** No idioma do site (a API usa o Accept-Language). */
  description: string | null;
  venue: string;
  /** Aeroporto da cidade do evento (ex.: "AMS"). */
  airportCode: string;
  city: string;
  /** Hora local da cidade do evento. */
  startsAt: string;
  capacity: number;
  available: number;
  price: number;
  currency: string;
}

export type ReservationStatus = "Pending" | "Confirmed" | "Expired" | "Cancelled";

export interface ReservationResponse {
  id: string;
  eventId: string;
  eventName: string;
  venue: string;
  city: string;
  startsAt: string;
  quantity: number;
  unitPrice: number;
  total: number;
  currency: string;
  status: ReservationStatus;
  expiresAt: string;
  /** Se os bilhetes fazem parte de uma viagem. */
  bookingReference: string | null;
  tickets: string[];
}

/** Bilhetes de um evento juntos à viagem. */
export interface BookedEvent {
  reservationId: string;
  eventId: string;
  name: string;
  category: EventCategory;
  venue: string;
  city: string;
  startsAt: string;
  quantity: number;
  unitPrice: number;
  total: number;
  status: ReservationStatus;
  tickets: string[];
}
