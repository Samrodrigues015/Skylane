import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { BookingResponse } from "../api/types";
import { clockTime, isoDate, money, shortDate } from "../lib/format";
import styles from "./BookingSummary.module.css";

/** Resumo de uma reserva já criada (dados vindos da API, com os preços finais). */
interface Props {
  booking: BookingResponse;
  /** Se indicado, cada evento tem um botão para o tirar da viagem (só antes de pagar). */
  onRemoveEvent?: (reservationId: string) => void;
  children?: ReactNode;
}

export default function BookingSummary({ booking, onRemoveEvent, children }: Props) {
  const { t } = useTranslation();

  return (
    <aside className={`panel ${styles.summary}`}>
      <div className={styles.head}>
        <h2>{booking.legs.length > 0 ? t("summary.title") : t("cart.orderTitle")}</h2>
        <span className={styles.ref}>
          {t("payment.reference")} <b>{booking.reference}</b>
        </span>
      </div>

      {booking.legs.map((leg) => (
        <div key={leg.flightId} className={styles.leg}>
          <small>
            {t(leg.direction === "Outbound" ? "summary.outbound" : "summary.return")} · {shortDate(isoDate(leg.departsAt))}
          </small>
          <b>
            {leg.origin.code} {clockTime(leg.departsAt)} → {leg.destination.code} {clockTime(leg.arrivesAt)}
          </b>
          <span className={styles.flight}>
            {leg.flightNumber} · {leg.origin.city} → {leg.destination.city}
          </span>
          <ul className={styles.seats}>
            {leg.seats.map((s) => (
              <li key={s.seatNumber}>
                <span>{s.passenger}</span>
                <span className={styles.seat}>{s.seatNumber}</span>
                <span className={styles.price}>{money(s.price, booking.currency)}</span>
              </li>
            ))}
          </ul>
        </div>
      ))}

      {booking.events.length > 0 && (
        <div className={styles.leg}>
          <small>{t("tripEvents.inTrip")}</small>
          <ul className={styles.events}>
            {booking.events.map((ev) => (
              <li key={ev.reservationId}>
                <span className={styles.eventName}>
                  <b>{ev.name}</b>
                  <span>
                    {shortDate(isoDate(ev.startsAt))} · {clockTime(ev.startsAt)} · {t("tripEvents.tickets", { count: ev.quantity })}
                  </span>
                </span>
                <span className={styles.price}>{money(ev.total, booking.currency)}</span>
                {onRemoveEvent && ev.status === "Pending" && (
                  <button
                    type="button"
                    className={styles.remove}
                    aria-label={t("tripEvents.remove", { name: ev.name })}
                    onClick={() => onRemoveEvent(ev.reservationId)}
                  >
                    ×
                  </button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className={styles.total}>
        <span>
          {booking.passengers.length > 0 ? t("summary.totalFor", { count: booking.passengers.length }) : t("summary.total")}
        </span>
        <b>{money(booking.total, booking.currency)}</b>
      </div>

      {children}
    </aside>
  );
}
