import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import EventImage from "../components/EventImage";
import { Link, useNavigate } from "react-router";
import { api } from "../api/client";
import { AlertIcon, ArrowRightIcon, MinusIcon, PlaneIcon, PlusIcon } from "../components/Icons";
import { cartSelections, MAX_TICKETS, useCart, type CartItem } from "../lib/cart";
import { errorMessage } from "../lib/errors";
import { searchToQuery, useFlow } from "../lib/flow";
import { addDays, clockTime, dayNumber, isoDate, money, monthShort, shortDate, todayIso } from "../lib/format";
import checkout from "./Checkout.module.css";
import styles from "./CartPage.module.css";

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function CartPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const cart = useCart();
  const { state, update, startSearch } = useFlow();
  const [email, setEmail] = useState(state.contactEmail);
  const [submitted, setSubmitted] = useState(false);

  // Só bilhetes: cria a "reserva" sem voos e segue para o mesmo pagamento das viagens.
  const checkoutTickets = useMutation({
    mutationFn: () => api.createBooking({ contactEmail: email.trim(), events: cartSelections(cart.items) }),
    onSuccess: (booking) => {
      update({ booking, contactEmail: email.trim(), search: undefined, outbound: undefined, inbound: undefined });
      cart.clear();
      navigate("/booking/payment");
    },
  });

  if (cart.items.length === 0) {
    return (
      <main className="wrap page">
        <section className={`panel ${styles.empty}`}>
          <h1>{t("cart.empty")}</h1>
          <p>{t("cart.emptyText")}</p>
          <div>
            <Link to="/events" className="btn btn-primary">
              {t("cart.seeEvents")}
            </Link>
            <Link to="/" className="btn btn-ghost">
              {t("cart.searchFlights")}
            </Link>
          </div>
        </section>
      </main>
    );
  }

  // Uma sugestão de voo por cidade: chegar na véspera do primeiro evento e voltar dois dias depois do último.
  const cities = [...new Set(cart.items.map((i) => i.event.airportCode))].map((code) => {
    const days = cart.items.filter((i) => i.event.airportCode === code).map((i) => isoDate(i.event.startsAt)).sort();
    const depart = addDays(days[0], -1) < todayIso() ? todayIso() : addDays(days[0], -1);
    const back = addDays(days[days.length - 1], 2);
    return { code, city: cart.items.find((i) => i.event.airportCode === code)!.event.city, depart, back };
  });

  const searchFlight = (c: (typeof cities)[number]) => {
    const preferred = state.search?.from ?? "OPO";
    const from = preferred !== c.code ? preferred : c.code === "LIS" ? "OPO" : "LIS";
    const adults = Math.max(...cart.items.filter((i) => i.event.airportCode === c.code).map((i) => i.quantity));
    const search = { from, to: c.code, depart: c.depart, return: c.back, adults: Math.min(adults, 9), cabin: "Economy" as const };
    startSearch(search);
    navigate(`/flights?${searchToQuery(search, "outbound")}`);
  };

  const emailInvalid = !EMAIL.test(email.trim());
  const payTickets = () => {
    setSubmitted(true);
    if (!emailInvalid) checkoutTickets.mutate();
  };

  return (
    <main className="wrap page">
      <h1 className="page-title">{t("cart.title")}</h1>
      <p className="page-lead">{t("cart.lead")}</p>

      <div className={checkout.layout}>
        <div>
          <section className={checkout.card}>
            <div className={styles.items}>
              {cart.items.map((item) => (
                <Item key={item.event.id} item={item} />
              ))}
            </div>
          </section>

          <section className={`${checkout.card} ${styles.flight}`}>
            <span className={styles.flightIcon}>
              <PlaneIcon />
            </span>
            <div>
              <h2>{t("cart.addFlightTitle")}</h2>
              <p>{t("cart.addFlightText")}</p>
              <div className={styles.flightButtons}>
                {cities.map((c) => (
                  <button key={c.code} type="button" className="btn btn-blue" onClick={() => searchFlight(c)}>
                    {t("cart.flightsTo", { city: c.city })}
                    <small>{t("cart.around", { from: shortDate(c.depart), to: shortDate(c.back) })}</small>
                  </button>
                ))}
              </div>
            </div>
          </section>
        </div>

        <aside className={`panel ${styles.summary}`}>
          <h2>{t("cart.orderTitle")}</h2>
          <div className={styles.line}>
            <span>{t("cart.eventsInCart", { count: cart.count })}</span>
            <b>{money(cart.total)}</b>
          </div>
          <div className={styles.total}>
            <span>{t("summary.total")}</span>
            <b>{money(cart.total)}</b>
          </div>

          <label className={checkout.field}>
            <span>{t("cart.email")}</span>
            <input
              type="email"
              autoComplete="email"
              value={email}
              aria-invalid={(submitted && emailInvalid) || undefined}
              onChange={(e) => setEmail(e.target.value)}
            />
            {submitted && emailInvalid && <small className={styles.invalid}>{t("passengers.invalidEmail")}</small>}
          </label>

          {checkoutTickets.isError && (
            <div className={checkout.alert} role="alert">
              <AlertIcon />
              <span>{errorMessage(checkoutTickets.error, t)}</span>
            </div>
          )}

          <button
            type="button"
            className={`btn btn-primary ${checkout.fullBtn}`}
            disabled={checkoutTickets.isPending}
            onClick={payTickets}
          >
            {t("cart.ticketsOnly")} <ArrowRightIcon />
          </button>
        </aside>
      </div>
    </main>
  );
}

function Item({ item }: { item: CartItem }) {
  const { t } = useTranslation();
  const cart = useCart();
  const { event, quantity } = item;
  const day = isoDate(event.startsAt);
  const max = Math.min(MAX_TICKETS, event.available);

  return (
    <article className={styles.item}>
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
        <span className={styles.city}>{event.city}</span>
      </div>
      <div className={styles.stepper}>
        <button type="button" aria-label="-" disabled={quantity <= 1} onClick={() => cart.setQuantity(event.id, quantity - 1)}>
          <MinusIcon />
        </button>
        <b>{quantity}</b>
        <button type="button" aria-label="+" disabled={quantity >= max} onClick={() => cart.setQuantity(event.id, quantity + 1)}>
          <PlusIcon />
        </button>
      </div>
      <div className={styles.price}>
        <b>{money(event.price * quantity, event.currency)}</b>
        <small>{t("tripEvents.each", { price: money(event.price, event.currency) })}</small>
      </div>
      <button type="button" className={styles.remove} onClick={() => cart.remove(event.id)}>
        {t("cart.remove")}
      </button>
    </article>
  );
}
