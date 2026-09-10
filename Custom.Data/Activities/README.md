# Custom.Data — DataTable

Kategori toolbox: **Custom.Data**. Warna header: biru `#FF0277BD`.

| Activity | Kartu canvas | Properties panel |
|---|---|---|
| **Build Data Table** | Columns + link "Edit columns", Save to | Table Name |
| **Filter Data Table** | Data Table *, Filter Expression, Save to | Select Columns, Sort |
| **Sort Data Table** | Data Table *, Column *, Save to | Order |
| **Remove Duplicate Rows** | Data Table *, Save to | Columns |
| **Merge Data Table** | Source *, Destination * | MissingSchemaAction |
| **Join Data Table** | Left *, Right *, Left Column *, Right Column *, Save to | JoinType |
| **Add Data Row** | Data Table *, Array Row | Data Row |
| **Get Row Item** | Row *, Column Name, Save to | Column Index, output Text |
| **Output Data Table** | Data Table *, Save to | Format, Max Rows |
| **Read CSV** | Path *, Save to | Delimiter, Has Headers, Encoding |
| **Write CSV** | Data Table *, Path * | Delimiter, Has Headers, Append, Encoding |
| **Extract Data Table** | Indicate on screen, screenshot, Save to | Selector, Html, TableIndex, AddHeaders, TabId, Timeout |

Semuanya punya `ContinueOnError` di kategori Common.

## Yang sengaja TIDAK dibuat

**For Each Row.** Sudah ada di OpenRPA (`ForEachDataRow`), bahkan lebih
lengkap dari yang diminta: menerima DataTable maupun DataView dan mendukung
Break. Membuat yang kedua hanya akan memecah tempat orang mencarinya.

## Keputusan yang perlu diketahui

**Semua activity mengembalikan TABEL BARU dan tidak mengubah sumbernya,**
kecuali dua yang memang seharusnya mengubah: **Merge Data Table** (menambah
ke Destination) dan **Add Data Row** (menambah ke tabel yang diberikan) —
keduanya mengikuti perilaku .NET dan UiPath, dan disebut jelas di Description
propertinya.

**Filter memakai sintaks `DataView.RowFilter` bawaan .NET**
(`Umur > 30 AND Kota = 'Jakarta'`), bukan parser ekspresi buatan sendiri.
Sintaks itu sudah ada, terdokumentasi Microsoft, dan tidak akan menyimpang
dari yang diharapkan orang.

**Join membandingkan kunci sebagai string InvariantCulture,** bukan sebagai
tipe aslinya, supaya kolom Int32 di satu tabel tetap bisa dipasangkan dengan
kolom String di tabel lain — keadaan yang sangat umum kalau salah satu tabel
datang dari CSV atau dari halaman web. Nama kolom yang bertabrakan diberi
akhiran `_1`, bukan ditimpa, karena menimpa berarti kehilangan data tanpa
peringatan. **rowspan tidak ditangani** (lihat Extract Data Table).

**Read CSV membuat semua kolom bertipe String.** Menebak tipe merusak data
tanpa terlihat: kode pos `0812` menjadi `812`, dan tanggal `03/04` bisa
tertukar bulan-tanggalnya. Konversi lebih baik dilakukan sadar-sadar di
ekspresi.

**Parser CSV ditulis sendiri, bukan String.Split dan bukan pustaka baru.**
Split merusak data pada kasus paling umum sekalipun (alamat yang mengandung
koma); sementara aturan CSV yang sebenarnya cukup sedikit — kutip ganda,
pemisah di dalam nilai, baris baru di dalam nilai — untuk ditulis dan diuji
langsung. Ketiganya ada di uji.

**Build Data Table menyimpan definisi kolom sebagai satu string**
(`Nama:String; Umur:Int32`), bukan struktur tersendiri, supaya ikut tersimpan
apa adanya di berkas .xaml dan terbaca di diff git. Dialog "Edit columns"
hanya alat bantu yang menulis kembali ke string yang sama, dan memvalidasi
dengan fungsi yang SAMA PERSIS dengan yang dipakai saat runtime.

**`CsvEncoding` sengaja berdiri sendiri, tidak memakai `TextFileEncoding`
milik Custom.Files,** supaya kategori ini tidak bergantung pada kategori lain
hanya untuk satu dropdown. Nilainya tidak pernah berpindah antar activity.

## Extract Data Table

Punya DUA sumber:

1. **Selector** — tabel di halaman yang sedang terbuka, lewat Studio Bridge.
   Ini menambahkan aksi baru `extractTable` di `Extension/background.js`,
   ditulis sebagai `op` di dalam `pageOpsFn` (bukan fungsi injeksi terpisah,
   supaya resolver selector tidak perlu disalin). Versi extension sekarang
   **0.8.0** — extension harus di-reload di `chrome://extensions/` supaya
   aksi ini ada.
2. **Html** — potongan HTML yang sudah di tangan (mis. hasil Get Attribute
   `outerHTML`, atau berkas HTML yang dibaca Read Text File). Tidak
   memerlukan browser sama sekali.

Kalau keduanya diisi, Selector yang dipakai: sumber langsung dari halaman
selalu lebih baru daripada HTML yang sudah tersimpan.

Penguraian HTML memakai regex, bukan parser HTML lengkap — keputusan yang
sama dengan yang sudah diambil di `background.js` untuk selector. Yang
dibutuhkan hanya baris dan sel. Konsekuensinya sel yang memuat `<table>`
bersarang tidak diurai benar; untuk kasus itu pakai jalur Selector.
`colspan` disalin ke beberapa kolom, `rowspan` TIDAK ditangani (di kedua
jalur) — menanganinya butuh menyimpan status antar baris dan hasilnya tetap
menebak untuk tabel rumit.

Project ini mereferensi `Custom.StudioBridge` HANYA untuk activity ini
(memakai `StudioPipeClient` dan `IndicateHelper` yang sudah ada, daripada
menyalin protokol named pipe dan alur Indicate ke sini). Konsekuensinya
`Newtonsoft.Json` dipasang di project ini dengan versi yang sama persis
(13.0.4).

## Status pengujian

Diuji dengan `WorkflowInvoker` (harness terpisah, 26 skenario) dan semuanya
lulus — termasuk: filter + Select Columns tanpa mengubah sumber, sort menurun
berdasarkan ANGKA (bukan teks), dedup penuh dan per kolom, merge yang
menambah kolom baru, join Inner/Left/Full dengan penamaan kolom kembar, Get
Row Item lewat nama dan indeks, Output Data Table dua format dengan Max Rows,
CSV pulang-pergi berisi koma/kutip/baris baru di dalam sel, CSV bertitik
koma, dan Extract Data Table jalur Html (entitas HTML, tag di dalam sel,
Table Index, tanpa judul).

Ke-12 activity terdeteksi dengan filter yang sama persis dipakai
`wfToolbox.xaml.cs`, dan ke-12 designer bisa di-instansiasi.

Dialog "Edit columns" sudah dipastikan bisa dibuat dan MEMBACA definisi yang
ada dengan benar (`Nama:String; Umur:Int32` masuk sebagai dua baris grid
dengan tipe yang tepat); yang belum diuji adalah interaksi manusianya
(mengetik, menekan OK) karena itu perlu dibuka lewat Studio.

**BELUM diuji:** jalur Selector pada Extract Data Table — butuh Chrome dengan
extension 0.8.0 yang sudah di-reload di `chrome://extensions/` dan native
host berjalan. Kode aksi `extractTable` di background.js juga belum bisa
saya jalankan sama sekali (tidak ada Node/Chrome untuk mengujinya di sini),
jadi anggap bagian itu sebagai kode yang baru dibaca ulang, bukan yang sudah
terbukti jalan.

---

## Tambahan: tombol pemilih berkas

Read CSV dan Write CSV kini punya tombol 📁 di samping Path — Read CSV
dengan dialog "buka berkas" (harus sudah ada), Write CSV dengan "simpan
sebagai" (nama baru boleh diketik). Lihat `Custom.Shared/README.md`.

---

## Ikon toolbox

12 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.
