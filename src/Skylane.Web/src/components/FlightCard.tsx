import { useTranslation } from "react-i18next";
import type { CabinClass, FlightSummary } from "../api/types";
import { fareFor } from "../lib/flow";
import { clockTime, duration, money } from "../lib/format";
import styles from "./FlightCard.module.css";

interface Props {
  flight: FlightSummary;
  cabin: CabinClass;
  selected: boolean;
  tags: string[];
  onSelect: () => void;
}

export default function FlightCard({ flight, cabin, selected, tags, onSelect }: Props) {
  const { t } = useTranslation();
  const fare = fareFor(flight, cabin);
  const other = flight.fares.find((f) => f.class !== cabin);
  const airlineCode = flight.number.slice(0, 2);

  const extraTags = [...tags];
  if (fare.seatsLeft > 0 && fare.seatsLeft <= 6) {
    extraTags.push(t("results.lastInCabin", { count: fare.seatsLeft, cabin: t(`cabin.${cabin}`) }));
  }

  return (
    <article className={`${styles.card} ${selected ? styles.selected : ""}`}>
      <div className={styles.main}>
        {(extraTags.length > 0 || selected) && (
          <div className={styles.tags}>
            {extraTags.map((tag) => (
              <span key={tag} className={styles.tagGreen}>
                {tag}
              </span>
            ))}
            {selected && <span className={styles.tagBlue}>{t("results.selected")}</span>}
          </div>
        )}

        <div className={styles.leg}>
          <span className={`${styles.airline} ${styles[airlineCode] ?? ""}`}>{airlineCode}</span>
          <div>
            <div className={styles.time}>{clockTime(flight.departsAt)}</div>
            <div className={styles.place}>
              {flight.origin.code} · {flight.origin.city}
            </div>
          </div>
          <div className={styles.mid}>
            {duration(flight.durationMinutes)}
            <div className={styles.line} />
            <span className={styles.direct}>{t("results.direct")}</span>
          </div>
          <div className={styles.end}>
            <div className={styles.time}>{clockTime(flight.arrivesAt)}</div>
            <div className={styles.place}>
              {flight.destination.code} · {flight.destination.city}
            </div>
          </div>
        </div>

        <div className={styles.meta}>
          <span>
            {flight.airline} · {flight.number}
          </span>
          <span>{t("results.aircraft")}</span>
          <span>{t("results.seatsLeft", { count: fare.seatsLeft })}</span>
        </div>
      </div>

      <div className={styles.price}>
        <small>{t("results.perPerson")}</small>
        <b>{money(fare.price)}</b>
        {other && (
          <span className={styles.otherFare}>
            {t(`cabin.${other.class}`)} {money(other.price)}
          </span>
        )}
        <button
          type="button"
          className={`btn ${selected ? styles.selectedBtn : "btn-blue"}`}
          onClick={onSelect}
          disabled={fare.seatsLeft === 0}
        >
          {selected ? t("results.selected") : t("results.choose")}
        </button>
      </div>
    </article>
  );
}
