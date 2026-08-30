# Studio Extension — Batch 4: Indicate on Screen

## Cara kerja

1. Klik "Indicate on screen" di kartu activity (Click/Set Text/Get Text/Highlight).
2. Kalau ada >1 tab terbuka, muncul window kecil buat pilih tab dulu.
3. Popup info muncul ("klik elemen di browser, Escape buat batal").
4. Browser masuk mode crosshair — hover nge-highlight (garis teal), klik
   elemen yang mau ditarget.
5. `TabId` dan `Selector` activity otomatis keisi.

## File yang berubah/baru

| File | Perubahan |
|---|---|
| `extension/manifest.json` | Versi 0.4.0 |
| `extension/background.js` | Tambah `pickerFn` (mode crosshair, generate selector) + action `startPicker` |
| `NativeHost/Program.cs` | Timeout per-command sekarang bisa custom (field `timeoutMs`), bukan hardcode 10 detik — perlu buat `startPicker` yang bisa makan waktu lama |
| `StudioPipeClient.cs` | Tambah `StartIndicate(tabId)` — timeout default 2 menit |
| `IndicateHelper.cs` **(baru)** | Orkestrasi: pilih tab → panggil picker → return hasil |
| `TabPickerWindow.xaml(.cs)` **(baru)** | Window kecil pilih tab kalau ada >1 |
| `Studio*Designer.xaml(.cs)` (4 file) | Tambah tombol "Indicate on screen" |

## Cara update

1. Reload extension (manifest+background.js baru) di `chrome://extensions/`.
2. Ganti `Program.cs` di project `Studio.NativeHost`, rebuild.
3. Ganti `StudioPipeClient.cs` yang lama di `Custom.StudioBridge`.
4. Add Existing Item: `IndicateHelper.cs`, `TabPickerWindow.xaml` + `.xaml.cs`
   (file BARU, belum ada di project).
5. Ganti ke-4 file `Studio*Designer.xaml` + `.xaml.cs` (Click/SetText/GetText/Highlight)
   dengan versi baru ini.
6. Rebuild Solution (jangan lupa: tutup browser total dulu kalau native host
   masih jalan, sesuai pelajaran dari batch-batch sebelumnya).

## Cara dapetin selector yang bagus

`generateSelector` di `background.js` prioritasnya:
1. Kalau elemen punya `id` unik → pakai itu (`#nama-id`), paling ringkas & stabil.
2. Kalau tidak, bangun path dari tag+class+posisi, naik ke parent sampai
   ketemu sesuatu yang punya `id` atau mentok ke root.

Ini heuristik sederhana — untuk halaman yang sangat dinamis (React dengan
class random tiap render, dst), selector hasil auto-generate bisa kurang
stabil. Kalau ketemu kasus begitu, kamu tetap bisa isi `Selector` manual
(hasil `DevTools > Copy selector`, atau ditulis sendiri).
