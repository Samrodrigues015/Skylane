import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import type { EventResponse } from "../api/types";

/*
 * Carrinho de bilhetes para eventos.
 *
 * Fica no browser (localStorage) e não prende lugares: como nas lojas online, os lugares só
 * ficam guardados quando se avança para o pagamento. Aí o carrinho é enviado à API junto com
 * os voos (se houver) num único pedido, e é tudo reservado de uma vez ou nada.
 */

export interface CartItem {
  /** Dados do evento no momento em que foi adicionado (para mostrar o carrinho sem pedidos à API). */
  event: EventResponse;
  quantity: number;
}

interface CartContextValue {
  items: CartItem[];
  count: number;
  total: number;
  add: (event: EventResponse, quantity: number) => void;
  setQuantity: (eventId: string, quantity: number) => void;
  remove: (eventId: string) => void;
  clear: () => void;
}

const STORAGE_KEY = "skylane.cart";
export const MAX_TICKETS = 10;

function load(): CartItem[] {
  try {
    const items: CartItem[] = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "[]");
    // Eventos que já passaram saem do carrinho sozinhos.
    return items.filter((i) => Date.parse(i.event.startsAt) > Date.now());
  } catch {
    return [];
  }
}

const CartContext = createContext<CartContextValue | null>(null);

export function CartProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<CartItem[]>(load);

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
    } catch {
      /* modo privado sem storage: o carrinho vive só nesta página */
    }
  }, [items]);

  const add = useCallback((event: EventResponse, quantity: number) => {
    setItems((list) => {
      const existing = list.find((i) => i.event.id === event.id);
      if (existing) {
        return list.map((i) =>
          i.event.id === event.id ? { event, quantity: Math.min(MAX_TICKETS, i.quantity + quantity) } : i,
        );
      }
      return [...list, { event, quantity }];
    });
  }, []);

  const setQuantity = useCallback((eventId: string, quantity: number) => {
    setItems((list) => list.map((i) => (i.event.id === eventId ? { ...i, quantity } : i)));
  }, []);

  const remove = useCallback((eventId: string) => setItems((list) => list.filter((i) => i.event.id !== eventId)), []);
  const clear = useCallback(() => setItems([]), []);

  const value = useMemo(
    () => ({
      items,
      count: items.reduce((sum, i) => sum + i.quantity, 0),
      total: items.reduce((sum, i) => sum + i.quantity * i.event.price, 0),
      add,
      setQuantity,
      remove,
      clear,
    }),
    [items, add, setQuantity, remove, clear],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const value = useContext(CartContext);
  if (!value) throw new Error("useCart tem de estar dentro de <CartProvider>");
  return value;
}

/** Formato que a API espera em CreateBookingRequest.events. */
export const cartSelections = (items: CartItem[]) => items.map((i) => ({ eventId: i.event.id, quantity: i.quantity }));
