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
