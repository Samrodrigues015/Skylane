import { useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router";
import { api } from "../api/client";
import type { FlightSummary } from "../api/types";
import FlightCard from "../components/FlightCard";
import { AlertIcon } from "../components/Icons";
import Steps from "../components/Steps";
import TripSummary from "../components/TripSummary";
import DestinationEvents from "../components/DestinationEvents";
import { errorMessage } from "../lib/errors";
import { fareFor, searchFromQuery, searchToQuery, useFlow } from "../lib/flow";
import { addDays, clockTime, dayNumber, duration, longDate, minutesOfDay, money, moneyShort, shortDate, todayIso, weekdayShort } from "../lib/format";
import styles from "./FlightsPage.module.css";

type Sort = "cheapest" | "fastest" | "earliest";
type TimeOfDay = "morning" | "afternoon" | "evening";

const timeOfDay = (iso: string): TimeOfDay => {
  const m = minutesOfDay(iso);
  return m < 12 * 60 ? "morning" : m < 18 * 60 ? "afternoon" : "evening";
};

export default function FlightsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { state, update, startSearch } = useFlow();

  const search = searchFromQuery(params);
  const leg = params.get("leg") === "return" ? "return" : "outbound";

  // Quem abre o link diretamente (ou faz refresh) recria a pesquisa no estado do fluxo.
  const searchKey = search ? searchToQuery(search) : "";
  useEffect(() => {
    if (!search) return;
    const prev = state.search;
    if (!prev) return startSearch(search);
    if (searchToQuery(prev) === searchKey) return;

    // Se só mudou a data de volta, o voo de ida escolhido continua válido.
    const sameOutbound =
      prev.from === search.from &&
      prev.to === search.to &&
      prev.depart === search.depart &&
      prev.adults === search.adults &&
      prev.cabin === search.cabin;

    if (sameOutbound) update({ search, inbound: undefined });
    else startSearch(search);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchKey]);

  const [sort, setSort] = useState<Sort>("cheapest");
  const [times, setTimes] = useState<Set<TimeOfDay>>(new Set(["morning", "afternoon", "evening"]));
  const [hiddenAirlines, setHiddenAirlines] = useState<Set<string>>(new Set());
  const [maxPrice, setMaxPrice] = useState<number | null>(null);

  const isReturn = leg === "return";
  const from = search ? (isReturn ? search.to : search.from) : "";
  const to = search ? (isReturn ? search.from : search.to) : "";
  const date = search ? (isReturn ? search.return ?? search.depart : search.depart) : "";
  const passengers = search?.adults ?? 1;

  const flights = useQuery({
    queryKey: ["search", from, to, date, passengers],
    queryFn: () => api.searchFlights(from, to, date, passengers),
    enabled: !!search,
  });

  const today = todayIso();
  const stripStart = addDays(date, -3) < today ? today : addDays(date, -3);
  const calendar = useQuery({
    queryKey: ["calendar", from, to, stripStart, passengers],
    queryFn: () => api.calendar(from, to, stripStart, 7, passengers),
    enabled: !!search,
  });

  // Ao mudar de dia ou de sentido, os filtros voltam ao início.
  useEffect(() => {
    setMaxPrice(null);
    setHiddenAirlines(new Set());
  }, [from, to, date]);

  const cabin = search?.cabin ?? "Economy";
  const all = flights.data ?? [];
  const priceOf = (f: FlightSummary) => fareFor(f, cabin).price;
  const highest = all.length ? Math.max(...all.map(priceOf)) : 0;
  const lowest = all.length ? Math.min(...all.map(priceOf)) : 0;
  const airlines = [...new Set(all.map((f) => f.airline))];

  const visible = useMemo(() => {
    const list = all.filter(
      (f) =>
        times.has(timeOfDay(f.departsAt)) &&
        !hiddenAirlines.has(f.airline) &&
        (maxPrice === null || priceOf(f) <= maxPrice),
    );
    const by: Record<Sort, (a: FlightSummary, b: FlightSummary) => number> = {
      cheapest: (a, b) => priceOf(a) - priceOf(b),
      fastest: (a, b) => a.durationMinutes - b.durationMinutes,
      earliest: (a, b) => a.departsAt.localeCompare(b.departsAt),
    };
    return [...list].sort(by[sort]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [all, times, hiddenAirlines, maxPrice, sort, cabin]);

  if (!search) return <Navigate to="/" replace />;
  if (isReturn && !state.outbound) return <Navigate to={`/flights?${searchToQuery(search, "outbound")}`} replace />;

  const roundTrip = search.return !== undefined;
  const selectedId = isReturn ? state.inbound?.id : state.outbound?.id;

  const cheapestId = all.length ? [...all].sort((a, b) => priceOf(a) - priceOf(b))[0].id : undefined;
  const earliestId = all.length ? [...all].sort((a, b) => a.departsAt.localeCompare(b.departsAt))[0].id : undefined;

  const choose = (flight: FlightSummary) => {
    if (isReturn) {
      update({ inbound: flight, seats: { ...state.seats, inbound: [] } });
      navigate("/booking/passengers");
    } else {
      update({ outbound: flight, inbound: undefined, seats: { outbound: [], inbound: [] } });
      navigate(roundTrip ? `/flights?${searchToQuery(search, "return")}` : "/booking/passengers");
    }
  };

  const goToDate = (day: string) => {
    const next = { ...search };
    if (isReturn) {
      next.return = day;
    } else {
      next.depart = day;
      if (next.return && next.return < day) next.return = day;
    }
    navigate(`/flights?${searchToQuery(next, leg)}`);
  };

  const toggle = <T,>(set: Set<T>, value: T) => {
    const copy = new Set(set);
    if (copy.has(value)) copy.delete(value);
    else copy.add(value);
    return copy;
  };

  const sortValue = (s: Sort) => {
    if (!all.length) return "—";
    if (s === "cheapest") return money(lowest);
    if (s === "fastest") return duration(Math.min(...all.map((f) => f.durationMinutes)));
    return clockTime([...all].sort((a, b) => a.departsAt.localeCompare(b.departsAt))[0].departsAt);
  };

  return (
    <main>
      <div className={styles.bar}>
        <div className={`wrap ${styles.barInner}`}>
          <span className={`${styles.chip} ${styles.codes}`}>
            {search.from} {roundTrip ? "⇄" : "→"} {search.to}
          </span>
          <span className={styles.chip}>
            {roundTrip ? `${shortDate(search.depart)} — ${shortDate(search.return!)}` : shortDate(search.depart)}
          </span>
          <span className={styles.chip}>
            {t("search.adults", { count: search.adults })} · {t(`cabin.${search.cabin}`)}
          </span>
          <Link to="/" className={`btn btn-primary ${styles.edit}`}>
            {t("results.edit")}
          </Link>
        </div>
      </div>

      <div className="wrap page">
        <Steps current={isReturn ? "return" : "outbound"} roundTrip={roundTrip} />

        <div className={styles.dates} role="tablist">
          {calendar.data?.map((d) => (
            <button
              key={d.date}
              type="button"
              role="tab"
              aria-selected={d.date === date}
              className={`${styles.day} ${d.date === date ? styles.dayOn : ""}`}
              onClick={() => goToDate(d.date)}
              disabled={isReturn && d.date < search.depart}
            >
              <small>
                {weekdayShort(d.date)} {dayNumber(d.date)}
              </small>
              <b className={d.minPrice !== null && calendar.data && d.minPrice <= Math.min(...calendar.data.filter((x) => x.minPrice !== null).map((x) => x.minPrice!)) ? styles.low : ""}>
                {d.minPrice === null ? "—" : moneyShort(d.minPrice)}
              </b>
            </button>
          ))}
          {calendar.isLoading && Array.from({ length: 7 }, (_, i) => <span key={i} className={styles.day} />)}
        </div>

        <div className={styles.layout}>
          <aside className={styles.filters}>
            <div className="panel">
              <h3>{t("results.departureTime")}</h3>
              {(
                [
                  ["morning", "06–12h"],
                  ["afternoon", "12–18h"],
                  ["evening", "18–24h"],
                ] as const
              ).map(([key, range]) => (
                <label key={key} className={styles.check}>
                  <input type="checkbox" checked={times.has(key)} onChange={() => setTimes(toggle(times, key))} />
                  {t(`results.${key}`)}
                  <span>{range}</span>
                </label>
              ))}
            </div>

            {airlines.length > 0 && (
              <div className="panel">
                <h3>{t("results.airlines")}</h3>
                {airlines.map((name) => {
                  const min = Math.min(...all.filter((f) => f.airline === name).map(priceOf));
                  return (
                    <label key={name} className={styles.check}>
                      <input
                        type="checkbox"
                        checked={!hiddenAirlines.has(name)}
                        onChange={() => setHiddenAirlines(toggle(hiddenAirlines, name))}
                      />
                      {name}
                      <span>{moneyShort(min)}</span>
                    </label>
                  );
                })}
              </div>
            )}

            {all.length > 1 && highest > lowest && (
              <div className="panel">
                <h3>{t("results.maxPrice")}</h3>
                <input
                  type="range"
                  className={styles.range}
                  min={Math.floor(lowest)}
                  max={Math.ceil(highest)}
                  step={1}
                  value={maxPrice ?? Math.ceil(highest)}
                  onChange={(e) => setMaxPrice(Number(e.target.value))}
                />
                <div className={styles.rangeLabels}>
                  <span>{moneyShort(lowest)}</span>
                  <span>{t("results.upTo", { price: moneyShort(maxPrice ?? highest) })}</span>
                </div>
              </div>
            )}
          </aside>

          <section>
            <div className={styles.listHead}>
              <h1 className={styles.routeTitle}>
                {from} → {to}
              </h1>
              <p>
                {longDate(date)}
                {flights.data && ` · ${t("results.count", { count: flights.data.length })}`}
              </p>
            </div>

            <div className={styles.sort} role="tablist">
              {(["cheapest", "fastest", "earliest"] as const).map((s) => (
                <button key={s} type="button" role="tab" aria-selected={sort === s} onClick={() => setSort(s)}>
                  <small>{t(`results.${s}`)}</small>
                  <b>{sortValue(s)}</b>
                </button>
              ))}
            </div>

            {flights.isLoading &&
              Array.from({ length: 3 }, (_, i) => <div key={i} className={styles.skeleton} />)}

            {flights.isError && (
              <div className={styles.message}>
                <AlertIcon />
                <div>
                  <b>{errorMessage(flights.error, t)}</b>
                  <button type="button" className="btn btn-ghost" onClick={() => flights.refetch()}>
                    {t("common.retry")}
                  </button>
                </div>
              </div>
            )}

            {flights.data && flights.data.length === 0 && (
              <div className={styles.message}>
                <AlertIcon />
                <div>
                  <b>{t("results.noneTitle")}</b>
                  <p>{t("results.noneText")}</p>
                </div>
              </div>
            )}

            {flights.data && flights.data.length > 0 && visible.length === 0 && (
              <div className={styles.message}>
                <AlertIcon />
                <div>
                  <b>{t("results.noneFiltered")}</b>
                  <button
                    type="button"
                    className="btn btn-ghost"
                    onClick={() => {
                      setTimes(new Set(["morning", "afternoon", "evening"]));
                      setHiddenAirlines(new Set());
                      setMaxPrice(null);
                    }}
                  >
                    {t("results.clearFilters")}
                  </button>
                </div>
              </div>
            )}

            {visible.map((f) => (
              <FlightCard
                key={f.id}
                flight={f}
                cabin={cabin}
                selected={f.id === selectedId}
                tags={[
                  ...(f.id === cheapestId && all.length > 1 ? [t("results.cheapest")] : []),
                  ...(f.id === earliestId && f.id !== cheapestId && all.length > 1 ? [t("results.earliest")] : []),
                ]}
                onSelect={() => choose(f)}
              />
            ))}
          </section>

          <TripSummary search={search} outbound={state.outbound} inbound={state.inbound} />
        </div>

        <DestinationEvents city={search.to} from={search.depart} to={search.return ?? addDays(search.depart, 7)} />
      </div>
    </main>
  );
}
