import { useTranslation } from "react-i18next";
import { CheckIcon } from "./Icons";
import styles from "./Steps.module.css";

export type StepKey = "outbound" | "return" | "passengers" | "seats" | "payment";

/** Barra de progresso da compra. Numa viagem só de ida, o passo "Voo de volta" não aparece. */
export default function Steps({ current, roundTrip }: { current: StepKey; roundTrip: boolean }) {
  const { t } = useTranslation();
  const steps: StepKey[] = roundTrip
    ? ["outbound", "return", "passengers", "seats", "payment"]
    : ["outbound", "passengers", "seats", "payment"];
  const currentIndex = steps.indexOf(current);

  return (
    <ol className={`${styles.steps} no-print`}>
      {steps.map((step, i) => {
        const state = i < currentIndex ? "done" : i === currentIndex ? "current" : "todo";
        return (
          <li key={step} className={styles[state]} aria-current={state === "current" ? "step" : undefined}>
            <b>{state === "done" ? <CheckIcon width={14} height={14} /> : i + 1}</b>
            <span>{t(`steps.${step}`)}</span>
          </li>
        );
      })}
    </ol>
  );
}
