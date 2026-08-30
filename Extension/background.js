// background.js -- Batch 3: interaksi elemen (klik, ketik, ambil teks, highlight).
//
// Pendekatan: chrome.scripting.executeScript menyuntikkan FUNGSI langsung ke
// dalam konteks halaman tab target, dijalankan SEKALI saat dipanggil (bukan
// content script yang nempel permanen di semua halaman) -- lebih ringan dan
// pas dengan arsitektur kita yang "on demand per command".
//
// Selector yang dipakai: CSS Selector standar (document.querySelector).
// Cara dapetinnya: klik kanan elemen di halaman -> Inspect -> klik kanan
// baris HTML-nya di DevTools -> Copy -> Copy selector. Tidak perlu UI
// "Indicate on screen" custom untuk versi ini.

const NATIVE_HOST_NAME = "com.studio.nativehost";
let port = null;
let connecting = false; // BARU: guard, cegah connectToNativeHost() jalan dobel

function connectToNativeHost() {
  // BARU Batch 7: cegah 2 koneksi/proses native host kebentuk bersamaan.
  // Bug sebelumnya: connectToNativeHost() dipanggil DUA KALI hampir
  // bersamaan (sekali dari baris paling bawah file yang jalan begitu
  // service worker start, sekali lagi dari onStartup listener) -- kalau
  // dua-duanya kebetulan jalan sebelum salah satu sempat selesai/kasih
  // tahu port sudah terisi, dua-duanya lolos dan bikin 2 native host
  // process. Guard ini pastikan cuma SATU proses connect yang jalan.
  if (port || connecting) {
    console.log("[Studio Bridge] Sudah ada koneksi aktif/sedang connect, skip.");
    return;
  }
  connecting = true;

  try {
    port = chrome.runtime.connectNative(NATIVE_HOST_NAME);
  } catch (err) {
    console.error("[Studio Bridge] Gagal connect ke native host:", err.message);
    connecting = false;
    scheduleReconnect();
    return;
  }

  port.onMessage.addListener(async (message) => {
    const { id, action } = message || {};
    let response;
    try {
      const data = await handleAction(action, message);
      response = { id, success: true, data };
    } catch (err) {
      response = { id, success: false, error: err.message };
    }
    try {
      port.postMessage(response);
    } catch (err) {
      console.error("[Studio Bridge] Gagal kirim balasan:", err.message);
    }
  });

  port.onDisconnect.addListener(() => {
    const reason = chrome.runtime.lastError ? chrome.runtime.lastError.message : "tidak diketahui";
    console.warn("[Studio Bridge] Koneksi ke native host terputus:", reason);
    port = null;
    connecting = false; // reset supaya reconnect berikutnya tidak ke-skip guard
    scheduleReconnect();
  });

  connecting = false; // koneksi sudah terbentuk, bukan "sedang proses" lagi
  console.log("[Studio Bridge] Terhubung ke native host, siap terima command.");
}

function scheduleReconnect() {
  setTimeout(connectToNativeHost, 2000);
}

async function handleAction(action, message) {
  switch (action) {
    // ----- Batch 2: tab management (tidak berubah) -----
    case "listTabs": {
      const tabs = await chrome.tabs.query({});
      return tabs.map(t => ({ id: t.id, url: t.url, title: t.title, active: t.active, windowId: t.windowId }));
    }
    case "openTab": {
      const tab = await chrome.tabs.create({ url: message.url, active: message.active !== false });
      return { id: tab.id, url: tab.url, title: tab.title };
    }
    case "activateTab": {
      await chrome.tabs.update(message.tabId, { active: true });
      const tab = await chrome.tabs.get(message.tabId);
      if (tab.windowId != null) await chrome.windows.update(tab.windowId, { focused: true });
      return { id: tab.id, url: tab.url, title: tab.title };
    }
    case "closeTab": {
      await chrome.tabs.remove(message.tabId);
      return { closed: message.tabId };
    }

    // ----- BARU Batch 3: interaksi elemen, BARU Batch 6: dukung timeoutMs (retry nunggu elemen) -----
    case "click": {
      return await runInPage(message.tabId, clickElementFn, [message.selector, message.timeoutMs]);
    }
    case "setText": {
      return await runInPage(message.tabId, setTextElementFn, [message.selector, message.text, message.timeoutMs]);
    }
    case "getText": {
      return await runInPage(message.tabId, getTextElementFn, [message.selector, message.timeoutMs]);
    }
    case "highlight": {
      return await runInPage(message.tabId, highlightElementFn, [message.selector, message.timeoutMs]);
    }
    case "startPicker": {
      // Timeout lebih panjang ditangani di sisi native host (lihat
      // Program.cs) -- di sini kita cuma tunggu Promise dari pickerFn
      // resolve, berapa lama pun user butuh buat klik elemen di layar.
      const pickResult = await runInPage(message.tabId, pickerFn, []);

      // BARU: setelah elemen ke-pick, ambil screenshot AREA ELEMEN ITU
      // SAJA (bukan halaman penuh) -- mirip "Informative Screenshot"
      // UiPath. chrome.tabs.captureVisibleTab() cuma bisa dipanggil dari
      // context background.js (bukan dari fungsi yang di-inject ke
      // halaman), makanya dilakukan di SINI, bukan di dalam pickerFn.
      // Kalau capture gagal karena alasan apa pun, TIDAK fatal -- Indicate
      // tetap sukses, cuma tanpa screenshot.
      try {
        pickResult.screenshotBase64 = await captureElementScreenshot(
          message.tabId, pickResult.rect, pickResult.devicePixelRatio);
      } catch (err) {
        console.warn("[Studio Bridge] Gagal ambil screenshot elemen:", err.message);
      }

      return pickResult;
    }

    case "ping":
      return { pong: true, timestamp: Date.now() };

    default:
      throw new Error(`Unknown action: ${action}`);
  }
}

/**
 * Jalankan satu fungsi DI DALAM konteks halaman tab target, ambil hasilnya.
 * Fungsi yang dioper harus SELF-CONTAINED (tidak boleh pakai variabel dari
 * luar closure background.js ini) -- itu batasan chrome.scripting.executeScript,
 * data cuma bisa dioper lewat parameter "args".
 */
/**
 * BARU: TabId sekarang OPSIONAL. Kalau tidak diisi (undefined/null),
 * otomatis pakai tab yang SEDANG AKTIF -- pola yang sama seperti
 * GetForegroundWindow() di Maximize Window. Ini penting karena robot
 * yang mulai dari "buka Chrome baru" tidak akan tahu TabId pasti sejak
 * awal (ID-nya baru ke-generate Chrome saat tab itu dibuat).
 */
async function resolveTabId(providedTabId) {
  if (providedTabId != null) return providedTabId;

  const [activeTab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!activeTab) throw new Error("Tidak ada tab aktif, dan tabId tidak diisi.");
  return activeTab.id;
}

async function runInPage(tabId, func, args) {
  // BARU Batch 8: kalau tabId TIDAK diisi manual (providedTabId asli
  // kosong), tab yang di-resolve resolveTabId() bisa jadi TRANSIENT --
  // Chrome yang BARU dinyalakan (apalagi dengan beberapa profile) kadang
  // punya tab sementara yang ke-tutup/diganti sendiri sesaat setelah
  // startup. Kalau tabId eksplisit DIISI user, TIDAK di-retry (anggap
  // memang itu targetnya, kalau salah ya salah, jangan diam-diam ganti
  // ke tab lain).
  const isAutoResolve = (tabId == null);
  const maxAttempts = isAutoResolve ? 5 : 1;
  const retryDelayMs = 500;

  let lastError;
  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const resolvedTabId = await resolveTabId(tabId);

      const results = await chrome.scripting.executeScript({
        target: { tabId: resolvedTabId },
        func,
        args
      });

      const result = results[0]?.result;
      if (!result) throw new Error("Tidak ada hasil dari halaman (tab mungkin sudah tertutup atau bukan halaman web biasa).");
      if (!result.success) throw new Error(result.error || "Aksi gagal di halaman.");
      return result;
    } catch (err) {
      lastError = err;
      // Kalau errornya soal tab yang sudah tidak ada ("No tab with id"),
      // DAN ini mode auto-resolve, DAN masih ada percobaan tersisa --
      // resolve ULANG (mungkin tab aktif sudah berubah ke yang stabil),
      // tunggu sebentar, coba lagi. Error LAIN (mis. elemen tidak
      // ditemukan) TIDAK di-retry di sini -- itu sudah ditangani retry-nya
      // sendiri di dalam waitForSelector masing-masing fungsi.
      const isTabGoneError = err.message && err.message.includes("No tab with id");
      if (isAutoResolve && isTabGoneError && attempt < maxAttempts) {
        await new Promise(r => setTimeout(r, retryDelayMs));
        continue;
      }
      throw err;
    }
  }
  throw lastError;
}

// ===================== Fungsi yang di-inject ke halaman =====================
// PENTING: fungsi-fungsi ini jalan di context HALAMAN TARGET, bukan di
// background.js -- TIDAK BISA akses variabel/fungsi lain di file ini,
// TERMASUK fungsi helper terpisah seperti "waitForSelector" (ini sempat
// jadi bug nyata: "waitForSelector is not defined" pas dijalankan,
// karena chrome.scripting.executeScript cuma nyuntik SATU fungsi yang
// dioper, tidak ikut bawa fungsi lain dari file ini). Fix: logic wait-nya
// sekarang jadi FUNGSI LOKAL DI DALAM tiap fungsi (nested function),
// bukan dipanggil dari luar -- itu boleh, karena nested function ikut
// terbawa sebagai bagian dari fungsi induknya sendiri.
//
// BARU Batch 6: keempat fungsi ini RETRY nunggu elemen muncul sampai
// `timeoutMs` (default 10 detik), bukan langsung nyerah begitu
// querySelector pertama gagal -- nyelesaiin race condition "tab baru
// dibuka, halaman belum selesai render".

async function clickElementFn(selector, timeoutMs) {
  function waitForSelector(sel, ms) {
    return new Promise((resolve, reject) => {
      const limit = ms || 10000;
      const deadline = Date.now() + limit;
      function check() {
        // Dukung XPath juga, tidak cuma CSS Selector. CSS native TIDAK
        // BISA cari elemen berdasarkan teks yang ditampilkan (tidak ada
        // :contains() bawaan browser) -- utk kasus itu, XPath (mis.
        // "//div[contains(text(),'X')]/following::input[1]") jadi
        // satu-satunya cara. Deteksi otomatis: string diawali "/" dianggap
        // XPath, selain itu dianggap CSS Selector seperti biasa.
        let el;
        try {
          el = sel.trim().startsWith('/')
            ? document.evaluate(sel, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue
            : document.querySelector(sel);
        } catch (err) {
          // BARU Batch 11 -- FIX PENTING: kalau evaluate/querySelector
          // throw (mis. XPath tidak valid), REJECT promise-nya di sini.
          // Tanpa try/catch ini, error yang lempar di iterasi setTimeout
          // manapun (bukan percobaan PERTAMA) akan HILANG BEGITU SAJA
          // (uncaught exception di task terpisah, tidak ikut alur
          // Promise sama sekali) -- bikin Promise-nya HANG SELAMANYA
          // (tidak resolve, tidak reject). Ini penyebab sebenarnya dari
          // "Pipe is broken" yang dilaporkan user: C# client dan native
          // host akhirnya sama-sama nyerah menunggu di waktu yang beda,
          // salah satunya coba tulis ke pipe yang sudah ditutup duluan.
          reject(err);
          return;
        }
        if (el) { resolve(el); return; }
        if (Date.now() >= deadline) { reject(new Error(`Elemen tidak ditemukan dalam ${limit}ms: ${sel}`)); return; }
        setTimeout(check, 200);
      }
      check();
    });
  }

  try {
    const el = await waitForSelector(selector, timeoutMs);
    el.click();
    return { success: true };
  } catch (err) {
    return { success: false, error: err.message };
  }
}

async function setTextElementFn(selector, text, timeoutMs) {
  function waitForSelector(sel, ms) {
    return new Promise((resolve, reject) => {
      const limit = ms || 10000;
      const deadline = Date.now() + limit;
      function check() {
        // Dukung XPath juga, tidak cuma CSS Selector. CSS native TIDAK
        // BISA cari elemen berdasarkan teks yang ditampilkan (tidak ada
        // :contains() bawaan browser) -- utk kasus itu, XPath (mis.
        // "//div[contains(text(),'X')]/following::input[1]") jadi
        // satu-satunya cara. Deteksi otomatis: string diawali "/" dianggap
        // XPath, selain itu dianggap CSS Selector seperti biasa.
        let el;
        try {
          el = sel.trim().startsWith('/')
            ? document.evaluate(sel, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue
            : document.querySelector(sel);
        } catch (err) {
          // BARU Batch 11 -- FIX PENTING: kalau evaluate/querySelector
          // throw (mis. XPath tidak valid), REJECT promise-nya di sini.
          // Tanpa try/catch ini, error yang lempar di iterasi setTimeout
          // manapun (bukan percobaan PERTAMA) akan HILANG BEGITU SAJA
          // (uncaught exception di task terpisah, tidak ikut alur
          // Promise sama sekali) -- bikin Promise-nya HANG SELAMANYA
          // (tidak resolve, tidak reject). Ini penyebab sebenarnya dari
          // "Pipe is broken" yang dilaporkan user: C# client dan native
          // host akhirnya sama-sama nyerah menunggu di waktu yang beda,
          // salah satunya coba tulis ke pipe yang sudah ditutup duluan.
          reject(err);
          return;
        }
        if (el) { resolve(el); return; }
        if (Date.now() >= deadline) { reject(new Error(`Elemen tidak ditemukan dalam ${limit}ms: ${sel}`)); return; }
        setTimeout(check, 200);
      }
      check();
    });
  }

  let el;
  try {
    el = await waitForSelector(selector, timeoutMs);
  } catch (err) {
    return { success: false, error: err.message };
  }

  el.focus();

  // Trik "native setter": React (dan framework serupa) meng-override property
  // setter bawaan "value" pada <input>/<textarea> supaya bisa nge-track
  // perubahan lewat virtual DOM-nya sendiri. Assign langsung "el.value = text"
  // TIDAK memicu listener React (onChange dkk), karena React tidak "melihat"
  // perubahan yang lewat setter aslinya sendiri. Solusinya: panggil setter
  // NATIVE bawaan HTMLInputElement/HTMLTextAreaElement secara eksplisit
  // (lewat Object.getOwnPropertyDescriptor ke prototype asli), baru trigger
  // event "input"/"change" manual supaya React (dkk) ikut update.
  const tag = el.tagName;
  let nativeSetter = null;
  if (tag === "TEXTAREA") {
    nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value")?.set;
  } else if (tag === "INPUT") {
    nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")?.set;
  }

  if (nativeSetter) {
    nativeSetter.call(el, text);
  } else {
    // Elemen bukan input/textarea standar (mis. contenteditable div) --
    // fallback ke textContent biasa.
    el.textContent = text;
  }

  el.dispatchEvent(new Event("input", { bubbles: true }));
  el.dispatchEvent(new Event("change", { bubbles: true }));

  return { success: true };
}

async function getTextElementFn(selector, timeoutMs) {
  function waitForSelector(sel, ms) {
    return new Promise((resolve, reject) => {
      const limit = ms || 10000;
      const deadline = Date.now() + limit;
      function check() {
        // Dukung XPath juga, tidak cuma CSS Selector. CSS native TIDAK
        // BISA cari elemen berdasarkan teks yang ditampilkan (tidak ada
        // :contains() bawaan browser) -- utk kasus itu, XPath (mis.
        // "//div[contains(text(),'X')]/following::input[1]") jadi
        // satu-satunya cara. Deteksi otomatis: string diawali "/" dianggap
        // XPath, selain itu dianggap CSS Selector seperti biasa.
        let el;
        try {
          el = sel.trim().startsWith('/')
            ? document.evaluate(sel, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue
            : document.querySelector(sel);
        } catch (err) {
          // BARU Batch 11 -- FIX PENTING: kalau evaluate/querySelector
          // throw (mis. XPath tidak valid), REJECT promise-nya di sini.
          // Tanpa try/catch ini, error yang lempar di iterasi setTimeout
          // manapun (bukan percobaan PERTAMA) akan HILANG BEGITU SAJA
          // (uncaught exception di task terpisah, tidak ikut alur
          // Promise sama sekali) -- bikin Promise-nya HANG SELAMANYA
          // (tidak resolve, tidak reject). Ini penyebab sebenarnya dari
          // "Pipe is broken" yang dilaporkan user: C# client dan native
          // host akhirnya sama-sama nyerah menunggu di waktu yang beda,
          // salah satunya coba tulis ke pipe yang sudah ditutup duluan.
          reject(err);
          return;
        }
        if (el) { resolve(el); return; }
        if (Date.now() >= deadline) { reject(new Error(`Elemen tidak ditemukan dalam ${limit}ms: ${sel}`)); return; }
        setTimeout(check, 200);
      }
      check();
    });
  }

  try {
    const el = await waitForSelector(selector, timeoutMs);
    return { success: true, text: el.innerText ?? el.textContent ?? "" };
  } catch (err) {
    return { success: false, error: err.message };
  }
}

async function highlightElementFn(selector, timeoutMs) {
  function waitForSelector(sel, ms) {
    return new Promise((resolve, reject) => {
      const limit = ms || 10000;
      const deadline = Date.now() + limit;
      function check() {
        // Dukung XPath juga, tidak cuma CSS Selector. CSS native TIDAK
        // BISA cari elemen berdasarkan teks yang ditampilkan (tidak ada
        // :contains() bawaan browser) -- utk kasus itu, XPath (mis.
        // "//div[contains(text(),'X')]/following::input[1]") jadi
        // satu-satunya cara. Deteksi otomatis: string diawali "/" dianggap
        // XPath, selain itu dianggap CSS Selector seperti biasa.
        let el;
        try {
          el = sel.trim().startsWith('/')
            ? document.evaluate(sel, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue
            : document.querySelector(sel);
        } catch (err) {
          // BARU Batch 11 -- FIX PENTING: kalau evaluate/querySelector
          // throw (mis. XPath tidak valid), REJECT promise-nya di sini.
          // Tanpa try/catch ini, error yang lempar di iterasi setTimeout
          // manapun (bukan percobaan PERTAMA) akan HILANG BEGITU SAJA
          // (uncaught exception di task terpisah, tidak ikut alur
          // Promise sama sekali) -- bikin Promise-nya HANG SELAMANYA
          // (tidak resolve, tidak reject). Ini penyebab sebenarnya dari
          // "Pipe is broken" yang dilaporkan user: C# client dan native
          // host akhirnya sama-sama nyerah menunggu di waktu yang beda,
          // salah satunya coba tulis ke pipe yang sudah ditutup duluan.
          reject(err);
          return;
        }
        if (el) { resolve(el); return; }
        if (Date.now() >= deadline) { reject(new Error(`Elemen tidak ditemukan dalam ${limit}ms: ${sel}`)); return; }
        setTimeout(check, 200);
      }
      check();
    });
  }

  let el;
  try {
    el = await waitForSelector(selector, timeoutMs);
  } catch (err) {
    return { success: false, error: err.message };
  }

  const originalOutline = el.style.outline;
  const originalOffset = el.style.outlineOffset;
  el.style.outline = "3px solid #FF0000";
  el.style.outlineOffset = "1px";
  el.scrollIntoView({ behavior: "smooth", block: "center" });

  setTimeout(() => {
    el.style.outline = originalOutline;
    el.style.outlineOffset = originalOffset;
  }, 1500);

  return { success: true };
}

/**
 * Mode "Indicate on screen": hover nge-highlight elemen, klik menangkap
 * selector-nya, Escape membatalkan. Return-nya berupa PROMISE yang resolve
 * pas user klik/Escape -- chrome.scripting.executeScript otomatis MENUNGGU
 * promise ini selesai sebelum balikin hasilnya ke background.js, jadi tidak
 * perlu mekanisme polling terpisah.
 */
function pickerFn() {
  return new Promise((resolve) => {
    let currentHighlighted = null;
    let previousOutline = "";

    function generateSelector(el) {
      // Prioritas 1 (DIUBAH -- sebelumnya ID dicek duluan): atribut yang
      // biasanya STABIL walau posisi elemen di halaman berubah-ubah (form
      // dinamis, spt RPA Challenge yang mengacak posisi field tiap
      // submit). Framework modern (Angular/React/dll) biasanya nempelin
      // salah satu dari ini ke elemen form, terikat ke FUNGSI field-nya:
      //   - name / formcontrolname -> HTML standar & Angular reactive forms
      //   - ng-reflect-name        -> Angular (dikonfirmasi dipakai di
      //                               RPA Challenge, lihat catatan)
      //   - data-testid            -> konvensi umum React/testing
      //   - aria-label             -> aksesibilitas, sering unik per field
      //
      // KENAPA DIUBAH urutannya: ID di banyak framework modern (termasuk
      // RPA Challenge, ditemukan user: "#ucLso") itu JUGA auto-generate
      // per render/reload -- BUKAN cuma posisi yang berubah, tapi ID-nya
      // sendiri juga tidak stabil. Jadi ID TIDAK otomatis lebih bisa
      // dipercaya dari atribut nama-field di framework macam ini.
      const stableAttrs = ["name", "formcontrolname", "ng-reflect-name", "data-testid", "aria-label"];
      const tag = el.tagName.toLowerCase();
      for (const attr of stableAttrs) {
        const val = el.getAttribute(attr);
        if (val) {
          return `${tag}[${attr}="${CSS.escape(val)}"]`;
        }
      }

      // Prioritas 2: ID -- dipakai kalau TIDAK ada satu pun atribut stabil
      // di atas. Masih berguna untuk situs yang ID-nya memang ditulis
      // manual oleh developer (jarang berubah), bukan auto-generate.
      if (el.id) return `#${CSS.escape(el.id)}`;

      // Fallback -- Prioritas 3: path posisional (tag+class+nth-of-type).
      // CATATAN: ini yang paling RENTAN kalau halaman-nya dinamis seperti
      // RPA Challenge -- kalau field target berubah posisi tiap kali,
      // selector jenis ini gampang salah sasaran. Prioritas 1/2 di atas
      // ada supaya kasus itu bisa dihindari SEBISA MUNGKIN.
      const path = [];
      let current = el;
      while (current && current.nodeType === Node.ELEMENT_NODE) {
        let part = current.tagName.toLowerCase();

        if (typeof current.className === "string" && current.className.trim()) {
          const classes = current.className.trim().split(/\s+/).filter(Boolean).map(c => CSS.escape(c));
          if (classes.length) part += "." + classes.join(".");
        }

        const parent = current.parentElement;
        if (parent) {
          const siblingsSameTag = Array.from(parent.children).filter(s => s.tagName === current.tagName);
          if (siblingsSameTag.length > 1) {
            const index = siblingsSameTag.indexOf(current) + 1;
            part += `:nth-of-type(${index})`;
          }
        }

        path.unshift(part);

        if (current.id) break; // sudah cukup unik, berhenti naik ke atas
        current = parent;
      }
      return path.join(" > ");
    }

    function onMouseOver(e) {
      if (currentHighlighted) currentHighlighted.style.outline = previousOutline;
      currentHighlighted = e.target;
      previousOutline = currentHighlighted.style.outline;
      currentHighlighted.style.outline = "2px solid #00897B";
      e.stopPropagation();
    }

    function onClick(e) {
      e.preventDefault();
      e.stopPropagation();
      const target = e.target;
      const selector = generateSelector(target);
      const tagName = target.tagName;
      const text = (target.innerText || "").slice(0, 60);
      // BARU: posisi elemen di viewport, dipakai background.js buat crop
      // screenshot area elemen ini saja (bukan screenshot halaman penuh).
      const rect = target.getBoundingClientRect();
      cleanup();
      resolve({
        success: true, selector, tagName, text,
        rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
        devicePixelRatio: window.devicePixelRatio || 1
      });
    }

    function onKeyDown(e) {
      if (e.key === "Escape") {
        cleanup();
        resolve({ success: false, error: "Dibatalkan (Escape ditekan)." });
      }
    }

    function cleanup() {
      if (currentHighlighted) currentHighlighted.style.outline = previousOutline;
      document.removeEventListener("mouseover", onMouseOver, true);
      document.removeEventListener("click", onClick, true);
      document.removeEventListener("keydown", onKeyDown, true);
      document.body.style.cursor = "";
    }

    document.body.style.cursor = "crosshair";
    document.addEventListener("mouseover", onMouseOver, true);
    document.addEventListener("click", onClick, true);
    document.addEventListener("keydown", onKeyDown, true);
  });
}

/**
 * Ambil screenshot area elemen (crop dari full-tab screenshot). Berjalan
 * di context background.js (BUKAN di halaman) karena chrome.tabs.
 * captureVisibleTab() cuma tersedia di context ini.
 *
 * scale (devicePixelRatio) penting: getBoundingClientRect() balikin
 * koordinat dalam CSS pixel, tapi captureVisibleTab() nangkep di
 * RESOLUSI PIKSEL ASLI layar (bisa beda kalau HiDPI/scaling >100%) --
 * tanpa dikali scale, hasil crop bisa salah posisi/ukuran di layar
 * beresolusi tinggi.
 */
async function captureElementScreenshot(tabId, rect, devicePixelRatio) {
  if (!rect || rect.width <= 0 || rect.height <= 0) {
    throw new Error("Ukuran elemen tidak valid untuk di-screenshot.");
  }

  const tab = await chrome.tabs.get(tabId);
  const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });

  const response = await fetch(dataUrl);
  const blob = await response.blob();
  const bitmap = await createImageBitmap(blob);

  const scale = devicePixelRatio || 1;
  const sx = Math.max(0, rect.x * scale);
  const sy = Math.max(0, rect.y * scale);
  const sw = Math.min(bitmap.width - sx, Math.max(1, rect.width * scale));
  const sh = Math.min(bitmap.height - sy, Math.max(1, rect.height * scale));

  const canvas = new OffscreenCanvas(sw, sh);
  const ctx = canvas.getContext("2d");
  ctx.drawImage(bitmap, sx, sy, sw, sh, 0, 0, sw, sh);

  const croppedBlob = await canvas.convertToBlob({ type: "image/png" });
  const arrayBuffer = await croppedBlob.arrayBuffer();
  return arrayBufferToBase64(arrayBuffer);
}

function arrayBufferToBase64(buffer) {
  let binary = "";
  const bytes = new Uint8Array(buffer);
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

connectToNativeHost();

// BARU: Manifest V3 service worker itu "malas" -- dia tidak otomatis
// hidup pas Chrome baru dibuka, kecuali ada trigger event yang eksplisit
// terdaftar. chrome.runtime.onStartup fires PERSIS saat browser baru
// dibuka (profile yang punya extension ini aktif) -- ini yang bikin
// service worker "kebangun" dan langsung coba connect, tanpa perlu klik
// reload manual seperti sebelumnya.
chrome.runtime.onStartup.addListener(() => {
  console.log("[Studio Bridge] Browser baru dibuka, coba connect...");
  connectToNativeHost();
});
