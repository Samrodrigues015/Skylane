import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Navigate, useNavigate } from "react-router";
import type { PassengerRequest } from "../api/types";
import { ArrowRightIcon } from "../components/Icons";
import Steps from "../components/Steps";
import TripSummary from "../components/TripSummary";
import { searchToQuery, useFlow } from "../lib/flow";
import styles from "./Checkout.module.css";

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function PassengersPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { state, update } = useFlow();
  const { search, outbound, inbound } = state;

  const count = search?.adults ?? 1;
  const [passengers, setPassengers] = useState<PassengerRequest[]>(() =>
    Array.from({ length: count }, (_, i) => state.passengers[i] ?? { firstName: "", lastName: "", documentNumber: "" }),
  );
  const [email, setEmail] = useState(state.contactEmail);
  const [submitted, setSubmitted] = useState(false);

  if (!search || !outbound) return <Navigate to="/" replace />;
  const roundTrip = search.return !== undefined;
  if (roundTrip && !inbound) return <Navigate to={`/flights?${searchToQuery(search, "return")}`} replace />;

  const setField = (i: number, field: keyof PassengerRequest, value: string) =>
    setPassengers((list) => list.map((p, idx) => (idx === i ? { ...p, [field]: value } : p)));

  const missing = (value: string, min = 1) => value.trim().length < min;
  const emailInvalid = !EMAIL.test(email.trim());
  const valid =
    !emailInvalid &&
    passengers.every((p) => !missing(p.firstName) && !missing(p.lastName) && !missing(p.documentNumber, 3));

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
    if (!valid) return;
    update({ passengers, contactEmail: email.trim() });
    navigate("/booking/seats");
  };

  const showError = (bad: boolean) => submitted && bad;

  return (
    <main className="wrap page">
      <Steps current="passengers" roundTrip={roundTrip} />

      <div className={styles.layout}>
        <form onSubmit={submit} noValidate>
          <h1 className="page-title">{t("passengers.title")}</h1>
          <p className="page-lead">{t("passengers.lead")}</p>

          {passengers.map((p, i) => (
            <section key={i} className={styles.card}>
              <div className={styles.cardHead}>
                <span className={styles.badge}>{i + 1}</span>
                <div>
                  <h2>{t("passengers.passenger", { n: i + 1 })}</h2>
                  <small>{t("passengers.adult")}</small>
                </div>
              </div>
              <div className={styles.grid3}>
                <Field
                  label={t("passengers.firstName")}
                  value={p.firstName}
                  autoComplete={i === 0 ? "given-name" : "off"}
                  error={showError(missing(p.firstName)) ? t("passengers.required") : undefined}
                  onChange={(v) => setField(i, "firstName", v)}
                />
                <Field
                  label={t("passengers.lastName")}
                  value={p.lastName}
                  autoComplete={i === 0 ? "family-name" : "off"}
                  error={showError(missing(p.lastName)) ? t("passengers.required") : undefined}
                  onChange={(v) => setField(i, "lastName", v)}
                />
                <Field
                  label={t("passengers.document")}
                  hint={t("passengers.documentHint")}
                  value={p.documentNumber}
                  error={showError(missing(p.documentNumber, 3)) ? t("passengers.required") : undefined}
                  onChange={(v) => setField(i, "documentNumber", v.toUpperCase())}
                />
              </div>
            </section>
          ))}

          <section className={styles.card}>
            <div className={styles.cardHead}>
              <h2>{t("passengers.contact")}</h2>
            </div>
            <div className={styles.grid2}>
              <Field
                label={t("passengers.email")}
                type="email"
                autoComplete="email"
                value={email}
                hint={t("passengers.emailHint")}
                error={showError(emailInvalid) ? t("passengers.invalidEmail") : undefined}
                onChange={setEmail}
              />
            </div>
          </section>

          <div className={styles.actions}>
            <button type="button" className="btn btn-ghost" onClick={() => navigate(-1)}>
              {t("seats.back")}
            </button>
            <button type="submit" className="btn btn-primary">
              {t("passengers.continue")} <ArrowRightIcon />
            </button>
          </div>
        </form>

        <TripSummary search={search} outbound={outbound} inbound={inbound} />
      </div>
    </main>
  );
}

function Field(props: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  hint?: string;
  error?: string;
  autoComplete?: string;
}) {
  return (
    <label className={styles.field}>
      <span>{props.label}</span>
      <input
        type={props.type ?? "text"}
        value={props.value}
        autoComplete={props.autoComplete}
        aria-invalid={props.error ? true : undefined}
        onChange={(e) => props.onChange(e.target.value)}
      />
      {props.error ? <small className={styles.error}>{props.error}</small> : props.hint && <small>{props.hint}</small>}
    </label>
  );
}
