import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { api } from "../api/client";
import DeparturesBoard from "../components/DeparturesBoard";
import FlapBoard from "../components/FlapBoard";
import { SwapIcon } from "../components/Icons";
import SearchForm from "../components/SearchForm";
import { searchToQuery, useFlow, type SearchParams } from "../lib/flow";
import { addDays, dayNumber, duration, isoDate, money, monthShort, todayIso } from "../lib/format";
import styles from "./HomePage.module.css";

const defaultSearch = (): SearchParams => {
  const depart = addDays(todayIso(), 3);
  return { from: "OPO", to: "FNC", depart, return: addDays(depart, 5), adults: 1, cabin: "Economy" };
};

export default function HomePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { state, startSearch } = useFlow();

  // Começa com a última pesquisa (se a data ainda não passou) ou com uma sugestão.
  const [search, setSearch] = useState<SearchParams>(() =>
    state.search && state.search.depart >= todayIso() ? state.search : defaultSearch(),
  );

  const airports = useQuery({ queryKey: ["airports"], queryFn: api.airports, staleTime: Infinity });
  const deals = useQuery({ queryKey: ["deals"], queryFn: () => api.deals(4) });
  const events = useQuery({ queryKey: ["events"], queryFn: api.events });

  const city = (code: string) => airports.data?.find((a) => a.code === code)?.city ?? code;

  const submit = (s: SearchParams) => {
    startSearch(s);
    navigate(`/flights?${searchToQuery(s, "outbound")}`);
  };

  const dealLink = (from: string, to: string) =>
    `/flights?${searchToQuery({ from, to, depart: addDays(todayIso(), 1), adults: 1, cabin: "Economy" }, "outbound")}`;

  return (
    <main>
      <section className={styles.hero}>
        <div className={`wrap ${styles.heroInner}`}>
          <div>
            <p className={styles.kicker}>{t("home.kicker")}</p>
            <h1 className={styles.title}>{t("home.title")}</h1>

            <div className={styles.board}>
              <div>
                <FlapBoard code={search.from} />
                <div className={styles.cityline}>
                  <b>{city(search.from)}</b>
                  <span>{t("home.departure")}</span>
                </div>
              </div>
              <button
                type="button"
                className={styles.swap}
                aria-label={t("home.swap")}
                onClick={() => setSearch({ ...search, from: search.to, to: search.from })}
              >
                <SwapIcon />
              </button>
              <div>
                <FlapBoard code={search.to} />
                <div className={styles.cityline}>
                  <b>{city(search.to)}</b>
                  <span>{t("home.arrival")}</span>
                </div>
              </div>
            </div>
          </div>

          <DeparturesBoard code={search.from} city={city(search.from)} />
        </div>
      </section>

      <div className="wrap">
        <div className={styles.searchSlot}>
          <SearchForm airports={airports.data ?? []} value={search} onChange={setSearch} onSubmit={submit} />
        </div>

        <section className={styles.section}>
          <div className={styles.sectionHead}>
            <h2>{t("home.dealsTitle")}</h2>
          </div>
          <div className={styles.routes}>
            {deals.data?.map((d) => (
              <Link key={`${d.origin.code}-${d.destination.code}`} to={dealLink(d.origin.code, d.destination.code)} className={styles.route}>
                <div className={styles.routeTop}>
                  <span className={styles.codes}>
                    {d.origin.code}
                    <i>→</i>
                    {d.destination.code}
                  </span>
                  <span className={styles.dur}>{duration(d.durationMinutes)}</span>
                </div>
                <span className={styles.routeCity}>
                  {d.origin.city} → {d.destination.city}
                </span>
                <div className={styles.routePrice}>
                  <small>{t("home.from")}</small>
                  <b>{money(d.minPrice)}</b>
                </div>
              </Link>
            ))}
            {deals.isLoading && Array.from({ length: 4 }, (_, i) => <div key={i} className={`${styles.route} ${styles.placeholder}`} />)}
          </div>
        </section>

        <section className={styles.section}>
          <div className={styles.sectionHead}>
            <h2>{t("home.eventsTitle")}</h2>
            <Link to="/events">{t("home.allEvents")}</Link>
          </div>
          <div className={styles.events}>
            {events.data?.slice(0, 3).map((ev, i) => (
              <Link key={ev.id} to="/events" className={`${styles.event} ${styles[`tone${i % 3}`]}`}>
                <div className={styles.date}>
                  <b>{dayNumber(isoDate(ev.startsAt))}</b>
                  <small>{monthShort(isoDate(ev.startsAt))}</small>
                </div>
                {ev.available > 0 && ev.available <= 10 && (
                  <span className={styles.last}>{t("home.lastTickets", { count: ev.available })}</span>
                )}
                <h3>{ev.name}</h3>
                <p>{ev.venue}</p>
              </Link>
            ))}
          </div>
        </section>
      </div>
    </main>
  );
}
