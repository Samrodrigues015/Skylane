import { useEffect, useState } from "react";

/** Segundos que faltam até `expiresAt` (instante UTC), atualizado a cada segundo. */
export function useCountdown(expiresAt?: string) {
  const left = () => (expiresAt ? Math.max(0, Math.floor((Date.parse(expiresAt) - Date.now()) / 1000)) : 0);
  const [seconds, setSeconds] = useState(left);

  useEffect(() => {
    setSeconds(left());
    if (!expiresAt) return;
    const id = window.setInterval(() => {
      const s = left();
      setSeconds(s);
      if (s === 0) window.clearInterval(id);
    }, 1000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [expiresAt]);

  const mm = String(Math.floor(seconds / 60)).padStart(2, "0");
  const ss = String(seconds % 60).padStart(2, "0");
  return { seconds, expired: !!expiresAt && seconds === 0, label: `${mm}:${ss}` };
}
