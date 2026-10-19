import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import type { BookingResponse, CabinClass, FlightSummary, PassengerRequest } from "../api/types";

/*
 * Estado do fluxo de reserva (pesquisa → voos → passageiros → lugares → pagamento).
 *
 * Fica guardado em sessionStorage para sobreviver a um refresh da página,
 * mas não entre separadores nem depois de fechar o browser.
 */

export interface SearchParams {
  from: string;
  to: string;
  depart: string;
  return?: string;
  adults: number;
  cabin: CabinClass;
}

export interface FlowState {
  search?: SearchParams;
  outbound?: FlightSummary;
  inbound?: FlightSummary;
  passengers: PassengerRequest[];
  contactEmail: string;
  seats: { outbound: string[]; inbound: string[] };
  booking?: BookingResponse;
}

const emptyState: FlowState = {
  passengers: [],
  contactEmail: "",
  seats: { outbound: [], inbound: [] },
};

const STORAGE_KEY = "skylane.flow";

function load(): FlowState {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? { ...emptyState, ...JSON.parse(raw) } : emptyState;
  } catch {
    return emptyState;
  }
}

interface FlowContextValue {
  state: FlowState;
  update: (patch: Partial<FlowState>) => void;
  startSearch: (search: SearchParams) => void;
  reset: () => void;
}

const FlowContext = createContext<FlowContextValue | null>(null);

export function FlowProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<FlowState>(load);

  useEffect(() => {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch {
      // sem sessionStorage: o fluxo funciona na mesma, só não sobrevive a um refresh
    }
  }, [state]);

  const update = useCallback((patch: Partial<FlowState>) => setState((s) => ({ ...s, ...patch })), []);

  // Uma pesquisa nova começa uma viagem nova: esquecemos os voos e lugares anteriores,
  // mas mantemos os nomes dos passageiros já escritos (se o número não mudar).
  const startSearch = useCallback(
    (search: SearchParams) =>
      setState((s) => ({
        ...emptyState,
        search,
        contactEmail: s.contactEmail,
        passengers: s.passengers.length === search.adults ? s.passengers : [],
      })),
    [],
  );

  const reset = useCallback(() => setState(emptyState), []);

  const value = useMemo(() => ({ state, update, startSearch, reset }), [state, update, startSearch, reset]);

  return <FlowContext.Provider value={value}>{children}</FlowContext.Provider>;
}

export function useFlow() {
  const ctx = useContext(FlowContext);
  if (!ctx) throw new Error("useFlow tem de ser usado dentro do FlowProvider");
  return ctx;
}

/** Preço de uma classe num voo (ou o mais baixo, se a classe não existir). */
export function fareFor(flight: FlightSummary, cabin: CabinClass) {
  return flight.fares.find((f) => f.class === cabin) ?? flight.fares[0];
}

export function searchToQuery(s: SearchParams, leg?: "outbound" | "return") {
  const q = new URLSearchParams({
    from: s.from,
    to: s.to,
    depart: s.depart,
    adults: String(s.adults),
    cabin: s.cabin,
  });
  if (s.return) q.set("return", s.return);
  if (leg) q.set("leg", leg);
  return q.toString();
}

export function searchFromQuery(q: URLSearchParams): SearchParams | null {
  const from = q.get("from");
  const to = q.get("to");
  const depart = q.get("depart");
  if (!from || !to || !depart) return null;
  const cabin = q.get("cabin") === "Business" ? "Business" : "Economy";
  const adults = Math.min(9, Math.max(1, Number(q.get("adults")) || 1));
  return { from, to, depart, return: q.get("return") ?? undefined, adults, cabin };
}
