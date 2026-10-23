import { useEffect } from "react";
import type { BookingResponse } from "../api/types";

/*
 * Reservas feitas ou consultadas neste browser. Sem login, é o que permite voltar a
 * "As minhas reservas" e encontrar a reserva sem ter de escrever outra vez a referência.
 * Só se guarda a referência e um resumo para mostrar na lista; os dados vêm sempre da API.
 */
export interface RecentBooking {
  reference: string;
  /** Ex.: "Lisboa → Funchal" ou o nome do evento, se for só bilhetes. */
  title: string;
  /** Data da ida ou do primeiro evento (ISO). */
  date: string;
  savedAt: number;
}

const KEY = "skylane.bookings";
const MAX = 5;
const KEEP_DAYS = 60;

export function recentBookings(): RecentBooking[] {
  try {
    const list = JSON.parse(localStorage.getItem(KEY) ?? "[]") as RecentBooking[];
    const limit = Date.now() - KEEP_DAYS * 86_400_000;
    return list.filter((b) => b.savedAt > limit);
  } catch {
    return [];
  }
}

function save(list: RecentBooking[]) {
  try {
    localStorage.setItem(KEY, JSON.stringify(list));
  } catch {
    // Modo privado ou armazenamento cheio: a página funciona na mesma, só não se lembra.
  }
}

export function rememberBooking(booking: BookingResponse) {
  const outbound = booking.legs[0];
  const firstEvent = booking.events[0];
  const entry: RecentBooking = {
    reference: booking.reference,
    title: outbound ? `${outbound.origin.city} → ${outbound.destination.city}` : (firstEvent?.name ?? booking.reference),
    date: outbound?.departsAt ?? firstEvent?.startsAt ?? "",
    savedAt: Date.now(),
  };
  save([entry, ...recentBookings().filter((b) => b.reference !== booking.reference)].slice(0, MAX));
}

export function forgetBooking(reference: string) {
  save(recentBookings().filter((b) => b.reference !== reference));
}

/** Guarda a reserva na lista assim que estiver carregada. */
export function useRememberBooking(booking?: BookingResponse) {
  useEffect(() => {
    if (booking) rememberBooking(booking);
  }, [booking]);
}
