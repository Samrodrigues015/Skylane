import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router";
import { api } from "../api/client";
import type { EventResponse } from "../api/types";
import EventImage from "../components/EventImage";
import { AlertIcon, ArrowRightIcon, CartIcon, CheckIcon, MinusIcon, PlusIcon } from "../components/Icons";
import { MAX_TICKETS, useCart } from "../lib/cart";
import { errorMessage } from "../lib/errors";
import { clockTime, dayNumber, isoDate, longDate, money, monthShort } from "../lib/format";
import styles from "./EventsPage.module.css";

export default function EventsPage() {
  const { t, i18n } = useTranslation();
  const [params, setParams] = useSearchParams();
  const city = params.get("city") ?? undefined;

  const airports = useQuery({ queryKey: ["airports"], queryFn: api.airports, staleTime: Infinity });
  // O idioma entra na chave: as descrições vêm da API no idioma do site.
  const events = useQuery({ queryKey: ["events", city, i18n.language], queryFn: () => api.events({ city }) });
  const [selected, setSelected] = useState<EventResponse | null>(null);

  // Vindo de "Durante a tua viagem": abre logo o evento escolhido.
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
        {events.data?.map((ev) => {
          const day = isoDate(ev.startsAt);
          const soldOut = ev.available === 0;
          return (
            <article key={ev.id} className={styles.card}>
              <button type="button" className={styles.cover} onClick={() => setSelected(ev)} aria-label={ev.name}>
                <EventImage category={ev.category} seed={ev.name} width={560} />
                <span className={styles.category}>{t(`eventCategory.${ev.category}.name`)}</span>
                <span className={styles.date}>
                  <b>{dayNumber(day)}</b>
                  <small>{monthShort(day)}</small>
                </span>
              </button>
              <div className={styles.body}>
                <span className={styles.cityLabel}>{ev.city}</span>
                <h2>{ev.name}</h2>
                <p className={styles.meta}>
                  {longDate(day)} · {clockTime(ev.startsAt)} · {ev.venue}
                </p>
                {ev.description && <p className={styles.description}>{ev.description}</p>}
                <div className={styles.foot}>
                  <div>
                    <p className={styles.cardPrice}>
                      <b>{money(ev.price, ev.currency)}</b> {t("events.perTicket")}
                    </p>
                    <span className={soldOut ? styles.soldOut : ev.available <= 10 ? styles.few : styles.ok}>
                      {soldOut ? t("events.soldOut") : t("events.available", { count: ev.available })}
                    </span>
                  </div>
                  <button type="button" className="btn btn-primary" disabled={soldOut} onClick={() => setSelected(ev)}>
                    {t("events.details")}
                  </button>
                </div>
              </div>
            </article>
          );
        })}
      </div>

      {selected && <EventDialog event={selected} onClose={() => setSelected(null)} />}
    </main>
  );
}

function EventDialog({ event, onClose }: { event: EventResponse; onClose: () => void }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const cart = useCart();
  const inCart = cart.items.find((i) => i.event.id === event.id)?.quantity ?? 0;
  const max = Math.max(0, Math.min(MAX_TICKETS - inCart, event.available - inCart));
  const [quantity, setQuantity] = useState(Math.min(1, max));
  const [added, setAdded] = useState(false);
  const day = isoDate(event.startsAt);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const add = () => {
    cart.add(event, quantity);
    setAdded(true);
  };

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div
        className={styles.dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby="event-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className={styles.banner}>
          <EventImage category={event.category} seed={event.name} width={700} />
          <button type="button" className={styles.close} onClick={onClose} aria-label={t("events.close")}>
            ×
          </button>
        </div>

        <div className={styles.dialogBody}>
          <span className={styles.chip}>{t(`eventCategory.${event.category}.name`)}</span>
          <h2 id="event-title">{event.name}</h2>
          <p className={styles.meta}>
            {longDate(day)} · {clockTime(event.startsAt)}
            <br />
            {event.venue} · {event.city}
          </p>

          {event.description && <p className={styles.fullDescription}>{event.description}</p>}

          <aside className={styles.about}>
            <b>{t(`eventCategory.${event.category}.aboutTitle`)}</b>
            <p>{t(`eventCategory.${event.category}.about`)}</p>
          </aside>

          {!added ? (
            <div className={styles.buy}>
              <div>
                <b className={styles.buyPrice}>{money(event.price, event.currency)}</b>
                <small className={styles.each}>{t("events.perTicket")}</small>
                {inCart > 0 && (
                  <small className={styles.each}>
                    {t("cart.inCart")}: {inCart}
                  </small>
                )}
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
              <button type="button" className="btn btn-primary" disabled={max === 0} onClick={add}>
                <CartIcon /> {t("events.addToCart", { amount: money(event.price * quantity, event.currency) })}
              </button>
            </div>
          ) : (
            <div className={styles.added}>
              <span className={styles.addedIcon}>
                <CheckIcon />
              </span>
              <div>
                <b>{t("cart.added")}</b>
                <span>{t("cart.addedText", { count: quantity, name: event.name })}</span>
              </div>
              <div className={styles.addedActions}>
                <button type="button" className="btn btn-ghost" onClick={onClose}>
                  {t("cart.keepBrowsing")}
                </button>
                <button type="button" className="btn btn-primary" onClick={() => navigate("/cart")}>
                  {t("cart.view")} <ArrowRightIcon />
                </button>
              </div>
            </div>
          )}
          {!added && <p className={styles.hint}>{t("events.cartHint")}</p>}
        </div>
      </div>
    </div>
  );
}
