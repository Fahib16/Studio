# Studio Extension — Batch 4: Indicate on Screen

Tidak perlu lagi copy selector manual dari DevTools. Klik **"Indicate on
screen"** di kartu activity → browser maju ke depan → arahkan mouse (elemen
ter-highlight biru) → klik → selector otomatis terisi ke property `Selector`.

## Perubahan rencana dari yang saya sebut kemarin

Kemarin saya bilang ini akan jadi *activity* `Studio Indicate`. Itu salah
sasaran: yang kamu butuh adalah kemudahan saat **menyusun** workflow, bukan
saat workflow jalan. Jadi ini dibuat sebagai **tombol di designer**
(pola yang sama dengan "Indicate browser on screen" di Attach Browser),
bukan activity runtime.

## File yang berubah

| File | Perubahan |
|---|---|
| `extension/manifest.json` | versi 0.4.0 (permission tidak berubah dari Batch 3) |
| `extension/background.js` | action `indicate` + overlay picker + `resolveTabId` |
| `NativeHost/Program.cs` | timeout jadi per-request (`timeoutMs`), bukan 10 detik mati |
| `Bridge/StudioPipeClient.cs` | kirim `timeoutMs`, tambah method `Indicate()` |
| `Bridge/Design/IndicateHelper.cs` | **BARU** — logic tombol, dipakai bareng 4 designer |
| 4 file designer (xaml + xaml.cs) | tambah hyperlink "Indicate on screen" |

## Cara pasang

1. **Extension**: ganti `manifest.json` + `background.js`, reload di
   `chrome://extensions/`.
2. **Native Host**: ganti `Program.cs`, tutup browser total (cek Task Manager
   tidak ada `Studio.NativeHost.exe` tersisa), Rebuild.
3. **Custom.StudioBridge**: ganti `StudioPipeClient.cs` + 4 designer lama,
   Add Existing Item untuk `IndicateHelper.cs` (file baru). Pastikan Build
   Action ke-4 `.xaml` tetap **Page**. Rebuild.

## Cara pakai

1. Buka halaman target di browser (biarkan terbuka).
2. Di Studio, drag `Studio Click` (atau Set Text/Get Text/Highlight) ke canvas.
3. Klik **"Indicate on screen"** di kartunya.
4. Browser otomatis maju ke depan. Gerakkan mouse — elemen di bawah kursor
   ter-highlight biru dengan label nama tag-nya.
5. Klik elemen yang mau dipilih. Selector otomatis masuk ke property `Selector`.
   Tekan **Esc** kalau mau batal.

## Cara selector di-generate (urutan prioritas)

1. `#id` — kalau elemen punya id dan id itu unik. Paling pendek & stabil.
2. `tag[data-testid="..."]` — juga coba `data-test`, `data-cy`, `name`,
   `aria-label`. Atribut begini biasanya sengaja dipasang developer dan
   jarang berubah.
3. Path `div > ul > li:nth-of-type(3) > a` — jalan terakhir. Berhenti naik
   begitu ketemu leluhur ber-id unik supaya tidak kepanjangan.

Kalau hasilnya ternyata cocok ke **lebih dari satu** elemen, akan muncul
peringatan — selectornya tetap diisi, tapi saat dijalankan elemen pertama
yang kena.

## Yang PERLU disadari

- **Selector otomatis tidak selalu yang paling stabil.** Kalau class/struktur
  halaman di-generate framework dan berubah tiap deploy, path `nth-of-type`
  bisa patah. Untuk elemen penting, selector buatan tangan lewat DevTools
  masih lebih tahan lama — Indicate mempercepat kasus umum, bukan
  menggantikan penilaian kamu.
- **Halaman internal browser tidak bisa** (`chrome://extensions/`,
  `edge://settings/`, dsb) — Chrome memblokir penyuntikan script di sana.
  Error-nya sudah dibuat jelas menyebut ini.
- **Selama indicate berjalan (maks 60 detik), pipe sedang terpakai** — command
  lain akan gagal connect sampai selesai. Tidak masalah untuk pemakaian
  normal (indicate itu aktivitas design-time, bukan runtime).
- Tombol indicate memakai **tab yang sedang aktif**. Kalau kamu punya banyak
  tab, aktifkan dulu tab yang benar sebelum mengklik tombolnya.

## Batasan yang belum ditangani

- Belum bisa memilih elemen **di dalam iframe** — `document.querySelector`
  hanya melihat dokumen utama. Perlu penanganan khusus kalau nanti ketemu
  kasus ini.
- Belum ada UI untuk **menyunting/memvalidasi** selector setelah diambil
  (mis. mengetes ulang berapa elemen yang cocok). Itu masuk ke UI Explorer.

---

# Pindahan dari project OpenRPA (batch ini)

Dua activity interaksi elemen yang dulu berada di project OpenRPA dipindahkan
ke kategori ini, tempat activity sejenis berkumpul:

| Dulu | Sekarang | Nama di toolbox |
|---|---|---|
| `OpenRPA.Activities.TypeInto` | `Custom.StudioBridge.TypeInto` | Type Into (UiPath style) |
| `OpenRPA.Activities.Custom.UiPathStyleClick` | `Custom.StudioBridge.UiPathStyleClick` | Click (UiPath style) |

> Nama tipenya berubah, jadi **workflow lama yang memakainya perlu di-drag
> ulang dari toolbox**. Tipe lamanya sudah dipastikan tidak ada lagi di
> `OpenRPA.exe`.

## Kapan memakai yang mana

| | Studio Click / Studio Set Text | Click / Type Into (UiPath style) |
|---|---|---|
| Bentuk selector | XML (`<webctrl/>`, `<wnd/>`) | JSON bawaan OpenRPA |
| Jalur web | extension Studio Bridge | native messaging (NM) OpenRPA |
| Indicate | satu tombol, web & desktop otomatis | dua tombol terpisah (desktop/web) |
| Anchor | belum ada | ada (Click) |
| Pilihan mouse/keyboard | terbatas | Virtual Click, Offset, Key Modifiers, Simulate Keystrokes |

Keduanya sengaja dipertahankan: yang satu unggul di kemudahan memilih elemen,
yang lain di kelengkapan kendali input.

## Yang berubah selain lokasinya

**Kartunya dibuat ulang** mengikuti pola kategori ini (header berwarna, judul
yang bisa diketik langsung di canvas, tautan Indicate/Highlight, dan
screenshot elemen yang menyembunyikan dirinya kalau belum pernah di-Indicate).
Dua hal dinaikkan dari Properties panel ke kartu karena paling sering diubah:
**Technology** pada Type Into (menentukan dialog Indicate mana yang dibuka —
harus terlihat SEBELUM menekan Indicate) dan **Click Type** pada Click.
Anchor pada Click punya barisnya sendiri, dan gambar anchornya hanya muncul
kalau anchor benar-benar dipakai.

**ContinueOnError ditambahkan** ke keduanya supaya seragam dengan activity
lain. Pada Click, isi Execute aslinya tidak disentuh sama sekali — hanya
dipindah ke `ExecuteCore` yang dipanggil di dalam try/catch.

**Atribut nama/keterangan yang tadinya memakai berkas terjemahan milik project
OpenRPA (`Resources.strings`) ditulis langsung** — berkas itu tidak bisa
diakses dari project Custom.*.

**`TypeText.GetKeys` / `TypeText.TypeString` TIDAK disalin.** Keduanya
dipindahkan ke `OpenRPA.Interfaces.KeyboardInput`, dan `OpenRPA.Activities.TypeText`
sekarang meneruskan ke sana. Jadi tetap SATU implementasi yang dipakai dua
project — bukan salinan yang cepat atau lambat menyimpang. Ini juga alasan
project ini kini mereferensi `OpenRPA.Interfaces` dan `OpenRPA.NM` secara
langsung (project gaya lama tidak mewarisi referensi transitif).

## Status pengujian

Keduanya **belum diuji jalan** — menjalankannya butuh aplikasi target
sungguhan plus dialog Indicate milik OpenRPA yang perlu diklik manusia. Yang
sudah dipastikan: compile bersih, kedua tipe terdeteksi sebagai activity di
kategori Custom.StudioBridge dengan filter yang sama persis dipakai
`wfToolbox.xaml.cs`, dan kedua designer bisa di-instansiasi (XAML-nya
ter-compile dan ter-load). Logika Execute-nya sendiri tidak diubah dari versi
yang selama ini Anda pakai.

---

# Pembaruan: selector tunggal, ikon, dan dua activity yang dihapus

Catatan di atas ditulis saat Batch 4. Yang berikut ini berlaku sekarang.

## Dua activity dihapus, pilihannya dipindahkan

| Dihapus | Pindah ke | Pilihan yang ikut pindah |
|---|---|---|
| `Type Into (UiPath style)` (`TypeInto.cs`) | `Type Into` (`StudioSetText`) | Click Before Typing, Simulate Keystrokes, Delay Between Keys (ms), Key Modifiers, Post Wait |
| `Click (UiPath style)` (`UiPathStyleClick.cs`) | `Click` (`StudioClick`) | Mouse Button, Offset X/Y, Key Modifiers, Focus, Virtual Click, Post Wait |

Keduanya memakai selector JSON bawaan OpenRPA. Sekarang tidak ada lagi dua cara
mengklik atau mengetik dengan dua bentuk selector yang berbeda: yang tersisa
hanya yang memakai selector XML, dan jenis targetnya dikenali dari bentuk
selector itu sendiri.

Offset memakai penanda `-1 = tidak diisi`, bukan 0, karena 0 adalah titik yang
sah (sudut kiri-atas elemen).

## Kartu activity dibuat dari satu kelas dasar

`IndicateDesignerBase.cs` menyediakan untuk semua kartu:

- baris `Indicate on screen | Edit selector`
- pengisian properti `Selector` dari hasil indicate
- penyimpanan `ScreenshotBase64` untuk pratinjau di kanvas
- penamaan otomatis lewat `SelectorLabel.Build`, yang tidak menimpa nama yang
  sudah diganti orang

Kelas ini juga dipakai di luar project ini: Custom.Browser, Custom.Data
(Extract Data Table), dan Custom.Window. Jadi satu perubahan tata letak berlaku
untuk semuanya sekaligus.

## Ikon toolbox

Sepuluh activity di project ini punya ikon sendiri di
`Custom.StudioBridge/Resources/`, dipasang lewat `[ToolboxBitmap]` dengan
penunjuk `ResFinder`. Delapan activity tab (`StudioOpenTab`, `StudioListTabs`,
dan seterusnya) juga akhirnya punya `[DisplayName]`, sehingga di toolbox
terbaca "Open Tab", "List Tabs", bukan nama kelasnya.

## Extension harus di-reload

`Extension/manifest.json` kini v0.9.0. Setelah menarik perubahan ini, buka
`chrome://extensions/` lalu tekan Reload pada extension Studio. Tanpa itu,
`Click` di halaman web akan menjawab "Unknown action" untuk kemampuan baru.
