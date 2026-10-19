import { useSyncExternalStore } from "react";
import { useTranslation } from "react-i18next";
import { slowNetwork } from "../api/client";
import { Link, NavLink, Outlet, useLocation } from "react-router";
import type { Language } from "../i18n";
import { useCart } from "../lib/cart";
import { CartIcon, PlaneIcon } from "./Icons";
import styles from "./Layout.module.css";

export default function Layout() {
  const { t, i18n } = useTranslation();
  const setLanguage = (lng: Language) => void i18n.changeLanguage(lng);
  const { pathname } = useLocation();
  const cart = useCart();
  const waking = useSyncExternalStore(slowNetwork.subscribe, slowNetwork.isSlow);
  // "Voos" fica ativo em todo o fluxo de compra, não só na página inicial.
  const inFlights = pathname === "/" || pathname.startsWith("/flights") || pathname.startsWith("/booking");

  return (
    <>
      <header className={`${styles.top} no-print`}>
        <div className={`wrap ${styles.topInner}`}>
          <Link to="/" className={styles.logo}>
            <span className={styles.mark}>
              <PlaneIcon />
            </span>
            Skylane
          </Link>

          <nav className={styles.nav}>
            <Link to="/" aria-current={inFlights ? "page" : undefined}>
              {t("nav.flights")}
            </Link>
            <NavLink to="/events">{t("nav.events")}</NavLink>
            <NavLink to="/manage">{t("nav.myBookings")}</NavLink>
          </nav>

          <div className={styles.right}>
            <div className={styles.lang} role="group" aria-label={t("common.language")}>
              {(["pt", "en"] as const).map((lng) => (
                <button
                  key={lng}
                  type="button"
                  aria-pressed={i18n.language === lng}
                  onClick={() => setLanguage(lng)}
                >
                  {lng.toUpperCase()}
                </button>
              ))}
            </div>
            <Link to="/cart" className={styles.cart} aria-label={t("cart.open", { count: cart.count })}>
              <CartIcon />
              {cart.count > 0 && <span className={styles.badge}>{cart.count}</span>}
            </Link>
            <Link to="/manage" className={styles.manage}>
              {t("nav.manage")}
            </Link>
          </div>
        </div>
      </header>

      {waking && (
        <div className={`${styles.waking} no-print`} role="status">
          <span className={styles.spinner} aria-hidden="true" />
          <span>
            <b>{t("waking.title")}</b> {t("waking.text")}
          </span>
        </div>
      )}

      <Outlet />

      <footer className={`${styles.footer} no-print`}>
        <div className={`wrap ${styles.footerInner}`}>
          <span>{t("footer.credit")}</span>
          <span>{t("footer.stack")}</span>
        </div>
      </footer>
    </>
  );
}
