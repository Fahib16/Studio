# Studio Extension — Batch 3: Interaksi Elemen

## Yang baru

| Activity | Fungsi |
|---|---|
| `Studio Click` | Klik elemen |
| `Studio Set Text` | Isi teks ke input/textarea (dengan trik native-setter utk React/Vue) |
| `Studio Get Text` | Ambil teks dari elemen |
| `Studio Highlight` | Kasih border merah 1.5 detik, buat verifikasi Selector |

Semua butuh **TabId** (dari `Studio Open Tab`/`Studio List Tabs`) + **Selector**
(CSS Selector standar).

## Cara dapetin CSS Selector — TIDAK perlu tool custom

1. Buka halaman target di Chrome/Edge.
2. Klik kanan elemen yang mau ditarget → **Inspect**.
3. Di DevTools, elemen HTML-nya otomatis ke-highlight → klik kanan baris itu
   → **Copy** → **Copy selector**.
4. Paste hasilnya ke property `Selector` activity.

## Mekanisme di balik layar

`chrome.scripting.executeScript` menyuntikkan satu fungsi JS langsung ke
konteks halaman tab target, dijalankan sekali per command (bukan content
script permanen) — pas dengan arsitektur "on-demand per command" yang sudah
kita bangun dari Batch 2.

## ⚠️ Perubahan permission — PERLU DIPERHATIKAN sebelum publish

Manifest sekarang minta `"host_permissions": ["<all_urls>"]` — akses ke
SEMUA halaman web, bukan cuma tab yang lagi aktif. Ini perlu supaya bisa
kerja di tab MANAPUN yang dipilih (bukan cuma yang lagi difokus), sesuai
kebutuhan automasi.

Konsekuensi: kalau nanti publish ke Chrome Web Store, Google **akan minta
justifikasi tertulis** kenapa butuh akses seluas ini (normal untuk tools
otomasi/RPA, tapi tetap ada proses review tambahan dibanding permission
yang lebih sempit). Untuk `Unlisted`/internal use, prosesnya biasanya lebih
longgar, tapi tetap ada review dasar.

## Trik "native setter" untuk Set Text

Kalau elemen targetnya dikontrol React/Vue (banyak web app modern), assign
`element.value = text` biasa **tidak akan terdeteksi** framework-nya — kamu
akan lihat teksnya "muncul" di layar tapi tombol Submit tetap disable/form
tidak update. `setTextElementFn` di `background.js` sudah pakai native
property setter + trigger event `input`/`change` manual, supaya ini
konsisten jalan.

## Cara update

1. Ganti `manifest.json` + `background.js` di folder extension, reload di
   `chrome://extensions/`.
2. Add Existing Item ke-4 file activity baru + designer-nya ke project
   `Custom.StudioBridge` yang sudah ada. Rebuild.

## Test cepat

Buka rpachallenge.com (atau halaman apa saja), buka DevTools, ambil selector
salah satu field input, coba `Studio Set Text` dengan Selector itu + teks
apa saja, lihat apakah field-nya beneran terisi.
