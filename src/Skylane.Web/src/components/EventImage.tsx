import { useState } from "react";
import type { EventCategory } from "../api/types";
import { CATEGORY_COLORS, eventPhoto } from "../lib/eventImages";

interface Props {
  category: EventCategory;
  /** Nome do evento: escolhe sempre a mesma foto dentro da categoria. */
  seed: string;
  /** Largura máxima em que a imagem vai aparecer (px CSS). */
  width?: number;
  className?: string;
}

/**
 * Foto do evento a ocupar o contentor (object-fit: cover), com a cor da categoria por baixo
 * enquanto carrega. Se a foto falhar, fica só a cor, sem ícone de imagem partida.
 */
export default function EventImage({ category, seed, width = 600, className }: Props) {
  const [failed, setFailed] = useState(false);

  return (
    <span
      className={className}
      style={{ display: "block", width: "100%", height: "100%", background: CATEGORY_COLORS[category] ?? "#1b2a4a" }}
    >
      {!failed && (
        <img
          src={eventPhoto(category, seed, width)}
          srcSet={`${eventPhoto(category, seed, width)} 1x, ${eventPhoto(category, seed, width * 2)} 2x`}
          alt=""
          loading="lazy"
          decoding="async"
          onError={() => setFailed(true)}
          style={{ display: "block", width: "100%", height: "100%", objectFit: "cover" }}
        />
      )}
    </span>
  );
}
