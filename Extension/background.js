// background.js -- Batch 2: tab management, koneksi PERSISTEN ke native host.
//
// Beda dari Batch 1: koneksi dibuka SEKALI saat service worker start, TETAP
// TERBUKA (bukan connect-sekali-lalu-disconnect). Ini penting supaya native
// host bisa kapan saja kirim COMMAND ke sini (dipicu dari Studio lewat named
// pipe), bukan cuma nunggu extension yang mulai duluan.
//
// CATATAN soal siklus hidup service worker MV3: selama "port" (hasil
// connectNative) masih terbuka, Chrome TIDAK akan mematikan service worker
// ini karena idle -- itu perilaku resmi yang didokumentasikan Google, jadi
// aman dipakai untuk skenario "selalu siap terima command" seperti ini.

const NATIVE_HOST_NAME = "com.studio.nativehost";
let port = null;

function connectToNativeHost() {
  try {
    port = chrome.runtime.connectNative(NATIVE_HOST_NAME);
  } catch (err) {
    console.error("[Studio Bridge] Gagal connect ke native host:", err.message);
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
      console.error("[Studio Bridge] Gagal kirim balasan (port mungkin sudah tertutup):", err.message);
    }
  });

  port.onDisconnect.addListener(() => {
    const reason = chrome.runtime.lastError ? chrome.runtime.lastError.message : "tidak diketahui";
    console.warn("[Studio Bridge] Koneksi ke native host terputus:", reason);
    port = null;
    scheduleReconnect();
  });

  console.log("[Studio Bridge] Terhubung ke native host, siap terima command.");
}

function scheduleReconnect() {
  setTimeout(connectToNativeHost, 2000);
}

/**
 * Dispatch satu action ke chrome.tabs.* API yang sesuai. Semua method di
 * sini API RESMI Chrome/Edge (didokumentasikan di developer.chrome.com),
 * bukan tebakan seperti kasus-kasus OpenRPA sebelumnya.
 */
async function handleAction(action, message) {
  switch (action) {
    case "listTabs": {
      const tabs = await chrome.tabs.query({});
      return tabs.map(t => ({
        id: t.id,
        url: t.url,
        title: t.title,
        active: t.active,
        windowId: t.windowId
      }));
    }

    case "openTab": {
      const tab = await chrome.tabs.create({
        url: message.url,
        active: message.active !== false
      });
      return { id: tab.id, url: tab.url, title: tab.title };
    }

    case "activateTab": {
      await chrome.tabs.update(message.tabId, { active: true });
      const tab = await chrome.tabs.get(message.tabId);
      // Aktifkan juga window-nya (kalau ada beberapa window browser terbuka,
      // tab bisa "active" di window-nya tapi window itu sendiri tidak fokus).
      if (tab.windowId != null) {
        await chrome.windows.update(tab.windowId, { focused: true });
      }
      return { id: tab.id, url: tab.url, title: tab.title };
    }

    case "closeTab": {
      await chrome.tabs.remove(message.tabId);
      return { closed: message.tabId };
    }

    case "ping": {
      // Tetap dipertahankan dari Batch 1, berguna untuk cek koneksi hidup.
      return { pong: true, timestamp: Date.now() };
    }

    default:
      throw new Error(`Unknown action: ${action}`);
  }
}

// Buka koneksi begitu service worker ini start (bukan nunggu event lain).
connectToNativeHost();
