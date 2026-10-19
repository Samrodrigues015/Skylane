import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "./en";
import pt from "./pt";

export type Language = "pt" | "en";

const STORAGE_KEY = "skylane.lang";

function initialLanguage(): Language {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === "pt" || saved === "en") return saved;
  } catch {
    // localStorage pode não estar disponível (modo privado); seguimos o browser.
  }
  return navigator.language.toLowerCase().startsWith("pt") ? "pt" : "en";
}

void i18n.use(initReactI18next).init({
  resources: { pt: { translation: pt }, en: { translation: en } },
  lng: initialLanguage(),
  fallbackLng: "pt",
  interpolation: { escapeValue: false },
});

i18n.on("languageChanged", (lng) => {
  document.documentElement.lang = lng;
  try {
    localStorage.setItem(STORAGE_KEY, lng);
  } catch {
    // ignorar
  }
});

document.documentElement.lang = i18n.language;

export default i18n;
