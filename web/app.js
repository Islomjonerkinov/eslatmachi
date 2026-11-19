(() => {
  "use strict";

  const STORAGE_KEY = "eslatmachi.reminders.v1";
  const INSTALLATION_KEY = "eslatmachi.installation.v1";
  const REPEAT_LABELS = { none: "Bir marta", daily: "Har kuni", weekly: "Har hafta" };
  const byId = (id) => document.getElementById(id);
  const ui = {
    todayLabel: byId("todayLabel"),
    notificationCard: byId("notificationCard"),
    notificationTitle: byId("notificationTitle"),
    notificationText: byId("notificationText"),
    enableNotifications: byId("enableNotifications"),
    disableNotifications: byId("disableNotifications"),
    nextCard: byId("nextCard"),
    nextTitle: byId("nextTitle"),
    nextTime: byId("nextTime"),
    activeCount: byId("activeCount"),
    reminderList: byId("reminderList"),
    reminderTemplate: byId("reminderTemplate"),
    emptyState: byId("emptyState"),
    openEditor: byId("openEditor"),
    editorOverlay: byId("editorOverlay"),
    editorTitle: byId("editorTitle"),
    closeEditor: byId("closeEditor"),
    cancelEditor: byId("cancelEditor"),
    reminderForm: byId("reminderForm"),
    titleInput: byId("titleInput"),
    dateInput: byId("dateInput"),
    timeInput: byId("timeInput"),
    repeatInput: byId("repeatInput"),
    notesInput: byId("notesInput"),
    formError: byId("formError"),
    saveReminder: byId("saveReminder"),
    alertOverlay: byId("alertOverlay"),
    alertTitle: byId("alertTitle"),
    alertTime: byId("alertTime"),
    alertNotes: byId("alertNotes"),
    alertDone: byId("alertDone"),
    alertSnooze: byId("alertSnooze"),
    alertClose: byId("alertClose"),
    installButton: byId("installButton"),
    installOverlay: byId("installOverlay"),
    installInstructions: byId("installInstructions"),
    closeInstall: byId("closeInstall"),
    installUnderstood: byId("installUnderstood"),
    browserInstall: byId("browserInstall"),
    toast: byId("toast")
  };

  let reminders = loadReminders();
  let currentFilter = "active";
  let editingId = null;
  let activeAlert = null;
  let alertQueue = [];
  let toastTimer = null;
  let syncTimer = null;
  let syncInProgress = false;
  let syncPending = false;
  let pushSubscription = null;
  let serverPushEnabled = false;
  let deferredInstallPrompt = null;

  const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent) ||
    (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  const isStandalone = window.matchMedia("(display-mode: standalone)").matches || navigator.standalone === true;

  function createId() {
    if (crypto.randomUUID)
      return crypto.randomUUID();
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  }

  function loadReminders() {
    try {
      const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) || "[]");
      if (!Array.isArray(parsed))
        return [];
      return parsed.filter((item) => item && typeof item.id === "string" &&
        typeof item.title === "string" && Number.isFinite(item.remindAt));
    } catch {
      return [];
    }
  }

  function saveReminders() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(reminders));
    } catch {
      showToast("Eslatmani qurilma xotirasiga saqlab bo‘lmadi.");
    }
    render();
    queueServerSync();
  }

  function installationId() {
    let id = localStorage.getItem(INSTALLATION_KEY);
    if (!id) {
      id = createId();
      localStorage.setItem(INSTALLATION_KEY, id);
    }
    return id;
  }

  function effectiveTime(reminder) {
    return reminder.snoozeUntil || reminder.remindAt;
  }

  function isActive(reminder) {
    return !reminder.completed;
  }

  function addInterval(timestamp, repeat) {
    const date = new Date(timestamp);
    date.setDate(date.getDate() + (repeat === "daily" ? 1 : 7));
    return date.getTime();
  }

  function advanceReminder(reminder, now) {
    reminder.snoozeUntil = null;
    if (reminder.repeat === "none") {
      reminder.completed = true;
      return;
    }
    do {
      reminder.remindAt = addInterval(reminder.remindAt, reminder.repeat);
    } while (reminder.remindAt <= now);
    reminder.updatedAt = Date.now();
  }

  function prepareForAlert(reminder, now, wasSnoozed) {
    reminder.snoozeUntil = null;
    if (!wasSnoozed)
      advanceReminder(reminder, now);
    else if (reminder.repeat === "none")
      reminder.completed = true;
    reminder.updatedAt = Date.now();
  }

  function completeReminder(reminder) {
    const wasSnoozed = Boolean(reminder.snoozeUntil);
    if (wasSnoozed) {
      reminder.snoozeUntil = null;
      if (reminder.repeat === "none")
        reminder.completed = true;
      reminder.updatedAt = Date.now();
    } else {
      advanceReminder(reminder, Date.now());
    }
  }

  function sameDay(left, right) {
    return left.getFullYear() === right.getFullYear() &&
      left.getMonth() === right.getMonth() && left.getDate() === right.getDate();
  }

  function formatTime(timestamp) {
    return new Intl.DateTimeFormat("uz-UZ", { hour: "2-digit", minute: "2-digit" }).format(timestamp);
  }

  function formatDateTime(timestamp) {
    const date = new Date(timestamp);
    const now = new Date();
    const tomorrow = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1);
    const time = formatTime(timestamp);
    if (sameDay(date, now))
      return `Bugun, ${time}`;
    if (sameDay(date, tomorrow))
      return `Ertaga, ${time}`;
    return `${new Intl.DateTimeFormat("uz-UZ", {
      day: "2-digit", month: "2-digit", year: "numeric"
    }).format(date)}, ${time}`;
  }

  function inputDate(timestamp) {
    const date = new Date(timestamp);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
  }

  function inputTime(timestamp) {
    const date = new Date(timestamp);
    return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
  }

  function render() {
    const active = reminders.filter(isActive);
    ui.activeCount.textContent = `${active.length} ta faol`;
    const next = active.slice().sort((left, right) => effectiveTime(left) - effectiveTime(right))[0];
    ui.nextCard.hidden = !next;
    if (next) {
      ui.nextTitle.textContent = next.title;
      ui.nextTime.textContent = formatDateTime(effectiveTime(next));
    }

    const visible = reminders.filter((reminder) => {
      if (currentFilter === "active")
        return isActive(reminder);
      if (currentFilter === "completed")
        return !isActive(reminder);
      return true;
    }).sort((left, right) => {
      if (isActive(left) !== isActive(right))
        return isActive(left) ? -1 : 1;
      return effectiveTime(left) - effectiveTime(right);
    });

    ui.reminderList.replaceChildren();
    for (const reminder of visible) {
      const fragment = ui.reminderTemplate.content.cloneNode(true);
      const card = fragment.querySelector(".reminder-card");
      card.dataset.id = reminder.id;
      card.classList.toggle("completed", !isActive(reminder));
      fragment.querySelector(".status-badge").textContent = !isActive(reminder)
        ? "Bajarildi" : reminder.snoozeUntil ? "Kechiktirildi" : "Kutilmoqda";
      fragment.querySelector(".repeat-label").textContent = REPEAT_LABELS[reminder.repeat] || REPEAT_LABELS.none;
      fragment.querySelector(".reminder-title").textContent = reminder.title;
      const notes = fragment.querySelector(".reminder-notes");
      notes.textContent = reminder.notes || "";
      notes.hidden = !reminder.notes;
      const time = fragment.querySelector(".reminder-time");
      time.textContent = formatDateTime(effectiveTime(reminder));
      time.dateTime = new Date(effectiveTime(reminder)).toISOString();
      fragment.querySelector(".complete-action").disabled = !isActive(reminder);
      ui.reminderList.append(fragment);
    }

    ui.emptyState.hidden = visible.length !== 0;
    const emptyTitle = ui.emptyState.querySelector("h2");
    const emptyText = ui.emptyState.querySelector("p");
    if (currentFilter === "completed" && visible.length === 0) {
      emptyTitle.textContent = "Bajarilgan eslatma yo‘q";
      emptyText.textContent = "Bajarilgan eslatmalar shu yerda ko‘rinadi.";
    } else if (currentFilter === "active" && reminders.length > 0 && visible.length === 0) {
      emptyTitle.textContent = "Faol eslatma yo‘q";
      emptyText.textContent = "Yangi eslatma qo‘shishingiz mumkin.";
    } else {
      emptyTitle.textContent = "Hozircha eslatma yo‘q";
      emptyText.textContent = "Birinchi eslatmangizni qo‘shing. U shu qurilmada saqlanadi.";
    }
  }

  function openEditor(reminder) {
    editingId = reminder ? reminder.id : null;
    const initial = reminder ? reminder.remindAt : Date.now() + 60 * 60 * 1000;
    ui.editorTitle.textContent = reminder ? "Eslatmani tahrirlash" : "Yangi eslatma";
    ui.saveReminder.textContent = reminder ? "O‘zgarishlarni saqlash" : "Eslatmani saqlash";
    ui.titleInput.value = reminder ? reminder.title : "";
    ui.dateInput.value = inputDate(initial);
    ui.timeInput.value = inputTime(initial);
    ui.repeatInput.value = reminder ? reminder.repeat : "none";
    ui.notesInput.value = reminder ? reminder.notes || "" : "";
    ui.formError.hidden = true;
    ui.editorOverlay.hidden = false;
    syncModalState();
    setTimeout(() => ui.titleInput.focus(), 100);
  }

  function closeEditor() {
    ui.editorOverlay.hidden = true;
    editingId = null;
    syncModalState();
  }

  function syncModalState() {
    const anyOpen = [ui.editorOverlay, ui.alertOverlay, ui.installOverlay].some((overlay) => !overlay.hidden);
    document.body.classList.toggle("modal-open", anyOpen);
  }

  function selectedDateTime() {
    const dateParts = ui.dateInput.value.split("-").map(Number);
    const timeParts = ui.timeInput.value.split(":").map(Number);
    if (dateParts.length !== 3 || timeParts.length !== 2 || dateParts.some(Number.isNaN) || timeParts.some(Number.isNaN))
      return NaN;
    return new Date(dateParts[0], dateParts[1] - 1, dateParts[2], timeParts[0], timeParts[1], 0, 0).getTime();
  }

  function showToast(message) {
    clearTimeout(toastTimer);
    ui.toast.textContent = message;
    ui.toast.hidden = false;
    toastTimer = setTimeout(() => { ui.toast.hidden = true; }, 3500);
  }

  function processAlertQueue() {
    if (activeAlert || alertQueue.length === 0)
      return;
    activeAlert = alertQueue.shift();
    ui.alertTitle.textContent = activeAlert.title;
    ui.alertTime.textContent = formatDateTime(activeAlert.time);
    ui.alertNotes.textContent = activeAlert.notes || "Qo‘shimcha izoh yo‘q.";
    ui.alertOverlay.hidden = false;
    syncModalState();
  }

  function closeAlert() {
    ui.alertOverlay.hidden = true;
    activeAlert = null;
    syncModalState();
    setTimeout(processAlertQueue, 120);
  }

  async function showSystemNotification(reminder, occurrence) {
    if (!("Notification" in window) || Notification.permission !== "granted" || !("serviceWorker" in navigator))
      return;
    try {
      const registration = await navigator.serviceWorker.ready;
      await registration.showNotification(`Eslatma: ${reminder.title}`, {
        body: reminder.notes || formatDateTime(occurrence),
        icon: "icons/icon-192.png",
        badge: "icons/icon-192.png",
        tag: `eslatmachi-${reminder.id}-${occurrence}`,
        data: { url: "./" },
        requireInteraction: true
      });
    } catch {
      // The in-app alert remains available when system notifications are restricted.
    }
  }

  function checkReminders() {
    const now = Date.now();
    let changed = false;
    for (const reminder of reminders) {
      if (!isActive(reminder) || effectiveTime(reminder) > now)
        continue;
      const wasSnoozed = Boolean(reminder.snoozeUntil);
      const occurrence = effectiveTime(reminder);
      const alert = {
        id: reminder.id,
        title: reminder.title,
        notes: reminder.notes || "",
        time: occurrence
      };
      prepareForAlert(reminder, now, wasSnoozed);
      alertQueue.push(alert);
      showSystemNotification(reminder, occurrence);
      changed = true;
    }
    if (changed)
      saveReminders();
    processAlertQueue();
  }

  function queueServerSync() {
    if (!pushSubscription || !serverPushEnabled)
      return;
    clearTimeout(syncTimer);
    syncTimer = setTimeout(syncWithServer, 500);
  }

  async function syncWithServer() {
    if (!pushSubscription || !serverPushEnabled)
      return;
    if (syncInProgress) {
      syncPending = true;
      return;
    }
    syncInProgress = true;
    try {
      const response = await fetch("api/installations/" + encodeURIComponent(installationId()) + "/state", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subscription: pushSubscription, reminders })
      });
      if (!response.ok)
        throw new Error("Sync failed");
      ui.notificationText.textContent = "Ilova yopiq bo‘lganda ham server orqali xabarnoma olasiz.";
    } catch {
      ui.notificationText.textContent = "Server bilan aloqa yo‘q. Internet qaytganda eslatmalar sinxronlanadi.";
    } finally {
      syncInProgress = false;
      if (syncPending) {
        syncPending = false;
        syncWithServer();
      }
    }
  }

  function updateNotificationCard() {
    const supported = "Notification" in window && "serviceWorker" in navigator;
    const granted = supported && Notification.permission === "granted";
    ui.notificationCard.classList.toggle("success", granted);
    ui.disableNotifications.hidden = !pushSubscription;
    if (granted && pushSubscription) {
      ui.notificationTitle.textContent = "Xabarnomalar yoqilgan";
      ui.notificationText.textContent = "Ilova yopiq bo‘lganda ham server orqali xabarnoma olasiz.";
      ui.enableNotifications.hidden = true;
    } else if (granted) {
      ui.notificationTitle.textContent = "Oddiy xabarnomalar yoqilgan";
      ui.notificationText.textContent = "Xabarnomalar ilova ochiq bo‘lganda ishlaydi. Yopiq holat uchun push server kerak.";
      ui.enableNotifications.hidden = false;
      ui.enableNotifications.textContent = "Qayta tekshirish";
    } else if (supported && Notification.permission === "denied") {
      ui.notificationTitle.textContent = "Xabarnomalarga ruxsat berilmagan";
      ui.notificationText.textContent = "Brauzer yoki telefon sozlamalaridan Eslatmachi xabarnomalarini yoqing.";
      ui.enableNotifications.textContent = "Qayta tekshirish";
    } else if (isIos && !isStandalone) {
      ui.notificationTitle.textContent = "Avval bosh ekranga qo‘shing";
      ui.notificationText.textContent = "iPhone’da xabarnomalar bosh ekranga o‘rnatilgan ilovada ishlaydi.";
      ui.enableNotifications.textContent = "O‘rnatishni ko‘rish";
    } else if (!supported) {
      ui.notificationTitle.textContent = "Xabarnoma qo‘llanmaydi";
      ui.notificationText.textContent = "Eslatmalar saqlanadi, lekin bu brauzer tizim xabarnomalarini qo‘llamaydi.";
      ui.enableNotifications.hidden = true;
    }
  }

  async function enableNotifications() {
    if (isIos && !isStandalone) {
      showInstallInstructions();
      return;
    }
    if (!("Notification" in window) || !("serviceWorker" in navigator)) {
      showToast("Bu brauzer xabarnomalarni qo‘llamaydi.");
      return;
    }
    try {
      const permission = await Notification.requestPermission();
      if (permission !== "granted") {
        updateNotificationCard();
        return;
      }
      const registration = await navigator.serviceWorker.ready;
      const configResponse = await fetch("api/config", { headers: { Accept: "application/json" } });
      if (!configResponse.ok)
        throw new Error("Push server unavailable");
      const config = await configResponse.json();
      if (config.pushConfigured && "PushManager" in window) {
        pushSubscription = await registration.pushManager.getSubscription();
        if (!pushSubscription) {
          pushSubscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(config.publicKey)
          });
        }
        serverPushEnabled = true;
        updateNotificationCard();
        saveReminders();
        showToast("Xabarnomalar yoqildi va eslatmalar sinxronlandi.");
      } else {
        updateNotificationCard();
        showToast("Oddiy xabarnomalar yoqildi. Push server hali sozlanmagan.");
      }
    } catch {
      serverPushEnabled = false;
      pushSubscription = null;
      updateNotificationCard();
      showToast("Server xabarnomasi ulanmadi. Ilova ochiq holatdagi eslatmalar ishlayveradi.");
    }
  }

  async function restorePushState() {
    if (!("Notification" in window) || Notification.permission !== "granted" || !("serviceWorker" in navigator))
      return;
    try {
      const registration = await navigator.serviceWorker.ready;
      pushSubscription = await registration.pushManager.getSubscription();
      if (pushSubscription) {
        const response = await fetch("api/config", { headers: { Accept: "application/json" } });
        const config = await response.json();
        serverPushEnabled = response.ok && config.pushConfigured;
      }
    } catch {
      pushSubscription = null;
      serverPushEnabled = false;
    }
    updateNotificationCard();
    queueServerSync();
  }

  async function disableServerNotifications() {
    try {
      if (pushSubscription) {
        await fetch("api/installations/" + encodeURIComponent(installationId()), { method: "DELETE" });
        await pushSubscription.unsubscribe();
      }
    } catch {
      showToast("Server bilan aloqa bo‘lmadi. Brauzer sozlamalaridan ham o‘chirishingiz mumkin.");
    }
    pushSubscription = null;
    serverPushEnabled = false;
    updateNotificationCard();
    showToast("Server xabarnomalari o‘chirildi.");
  }

  function urlBase64ToUint8Array(value) {
    const padding = "=".repeat((4 - value.length % 4) % 4);
    const base64 = (value + padding).replace(/-/g, "+").replace(/_/g, "/");
    const raw = atob(base64);
    return Uint8Array.from(raw, (character) => character.charCodeAt(0));
  }

  function showInstallInstructions() {
    if (isStandalone) {
      ui.installInstructions.innerHTML = "<p><strong>Eslatmachi bosh ekranga qo‘shilgan.</strong> Endi uni oddiy ilova kabi ochishingiz mumkin.</p>";
    } else if (isIos) {
      ui.installInstructions.innerHTML = "<p>iPhone’da quyidagi amallarni bajaring:</p><ol><li>Sahifani <strong>Safari</strong> brauzerida oching.</li><li>Pastdagi yoki yuqoridagi <strong>Ulashish</strong> tugmasini bosing.</li><li><strong>Bosh ekranga qo‘shish</strong> bandini tanlang.</li><li><strong>Qo‘shish</strong> tugmasini bosing. Eslatmachi ikonkasi paydo bo‘ladi.</li></ol>";
    } else {
      ui.installInstructions.innerHTML = "<p>Brauzer menyusidan <strong>Ilovani o‘rnatish</strong> yoki <strong>Bosh ekranga qo‘shish</strong> bandini tanlang.</p>";
    }
    ui.browserInstall.hidden = !deferredInstallPrompt;
    ui.installOverlay.hidden = false;
    syncModalState();
  }

  ui.openEditor.addEventListener("click", () => openEditor());
  ui.closeEditor.addEventListener("click", closeEditor);
  ui.cancelEditor.addEventListener("click", closeEditor);
  ui.editorOverlay.addEventListener("click", (event) => {
    if (event.target === ui.editorOverlay)
      closeEditor();
  });

  ui.reminderForm.addEventListener("submit", (event) => {
    event.preventDefault();
    const title = ui.titleInput.value.trim();
    const remindAt = selectedDateTime();
    if (!title) {
      ui.formError.textContent = "Ish nomini kiriting.";
      ui.formError.hidden = false;
      ui.titleInput.focus();
      return;
    }
    if (!Number.isFinite(remindAt) || remindAt <= Date.now()) {
      ui.formError.textContent = "Eslatma vaqti hozirgi vaqtdan keyin bo‘lishi kerak.";
      ui.formError.hidden = false;
      return;
    }

    let reminder = reminders.find((item) => item.id === editingId);
    if (!reminder) {
      reminder = { id: createId(), completed: false, createdAt: Date.now() };
      reminders.push(reminder);
    }
    reminder.title = title;
    reminder.notes = ui.notesInput.value.trim();
    reminder.remindAt = remindAt;
    reminder.repeat = ui.repeatInput.value;
    reminder.snoozeUntil = null;
    reminder.completed = false;
    reminder.updatedAt = Date.now();
    saveReminders();
    closeEditor();
    showToast("Eslatma saqlandi.");
  });

  document.querySelectorAll(".filter-button").forEach((button) => {
    button.addEventListener("click", () => {
      currentFilter = button.dataset.filter;
      document.querySelectorAll(".filter-button").forEach((item) => {
        const selected = item === button;
        item.classList.toggle("active", selected);
        item.setAttribute("aria-selected", String(selected));
      });
      render();
    });
  });

  ui.reminderList.addEventListener("click", (event) => {
    const button = event.target.closest("button[data-action]");
    if (!button)
      return;
    const card = button.closest(".reminder-card");
    const reminder = reminders.find((item) => item.id === card.dataset.id);
    if (!reminder)
      return;
    if (button.dataset.action === "edit") {
      openEditor(reminder);
    } else if (button.dataset.action === "complete") {
      completeReminder(reminder);
      saveReminders();
      showToast(reminder.repeat === "none" ? "Eslatma bajarildi." : "Keyingi eslatma sanasi belgilandi.");
    } else if (button.dataset.action === "delete") {
      if (!window.confirm(`“${reminder.title}” eslatmasi o‘chirilsinmi?`))
        return;
      reminders = reminders.filter((item) => item.id !== reminder.id);
      saveReminders();
      showToast("Eslatma o‘chirildi.");
    }
  });

  ui.alertDone.addEventListener("click", closeAlert);
  ui.alertClose.addEventListener("click", closeAlert);
  ui.alertSnooze.addEventListener("click", () => {
    const reminder = reminders.find((item) => item.id === activeAlert.id);
    if (reminder) {
      reminder.snoozeUntil = Date.now() + 10 * 60 * 1000;
      reminder.completed = false;
      reminder.updatedAt = Date.now();
      saveReminders();
      showToast("Eslatma 10 daqiqaga kechiktirildi.");
    }
    closeAlert();
  });

  ui.enableNotifications.addEventListener("click", enableNotifications);
  ui.disableNotifications.addEventListener("click", disableServerNotifications);
  ui.installButton.addEventListener("click", showInstallInstructions);
  ui.closeInstall.addEventListener("click", () => { ui.installOverlay.hidden = true; syncModalState(); });
  ui.installUnderstood.addEventListener("click", () => { ui.installOverlay.hidden = true; syncModalState(); });
  ui.installOverlay.addEventListener("click", (event) => {
    if (event.target === ui.installOverlay) {
      ui.installOverlay.hidden = true;
      syncModalState();
    }
  });
  ui.browserInstall.addEventListener("click", async () => {
    if (!deferredInstallPrompt)
      return;
    deferredInstallPrompt.prompt();
    await deferredInstallPrompt.userChoice;
    deferredInstallPrompt = null;
    ui.installOverlay.hidden = true;
    syncModalState();
  });

  window.addEventListener("beforeinstallprompt", (event) => {
    event.preventDefault();
    deferredInstallPrompt = event;
  });
  window.addEventListener("appinstalled", () => showToast("Eslatmachi bosh ekranga qo‘shildi."));
  window.addEventListener("online", queueServerSync);
  document.addEventListener("visibilitychange", () => {
    if (!document.hidden)
      checkReminders();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape")
      return;
    if (!ui.alertOverlay.hidden)
      closeAlert();
    else if (!ui.editorOverlay.hidden)
      closeEditor();
    else if (!ui.installOverlay.hidden) {
      ui.installOverlay.hidden = true;
      syncModalState();
    }
  });

  ui.todayLabel.textContent = new Intl.DateTimeFormat("uz-UZ", {
    weekday: "long", day: "numeric", month: "long", year: "numeric"
  }).format(new Date());
  if (isStandalone)
    ui.installButton.hidden = true;
  render();
  updateNotificationCard();
  checkReminders();
  setInterval(checkReminders, 1000);

  if ("serviceWorker" in navigator) {
    window.addEventListener("load", () => {
      navigator.serviceWorker.register("sw.js").then(restorePushState).catch(() => {
        ui.notificationText.textContent = "Oflayn xizmat ulanmadi. Sahifani HTTPS orqali oching.";
      });
    });
  }
})();
