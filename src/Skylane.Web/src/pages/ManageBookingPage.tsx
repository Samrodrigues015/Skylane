import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useSearchParams } from "react-router";
import { api } from "../api/client";
import BookingSummary from "../components/BookingSummary";
import { AlertIcon, ArrowRightIcon, SearchIcon } from "../components/Icons";
import { errorMessage } from "../lib/errors";
import { useFlow } from "../lib/flow";
import { clockTime, isoDate, shortDate } from "../lib/format";
import { forgetBooking, recentBookings, rememberBooking } from "../lib/recentBookings";
import { useCountdown } from "../lib/useCountdown";
import checkout from "./Checkout.module.css";
import styles from "./ManageBookingPage.module.css";

const clean = (v: string) => v.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 6);

export default function ManageBookingPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { update } = useFlow();
  const [params, setParams] = useSearchParams();
  const [recent, setRecent] = useState(recentBookings);
  // Sem referência no link, abre a última reserva deste browser: sair e voltar à página não perde nada.
  const reference = params.get("ref") ?? recent[0]?.reference ?? "";
  const [input, setInput] = useState(reference);

  const query = useQuery({
    queryKey: ["booking", reference],
    queryFn: () => api.getBooking(reference),
    enabled: reference.length === 6,
    retry: false,
  });
  const booking = query.data;

  useEffect(() => {
    if (!booking) return;
    rememberBooking(booking);
    setRecent(recentBookings());
  }, [booking]);

  const open = (ref: string) => {
    setInput(ref);
    setParams({ ref });
  };

  const forget = (ref: string) => {
    forgetBooking(ref);
    const rest = recentBookings();
    setRecent(rest);
    if (ref === reference) {
      setInput("");
      setParams(rest[0] ? { ref: rest[0].reference } : { ref: "" }, { replace: true });
    }
  };
  const countdown = useCountdown(booking?.status === "Pending" ? booking.expiresAt : undefined);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (input.length === 6) open(input);
  };

  const continuePayment = () => {
    update({ booking });
    navigate("/booking/payment");
  };

  const status = booking && (booking.status === "Pending" && countdown.expired ? "Expired" : booking.status);

  return (
    <main className="wrap page">
      <h1 className="page-title">{t("manage.title")}</h1>
      <p className="page-lead">{t("manage.lead")}</p>

      <form className={`panel ${styles.search}`} onSubmit={submit}>
        <label className={checkout.field}>
          <span>{t("manage.reference")}</span>
          <input
            value={input}
            placeholder="AB12CD"
            maxLength={6}
            autoCapitalize="characters"
            spellCheck={false}
            onChange={(e) => setInput(clean(e.target.value))}
          />
        </label>
        <button type="submit" className="btn btn-primary" disabled={input.length !== 6}>
          <SearchIcon /> {t("manage.find")}
        </button>
      </form>

      {recent.length > 0 && (
        <section className={styles.recent}>
          <h2>{t("manage.recentTitle")}</h2>
          <ul>
            {recent.map((r) => (
              <li key={r.reference} className={r.reference === reference ? styles.current : undefined}>
                <button type="button" className={styles.recentOpen} onClick={() => open(r.reference)}>
                  <b>{r.reference}</b>
                  <span>{r.title}</span>
                  {r.date && (
                    <small>
                      {shortDate(isoDate(r.date))} · {clockTime(r.date)}
                    </small>
                  )}
                </button>
                <button
                  type="button"
                  className={styles.recentForget}
                  aria-label={t("manage.forget", { reference: r.reference })}
                  title={t("manage.forget", { reference: r.reference })}
                  onClick={() => forget(r.reference)}
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
          <p>{t("manage.recentNote")}</p>
        </section>
      )}

      {query.isLoading && <p>{t("common.loading")}</p>}

      {query.isError && (
        <div className={checkout.alert} role="alert">
          <AlertIcon />
          <span>{errorMessage(query.error, t)}</span>
        </div>
      )}

      {booking && status && (
        <div className={styles.result}>
          <BookingSummary booking={booking}>
            <div className={`${styles.status} ${styles[status]}`}>
              <span>{t(`manage.status.${status}`)}</span>
              {status === "Pending" && <b>{countdown.label}</b>}
            </div>

            {status === "Confirmed" && (
              <Link to={`/booking/${booking.reference}/confirmation`} className={`btn btn-blue ${checkout.fullBtn}`}>
                {t("manage.showPasses")} <ArrowRightIcon />
              </Link>
            )}
            {status === "Pending" && (
              <button type="button" className={`btn btn-primary ${checkout.fullBtn}`} onClick={continuePayment}>
                {t("manage.continuePayment")} <ArrowRightIcon />
              </button>
            )}
            {status === "Expired" && <p className={styles.note}>{t("manage.noSeats")}</p>}
          </BookingSummary>
        </div>
      )}
    </main>
  );
}
