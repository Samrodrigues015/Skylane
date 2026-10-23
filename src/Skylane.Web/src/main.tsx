import { MutationCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Route, Routes } from "react-router";
import "./i18n";
import "./styles/global.css";
import Layout from "./components/Layout";
import { CartProvider } from "./lib/cart";
import { FlowProvider } from "./lib/flow";
import CartPage from "./pages/CartPage";
import ConfirmationPage from "./pages/ConfirmationPage";
import EventsPage from "./pages/EventsPage";
import FlightsPage from "./pages/FlightsPage";
import HomePage from "./pages/HomePage";
import ManageBookingPage from "./pages/ManageBookingPage";
import PassengersPage from "./pages/PassengersPage";
import PaymentPage from "./pages/PaymentPage";
import SeatsPage from "./pages/SeatsPage";

const queryClient: QueryClient = new QueryClient({
  // Depois de reservar, pagar ou cancelar, os lugares disponíveis mudam:
  // marca tudo como desatualizado para não se ver um número antigo de lugares.
  mutationCache: new MutationCache({
    onSuccess: () => void queryClient.invalidateQueries(),
  }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <FlowProvider>
        <CartProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<HomePage />} />
              <Route path="flights" element={<FlightsPage />} />
              <Route path="booking/passengers" element={<PassengersPage />} />
              <Route path="booking/seats" element={<SeatsPage />} />
              <Route path="booking/payment" element={<PaymentPage />} />
              <Route path="booking/:reference/confirmation" element={<ConfirmationPage />} />
              <Route path="manage" element={<ManageBookingPage />} />
              <Route path="events" element={<EventsPage />} />
              <Route path="cart" element={<CartPage />} />
              <Route path="*" element={<HomePage />} />
            </Route>
          </Routes>
        </BrowserRouter>
        </CartProvider>
      </FlowProvider>
    </QueryClientProvider>
  </StrictMode>,
);
