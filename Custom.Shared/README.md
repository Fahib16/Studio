# Custom.Shared — bagian yang dipakai bersama kartu activity

Project ini **tidak berisi activity sama sekali**, jadi tidak menambah
kategori di toolbox: `wfToolbox.xaml.cs` hanya menambahkan kategori kalau
assembly-nya punya minimal satu activity (`if (wfToolboxCategory.Tools.Count > 0)`).
Isinya hanya dua kelas yang dipakai bersama oleh project Custom.* lain.

## Kenapa ada

Tombol pemilih berkas/folder dibutuhkan di enam project sekaligus
(Files, Excel, Data, Mail, Browser, dan Excel Application Scope). Kalau kode
dialognya disalin ke tiap project, cepat atau lambat salah satunya menyimpang
— mis. satu menulis path sebagai `Literal` (yang tidak kelihatan di kotak
ekspresi) sementara yang lain menulis string VB. Satu tempat, satu perilaku.

Alternatif "taruh saja di OpenRPA.Interfaces" sengaja tidak dipakai: ini
murni kebutuhan kartu activity buatan sendiri, bukan bagian dari inti OpenRPA.

## Isi

### `PathPicker`

Dialog dan penulisan nilai:

| Method | Untuk |
|---|---|
| `OpenFile` | pilih satu berkas yang sudah ada |
| `OpenFiles` | pilih beberapa berkas |
| `SaveFile` | tentukan berkas tujuan (boleh yang belum ada) |
| `Folder` | pilih folder |
| `ToVbLiteral` / `ToVbArrayLiteral` | bungkus hasilnya jadi ekspresi VB |
| `FromVbLiteral` | kebalikannya, dipakai untuk membuka dialog di folder yang sedang dipakai |

Catatan yang penting:

- **Hasilnya ditulis sebagai ekspresi VB berkutip** (`"C:\data\a.xlsx"`),
  bukan `Literal`. `Literal` tersimpan benar tapi tidak bisa ditampilkan
  `ExpressionTextBox`, jadi kotaknya akan terlihat KOSONG padahal sudah
  berisi path — persis kebingungan yang tombol ini seharusnya hilangkan.
- **Backslash tidak di-escape**: string VB memang tidak mengenal escape
  backslash, dan itu yang membuat path Windows enak dibaca. Kutip di dalam
  nama berkas digandakan sesuai aturan VB.
- Pemilih folder memakai `FolderBrowserDialog` milik WinForms karena
  .NET Framework 4.6.2 tidak punya pemilih folder di `Microsoft.Win32`, dan
  menulis pembungkus `IFileDialog` sendiri lewat COM jauh lebih banyak kode
  daripada nilai yang didapat.
- Dialog dibuka di folder yang sedang tertulis di kotak (kalau isinya memang
  path); ekspresi yang nilainya baru ada saat runtime diabaikan, bukan bikin
  gagal.

### `PathDesignerBase`

Kelas dasar kartu yang menyediakan handler `Browse_Click`. Tombolnya cukup
menyebut apa yang dia mau lewat `Tag`:

```xml
<Button Click="Browse_Click"
        Tag="WorkbookPath|file|Berkas Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Semua berkas (*.*)|*.*">
    <TextBlock Text="📁" />
</Button>
```

Bentuk Tag: `NamaProperty|mode|rantai filter`, dengan mode
`file` / `files` / `save` / `folder`. Filter boleh dikosongkan.

Kartu yang juga punya tombol Indicate (Custom.Browser) memakai
`ElementDesignerBase`, yang **turunan dari `PathDesignerBase`** — jadi satu
kartu bisa punya Indicate dan tombol berkas sekaligus (Take Screenshot).

## Status pengujian

Diuji lewat harness terpisah (19 skenario, semuanya lulus):

- Penulisan dan pembacaan ekspresi VB: pembungkusan kutip, backslash yang
  TIDAK di-escape, kutip ganda di dalam nama berkas, pulang-pergi
  `ToVbLiteral` → `FromVbLiteral`, penulisan array, dan penolakan ekspresi
  yang bukan string berkutip.
- Keberadaan dan bentuk Tag setiap tombol pada 11 kartu yang seharusnya
  punya tombol (Copy File, Create Folder, Zip Files, Unzip, Read Range,
  Save Workbook, Read CSV, Write CSV, Save Attachments, Send Mail, Take
  Screenshot) — diperiksa dari XAML yang sudah ter-compile, bukan dari
  kode sumbernya.

**BELUM diuji:** klik tombolnya sampai dialog terbuka dan nilainya masuk ke
kotak — itu perlu tangan manusia, karena dialognya modal.
