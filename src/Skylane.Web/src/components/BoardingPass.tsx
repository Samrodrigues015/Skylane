import { QRCodeSVG } from "qrcode.react";
import { useTranslation } from "react-i18next";
import type { BookedSeat, BookingLeg } from "../api/types";
import { clockTime, isoDate, minutesOfDay, shortDate } from "../lib/format";
import { PlaneIcon } from "./Icons";
import styles from "./BoardingPass.module.css";

const BOARDING_MINUTES = 40;

function boardingTime(departsAt: string) {
  const m = (minutesOfDay(departsAt) - BOARDING_MINUTES + 1440) % 1440;
  return `${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`;
}

/** Porta "inventada" mas estável: o mesmo voo mostra sempre a mesma porta. */
function gate(flightNumber: string) {
  const n = [...flightNumber].reduce((sum, c) => sum + c.charCodeAt(0), 0);
  return `${"ABC"[n % 3]}${(n % 38) + 1}`;
}

export default function BoardingPass({ reference, leg, seat }: { reference: string; leg: BookingLeg; seat: BookedSeat }) {
  const { t } = useTranslation();
  const qr = ["SKYLANE", reference, leg.flightNumber, isoDate(leg.departsAt), seat.seatNumber, seat.passenger].join("|");

  return (
    <article className={styles.pass}>
      <div className={styles.main}>
        <header className={styles.head}>
          <span className={styles.brand}>
            <PlaneIcon /> Skylane
          </span>
          <span>{t("confirmation.boardingPass")}</span>
          <span className={styles.cabin}>{t(`cabin.${seat.class}`)}</span>
        </header>

        <div className={styles.route}>
          <div>
            <b>{leg.origin.code}</b>
            <span>{leg.origin.city}</span>
          </div>
          <div className={styles.line}>
            <PlaneIcon />
          </div>
          <div className={styles.right}>
            <b>{leg.destination.code}</b>
            <span>{leg.destination.city}</span>
          </div>
        </div>

        <dl className={styles.grid}>
          <div className={styles.span2}>
            <dt>{t("confirmation.passenger")}</dt>
            <dd>{seat.passenger.toUpperCase()}</dd>
          </div>
          <div>
            <dt>{t("confirmation.flight")}</dt>
            <dd>{leg.flightNumber}</dd>
          </div>
          <div>
            <dt>{t("confirmation.date")}</dt>
            <dd>{shortDate(isoDate(leg.departsAt))}</dd>
          </div>
          <div>
            <dt>{t("confirmation.boarding")}</dt>
            <dd>{boardingTime(leg.departsAt)}</dd>
          </div>
          <div>
            <dt>{t("confirmation.departure")}</dt>
            <dd>{clockTime(leg.departsAt)}</dd>
          </div>
          <div>
            <dt>{t("confirmation.gate")}</dt>
            <dd>{gate(leg.flightNumber)}</dd>
          </div>
          <div>
            <dt>{t("confirmation.seat")}</dt>
            <dd className={styles.seat}>{seat.seatNumber}</dd>
          </div>
        </dl>
      </div>

      <div className={styles.stub}>
        <QRCodeSVG value={qr} size={112} level="M" marginSize={0} />
        <span className={styles.ref}>{reference}</span>
        <span className={styles.stubSeat}>
          {t("confirmation.seat")} <b>{seat.seatNumber}</b>
        </span>
      </div>
    </article>
  );
}
