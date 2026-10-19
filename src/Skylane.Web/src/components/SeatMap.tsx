import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { CabinClass, SeatInfo } from "../api/types";
import { money } from "../lib/format";
import styles from "./SeatMap.module.css";

interface Props {
  seats: SeatInfo[];
  cabin: CabinClass;
  prices: Record<CabinClass, number>;
  /** Lugar → índice do passageiro (0, 1, …) que o tem. */
  assigned: Record<string, number>;
  onSeatClick: (seat: SeatInfo) => void;
}

const BUSINESS_LETTERS = ["A", "C", null, "D", "F"];
const ECONOMY_LETTERS = ["A", "B", "C", null, "D", "E", "F"];
const EXIT_ROWS = new Set([10, 16]);

function position(letter: string, cabin: CabinClass) {
  if (letter === "A" || letter === "F") return "window";
  if (cabin === "Business" || letter === "C" || letter === "D") return "aisle";
  return "middle";
}

/**
 * Mapa do avião visto de cima. As filas e lugares vêm da API (GET /api/flights/{id}/seats),
 * por isso o componente funciona com qualquer avião que a API venha a ter.
 */
export default function SeatMap({ seats, cabin, prices, assigned, onSeatClick }: Props) {
  const { t } = useTranslation();
  const [hovered, setHovered] = useState<SeatInfo | null>(null);

  const rows = new Map<number, Map<string, SeatInfo>>();
  for (const seat of seats) {
    if (!rows.has(seat.row)) rows.set(seat.row, new Map());
    rows.get(seat.row)!.set(seat.letter, seat);
  }
  const rowNumbers = [...rows.keys()].sort((a, b) => a - b);
  const firstEconomyRow = rowNumbers.find((r) => [...rows.get(r)!.values()][0].class === "Economy");

  const describe = (seat: SeatInfo) =>
    `${seat.seatNumber} · ${t(`cabin.${seat.class}`)} · ${t(`seats.${position(seat.letter, seat.class)}`)}`;

  const renderRow = (row: number) => {
    const map = rows.get(row)!;
    const rowCabin = [...map.values()][0].class;
    const letters = rowCabin === "Business" ? BUSINESS_LETTERS : ECONOMY_LETTERS;

    return (
      <div key={row} className={`${styles.row} ${rowCabin === "Business" ? styles.business : ""}`}>
        {letters.map((letter, i) => {
          if (letter === null) {
            return (
              <span key={`aisle-${i}`} className={styles.rowNumber}>
                {row}
              </span>
            );
          }
          const seat = map.get(letter);
          if (!seat) return <span key={letter} />;

          const owner = assigned[seat.seatNumber];
          const mine = owner !== undefined;
          const wrongCabin = seat.class !== cabin;
          const disabled = !mine && (!seat.available || wrongCabin);

          return (
            <button
              key={letter}
              type="button"
              className={[
                styles.seat,
                mine ? styles.mine : "",
                !seat.available && !mine ? styles.taken : "",
                wrongCabin && seat.available ? styles.other : "",
              ].join(" ")}
              disabled={disabled}
              aria-label={`${describe(seat)}${!seat.available ? ` · ${t("seats.occupied")}` : ""}`}
              aria-pressed={mine}
              onClick={() => onSeatClick(seat)}
              onMouseEnter={() => setHovered(seat)}
              onFocus={() => setHovered(seat)}
            >
              {mine ? owner + 1 : ""}
            </button>
          );
        })}
      </div>
    );
  };

  const letterHeader = (business: boolean) => (
    <div className={`${styles.row} ${styles.letters} ${business ? styles.business : ""}`} aria-hidden="true">
      {(business ? BUSINESS_LETTERS : ECONOMY_LETTERS).map((l, i) => (
        <span key={i}>{l ?? ""}</span>
      ))}
    </div>
  );

  return (
    <div className={styles.wrapper}>
      <div className={styles.info} aria-live="polite">
        {hovered ? (
          <>
            <b>{describe(hovered)}</b>
            <span>
              {!hovered.available && assigned[hovered.seatNumber] === undefined
                ? t("seats.occupied")
                : hovered.class !== cabin
                  ? t("seats.otherCabin")
                  : `${money(prices[hovered.class])} · ${t("seats.clickToChoose")}`}
            </span>
          </>
        ) : (
          <span>{t("seats.hint")}</span>
        )}
      </div>

      <div className={styles.fuselage} onMouseLeave={() => setHovered(null)}>
        <div className={styles.cockpit} />
        <div className={`${styles.wing} ${styles.wingLeft}`} />
        <div className={`${styles.wing} ${styles.wingRight}`} />

        <div className={styles.cabinLabel}>
          {t("cabin.Business")} · {money(prices.Business)}
        </div>
        {letterHeader(true)}
        {rowNumbers.filter((r) => firstEconomyRow === undefined || r < firstEconomyRow).map(renderRow)}

        <div className={styles.cabinLabel}>
          {t("cabin.Economy")} · {money(prices.Economy)}
        </div>
        {letterHeader(false)}
        {rowNumbers
          .filter((r) => firstEconomyRow !== undefined && r >= firstEconomyRow)
          .map((r) => (
            <div key={r}>
              {EXIT_ROWS.has(r) && (
                <div className={styles.exit} aria-hidden="true">
                  <span>◀ {t("seats.exit")}</span>
                  <span>
                    {t("seats.exit")} ▶
                  </span>
                </div>
              )}
              {renderRow(r)}
            </div>
          ))}
      </div>
    </div>
  );
}
