import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useParams } from "react-router";
import { api } from "../api/client";
import BoardingPass from "../components/BoardingPass";
import { AlertIcon, CheckIcon, PrinterIcon } from "../components/Icons";
import { errorMessage } from "../lib/errors";
import { useFlow } from "../lib/flow";
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

  return (
    <main className="wrap page">
      <section className={styles.hero}>
        <span className={styles.check}>
          <CheckIcon width={28} height={28} />
        </span>
        <div>
          <h1>{t("confirmation.title", { name: firstName })}</h1>
          <p>{t("confirmation.lead", { email: booking.contactEmail })}</p>
        </div>
        <div className={styles.ref}>
          <small>{t("confirmation.reference")}</small>
          <b>{booking.reference}</b>
        </div>
      </section>

      <div className={`${styles.actions} no-print`}>
        <button type="button" className="btn btn-blue" onClick={() => window.print()}>
          <PrinterIcon /> {t("confirmation.print")}
        </button>
        <Link to="/" className="btn btn-ghost">
          {t("confirmation.newSearch")}
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
    </main>
  );
}
