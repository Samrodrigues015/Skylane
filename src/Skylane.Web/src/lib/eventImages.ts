import type { EventCategory } from "../api/types";

/*
 * Fotografias dos eventos, do Unsplash (licença Unsplash: uso gratuito, sem atribuição obrigatória).
 * São servidas pela CDN do Unsplash, que corta e redimensiona com os parâmetros do URL.
 * Cada categoria tem uma ou mais fotos; o evento escolhe sempre a mesma (pelo id), para não mudar a cada visita.
 */
const PHOTOS: Record<EventCategory, string[]> = {
  Fado: ["1763567712690-8a7a7f9e373b", "1646898463399-c3d670138056"],
  Jazz: ["1511192336575-5a79af67a629", "1415201364774-f6f0bb35f28f", "1705848533113-05690a3241e0"],
  Classical: ["1465847899084-d164df4dedc6", "1519683109079-d5f539e1542f"],
  Opera: ["1565798846807-2af22c843402", "1514306191717-452ec28c7814"],
  Flamenco: ["1773708371569-77b65c70ff1e", "1773708370809-be6ba557078d"],
  Guitar: ["1510915361894-db8b60106cb1", "1525201548942-d8732f6617a0"],
  Electronic: ["1578736641330-3155e606cd40"],
  Festival: ["1549452026-91574599e7f6", "1533174072545-7a4b6ad7a6c3"],
  LightFestival: ["1608991969807-3833f043d0ca", "1523376460408-aeb5f5d051b8"],
  Concert: ["1470229722913-7c0e2dbbafd3", "1459749411175-04bf5292ceea", "1524368535928-5b5e00ddc76b"],
  Comedy: ["1580188928585-0ef5c1a5c4dd"],
  Cinema: ["1574267432553-4b4628081c31", "1688678004647-945d5aaf91c1"],
  Food: ["1632389879997-330b17bf1923"],
  Flowers: ["1491994336086-44f5d76dd8f2"],
  Fireworks: ["1753248186812-44cd8d9fc176"],
};

/** Cor de fundo enquanto a foto carrega (ou se a CDN falhar). */
export const CATEGORY_COLORS: Record<EventCategory, string> = {
  Fado: "#1f3a72",
  Jazz: "#3b1f5c",
  Classical: "#5c1f2e",
  Opera: "#7a1424",
  Flamenco: "#b3242c",
  Guitar: "#13505b",
  Electronic: "#5b1fa8",
  Festival: "#c25a2a",
  LightFestival: "#10295c",
  Concert: "#8a3f5a",
  Comedy: "#7c2f22",
  Cinema: "#1b2a4a",
  Food: "#0f6e74",
  Flowers: "#2f7d4f",
  Fireworks: "#14244d",
};

function hash(value: string) {
  let h = 0;
  for (const c of value) h = (h * 31 + c.charCodeAt(0)) | 0;
  return Math.abs(h);
}

/** URL da foto do evento, já cortada para a largura pedida. */
export function eventPhoto(category: EventCategory, seed: string, width: number) {
  const list = PHOTOS[category] ?? PHOTOS.Concert;
  const id = list[hash(seed) % list.length];
  return `https://images.unsplash.com/photo-${id}?auto=format&fit=crop&w=${width}&q=70`;
}
