import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import EventImage from "./EventImage";
import { api } from "../api/client";
import type { BookingResponse, EventResponse } from "../api/types";
import { errorMessage } from "../lib/errors";
import { addDays, clockTime, dayNumber, isoDate, money, monthShort, shortDate } from "../lib/format";
import { MinusIcon, PlusIcon } from "./Icons";
import styles from "./TripEvents.module.css";

interface Props {
  booking: BookingResponse;
  onChange: (booking: BookingResponse) => void;
}

/**
 * "Junta à tua viagem": eventos no destino durante a estadia, que se podem acrescentar
 * à reserva antes de pagar. Ficam presos com os lugares do avião e paga-se tudo de uma vez.
 */
export default function TripEvents({ booking, onChange }: Props) {
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

  const added = new Set(booking.events.map((e) => e.eventId));
  const available = (events.data ?? []).filter((e) => !added.has(e.id) && e.available > 0);

  if (available.length === 0) return null;

  return (
    <section className={styles.section}>
      <div className={styles.head}>
        <span>{outbound.destination.city}</span>
        <h2>{t("tripEvents.title")}</h2>
        <p>{t("tripEvents.lead")}</p>
      </div>
      <div className={styles.list}>
        {available.map((ev) => (
          <EventRow
            key={ev.id}
            event={ev}
            reference={booking.reference}
            passengers={booking.passengers.length}
            onAdded={onChange}
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
  onAdded: (booking: BookingResponse) => void;
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

  return (
    <article className={styles.row}>
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
        {t("tripEvents.add")}
      </button>
    </article>
  );
}
