# Custom.Window — jendela desktop

Kategori toolbox: **Custom.Window**. Warna header: oranye `#FFEF6C00`
(warna yang sama dengan MaximizeWindow versi lama, supaya terasa berlanjut).

| Activity | Kartu canvas | Properties panel |
|---|---|---|
| **Maximize Window** | Window Title | Tab, Title Match Mode, Process Name |
| **Minimize Window** | Window Title | idem |
| **Restore Window** | Window Title | idem |
| **Close Window** | Window Title | idem + WaitForClose, Timeout, output Closed |
| **Get Active Window** | Title, Process Name | output Handle |
| **Wait For Window** | Window Title *, Timeout | Title Match Mode, Process Name, BringToFront, ThrowOnTimeout, output Found & Handle |
| **Set Window Bounds** | X, Y, Width, Height | penentuan window |

Semuanya punya `ContinueOnError` di kategori Common.

## Maximize Window DIPINDAHKAN ke sini

Tipe lama `OpenRPA.Activities.Custom.MaximizeWindow` **sudah dihapus** dari
project OpenRPA dan digantikan `Custom.Window.MaximizeWindow`. Cara kerjanya
tidak berubah (Windows API lewat judul/proses, properti `Tab` tetap ada), tapi
**nama tipenya berubah**, jadi:

> Workflow .xaml yang sudah menyimpan `MaximizeWindow` versi lama tidak akan
> bisa dibuka sampai activity itu di-drag ulang dari toolbox.

Sudah diverifikasi: tipe lama benar-benar tidak ada lagi di `OpenRPA.exe`.

## Cara window ditentukan

Urutan prioritas (lihat `WindowActivityBase`):

1. **Tab** — output Browser dari Open Browser / Attach Browser. Judul tab-nya
   dipakai; paling presisi kalau targetnya browser. Pencocokannya dipaksa
   Contains karena judul window browser biasanya judul tab + nama browser.
2. **Window Title dan/atau Process Name**.
3. **Window yang sedang aktif**, kalau ketiganya kosong.

Butir 3 dipertahankan dari versi lama karena itulah yang membuat "buka
aplikasi lalu maximize" bisa ditulis tanpa mengisi apa pun. Perlu diingat saat
menguji dari Studio: window aktif bisa jadi Studio itu sendiri — isi Process
Name kalau hasilnya harus konsisten.

`Title Match Mode` mendukung **Wildcard** (`*` dan `?`), yang dibutuhkan
Wait For Window karena judul window sering memuat bagian yang berubah-ubah
(mis. `Laporan 2026-09 - Excel`).

Kalau ada beberapa window dengan judul mirip, yang dipakai adalah yang PERTAMA
cocok menurut urutan `EnumWindows` (umumnya mengikuti Z-order). Isi Process
Name untuk mempersempit. Keterbatasan ini terbawa dari pendekatan berbasis
judul dan sudah ada sejak versi lama.

## Keputusan yang perlu diketahui

**Close Window mengirim WM_CLOSE, bukan mematikan proses.** Aplikasi masih
sempat menyimpan dan boleh menampilkan dialog "simpan perubahan?" — mematikan
proses akan membuang pekerjaan yang belum tersimpan tanpa peringatan. Untuk
mematikan paksa, OpenRPA sudah punya activity **Close Application**.
`Wait For Close` (default true) menunggu window benar-benar hilang, supaya
activity berikutnya tidak jalan saat dialog konfirmasi masih terbuka.

**Set Window Bounds menggabungkan posisi dan ukuran dalam satu activity**
(padanan "Set Window Position / Size"), karena Windows hanya punya SATU
panggilan yang mengubah keduanya (MoveWindow). Memisahkannya berarti "Set
Position" tetap harus membaca ukuran lama lalu menuliskannya kembali — lebih
banyak langkah, lebih banyak kemungkinan salah. Nilai `-1` berarti "jangan
ubah" (0 tidak bisa dipakai sebagai penanda karena itu koordinat yang sah).
Window yang sedang maximize di-restore dulu, karena MoveWindow pada window
maximize tidak berpengaruh apa-apa dan itu terlihat seperti kegagalan diam.

**Get Active Window tidak mewarisi properti pencarian.** Yang ditanyakan
justru window mana yang sedang aktif; menampilkan Window Title di sana hanya
akan membingungkan.

## Status pengujian

Diuji dengan `WorkflowInvoker` terhadap **window Notepad sungguhan** (harness
terpisah, 14 skenario) dan semuanya lulus: Wait For Window dengan pola
wildcard, timeout yang melempar dan yang hanya mengisi `Found=False`, Get
Active Window membaca judul + nama proses, Set Window Bounds memindahkan dan
mengubah ukuran (serta membiarkan nilai `-1` apa adanya), Maximize/Restore/
Minimize/Restore yang benar-benar mengubah keadaan window (diperiksa lewat
`IsZoomed`/`IsIconic`), pesan yang jelas saat window tidak ditemukan,
ContinueOnError, serta Close Window yang menutup window dan mengakhiri
prosesnya.

---

## Pembaruan: menunjuk jendela lewat selector

Lima activity di project ini (Maximize, Minimize, Restore, Close, Set Window
Bounds) kini bisa menunjuk jendela lewat **selector desktop** yang sama dengan
yang dipakai Custom.StudioBridge, bukan hanya lewat judul jendela.

Cara kerjanya: `WindowActivityBase.FindWindow` mencoba selector lebih dulu.
Elemen hasilnya dicari `DesktopActions.WaitFor`, lalu handle jendela akarnya
diambil dengan `WindowFinder.RootOf(handle)` — memakai `GetAncestor(GA_ROOT)`,
karena selector sering menunjuk kontrol di dalam jendela, bukan jendelanya.
Kalau Selector kosong, perilaku lama (cari berdasarkan judul) tetap berlaku.

Kartunya memakai `IndicateDesignerBase`, jadi tombol "Indicate on screen" dan
"Edit selector" ada dan tampilannya sama dengan kartu activity lain.

Tujuh activity di project ini juga sudah punya ikon toolbox sendiri di
`Custom.Window/Resources/`.
