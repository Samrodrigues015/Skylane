import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link, Navigate, useNavigate } from "react-router";
import { api } from "../api/client";
import type { BookingResponse, CardPayment } from "../api/types";
import BookingSummary from "../components/BookingSummary";
import CardForm from "../components/CardForm";
import { AlertIcon, ClockIcon, LockIcon } from "../components/Icons";
import Steps from "../components/Steps";
import TripEvents from "../components/TripEvents";
import { errorMessage } from "../lib/errors";
import { useFlow } from "../lib/flow";
import { money } from "../lib/format";
import { useCountdown } from "../lib/useCountdown";
import checkout from "./Checkout.module.css";
import styles from "./PaymentPage.module.css";

export default function PaymentPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { state, update, reset } = useFlow();
  const booking = state.booking;

  const countdown = useCountdown(booking?.status === "Pending" ? booking.expiresAt : undefined);

  const pay = useMutation({
    mutationFn: (card: CardPayment) => api.payBooking(booking!.reference, card),
    onSuccess: (confirmed) => {
      update({ booking: confirmed });
      navigate(`/booking/${confirmed.reference}/confirmation`);
    },
  });

  // Tirar um evento da viagem (o "carrinho").
  const removeEvent = useMutation({
    mutationFn: (reservationId: string) => api.removeBookingEvent(booking!.reference, reservationId),
    onSuccess: (updated: BookingResponse) => update({ booking: updated }),
  });

  if (!booking) return <Navigate to="/" replace />;
  if (booking.status === "Confirmed") return <Navigate to={`/booking/${booking.reference}/confirmation`} replace />;

  const roundTrip = booking.legs.length > 1;
  const expired = booking.status === "Expired" || countdown.expired;

  if (expired) {
    return (
      <main className="wrap page">
        <Steps current="payment" roundTrip={roundTrip} />
        <section className={`panel ${styles.expired}`}>
          <ClockIcon width={40} height={40} />
          <h1>{t("payment.expiredTitle")}</h1>
          <p>{t("payment.expiredText")}</p>
          <Link to="/" className="btn btn-primary" onClick={() => reset()}>
            {t("payment.newSearch")}
          </Link>
        </section>
      </main>
    );
  }

  const error = pay.error ?? removeEvent.error;

  return (
    <main className="wrap page">
      <Steps current="payment" roundTrip={roundTrip} />

      <div className={checkout.layout}>
        <div>
          <div className={styles.titleRow}>
            <h1 className="page-title">{t("payment.title")}</h1>
            <div className={`${styles.timer} ${countdown.seconds < 120 ? styles.urgent : ""}`} role="timer">
              <ClockIcon />
              <span>{t("payment.heldFor")}</span>
              <b>{countdown.label}</b>
            </div>
          </div>

          {error && (
            <div className={checkout.alert} role="alert">
              <AlertIcon />
              <span>{errorMessage(error, t)}</span>
            </div>
          )}

          <TripEvents booking={booking} onChange={(updated) => update({ booking: updated })} />

          <section className={checkout.card}>
            <div className={checkout.cardHead}>
              <h2>{t("payment.card")}</h2>
              <span className={styles.brands} aria-hidden="true">
                <i>VISA</i>
                <i>MC</i>
              </span>
            </div>
            <CardForm
              id="card-form"
              holder={booking.passengers[0]}
              disabled={pay.isPending}
              onSubmit={(card) => pay.mutate(card)}
            />
          </section>

          <p className={styles.secure}>
            <LockIcon /> {t("payment.secure")}
          </p>
        </div>

        <BookingSummary
          booking={booking}
          onRemoveEvent={removeEvent.isPending ? undefined : (id) => removeEvent.mutate(id)}
        >
          <button type="submit" form="card-form" className={`btn btn-primary ${checkout.fullBtn}`} disabled={pay.isPending}>
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
