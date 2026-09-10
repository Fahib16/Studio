# Custom Activity: Terminal (TN3270 Direct Connection) ala UiPath untuk OpenRPA

Batch activity mainframe/AS400, arsitektur BEDA TOTAL dari Click/TypeInto
(bukan UI Automation — protokol TN3270 lewat library **Open3270**).

## Prasyarat WAJIB sebelum compile

1. **Install Open3270 lewat NuGet** di project `OpenRPA` (yang berisi custom
   activity kita): buka Package Manager Console atau NuGet Package Manager GUI,
   jalankan:
   ```
   Install-Package Open3270
   ```
   Tanpa ini, `using Open3270;` di semua file di batch ini akan gagal compile
   (CS0246) — tapi ini beneran package NuGet publik, bukan project reference
   internal seperti kasus `OpenRPA.NM` sebelumnya.

## Isi

| File | Fungsi |
|---|---|
| `TerminalSession.cs` + Designer | Container: connect ke host, sediakan `TNEmulator` untuk activity anak, disconnect di akhir scope |
| `SendTerminalKey.cs` + Designer | Kirim AID key (Enter, Clear, PF1-24, PA1-3, dst) |
| `TypeIntoTerminal.cs` + Designer | Ketik teks di posisi kursor saat ini |
| `TerminalReadActivities.cs` (`WaitForTerminalText` + `GetTerminalText`) + Designer | Tunggu teks muncul di posisi tertentu / baca seluruh layar |

## Sumber API — dikonfirmasi, bukan tebakan

Semua signature (`Connect`, `Config.TermType`, `SendKeyFromText`, `SendText`,
`WaitForText`, `CurrentScreenXML.Dump()`, `Close`) diambil langsung dari
`TheDemo.cs`, contoh resmi di repo Open3270/Open3270 di GitHub.

## Yang PERLU disadari / belum lengkap

- **Hanya TN3270** (mainframe/Z-series). Open3270 **tidak** dukung TN5250
  (AS/400/iSeries) — kalau kamu butuh itu juga, perlu library terpisah,
  belum saya cari.
- **`TypeIntoTerminal` Row/Column BELUM presisi** — saya belum konfirmasi
  apakah `TNEmulator` punya method set-cursor-ke-posisi-tertentu. Untuk
  sekarang, isi Row/Column cuma trigger kirim key `Home` (bukan pindah ke
  posisi spesifik). Kalau kamu butuh ini akurat, kasih tahu — saya perlu
  telusuri lebih lanjut struktur `TnXMLScreen`/`Field` Open3270 dulu.
- **`GetTerminalText` cuma dump seluruh layar** (`CurrentScreenXML.Dump()`),
  belum ekstraksi per-region (row/col/length) seperti "Get Text" UiPath yang
  bisa ambil satu field spesifik saja.
- **Cleanup koneksi**: sudah dibenerin pakai `Variable<TNEmulator>` supaya
  `Close()` selalu terpanggil di akhir scope (sukses maupun Body gagal
  di tengah) — bukan cuma local variable yang hilang di callback.
- **Belum ada Get/Set Field** (versi UiPath yang berbasis field attribute,
  bukan koordinat mentah) — kalau dibutuhkan, ini batch terpisah karena perlu
  pelajari struktur `Field[]`/`XMLScreenField` Open3270 lebih dalam.

## Cara pakai

1. Install NuGet Open3270 (lihat Prasyarat di atas).
2. Taruh 15 file (.cs + designer-nya) di `Activities/Custom/Terminal/` di
   project lokal kamu — klik kanan folder `Custom` di Solution Explorer →
   Add → New Folder → beri nama "Terminal", lalu Add → Existing Item untuk
   semua file ini.
3. Rebuild Solution.
4. Di canvas: drag `Terminal Session`, isi Host/Port, drop `Send Terminal Key`/
   `Type Into Terminal`/dst di dalamnya — semuanya otomatis pakai `session`
   yang sama (isi expression `Session` = `session`, mengikuti nama delegate
   argument `ActivityAction<TNEmulator>` di `TerminalSession`).

---

# Pelengkapan Custom.Terminal (batch ini)

Tiga kekurangan yang tercatat sebelumnya sudah ditutup, dan satu bug
koordinat ditemukan sekaligus diperbaiki.

## 1. Type Into Terminal sekarang PRESISI Row/Column

Sebelumnya, mengisi Row/Column hanya memicu tombol Home dengan catatan
"Open3270 tidak punya SetCursor yang terkonfirmasi". Ternyata ada:
`TNEmulator.SetCursor(int x, int y)`.

## 2. Get Terminal Text sekarang bisa per-region

Sebelumnya hanya bisa dump seluruh layar. Sekarang:

| Isian | Hasil |
|---|---|
| Row = 0 | seluruh layar (perilaku lama, tetap default) |
| Row diisi, Length 0 | seluruh baris itu |
| Row + Column, Length 0 | dari kolom itu sampai akhir baris |
| Row + Column + Length | potongan sepanjang Length |

Ada opsi `Trim` (default true): layar 3270 selalu dipenuhi spasi sampai batas
kolom, dan spasi itu hampir tidak pernah diinginkan.

## 3. Get / Set Terminal Field — BARU

Field adalah bagian layar yang dikelola host (label dan kotak isian).
Membaca/menulis lewat field lebih tahan banting daripada lewat koordinat
mentah: kalau tata letak bergeser satu baris, koordinat langsung salah,
sedangkan urutan field biasanya tetap.

Tiga cara menunjuk field, dengan urutan prioritas yang sama untuk keduanya:

1. **Index** — nomor urut field, mulai 0
2. **Label** — cari field yang teksnya memuat label ini, ambil/isi field
   BERIKUTNYA (pola `Nama:` lalu kotak isiannya)
3. **Row/Column** — field yang memuat posisi itu

Set Terminal Field memakai `TNEmulator.SetField(index, text)` — cara host
sendiri mengisi field, jadi tidak bergantung pada posisi kursor saat itu.
Itulah bedanya dengan Type Into Terminal, yang menuntut kursor sudah berada
di tempat yang benar.

## 4. PERBAIKAN BUG: koordinat Wait For Terminal Text terbalik

`WaitForTerminalText` memanggil `session.WaitForText(row, col, text, ms)`,
padahal signature Open3270 adalah `WaitForText(int x, int y, ...)` dengan
**x = KOLOM dan y = BARIS**. Akibatnya, menunggu teks di baris 5 kolom 20
sebenarnya memeriksa baris 20 kolom 5.

Ini bukan tebakan — dipastikan dari source Open3270 yang ada di repo ini:

- `Open3270/src/Open3270Library/Engine/TnXMLScreen.cs`:
  `GetText(x, y, len)` menghitung offset sebagai `x + y * lebarLayar`
- `Open3270/src/Open3270Library/TN3270E/X3270/Controller.cs`:
  `MoveCursor` menghitung alamat sebagai `(y * columnCount) + x`

Seluruh aturan koordinat sekarang ada di satu tempat
(`TerminalCoordinates.cs`), termasuk keputusan bahwa **Row/Column di sisi
activity berbasis 1** (itu yang tertera di layar 3270 dan yang orang baca),
sementara Open3270 berbasis 0, dan **0 berarti "tidak diisi"**.

> Perilaku Wait For Terminal Text BERUBAH dengan perbaikan ini. Workflow lama
> yang sudah "menyesuaikan diri" dengan urutan terbalik (mis. sengaja mengisi
> Row dengan nomor kolom) perlu ditukar kembali.

## Status pengujian

Tidak ada host TN3270 di sini, jadi yang tidak bisa saya uji adalah
sambungan sesungguhnya ke mainframe. Yang BISA diuji sudah diuji dan lulus
semua (18 skenario, harness terpisah): aturan konversi koordinat 1-based ke
0-based, `FieldIndexAt`, `FieldIndexAfterLabel` (termasuk tidak peduli
besar-kecil huruf), serta urutan prioritas `TerminalFieldLocator` beserta
ketiga pesan errornya. Layar tiruannya dibangun dari `XMLScreenField` milik
Open3270 sendiri, bukan tipe palsu, supaya yang diuji benar-benar tipe yang
dipakai saat runtime.

**BELUM diuji:** `SetCursor`, `SetField`, `GetText` per-region, dan
`WaitForText` terhadap host sungguhan — semuanya memakai API Open3270 yang
signature-nya sudah saya pastikan dari source, tapi perilaku host nyata
(kapan layar siap, field mana yang bisa ditulis) baru terbukti saat Anda
menjalankannya ke mainframe.

---

## Ikon toolbox

7 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.

## Nama tampilan

Lima activity yang sebelumnya tampil dengan nama kelas mentah kini punya
`[DisplayName]` dan `[Description]`: Send Terminal Key, Wait For Terminal Text,
Get Terminal Text, Terminal Session, dan Type Into Terminal. Konstruktornya
juga menyetel `DisplayName`, jadi namanya sama di toolbox maupun di kanvas.
