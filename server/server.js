"use strict";

const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");
const webPush = require("web-push");

const PORT = Number(process.env.PORT || 3000);
const PUBLIC_DIRECTORY = path.resolve(__dirname, "..", "web");
const DATA_DIRECTORY = process.env.DATA_DIR || path.join(__dirname, "data");
const STATE_PATH = path.join(DATA_DIRECTORY, "push-state.json");
const VAPID_PATH = path.join(DATA_DIRECTORY, "vapid-keys.json");
const MAX_REMINDERS = 500;
const lastNotified = new Map();
let state = loadState();
let schedulerRunning = false;

fs.mkdirSync(DATA_DIRECTORY, { recursive: true });
const vapidKeys = loadVapidKeys();
webPush.setVapidDetails(
  process.env.VAPID_SUBJECT || "mailto:eslatmachi@localhost",
  vapidKeys.publicKey,
  vapidKeys.privateKey
);

function loadState() {
  try {
    const parsed = JSON.parse(fs.readFileSync(STATE_PATH, "utf8"));
    return parsed && parsed.installations ? parsed : { installations: {} };
  } catch {
    return { installations: {} };
  }
}

function saveState() {
  fs.mkdirSync(DATA_DIRECTORY, { recursive: true });
  const temporary = STATE_PATH + ".tmp";
  fs.writeFileSync(temporary, JSON.stringify(state), { mode: 0o600 });
  fs.renameSync(temporary, STATE_PATH);
}

function loadVapidKeys() {
  if (process.env.VAPID_PUBLIC_KEY && process.env.VAPID_PRIVATE_KEY) {
    return {
      publicKey: process.env.VAPID_PUBLIC_KEY,
      privateKey: process.env.VAPID_PRIVATE_KEY
    };
  }
  try {
    const saved = JSON.parse(fs.readFileSync(VAPID_PATH, "utf8"));
    if (saved.publicKey && saved.privateKey)
      return saved;
  } catch {
    // New keys are generated below on the first run.
  }
  const generated = webPush.generateVAPIDKeys();
  fs.mkdirSync(DATA_DIRECTORY, { recursive: true });
  fs.writeFileSync(VAPID_PATH, JSON.stringify(generated), { mode: 0o600 });
  return generated;
}

function sendJson(response, status, value) {
  const body = JSON.stringify(value);
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
    "Cache-Control": "no-store",
    "X-Content-Type-Options": "nosniff"
  });
  response.end(body);
}

function readJson(request) {
  return new Promise((resolve, reject) => {
    let size = 0;
    let tooLarge = false;
    const chunks = [];
    request.on("data", (chunk) => {
      size += chunk.length;
      if (size > 1024 * 1024) {
        tooLarge = true;
        return;
      }
      chunks.push(chunk);
    });
    request.on("end", () => {
      if (tooLarge) {
        reject(new Error("Request too large"));
        return;
      }
      try {
        resolve(JSON.parse(Buffer.concat(chunks).toString("utf8") || "{}"));
      } catch {
        reject(new Error("Invalid JSON"));
      }
    });
    request.on("error", reject);
  });
}

function validInstallationId(value) {
    return typeof value === "string" && /^[a-zA-Z0-9_-]{16,80}$/.test(value);
}

function validSubscription(value) {
  return value && typeof value.endpoint === "string" && value.endpoint.length < 4096 &&
    /^https:\/\//.test(value.endpoint) && value.keys &&
    typeof value.keys.p256dh === "string" && typeof value.keys.auth === "string";
}

function cleanReminder(value) {
  if (!value || typeof value.id !== "string" || value.id.length > 80 ||
      typeof value.title !== "string" || !value.title.trim() || !Number.isFinite(value.remindAt))
    return null;
  return {
    id: value.id,
    title: value.title.slice(0, 200),
    notes: typeof value.notes === "string" ? value.notes.slice(0, 500) : "",
    remindAt: value.remindAt,
    snoozeUntil: Number.isFinite(value.snoozeUntil) ? value.snoozeUntil : null,
    repeat: ["none", "daily", "weekly"].includes(value.repeat) ? value.repeat : "none",
    completed: Boolean(value.completed),
    updatedAt: Number.isFinite(value.updatedAt) ? value.updatedAt : 0
  };
}

async function handleApi(request, response, pathname) {
  if (request.method === "GET" && pathname === "/api/health") {
    sendJson(response, 200, { ok: true, pushConfigured: true });
    return;
  }
  if (request.method === "GET" && pathname === "/api/config") {
    sendJson(response, 200, { pushConfigured: true, publicKey: vapidKeys.publicKey });
    return;
  }

  const stateMatch = pathname.match(/^\/api\/installations\/([^/]+)\/state$/);
  const installationMatch = pathname.match(/^\/api\/installations\/([^/]+)$/);
  const id = decodeURIComponent((stateMatch || installationMatch || [])[1] || "");
  if (!validInstallationId(id)) {
    sendJson(response, 404, { error: "Not found" });
    return;
  }

  if (request.method === "PUT" && stateMatch) {
    const body = await readJson(request);
    if (!validSubscription(body.subscription) || !Array.isArray(body.reminders) || body.reminders.length > MAX_REMINDERS) {
      sendJson(response, 400, { error: "Invalid state" });
      return;
    }
    const reminders = body.reminders.map(cleanReminder).filter(Boolean);
    const previous = state.installations[id];
    state.installations[id] = {
      subscription: body.subscription,
      reminders,
      lastNotified: previous && previous.lastNotified ? previous.lastNotified : {},
      updatedAt: Date.now()
    };
    saveState();
    sendJson(response, 200, { ok: true });
    return;
  }

  if (request.method === "DELETE" && installationMatch) {
    delete state.installations[id];
    saveState();
    sendJson(response, 200, { ok: true });
    return;
  }
  sendJson(response, 404, { error: "Not found" });
}

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".webmanifest": "application/manifest+json; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon"
};

function serveStatic(request, response, pathname) {
  let decoded;
  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    sendJson(response, 400, { error: "Invalid path" });
    return;
  }
  const relative = decoded === "/" ? "index.html" : decoded.replace(/^\/+/, "");
  const filePath = path.resolve(PUBLIC_DIRECTORY, relative);
  if (filePath !== PUBLIC_DIRECTORY && !filePath.startsWith(PUBLIC_DIRECTORY + path.sep)) {
    sendJson(response, 403, { error: "Forbidden" });
    return;
  }
  fs.stat(filePath, (error, stats) => {
    if (error || !stats.isFile()) {
      sendJson(response, 404, { error: "Not found" });
      return;
    }
    const extension = path.extname(filePath);
    response.writeHead(200, {
      "Content-Type": contentTypes[extension] || "application/octet-stream",
      "Content-Length": stats.size,
      "Cache-Control": extension === ".html" || filePath.endsWith("sw.js") ? "no-cache" : "public, max-age=300",
      "X-Content-Type-Options": "nosniff",
      "Referrer-Policy": "no-referrer"
    });
    if (request.method === "HEAD") {
      response.end();
      return;
    }
    const stream = fs.createReadStream(filePath);
    stream.on("error", () => response.destroy());
    stream.pipe(response);
  });
}

const server = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url, "http://localhost");
    if (url.pathname.startsWith("/api/"))
      await handleApi(request, response, url.pathname);
    else if (["GET", "HEAD"].includes(request.method))
      serveStatic(request, response, url.pathname);
    else
      sendJson(response, 405, { error: "Method not allowed" });
  } catch (error) {
    if (!response.headersSent)
      sendJson(response, 400, { error: error.message === "Request too large" ? "Request too large" : "Bad request" });
    else
      response.destroy();
  }
});

function occurrenceFor(reminder, notified, now) {
  if (reminder.completed)
    return null;
  const previous = Number(notified[reminder.id] || 0);
  let occurrence = Number.isFinite(reminder.snoozeUntil) ? reminder.snoozeUntil : reminder.remindAt;
  if (!Number.isFinite(reminder.snoozeUntil) && reminder.repeat !== "none") {
    const step = reminder.repeat === "daily" ? 86400000 : 7 * 86400000;
    if (previous) {
      while (occurrence <= previous)
        occurrence += step;
    }
    while (occurrence + step <= now)
      occurrence += step;
  }
  if (occurrence > now || occurrence === previous)
    return null;
  return occurrence;
}

async function runPushScheduler() {
  if (schedulerRunning)
    return;
  schedulerRunning = true;
  const now = Date.now();
  let changed = false;
  try {
    for (const [id, installation] of Object.entries(state.installations)) {
      if (!installation.subscription)
        continue;
      installation.lastNotified = installation.lastNotified || {};
      for (const reminder of installation.reminders || []) {
        const occurrence = occurrenceFor(reminder, installation.lastNotified, now);
        if (!occurrence)
          continue;
        const body = reminder.notes || new Intl.DateTimeFormat("uz-UZ", {
          day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit"
        }).format(occurrence);
        try {
          await webPush.sendNotification(installation.subscription, JSON.stringify({
            title: `Eslatma: ${reminder.title}`,
            body,
            tag: `eslatmachi-${reminder.id}-${occurrence}`,
            url: "./"
          }), { TTL: 24 * 60 * 60, urgency: "high" });
          installation.lastNotified[reminder.id] = occurrence;
          changed = true;
        } catch (error) {
          if (error.statusCode === 404 || error.statusCode === 410) {
            delete state.installations[id];
            changed = true;
            break;
          }
          console.error("Push delivery failed:", error.statusCode || error.message);
        }
      }
    }
    if (changed)
      saveState();
  } finally {
    schedulerRunning = false;
  }
}

server.listen(PORT, "0.0.0.0", () => {
  console.log(`Eslatmachi PWA: http://localhost:${PORT}`);
  console.log("Web Push tayyor. Qurilma xabarnomalarini faqat foydalanuvchi ruxsati bilan yuboradi.");
});

const scheduler = setInterval(runPushScheduler, 5000);
function shutdown() {
  clearInterval(scheduler);
  server.close(() => process.exit(0));
}
process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
