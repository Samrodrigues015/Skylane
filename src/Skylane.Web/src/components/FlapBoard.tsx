import { useEffect, useRef, useState } from "react";
import styles from "./FlapBoard.module.css";

const ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

/**
 * Placas de um painel de partidas antigo (split-flap).
 * Quando o código muda, cada placa "roda" por letras aleatórias antes de parar
 * na letra certa, com um pequeno atraso entre placas. É a única animação
 * decorativa do site; quem tem "reduzir movimento" ativo vê a mudança direta.
 */
export default function FlapBoard({ code }: { code: string }) {
  const target = code.padEnd(3, " ").slice(0, 3).toUpperCase();
  const [letters, setLetters] = useState(target.split(""));
  const first = useRef(true);

  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduceMotion) {
      setLetters(target.split(""));
      return;
    }

    let tick = 0;
    const timer = window.setInterval(() => {
      tick++;
      setLetters(
        target.split("").map((ch, i) => {
          // A placa i para ao fim de 6 + 3*i passos.
          const settled = tick >= 6 + i * 3;
          return settled ? ch : ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
        }),
      );
      if (tick >= 6 + 2 * 3) window.clearInterval(timer);
    }, 55);

    return () => window.clearInterval(timer);
  }, [target]);

  return (
    <div className={styles.flaps} aria-label={target.trim()} role="img">
      {letters.map((ch, i) => (
        <span key={i} className={styles.flap} aria-hidden="true">
          {ch}
        </span>
      ))}
    </div>
  );
}
