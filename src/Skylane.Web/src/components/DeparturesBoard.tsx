import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "../api/client";
import { clockTime } from "../lib/format";
import styles from "./DeparturesBoard.module.css";

/** Quadro de partidas do aeroporto de origem escolhido na pesquisa. */
export default function DeparturesBoard({ code, city }: { code: string; city: string }) {
  const { t } = useTranslation();
  const { data, isLoading } = useQuery({
    queryKey: ["departures", code],
    queryFn: () => api.departures(code, 6),
    enabled: code.length === 3,
  });

  return (
    <section className={styles.board} aria-label={t("home.departuresTitle", { city })}>
      <div className={styles.head}>
        <span>{t("home.departuresTitle", { city })}</span>
        <span className={styles.next}>{t("home.departuresNext")}</span>
      </div>
      <div className={`${styles.row} ${styles.cols}`}>
        <span>{t("home.colTime")}</span>
        <span>{t("home.colFlight")}</span>
        <span>{t("home.colDestination")}</span>
        <span className={styles.right}>{t("home.colStatus")}</span>
      </div>

      {isLoading &&
        Array.from({ length: 6 }, (_, i) => (
          <div key={i} className={styles.row}>
            <span className={styles.skeleton} />
          </div>
        ))}

      {data?.map((d) => (
        <div key={d.flightId} className={styles.row}>
          <b>{clockTime(d.departsAt)}</b>
          <span>{d.number}</span>
          <span className={styles.dest}>{d.destination.city}</span>
          <em className={`${styles.right} ${styles[d.status]}`}>{t(`home.status.${d.status}`)}</em>
        </div>
      ))}
    </section>
  );
}
