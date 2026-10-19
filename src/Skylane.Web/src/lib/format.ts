import i18n from "../i18n";

/*
 * Datas e horas dos voos.
 *
 * A API devolve as horas já na hora local de cada aeroporto, com o offset
 * (ex.: "2026-10-24T07:35:00+01:00"). Não podemos usar `new Date(...)` para as mostrar:
 * o browser converteria para o fuso de quem está a ver a página, e um voo que parte
 * às 07:35 no Porto apareceria às 02:35 a alguém em Nova Iorque. Por isso lemos
 * a hora e a data diretamente do texto.
 */

const locale = () => (i18n.language === "en" ? "en-GB" : "pt-PT");

/** "07:35" a partir de "2026-10-24T07:35:00+01:00". */
export const clockTime = (iso: string) => iso.slice(11, 16);

/** "2026-10-24" a partir de uma data ISO com hora. */
export const isoDate = (iso: string) => iso.slice(0, 10);

/** Data "de calendário" (sem fuso) para formatar dia da semana e mês. */
function calendarDate(isoDay: string) {
  const [y, m, d] = isoDay.slice(0, 10).split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, d, 12));
}

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

/** "Sex, 24 out" / "Fri, 24 Oct" (montado à mão: o formato do Intl muda de browser para browser). */
export const shortDate = (isoDay: string) =>
  `${capitalize(weekdayShort(isoDay))}, ${Number(dayNumber(isoDay))} ${monthShort(isoDay)}`;

/** "sexta-feira, 24 de outubro" */
export const longDate = (isoDay: string) =>
  new Intl.DateTimeFormat(locale(), { weekday: "long", day: "numeric", month: "long", timeZone: "UTC" }).format(
    calendarDate(isoDay),
  );

export const dayNumber = (isoDay: string) => isoDay.slice(8, 10);

export const monthShort = (isoDay: string) =>
  new Intl.DateTimeFormat(locale(), { month: "short", timeZone: "UTC" })
    .format(calendarDate(isoDay))
    .replace(".", "");

/** "seg", "ter"… / "Mon", "Tue"… (em pt-PT o Intl devolve "segunda", por isso cortamos). */
export const weekdayShort = (isoDay: string) => {
  const name = new Intl.DateTimeFormat(locale(), { weekday: "short", timeZone: "UTC" })
    .format(calendarDate(isoDay))
    .replace(".", "");
  return i18n.language === "en" ? name : name.slice(0, 3);
};

/** "1h 45m" / "55m" */
export function duration(minutes: number) {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h === 0) return `${m}m`;
  return m === 0 ? `${h}h` : `${h}h ${String(m).padStart(2, "0")}m`;
}

export const money = (value: number, currency = "EUR") =>
  new Intl.NumberFormat(locale(), { style: "currency", currency }).format(value);

/** Preço arredondado, para a faixa de datas e cartões compactos ("79 €"). */
export const moneyShort = (value: number, currency = "EUR") =>
  new Intl.NumberFormat(locale(), { style: "currency", currency, maximumFractionDigits: 0 }).format(Math.round(value));

/** Data de hoje (no fuso do browser) em "aaaa-mm-dd". */
export function todayIso() {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
}

export function addDays(isoDay: string, days: number) {
  const date = calendarDate(isoDay);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** Minutos desde a meia-noite, para os filtros por hora de partida. */
export const minutesOfDay = (iso: string) => Number(iso.slice(11, 13)) * 60 + Number(iso.slice(14, 16));
