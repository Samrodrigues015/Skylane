import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import type { CardPayment, EventResponse, ReservationResponse } from "../api/types";
import CardForm from "../components/CardForm";
import { AlertIcon, ClockIcon, LockIcon, MinusIcon, PlusIcon } from "../components/Icons";
import { errorMessage } from "../lib/errors";
import { clockTime, dayNumber, isoDate, longDate, money, monthShort } from "../lib/format";
import { useCountdown } from "../lib/useCountdown";
import styles from "./EventsPage.module.css";

const MAX_TICKETS = 6;

export default function EventsPage() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const city = params.get("city") ?? undefined;

  const airports = useQuery({ queryKey: ["airports"], queryFn: api.airports, staleTime: Infinity });
  const events = useQuery({ queryKey: ["events", city], queryFn: () => api.events({ city }) });
  const [selected, setSelected] = useState<EventResponse | null>(null);

  // Vindo de "Em Amesterdão durante a tua viagem": abre logo a compra do evento escolhido.
  const eventParam = params.get("event");
  useEffect(() => {
    if (!eventParam || !events.data) return;
    const ev = events.data.find((e) => e.id === eventParam);
    if (ev) setSelected(ev);
    params.delete("event");
    setParams(params, { replace: true });
  }, [eventParam, events.data, params, setParams]);

  const chooseCity = (code?: string) => setParams(code ? { city: code } : {});

  return (
    <main className="wrap page">
      <h1 className="page-title">{t("events.title")}</h1>
      <p className="page-lead">{t("events.lead")}</p>

      <div className={styles.cities} role="group" aria-label={t("events.allCities")}>
        <button type="button" aria-pressed={!city} onClick={() => chooseCity()}>
          {t("events.allCities")}
        </button>
        {[...(airports.data ?? [])]
          .sort((a, b) => a.city.localeCompare(b.city))
          .map((a) => (
            <button key={a.code} type="button" aria-pressed={city === a.code} onClick={() => chooseCity(a.code)}>
              {a.city}
            </button>
          ))}
      </div>

      {events.isLoading && <p>{t("common.loading")}</p>}
      {events.isError && (
        <p className={styles.error} role="alert">
          <AlertIcon /> {errorMessage(events.error, t)}
        </p>
      )}

      <div className={styles.grid}>
        {events.data?.map((ev, i) => {
          const day = isoDate(ev.startsAt);
          const soldOut = ev.available === 0;
          return (
            <article key={ev.id} className={styles.card}>
              <div className={`${styles.cover} ${styles[`tone${i % 4}`]}`}>
                <div className={styles.date}>
                  <b>{dayNumber(day)}</b>
                  <small>{monthShort(day)}</small>
                </div>
                <h2>{ev.name}</h2>
              </div>
              <div className={styles.body}>
                <p>
                  {longDate(day)} · {clockTime(ev.startsAt)}
                </p>
                <p className={styles.venue}>
                  {ev.venue} · <b>{ev.city}</b>
                </p>
                <p className={styles.cardPrice}>
                  <b>{money(ev.price, ev.currency)}</b> {t("events.perTicket")}
                </p>
                <div className={styles.foot}>
                  <span className={soldOut ? styles.soldOut : ev.available <= 10 ? styles.few : styles.ok}>
                    {soldOut ? t("events.soldOut") : t("events.available", { count: ev.available })}
                  </span>
                  <button type="button" className="btn btn-primary" disabled={soldOut} onClick={() => setSelected(ev)}>
                    {t("events.buy")}
                  </button>
                </div>
              </div>
            </article>
          );
        })}
      </div>

      {selected && <TicketDialog event={selected} onClose={() => setSelected(null)} />}
    </main>
  );
}

function TicketDialog({ event, onClose }: { event: EventResponse; onClose: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [quantity, setQuantity] = useState(1);
  const [reservation, setReservation] = useState<ReservationResponse | null>(null);
  const countdown = useCountdown(reservation?.status === "Pending" ? reservation.expiresAt : undefined);
  const max = Math.min(MAX_TICKETS, event.available);

  const refreshEvents = () => queryClient.invalidateQueries({ queryKey: ["events"] });

  // 1.º passo: prender os lugares (como no avião, ficam guardados uns minutos).
  const reserve = useMutation({
    mutationFn: () => api.reserveEvent(event.id, quantity),
    onSuccess: (r) => {
      setReservation(r);
      void refreshEvents();
    },
  });

  // 2.º passo: pagar. Só depois do pagamento aprovado é que os bilhetes são emitidos.
  const pay = useMutation({
    mutationFn: (card: CardPayment) => api.payReservation(reservation!.id, card),
    onSuccess: setReservation,
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const error = reserve.error ?? pay.error;
  const expired = reservation?.status === "Expired" || countdown.expired;
  const step = !reservation ? 1 : reservation.status === "Confirmed" ? 3 : 2;

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div
        className={styles.dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby="ticket-title"
        onClick={(e) => e.stopPropagation()}
      >
        <header className={styles.dialogHead}>
          <div>
            <h2 id="ticket-title">{event.name}</h2>
            <span>
              {longDate(isoDate(event.startsAt))} · {clockTime(event.startsAt)} · {event.venue}
            </span>
          </div>
          <button type="button" className={styles.close} onClick={onClose} aria-label={t("events.close")}>
            ×
          </button>
        </header>

        <ol className={styles.progress} aria-label={t("events.steps")}>
          {[t("events.stepTickets"), t("events.stepPayment"), t("events.stepDone")].map((label, i) => (
            <li key={label} className={step === i + 1 ? styles.current : step > i + 1 ? styles.done : ""}>
              {label}
            </li>
          ))}
        </ol>

        {error && (
          <p className={styles.error} role="alert">
            <AlertIcon /> {errorMessage(error, t)}
          </p>
        )}

        {step === 1 && (
          <div className={styles.step}>
            <div>
              <span className={styles.label}>{t("events.tickets")}</span>
              <small className={styles.each}>{t("tripEvents.each", { price: money(event.price, event.currency) })}</small>
            </div>
            <div className={styles.stepper}>
              <button type="button" aria-label="-" disabled={quantity <= 1} onClick={() => setQuantity(quantity - 1)}>
                <MinusIcon />
              </button>
              <b>{quantity}</b>
              <button type="button" aria-label="+" disabled={quantity >= max} onClick={() => setQuantity(quantity + 1)}>
                <PlusIcon />
              </button>
            </div>
            <button type="button" className="btn btn-primary" disabled={reserve.isPending} onClick={() => reserve.mutate()}>
              {t("events.toPayment", { amount: money(event.price * quantity, event.currency) })}
            </button>
          </div>
        )}

        {step === 2 && reservation && !expired && (
          <div>
            <div className={styles.payHead}>
              <div className={styles.timer}>
                <ClockIcon />
                <span>{t("events.heldFor")}</span>
                <b>{countdown.label}</b>
              </div>
              <span>
                {t("tripEvents.tickets", { count: reservation.quantity })} ·{" "}
                <b>{money(reservation.total, reservation.currency)}</b>
              </span>
            </div>
            <CardForm id="event-card-form" disabled={pay.isPending} onSubmit={(card) => pay.mutate(card)} />
            <button type="submit" form="event-card-form" className={`btn btn-primary ${styles.payButton}`} disabled={pay.isPending}>
              <LockIcon /> {pay.isPending ? t("payment.processing") : t("payment.pay", { amount: money(reservation.total, reservation.currency) })}
            </button>
          </div>
        )}

        {expired && (
          <p className={styles.error} role="alert">
            <AlertIcon /> {t("errors.reservation_expired")}
          </p>
        )}

        {step === 3 && reservation && (
          <div>
            <h3 className={styles.ticketsTitle}>{t("events.yourTickets")}</h3>
            <p className={styles.muted}>{t("events.showAtEntrance")}</p>
            <div className={styles.tickets}>
              {reservation.tickets.map((code) => (
                <div key={code} className={styles.ticket}>
                  <QRCodeSVG value={code} size={96} marginSize={0} />
                  <code>{code}</code>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
