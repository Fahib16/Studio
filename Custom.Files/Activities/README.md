# Custom.Files — berkas & folder

Kategori toolbox: **Custom.Files**. Warna header: cokelat `#FF6D4C41`.
Tidak menambah dependensi apa pun — semuanya memakai `System.IO` dan
`System.IO.Compression` bawaan .NET Framework.

| Activity | Kartu canvas | Properties panel |
|---|---|---|
| **Copy File** | Path *, Destination * | Overwrite |
| **Move File** | Path *, Destination * | Overwrite |
| **Delete File** | Path * | ContinueIfMissing (default True) |
| **Create Folder** | Path * | — |
| **Delete Folder** | Path * | Recursive (default True) |
| **File Exists** | Path *, Save to | — |
| **Folder Exists** | Path *, Save to | — |
| **Folder Info** | Path * | Recursive + 6 output |
| **Read Text File** | Path *, Save to | Encoding |
| **Write Text File** | Path *, Content * | Encoding |
| **Append Line** | Path *, Content * | Encoding |
| **List Files In Folder** | Path *, Save to | Pattern (`*.*`), Recursive |
| **Wait For File** | Path *, Timeout | WaitUntilStable, ThrowOnTimeout, output Found |
| **Zip Files** | Files *, Zip Path * | Overwrite |
| **Unzip** | Zip Path *, Destination Folder * | Overwrite, output Files |

Semuanya punya `ContinueOnError` di kategori Common.

## Aturan yang perlu diketahui

**Destination pada Copy/Move File dianggap FOLDER hanya kalau** foldernya
memang sudah ada, atau ditulis dengan pemisah path di akhir (`C:\out\`).
Selain itu dianggap nama berkas lengkap. Folder induknya dibuat otomatis —
kalau tidak, activity gagal karena alasan yang hampir selalu bukan yang
dimaksud user.

**Delete File / Delete Folder tidak menganggap "sudah tidak ada" sebagai
kegagalan** (Delete File lewat ContinueIfMissing yang default True). Maksud
orang menghapus hampir selalu "pastikan ini tidak ada".

**Folder Info tidak melempar error untuk folder yang tidak ada** — `Exists`
diisi False dan angka lainnya nol. Activity ini memang untuk memeriksa
keadaan, jadi "tidak ada" adalah salah satu jawabannya.

**Wait For File menunggu berkas SELESAI ditulis, bukan sekadar ada**
(`WaitUntilStable`, default True): ukurannya harus tidak berubah lagi selama
dua pemeriksaan berturut-turut DAN berkasnya sudah bisa dibuka untuk dibaca.
Tanpa itu activity berikutnya sering membaca berkas unduhan yang baru terisi
separuh. Timeout defaultnya 30 detik — beda dari 10 detik yang dipakai
activity lain di solution ini, karena yang ditunggu unduhan, bukan elemen UI.
Penungguannya MEMBLOKIR thread workflow (sama seperti `WaitForTerminalText`
di Custom.Terminal), jadi tombol Stop baru terasa setelah timeout.

**Unzip mengekstrak entri per entri, bukan `ZipFile.ExtractToDirectory`,**
karena metode itu selalu gagal kalau ada berkas yang sudah ada di tujuan
tanpa pilihan menimpa — padahal menjalankan ulang workflow yang sama adalah
keadaan paling umum. Path setiap entri juga diperiksa supaya hasil ekstraksi
tidak bisa keluar dari folder tujuan.

**Zip Files memberi awalan nama folder** untuk entri yang berasal dari folder
(`stat/dalam/dua.txt`), supaya isi dua folder berbeda tidak saling menimpa
hanya karena ada berkas bernama sama.

**Nilai default ditulis sebagai ekspresi VB, bukan Literal** (mis.
`ContinueIfMissing = True`, `Pattern = "*.*"`). `Literal` tersimpan benar tapi
tidak bisa ditampilkan `ExpressionTextBox`, sehingga kotaknya terlihat kosong
padahal berisi nilai.

**Mengisi argumen bertipe array dari kode** (mis. `Files` pada Zip Files)
tidak bisa memakai `Literal` — WF menolak `Literal<String[]>`. Di Studio hal
ini tidak terasa karena user mengetik ekspresi VB; yang perlu tahu hanya
kalau ada yang menyusun activity ini dari kode.

## Status pengujian

Diuji dengan `WorkflowInvoker` pada folder temp sungguhan (harness terpisah,
29 skenario) dan semuanya lulus — termasuk: pembuatan folder tujuan otomatis,
penolakan menimpa tanpa Overwrite, ContinueIfMissing, hitungan Folder Info
rekursif dan non-rekursif (2 berkas / 1 subfolder / 150 byte), BOM pada
Utf8WithBom, pola `*.txt`, Wait For File pada berkas yang baru muncul 0,8
detik kemudian, TimeoutException, serta zip→unzip pulang-pergi lengkap dengan
strukturnya.

Kategori toolbox dan ke-15 activity sudah dipastikan terdeteksi dengan filter
yang sama persis dipakai `wfToolbox.xaml.cs`, dan ke-15 designer sudah
dipastikan bisa di-instansiasi (XAML-nya ter-compile dan ter-load).

---

## Tambahan: tombol pemilih berkas/folder

Setiap properti path di kartu kini punya tombol 📁 di sebelah kanannya.
Modenya dipilih sesuai maksud activity-nya, bukan seragam:

| Mode | Dipakai di |
|---|---|
| pilih berkas (harus sudah ada) | Copy/Move File (Path), Delete File, File Exists, Read Text File, Unzip (Zip Path) |
| tentukan berkas (boleh belum ada) | Copy/Move File (Destination), Write Text File, Append Line, Wait For File, Zip Files (Zip Path) |
| pilih folder | Create/Delete Folder, Folder Exists, Folder Info, List Files In Folder, Unzip (Destination Folder) |
| pilih beberapa berkas | Zip Files (Files) — hasilnya ditulis sebagai array VB `{"a","b"}` |

Wait For File sengaja memakai dialog "tentukan berkas", bukan "buka berkas":
berkas yang ditunggu memang biasanya BELUM ada saat workflow disusun.

Mekanisme dan alasan penulisan nilainya ada di `Custom.Shared/README.md`.

---

## Ikon toolbox

15 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.
