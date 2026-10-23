import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useParams } from "react-router";
import { api } from "../api/client";
import BoardingPass from "../components/BoardingPass";
import DestinationEvents from "../components/DestinationEvents";
import { AlertIcon, CheckIcon, PrinterIcon } from "../components/Icons";
import { errorMessage } from "../lib/errors";
import { useFlow } from "../lib/flow";
import { useRememberBooking } from "../lib/recentBookings";
import { addDays, clockTime, isoDate, shortDate } from "../lib/format";
import { QRCodeSVG } from "qrcode.react";
import styles from "./ConfirmationPage.module.css";

export default function ConfirmationPage() {
  const { t } = useTranslation();
  const { reference = "" } = useParams();
  const { state, reset } = useFlow();

  // Se acabámos de pagar, já temos a reserva no fluxo; senão vamos buscá-la (ex.: link direto).
  const cached = state.booking?.reference === reference ? state.booking : undefined;
  const query = useQuery({
    queryKey: ["booking", reference],
    queryFn: () => api.getBooking(reference),
    initialData: cached?.status === "Confirmed" ? cached : undefined,
  });

  useRememberBooking(query.data);

  // A compra terminou: limpamos o fluxo para a próxima pesquisa começar do zero.
  useEffect(() => {
    if (query.data?.status === "Confirmed" && state.booking?.reference === reference) reset();
  }, [query.data?.status, reference, reset, state.booking?.reference]);

  if (query.isLoading) return <main className="wrap page">{t("common.loading")}</main>;

  if (query.isError || !query.data) {
    return (
      <main className="wrap page">
        <div className={styles.error} role="alert">
          <AlertIcon /> {errorMessage(query.error, t)}
        </div>
        <Link to="/manage" className="btn btn-ghost">
          {t("manage.title")}
        </Link>
      </main>
    );
  }

  const booking = query.data;
  if (booking.status !== "Confirmed") return <Navigate to={`/manage?ref=${booking.reference}`} replace />;

  const firstName = booking.passengers[0]?.split(" ")[0] ?? "";
  const outbound = booking.legs.find((l) => l.direction === "Outbound") ?? booking.legs[0];
  const hasFlights = booking.legs.length > 0;
  const inbound = booking.legs.find((l) => l.direction === "Return");

  return (
    <main className="wrap page">
      <section className={styles.hero}>
        <span className={styles.check}>
          <CheckIcon width={28} height={28} />
        </span>
        <div>
          <h1>{hasFlights ? t("confirmation.title", { name: firstName }) : t("confirmation.ticketsTitle")}</h1>
          <p>{t(hasFlights ? "confirmation.lead" : "confirmation.ticketsLead", { email: booking.contactEmail })}</p>
        </div>
        <div className={styles.ref}>
          <small>{t("confirmation.reference")}</small>
          <b>{booking.reference}</b>
        </div>
      </section>

      <div className={`${styles.actions} no-print`}>
        <button type="button" className="btn btn-blue" onClick={() => window.print()}>
          <PrinterIcon /> {t(hasFlights ? "confirmation.print" : "confirmation.printTickets")}
        </button>
        <Link to={hasFlights ? "/" : "/events"} className="btn btn-ghost">
          {t(hasFlights ? "confirmation.newSearch" : "cart.seeEvents")}
        </Link>
      </div>

      {booking.legs.map((leg) => (
        <section key={leg.flightId} className={styles.leg}>
          <h2>
            {t(leg.direction === "Outbound" ? "summary.outbound" : "summary.return")} · {leg.origin.city} →{" "}
            {leg.destination.city}
          </h2>
          <div className={styles.passes}>
            {leg.seats.map((seat) => (
              <BoardingPass key={seat.seatNumber} reference={booking.reference} leg={leg} seat={seat} />
            ))}
          </div>
        </section>
      ))}

      {booking.events.length > 0 && (
        <section className={styles.leg}>
          <h2>{t("tripEvents.yourTickets")}</h2>
          <div className={styles.eventTickets}>
            {booking.events.flatMap((ev) =>
              ev.tickets.map((code) => (
                <article key={code} className={styles.eventTicket}>
                  <QRCodeSVG value={code} size={92} marginSize={0} />
                  <div>
                    <b>{ev.name}</b>
                    <span>
                      {shortDate(isoDate(ev.startsAt))} · {clockTime(ev.startsAt)}
                    </span>
                    <span>
                      {ev.venue} · {ev.city}
                    </span>
                    <code>{code}</code>
                  </div>
                </article>
              )),
            )}
          </div>
        </section>
      )}

      {hasFlights && (
        <DestinationEvents
          city={outbound.destination.code}
          from={isoDate(outbound.arrivesAt)}
          to={inbound ? isoDate(inbound.departsAt) : addDays(isoDate(outbound.arrivesAt), 7)}
          exclude={booking.events.map((e) => e.eventId)}
        />
      )}
    </main>
  );
}
