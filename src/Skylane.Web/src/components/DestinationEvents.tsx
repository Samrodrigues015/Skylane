import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { api } from "../api/client";
import { clockTime, dayNumber, isoDate, money, monthShort, shortDate } from "../lib/format";
import EventImage from "./EventImage";
import { ArrowRightIcon } from "./Icons";
import styles from "./DestinationEvents.module.css";

interface Props {
  /** Aeroporto de destino (ex.: "AMS"). */
  city: string;
  /** Primeiro e último dia da estadia (yyyy-MM-dd). */
  from: string;
  to: string;
  /** Eventos a não mostrar (ex.: os que já estão na reserva). */
  exclude?: string[];
}

/**
 * Eventos na cidade de destino durante a viagem. Se não houver nenhum nessas datas,
 * mostra os próximos eventos da cidade, para a secção nunca ficar vazia sem explicação.
 */
export default function DestinationEvents({ city, from, to, exclude = [] }: Props) {
  const { t } = useTranslation();

  const query = useQuery({
    queryKey: ["destination-events", city, from, to],
    queryFn: async () => {
      const during = await api.events({ city, from, to, limit: 3 });
      if (during.length > 0) return { during: true, events: during };
      return { during: false, events: await api.events({ city, limit: 3 }) };
    },
    staleTime: 60_000,
  });

  const events = (query.data?.events ?? []).filter((e) => !exclude.includes(e.id));
  if (events.length === 0) return null;

  const cityName = events[0].city;

  return (
    <section className={styles.section}>
      <div className={styles.head}>
        <div>
          <span className={styles.city}>{cityName}</span>
          <h2>{query.data!.during ? t("destinationEvents.during") : t("destinationEvents.upcoming")}</h2>
          <p>{query.data!.during ? t("destinationEvents.duringLead") : t("destinationEvents.upcomingLead")}</p>
        </div>
        <Link to={`/events?city=${city}`} className={styles.all}>
          {t("destinationEvents.all", { city: cityName })}
          <ArrowRightIcon />
        </Link>
      </div>

      <div className={styles.grid}>
        {events.map((ev, i) => {
          const day = isoDate(ev.startsAt);
          return (
            <Link
              key={ev.id}
              to={`/events?city=${city}&event=${ev.id}`}
              className={styles.event}
            >
              <EventImage className={styles.photo} category={ev.category} seed={ev.name} width={i === 0 ? 640 : 420} />
              <div className={styles.date}>
                <b>{dayNumber(day)}</b>
                <small>{monthShort(day)}</small>
              </div>
              {ev.available > 0 && ev.available <= 10 && (
                <span className={styles.last}>{t("home.lastTickets", { count: ev.available })}</span>
              )}
              <h3>{ev.name}</h3>
              <p>
                {shortDate(day)} · {clockTime(ev.startsAt)} · {ev.venue}
              </p>
              <span className={styles.price}>{t("tripEvents.each", { price: money(ev.price, ev.currency) })}</span>
            </Link>
          );
        })}
      </div>
    </section>
  );
}
