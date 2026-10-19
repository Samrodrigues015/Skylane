import type { TFunction } from "i18next";
import { ApiError } from "../api/client";

/** Mensagem traduzida a partir do `code` do erro da API. */
export function errorMessage(error: unknown, t: TFunction) {
  if (error instanceof ApiError) {
    const key = `errors.${error.code}`;
    const translated = t(key);
    return translated === key ? error.message : translated;
  }
  return t("errors.unexpected");
}
