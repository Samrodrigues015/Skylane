import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { FlightSummary } from "../api/types";
import { useCart } from "../lib/cart";
import { fareFor, type SearchParams } from "../lib/flow";
import { clockTime, money, shortDate } from "../lib/format";
import { ClockIcon } from "./Icons";
import styles from "./TripSummary.module.css";

interface Props {
  search: SearchParams;
  outbound?: FlightSummary;
  inbound?: FlightSummary;
  children?: ReactNode;
  showHoldInfo?: boolean;
}

/** Resumo lateral da viagem, presente em todos os passos da compra. */
export default function TripSummary({ search, outbound, inbound, children, showHoldInfo = true }: Props) {
  const { t } = useTranslation();
  const roundTrip = search.return !== undefined;

  const legTotal = (f?: FlightSummary) => (f ? fareFor(f, search.cabin).price * search.adults : 0);
  const cart = useCart();
  const total = legTotal(outbound) + legTotal(inbound) + cart.total;

  return (
    <aside className={`panel ${styles.summary}`}>
      <h2>{t("summary.title")}</h2>

      <Leg label={t("summary.outbound")} date={search.depart} flight={outbound} cabin={t(`cabin.${search.cabin}`)} />
      {roundTrip && (
        <Leg
          label={t("summary.return")}
          date={search.return!}
          flight={inbound}
          cabin={t(`cabin.${search.cabin}`)}
          empty={t("summary.chooseReturn")}
        />
      )}

      {cart.items.length > 0 && (
        <div className={styles.leg}>
          <small>{t("cart.inCart")}</small>
          {cart.items.map((i) => (
            <span key={i.event.id} className={styles.cartLine}>
              <span>
                {i.quantity} × {i.event.name}
              </span>
              <span>{money(i.quantity * i.event.price, i.event.currency)}</span>
            </span>
          ))}
        </div>
      )}

      <div className={styles.total}>
        <span>{t("summary.totalFor", { count: search.adults })}</span>
        <b>{outbound ? money(total) : "—"}</b>
      </div>

      {children}

      {showHoldInfo && (
        <div className={styles.hold}>
          <ClockIcon />
          <span>{t("summary.holdInfo")}</span>
        </div>
      )}
    </aside>
  );
}

function Leg(props: { label: string; date: string; flight?: FlightSummary; cabin: string; empty?: string }) {
  return (
    <div className={styles.leg}>
      <small>
        {props.label} · {shortDate(props.date)}
      </small>
      {props.flight ? (
        <>
          <b>
            {clockTime(props.flight.departsAt)} {props.flight.origin.code} → {clockTime(props.flight.arrivesAt)}{" "}
            {props.flight.destination.code}
          </b>
          <span>
            {props.flight.number} · {props.cabin}
          </span>
        </>
      ) : (
        <span className={styles.pending}>{props.empty ?? "—"}</span>
      )}
    </div>
  );
}
