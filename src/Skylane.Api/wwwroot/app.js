"use strict";

const MAX_PER_RESERVATION = 10;

const eventList = document.getElementById("event-list");
const eventsEmpty = document.getElementById("events-empty");
const eventTemplate = document.getElementById("event-template");
const basket = document.getElementById("basket");
const checkinForm = document.getElementById("checkin-form");
const checkinResult = document.getElementById("checkin-result");

const dayFormat = new Intl.DateTimeFormat("pt-PT", { day: "2-digit" });
const monthFormat = new Intl.DateTimeFormat("pt-PT", { month: "short" });
const whenFormat = new Intl.DateTimeFormat("pt-PT", {
  weekday: "long", hour: "2-digit", minute: "2-digit"
});

let countdownTimer = null;

// ---------- API ----------

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: { "Content-Type": "application/json" },
    ...options
  });

  const body = response.status === 204 ? null : await response.json().catch(() => null);

  if (!response.ok) {
    // A API responde com ProblemDetails: "title" traz a mensagem para a pessoa.
    const error = new Error(body?.title ?? "Não foi possível concluir o pedido.");
    error.status = response.status;
    throw error;
  }

  return body;
}

// ---------- Eventos ----------

async function loadEvents() {
  const events = await api("/api/events");
  eventList.replaceChildren(...events.map(renderEvent));
  eventsEmpty.hidden = events.length > 0;
}

function renderEvent(ev) {
  const node = eventTemplate.content.firstElementChild.cloneNode(true);
  const startsAt = new Date(ev.startsAt);
  const soldOut = ev.available === 0;

  node.querySelector(".event__date").dateTime = ev.startsAt;
  node.querySelector(".event__day").textContent = dayFormat.format(startsAt);
  node.querySelector(".event__month").textContent = monthFormat.format(startsAt).replace(".", "");
  node.querySelector(".event__name").textContent = ev.name;
  node.querySelector(".event__meta").textContent = `${ev.venue}, ${whenFormat.format(startsAt)}`;

  const taken = ev.capacity - ev.available;
  node.querySelector(".seats__fill").style.width = `${(taken / ev.capacity) * 100}%`;

  const label = node.querySelector(".seats__label");
  label.textContent = soldOut
    ? "Esgotado"
    : `${ev.available} de ${ev.capacity} lugares disponíveis`;
  label.classList.toggle("seats__label--low", soldOut || ev.available <= ev.capacity * 0.1);

  const form = node.querySelector(".event__buy");
  const select = form.querySelector("select");
  const selectId = `qty-${ev.id}`;
  select.id = selectId;
  form.querySelector("label").htmlFor = selectId;

  const max = Math.min(MAX_PER_RESERVATION, Math.max(ev.available, 1));
  for (let i = 1; i <= max; i++) {
    select.append(new Option(`${i} ${i === 1 ? "bilhete" : "bilhetes"}`, i));
  }

  const button = form.querySelector("button");
  if (soldOut) {
    node.classList.add("event--soldout");
    select.disabled = true;
    button.disabled = true;
    button.textContent = "Esgotado";
  }

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    button.disabled = true;
    try {
      const reservation = await api(`/api/events/${ev.id}/reservations`, {
        method: "POST",
        body: JSON.stringify({ quantity: Number(select.value) })
      });
      showHold(reservation);
    } catch (err) {
      showMessage(err.message);
    } finally {
      await loadEvents();
    }
  });

  return node;
}

// ---------- Reserva ----------

function showHold(reservation) {
  clearInterval(countdownTimer);

  const wrapper = document.createElement("div");
  wrapper.className = "hold";

  const summary = document.createElement("p");
  summary.textContent = `${reservation.quantity} ${reservation.quantity === 1 ? "bilhete" : "bilhetes"} para ${reservation.eventName}. Os lugares estão guardados durante:`;

  const countdown = document.createElement("p");
  countdown.className = "countdown";

  const confirm = document.createElement("button");
  confirm.className = "btn";
  confirm.textContent = "Confirmar compra";

  wrapper.append(summary, countdown, confirm);
  basket.replaceChildren(wrapper);

  const expiresAt = new Date(reservation.expiresAt).getTime();

  const tick = () => {
    const remaining = Math.max(0, expiresAt - Date.now());
    const minutes = Math.floor(remaining / 60000);
    const seconds = Math.floor((remaining % 60000) / 1000);
    countdown.textContent = `${minutes}:${String(seconds).padStart(2, "0")}`;
    countdown.classList.toggle("countdown--urgent", remaining < 60000);

    if (remaining === 0) {
      clearInterval(countdownTimer);
      confirm.disabled = true;
      showMessage("O tempo acabou e os lugares foram libertados. Podes fazer uma nova reserva.");
    }
  };

  tick();
  countdownTimer = setInterval(tick, 1000);

  confirm.addEventListener("click", async () => {
    confirm.disabled = true;
    try {
      const confirmed = await api(`/api/reservations/${reservation.id}/confirm`, { method: "POST" });
      clearInterval(countdownTimer);
      showTickets(confirmed);
    } catch (err) {
      clearInterval(countdownTimer);
      showMessage(err.message);
    } finally {
      await loadEvents();
    }
  });
}

function showTickets(reservation) {
  const intro = document.createElement("p");
  intro.className = "empty";
  intro.textContent = "Compra confirmada. Guarda estes códigos: vais precisar deles à entrada.";

  const list = document.createElement("div");
  list.className = "tickets";

  reservation.tickets.forEach((code, index) => {
    const ticket = document.createElement("article");
    ticket.className = "ticket";
    ticket.style.animationDelay = `${index * 90}ms`;

    const info = document.createElement("div");
    const eventName = document.createElement("p");
    eventName.className = "ticket__event";
    eventName.textContent = reservation.eventName;
    const codeText = document.createElement("p");
    codeText.className = "ticket__code";
    codeText.textContent = code;
    info.append(eventName, codeText);

    const copy = document.createElement("button");
    copy.className = "btn btn--quiet";
    copy.textContent = "Usar";
    copy.title = "Colocar este código no campo de validação";
    copy.addEventListener("click", () => {
      document.getElementById("checkin-code").value = code;
      document.getElementById("checkin-code").focus();
    });

    ticket.append(info, copy);
    list.append(ticket);
  });

  basket.replaceChildren(intro, list);
}

function showMessage(text) {
  const p = document.createElement("p");
  p.className = "notice";
  p.textContent = text;
  basket.replaceChildren(p);
}

// ---------- Check-in ----------

checkinForm.addEventListener("submit", async (e) => {
  e.preventDefault();
  const code = new FormData(checkinForm).get("code").toString().trim();
  checkinResult.className = "checkin__result";

  try {
    const ticket = await api(`/api/tickets/${encodeURIComponent(code)}/check-in`, { method: "POST" });
    checkinResult.textContent = `Entrada autorizada: ${ticket.eventName}.`;
    checkinResult.classList.add("checkin__result--ok");
  } catch (err) {
    checkinResult.textContent = err.message;
    checkinResult.classList.add("checkin__result--error");
  }
});

loadEvents().catch(() => {
  eventsEmpty.hidden = false;
  eventsEmpty.textContent = "Não foi possível carregar os eventos. Confirma que a API e a base de dados estão a correr.";
});
