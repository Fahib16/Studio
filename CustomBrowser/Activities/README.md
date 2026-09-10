# Custom.Browser — browser & elemen

Kategori toolbox: **Custom.Browser**. Warna header: biru `#FF1565C0`.

| Activity | Kartu canvas | Properties panel |
|---|---|---|
| **Open Browser** (lama) | Url *, drop-zone | — |
| **Attach Browser** (lama) | Indicate browser, drop-zone | — |
| **Navigate URL** | Url * | TabId, WaitForLoad, Timeout, output Title |
| **Go Back** / **Go Forward** / **Refresh Page** | TabId | WaitForLoad, Timeout, output Title |
| **Element Exists** | Indicate, screenshot, Save to | Selector, TabId, Timeout |
| **Wait Element Vanish** | Indicate, screenshot, Timeout | Selector, TabId, ThrowOnTimeout, output Vanished |
| **Get Attribute** | Indicate, screenshot, Attribute Name *, Save to | Selector, TabId, Timeout |
| **Select Item** | Indicate, screenshot, Item * | Selector, TabId, ByIndex, Timeout |
| **Check / Uncheck** | Indicate, screenshot | Selector, Action, TabId, Timeout, output Checked |
| **Take Screenshot** | Indicate, screenshot, Path, Save to | Selector (opsional), TabId, Timeout |

Semuanya punya `ContinueOnError` di kategori Common.

## Web dan desktop dari activity yang sama

Kelima activity elemen (Element Exists, Wait Element Vanish, Get Attribute,
Select Item, Check/Uncheck) — dan Take Screenshot — menentukan jenis target
dari **BENTUK selector** lewat `SelectorKindDetector`, bukan dari properti
terpisah. Pola yang sama persis dengan `StudioClick`/`StudioSetText`: dengan
begitu tidak mungkin selector desktop dijalankan lewat jalur browser hanya
karena suatu pilihan lupa diubah.

Jalur web memakai `StudioPipeClient`, jalur desktop memakai `DesktopActions`,
dan tombol Indicate memakai `IndicateHelper` — semuanya yang SUDAH ADA di
`Custom.StudioBridge`, bukan salinannya. Karena itu project ini sekarang
mereferensi Custom.StudioBridge (dan Newtonsoft.Json 13.0.4, versi yang sama).

Logika tombol "Indicate on screen / Edit selector" ada di satu tempat:
`ElementDesignerBase`, yang dipakai sebagai elemen akar XAML keenam kartu itu.
Kalau disalin ke tiap code-behind, cepat atau lambat salah satunya menyimpang
(mis. lupa aturan tidak menimpa nama yang sudah diubah user).

## Perubahan di extension (WAJIB reload)

Versi extension dinaikkan ke **0.8.0**. Aksi baru di `Extension/background.js`:

| Aksi | Bentuk | Untuk |
|---|---|---|
| `getAttribute` | `op` di `pageOpsFn` | Get Attribute |
| `selectItem` | `op` di `pageOpsFn` | Select Item |
| `setCheck` | `op` di `pageOpsFn` | Check / Uncheck |
| `rect` | `op` di `pageOpsFn` | pemotongan screenshot elemen |
| `extractTable` | `op` di `pageOpsFn` | Extract Data Table (Custom.Data) |
| `navigate`, `goBack`, `goForward`, `reload` | `case` di `handleAction` | activity navigasi |
| `screenshot` | `case` di `handleAction` | Take Screenshot |

Operasi elemen ditulis sebagai `op` DI DALAM `pageOpsFn`, bukan fungsi injeksi
terpisah — sesuai aturan yang sudah ada: fungsi yang di-inject tidak bisa
memanggil fungsi lain, jadi resolver selector harus disalin dan salinannya
pasti menyimpang. Navigasi dan screenshot BUKAN operasi halaman (yang
dikerjakan tab-nya, bukan elemen di dalamnya), jadi tetap di `handleAction`.

> **Extension harus di-reload di `chrome://extensions/`.** Sudah saya
> pastikan: extension yang sedang berjalan di Chrome Anda masih versi lama —
> pemanggilan `getAttribute` dijawab "Unknown action: getAttribute".

## Keputusan yang perlu diketahui

**Wait For Load ada di semua activity navigasi (default true).** Tanpa itu
activity berikutnya sering berjalan di atas halaman LAMA — kegagalan yang
paling sering terjadi pada otomasi web dan paling sulit dilacak karena hanya
kadang-kadang terjadi. Penungguannya memeriksa status tab secara berkala,
bukan memasang listener `onUpdated`, karena listener bisa TERLEWAT kalau
halaman sudah selesai sebelum listener terpasang.

**Element Exists tidak melempar kalau elemen tidak ada** — itu memang
jawabannya. Timeout di sana berarti "tunggu sampai segini kalau belum
muncul". Sebaliknya **Wait Element Vanish MELEMPAR saat timeout** (kecuali
Throw On Timeout dimatikan), karena melanjutkan saat halaman masih sibuk
hampir selalu berakhir salah.

**Get Attribute membaca PROPERTI DOM untuk nama-nama yang penting.**
`value` pada `<input>` yang sudah diketik user tidak pernah muncul di
`getAttribute("value")` — yang berubah adalah propertinya. Begitu juga
`innerText`, `outerHTML`, `checked`, `selected`, `disabled`, `href`, `src`.
Nama lain dibaca apa adanya. Untuk desktop, nama propertinya disamakan dengan
kosakata selector (name, automationid, classname, controltype, isenabled,
isoffscreen, checked, value).

**Select Item mencocokkan TEKS yang terlihat dulu, baru value** — yang dilihat
orang saat menyusun workflow adalah teksnya, sedangkan value sering berupa
kode yang tidak muncul di layar.

**Select Item desktop: dropdown DIBUKA lalu itemnya DIKLIK.** Ini bukan
pilihan gaya. Pada combo box WinForms, `SelectionItem.Select()` (yang dipakai
`ComboBox.Select` milik FlaUI) membuat UI Automation melaporkan item itu
"terpilih" PADAHAL isi kontrolnya belum berubah — saya buktikan lewat probe:
setelah `Select()`, `SelectedItem` berkata "Surabaya" tapi teks combo box
masih kosong; setelah Expand + Click barulah benar-benar terisi. Karena itu
hasilnya juga selalu DIPERIKSA lewat `Value` (teks yang benar-benar tampil),
bukan lewat `SelectedItem`.

**Check/Uncheck web: elemen DIKLIK dulu, bukan langsung diisi properti.**
Banyak halaman baru bereaksi pada event klik; mengisi `checked` saja membuat
centang berubah di layar tapi tidak di data halaman. Radio button tidak bisa
di-Uncheck (batasan HTML) dan itu dilaporkan sebagai error, bukan didiamkan.

**Take Screenshot tanpa Selector = bagian halaman yang TERLIHAT,** bukan
seluruh halaman termasuk yang harus digulir. Chrome hanya menyediakan potret
area terlihat; menjahit beberapa potret sambil menggulir sering salah pada
halaman dengan elemen melayang. Selector desktop dipotret dari layar seukuran
elemennya. Hasilnya PNG; `Path` opsional dan `Image Base64` tetap terisi.

## Status pengujian

**Jalur desktop — diuji dan lulus semua (14 skenario).** Harness membuat
jendela WinForms sendiri dengan judul unik (supaya tidak mungkin mengenai
jendela milik user) berisi kotak teks, checkbox, dan combo box, lalu
menjalankan activity-nya lewat `WorkflowInvoker`: Element Exists ada/tidak
ada, Get Attribute (`value`, `automationid`, `checked`, plus penolakan nama
properti yang tidak dikenal), Check dan Toggle yang benar-benar mengubah
keadaan, Select Item yang benar-benar mengubah isi combo box (dan menolak
pilihan yang tidak ada), Wait Element Vanish sebelum dan sesudah kontrol
disembunyikan, Take Screenshot elemen yang menghasilkan PNG, serta
ContinueOnError.

**Kode extension — diuji terhadap DOM sungguhan (bukan sekadar dibaca).**
`background.js` lolos pemeriksaan sintaks Node, lalu `pageOpsFn` disuntikkan
ke halaman HTML uji di server lokal dan dipanggil langsung:

- `getAttribute`: `value` mengembalikan teks yang DIKETIK (`diketik-user`),
  bukan atribut HTML-nya (`nilai-atribut`) — inti keputusan di atas terbukti;
  `href` dikembalikan sebagai URL lengkap; atribut yang tidak ada memberi
  `exists:false`.
- `selectItem`: cocok lewat teks, lewat value, dan lewat indeks; pilihan yang
  tidak ada dan elemen yang bukan `<select>` ditolak dengan pesan yang jelas;
  event `change` benar-benar terpicu.
- `setCheck`: check → tercentang, check lagi → tidak berubah (tidak
  ter-toggle dua kali), toggle → berbalik, elemen yang tidak bisa dicentang
  ditolak; event `change` hanya terpicu saat keadaannya berubah.
- `extractTable`: entitas HTML diterjemahkan (`Budi & Ani`), `colspan`
  disalin ke dua kolom, dan selector yang menunjuk `<th>` tetap menemukan
  tabelnya (pencarian ke atas).
- `rect`: mengembalikan posisi/ukuran beserta devicePixelRatio.

**BELUM diuji:** jalur WEB dari sisi activity — yaitu rangkaian penuh
activity → named pipe → native host → extension 0.8.0 → halaman. Itu baru
bisa dijalankan setelah extension di-reload di `chrome://extensions/`.
Bagian JavaScript-nya sendiri sudah diuji seperti di atas; yang belum
terbukti adalah sambungannya (nama aksi, bentuk balasan) untuk aksi-aksi
baru.

---

## Tambahan: tombol pemilih berkas

**Take Screenshot** kini punya tombol 📁 di samping Path (mode "simpan
sebagai", filter PNG). Kartu ini menjadi contoh bahwa satu kartu bisa punya
Indicate DAN tombol berkas sekaligus: `ElementDesignerBase` adalah turunan
dari `Custom.Shared.PathDesignerBase`.

---

## Ikon toolbox

12 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.

## Selector dan nama tampilan

Enam activity yang memerlukan selector di project ini memakai selector XML
milik Custom.StudioBridge lewat `IndicateDesignerBase`, bukan selector JSON
bawaan OpenRPA. `Open Browser` dan `Attach Browser` juga akhirnya punya
`[DisplayName]` dan `[Description]`.
