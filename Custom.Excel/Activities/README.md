# Custom.Excel — Excel berbasis berkas (ClosedXML)

Kategori toolbox: **Custom.Excel**. Warna header: hijau `#FF2E7D32`.

| Activity | Kartu canvas | Properties panel |
|---|---|---|
| **Excel Application Scope** | Workbook Path *, drop-zone "Do" | AutoSave, CreateIfMissing |
| **Read Range** | Sheet, Range, Save to | Workbook Path, AddHeaders |
| **Write Range** | Sheet, Start Cell, Data Table * | Workbook Path, AddHeaders |
| **Append Range** | Sheet, Data Table * | Workbook Path |
| **Read Cell** | Sheet, Cell *, Save to | Workbook Path |
| **Write Cell** | Sheet, Cell *, Value | Workbook Path |
| **Save Workbook** | Save As Path | — |

Semuanya punya `ContinueOnError` di kategori Common.

## Hubungannya dengan OpenRPA.Office (penting)

`OpenRPA.Office` sudah punya set Excel lengkap berbasis **COM Interop**:
mengendalikan aplikasi Excel yang berjalan, bisa memakai berkas yang sedang
terbuka, menjalankan makro, mengatur format.

Project ini **bukan penggantinya**, melainkan pelengkap yang berbeda sifat:

|  | Custom.Excel (ClosedXML) | OpenRPA.Office (Interop) |
|---|---|---|
| Excel harus terpasang | tidak | ya |
| Berkas sedang dibuka di Excel | tidak boleh | justru itu gunanya |
| Format .xls lama | tidak didukung | didukung |
| Makro, format rumit | tidak | ya |
| Jalan di robot tanpa profil Office | ya | tidak |

**Karena itu properti `Visible` TIDAK ditiru.** ClosedXML tidak pernah membuka
aplikasi Excel, jadi tidak ada apa pun yang bisa ditampilkan; propertinya
hanya akan jadi hiasan yang menyesatkan. Kalau yang dibutuhkan adalah melihat
Excel bekerja di layar, itu wilayah OpenRPA.Office.

## Dua cara memakai

1. **Di dalam Excel Application Scope** — kosongkan Workbook Path pada
   activity di dalamnya. Berkas dibuka sekali, semua operasi berbagi workbook
   yang sama, lalu disimpan sekali saat scope selesai.
2. **Berdiri sendiri** — isi Workbook Path pada activity-nya. Berkas dibuka,
   dikerjakan, disimpan, ditutup.

Keduanya ada karena memaksa scope untuk satu operasi tunggal membuat workflow
sederhana jadi bertele-tele, sedangkan tanpa scope, sepuluh operasi berturut
berarti sepuluh kali buka-tutup berkas. Activity yang tidak punya keduanya
memberi pesan yang menyebut kedua pilihan itu, bukan NullReferenceException.

Workbook dibagikan lewat **execution property** WF (mekanisme bawaan untuk
"berlaku selama scope berjalan", seperti TransactionScope), bukan variabel
global — jadi dua scope bersarang tidak akan saling menimpa.

## Keputusan yang perlu diketahui

**Scope TIDAK menyimpan workbook kalau isinya gagal di tengah jalan.**
Menyimpan berkas yang prosesnya berhenti separuh berarti menuliskan keadaan
setengah jadi ke berkas yang mungkin sudah berisi data penting. Ini sudah
diuji: scope yang melempar exception di tengah meninggalkan berkas lamanya
utuh.

**Read Range dan Read Cell mengembalikan TEKS seperti yang terlihat di
Excel** (mengikuti format sel), semua kolom bertipe String. Alasannya sama
dengan Read CSV di Custom.Data: menebak tipe merusak data tanpa terlihat, dan
angka seri tanggal (45900) bukan yang diharapkan orang yang melihat
"03/09/2026" di layar.

**Write Range dan Write Cell menulis dengan TIPE ASLINYA** (angka tetap
angka, tanggal tetap tanggal) supaya hasilnya bisa dijumlahkan dan diurutkan
di Excel. Ini sudah diuji: sel angka benar-benar bertipe Number, bukan teks.

**Write Range tidak membersihkan sisa sheet.** Yang ditimpa hanya seluas isi
DataTable — sama seperti Write Range UiPath. Membersihkan seluruh sheet
diam-diam justru berbahaya.

**Append Range menulis judul HANYA kalau sheet masih kosong.** Kalau tidak,
judul akan terselip di tengah data.

**Sheet yang tidak ada:** saat MEMBACA ditolak (salah ketik nama sheet
seharusnya terlihat, bukan menghasilkan tabel kosong), saat MENULIS dibuat.

**ClosedXML tidak disalin ke folder output** (`Private=False`): satu-satunya
salinan tetap milik OpenRPA (0.105.1, lewat PackageReference), jadi tidak
mungkin ada dua versi di folder yang sama.

## Yang sengaja TIDAK dibuat

**Close Workbook.** Dengan ClosedXML tidak ada aplikasi maupun berkas yang
"terbuka" di luar scope — scope sudah menutup workbook-nya sendiri saat
selesai, dan activity berdiri sendiri menutupnya setiap kali selesai. Sebuah
Close Workbook tersendiri tidak punya makna yang jujur di sini. Yang ada
adalah **Save Workbook**, yang benar-benar berguna: menyimpan hasil sebagian
di tengah proses panjang, atau menyimpan salinan ke berkas lain (Save As
Path) tanpa menutup apa pun.

**Word: Replace Text dan Word: Export To PDF.** `OpenRPA.Office` sudah punya
aktivitas Word lengkap termasuk ExportDocument ke PDF (lewat Interop).
Membuat versi berbasis berkas berarti menambah dependensi baru untuk membuat
PDF — dan itu perlu Anda putuskan dulu, bukan saya tambahkan diam-diam.

## Status pengujian

Diuji dengan `WorkflowInvoker` pada berkas .xlsx sungguhan di folder temp
(harness terpisah, 14 skenario) dan semuanya lulus: Write Range membuat
berkas baru dan menyimpan angka sebagai angka; Read Range dengan/tanpa judul
dan dengan range terbatas; sheet tak dikenal ditolak saat membaca; Append
Range menambah tanpa mengulang judul; Write Cell/Read Cell pulang-pergi;
Excel Application Scope membagi satu workbook ke dua activity di dalamnya;
pesan yang jelas saat tanpa path dan tanpa scope; Save Workbook di luar scope
ditolak; dan scope yang gagal di tengah tidak menimpa berkas lama.

Catatan: harness perlu `app.config` dengan binding redirect yang sama seperti
`OpenRPA.exe.config` (ClosedXML 0.105 memuat SixLabors.Fonts yang menuntut
`System.Numerics.Vectors` 4.1.4). Di dalam Studio hal ini sudah beres karena
`OpenRPA.exe.config` memang punya redirect itu — tapi kalau suatu saat
activity ini dipakai dari host lain (mis. robot tanpa config), redirect
tersebut harus ikut dibawa.

---

## Tambahan: tombol pemilih berkas + Read Range Workbook

**Semua kartu sekarang punya Workbook Path DI KARTU, lengkap dengan tombol
pemilih berkas** (📁) — sebelumnya Workbook Path hanya ada di Properties
panel. Alasannya: itu isian pertama yang orang cari, dan mengetik path
panjang dengan tangan adalah sumber salah ketik yang paling sering.
Mekanismenya ada di `Custom.Shared.PathDesignerBase` (lihat README project
itu); kosongkan saja kalau activity-nya dipakai di dalam Excel Application
Scope.

Mode tombolnya sengaja berbeda per activity: yang MEMBACA (Read Range, Read
Cell, Read Range Workbook) memakai dialog "buka berkas" yang menuntut
berkasnya sudah ada, yang MENULIS (Write Range, Write Cell, Append Range,
Save Workbook, dan scope-nya) memakai dialog "simpan sebagai" supaya nama
berkas baru boleh diketik.

**Read Range Workbook** dipindahkan ke sini dari project OpenRPA
(`OpenRPA.Activities.ReadRangeWorkbook`). Isi Execute-nya dipertahankan apa
adanya — termasuk penolakan berkas ber-password dan penamaan kolom kembar —
sedangkan kartunya dibuat ulang mengikuti pola kategori ini dan diberi
tombol pemilih berkas. Tipe lamanya sudah tidak ada lagi di `OpenRPA.exe`
(sudah diperiksa), jadi **workflow lama yang memakainya perlu di-drag ulang
dari toolbox**.

Bedanya dengan Read Range ada di bagian atas README ini; ringkasnya: Read
Range bisa ikut Excel Application Scope, Read Range Workbook selalu membuka
berkasnya sendiri.

---

## Ikon toolbox

8 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.
