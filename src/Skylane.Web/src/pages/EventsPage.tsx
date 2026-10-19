import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import type { EventResponse, ReservationResponse } from "../api/types";
import { AlertIcon, ClockIcon, MinusIcon, PlusIcon } from "../components/Icons";
import { errorMessage } from "../lib/errors";
import { clockTime, dayNumber, isoDate, longDate, monthShort } from "../lib/format";
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

  const reserve = useMutation({
    mutationFn: () => api.reserveEvent(event.id, quantity),
    onSuccess: (r) => {
      setReservation(r);
      void refreshEvents();
    },
  });

  const confirm = useMutation({
    mutationFn: () => api.confirmReservation(reservation!.id),
    onSuccess: setReservation,
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const error = reserve.error ?? confirm.error;
  const expired = reservation?.status === "Expired" || countdown.expired;

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

        {error && (
          <p className={styles.error} role="alert">
            <AlertIcon /> {errorMessage(error, t)}
          </p>
        )}

        {!reservation && (
          <div className={styles.step}>
            <span className={styles.label}>{t("events.tickets")}</span>
            <div className={styles.stepper}>
              <button
                type="button"
                aria-label="-"
                disabled={quantity <= 1}
                onClick={() => setQuantity(quantity - 1)}
              >
                <MinusIcon />
              </button>
              <b>{quantity}</b>
              <button
                type="button"
                aria-label="+"
                disabled={quantity >= max}
                onClick={() => setQuantity(quantity + 1)}
              >
                <PlusIcon />
              </button>
            </div>
            <button
              type="button"
              className="btn btn-primary"
              disabled={reserve.isPending}
              onClick={() => reserve.mutate()}
            >
              {t("events.reserve")}
            </button>
          </div>
        )}

        {reservation?.status === "Pending" && !expired && (
          <div className={styles.step}>
            <div className={styles.timer}>
              <ClockIcon />
              <span>{t("events.heldFor")}</span>
              <b>{countdown.label}</b>
            </div>
            <span>{t("events.ticketCount", { count: reservation.quantity })}</span>
            <button
              type="button"
              className="btn btn-primary"
              disabled={confirm.isPending}
              onClick={() => confirm.mutate()}
            >
              {t("events.confirm")}
            </button>
          </div>
        )}

        {expired && (
          <p className={styles.error} role="alert">
            <AlertIcon /> {t("errors.reservation_expired")}
          </p>
        )}

        {reservation?.status === "Confirmed" && (
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
