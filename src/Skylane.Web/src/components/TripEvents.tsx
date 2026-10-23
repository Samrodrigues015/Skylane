import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import EventImage from "./EventImage";
import { api } from "../api/client";
import type { BookedEvent, BookingResponse, EventResponse } from "../api/types";
import { errorMessage } from "../lib/errors";
import { addDays, clockTime, dayNumber, isoDate, money, monthShort, shortDate } from "../lib/format";
import { CheckIcon, MinusIcon, PlusIcon } from "./Icons";
import styles from "./TripEvents.module.css";

interface Props {
  booking: BookingResponse;
  onChange: (booking: BookingResponse) => void;
  /** Tirar um evento já adicionado (o mesmo que o × do resumo). */
  onRemove: (reservationId: string) => void;
  removing?: boolean;
}

/**
 * "Junta à tua viagem": eventos no destino durante a estadia, que se podem acrescentar
 * à reserva antes de pagar. Ficam presos com os lugares do avião e paga-se tudo de uma vez.
 */
export default function TripEvents({ booking, onChange, onRemove, removing }: Props) {
  const { t } = useTranslation();

  const outbound = booking.legs.find((l) => l.direction === "Outbound") ?? booking.legs[0];
  const inbound = booking.legs.find((l) => l.direction === "Return");
  const city = outbound.destination.code;
  const from = isoDate(outbound.arrivesAt);
  const to = inbound ? isoDate(inbound.departsAt) : addDays(from, 7);

  const events = useQuery({
    queryKey: ["trip-events", city, from, to],
    queryFn: () => api.events({ city, from, to, limit: 4 }),
  });

  // Os eventos adicionados continuam na lista, marcados, como num carrinho normal.
  const inTrip = new Map(booking.events.map((e) => [e.eventId, e]));
  const visible = (events.data ?? []).filter((e) => inTrip.has(e.id) || e.available > 0);

  if (visible.length === 0) return null;

  return (
    <section className={styles.section}>
      <div className={styles.head}>
        <span>{outbound.destination.city}</span>
        <h2>{t("tripEvents.title")}</h2>
        <p>{t("tripEvents.lead")}</p>
      </div>
      <div className={styles.list}>
        {visible.map((ev) => (
          <EventRow
            key={ev.id}
            event={ev}
            reference={booking.reference}
            passengers={booking.passengers.length}
            inTrip={inTrip.get(ev.id)}
            onAdded={onChange}
            onRemove={onRemove}
            removing={removing}
          />
        ))}
      </div>
    </section>
  );
}

function EventRow(props: {
  event: EventResponse;
  reference: string;
  passengers: number;
  inTrip?: BookedEvent;
  onAdded: (booking: BookingResponse) => void;
  onRemove: (reservationId: string) => void;
  removing?: boolean;
}) {
  const { t } = useTranslation();
  const { event } = props;
  const max = Math.min(10, event.available);
  const [quantity, setQuantity] = useState(Math.min(props.passengers, max));
  const day = isoDate(event.startsAt);

  const add = useMutation({
    mutationFn: () => api.addBookingEvent(props.reference, event.id, quantity),
    onSuccess: props.onAdded,
  });

  const { inTrip } = props;

  return (
    <article className={`${styles.row} ${inTrip ? styles.added : ""}`}>
      <div className={styles.date}>
        <EventImage className={styles.thumb} category={event.category} seed={event.name} width={120} />
        <b>{dayNumber(day)}</b>
        <small>{monthShort(day)}</small>
      </div>
      <div className={styles.info}>
        <h3>{event.name}</h3>
        <span>
          {shortDate(day)} · {clockTime(event.startsAt)} · {event.venue}
        </span>
        {add.isError && <span className={styles.error}>{errorMessage(add.error, t)}</span>}
      </div>

      {inTrip ? (
        <>
          <span className={styles.count}>{t("tripEvents.tickets", { count: inTrip.quantity })}</span>
          <div className={styles.price}>
            <b>{money(inTrip.total, event.currency)}</b>
            <small>{t("tripEvents.each", { price: money(inTrip.unitPrice, event.currency) })}</small>
          </div>
          <div className={styles.addedActions} aria-live="polite">
            <span className={styles.addedBadge}>
              <CheckIcon /> {t("tripEvents.added")}
            </span>
            <button type="button" disabled={props.removing} onClick={() => props.onRemove(inTrip.reservationId)}>
              {t("tripEvents.undo")}
            </button>
          </div>
        </>
      ) : (
        <>
          <div className={styles.stepper}>
            <button type="button" aria-label="-" disabled={quantity <= 1} onClick={() => setQuantity(quantity - 1)}>
              <MinusIcon />
            </button>
            <b aria-live="polite">{quantity}</b>
            <button type="button" aria-label="+" disabled={quantity >= max} onClick={() => setQuantity(quantity + 1)}>
              <PlusIcon />
            </button>
          </div>
          <div className={styles.price}>
            <b>{money(event.price * quantity, event.currency)}</b>
            <small>{t("tripEvents.each", { price: money(event.price, event.currency) })}</small>
          </div>
          <button type="button" className="btn btn-blue" disabled={add.isPending} onClick={() => add.mutate()}>
            {add.isPending ? t("tripEvents.adding") : t("tripEvents.add")}
          </button>
        </>
      )}
    </article>
  );
}
