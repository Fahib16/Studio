# Studio Extension — Batch 2: Tab Management

## Yang baru dari Batch 1

| | Batch 1 | Batch 2 |
|---|---|---|
| Koneksi extension↔native host | Sekali pakai (connect→ping→pong→disconnect) | **Persisten** (connect sekali, tetap terbuka) |
| Siapa yang mulai | Extension (klik ikon) | **Studio** (lewat Named Pipe) |
| Fitur | Cuma ping-pong | `listTabs`, `openTab`, `activateTab`, `closeTab` |

## Alur lengkap

```
Studio / TestClient.ps1
      │  (Named Pipe: "StudioNativeHostPipe")
      ▼
Native Host (Program.cs)
      │  (Native Messaging: stdin/stdout, length-prefixed)
      ▼
Extension (background.js)
      │  (chrome.tabs.* API resmi)
      ▼
Browser (tab beneran dibuka/dipilih/ditutup)
```

## API yang dipakai — semua resmi Google, terdokumentasi

`chrome.tabs.query`, `chrome.tabs.create`, `chrome.tabs.update`,
`chrome.tabs.get`, `chrome.tabs.remove`, `chrome.windows.update` — semua
dari [developer.chrome.com](https://developer.chrome.com/docs/extensions/reference/api/tabs),
bukan tebakan seperti kasus OpenRPA sebelumnya.

## Format command (lewat Named Pipe, satu baris JSON + newline)

```json
{"action": "listTabs"}
{"action": "openTab", "url": "https://google.com"}
{"action": "activateTab", "tabId": 123}
{"action": "closeTab", "tabId": 123}
```

Balasannya:
```json
{"id": "...", "success": true, "data": [...]}
{"id": "...", "success": false, "error": "pesan error"}
```

## Cara update project kamu

1. **Extension**: ganti `manifest.json` dan `background.js` di folder extension
   kamu dengan versi Batch 2 ini. Reload extension-nya di `chrome://extensions/`
   (klik ikon refresh di card extension-nya).
2. **Native Host**: ganti `Program.cs` di project `Studio.NativeHost` dengan
   versi Batch 2 ini. Rebuild.
3. **Tidak ada perubahan** di `nativehost-manifest.json` atau registry —
   tetap yang sudah kamu setup dari Batch 1.

## Cara test (tanpa Studio dulu)

1. Pastikan Chrome/Edge terbuka, extension aktif (klik sekali ikonnya biar
   service worker pasti hidup, atau tunggu — dia auto-connect begitu start).
2. Buka PowerShell, masuk ke folder `TestClient`, jalankan:
   ```powershell
   .\TestClient.ps1 -Action listTabs
   ```
   Harusnya keluar daftar semua tab yang lagi kebuka (JSON array).
3. Coba buka tab baru:
   ```powershell
   .\TestClient.ps1 -Action openTab -Url "https://www.google.com"
   ```
4. Ambil salah satu `id` dari hasil `listTabs`, coba:
   ```powershell
   .\TestClient.ps1 -Action activateTab -TabId <id_tadi>
   .\TestClient.ps1 -Action closeTab -TabId <id_tadi>
   ```

## Yang PERLU disadari

- **Bridge cuma hidup kalau browser+extension aktif** — native host itu
  proses yang di-spawn browser, bukan service Windows yang selalu jalan.
  Kalau `TestClient.ps1` error "Connect timeout", cek dulu apakah
  browser+extension beneran lagi aktif.
- **Satu command diproses at a time** (pipe cuma terima 1 koneksi
  bersamaan) — cukup untuk Batch 2, tapi kalau nanti Studio perlu kirim
  banyak command cepat berurutan, ini masih aman (antre otomatis), cuma
  belum ada paralelisme.
- **Belum ada integrasi ke Studio activity sungguhan** — itu langkah
  berikutnya setelah Batch 2 ini kamu konfirmasi jalan, tinggal bikin
  custom activity yang manggil Named Pipe ini (miripnya dengan gimana
  `TestClient.ps1` manggilnya, tapi dari C# activity langsung, bukan
  PowerShell).
