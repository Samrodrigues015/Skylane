import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { CardPayment } from "../api/types";
import checkout from "../pages/Checkout.module.css";
import styles from "./CardForm.module.css";

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

interface Props {
  /** O botão de pagar pode estar fora do formulário e apontar para ele com form={id}. */
  id: string;
  /** Nome a sugerir quando se escolhe um cartão de teste. */
  holder?: string;
  disabled?: boolean;
  onSubmit: (card: CardPayment) => void;
}

/**
 * Formulário de cartão, usado no pagamento das viagens e dos bilhetes para eventos.
 * Os cartões de teste preenchem tudo de uma vez (nenhum valor é cobrado).
 */
export default function CardForm({ id, holder = "", disabled, onSubmit }: Props) {
  const { t } = useTranslation();
  const [card, setCard] = useState({ number: "", holder: "", expiry: "", cvc: "" });
  const [submitted, setSubmitted] = useState(false);

  const errors = {
    number: digits(card.number).length < 13,
    holder: card.holder.trim().length < 2,
    expiry: !/^\d{2}\/\d{2}$/.test(card.expiry),
    cvc: !/^\d{3,4}$/.test(card.cvc),
  };
  const valid = !Object.values(errors).some(Boolean);
  const show = (bad: boolean) => (submitted && bad) || undefined;

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitted(true);
    if (valid && !disabled) {
      onSubmit({ cardNumber: digits(card.number), cardHolder: card.holder.trim(), expiry: card.expiry, cvc: card.cvc });
    }
  };

  const fillTestCard = (number: string) =>
    setCard({
      number,
      holder: card.holder || holder || t("payment.sampleHolder"),
      expiry: card.expiry || sampleExpiry(),
      cvc: card.cvc || "123",
    });

  return (
    <form id={id} onSubmit={submit} noValidate>
      <div className={styles.testCards}>
        <span>{t("payment.testCards")}</span>
        <div>
          {TEST_CARDS.map((c) => (
            <button key={c.number} type="button" className={`${styles.chip} ${styles[c.tone]}`} onClick={() => fillTestCard(c.number)}>
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
            aria-invalid={show(errors.number)}
            onChange={(e) => setCard({ ...card, number: formatCard(e.target.value) })}
          />
        </label>
        <label className={`${checkout.field} ${styles.wide}`}>
          <span>{t("payment.cardHolder")}</span>
          <input
            autoComplete="cc-name"
            value={card.holder}
            aria-invalid={show(errors.holder)}
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
            aria-invalid={show(errors.expiry)}
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
            aria-invalid={show(errors.cvc)}
            onChange={(e) => setCard({ ...card, cvc: digits(e.target.value).slice(0, 4) })}
          />
        </label>
      </div>
      {submitted && !valid && <p className={styles.invalid}>{t("errors.validation")}</p>}
    </form>
  );
}
