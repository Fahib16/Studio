# Custom.Mail — email lewat MailKit

Kategori toolbox: **Custom.Mail**. Warna header: merah `#FFC62828`.
Satu pustaka saja seperti yang diminta: **MailKit 3.1.1** (dengan MimeKit 3.1.1).

| Activity | Kartu canvas | Properties panel |
|---|---|---|
| **Send Mail (SMTP)** | To *, Subject, Body | Host *, Port, Security, Username, Password, From *, Cc, Bcc, IsBodyHtml, Attachments, Timeout |
| **Get Mail (IMAP)** | Folder, Top, Save to | Host *, Port, Security, Username, Password, OnlyUnread, MarkAsRead, Timeout |
| **Get Mail (POP3)** | Top, Save to | Host *, Port, Security, Username, Password, DeleteAfterDownload, Timeout |
| **Save Attachments** | Message *, Folder *, Save to | Filter, Overwrite |
| **Move Mail To Folder** | Message *, Destination Folder * | Host *, Port, Security, Username, Password, CreateFolderIfMissing |

## Hubungannya dengan OpenRPA.Office

`OpenRPA.Office` sudah punya aktivitas email berbasis **Outlook Interop**
(Get Mails, New Mail Item, Reply, Save, Move). Project ini bukan
penggantinya, melainkan pelengkap: bekerja langsung ke server SMTP/IMAP/POP3
sehingga **tidak memerlukan Outlook terpasang** dan bisa dipakai dari robot
yang berjalan tanpa profil Office.

## Keputusan yang perlu diketahui

**Password bertipe `SecureString`, bukan String.** Itu tipe yang SAMA dengan
output activity `Get Credentials` milik OpenRPA.Utilities, jadi kata sandi
bisa mengalir dari Windows Credential Manager ke activity ini tanpa pernah
singgah di variabel workflow biasa. Untuk mengetik langsung dalam ekspresi:

    New System.Net.NetworkCredential("", "rahasia").SecurePassword

Konversi ke string biasa dilakukan tepat saat dipanggilkan ke MailKit lalu
salinannya dibuang dari memori tak terkelola (`SecureStrings.ToPlain`) —
MailKit sendiri hanya menerima string biasa, jadi konversi tidak bisa
dihindari, yang bisa hanya memendekkan umurnya.

**Username kosong berarti kirim/ambil tanpa autentikasi,** bukan error.
Server relay internal memang bekerja begitu.

**Security = Auto memilih dari nomor port:** port SSL baku protokolnya
(465 SMTP, 993 IMAP, 995 POP3) berarti TLS sejak koneksi dibuka; port lain
memakai STARTTLS kalau server mengiklankannya.

**Hasil Get Mail berupa `MailItem`, bukan `MimeMessage` mentah.**
`MimeMessage` tidak membawa identitas server-nya — tanpa UID dan nama folder,
Move Mail To Folder tidak punya cara menunjuk pesan mana yang dimaksud.
Pesan aslinya tetap tersedia di properti `Message`.

**POP3 tidak punya Only Unread dan Mark As Read.** Protokolnya memang tidak
menyimpan status itu; menyediakan propertinya hanya akan jadi hiasan yang
menyesatkan. Sebagai gantinya ada `Delete After Download` — itulah cara POP3
menandai "sudah diproses". Kalau butuh status baca/belum, pakai IMAP.

**Penandaan/penghapusan dilakukan SETELAH semua pesan berhasil diunduh,**
bukan per pesan, supaya kegagalan di tengah jalan tidak meninggalkan sebagian
email tertandai sudah dibaca (atau terhapus) padahal workflow belum
memprosesnya.

**Move Mail To Folder menolak email hasil POP3** dengan pesan yang menyebut
alasannya, bukan gagal dengan error protokol yang membingungkan.

**Save Attachments tidak menimpa berkas bernama sama** (kecuali Overwrite):
lampiran email sering bernama sama (`invoice.pdf`) padahal isinya berbeda,
jadi berkas kedua diberi akhiran `(1)`, `(2)`, dst.

## Catatan dependensi (penting)

MailKit 3.1.1 → MimeKit 3.1.1 → System.Buffers 4.5.1 + Portable.BouncyCastle 1.9.0.

- `MailKit.dll` dan `MimeKit.dll` **baru** di folder output.
- `System.Buffers` **tidak disalin** (`Private=False`): yang sudah ada di
  output adalah versi assembly 4.0.3.0 — persis yang dibawa paket 4.5.1 —
  dan `App.config` OpenRPA sudah punya binding redirect untuknya.
- `BouncyCastle.Crypto` **tidak disalin** (`Private=False`). Folder output
  sudah berisi versi **1.8.6** yang dibawa iTextSharp (lewat
  OpenRPA.Utilities). Menyalin 1.9.0 ke sana berarti MENIMPA berkas milik
  paket lain — persis kasus "satu tipe jadi dua tipe" yang harus dihindari,
  dan iTextSharp yang dikompilasi terhadap 1.8.6 akan gagal memuatnya tanpa
  binding redirect.

  Konsekuensinya: MimeKit hanya memerlukan BouncyCastle untuk **S/MIME**
  (tanda tangan dan enkripsi email). Semua yang ada di project ini —
  kirim/terima biasa, lampiran — tidak menyentuhnya, dan itu SUDAH DIBUKTIKAN
  lewat uji kirim ke server SMTP (tidak ada FileLoadException). Kalau suatu
  saat S/MIME dibutuhkan, jalan yang benar adalah menaikkan BouncyCastle ke
  1.9.0 untuk SELURUH solution sekaligus dengan binding redirect, bukan
  menyalinnya diam-diam dari sini.

## Status pengujian

Diuji dengan `WorkflowInvoker` (harness terpisah, 12 skenario) dan semuanya
lulus. **Send Mail diuji terhadap server SMTP sungguhan yang dijalankan di
dalam harness** (TcpListener yang berbicara SMTP), bukan sekadar dicek
compile-nya:

- 3 RCPT TO (To berisi dua alamat dipisah koma + satu Cc) — pemisahan alamat
  koma/titik koma terbukti bekerja.
- MAIL FROM benar; subject, isi, dan lampiran benar-benar terkirim di DATA.
- Lampiran yang tidak ada melempar FileNotFoundException; tanpa tujuan
  melempar ArgumentException; ContinueOnError menelan keduanya.
- MailItem memetakan subject/from/to/cc/tanggal/jumlah lampiran dengan benar.
- Save Attachments: 2 lampiran, filter `*.pdf` menyisakan 1, dan berkas
  bernama sama tidak ditimpa (jadi `invoice (1).pdf`).
- Move Mail To Folder menolak email non-IMAP dengan pesan yang menjelaskan.

**BELUM diuji:** Get Mail (IMAP), Get Mail (POP3), dan jalur sukses Move Mail
To Folder — ketiganya perlu server IMAP/POP3 sungguhan dengan akun yang
valid, dan saya tidak punya kredensial untuk itu. Kodenya sudah dipastikan
compile bersih dan memakai API MailKit apa adanya, tapi anggap ketiganya
belum terbukti jalan sampai Anda mencobanya ke server nyata.

---

## Tambahan: tombol pemilih berkas/folder

- **Save Attachments**: tombol 📁 di samping Folder membuka pemilih folder.
- **Send Mail**: Attachments kini ADA DI KARTU dengan tombol pemilih
  beberapa berkas sekaligus; hasilnya ditulis sebagai array VB
  (`{"C:\a.pdf", "C:\b.pdf"}`) yang bisa langsung disunting.

Lihat `Custom.Shared/README.md` untuk mekanismenya.

---

## Ikon toolbox

5 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.
