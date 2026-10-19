import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useNavigate } from "react-router";
import { api } from "../api/client";
import { AlertIcon, ClockIcon, LockIcon } from "../components/Icons";
import BookingSummary from "../components/BookingSummary";
import Steps from "../components/Steps";
import { errorMessage } from "../lib/errors";
import { useFlow } from "../lib/flow";
import { money } from "../lib/format";
import { useCountdown } from "../lib/useCountdown";
import checkout from "./Checkout.module.css";
import styles from "./PaymentPage.module.css";

const TEST_CARDS = [
  { number: "4242 4242 4242 4242", label: "payment.testApproved", tone: "ok" },
  { number: "4000 0000 0000 0002", label: "payment.testDeclined", tone: "bad" },
  { number: "4000 0000 0000 9995", label: "payment.testFunds", tone: "bad" },
] as const;

const digits = (v: string) => v.replace(/\D/g, "");
const formatCard = (v: string) => digits(v).slice(0, 19).replace(/(\d{4})(?=\d)/g, "$1 ");
function formatExpiry(v: string) {
  const d = digits(v).slice(0, 4);
  return d.length > 2 ? `${d.slice(0, 2)}/${d.slice(2)}` : d;
}

/** Validade de exemplo para os cartões de teste: daqui a dois anos. */
function sampleExpiry() {
  const now = new Date();
  return `${String(now.getMonth() + 1).padStart(2, "0")}/${String((now.getFullYear() + 2) % 100).padStart(2, "0")}`;
}

export default function PaymentPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { state, update, reset } = useFlow();
  const booking = state.booking;

  const [card, setCard] = useState({ number: "", holder: "", expiry: "", cvc: "" });
  const [submitted, setSubmitted] = useState(false);
  const countdown = useCountdown(booking?.status === "Pending" ? booking.expiresAt : undefined);

  const pay = useMutation({
    mutationFn: () =>
      api.payBooking(booking!.reference, {
        cardNumber: digits(card.number),
        cardHolder: card.holder.trim(),
        expiry: card.expiry,
        cvc: card.cvc,
      }),
    onSuccess: (confirmed) => {
      update({ booking: confirmed });
      navigate(`/booking/${confirmed.reference}/confirmation`);
    },
  });

  if (!booking) return <Navigate to="/" replace />;
  if (booking.status === "Confirmed") return <Navigate to={`/booking/${booking.reference}/confirmation`} replace />;

  const roundTrip = booking.legs.length > 1;
  const expired = booking.status === "Expired" || countdown.expired;

  const errors = {
    number: digits(card.number).length < 13,
    holder: card.holder.trim().length < 2,
    expiry: !/^\d{2}\/\d{2}$/.test(card.expiry),
    cvc: !/^\d{3,4}$/.test(card.cvc),
  };
  const valid = !Object.values(errors).some(Boolean);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
    if (valid && !expired) pay.mutate();
  };

  const fillTestCard = (number: string) => {
    setCard({
      number,
      holder: card.holder || booking.passengers[0] || "",
      expiry: card.expiry || sampleExpiry(),
      cvc: card.cvc || "123",
    });
  };

  const show = (bad: boolean) => submitted && bad;

  if (expired) {
    return (
      <main className="wrap page">
        <Steps current="payment" roundTrip={roundTrip} />
        <section className={`panel ${styles.expired}`}>
          <ClockIcon width={40} height={40} />
          <h1>{t("payment.expiredTitle")}</h1>
          <p>{t("payment.expiredText")}</p>
          <Link
            to="/"
            className="btn btn-primary"
            onClick={() => reset()}
          >
            {t("payment.newSearch")}
          </Link>
        </section>
      </main>
    );
  }

  return (
    <main className="wrap page">
      <Steps current="payment" roundTrip={roundTrip} />

      <div className={checkout.layout}>
        <form onSubmit={submit} noValidate>
          <div className={styles.titleRow}>
            <h1 className="page-title">{t("payment.title")}</h1>
            <div className={`${styles.timer} ${countdown.seconds < 120 ? styles.urgent : ""}`} role="timer">
              <ClockIcon />
              <span>{t("payment.heldFor")}</span>
              <b>{countdown.label}</b>
            </div>
          </div>

          {pay.isError && (
            <div className={checkout.alert} role="alert">
              <AlertIcon />
              <span>{errorMessage(pay.error, t)}</span>
            </div>
          )}

          <section className={checkout.card}>
            <div className={checkout.cardHead}>
              <h2>{t("payment.card")}</h2>
              <span className={styles.brands} aria-hidden="true">
                <i>VISA</i>
                <i>MC</i>
              </span>
            </div>

            <div className={styles.testCards}>
              <span>{t("payment.testCards")}</span>
              <div>
                {TEST_CARDS.map((c) => (
                  <button
                    key={c.number}
                    type="button"
                    className={`${styles.chip} ${styles[c.tone]}`}
                    onClick={() => fillTestCard(c.number)}
                  >
                    <code>{c.number}</code>
                    <small>{t(c.label)}</small>
                  </button>
                ))}
              </div>
            </div>

            <div className={styles.fields}>
              <label className={`${checkout.field} ${styles.wide}`}>
                <span>{t("payment.cardNumber")}</span>
                <input
                  inputMode="numeric"
                  autoComplete="cc-number"
                  placeholder="1234 5678 9012 3456"
                  value={card.number}
                  aria-invalid={show(errors.number) || undefined}
                  onChange={(e) => setCard({ ...card, number: formatCard(e.target.value) })}
                />
              </label>
              <label className={`${checkout.field} ${styles.wide}`}>
                <span>{t("payment.cardHolder")}</span>
                <input
                  autoComplete="cc-name"
                  value={card.holder}
                  aria-invalid={show(errors.holder) || undefined}
                  onChange={(e) => setCard({ ...card, holder: e.target.value })}
                />
              </label>
              <label className={checkout.field}>
                <span>{t("payment.expiry")}</span>
                <input
                  inputMode="numeric"
                  autoComplete="cc-exp"
                  placeholder={t("payment.expiryPlaceholder")}
                  value={card.expiry}
                  aria-invalid={show(errors.expiry) || undefined}
                  onChange={(e) => setCard({ ...card, expiry: formatExpiry(e.target.value) })}
                />
              </label>
              <label className={checkout.field}>
                <span>{t("payment.cvc")}</span>
                <input
                  inputMode="numeric"
                  autoComplete="cc-csc"
                  placeholder="123"
                  value={card.cvc}
                  aria-invalid={show(errors.cvc) || undefined}
                  onChange={(e) => setCard({ ...card, cvc: digits(e.target.value).slice(0, 4) })}
                />
              </label>
            </div>
            {submitted && !valid && <p className={styles.invalid}>{t("errors.validation")}</p>}
          </section>

          <p className={styles.secure}>
            <LockIcon /> {t("payment.secure")}
          </p>
        </form>

        <BookingSummary booking={booking}>
          <button
            type="button"
            className={`btn btn-primary ${checkout.fullBtn}`}
            disabled={pay.isPending}
            onClick={submit}
          >
            {pay.isPending ? (
              <>
                <span className={styles.spinner} aria-hidden="true" /> {t("payment.processing")}
              </>
            ) : (
              <>
                <LockIcon /> {t("payment.pay", { amount: money(booking.total, booking.currency) })}
              </>
            )}
          </button>
        </BookingSummary>
      </div>
    </main>
  );
}
