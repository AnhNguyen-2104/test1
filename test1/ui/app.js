const host = window.chrome && window.chrome.webview ? window.chrome.webview : null;

const state = {
  view: "control",
  theme: "dark",
  control: {
    connection: { connected: false, ip: "192.168.3.39", port: 3000, banner: "PLC disconnected", meta: "MX Component logical station: 0", buttonText: "CONNECT SYSTEM" },
    coordinates: [],
    velocity: { value: 15, display: "1.5", register: "D406", min: 0, max: 50 },
    integrity: { state: "IDLE", detail: "STOP", tone: "idle" },
    monitorRows: []
  },
  dxf: {
    filePath: "",
    fileName: "",
    bounds: { left: 0, top: 0, width: 100, height: 100 },
    primitives: [],
    points: [],
    selectedPointKey: "",
    assignedPointKeys: {},
    processRows: []
  }
};


const dom = {};
let modalSubmit = null;

window.app = {
  receive(message) {
    handleHostMessage(message || {});
  }
};

document.addEventListener("DOMContentLoaded", () => {
  cacheDom();
  bindEvents();
  applyTheme(state.theme);
  applyView(state.view);
  post("uiReady");
});

function cacheDom() {
  dom.html = document.documentElement;
  dom.topViewButtons = Array.from(document.querySelectorAll(".top-nav [data-view]"));
  dom.sideViewButtons = Array.from(document.querySelectorAll(".side-nav [data-view]"));
  dom.placeholderButtons = Array.from(document.querySelectorAll("[data-placeholder]"));
  dom.themeToggle = document.getElementById("theme-toggle");
  dom.connectButton = document.getElementById("connect-button");
  dom.plcIp = document.getElementById("plc-ip");
  dom.plcPort = document.getElementById("plc-port");
  dom.connectionBanner = document.getElementById("connection-banner");
  dom.connectionMeta = document.getElementById("connection-meta");
  dom.sidebarStatus = document.getElementById("sidebar-status");
  dom.velocitySlider = document.getElementById("velocity-slider");
  dom.velocityValue = document.getElementById("velocity-value");
  dom.velocityRaw = document.getElementById("velocity-raw");
  dom.velocitySubtitle = document.getElementById("velocity-subtitle");
  dom.integrityState = document.getElementById("integrity-state");
  dom.integrityDetail = document.getElementById("integrity-detail");
  dom.monitorBody = document.getElementById("monitor-table-body");
  dom.monitorEmpty = document.getElementById("monitor-empty");
  dom.addRegister = document.getElementById("add-register");
  dom.emergencyStop = document.getElementById("emergency-stop");
  dom.viewControl = document.getElementById("view-control");
  dom.viewDxf = document.getElementById("view-dxf");
  dom.openDxf = document.getElementById("open-dxf");
  dom.cadPath = document.getElementById("cad-path");
  dom.cadFile = document.getElementById("cad-file");
  dom.cadPreview = document.getElementById("cad-preview");
  dom.cadPlaceholder = document.getElementById("cad-placeholder");
  dom.pointsBody = document.getElementById("points-table-body");
  dom.pointsEmpty = document.getElementById("points-empty");
  dom.processBody = document.getElementById("process-table-body");
  dom.assignButtons = Array.from(document.querySelectorAll("[data-assign-slot]"));
  dom.processButtons = Array.from(document.querySelectorAll("[data-process-key]"));
  dom.runButtons = Array.from(document.querySelectorAll("[data-run-action]"));
  dom.toastContainer = document.getElementById("toast-container");
  dom.modal = document.getElementById("prompt-modal");
  dom.modalTitle = document.getElementById("modal-title");
  dom.modalLabel = document.getElementById("modal-label");
  dom.modalInput = document.getElementById("modal-input");
  dom.modalConfirm = document.getElementById("modal-confirm");
  dom.modalCancel = document.getElementById("modal-cancel");
}

function bindEvents() {
  dom.topViewButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const view = button.dataset.view;
      state.view = view;
      applyView(view);
      post("switchView", { view });
    });
  });

  dom.sideViewButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const view = button.dataset.view;
      state.view = view;
      applyView(view);
      post("switchView", { view });
    });
  });

  dom.placeholderButtons.forEach((button) => {
    button.addEventListener("click", () => {
      showToast("info", button.dataset.placeholder, "Mục này đang để placeholder. CONTROL và DXF RUN đang hoạt động.");
    });
  });

  dom.themeToggle.addEventListener("click", () => {
    state.theme = state.theme === "dark" ? "light" : "dark";
    applyTheme(state.theme);
    post("setTheme", { theme: state.theme });
  });

  dom.connectButton.addEventListener("click", () => {
    post("connectToggle", {
      ip: dom.plcIp.value.trim(),
      port: parseInt(dom.plcPort.value, 10) || 0
    });
  });

  dom.velocitySlider.addEventListener("input", () => {
    const rawValue = parseInt(dom.velocitySlider.value, 10) || 0;
    dom.velocityValue.textContent = (rawValue / 10).toFixed(1);
    dom.velocityRaw.textContent = `Raw: ${rawValue} (${state.control.velocity.register || "D406"})`;
  });

  dom.velocitySlider.addEventListener("change", () => {
    post("setVelocity", {
      value: parseInt(dom.velocitySlider.value, 10) || 0
    });
  });

  dom.addRegister.addEventListener("click", () => {
    openPrompt("Add register", "Enter a PLC register to monitor:", "", (value) => {
      post("addRegister", { register: value });
    });
  });

  document.querySelectorAll("[data-jog-offset]").forEach((button) => {
    const offset = parseInt(button.dataset.jogOffset, 10);
    const stop = () => post("jogStop", { offset });
    button.addEventListener("pointerdown", (event) => {
      if (event.button !== 0) {
        return;
      }

      post("jogStart", { offset });
    });
    button.addEventListener("pointerup", stop);
    button.addEventListener("pointerleave", stop);
    button.addEventListener("pointercancel", stop);
  });

  dom.emergencyStop.addEventListener("click", () => post("emergencyStop"));
  dom.openDxf.addEventListener("click", () => post("openDxf"));

  dom.assignButtons.forEach((button) => {
    button.addEventListener("click", () => {
      if (!state.dxf.selectedPointKey) {
        showToast("info", "DXF", "Hãy chọn một điểm trước khi gán.");
        return;
      }

      post("assignPoint", {
        slot: button.dataset.assignSlot,
        key: state.dxf.selectedPointKey
      });
    });
  });

  dom.processButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const key = button.dataset.processKey;
      const row = state.dxf.processRows.find((item) => item.key === key);
      const currentValue = key === "speed" ? (row ? row.speed : "") : (row ? row.mCodeValue : "");
      const titleMap = {
        zDown: "Độ cao Z hạ",
        zSafe: "Độ cao Z an toàn",
        speed: "Tốc độ"
      };

      openPrompt(titleMap[key] || "Input", "Nhập giá trị:", currentValue || "", (value) => {
        post("setProcessValue", { key, value });
      });
    });
  });

  dom.runButtons.forEach((button) => {
    button.addEventListener("click", () => {
      post("runAction", { command: button.dataset.runAction });
    });
  });

  dom.monitorBody.addEventListener("click", (event) => {
    const target = event.target.closest("[data-remove-register]");
    if (!target) {
      return;
    }

    post("removeRegister", { register: target.dataset.removeRegister });
  });

  dom.pointsBody.addEventListener("click", (event) => {
    const row = event.target.closest("[data-point-key]");
    if (!row) {
      return;
    }

    state.dxf.selectedPointKey = row.dataset.pointKey;
    renderPointsTable();
    renderCadPreview();
    post("selectCadPoint", { key: state.dxf.selectedPointKey });
  });

  dom.cadPreview.addEventListener("click", (event) => {
    const target = event.target.closest("[data-point-key]");
    if (!target) {
      return;
    }

    state.dxf.selectedPointKey = target.dataset.pointKey;
    renderPointsTable();
    renderCadPreview();
    post("selectCadPoint", { key: state.dxf.selectedPointKey });
  });

  dom.modalCancel.addEventListener("click", closePrompt);
  dom.modalConfirm.addEventListener("click", submitPrompt);
  dom.modal.addEventListener("click", (event) => {
    if (event.target === dom.modal) {
      closePrompt();
    }
  });
  dom.modalInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      submitPrompt();
    }
    if (event.key === "Escape") {
      closePrompt();
    }
  });
}

function handleHostMessage(message) {
  if (!message || !message.type) {
    return;
  }

  switch (message.type) {
    case "controlState":
      state.control = message.payload || state.control;
      state.view = state.control.view || state.view;
      state.theme = state.control.theme || state.theme;
      applyTheme(state.theme);
      applyView(state.view);
      renderControl();
      break;

    case "dxfState":
      state.dxf = message.payload || state.dxf;
      state.view = state.dxf.view || state.view;
      state.theme = state.dxf.theme || state.theme;
      applyTheme(state.theme);
      applyView(state.view);
      renderDxf();
      break;

    case "notify":
      showToast(message.payload.kind, message.payload.title, message.payload.message);
      break;
  }
}

function renderControl() {
  const connection = state.control.connection || {};
  syncInputValue(dom.plcIp, connection.ip || "");
  syncInputValue(dom.plcPort, connection.port != null ? String(connection.port) : "");
  dom.connectButton.textContent = connection.buttonText || "CONNECT SYSTEM";
  dom.connectionBanner.textContent = (connection.banner || "PLC disconnected").toUpperCase();
  dom.connectionBanner.classList.toggle("connected", !!connection.connected);
  dom.connectionBanner.classList.toggle("disconnected", !connection.connected);
  dom.connectionMeta.textContent = connection.meta || "";
  dom.sidebarStatus.textContent = connection.banner || "PLC disconnected";

  (state.control.coordinates || []).forEach((coordinate) => {
    const key = coordinate.key;
    setText(`coord-${key}-value`, coordinate.display || "0.00");
    setText(`coord-${key}-raw`, `Raw: ${coordinate.raw || 0} (${coordinate.register || ""})`);
  });

  const velocity = state.control.velocity || {};
  dom.velocitySlider.min = velocity.min != null ? velocity.min : 0;
  dom.velocitySlider.max = velocity.max != null ? velocity.max : 50;
  dom.velocitySlider.value = velocity.value != null ? velocity.value : 0;
  dom.velocityValue.textContent = velocity.display || "0.0";
  dom.velocityRaw.textContent = `Raw: ${velocity.value || 0} (${velocity.register || "D406"})`;
  dom.velocitySubtitle.textContent = `Target write velocity (${velocity.register || "D406"})`;

  const integrity = state.control.integrity || {};
  dom.integrityState.textContent = integrity.state || "IDLE";
  dom.integrityState.className = `integrity-state ${integrity.tone || "idle"}`;
  dom.integrityDetail.textContent = integrity.detail || "STOP";

  renderMonitorTable();
  updateNavState();
}

function renderMonitorTable() {
  const rows = state.control.monitorRows || [];
  dom.monitorBody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.register || "")}</td>
      <td>${escapeHtml(row.value || "-")}</td>
      <td>${escapeHtml(row.status || "")}</td>
      <td><button class="row-action" data-remove-register="${escapeHtml(row.register || "")}">x</button></td>
    </tr>
  `).join("");

  dom.monitorEmpty.classList.toggle("hidden", rows.length > 0);
}

function renderDxf() {
  syncInputValue(dom.cadPath, state.dxf.filePath || "");
  syncInputValue(dom.cadFile, state.dxf.fileName || "");
  renderPointsTable();
  renderProcessTable();
  renderCadPreview();
  updateNavState();
}

function renderPointsTable() {
  const points = state.dxf.points || [];
  dom.pointsBody.innerHTML = points.map((point) => {
    const selected = point.key === state.dxf.selectedPointKey ? "is-selected" : "";
    return `
      <tr class="${selected}" data-point-key="${escapeHtml(point.key || "")}">
        <td>${escapeHtml(point.index != null ? String(point.index) : "")}</td>
        <td>${escapeHtml(point.lineType || "")}</td>
        <td>${escapeHtml(point.xDisplay || "")}</td>
        <td>${escapeHtml(point.yDisplay || "")}</td>
      </tr>
    `;
  }).join("");

  dom.pointsEmpty.classList.toggle("hidden", points.length > 0);
}

function renderProcessTable() {
  const rows = state.dxf.processRows || [];
  dom.processBody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.motionType || "")}</td>
      <td>${escapeHtml(row.mCodeValue || "")}</td>
      <td>${escapeHtml(row.dwell || "")}</td>
      <td>${escapeHtml(row.speed || "")}</td>
      <td>${escapeHtml(row.endCoordinate || "")}</td>
      <td>${escapeHtml(row.centerCoordinate || "")}</td>
    </tr>
  `).join("");
}

function renderCadPreview() {
  const primitives = state.dxf.primitives || [];
  const points = state.dxf.points || [];
  const bounds = state.dxf.bounds || { left: 0, top: 0, width: 100, height: 100 };

  if (!primitives.length) {
    dom.cadPreview.innerHTML = "";
    dom.cadPlaceholder.classList.remove("hidden");
    return;
  }

  dom.cadPlaceholder.classList.add("hidden");

  const width = 1000;
  const height = 560;
  const padding = 28;
  const worldWidth = Math.max(bounds.width || 0, 1);
  const worldHeight = Math.max(bounds.height || 0, 1);
  const scale = Math.min((width - padding * 2) / worldWidth, (height - padding * 2) / worldHeight);
  const offsetX = (width - worldWidth * scale) / 2;
  const offsetY = (height - worldHeight * scale) / 2;

  const project = (point) => {
    const x = offsetX + (point.x - bounds.left) * scale;
    const y = height - offsetY - (point.y - bounds.top) * scale;
    return { x, y };
  };

  const polylineMarkup = primitives.map((primitive) => {
    const pointsAttr = (primitive.points || []).map((point) => {
      const projected = project(point);
      return `${projected.x.toFixed(2)},${projected.y.toFixed(2)}`;
    }).join(" ");
    return `<polyline class="cad-line" points="${pointsAttr}"></polyline>`;
  }).join("");

  const pointMarkup = points.map((point) => {
    const projected = project(point);
    const selectedClass = point.key === state.dxf.selectedPointKey ? "is-selected" : "";
    return `
      <circle
        class="cad-point ${selectedClass}"
        cx="${projected.x.toFixed(2)}"
        cy="${projected.y.toFixed(2)}"
        r="4.8"
        data-point-key="${escapeHtml(point.key || "")}">
      </circle>
    `;
  }).join("");

  const assignmentMarkup = Object.entries(state.dxf.assignedPointKeys || {}).map(([slot, key]) => {
    const point = points.find((item) => item.key === key);
    if (!point) {
      return "";
    }

    const projected = project(point);
    const tone = getAssignmentTone(slot);
    return `
      <circle cx="${projected.x.toFixed(2)}" cy="${projected.y.toFixed(2)}" r="10.5" fill="${tone.fill}" stroke="white" stroke-width="1.8"></circle>
      <text class="cad-assignment-text" x="${projected.x.toFixed(2)}" y="${projected.y.toFixed(2)}">${tone.label}</text>
    `;
  }).join("");

  dom.cadPreview.innerHTML = `
    <g>${polylineMarkup}</g>
    <g>${pointMarkup}</g>
    <g>${assignmentMarkup}</g>
  `;
}

function applyTheme(theme) {
  dom.html.classList.toggle("theme-dark", theme === "dark");
  dom.html.classList.toggle("theme-light", theme !== "dark");
  dom.themeToggle.textContent = theme === "dark" ? "◐" : "◑";
}

function applyView(view) {
  const isControl = view !== "dxf";
  dom.viewControl.classList.toggle("is-active", isControl);
  dom.viewDxf.classList.toggle("is-active", !isControl);
  updateNavState();
}

function updateNavState() {
  const setActive = (button) => {
    const active = button.dataset.view === state.view;
    button.classList.toggle("is-active", active);
  };

  dom.topViewButtons.forEach(setActive);
  dom.sideViewButtons.forEach(setActive);
}

function openPrompt(title, label, currentValue, onSubmit) {
  modalSubmit = onSubmit;
  dom.modalTitle.textContent = title;
  dom.modalLabel.textContent = label;
  dom.modalInput.value = currentValue || "";
  dom.modal.classList.remove("hidden");
  dom.modalInput.focus();
  dom.modalInput.select();
}

function closePrompt() {
  modalSubmit = null;
  dom.modal.classList.add("hidden");
}

function submitPrompt() {
  if (typeof modalSubmit === "function") {
    modalSubmit(dom.modalInput.value.trim());
  }
  closePrompt();
}

function showToast(kind, title, message) {
  const toast = document.createElement("div");
  toast.className = `toast ${kind || "info"}`;
  toast.innerHTML = `
    <div class="toast-title">${escapeHtml(title || "Message")}</div>
    <div class="toast-message">${escapeHtml(message || "")}</div>
  `;
  dom.toastContainer.appendChild(toast);
  window.setTimeout(() => {
    toast.remove();
  }, 4200);
}

function post(action, payload = {}) {
  if (host) {
    host.postMessage({ action, payload });
  }
}

function syncInputValue(input, value) {
  if (!input || document.activeElement === input) {
    return;
  }

  input.value = value;
}

function setText(id, value) {
  const element = document.getElementById(id);
  if (element) {
    element.textContent = value;
  }
}

function getAssignmentTone(slot) {
  switch (slot) {
    case "start":
      return { fill: "#22c55e", label: "S" };
    case "glueStart":
      return { fill: "#f59e0b", label: "B" };
    case "glueEnd":
      return { fill: "#ef4444", label: "E" };
    default:
      return { fill: "#94a3b8", label: "?" };
  }
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}
