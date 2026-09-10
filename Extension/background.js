// background.js — Studio Automation Bridge
//
// PERUBAHAN BESAR: selector sekarang bisa dua format.
//
//   1. CSS lama          "button.btn-large"
//   2. XML berjenjang    <webctrl css-selector='body>app-root' tag='DIV' />
//                        <webctrl tag='BUTTON' innertext='START' />
//
// Dibedakan dari karakter pertama: diawali '<' berarti XML. Format lama tetap
// didukung supaya workflow yang sudah terlanjur dibuat tidak rusak.
//
// Semua operasi halaman disatukan di SATU fungsi injeksi (pageOpsFn), bukan
// satu fungsi per aksi seperti sebelumnya. Alasannya: fungsi yang di-inject
// tidak bisa memanggil fungsi lain di file ini, jadi kalau dipisah-pisah,
// resolver selector harus disalin ke setiap fungsi — dan salinan yang
// tertinggal saat resolver diperbaiki adalah sumber bug yang sulit dilacak.

// Nama host DIGANTI dari "com.studio.nativehost" pada 10 September 2026.
//
// Nama lama itu ditolak Chrome dengan "Access to the specified native messaging
// host is forbidden." pada mesin pengembangan ini, dan penolakannya bertahan
// walaupun: manifestnya memuat ID ekstensi yang benar, registry HKCU menunjuk
// berkas yang benar, tidak ada kebijakan Chrome apa pun di mesin itu, dan tidak
// ada pendaftaran tandingan di HKLM mana pun.
//
// Yang membuktikan sebabnya melekat pada NAMA: sebuah nama host kedua yang
// didaftarkan dengan cara persis sama, menunjuk .exe yang sama dan mengizinkan
// ID yang sama, TERSAMBUNG dengan sukses pada saat yang sama ketika nama lama
// masih ditolak. Sumber penolakannya tidak pernah berhasil ditemukan.
//
// Mengganti nama membuang seluruh keadaan basi yang menempel pada nama lama,
// dan komputer lain tidak mewarisinya.
// Nama host, diganti dari "com.studio.nativehost" pada 10 September 2026.
//
// Penggantiannya sendiri BUKAN perbaikan — nama lama ternyata tidak pernah
// bermasalah. Nama ini dipakai karena lebih jelas kepemilikannya, dan karena
// nama lama sempat dicurigai lalu dibuktikan tidak bersalah.
//
// Yang perlu diketahui siapa pun yang menelusuri kegagalan jembatan ini:
// peramban membaca daftar native messaging host SEKALI saat dijalankan, dan
// pada satu mesin pengembangan Chrome 152 berhenti membaca pendaftaran
// tingkat-pengguna (HKCU) sama sekali — satu nama yang didaftarkan ke lima
// cabang registry sekaligus tetap dijawab "Can't find manifest". Kalau gejala
// itu muncul lagi, jalankan:
//
//     .\pasang-jembatan-chrome.ps1 -Periksa
//
// dan jalankan peramban dengan --enable-logging --v=1; Chrome menuliskan
// alasannya sendiri ke chrome_debug.log, dan itu jauh lebih cepat daripada
// menebak dari sisi ekstensi.
const NATIVE_HOST_NAME = "com.jakforge.studiobridge";

let port = null;


function connectToNativeHost() {
  const namaDipakai = NATIVE_HOST_NAME;

  try {
    port = chrome.runtime.connectNative(namaDipakai);
  } catch (err) {
    console.error("[Studio Bridge] Gagal connect ke native host:", err.message,
                  "\n  nama dicoba :", namaDipakai);


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

    // ID-nya ikut dicetak, dan itu bukan hiasan.
    //
    // Chrome menolak sambungan yang ID-nya tidak ada di allowed_origins dengan
    // kalimat "Access to the specified native messaging host is forbidden."
    // yang TIDAK menyebut ID mana pun. Tanpa baris ini, satu-satunya cara
    // mengetahui ID yang benar-benar memanggil adalah menebak dari catatan di
    // profil Chrome — dan catatan itu bisa berbeda dari kenyataan.
    console.warn(
      "[Studio Bridge] Koneksi ke native host terputus:", reason,
      "\n  ID ekstensi ini :", chrome.runtime.id,
      "\n  nama dicoba     :", namaDipakai,
      "\n  Kalau sebabnya 'forbidden', masukkan ID di atas ke allowed_origins:",
      "\n    .\\pasang-jembatan-chrome.ps1 -Izinkan " + chrome.runtime.id);


    port = null;
    scheduleReconnect();
  });


  console.log("[Studio Bridge] Terhubung ke native host, siap terima command."
              + " (nama: " + namaDipakai + ")");
}

function scheduleReconnect() {
  setTimeout(connectToNativeHost, 2000);
}

async function handleAction(action, message) {
  switch (action) {
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

    case "click":
      return await pageOp(message, "click", {
        doubleClick: message.doubleClick === true,
        button: message.button || 0,
        offsetX: message.offsetX,
        offsetY: message.offsetY,
        modifiers: message.modifiers || null
      });
    case "setText":
      return await pageOp(message, "setText", { text: message.text });
    case "getText":
      return await pageOp(message, "getText");
    case "highlight":
      return await pageOp(message, "highlight");
    case "extractTable":
      return await pageOp(message, "extractTable");
    case "getAttribute":
      return await pageOp(message, "getAttribute", { name: message.name });
    case "selectItem":
      return await pageOp(message, "selectItem", { item: message.item, byIndex: message.byIndex === true });
    case "setCheck":
      return await pageOp(message, "setCheck", { mode: message.mode || "check" });
    case "focus":
      return await pageOp(message, "focus");
    case "hover":
      return await pageOp(message, "hover");

    // ----- Navigasi tab -----
    //
    // Ini BUKAN operasi halaman: yang dikerjakan adalah tab-nya, bukan elemen
    // di dalamnya, jadi dilakukan lewat chrome.tabs dan tidak lewat pageOpsFn.
    case "navigate": {
      const tabId = await resolveTabId(message);
      await chrome.tabs.update(tabId, { url: message.url });
      if (message.waitForLoad !== false) await waitForTabComplete(tabId, message.timeoutMs || 30000);
      const tab = await chrome.tabs.get(tabId);
      return { id: tab.id, url: tab.url, title: tab.title };
    }

    case "goBack":
    case "goForward":
    case "reload": {
      const tabId = await resolveTabId(message);

      if (action === "goBack") await chrome.tabs.goBack(tabId);
      else if (action === "goForward") await chrome.tabs.goForward(tabId);
      else await chrome.tabs.reload(tabId, { bypassCache: message.bypassCache === true });

      if (message.waitForLoad !== false) await waitForTabComplete(tabId, message.timeoutMs || 30000);
      const tab = await chrome.tabs.get(tabId);
      return { id: tab.id, url: tab.url, title: tab.title };
    }

    case "screenshot": {
      const tabId = await resolveTabId(message);
      const tab = await chrome.tabs.get(tabId);

      // captureVisibleTab hanya bisa memotret tab yang sedang terlihat, jadi
      // tabnya diaktifkan dulu — tanpa ini hasilnya potret tab lain.
      await chrome.tabs.update(tabId, { active: true });
      if (tab.windowId != null) await chrome.windows.update(tab.windowId, { focused: true });

      let rect = null;
      if (message.selector) {
        const r = await runInPage(tabId, pageOpsFn, ["rect", message.selector, null]);
        if (!r || !r.success) throw new Error((r && r.error) || "Elemen tidak ditemukan untuk screenshot.");
        rect = r.rect;
        await new Promise(resolve => setTimeout(resolve, 250));   // beri waktu scroll selesai
      } else {
        await new Promise(resolve => setTimeout(resolve, 120));
      }

      const shot = await chrome.tabs.captureVisibleTab(tab.windowId, { format: "png" });
      const base64 = rect ? await cropExact(shot, rect) : shot.split(",")[1];

      return { imageBase64: base64, cropped: rect != null, tabId };
    }

    /**
     * Highlight KHUSUS UI Explorer. Sengaja aksi tersendiri, bukan flag di
     * "highlight" biasa: jalur runtime (activity StudioHighlight) jadi tidak
     * tersentuh sama sekali oleh perubahan di sini — tidak ada risiko
     * perilaku saat workflow berjalan ikut berubah karena penyesuaian
     * tampilan yang cuma dibutuhkan Explorer.
     */
    case "explorerHighlight": {
      const tabId = await resolveTabId(message);

      const tab = await chrome.tabs.get(tabId);
      await chrome.tabs.update(tabId, { active: true });
      if (tab.windowId != null) await chrome.windows.update(tab.windowId, { focused: true });

      return await runInPage(tabId, pageOpsFn, ["explorerHighlight", message.selector || "", null]);
    }

    // ----- UI Explorer -----
    case "validateSelector":
      return await pageOp(message, "validate");
    case "getElementChain":
      return await pageOp(message, "chain");
    case "getDomTree":
      return await pageOp(message, "domtree", {
        maxDepth: message.maxDepth || 30,
        maxNodes: message.maxNodes || 20000
      });

    case "indicate": {
      const tabId = await resolveTabId(message);

      const tab = await chrome.tabs.get(tabId);
      await chrome.tabs.update(tabId, { active: true });
      if (tab.windowId != null) await chrome.windows.update(tab.windowId, { focused: true });

      const timeoutMs = message.timeoutMs || 60000;
      const result = await runInPage(tabId, indicatePickerFn, [timeoutMs]);

      // Rantai leluhur diambil lewat panggilan TERPISAH memakai css path dari
      // picker, bukan dihitung di dalam picker. Dengan begitu logic penyusun
      // rantai cuma ada satu tempat (pageOpsFn) dan tidak bisa menyimpang
      // antara jalur Indicate dan jalur UI Explorer.
      if (result.cssPath) {
        try {
          const chain = await runInPage(tabId, pageOpsFn, ["chain", result.cssPath, null]);
          result.levels = chain.levels;
        } catch (err) {
          console.warn("[Studio Bridge] gagal ambil chain:", err && err.message);
        }
      }

      if (result.rect) {
        try {
          await new Promise(r => setTimeout(r, 120));
          const fullShot = await chrome.tabs.captureVisibleTab(tab.windowId, {
            format: "jpeg", quality: 50
          });
          result.screenshotBase64 = await cropScreenshot(fullShot, result.rect);
        } catch (err) {
          const msg = (err && err.message) || String(err);
          console.warn("[Studio Bridge] screenshot/crop gagal:", msg);
          result.screenshotError = msg;
        }
      }
      delete result.rect;

      return { ...result, tabId };
    }

    /**
     * Batalkan picker yang sedang berjalan di halaman.
     *
     * Dibutuhkan auto-detect hover: saat kursor berpindah dari jendela browser
     * ke aplikasi desktop, picker web harus dimatikan sebelum picker desktop
     * dinyalakan. Tanpa ini, overlay biru tertinggal di halaman dan klik
     * berikutnya bisa ditelan olehnya.
     */
    case "cancelIndicate": {
      const tabId = await resolveTabId(message);
      try {
        await chrome.scripting.executeScript({
          target: { tabId },
          func: () => { window.dispatchEvent(new CustomEvent("__studioBridgeCancelIndicate")); }
        });
      } catch (err) {
        // Tab sudah tertutup atau tidak bisa disuntik: tidak ada picker yang
        // perlu dibatalkan, jadi ini bukan kegagalan.
      }
      return { cancelled: true };
    }

    case "ping":
      return { pong: true, timestamp: Date.now() };

    default:
      throw new Error(`Unknown action: ${action}`);
  }
}

async function pageOp(message, op, payload) {
  const tabId = await resolveTabId(message);

  // timeoutMs diteruskan ke halaman: itulah lama menunggu elemen muncul.
  // Nilainya datang dari properti Timeout activity di Studio.
  return await runInPage(tabId, pageOpsFn,
    [op, message.selector || "", payload || null, message.timeoutMs || 0]);
}

function isInjectableUrl(url) {
  return !!url && /^(https?|file):/i.test(url);
}

async function resolveTabId(message) {
  if (message.tabId != null && message.tabId !== 0) return message.tabId;

  const [activeTab] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (!activeTab) throw new Error("Tidak ada tab aktif yang bisa dipakai.");

  if (isInjectableUrl(activeTab.url)) return activeTab.id;

  // JARING PENGAMAN saat robot berjalan: tab aktif bisa saja halaman New Tab
  // atau chrome:// (mis. user tidak sengaja berpindah tab, atau Chrome baru
  // dibuka). Kalau di seluruh browser HANYA ADA SATU tab yang bisa
  // diotomasi, tidak ada keraguan tab mana yang dimaksud, jadi tab itu yang
  // dipakai.
  //
  // Kalau ada BEBERAPA, sengaja tetap gagal: menebak di sini berarti robot
  // bisa mengetik atau mengklik di halaman yang salah tanpa ada yang tahu —
  // jauh lebih berbahaya daripada berhenti dengan pesan yang jelas.
  const all = await chrome.tabs.query({});
  const usable = all.filter(t => isInjectableUrl(t.url));

  if (usable.length === 1) return usable[0].id;

  const shown = (activeTab.url || "tanpa URL").split("/").slice(0, 3).join("/");

  if (usable.length === 0) {
    throw new Error(
      "Tab yang sedang aktif adalah halaman internal browser (" + shown +
      ") dan tidak ada tab halaman web lain yang terbuka. Buka dulu halaman " +
      "yang mau diotomasi, atau pakai Studio Open Tab di awal workflow."
    );
  }

  throw new Error(
    "Tab yang sedang aktif adalah halaman internal browser (" + shown +
    ") dan ada " + usable.length + " tab web lain yang terbuka, jadi tab tujuan " +
    "tidak bisa ditentukan. Isi property TabId (mis. dari Output Studio Open Tab) " +
    "supaya tab tujuannya pasti."
  );
}

async function runInPage(tabId, func, args) {
  if (tabId == null) throw new Error("tabId wajib diisi untuk interaksi elemen.");

  const results = await chrome.scripting.executeScript({ target: { tabId }, func, args });

  const result = results[0]?.result;
  if (!result) throw new Error("Tidak ada hasil dari halaman (tab mungkin sudah tertutup, atau ini halaman internal browser seperti chrome:// yang tidak bisa disuntik script).");
  if (!result.success) throw new Error(result.error || "Aksi gagal di halaman.");
  return result;
}

function dataUrlToBlob(dataUrl) {
  const comma = dataUrl.indexOf(",");
  if (comma < 0) throw new Error("Data URL tidak valid (tidak ada koma pemisah).");

  const meta = dataUrl.substring(0, comma);
  const base64 = dataUrl.substring(comma + 1);
  const mimeMatch = meta.match(/data:([^;]+)/);
  const mime = mimeMatch ? mimeMatch[1] : "image/png";

  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

  return new Blob([bytes], { type: mime });
}

async function cropScreenshot(fullDataUrl, rect) {
  const bitmap = await createImageBitmap(dataUrlToBlob(fullDataUrl));

  const ratio = rect.dpr || 1;
  const elX = Math.round(rect.left * ratio);
  const elY = Math.round(rect.top * ratio);
  const elW = Math.max(1, Math.round(rect.width * ratio));
  const elH = Math.max(1, Math.round(rect.height * ratio));

  const ctxPad = Math.round(50 * ratio);

  // Area tangkap DIBATASI dan dipusatkan ke elemen. Tanpa batas ini, elemen
  // besar (area teks, tabel, panel) menghasilkan potret hampir seluruh
  // jendela — setelah dikecilkan isinya tidak terbaca, dan hasilnya juga
  // tampak sangat berbeda dari potret elemen kecil. Dengan batas yang sama
  // di sisi web dan desktop, kedua jenis screenshot jadi seragam.
  const maxRegionW = Math.round(520 * ratio);
  const maxRegionH = Math.round(260 * ratio);

  let w = Math.min(elW + ctxPad * 2, maxRegionW);
  let h = Math.min(elH + ctxPad * 2, maxRegionH);

  const cx = elX + elW / 2;
  const cy = elY + elH / 2;
  let x = Math.round(cx - w / 2);
  let y = Math.round(cy - h / 2);

  if (x < 0) x = 0;
  if (y < 0) y = 0;
  if (x + w > bitmap.width) x = Math.max(0, bitmap.width - w);
  if (y + h > bitmap.height) y = Math.max(0, bitmap.height - h);
  w = Math.min(w, bitmap.width - x);
  h = Math.min(h, bitmap.height - y);
  if (w <= 0 || h <= 0) { x = 0; y = 0; w = bitmap.width; h = bitmap.height; }

  // Kanvas keluaran BERUKURAN TETAP, isinya diskalakan dan dipusatkan.
  // Kalau ukuran keluaran mengikuti rasio potongan, tiap kartu di canvas
  // Studio punya tinggi berbeda-beda dan hasil dari web tidak pernah sama
  // besar dengan hasil dari desktop — persis keluhan yang muncul.
  const OUT_W = 240, OUT_H = 120;

  const scale = Math.min(OUT_W / w, OUT_H / h);
  const drawW = Math.max(1, Math.round(w * scale));
  const drawH = Math.max(1, Math.round(h * scale));
  const offX = Math.round((OUT_W - drawW) / 2);
  const offY = Math.round((OUT_H - drawH) / 2);

  const canvas = new OffscreenCanvas(OUT_W, OUT_H);
  const ctx = canvas.getContext("2d");

  ctx.fillStyle = "#FFFFFF";
  ctx.fillRect(0, 0, OUT_W, OUT_H);
  ctx.drawImage(bitmap, x, y, w, h, offX, offY, drawW, drawH);

  ctx.strokeStyle = "#E53935";
  ctx.lineWidth = 2;
  ctx.strokeRect(
    offX + (elX - x) * scale,
    offY + (elY - y) * scale,
    elW * scale,
    elH * scale
  );

  const outBlob = await canvas.convertToBlob({ type: "image/jpeg", quality: 0.75 });
  const buffer = await outBlob.arrayBuffer();
  const bytes = new Uint8Array(buffer);

  let binary = "";
  const chunkSize = 0x8000;
  for (let i = 0; i < bytes.length; i += chunkSize) {
    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
  }
  return btoa(binary);
}

/**
 * Tunggu tab selesai memuat.
 *
 * Dipakai Navigate/Back/Forward/Refresh supaya activity berikutnya tidak
 * berjalan di atas halaman lama — kesalahan yang paling sering terjadi pada
 * otomasi web dan paling sulit dilacak karena kadang-kadang saja terjadi
 * (tergantung kecepatan jaringan).
 *
 * Polling status tab, bukan chrome.tabs.onUpdated: listener bisa TERLEWAT
 * kalau halaman sudah selesai sebelum listener terpasang.
 */
async function waitForTabComplete(tabId, timeoutMs) {
  const deadline = Date.now() + (timeoutMs || 30000);

  // Jeda awal supaya status "complete" milik halaman LAMA tidak terbaca
  // sebagai tanda halaman baru sudah selesai.
  await new Promise(r => setTimeout(r, 150));

  while (Date.now() < deadline) {
    try {
      const tab = await chrome.tabs.get(tabId);
      if (tab.status === "complete") return true;
    } catch (err) {
      throw new Error("Tab hilang saat menunggu halaman selesai dimuat.");
    }
    await new Promise(r => setTimeout(r, 150));
  }

  return false;   // batas waktu habis; halaman mungkin masih memuat sebagian
}

/**
 * Potong screenshot tepat seukuran elemen, TANPA penyesuaian ukuran atau
 * bingkai merah seperti cropScreenshot.
 *
 * Dibuat terpisah karena tujuannya berbeda: cropScreenshot menghasilkan
 * thumbnail seragam 240x120 untuk kartu di canvas, sedangkan yang ini
 * menghasilkan gambar yang akan disimpan user sebagai bukti/lampiran, jadi
 * justru harus apa adanya.
 */
async function cropExact(dataUrl, rect) {
  const blob = await (await fetch(dataUrl)).blob();
  const bitmap = await createImageBitmap(blob);

  const ratio = rect.dpr || 1;
  let x = Math.round(rect.left * ratio);
  let y = Math.round(rect.top * ratio);
  let w = Math.max(1, Math.round(rect.width * ratio));
  let h = Math.max(1, Math.round(rect.height * ratio));

  if (x < 0) { w += x; x = 0; }
  if (y < 0) { h += y; y = 0; }
  w = Math.min(w, bitmap.width - x);
  h = Math.min(h, bitmap.height - y);
  if (w <= 0 || h <= 0) throw new Error("Elemen berada di luar area yang terlihat.");

  const canvas = new OffscreenCanvas(w, h);
  canvas.getContext("2d").drawImage(bitmap, x, y, w, h, 0, 0, w, h);

  const outBlob = await canvas.convertToBlob({ type: "image/png" });
  const bytes = new Uint8Array(await outBlob.arrayBuffer());

  let binary = "";
  const chunkSize = 0x8000;
  for (let i = 0; i < bytes.length; i += chunkSize) {
    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
  }
  return btoa(binary);
}

// ============ SATU fungsi injeksi untuk semua operasi halaman ============
//
// SELF-CONTAINED: tidak bisa mengakses apa pun di luar dirinya. Semua helper
// (parser selector, resolver, penyusun rantai) ada di dalam sini.

async function pageOpsFn(op, selector, payload, timeoutMs) {

  // ---------- Parser selector XML ----------
  // Sengaja regex, bukan DOMParser: selector UiPath memakai kutip tunggal dan
  // sering memuat karakter yang membuat XML parser ketat menolak seluruh
  // string. Toleran lebih berguna daripada benar secara formal di sini.
  function parseSelector(sel) {
    const nodes = [];
    const nodeRe = /<\s*([\w:-]+)([^>]*?)\/?>/g;
    let m;
    while ((m = nodeRe.exec(sel)) !== null) {
      const attrs = {};
      const attrRe = /([\w:.\-]+)\s*=\s*(['"])([\s\S]*?)\2/g;
      let a;
      while ((a = attrRe.exec(m[2])) !== null) attrs[a[1].toLowerCase()] = a[3];
      nodes.push({ name: m[1].toLowerCase(), attrs });
    }
    return nodes;
  }

  // Wildcard ala UiPath: '*' banyak karakter, '?' satu karakter.
  function wildcardMatch(pattern, value) {
    if (pattern === undefined || pattern === null) return true;
    value = value === undefined || value === null ? "" : String(value);
    if (pattern.indexOf("*") < 0 && pattern.indexOf("?") < 0) return value === pattern;
    const esc = pattern
      .replace(/[.+^${}()|[\]\\]/g, "\\$&")
      .replace(/\*/g, "[\\s\\S]*")
      .replace(/\?/g, "[\\s\\S]");
    try { return new RegExp("^" + esc + "$").test(value); } catch (e) { return false; }
  }

  function ownText(el) {
    let t = "";
    for (let i = 0; i < el.childNodes.length; i++) {
      const n = el.childNodes[i];
      if (n.nodeType === 3) t += n.nodeValue;
    }
    return t.trim();
  }

  /// Merapikan spasi: semua rentetan spasi, tab, dan baris baru menjadi satu
  /// spasi. WAJIB untuk atribut teks. Contoh nyata: tombol "Download Excel"
  /// di rpachallenge.com berisi ikon yang di-float, sehingga innerText-nya
  /// mengandung BARIS BARU ("DOWNLOAD EXCEL\ncloud_download") sementara
  /// selector menyimpannya dengan satu spasi. Tanpa perapian ini, keduanya
  /// tidak pernah dianggap sama dan Click gagal dengan "Elemen tidak ditemukan".
  function normText(s) {
    return (s === undefined || s === null ? "" : String(s)).replace(/\s+/g, " ").trim();
  }

  const TEXT_ATTRS = {
    innertext: 1, visibleinnertext: 1, owntext: 1, aaname: 1, text: 1
  };

  /// Nama yang dibacakan pembaca layar untuk elemen ini — padanan "aaname"
  /// di UiPath. Urutannya mengikuti aturan penamaan aksesibilitas HTML.
  function accessibleName(el) {
    var v = el.getAttribute && el.getAttribute("aria-label");
    if (v) return normText(v);

    var by = el.getAttribute && el.getAttribute("aria-labelledby");
    if (by) {
      var ref = document.getElementById(by);
      if (ref) return normText(ref.innerText || ref.textContent);
    }

    var tag = el.tagName ? el.tagName.toUpperCase() : "";

    if (tag === "INPUT") {
      var type = (el.getAttribute("type") || "").toLowerCase();
      if (type === "button" || type === "submit" || type === "reset") return normText(el.value);
      if (el.labels && el.labels.length) return normText(el.labels[0].innerText);
      var ph = el.getAttribute("placeholder");
      if (ph) return normText(ph);
    }

    if (tag === "IMG") {
      var alt = el.getAttribute("alt");
      if (alt) return normText(alt);
    }

    var title = el.getAttribute && el.getAttribute("title");
    if (title) return normText(title);

    return normText(el.innerText || el.textContent);
  }

  function attrValue(el, key) {
    switch (key) {
      case "tag": return el.tagName.toLowerCase();
      case "innertext":
      case "visibleinnertext":
      case "text": return normText(el.innerText);
      case "owntext": return normText(ownText(el));
      case "aaname": return accessibleName(el);
      case "parentclass": return el.parentElement ? (el.parentElement.getAttribute("class") || "") : "";
      case "isleaf": return el.children.length === 0 ? "1" : "0";
      default: return el.getAttribute(key);
    }
  }

  function matchNode(el, node) {
    for (const key in node.attrs) {
      // idx bersifat posisional dan css-selector sudah dipakai saat
      // mengumpulkan kandidat, jadi keduanya tidak dicocokkan di sini.
      if (key === "idx" || key === "css-selector") continue;

      const expected = node.attrs[key];
      const actual = attrValue(el, key);

      if (key === "tag") {
        if (!wildcardMatch(expected.toLowerCase(), (actual || "").toLowerCase())) return false;
      } else if (TEXT_ATTRS[key]) {
        // Teks dibandingkan setelah spasinya dirapikan DAN tanpa membedakan
        // huruf besar-kecil. Alasan huruf: CSS text-transform membuat teks
        // yang terlihat berbeda dari teks di DOM (tombol Materialize tampil
        // KAPITAL padahal ditulis "Download Excel"), jadi keduanya harus
        // dianggap sama.
        if (!wildcardMatch(normText(expected).toLowerCase(), (actual || "").toLowerCase())) return false;
      } else {
        if (!wildcardMatch(expected, actual)) return false;
      }
    }
    return true;
  }

  function resolveXml(sel) {
    const nodes = parseSelector(sel);
    if (nodes.length === 0) return [];

    let scopes = [document];

    for (const node of nodes) {
      // <html app='chrome.exe' title='...' /> adalah tingkat JENDELA, bukan
      // elemen di dalam halaman. Kita sudah berada di tab yang benar (dipilih
      // lewat tabId), jadi tingkat ini dilewati — kalau dicocokkan sebagai
      // elemen, selector dari UiPath tidak akan pernah ketemu apa pun.
      if (node.name === "html" && !node.attrs.tag && !node.attrs["css-selector"]) continue;

      const css = node.attrs["css-selector"];
      let found = [];

      for (const scope of scopes) {
        let cands;
        try {
          if (css) cands = Array.prototype.slice.call(scope.querySelectorAll(css));
          else if (node.attrs.tag && node.attrs.tag.indexOf("*") < 0) cands = Array.prototype.slice.call(scope.querySelectorAll(node.attrs.tag));
          else cands = Array.prototype.slice.call(scope.querySelectorAll("*"));
        } catch (e) { cands = []; }

        for (const el of cands) if (matchNode(el, node)) found.push(el);
      }

      found = found.filter((v, i, arr) => arr.indexOf(v) === i);

      const idx = node.attrs.idx ? parseInt(node.attrs.idx, 10) : 0;
      if (idx >= 1) found = found[idx - 1] ? [found[idx - 1]] : [];

      scopes = found;
      if (scopes.length === 0) break;
    }

    return scopes.filter(e => e !== document);
  }

  function resolveAll(sel) {
    if (!sel) return [];
    if (/^\s*</.test(sel)) return resolveXml(sel);
    try { return Array.prototype.slice.call(document.querySelectorAll(sel)); }
    catch (e) { throw new Error("Selector tidak valid: " + e.message); }
  }

  function nthOfType(el) {
    const p = el.parentElement;
    if (!p) return 0;
    let n = 0;
    for (let i = 0; i < p.children.length; i++) {
      const c = p.children[i];
      if (c.tagName === el.tagName) { n++; if (c === el) return n; }
    }
    return 0;
  }

  function cssPathFor(el) {
    const parts = [];
    let cur = el;
    while (cur && cur.nodeType === 1 && cur.tagName !== "BODY" && cur !== document.documentElement) {
      if (cur.id) {
        let unique = false;
        try { unique = document.querySelectorAll("#" + CSS.escape(cur.id)).length === 1; } catch (e) { }
        if (unique) { parts.unshift("#" + CSS.escape(cur.id)); break; }
      }
      let part = cur.tagName.toLowerCase();
      const k = nthOfType(cur);
      const p = cur.parentElement;
      if (p) {
        let same = 0;
        for (let i = 0; i < p.children.length; i++) if (p.children[i].tagName === cur.tagName) same++;
        if (same > 1 && k > 0) part += ":nth-of-type(" + k + ")";
      }
      parts.unshift(part);
      cur = cur.parentElement;
    }
    if (parts.length === 0) return "body";
    if (parts[0].charAt(0) !== "#") parts.unshift("body");
    return parts.join(" > ");
  }

  function levelInfo(el) {
    const attrs = {};
    for (let i = 0; i < el.attributes.length; i++) {
      const a = el.attributes[i];
      if (a.name === "style") continue;
      if (a.value && a.value.length > 200) continue;
      attrs[a.name] = a.value;
    }
    // Teks dikirim dalam bentuk yang SUDAH dirapikan spasinya, sama persis
    // dengan bentuk yang dipakai saat mencocokkan. Kalau berbeda, selector
    // hasil Indicate bisa gagal mencocokkan elemen yang justru baru ditunjuk.
    return {
      tag: el.tagName.toLowerCase(),
      attrs,
      innertext: normText(el.innerText).slice(0, 120),
      visibleinnertext: normText(el.innerText).slice(0, 120),
      aaname: accessibleName(el).slice(0, 120),
      owntext: normText(ownText(el)).slice(0, 120),
      parentclass: el.parentElement ? (el.parentElement.getAttribute("class") || "") : "",
      isleaf: el.children.length === 0,
      css: cssPathFor(el)
    };
  }

  function chainFor(el) {
    const chain = [];
    let cur = el;
    while (cur && cur.nodeType === 1 && cur !== document.documentElement) {
      chain.unshift(cur);
      cur = cur.parentElement;
    }
    return chain.map(levelInfo);
  }

  // ---------- Menunggu elemen ----------

  /**
   * Cari elemen berulang kali sampai ketemu atau waktunya habis.
   *
   * Jeda 60 ms dipilih supaya elemen yang muncul cepat tetap terasa seketika,
   * sementara halaman yang lambat tetap ditunggu tanpa membebani prosesor.
   *
   * timeoutMs 0 atau kosong berarti SEKALI CARI — dipakai perkakas seperti
   * UI Explorer, yang memang ingin jawaban apa adanya saat itu juga, bukan
   * menunggu sesuatu yang mungkin tidak akan pernah muncul.
   */
  async function waitForElements(sel, timeoutMs) {
    const first = resolveAll(sel);
    if (first.length > 0 || !timeoutMs || timeoutMs <= 0) return first;

    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
      await new Promise(function (done) { setTimeout(done, 60); });

      const again = resolveAll(sel);
      if (again.length > 0) return again;
    }

    return [];
  }

  // ---------- Menunggu halaman selesai bereaksi ----------

  /** Jeda singkat; dipakai penantian di bawah. */
  function tidur(ms) {
    return new Promise(function (done) { setTimeout(done, ms); });
  }

  /**
   * Berapa lama menunggu halaman MULAI bereaksi terhadap tindakan barusan.
   *
   * NOL untuk keduanya, dan itu keputusan yang diukur, bukan diasumsikan.
   *
   * Penantian ini semula diberi tenggang 400 ms untuk klik, dengan alasan
   * penggambaran ulang bisa datang lebih lambat daripada satu bingkai. Di
   * halaman sungguhan alasan itu tidak terbukti: satu jalan RPA Challenge yang
   * tadinya 843 ms menjadi 6480 ms, dan selisihnya hampir tepat 10 klik dikali
   * 400 ms. Tenggang penuh yang selalu habis berarti TIDAK ADA perubahan DOM
   * yang terdeteksi sesudah klik -- jadi penantian itu membayar empat detik
   * untuk sesuatu yang tidak pernah ia temukan.
   *
   * Yang tetap dipertahankan: kalau halaman bereaksi SEKETIKA -- dan kerangka
   * kerja modern memang menjalankan deteksi perubahannya di akhir event, jadi
   * reaksinya sudah terlihat begitu microtask selesai -- perubahan itu tetap
   * ditunggu sampai tenang. Perlindungannya ada di tempat yang bisa dideteksi,
   * tanpa menagih biaya di tempat yang tidak.
   *
   * Kalau suatu saat muncul lagi gejala isian hilang diam-diam pada halaman
   * yang menggambar ulang dengan sangat lambat, INILAH tombolnya: naikkan
   * SETTLE_GRACE_CLICK_MS. Tapi menaikkannya menagih biaya itu pada SETIAP
   * klik di setiap workflow, jadi jangan dinaikkan tanpa gejala.
   */
  const SETTLE_GRACE_TYPE_MS = 0;
  const SETTLE_GRACE_CLICK_MS = 0;
  /** Berapa lama tanpa perubahan baru dianggap "sudah tenang". */
  const SETTLE_QUIET_MS = 60;

  /** Batas atas, untuk halaman yang memang tidak pernah diam (animasi, jam). */
  const SETTLE_MAX_MS = 1500;

  /**
   * Jalankan satu tindakan yang MENGUBAH halaman, lalu tunggu halamannya
   * selesai bereaksi sebelum melapor berhasil.
   *
   * Ini menutup kegagalan yang paling sulit dilihat di automasi peramban:
   * bukan elemen yang tidak ditemukan, melainkan elemen yang ditemukan
   * TERLALU CEPAT.
   *
   * Kerangka kerja seperti Angular tidak mengosongkan formulir setelah
   * dikirim -- ia MENGGANTI elemennya dengan yang baru. Penggantian itu bisa
   * datang seratus milidetik sesudah klik, lebih lama daripada satu putaran
   * robot. Robot yang langsung mengetik lagi akan menemukan elemen LAMA yang
   * masih ada di DOM, menulis ke situ, dan seluruh isiannya terbuang saat
   * formulir itu diganti tepat sebelum tombol Submit ditekan.
   *
   * Yang membuatnya berbahaya: pencarian elemennya BERHASIL, jadi tidak ada
   * galat apa pun. Isiannya saja yang diam-diam kosong. Di RPA Challenge
   * gejalanya "7 dari 70 isian benar" -- hanya putaran pertama yang selamat,
   * karena hanya di situ halamannya sudah tenang lebih dulu.
   *
   * Karena hasilnya bergantung siapa yang kebetulan lebih cepat, workflow yang
   * sama bisa memberi hasil berbeda di Studio dan di JakRunner.
   *
   * @param act      tindakannya; nilai kembaliannya diteruskan apa adanya
   * @param graceMs  lama menunggu reaksi PERTAMA
   * @param watchEl  elemen yang ditindak. Kalau elemen ini LEPAS dari dokumen,
   *                 itu bukti pasti halamannya menggambar ulang -- sinyal yang
   *                 jauh lebih tegas daripada sekadar "ada perubahan".
   */
  async function actAndSettle(act, graceMs, watchEl) {
    let berubah = false;
    let terakhir = 0;

    const obs = new MutationObserver(function () {
      berubah = true;
      terakhir = Date.now();
    });
    obs.observe(document.documentElement, {
      childList: true, subtree: true, attributes: true, characterData: true
    });

    const lepas = function () { return watchEl && !watchEl.isConnected; };

    let hasil;
    try {
      hasil = act();

      // Microtask dulu: kerangka kerja yang menggambar ulang secara sinkron
      // sudah selesai di titik ini, jadi kasus yang paling umum tidak
      // membayar penantian sama sekali.
      await tidur(0);

      const batasTenggang = Date.now() + graceMs;
      while (!berubah && !lepas() && Date.now() < batasTenggang) await tidur(8);

      if (berubah || lepas()) {
        const batasAkhir = Date.now() + SETTLE_MAX_MS;
        if (!terakhir) terakhir = Date.now();
        while (Date.now() < batasAkhir && Date.now() - terakhir < SETTLE_QUIET_MS) {
          await tidur(15);
        }
      }
    } finally {
      obs.disconnect();
    }

    return hasil;
  }

  // ---------- Operasi ----------

  try {
    if (op === "validate") {
      if (!selector) return { success: true, valid: false, count: 0, error: "Selector kosong." };
      try {
        const found = resolveAll(selector);
        return { success: true, valid: true, count: found.length };
      } catch (e) {
        return { success: true, valid: false, count: 0, error: e.message };
      }
    }

    if (op === "chain") {
      const found = resolveAll(selector);
      if (found.length === 0) return { success: false, error: "Elemen tidak ditemukan: " + selector };
      return { success: true, levels: chainFor(found[0]) };
    }

    if (op === "domtree") {
      const SKIP = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, META: 1, LINK: 1, HEAD: 1, TEMPLATE: 1 };
      let target = null;
      try { if (selector) { const f = resolveAll(selector); target = f.length ? f[0] : null; } } catch (e) { }

      let targetPath = null, count = 0, truncated = false;
      const maxDepth = payload && payload.maxDepth ? payload.maxDepth : 30;
      const maxNodes = payload && payload.maxNodes ? payload.maxNodes : 20000;

      function walk(el, depth, path) {
        count++;
        if (count > maxNodes) { truncated = true; return null; }

        const cls = el.getAttribute ? el.getAttribute("class") : null;
        const nm = el.getAttribute ? el.getAttribute("name") : null;

        const node = { t: el.tagName.toLowerCase(), k: nthOfType(el), ch: [] };
        if (el.id) node.i = el.id;
        if (cls) node.c = cls.length > 60 ? cls.slice(0, 60) : cls;
        if (nm) node.n = nm;

        const own = ownText(el);
        if (own) node.x = own.length > 40 ? own.slice(0, 40) : own;

        if (el === target) targetPath = path.slice();

        if (depth < maxDepth) {
          let idx = 0;
          for (let i = 0; i < el.children.length; i++) {
            const child = el.children[i];
            if (SKIP[child.tagName]) continue;
            path.push(idx);
            const built = walk(child, depth + 1, path);
            path.pop();
            if (built) { node.ch.push(built); idx++; }
          }
        }
        return node;
      }

      const root = walk(document.body, 0, []);
      if (!root) return { success: false, error: "Halaman terlalu besar untuk dibaca." };
      return { success: true, tree: root, path: targetPath, truncated, nodeCount: count };
    }

    // Operasi yang butuh satu elemen konkret.
    //
    // Elemennya DITUNGGU, tidak sekadar dicari sekali.
    //
    // Halaman modern menggambar ulang formulirnya setelah dikirim — RPA
    // Challenge melakukannya tiap baris — dan robot yang mengetik lebih cepat
    // daripada halaman menggambar akan menemui DOM yang sedang kosong. Dulu
    // pencariannya sekali jalan, sehingga hasilnya bergantung pada siapa yang
    // kebetulan lebih lambat: dijalankan dari Studio bisa berhasil, dari
    // JakRunner yang tanpa beban antarmuka bisa gagal di baris yang sama.
    //
    // Activity di sisi Studio SUDAH mengirim timeoutMs sejak awal; yang belum
    // ada justru penantiannya di sini.
    const found = await waitForElements(selector, timeoutMs);

    if (found.length === 0) {
      // Sebutkan lama penantiannya HANYA kalau memang menunggu. "Tidak
      // ditemukan setelah menunggu 0 detik" membingungkan pembaca log, dan
      // justru menyembunyikan hal yang sebenarnya penting: timeout-nya
      // memang belum diisi.
      return {
        success: false,
        error: timeoutMs > 0
          ? "Elemen tidak ditemukan setelah menunggu "
            + (Math.round(timeoutMs / 100) / 10) + " detik: " + selector
          : "Elemen tidak ditemukan: " + selector,
      };
    }

    const el = found[0];

    if (op === "click") {
      // Tombol mouse, tombol penahan (Ctrl/Shift/Alt), dan offset baru
      // ditambahkan di versi ini. Semuanya OPSIONAL: kalau payload tidak
      // menyebutkannya, perilakunya sama persis dengan sebelumnya —
      // el.click() polos untuk klik kiri tunggal.
      var button = payload && payload.button ? payload.button : 0;
      var mods = (payload && payload.modifiers) || {};
      var hasMods = mods.ctrl || mods.shift || mods.alt;
      var hasOffset = payload && (payload.offsetX != null || payload.offsetY != null);

      if (!(payload && payload.doubleClick) && button === 0 && !hasMods && !hasOffset) {
        return await actAndSettle(function () {
          el.click();
          return { success: true, matched: found.length };
        }, SETTLE_GRACE_CLICK_MS, el);
      }

      // Klik ganda TIDAK cukup dengan memanggil el.click() dua kali: banyak
      // kerangka kerja (dan handler ondblclick bawaan HTML) hanya bereaksi
      // pada event "dblclick", yang tidak pernah dihasilkan sendiri oleh
      // el.click(). Jadi urutan event aslinya ditiru, lengkap dengan
      // properti "detail" yang menandai klik ke berapa — beberapa handler
      // membedakan klik tunggal dan ganda justru lewat angka itu.
      const r = el.getBoundingClientRect();

      // Offset dihitung dari SUDUT KIRI-ATAS elemen, sama seperti di jalur
      // desktop, supaya angka yang sama berarti hal yang sama di kedua sisi.
      // Tanpa offset, titik kliknya tengah elemen.
      var clientX = payload && payload.offsetX != null
        ? Math.round(r.left + payload.offsetX)
        : Math.round(r.left + r.width / 2);
      var clientY = payload && payload.offsetY != null
        ? Math.round(r.top + payload.offsetY)
        : Math.round(r.top + r.height / 2);

      const base = {
        bubbles: true,
        cancelable: true,
        view: window,
        clientX: clientX,
        clientY: clientY,
        button: button,
        buttons: button === 2 ? 2 : (button === 1 ? 4 : 1),
        ctrlKey: !!mods.ctrl,
        shiftKey: !!mods.shift,
        altKey: !!mods.alt
      };

      var times = (payload && payload.doubleClick) ? 2 : 1;

      return await actAndSettle(function () {
        for (let i = 1; i <= times; i++) {
          el.dispatchEvent(new MouseEvent("mousedown", Object.assign({}, base, { detail: i })));
          el.dispatchEvent(new MouseEvent("mouseup", Object.assign({}, base, { detail: i })));
          el.dispatchEvent(new MouseEvent("click", Object.assign({}, base, { detail: i })));
        }

        if (times === 2) el.dispatchEvent(new MouseEvent("dblclick", Object.assign({}, base, { detail: 2 })));

        // Klik kanan tidak lengkap tanpa contextmenu: itulah event yang
        // sebenarnya dipakai halaman untuk memunculkan menunya.
        if (button === 2) el.dispatchEvent(new MouseEvent("contextmenu", Object.assign({}, base, { detail: 1 })));

        return { success: true, matched: found.length };
      }, SETTLE_GRACE_CLICK_MS, el);
    }

    if (op === "setText") {
      const text = payload ? payload.text : "";

      return await actAndSettle(function () {
        el.focus();

        let nativeSetter = null;
        if (el.tagName === "TEXTAREA") {
          nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, "value").set;
        } else if (el.tagName === "INPUT") {
          nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value").set;
        }

        if (nativeSetter) nativeSetter.call(el, text); else el.textContent = text;

        el.dispatchEvent(new Event("input", { bubbles: true }));
        el.dispatchEvent(new Event("change", { bubbles: true }));
        return { success: true, matched: found.length };
      }, SETTLE_GRACE_TYPE_MS, el);
    }

    if (op === "getText") {
      return { success: true, text: el.innerText || el.textContent || "", matched: found.length };
    }

    if (op === "highlight") {
      // Jalur RUNTIME (activity StudioHighlight). Dibiarkan persis seperti
      // sebelumnya — sudah dipakai workflow yang berjalan, jadi tidak ikut
      // diubah saat tampilan Explorer disesuaikan.
      const oldOutline = el.style.outline;
      const oldOffset = el.style.outlineOffset;
      el.style.outline = "3px solid #FF0000";
      el.style.outlineOffset = "1px";
      el.scrollIntoView({ behavior: "smooth", block: "center" });
      setTimeout(() => { el.style.outline = oldOutline; el.style.outlineOffset = oldOffset; }, 1500);
      return { success: true, matched: found.length };
    }

    if (op === "explorerHighlight") {
      // Jalur UI EXPLORER.
      //
      // Dipakai LAPISAN TERAPUNG terpisah, bukan mengubah style elemennya.
      // Kalau background-color elemen yang diubah, isinya sering tertutup
      // latar milik anak-anaknya (jadi tidak kelihatan), dan kalau pemulihan
      // style gagal, tampilan halaman ikut rusak. Lapisan terpisah tidak
      // menyentuh elemen sama sekali.
      const layer = document.createElement("div");
      layer.style.cssText =
        "position:fixed;pointer-events:none;z-index:2147483647;" +
        "background:rgba(0,176,255,0.30);" +
        "border:3px solid #00B0FF;box-sizing:border-box;border-radius:2px;" +
        "box-shadow:0 0 18px 4px rgba(0,176,255,0.55);";
      document.body.appendChild(layer);

      el.scrollIntoView({ behavior: "smooth", block: "center", inline: "center" });

      // Posisi disegarkan tiap frame: scroll halus membuat elemen bergerak
      // selama beberapa ratus milidetik, jadi posisi yang dihitung sekali di
      // awal akan meleset dan sorotan tampak tertinggal dari elemennya.
      const started = Date.now();
      const DURATION = 1800;

      function follow() {
        const r = el.getBoundingClientRect();
        layer.style.left = r.left + "px";
        layer.style.top = r.top + "px";
        layer.style.width = r.width + "px";
        layer.style.height = r.height + "px";

        if (Date.now() - started < DURATION) {
          requestAnimationFrame(follow);
        } else if (layer.parentNode) {
          layer.parentNode.removeChild(layer);
        }
      }
      requestAnimationFrame(follow);

      return { success: true, matched: found.length };
    }

    if (op === "getAttribute") {
      var attr = payload ? payload.name : "";
      if (!attr) return { success: false, error: "Nama atribut kosong." };

      var lower = attr.toLowerCase();
      var value;

      // Beberapa "atribut" yang paling sering diminta sebenarnya PROPERTI DOM,
      // bukan atribut HTML: value sebuah <input> yang sudah diketik user tidak
      // pernah muncul di getAttribute("value"). Membaca propertinya adalah yang
      // orang harapkan.
      if (lower === "value") value = el.value !== undefined ? el.value : el.getAttribute("value");
      else if (lower === "innertext" || lower === "text") value = el.innerText;
      else if (lower === "innerhtml") value = el.innerHTML;
      else if (lower === "outerhtml") value = el.outerHTML;
      else if (lower === "checked") value = el.checked === true ? "true" : "false";
      else if (lower === "selected") value = el.selected === true ? "true" : "false";
      else if (lower === "disabled") value = el.disabled === true ? "true" : "false";
      else if (lower === "tag") value = el.tagName.toLowerCase();
      else if (lower === "href" || lower === "src") value = el[lower] || el.getAttribute(attr);
      else value = el.getAttribute(attr);

      return {
        success: true,
        value: value === null || value === undefined ? "" : String(value),
        exists: value !== null && value !== undefined,
        matched: found.length
      };
    }

    if (op === "selectItem") {
      if (el.tagName !== "SELECT") return { success: false, error: "Elemen bukan <select>: " + el.tagName };

      var wanted = payload ? String(payload.item) : "";
      var byIndex = payload && payload.byIndex === true;
      var chosen = -1;

      if (byIndex) {
        chosen = parseInt(wanted, 10);
        if (!(chosen >= 0 && chosen < el.options.length))
          return { success: false, error: "Indeks pilihan di luar jangkauan: " + wanted };
      } else {
        // Dicocokkan ke TEKS yang terlihat dulu, baru ke value. Yang dilihat
        // orang saat merekam adalah teksnya, sedangkan value sering berupa kode
        // yang tidak ada di layar.
        for (var i = 0; i < el.options.length; i++) {
          if (el.options[i].text.trim() === wanted.trim()) { chosen = i; break; }
        }
        if (chosen < 0) {
          for (var j = 0; j < el.options.length; j++) {
            if (el.options[j].value === wanted) { chosen = j; break; }
          }
        }
        if (chosen < 0) return { success: false, error: "Pilihan tidak ada di dropdown: " + wanted };
      }

      el.selectedIndex = chosen;
      el.dispatchEvent(new Event("input", { bubbles: true }));
      el.dispatchEvent(new Event("change", { bubbles: true }));

      return { success: true, selectedIndex: chosen, selectedText: el.options[chosen].text, matched: found.length };
    }

    if (op === "setCheck") {
      var mode = payload ? payload.mode : "check";
      if (el.checked === undefined)
        return { success: false, error: "Elemen tidak bisa dicentang: " + el.tagName };

      var target = mode === "toggle" ? !el.checked : (mode === "uncheck" ? false : true);

      // Radio button tidak bisa "dilepas" lewat properti; melaporkannya lebih
      // jujur daripada diam-diam tidak melakukan apa-apa.
      if (el.type === "radio" && target === false)
        return { success: false, error: "Radio button tidak bisa di-uncheck." };

      if (el.checked !== target) {
        // Diklik, bukan diisi propertinya: banyak halaman baru bereaksi pada
        // event klik, dan mengisi properti saja membuat centangnya berubah di
        // layar tapi tidak di data halaman.
        el.click();
        if (el.checked !== target) {
          el.checked = target;
          el.dispatchEvent(new Event("input", { bubbles: true }));
          el.dispatchEvent(new Event("change", { bubbles: true }));
        }
      }

      return { success: true, checked: el.checked, matched: found.length };
    }

    if (op === "focus") {
      // Menaruh fokus keyboard tanpa mengklik. Berguna sebelum mengirim
      // tombol, dan untuk memicu validasi yang menunggu blur/focus.
      el.scrollIntoView({ block: "center", inline: "center" });
      try { el.focus({ preventScroll: true }); } catch (e) { el.focus(); }
      return { success: true, focused: document.activeElement === el, matched: found.length };
    }

    if (op === "hover") {
      // Halaman tidak mengenal "kursor melayang" tanpa event, jadi urutan
      // event yang sama dengan yang dikirim peramban saat mouse benar-benar
      // masuk ke elemen yang dikirimkan di sini.
      el.scrollIntoView({ block: "center", inline: "center" });
      var hr = el.getBoundingClientRect();
      var hx = hr.left + hr.width / 2;
      var hy = hr.top + hr.height / 2;

      ["pointerover", "mouseover", "pointerenter", "mouseenter", "pointermove", "mousemove"].forEach(function (name) {
        var isPointer = name.indexOf("pointer") === 0;
        var Ctor = isPointer && window.PointerEvent ? PointerEvent : MouseEvent;
        el.dispatchEvent(new Ctor(name, {
          bubbles: name !== "mouseenter" && name !== "pointerenter",
          cancelable: true,
          clientX: hx,
          clientY: hy,
          view: window
        }));
      });

      return { success: true, matched: found.length };
    }

    if (op === "rect") {
      el.scrollIntoView({ block: "center", inline: "center" });
      var r = el.getBoundingClientRect();
      return {
        success: true,
        rect: {
          left: r.left, top: r.top, width: r.width, height: r.height,
          dpr: window.devicePixelRatio || 1
        },
        matched: found.length
      };
    }

    if (op === "extractTable") {
      // Elemen yang di-Indicate belum tentu <table> itu sendiri: orang sering
      // menunjuk sel, baris, atau pembungkusnya. Jadi dicari ke ATAS dulu
      // (closest), baru ke BAWAH — urutan itu penting, karena pembungkus yang
      // memuat beberapa tabel akan mengembalikan tabel pertama yang belum
      // tentu yang dimaksud kalau dicari ke bawah lebih dulu.
      var table = el.tagName === "TABLE" ? el : (el.closest ? el.closest("table") : null);
      if (!table) table = el.querySelector("table");
      if (!table) return { success: false, error: "Tidak ada elemen <table> di sekitar selector ini." };

      var rows = [];
      var hasHeaderCell = false;
      var trs = table.querySelectorAll("tr");

      for (var i = 0; i < trs.length; i++) {
        var cells = trs[i].querySelectorAll("th, td");
        if (cells.length === 0) continue;

        var row = [];
        for (var j = 0; j < cells.length; j++) {
          var c = cells[j];
          if (i === 0 && c.tagName === "TH") hasHeaderCell = true;

          // innerText, bukan textContent: innerText mengikuti apa yang
          // TERLIHAT (baris tersembunyi ikut kosong, <br> jadi baris baru),
          // dan itu yang diharapkan orang saat menyalin tabel dari layar.
          var text = (c.innerText || c.textContent || "").replace(/\s+/g, " ").trim();

          // colspan disalin ke beberapa kolom supaya kolom di bawahnya tidak
          // bergeser. rowspan TIDAK ditangani: menanganinya butuh menyimpan
          // status antar baris, dan hasilnya tetap menebak untuk tabel yang
          // rumit. Lebih jujur membiarkannya apa adanya.
          var span = parseInt(c.getAttribute("colspan") || "1", 10);
          if (!(span >= 1)) span = 1;
          for (var k = 0; k < span; k++) row.push(text);
        }
        rows.push(row);
      }

      if (rows.length === 0) return { success: false, error: "Tabel ditemukan tapi tidak ada barisnya." };

      return { success: true, rows: rows, hasHeaderCell: hasHeaderCell, matched: found.length };
    }

    return { success: false, error: "Operasi tidak dikenal: " + op };
  } catch (e) {
    return { success: false, error: e.message };
  }
}

/**
 * Overlay picker. Sekarang juga mengembalikan cssPath, yang dipakai
 * background untuk meminta rantai leluhur lewat panggilan terpisah.
 */
function indicatePickerFn(timeoutMs) {
  return new Promise((resolve) => {
    let finished = false;
    /// Merapikan spasi, SALINAN LOKAL dari fungsi bernama sama di pageOpsFn.
    ///
    /// Wajib disalin, bukan dipakai bersama: fungsi ini disuntikkan ke halaman
    /// lewat chrome.scripting.executeScript, yang hanya mengirim SUMBER FUNGSI
    /// INI SAJA. Apa pun yang berada di luar tubuhnya tidak ikut terkirim, jadi
    /// memanggil helper dari fungsi lain di berkas ini akan gagal di halaman
    /// dengan "normText is not defined" — dan karena kegagalannya terjadi di
    /// dalam penangan klik, Promise-nya tidak pernah selesai: picker seolah
    /// tidak melakukan apa-apa sampai kehabisan waktu.
    function normText(s) {
      return (s === undefined || s === null ? "" : String(s)).replace(/\s+/g, " ").trim();
    }


    function isUnique(sel) {
      try { return document.querySelectorAll(sel).length === 1; } catch (e) { return false; }
    }

    function nthOfType(el) {
      const p = el.parentElement;
      if (!p) return 0;
      let n = 0;
      for (let i = 0; i < p.children.length; i++) {
        const c = p.children[i];
        if (c.tagName === el.tagName) { n++; if (c === el) return n; }
      }
      return 0;
    }

    function cssPathFor(el) {
      const parts = [];
      let cur = el;
      while (cur && cur.nodeType === 1 && cur.tagName !== "BODY" && cur !== document.documentElement) {
        if (cur.id) {
          const byId = "#" + CSS.escape(cur.id);
          if (isUnique(byId)) { parts.unshift(byId); break; }
        }
        let part = cur.tagName.toLowerCase();
        const p = cur.parentElement;
        if (p) {
          let same = 0;
          for (let i = 0; i < p.children.length; i++) if (p.children[i].tagName === cur.tagName) same++;
          const k = nthOfType(cur);
          if (same > 1 && k > 0) part += ":nth-of-type(" + k + ")";
        }
        parts.unshift(part);
        cur = cur.parentElement;
      }
      if (parts.length === 0) return "body";
      if (parts[0].charAt(0) !== "#") parts.unshift("body");
      return parts.join(" > ");
    }

    const box = document.createElement("div");
    box.style.cssText =
      "position:fixed;pointer-events:none;z-index:2147483647;" +
      "border:2px solid #2196F3;background:rgba(33,150,243,0.30);" +
      "box-sizing:border-box;display:none;";

    const badge = document.createElement("div");
    badge.style.cssText =
      "position:fixed;pointer-events:none;z-index:2147483647;" +
      "background:#2196F3;color:#fff;font:12px/1.4 monospace;" +
      "padding:2px 6px;border-radius:3px;display:none;white-space:nowrap;";

    const hint = document.createElement("div");
    hint.textContent = "Klik elemen yang mau dipilih   |   F2 = jeda 5 detik   |   Esc untuk batal";
    hint.style.cssText =
      "position:fixed;top:12px;left:50%;transform:translateX(-50%);" +
      "z-index:2147483647;background:#212121;color:#fff;" +
      "font:13px/1.5 sans-serif;padding:8px 16px;border-radius:4px;" +
      "pointer-events:none;box-shadow:0 2px 8px rgba(0,0,0,0.3);";

    // Panel hitungan mundur F2, sepadan dengan yang ada di picker desktop.
    const countBox = document.createElement("div");
    countBox.style.cssText =
      "position:fixed;left:50%;top:50%;transform:translate(-50%,-50%);" +
      "z-index:2147483647;pointer-events:none;display:none;text-align:center;" +
      "background:rgba(33,33,33,0.60);border:2px solid rgba(0,176,255,0.72);" +
      "border-radius:10px;padding:12px 22px;color:#fff;" +
      "font-family:sans-serif;";

    const countNumber = document.createElement("div");
    countNumber.style.cssText = "font-size:44px;font-weight:bold;line-height:1.1;";

    const countLabel = document.createElement("div");
    countLabel.textContent = "F2 lagi untuk memperpanjang";
    countLabel.style.cssText = "font-size:12px;color:#c8c8c8;margin-top:4px;";

    countBox.appendChild(countNumber);
    countBox.appendChild(countLabel);

    const cursorStyle = document.createElement("style");
    cursorStyle.textContent = "*{cursor:crosshair !important;}";

    document.body.appendChild(box);
    document.body.appendChild(badge);
    document.body.appendChild(hint);
    document.body.appendChild(countBox);
    document.head.appendChild(cursorStyle);

    let currentEl = null;

    // Listener pemilihan dipisah dari listener keyboard supaya bisa DILEPAS
    // selama jeda F2. Kalau tidak dilepas, klik untuk membuka dropdown akan
    // ditelan picker dan dropdown-nya tidak pernah terbuka — yang justru
    // menghapus gunanya jeda ini.
    function addPickListeners() {
      document.addEventListener("mousemove", onMove, true);
      document.addEventListener("click", onClick, true);
      document.addEventListener("mousedown", swallow, true);
      document.addEventListener("mouseup", swallow, true);
      cursorStyle.textContent = "*{cursor:crosshair !important;}";
    }

    function removePickListeners() {
      document.removeEventListener("mousemove", onMove, true);
      document.removeEventListener("click", onClick, true);
      document.removeEventListener("mousedown", swallow, true);
      document.removeEventListener("mouseup", swallow, true);
      cursorStyle.textContent = "";
    }

    let paused = false;
    let pauseUntil = 0;
    let pauseTimer = null;

    function beginPause() {
      // Hitungan SELALU disetel ulang, termasuk saat jeda sedang berjalan,
      // supaya F2 bisa ditekan berkali-kali untuk memperpanjang.
      pauseUntil = Date.now() + 5000;
      if (paused) return;

      paused = true;
      removePickListeners();

      box.style.display = "none";
      badge.style.display = "none";
      countBox.style.display = "block";
      hint.textContent = "Dijeda — pemilihan berlanjut otomatis setelah hitungan selesai";

      pauseTimer = setInterval(() => {
        const left = (pauseUntil - Date.now()) / 1000;
        if (left <= 0) { endPause(); return; }
        countNumber.textContent = String(Math.ceil(left));
      }, 100);
    }

    function endPause() {
      paused = false;
      if (pauseTimer) { clearInterval(pauseTimer); pauseTimer = null; }
      countBox.style.display = "none";
      hint.textContent = "Klik elemen yang mau dipilih   |   F2 = jeda 5 detik   |   Esc untuk batal";
      addPickListeners();
    }

    function cleanup() {
      finished = true;
      if (pauseTimer) { clearInterval(pauseTimer); pauseTimer = null; }
      removePickListeners();
      document.removeEventListener("keydown", onKey, true);
      window.removeEventListener("__studioBridgeCancelIndicate", onExternalCancel);
      [box, badge, hint, countBox, cursorStyle].forEach(n => { if (n.parentNode) n.parentNode.removeChild(n); });
    }

    function onMove(e) {
      const el = document.elementFromPoint(e.clientX, e.clientY);
      if (!el || el === box || el === badge || el === hint) return;
      currentEl = el;

      const r = el.getBoundingClientRect();
      box.style.display = "block";
      box.style.left = r.left + "px";
      box.style.top = r.top + "px";
      box.style.width = r.width + "px";
      box.style.height = r.height + "px";

      badge.style.display = "block";
      badge.textContent = el.tagName.toLowerCase() + (el.id ? "#" + el.id : "");
      badge.style.left = r.left + "px";
      badge.style.top = (r.top > 22 ? r.top - 22 : r.bottom + 4) + "px";
    }

    function swallow(e) { e.preventDefault(); e.stopPropagation(); }

    function onClick(e) {
      e.preventDefault();
      e.stopPropagation();
      if (finished) return;

      const target = currentEl || document.elementFromPoint(e.clientX, e.clientY);
      if (!target) return;

      try {
        const cssPath = cssPathFor(target);
        const r = target.getBoundingClientRect();
        const rect = {
          left: r.left, top: r.top, width: r.width, height: r.height,
          dpr: window.devicePixelRatio || 1
        };

        cleanup();
        resolve({
          success: true,
          cssPath,
          selector: cssPath,
          unique: isUnique(cssPath),
          rect,
          tagName: target.tagName.toLowerCase(),
          text: normText(target.innerText).slice(0, 80)
        });
      } catch (err) {
        // Kegagalan apa pun di sini HARUS tetap menyelesaikan Promise.
        //
        // Sebelum ini tidak ada penjaganya, dan akibatnya jauh lebih buruk
        // daripada sekadar satu kesalahan: Promise tidak pernah selesai, jadi
        // native host menunggu sampai batas waktunya, dan dari sisi user
        // tombol Indicate tampak "tidak melakukan apa-apa sama sekali" —
        // tanpa satu pun pesan yang menunjuk ke penyebabnya.
        cleanup();
        resolve({
          success: false,
          error: "Picker gagal menyusun hasil: " + (err && err.message ? err.message : String(err))
        });
      }
    }

    function onKey(e) {
      if (e.key === "F2") {
        e.preventDefault();
        e.stopPropagation();
        beginPause();
        return;
      }

      if (e.key === "Escape") {
        e.preventDefault();
        e.stopPropagation();
        cleanup();
        resolve({ success: false, error: "Dibatalkan (Esc ditekan)." });
      }
    }

    // Dibatalkan dari luar (aksi cancelIndicate). Dipakai auto-detect hover
    // saat kursor berpindah dari browser ke jendela desktop: picker web harus
    // mati dulu supaya overlay-nya tidak tertinggal di halaman.
    function onExternalCancel() {
      if (finished) return;
      cleanup();
      resolve({ success: false, error: "Dibatalkan (pindah ke jendela lain)." });
    }
    window.addEventListener("__studioBridgeCancelIndicate", onExternalCancel);

    addPickListeners();
    document.addEventListener("keydown", onKey, true);

    setTimeout(() => {
      if (!finished) {
        cleanup();
        resolve({ success: false, error: "Timeout: tidak ada elemen yang dipilih." });
      }
    }, timeoutMs);
  });
}

/**
 * Service worker MV3 DIMATIKAN Chrome setelah sekitar 30 detik tanpa
 * aktivitas. Saat itu terjadi, proses native host ikut berhenti dan named
 * pipe-nya hilang, sehingga Studio mendapat "tidak bisa terhubung ke native
 * host" — padahal browser dan extension-nya kelihatan normal. Satu-satunya
 * cara membangunkannya kembali adalah alarm: setTimeout TIDAK bisa dipakai
 * karena timer ikut mati bersama service worker-nya.
 *
 * Setengah menit adalah interval terkecil yang diizinkan Chrome untuk alarm
 * berulang, dan itu lebih pendek dari ambang idle-nya.
 */
const KEEPALIVE_ALARM = "studio-bridge-keepalive";

chrome.alarms.create(KEEPALIVE_ALARM, { periodInMinutes: 0.5 });

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name !== KEEPALIVE_ALARM) return;
  if (!port) connectToNativeHost();
});

// Sambungkan ulang juga saat browser dinyalakan atau extension dipasang,
// supaya user tidak perlu membuka apa pun dulu agar Studio bisa terhubung.
chrome.runtime.onStartup.addListener(() => { if (!port) connectToNativeHost(); });
chrome.runtime.onInstalled.addListener(() => { if (!port) connectToNativeHost(); });

connectToNativeHost();
