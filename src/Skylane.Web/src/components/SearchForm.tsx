import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { Airport, CabinClass } from "../api/types";
import type { SearchParams } from "../lib/flow";
import { addDays, shortDate, todayIso } from "../lib/format";
import { usePopover } from "../lib/usePopover";
import { ArrowRightIcon, CheckIcon, MinusIcon, PlusIcon, SearchIcon } from "./Icons";
import styles from "./SearchForm.module.css";

interface Props {
  airports: Airport[];
  value: SearchParams;
  onChange: (value: SearchParams) => void;
  onSubmit: (value: SearchParams) => void;
}

/** Caixa de pesquisa de voos, ao estilo dos comparadores de voos. */
export default function SearchForm({ airports, value, onChange, onSubmit }: Props) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const roundTrip = value.return !== undefined;
  const today = todayIso();

  const set = (patch: Partial<SearchParams>) => {
    setError(null);
    onChange({ ...value, ...patch });
  };

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (value.from === value.to) return setError(t("search.sameAirports"));
    if (value.return && value.return < value.depart) return setError(t("search.returnBeforeDepart"));
    onSubmit(value);
  };

  return (
    <form className={styles.search} onSubmit={submit}>
      <div className={styles.trip} role="radiogroup">
        <button
          type="button"
          role="radio"
          aria-checked={roundTrip}
          onClick={() => set({ return: value.return ?? addDays(value.depart, 5) })}
        >
          {t("search.roundTrip")}
        </button>
        <button type="button" role="radio" aria-checked={!roundTrip} onClick={() => set({ return: undefined })}>
          {t("search.oneWay")}
        </button>
      </div>

      <div className={`${styles.fields} ${roundTrip ? "" : styles.oneWay}`}>
        <AirportField
          label={t("search.from")}
          airports={airports}
          code={value.from}
          exclude={value.to}
          onPick={(from) => set({ from })}
        />
        <AirportField
          label={t("search.to")}
          airports={airports}
          code={value.to}
          exclude={value.from}
          onPick={(to) => set({ to })}
        />

        <label className={styles.field}>
          <small>{t("search.depart")}</small>
          <b>{shortDate(value.depart)}</b>
          <em>{value.depart.slice(0, 4)}</em>
          <input
            type="date"
            className={styles.dateInput}
            value={value.depart}
            min={today}
            onChange={(e) => {
              const depart = e.target.value || today;
              set({ depart, return: value.return && value.return < depart ? depart : value.return });
            }}
          />
        </label>

        {roundTrip && (
          <label className={styles.field}>
            <small>{t("search.return")}</small>
            <b>{shortDate(value.return!)}</b>
            <em>{value.return!.slice(0, 4)}</em>
            <input
              type="date"
              className={styles.dateInput}
              value={value.return}
              min={value.depart}
              onChange={(e) => set({ return: e.target.value || value.depart })}
            />
          </label>
        )}

        <TravellersField adults={value.adults} cabin={value.cabin} onChange={(adults, cabin) => set({ adults, cabin })} />

        <button type="submit" className={styles.go}>
          {t("search.submit")} <ArrowRightIcon />
        </button>
      </div>

      {error && (
        <p className={styles.error} role="alert">
          {error}
        </p>
      )}

      <div className={styles.trust}>
        <span>
          <CheckIcon /> {t("search.trustSeat")}
        </span>
        <span>
          <CheckIcon /> {t("search.trustHold")}
        </span>
        <span>
          <CheckIcon /> {t("search.trustFees")}
        </span>
      </div>
    </form>
  );
}

function AirportField(props: {
  label: string;
  airports: Airport[];
  code: string;
  exclude: string;
  onPick: (code: string) => void;
}) {
  const { t } = useTranslation();
  const { open, setOpen, ref } = usePopover();
  const [query, setQuery] = useState("");
  const current = props.airports.find((a) => a.code === props.code);

  const q = query.trim().toLowerCase();
  const options = props.airports.filter(
    (a) =>
      a.code !== props.exclude &&
      (!q || a.city.toLowerCase().includes(q) || a.code.toLowerCase().includes(q) || a.name.toLowerCase().includes(q)),
  );

  return (
    <div className={styles.popoverHost} ref={ref}>
      <button
        type="button"
        className={styles.field}
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => {
          setQuery("");
          setOpen(!open);
        }}
      >
        <small>{props.label}</small>
        <b>{current ? `${current.city} (${current.code})` : props.code}</b>
        <em>{current?.country ?? " "}</em>
      </button>

      {open && (
        <div className={styles.popover}>
          <div className={styles.filter}>
            <SearchIcon />
            <input
              autoFocus
              placeholder={t("search.searchAirport")}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && options[0]) {
                  e.preventDefault();
                  props.onPick(options[0].code);
                  setOpen(false);
                }
              }}
            />
          </div>
          <ul role="listbox" className={styles.options}>
            {options.length === 0 && <li className={styles.empty}>{t("search.noAirport")}</li>}
            {options.map((a) => (
              <li key={a.code}>
                <button
                  type="button"
                  role="option"
                  aria-selected={a.code === props.code}
                  onClick={() => {
                    props.onPick(a.code);
                    setOpen(false);
                  }}
                >
                  <span className={styles.code}>{a.code}</span>
                  <span>
                    <b>{a.city}</b>
                    <small>{a.name}</small>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function TravellersField(props: { adults: number; cabin: CabinClass; onChange: (adults: number, cabin: CabinClass) => void }) {
  const { t } = useTranslation();
  const { open, setOpen, ref } = usePopover();

  return (
    <div className={styles.popoverHost} ref={ref}>
      <button type="button" className={styles.field} aria-expanded={open} onClick={() => setOpen(!open)}>
        <small>{t("search.passengersAndClass")}</small>
        <b>{t("search.adults", { count: props.adults })}</b>
        <em>{t(`cabin.${props.cabin}`)}</em>
      </button>

      {open && (
        <div className={`${styles.popover} ${styles.travellers}`}>
          <div className={styles.counter}>
            <div>
              <b>{t("search.adultsLabel")}</b>
              <small>{t("search.adultsHint")}</small>
            </div>
            <Stepper value={props.adults} min={1} max={9} onChange={(n) => props.onChange(n, props.cabin)} />
          </div>
          <div className={styles.cabins}>
            <small>{t("search.cabin")}</small>
            {(["Economy", "Business"] as const).map((c) => (
              <label key={c} className={styles.radio}>
                <input
                  type="radio"
                  name="cabin"
                  checked={props.cabin === c}
                  onChange={() => props.onChange(props.adults, c)}
                />
                {t(`cabin.${c}`)}
              </label>
            ))}
          </div>
          <button type="button" className="btn btn-blue" onClick={() => setOpen(false)}>
            {t("search.done")}
          </button>
        </div>
      )}
    </div>
  );
}

function Stepper(props: { value: number; min: number; max: number; onChange: (n: number) => void }): ReactNode {
  return (
    <div className={styles.stepper}>
      <button
        type="button"
        aria-label="−"
        disabled={props.value <= props.min}
        onClick={() => props.onChange(props.value - 1)}
      >
        <MinusIcon />
      </button>
      <span aria-live="polite">{props.value}</span>
      <button
        type="button"
        aria-label="+"
        disabled={props.value >= props.max}
        onClick={() => props.onChange(props.value + 1)}
      >
        <PlusIcon />
      </button>
    </div>
  );
}
