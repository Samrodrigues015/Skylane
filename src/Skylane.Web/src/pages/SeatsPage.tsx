import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate, useNavigate } from "react-router";
import { api, ApiError } from "../api/client";
import type { CabinClass, FlightSummary, SeatInfo } from "../api/types";
import { AlertIcon, ArrowRightIcon } from "../components/Icons";
import SeatMap from "../components/SeatMap";
import Steps from "../components/Steps";
import TripSummary from "../components/TripSummary";
import { errorMessage } from "../lib/errors";
import { cartSelections, useCart } from "../lib/cart";
import { useFlow } from "../lib/flow";
import { shortDate, isoDate } from "../lib/format";
import checkout from "./Checkout.module.css";
import styles from "./SeatsPage.module.css";

type LegKey = "outbound" | "inbound";

export default function SeatsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { state, update } = useFlow();
  const cart = useCart();
  const { search, outbound, inbound, passengers } = state;

  const count = passengers.length;
  const fill = (list: string[]) => Array.from({ length: count }, (_, i) => list[i] ?? "");

  const [leg, setLeg] = useState<LegKey>("outbound");
  const [active, setActive] = useState(0);
  const [seats, setSeats] = useState({ outbound: fill(state.seats.outbound), inbound: fill(state.seats.inbound) });
  const [error, setError] = useState<string | null>(null);

  const flight: FlightSummary | undefined = leg === "outbound" ? outbound : inbound;

  const seatMap = useQuery({
    queryKey: ["seatmap", flight?.id],
    queryFn: () => api.seatMap(flight!.id),
    enabled: !!flight,
    staleTime: 0,
  });

  const book = useMutation({
    mutationFn: () =>
      api.createBooking({
        contactEmail: state.contactEmail,
        passengers,
        outbound: { flightId: outbound!.id, seats: seats.outbound },
        return: inbound ? { flightId: inbound.id, seats: seats.inbound } : undefined,
        // Os bilhetes do carrinho vão no mesmo pedido: ou fica tudo reservado, ou nada.
        events: cartSelections(cart.items),
      }),
    onSuccess: (booking) => {
      update({ booking, seats });
      cart.clear();
      navigate("/booking/payment");
    },
    onError: (err) => {
      setError(errorMessage(err, t));
      // Alguém ficou com um dos lugares entretanto: recarregamos o mapa para o mostrar ocupado.
      if (err instanceof ApiError && err.code === "seat_unavailable") void seatMap.refetch();
    },
  });

  if (!search || !outbound || count === 0) return <Navigate to="/" replace />;
  const roundTrip = search.return !== undefined;
  const cabin: CabinClass = search.cabin;

  const legSeats = seats[leg];
  const assigned: Record<string, number> = {};
  legSeats.forEach((seat, i) => seat && (assigned[seat] = i));

  const prices = Object.fromEntries((flight?.fares ?? []).map((f) => [f.class, f.price])) as Record<CabinClass, number>;

  const clickSeat = (seat: SeatInfo) => {
    setError(null);
    const next = [...legSeats];
    const owner = assigned[seat.seatNumber];

    if (owner === active) {
      next[active] = ""; // clicar no próprio lugar tira-o
    } else {
      if (owner !== undefined) next[owner] = ""; // o lugar passa para o passageiro ativo
      next[active] = seat.seatNumber;
    }

    setSeats({ ...seats, [leg]: next });

    // Avança para o próximo passageiro sem lugar, para escolher rápido.
    const nextFree = next.findIndex((s, i) => !s && i !== active);
    if (next[active] && nextFree !== -1) setActive(nextFree);
  };

  const complete = (list: string[]) => list.every(Boolean);
  const allDone = complete(seats.outbound) && (!roundTrip || complete(seats.inbound));

  const submit = () => {
    if (!allDone) {
      setError(t("seats.missing"));
      if (!complete(seats.outbound)) setLeg("outbound");
      else setLeg("inbound");
      return;
    }
    setError(null);
    book.mutate();
  };

  const legTab = (key: LegKey, f?: FlightSummary) =>
    f && (
      <button
        type="button"
        role="tab"
        aria-selected={leg === key}
        onClick={() => {
          setLeg(key);
          setActive(Math.max(0, seats[key].findIndex((s) => !s)));
        }}
      >
        {t(key === "outbound" ? "summary.outbound" : "summary.return")}
        <small>
          {f.origin.code} → {f.destination.code} · {shortDate(isoDate(f.departsAt))}
        </small>
      </button>
    );

  return (
    <main className="wrap page">
      <Steps current="seats" roundTrip={roundTrip} />

      <div className={styles.layout}>
        <aside className={`panel ${styles.side}`}>
          {roundTrip && (
            <div className={styles.legs} role="tablist">
              {legTab("outbound", outbound)}
              {legTab("inbound", inbound)}
            </div>
          )}

          <div className={styles.pax}>
            {passengers.map((p, i) => (
              <button
                key={i}
                type="button"
                className={`${styles.passenger} ${active === i ? styles.active : ""}`}
                aria-pressed={active === i}
                onClick={() => setActive(i)}
              >
                <span className={styles.avatar}>{i + 1}</span>
                <span className={styles.name}>
                  <b>
                    {p.firstName} {p.lastName}
                  </b>
                  <small>{t("passengers.adult")}</small>
                </span>
                {legSeats[i] ? (
                  <span className={styles.seatChip}>{legSeats[i]}</span>
                ) : (
                  <span className={styles.seatEmpty}>{t("seats.chooseOnMap")}</span>
                )}
              </button>
            ))}
          </div>
          <p className={styles.hint}>{t("seats.hint")}</p>
        </aside>

        <section className={`panel ${styles.plane}`}>
          <div className={styles.planeHead}>
            <div>
              <b>
                {flight?.number} · {flight?.origin.city} → {flight?.destination.city}
              </b>
              <span>
                {t("results.aircraft")} · {t("seats.seatsCount", { count: seatMap.data?.seats.length ?? 174 })}
              </span>
            </div>
            <div className={styles.legend}>
              <span>
                <i className={styles.lBusiness} />
                {t("cabin.Business")}
              </span>
              <span>
                <i className={styles.lEconomy} />
                {t("cabin.Economy")}
              </span>
              <span>
                <i className={styles.lTaken} />
                {t("seats.occupied")}
              </span>
              <span>
                <i className={styles.lMine} />
                {t("seats.yours")}
              </span>
            </div>
          </div>

          {seatMap.isLoading && <p className={styles.loading}>{t("common.loading")}</p>}
          {seatMap.data && (
            <SeatMap seats={seatMap.data.seats} cabin={cabin} prices={prices} assigned={assigned} onSeatClick={clickSeat} />
          )}
        </section>

        <div>
          {error && (
            <div className={checkout.alert} role="alert">
              <AlertIcon />
              <span>{error}</span>
            </div>
          )}
          <TripSummary search={search} outbound={outbound} inbound={inbound} showHoldInfo={false}>
            <div className={styles.free}>
              <span>{t("summary.seatSelection")}</span>
              <b>{t("summary.free")}</b>
            </div>
            <p className={styles.notice}>{t("seats.holdNotice")}</p>
            <button
              type="button"
              className={`btn btn-primary ${checkout.fullBtn}`}
              onClick={submit}
              disabled={book.isPending}
            >
              {t("seats.continue")} <ArrowRightIcon />
            </button>
          </TripSummary>
        </div>
      </div>
    </main>
  );
}
