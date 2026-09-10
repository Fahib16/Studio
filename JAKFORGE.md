# JakForge — tampilan dan selector Studio

Catatan ini menjelaskan tiga hal yang baru saja masuk: tampilan JakForge,
penyatuan selector, dan ikon activity. Isinya sengaja menyebut juga bagian
yang **tidak** dikerjakan beserta alasannya, supaya tidak ada yang mengira
sesuatu sudah jalan padahal belum.

---

## 1. Tampilan JakForge

Tiga layar di mockup dibuat sebagai tiga bagian nyata di aplikasi.

| Bagian mockup | Berkas | Isi |
|---|---|---|
| Loading | `OpenRPA/Views/JakForgeLoadingWindow.xaml` | Logo, wordmark, teks status, batang progres, cincin titik |
| Project List & Settings | `OpenRPA/Views/JakForgeDashboardWindow.xaml` | Sidebar, sambutan, tombol buat proyek, kartu proyek |
| Canvas Editor | `OpenRPA/MainWindow.xaml` (baris 0 dan 1) | Bilah judul gelap + bilah JakForge |

Semua warna dan huruf berasal dari satu berkas: `OpenRPA/Themes/JakForge.xaml`,
yang dipasang di `App.xaml`. Warnanya diambil dengan **mengukur piksel mockup**,
bukan dikira-kira — krem `#FAF7EE`, cokelat landasan `#4A3927`, cokelat roda gigi
`#8D6C43`, batang progres `#A07E59` di atas `#E7DBCB`.

Logo dan wordmark dipotong langsung dari mockup menjadi
`OpenRPA/Resources/JakForge/logo_mark.png` dan `logo_wordmark.png`. Alasannya:
huruf pada wordmark itu geometris dan tidak ada di Windows; kalau ditulis ulang
dengan font lain bentuknya jelas berbeda.

### Alur saat aplikasi dijalankan

1. `App.Application_Startup` menampilkan layar Loading lebih dulu.
2. Pesan status robot diteruskan ke layar itu (`App_Status`), jadi teksnya
   bergerak sesuai pekerjaan yang benar-benar sedang berlangsung.
3. `MainWindow.Window_Loaded` memanggil `JakForgeLoadingWindow.HandOverTo(this)`
   — merebut status `Application.MainWindow` dari splash lalu menutupnya.
   Urutan ini penting: dengan `ShutdownMode="OnMainWindowClose"`, menutup splash
   sebelum penyerahan berarti mematikan aplikasi.
4. Dashboard muncul di atas jendela utama. Mengklik kartu proyek membuka
   workflow pertamanya lewat `MainWindow.OnOpenWorkflow` (jalur yang sama
   dengan sebelumnya), lalu dashboard menutup.

### Bilah atas jendela utama

Bilah judul digambar sendiri memakai `WindowChrome` (bukan
`AllowsTransparency`), sehingga ubah ukuran dari tepi, snap, dan maximize yang
berhenti di taskbar tetap berperilaku normal.

| Tombol | Terhubung ke |
|---|---|
| Run/Debug | `MainWindow.OnPlay` — sama persis dengan tombol Play lama |
| Panah di sebelahnya | Visual Tracking, Slow Motion, Stop |
| Run | `PlayInChildCommand` (jalan di sesi anak) |
| Save | `SaveCommand` |
| Version Control | **dinonaktifkan** |
| Ikon garis tiga | Membuka kembali pita OpenRPA yang lengkap |

Pita OpenRPA **tidak dihapus**, hanya disembunyikan. Semua perintah lama (New,
Open, Import, Export, Permissions, Record, Package Manager, dan seterusnya)
masih ada di sana dan tombol garis tiga yang membukanya.

### Yang sengaja berbeda dari mockup

| Di mockup | Di aplikasi | Alasan |
|---|---|---|
| Tombol **Version Control** aktif | Tampil tetapi dinonaktifkan, dengan keterangan saat disentuh | Studio ini belum punya integrasi version control apa pun. Tombol yang tidak melakukan apa-apa lebih membingungkan daripada tombol yang jujur mati. |
| Kartu proyek: `Status: Selesai` | `Workflow: N` | OpenRPA tidak menyimpan status selesai per proyek. Menulis "Selesai" berarti memajang keterangan tanpa sumber data. |
| Menu **Template** | Menampilkan pesan bahwa galeri template belum ada | Belum ada fiturnya; daftar kosong akan terbaca seperti kerusakan. |
| Nama dan foto pengguna | Nama dari OpenFlow kalau tersambung, kalau tidak nama pengguna Windows; inisial di lingkaran | Tidak ada sumber foto profil. |
| Panel bawah `Variabel / Argumen` | `Variables / Arguments / Imports` | Bilah itu milik perancang alur kerja bawaan .NET (`System.Activities.Presentation`), bukan XAML kita. |

---

## 2. Selector: satu jalan untuk web dan desktop

Selector bawaan OpenRPA (bentuk JSON, jendela `SelectorWindow`) **tidak dipakai
lagi** di activity buatan sendiri. Semua yang butuh selector kini memakai
selector XML milikmu yang dikenali otomatis bentuknya:

- `<webctrl .../>` → dijalankan lewat pipa `StudioPipeClient` ke
  `Studio.NativeHost` lalu ke extension Chrome.
- `<wnd .../><ctrl .../>` → dijalankan lewat `DesktopActions` (FlaUI).

Pemilihan jalur dilakukan `SelectorKindDetector` dari **bentuk selector**, bukan
dari properti terpisah, sehingga tidak mungkin selector desktop dijalankan lewat
jalur browser gara-gara satu pilihan lupa diubah.

Kartu semua activity itu dibuat dari satu kelas dasar,
`Custom.StudioBridge/Activities/IndicateDesignerBase.cs`, yang menyediakan
baris **Indicate on screen | Edit selector**, pengisian `Selector`, penyimpanan
screenshot, dan penamaan otomatis. Karena satu sumber, tampilannya seragam.

Activity yang memakainya: Custom.StudioBridge (4), Custom.Browser (6),
Custom.Data (1: Extract Data Table), Custom.Window (5).

Dua activity lama yang memakai selector OpenRPA sudah **dihapus**:

| Dihapus | Penggantinya | Pilihannya dipindah ke |
|---|---|---|
| `Type Into (UiPath style)` | `Type Into` (Custom.StudioBridge) | Click Before Typing, Simulate Keystrokes, Delay Between Keys, Key Modifiers, Post Wait |
| `Click (UiPath style)` | `Click` (Custom.StudioBridge) | Mouse Button, Offset X/Y, Key Modifiers, Focus, Virtual Click, Post Wait |

Extension Chrome ikut naik ke **v0.9.0** (klik kanan/tengah, tombol penahan,
offset dari sudut kiri-atas elemen, `getAttribute`, `selectItem`, `setCheck`,
`rect`, `extractTable`). **Extension harus di-reload** di `chrome://extensions/`
setelah menarik perubahan ini; kalau tidak, jalur web akan menjawab
"Unknown action".

---

## 3. Ikon activity

80 activity kini punya ikon sendiri di toolbox, bukan gerigi bawaan. Ikonnya
digambar sebagai PNG 16x16 dengan warna kategori JakForge dan lambang putih
sesuai fungsi (map, jam, folder, amplop, terminal, kamera, dan seterusnya).

Cara pemasangannya sama dengan yang sudah dipakai OpenRPA sendiri:

1. PNG ada di `<Project>/Resources/<nama>.png`, terdaftar sebagai
   `EmbeddedResource` di berkas project.
2. Tiap project punya kelas kosong `ResFinder` di namespace tipenya.
3. Activity memakai
   `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.

Berbeda dari OpenRPA, tiap `EmbeddedResource` di sini menuliskan `LogicalName`
secara eksplisit. Itu perlu karena satu project (`CustomBrowser`) punya
`RootNamespace` yang berbeda dari namespace tipenya (`Custom.Browser`), dan
tanpa `LogicalName` nama resource yang dibentuk MSBuild tidak akan cocok dengan
yang dicari `ToolboxBitmap`.

Sekalian, 15 activity yang selama ini tampil dengan nama kelas mentah
(`StudioActivateTab`, `TypeIntoTerminal`, ...) kini punya `[DisplayName]`,
`[Description]`, dan konstruktor yang menyetel nama itu, sehingga namanya
terbaca sama di toolbox maupun di kanvas.

---

## Yang sudah diuji

- Ikon: 80 dari 80 benar-benar termuat lewat `ToolboxBitmapAttribute.GetImage`
  (diuji per-assembly), dan terlihat di toolbox Studio yang berjalan.
- Alur tampilan: Loading, dashboard, klik kartu proyek, editor kanvas terbuka —
  semuanya dijalankan sungguhan dan difoto layar.
- Regresi activity: harness System, Files, Data, Excel, Window, dan PathPicker
  dijalankan ulang setelah perubahan, semuanya lulus.

## Yang belum diuji

- Jalur **web** untuk kemampuan baru extension v0.9.0 (klik kanan, modifier,
  offset). Kodenya ada di kedua sisi, tetapi Chrome di mesin ini masih memuat
  extension versi lama saat terakhir diperiksa.
- Dashboard dengan banyak proyek: di mesin ini hanya ada satu proyek, jadi tata
  letak kartu yang membungkus ke baris berikutnya belum terlihat dengan data
  sungguhan.

---

# Putaran kedua: 11 penyesuaian

## 1. Bilah judul gelap dihilangkan
Lajur gelap berisi tiga titik lampu lalu lintas sudah tidak ada. Bilah JakForge
sekarang merangkap bilah judul: menyeretnya memindahkan jendela
(`WindowChrome.CaptionHeight` disamakan dengan tinggi bilah), dan tombol
minimalkan/perbesar/tutup pindah ke ujung kanannya. Dashboard mengikuti pola
yang sama.

## 2. Ikon di kanvas = ikon di toolbox
Kepala kartu tiap activity kini menampilkan berkas PNG yang sama dengan
ikonnya di toolbox, bukan emoji. PNG-nya didaftarkan dua kali di berkas
project: `EmbeddedResource` (dipakai `ToolboxBitmap`) dan `Resource` (dipakai
pack URI di XAML kartu). `ActivityDesigner.Icon` juga diisi gambar yang sama,
untuk bilah remah roti saat masuk ke dalam wadah.

## 3. Bingkai bawaan di kanvas dibuang
Kartu tidak lagi terbungkus bilah abu-abu bawaan `ActivityDesigner`
(nama + panah lipat). Template penggantinya ada di
`<Project>/Resources/JakForgeCard.xaml`, satu berkas per project, dipakai
bersama oleh semua kartu di project itu. Bingkai pilihan digambar ulang di
template itu, karena bingkai bawaan ikut hilang bersama template lamanya.

## 4. Warna kartu diselaraskan
Empat belas warna kepala kartu yang berbeda-beda (biru, hijau, nila, tosca)
diganti satu warna per kelompok, dan warnanya SAMA dengan warna ikon kelompok
itu di toolbox. Warna literal lain di dalam kartu (abu-abu, putih) ikut
dipetakan ke warna tema.

## 5. Bilah Variables / Arguments / Imports
Bilah itu digambar oleh perancang alur kerja .NET, dan warnanya diambil dari
properti statis `WorkflowDesignerColors` yang hanya bisa dibaca — tidak ada
kunci resource yang bisa ditimpa. Karena itu bilahnya dicat ulang setelah
tampilan dimuat (`JakForgeWorkflowTheme.Repaint`), dan pencatatannya dibatasi
HANYA pada wadah yang memuat tulisan "Variables" dan "Imports" sekaligus.
Pembatasan itu penting: percobaan pertama mencat seluruh pohon, dan karena
sebagian warna dipakai bersama, latar kanvas ikut menghitam.

Panel Properties memakai jalur resmi
`WorkflowDesigner.PropertyInspectorFontAndColorData`.

## 6. Pita menu lengkap
Warna pita, tab, grup, dan tombolnya disetel di `Ribbon.Resources` supaya hanya
berlaku di sana. Tombol menu aplikasi memakai logo JakForge.
**Belum**: ikon berwarna bawaan tiap tombol pita (New, Save, Open, ...) masih
gaya lama.

## 7. Animasi perpindahan
Layar Loading memudar keluar sebelum ditutup; dashboard memudar masuk sambil
membesar dari 98%; saat proyek dipilih dashboard memudar keluar lalu isi
jendela utama memudar masuk. Semuanya 200-300 ms.

## 8. Tombol "+" penyisip antar-activity
`JakForgeSequenceDesigner` menggantikan designer Sequence bawaan. Tombol bulat
"+" muncul di setiap sela antar-activity; mengkliknya membuka kotak cari
(`JakForgeActivityPicker`) berisi activity yang sama persis dengan panel
Aktivitas, dan pilihan yang diambil disisipkan TEPAT di sela itu.

Posisi sisip dihitung dengan menghitung activity sungguhan yang berada sebelum
sela tersebut di dalam panel, bukan dengan menebak pola selang-seling penyisip
dan activity, supaya tetap benar kalau tata letaknya berubah.

## 9. Minimize tidak lagi ke tray
`Window_StateChanged` tidak lagi menyembunyikan jendela dan memunculkan ikon
tray. Meminimalkan berhenti di taskbar seperti aplikasi lain. Pengaturan
`Config.local.minimize_to_tray` karena itu tidak berpengaruh lagi pada jendela
Studio; ikon tray tetap dipakai mode Assistant.

## 10. Nama dan ikon aplikasi
Judul jendela, `Product`, dan `AssemblyTitle` menjadi JakForge / JakForge
Studio. Ikon aplikasi, ikon jendela, dan ikon tray memakai
`Resources/JakForge/jakforge.ico`, dibuat dari logo dengan tujuh ukuran
(16 sampai 256 piksel).

## 11. Activity bawaan OpenRPA

Aturannya ada di `OpenRPA/Views/JakForgeToolbox.cs`, dan toolbox
(`wfToolbox.xaml.cs`) sekarang mengelompokkan menurut FUNGSI, bukan menurut
nama assembly.

**Disembunyikan karena sudah ada penggantinya (24 activity).** Tipenya tidak
dihapus, jadi workflow lama tetap terbuka dan tetap jalan — hanya tidak bisa
ditambahkan lagi dari toolbox:

| Disembunyikan | Penggantinya |
|---|---|
| Click Element, Type Text, Highlight Element | Click, Type Into, Highlight |
| Focus Element, Move Element | **Focus Element** dan **Hover Element** yang baru |
| NM: OpenURL, CloseTab, GetTab, GetElement, GetTable | Open Browser, Close Tab, Attach Tab, selector JakForge, Extract Data Table |
| IE: OpenURL, GetElement | Open Browser, selector JakForge |
| Windows: GetElement, CloseWindow | selector JakForge, Close Window |
| Utilities: AddDataRow, CreateDataTable, ReadCSV, WriteCSV, ReadExcel, WriteExcel, CompressArchive, ExpandArchive | Add Data Row, Build Data Table, Read/Write CSV, Read Range Workbook, Write Range, Zip Files, Unzip |

**Dua activity baru** menggantikan yang memakai selector lama:
`Focus Element` (fokus tanpa klik) dan `Hover Element` (arahkan mouse tanpa
klik), keduanya memakai selector JakForge dan bekerja untuk web maupun desktop.
Operasi `focus` dan `hover` ditambahkan ke extension (naik ke **v0.10.0**,
harus di-reload lagi di `chrome://extensions/`).

**Dipertahankan dan dipindah kelompok (46 activity)** — lengkap dengan ikon
bertema JakForge yang dibuat baru, menggantikan ikon gaya lama:

| Kelompok | Isi tambahannya |
|---|---|
| Otomasi UI | Move Mouse |
| Browser | Execute Script, Wait For Download |
| Jendela & Aplikasi | Open/Close Application, Start/Kill Process, Get Windows |
| Manajemen File | Select File, Select Folder, Download File, Read PDF |
| Data & Tabel | Add Data Column, Delete Row, Delete All Rows, Set All Rows State, JArray To DataTable, Read JSON, Match, Matches, Replace, For Each Data Row |
| Sistem & Kontrol Alur | Break, Continue, Do While, While, For Each Of, Comment Out, Copy/Insert Clipboard, Show Balloon Tip, Get/Set Credentials, Set Auto Login |
| Orkestrasi | Detector, Invoke OpenFlow/OpenRPA/Remote, Stop OpenRPA, Get Workflow Instance |
| Workitem | Add/Bulk Add/Delete/Update/Pop Workitem, Throw Business Rule Exception |

Kelompok buatan sendiri juga berganti nama menjadi nama fungsi: Otomasi UI,
Browser, Jendela & Aplikasi, Manajemen File, Data & Tabel, Excel, Email,
Terminal, Sistem & Kontrol Alur.

**Belum**: kartu kanvas activity bawaan masih memakai bingkai abu-abu bawaan
.NET; yang sudah bertema JakForge baru kartu activity buatan sendiri dan
Sequence.

---

# Putaran ketiga: 8 penyesuaian

## 1. Sisa activity bawaan: dikelompokkan dan diberi ikon

Toolbox tidak lagi mengelompokkan menurut nama assembly. Aturannya ada di
`OpenRPA/Views/JakForgeToolbox.cs`.

**Tambahan yang disembunyikan** karena kembar dengan activity buatan sendiri:

| Disembunyikan | Penggantinya |
|---|---|
| `System.Activities.Statements.Delay` | Delay (Custom.System) |
| `System.Activities.Statements.WriteLine` | Log Message |
| `OpenRPA.Script.Activities.CustomLogMessage` | Log Message |

**Dikelompokkan menurut fungsi:**

| Kelompok | Isinya |
|---|---|
| Sistem & Kontrol Alur | System.Activities (If, While, Switch, TryCatch, Parallel, Pick, Assign, ...), OpenRPA.Script (Invoke Code, Pip Install), OpenRPA.Forms, OpenRPA.MSSpeech |
| OpenFlow | 15 activity OpenRPA.OpenFlowDB |
| Office (COM) | 25 activity OpenRPA.Office |
| Gambar & OCR | 7 activity OpenRPA.Image |
| Database | 5 activity OpenRPA.Database |
| Perekaman Layar | 3 activity OpenRPA.AviRecorder |
| Rossum | 6 activity OpenRPA.Elis.Rossum |
| Java | OpenRPA.Java |

Semua tetap dipertahankan karena fungsinya memang berbeda: Office memakai
Microsoft Office lewat COM (butuh Office terpasang) sedangkan Excel/Email
buatan sendiri tidak; OpenFlowDB bicara ke server OpenFlow; Image mengandalkan
pencocokan gambar dan OCR.

**Ikon**: activity milik .NET tidak bisa diberi atribut `ToolboxBitmap` dari
luar. Karena itu ikonnya dipasok lewat parameter bitmap pada
`ToolboxItemWrapper` (`JakForgeToolbox.IconFor`), jadi tidak ada assembly asing
yang perlu diubah. Activity kontrol alur yang sering dipakai punya ikon
sendiri-sendiri (If, While, ForEach, Switch, TryCatch, Assign, Parallel, Pick,
Sequence, Throw, Terminate, Flowchart, State, Collection); sisanya memakai satu
ikon per kelompok.

## 2. Jarak isi saat dimaksimalkan

Jendela tanpa bilah judul bawaan dibesarkan Windows sampai melebihi layar
sebesar tebal bingkai ubah-ukuran. `ApplyMaximizedPadding` mengganti selisih
itu dengan margin yang dibaca dari `SystemParameters`, sehingga tulisan panel
kiri dan tab panel bawah tidak lagi menempel di tepi layar.

Bilah atas juga diubah menjadi dua lajur: nama proyek boleh menyusut dan
dipotong elipsis, tombol di kanan tidak lagi menimpanya.

## 3. Ikon pita

23 ikon pita 32x32 dibuat ulang bertema JakForge
(`OpenRPA/Resources/JakForge/ribbon/`), menggantikan ikon berwarna gaya lama:
New, Save, Open, Copy, Delete, Permissions, Reload, Import, Export, Play, Stop,
Record, Signout, Exit, tiga ikon browser, swap, dan picture-in-picture.

## 4. Tombol Home

Tombol Home di bilah atas mengembalikan tampilan ke layar Home tanpa menutup
apa pun; workflow yang sedang dibuka tetap terbuka. Perilakunya mengikuti Home
di UiPath Studio.

## 5. Animasi layar Loading

Dua perbaikan:

* Layar Loading kini hidup di **thread-nya sendiri**. Sebelumnya ia berbagi
  thread UI dengan penyiapan Studio, jadi animasinya membeku persis saat
  Studio sedang sibuk memuat — yaitu sepanjang waktu layar itu tampil.
* Batang progresnya berupa potongan yang bergeser dari kiri ke kanan, bukan
  isian yang melebar lalu mengulang. Pengulangan yang lama terlihat sebagai
  lompatan.

## 6. Alur jendela: Home dulu, kanvas menyusul

`OpenRPA/Views/JakForgeShell.cs` yang mengatur:

* Sesudah layar Loading, yang tampil HANYA layar Home.
* Jendela kanvas tetap dimuat di belakang layar (penyiapan Studio bergantung
  padanya) tetapi disembunyikan.
* Kanvas baru tampil saat proyek dibuka atau dibuat.
* Menutup layar Home saat kanvas belum pernah tampil berarti keluar dari
  aplikasi; kalau kanvas sudah tampil, Home cuma disembunyikan.

Satu jebakan yang perlu dicatat: menyembunyikan jendela di dalam `Loaded`
tidak berpengaruh, karena `Window.Show()` menetapkan `Visibility=Visible`
SESUDAH memicu `Loaded`. Penyembunyiannya karena itu ditunda satu putaran
dispatcher.

## 7. Panel Output dan level Log Message

Dua hal yang saling terkait:

* **Kenapa dulu hanya level Info yang bekerja**: panel Output lama hanya
  menampung baris berkategori "Output" (`Tracing.WriteLine`). Warning, Error,
  dan Verbose hanya masuk ke panel Logging. Selain itu `Log.Warning`,
  `Log.Error`, dan `Log.Verbose` disaring pengaturan global, dan `log_verbose`
  bawaannya mati.
* **Perbaikannya**: `JakForgeOutputFeed` menerima semua level, dan activity
  Log Message menulis langsung ke `System.Diagnostics.Trace` dengan kategori
  yang sesuai sehingga tidak tersaring pengaturan.

Panel barunya (`JakForgeOutputView`) punya penghitung per level yang sekaligus
menjadi saringan, kotak cari, penanda level berwarna, tombol bersihkan, dan
tombol salin. Level Trace dimatikan secara bawaan karena jumlahnya sangat
banyak.

## 8. Continue On Error dengan kotak centang

`Custom.Shared/ContinueOnErrorEditor.cs` memasang kotak centang di sebelah
kotak teks pada panel Properties untuk 61 properti Continue On Error.
Urutannya: kosong, True, False, kembali kosong — berbeda dari urutan bawaan
WPF, karena keadaan awal properti ini memang kosong sehingga klik pertama
sebaiknya langsung berarti "ya". Mengetik ekspresi sendiri di kotak teksnya
tetap bisa.

---

# Insiden: Studio mati di layar Loading (dan cara menemukannya)

**Gejala.** Studio berhenti di layar Loading dan tidak pernah sampai ke layar
Home. Menjalankannya berulang kali membuat seluruh mesin ikut berat.

**Yang terlihat dari luar menyesatkan.** Prosesnya masih ada dan layar
Loading-nya masih tergambar, jadi tampak seperti "menggantung". Yang sebenarnya
terjadi: prosesnya sudah MATI, dan Windows Error Reporting membekukan seluruh
thread-nya untuk mengambil dump. Layar Loading tetap terlihat karena
gambarnya sudah terlanjur ada di layar.

**Bukti yang dipakai:**

1. Pemakaian CPU berhenti di angka tetap (4,7 detik) dan tidak naik lagi —
   berarti bukan lambat, melainkan tidak berjalan sama sekali.
2. 46 dari 47 thread berstatus `Wait, Suspended` — pola khas proses yang
   dibekukan Windows Error Reporting, bukan proses yang sibuk.
3. Catatan `Application Error` di Event Viewer: **Exception Code c00000fd**,
   yaitu STACK OVERFLOW, dengan modul `ntdll.dll`.

**Cara menemukan penyebabnya.** StackOverflowException tidak bisa ditangkap
dan tidak meninggalkan jejak stack terkelola, jadi penyebabnya dicari dengan
membelah: dipasang dua saklar sementara lewat variabel lingkungan, lalu
aplikasi dijalankan sekali untuk tiap saklar.

| Percobaan | Hasil |
|---|---|
| Umpan panel Output dimatikan | tetap mati |
| Ikon toolbox dimatikan | **hidup normal** |

**Penyebabnya**: mengoper pack URI sebagai parameter bitmap ke
`ToolboxItemWrapper(type, bitmap, displayName)`. Cara itu dipakai untuk memberi
ikon bertema pada activity milik .NET yang tidak bisa diberi atribut
`ToolboxBitmap`. Ternyata resolusi bitmap di dalam WF berujung rekursi tak
berujung.

**Perbaikannya**: kembali memakai konstruktor tanpa bitmap. Pemetaan
tipe-ke-ikon (`JakForgeToolbox.IconFor`) dipertahankan tetapi tidak dipanggil,
menunggu jalur yang aman, yaitu template item toolbox milik sendiri.

**Akibatnya sekarang**: activity buatan sendiri dan activity OpenRPA tetap
memakai ikon JakForge (lewat atribut `ToolboxBitmap`, jalur yang berbeda dan
aman). Yang kembali memakai ikon bawaan hanyalah activity milik .NET
(If, While, Switch, TryCatch, dan kawan-kawan) serta plugin yang tidak bisa
disentuh.

## Satu rekursi lain yang ikut diperbaiki

`JakForgeShell.HomeClosing` memanggil `Application.Shutdown()`, dan Shutdown
menutup semua jendela sehingga `OnClosing` layar Home terpanggil LAGI —
memanggil Shutdown lagi di tumpukan yang sama. Sekarang dijaga penanda
`shuttingDown`. Bug ini nyata meskipun bukan penyebab macet di layar Loading.

## Umpan panel Output juga diperbaiki

Versi pertama mengirim setiap baris log ke thread UI lewat
`Dispatcher.BeginInvoke`. OpenRPA menulis ribuan baris saat memuat, tepat ketika
thread UI sedang sibuk — antrean menumpuk dan tiap baris memicu pembaruan
daftar berfilter. Sekarang baris hanya ditumpuk di memori dengan kunci biasa,
dan panel Output yang MENARIK isinya tiap setengah detik. Kalau panelnya belum
pernah dibuka, tidak ada pekerjaan UI sama sekali.

---

# Putaran keempat: 6 catatan

## 1. Tombol Run tidak berfungsi

Tombol itu dulu memanggil `PlayInChildCommand`, dan `CanPlayInChild` menolak
kecuali ada sesi anak yang aktif (`OpenRPAServiceUtil.RemoteInstance`) — pada
pemakaian biasa memang tidak ada, jadi tombolnya diam saja.

Sekarang keduanya menjalankan workflow sungguhan lewat `OnPlay`, bedanya:

| Tombol | Perilaku |
|---|---|
| Run/Debug | penelusuran (Visual Tracking) dinyalakan, activity yang berjalan disorot di kanvas |
| Run | penelusuran dan gerak lambat dimatikan — jalan secepatnya |

Kalau tidak ada workflow yang terbuka, sekarang muncul pesan di panel Output,
bukan diam tanpa penjelasan.

## 2. Click ke tombol Download tidak jalan

Belum ada bukti bahwa jalur klik-nya sendiri rusak: berkas extension tidak
berubah selain penambahan operasi `focus` dan `hover` (bagian `click` tidak
disentuh). Yang pasti rusak adalah dua hal di sekitarnya, dan keduanya membuat
workflow tidak pernah benar-benar berjalan:

* tombol Run diam saja (catatan 1),
* memilih Slow Motion melempar kesalahan (catatan 4).

Keduanya sudah diperbaiki. Kalau setelah ini Click masih tidak jalan, panel
Output sekarang menampilkan level Error, jadi pesan aslinya akan terlihat di
sana. Pastikan juga extension sudah di-reload di `chrome://extensions/`
(versinya kini 0.10.0).

## 3. Wait For Download versi baru

Activity bawaan (`OpenRPA.NM.WaitForDownload`) disembunyikan dari toolbox dan
diganti `Wait For Download` di kelompok Manajemen File. Bedanya:

| Bawaan | Versi baru |
|---|---|
| tidak memberi lokasi berkas hasil unduhan | properti **Downloaded file** berisi lokasi lengkapnya |
| hanya folder unduhan Chrome lewat extension | folder mana pun, termasuk unduhan aplikasi desktop |
| tidak tahu kapan berkas selesai | menunggu ukurannya berhenti bertambah DAN berkasnya tidak lagi terkunci |
| akhiran sementara tidak diurus | `.crdownload`, `.part`, `.partial`, `.tmp`, `.download`, `.opdownload` diabaikan, ditambah yang kamu sebut sendiri |

Isi folder dicatat SEBELUM activity pemicu dijalankan, sehingga berkas lama
tidak mungkin dikira hasil unduhan baru.

## 4. Slow Motion melempar InvalidCastException

Menu Slow Motion dan Visual Tracking memanggil perintah OpenRPA dengan
`CommandParameter="{Binding}"`, sedangkan `OnSlowMotion` meng-cast
parameternya menjadi `bool` — yang terkirim justru DataContext jendela.
Sekarang penanganannya di code-behind, memakai nilai centang menu itu sendiri,
dan centangnya disamakan dengan keadaan designer setiap menu dibuka.

## 5. Panel Project menggantikan tab "Open project"

`JakForgeProjectView` dipasang sebagai panel di sisi kiri, bersebelahan dengan
Aktivitas dan Snippets — susunan yang sama dengan UiPath Studio. Isinya pohon:
Dependencies, Workflows, dan berkas di folder proyek, lengkap dengan kotak cari.
Klik dua kali pada workflow membukanya di kanvas; pada berkas biasa
membukanya dengan aplikasi bawaan Windows. Empat tombol di atasnya: muat ulang,
buka folder proyek, kelola paket, dan workflow baru.

## 6. Tab Design dan Debug

Dua tab baru di pita menu (dibuka lewat tombol garis tiga).

**Design**: New, Save, Export as Template, Publish, Debug File, Stop,
Cut/Copy/Paste, Undo/Redo, Manage Packages, Recording, UI Explorer, User Events.

**Debug**: Debug File, Stop, Restart, Step, Continue, Breakpoint,
Execution Trail, Slow Step, Minimize saat jalan, Open Logs.

Semua tombol itu terhubung ke kemampuan yang memang ada. Yang TIDAK dibuatkan
tombol, karena Studio ini belum punya padanannya sama sekali: Test Manager,
Manage Entities, Remote Debugging, Profile Execution, Picture in Picture,
Analyze File, Remove Unused, Export to Excel, Screen Scraping, dan pembedaan
Step Into / Step Over / Step Out (OpenRPA hanya punya satu bentuk langkah,
jadi tombolnya juga satu: **Step**).

## Pencatat kegagalan

Ditambahkan `jakforge-crash.log` di folder proyek OpenRPA. Kegagalan yang tidak
tertangani ditulis lengkap beserta InnerException-nya. Sebelum ini, kegagalan
saat memuat jendela utama hanya meninggalkan nama pengecualian di Event Viewer
tanpa pesan dan tanpa baris yang salah — dan itu memperlambat penelusuran dua
insiden terakhir.

---

# Putaran kelima: 2 catatan

## 1. Pencocokan teks disamakan dengan UI Explorer UiPath

Activity **Click** yang diarahkan ke tombol *DOWNLOAD EXCEL* di rpachallenge.com
gagal dengan pesan:

```
Studio Bridge: Elemen tidak ditemukan:
<webctrl tag='a' innertext='DOWNLOAD EXCEL cloud_download' />
```

Padahal selector itu dibuat oleh pemilih kita sendiri dari elemen yang sama.
Ada dua sebab, keduanya di sisi pencocokan:

1. **Spasi.** `innerText` tombol itu berisi baris baru — ikon `cloud_download`
   dirender sebagai blok tersendiri — sedangkan selector menyimpan versi satu
   spasi. Dibandingkan mentah-mentah, keduanya tidak pernah sama.
2. **Huruf besar-kecil.** Tombol itu tampil KAPITAL karena `text-transform`
   CSS, sementara teks di DOM ditulis "Download Excel". Yang tersimpan di
   selector adalah versi yang terlihat.

Perbaikannya di `Extension/background.js`: seluruh atribut yang berisi teks
(`innertext`, `visibleinnertext`, `owntext`, `aaname`, `text`) dinormalkan
spasinya lalu dibandingkan tanpa membedakan huruf besar-kecil. Atribut `tag`
juga sudah begitu, jadi selector dari UiPath (yang menulis `tag='A'`) tetap
cocok.

Ditambahkan pula atribut `aaname` — nama yang dibacakan pembaca layar, dihitung
dari `aria-label`, `aria-labelledby`, label/placeholder untuk input, `alt`
gambar, `title`, lalu teks elemen. Inilah atribut yang dipakai UiPath sebagai
pilihan bawaan untuk tombol dan tautan, dan sekarang UI Explorer kita
mencentangnya juga (`aaname` + `tag`), dengan `innertext` sebagai cadangan
terakhir. Daftar atribut di panel kanan UI Explorer kini sama isinya dengan
UiPath: `aaname`, `class`, `css-selector`, `href`, `innertext`, `isleaf`,
`parentclass`, `visibleinnertext`.

Versi extension dinaikkan ke **0.11.0**, jadi extension-nya harus dimuat ulang
di `chrome://extensions/` sebelum perubahan ini terasa.

## 2. Attach Browser lepas dari selector OpenRPA

Ini activity terakhir yang masih membuka `SelectorWindow` milik OpenRPA —
jendela berjudul "Selector" dengan isi `{"Selector": "NM"}` itu. Yang aneh,
dari selector sepanjang itu, satu-satunya yang benar-benar dibaca saat
dijalankan adalah URL tab-nya.

Sekarang:

- Properti **Url** menggantikan **Selector**. Isinya dicocokkan dengan tab yang
  URL-nya memuat teks itu — persis yang dulu dilakukan diam-diam lewat
  `NMSelectorItem`, jadi tidak ada kemampuan yang hilang.
- Tombol dan tautan *Indicate browser on screen* memanggil
  `IndicateHelper.PickTabForAttach()`, pemilih tab dari jembatan Studio sendiri.
- Kartunya sekarang menampilkan kotak Url yang bisa langsung diketik atau diisi
  ekspresi, bukan lagi baris selector yang tidak terbaca.
- Properti `Selector` lama masih ada tapi `[Browsable(false)]`, semata supaya
  workflow yang terlanjur menyimpannya tetap bisa dibuka.

Setelah ini tidak ada lagi berkas di `Custom.*` yang memanggil selector OpenRPA;
yang tersisa hanya kalimat di komentar.

## Yang sudah diuji dan yang belum

Sudah: seluruh solusi dibangun tanpa error, dan Studio dijalankan sampai layar
Dashboard lalu ditutup — tidak ada `jakforge-crash.log` yang terbentuk.

Belum: pencocokan teks yang baru belum saya jalankan melawan rpachallenge.com,
karena itu perlu extension dimuat ulang di Chrome milik Anda dan tidak bisa saya
lakukan dari sini. Begitu juga kartu Attach Browser yang baru — sudah terbangun,
tapi belum pernah saya buka di kanvas.

## 3. "The semaphore timeout period has expired" saat Indicate

Pesan itu bukan berasal dari kode kita — itu terjemahan apa adanya dari kode
kesalahan Windows `ERROR_SEM_TIMEOUT` (121), yang muncul saat sebuah *named
pipe* memang ADA tapi tidak ada instans yang bisa dipakai.

Penyebabnya di native host:

```csharp
new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1)
```

Angka `1` itu artinya hanya SATU sambungan yang bisa dilayani pada satu waktu.
Biasanya cukup, karena perintah seperti `listTabs` selesai dalam sekejap.
Tapi perintah `indicate` menahan sambungannya sampai user mengklik elemen —
bisa semenit penuh. Selama itu pipe-nya tertutup untuk siapa pun.

Yang paling sering menabraknya justru bagian yang dirancang untuk berjalan
BERSAMAAN dengan indicate: `WebIndicateWaitWindow` memantau kursor tiap 120 ms,
dan begitu kursor keluar dari browser ia mengirim `cancelIndicate` — persis
selagi `indicate` masih menggantung. Pembatalan itu tidak pernah kebagian
instans, jadi:

1. `cancelIndicate` gagal diam-diam (kegagalannya memang sengaja ditelan),
2. `indicate` lama tetap menggantung sampai 60 detik penuh,
3. perintah berikutnya — `listTabs` saat user kembali ke browser — ikut
   kehabisan waktu menunggu instans, dan **itulah kotak dialog yang muncul.**

Bukti pendukungnya ada di `%LOCALAPPDATA%\Studio\NativeHost\error.log`: berkas
4,3 MB berisi 4.075 baris `IOException: All pipe instances are busy` — proses
host kedua yang mencoba membuat server tiap 500 ms tanpa henti selamanya.

Perbaikannya tiga lapis:

**Native host** — bukan lagi satu pelayan, tapi kolam berisi empat
(`PipeInstances = 4`), masing-masing di thread sendiri. Badan pelayanannya
dipindah apa adanya ke `ServePipeRequest`, jadi yang berubah hanya siapa yang
memanggilnya. Penulisan ke stdout browser sudah dilindungi `_writeLock` sejak
awal, dan korelasi balasan sudah memakai `id`, jadi keduanya memang sudah siap
untuk dipakai bersamaan.

**Host cadangan tidak lagi membanjiri log** — kalau `ERROR_PIPE_BUSY` yang
didapat, berarti proses host LAIN sudah memegang pipe ini. Proses itu sekarang
diam sebagai cadangan: jeda 5 detik, dan dicatat SEKALI saja per proses.

**Sisi Studio** — `NamedPipeClientStream.Connect` hanya sabar untuk satu
keadaan, yaitu pipe belum ada sama sekali; keadaan "semua instans terpakai"
langsung dilempar sebagai `IOException` mentah. Sekarang dibungkus
`ConnectWithRetry`: dua-duanya dicoba ulang sampai jatah waktunya habis, baru
menyerah dengan pesan berbahasa manusia. Sengaja TIDAK dipasang kunci di sisi
klien — `cancelIndicate` memang HARUS bisa berjalan selagi `indicate` menunggu.

## 4. Indicate mati total — regresi dari perbaikan sebelumnya

Ini kesalahan saya di putaran sebelumnya, bukan efek samping perubahan
tampilan.

Saat merapikan pencocokan teks, saya menambahkan `normText()` dan memakainya
juga di baris hasil picker:

```js
text: normText(target.innerText).slice(0, 80)
```

Masalahnya, baris itu berada di dalam `indicatePickerFn`, sedangkan `normText`
didefinisikan di dalam `pageOpsFn` — fungsi yang sama sekali berbeda. Keduanya
disuntikkan ke halaman lewat `chrome.scripting.executeScript`, dan yang dikirim
ke halaman **hanya sumber fungsi yang bersangkutan**. Apa pun di luar tubuhnya
tidak ikut. Jadi di halaman, `normText` tidak pernah ada.

Akibatnya jauh lebih buruk daripada satu pesan kesalahan. Baris itu dijalankan
DI DALAM penangan klik, tepat sesudah `cleanup()` dan tepat sebelum `resolve()`:

1. overlay picker sudah dibersihkan, jadi sorotan birunya hilang,
2. `resolve()` tidak pernah dipanggil, jadi Promise-nya menggantung,
3. native host menunggu sampai batas waktunya.

Dari sisi user: klik Indicate, klik elemen di halaman, lalu **tidak terjadi
apa-apa sama sekali** — tanpa satu pun pesan yang menunjuk penyebabnya.

Perbaikannya dua bagian:

- `normText` disalin sebagai fungsi lokal di dalam `indicatePickerFn`. Salinan,
  bukan berbagi — memang begitu aturannya untuk fungsi yang disuntikkan.
- Penyusunan hasil di penangan klik dibungkus `try/catch` yang tetap memanggil
  `resolve()` dengan pesan kesalahan. Kegagalan sejenis di kemudian hari akan
  muncul sebagai pesan, bukan sebagai kebisuan.

Diuji langsung, bukan hanya dibaca: fungsi picker-nya diekstrak dan dijalankan
di Chrome headless terhadap tombol tiruan yang meniru "DOWNLOAD EXCEL"
rpachallenge (teks berisi baris baru, huruf dikapitalkan lewat CSS).

- versi sekarang → `{"success":true, ..., "text":"DOWNLOAD EXCEL CLOUD_DOWNLOAD"}`
- versi tanpa `normText` lokal → `normText is not defined`

Versi extension naik ke **0.11.1**, jadi harus dimuat ulang di
`chrome://extensions/`.

Satu catatan tentang penelusurannya: dugaan awal saya salah. Saya sempat curiga
`JF.CardTemplate` menelan klik pada tautan Indicate. Menjalankan Studio dan
menekan tautannya membuktikan sebaliknya — picker desktop muncul normal, jadi
kartu dan tombolnya baik-baik saja. Yang rusak justru ada di seberang jembatan,
di dalam halaman.

## 5. UI Explorer: hasil sunting centang tidak tersimpan

Gejalanya: di panel Selected/Unselected Items, centang `id` dilepas dan
`ng-reflect-name` dicentang, lalu Save — tapi yang tersimpan tetap selector
semula.

Petunjuk pertamanya ada di layar itu sendiri. Selector yang dibawa masuk hanya
SATU baris (`<webctrl id='RtHBK' />`), tapi Selector Editor menampilkan TIGA
BELAS baris `<webctrl />` — seluruh rantai leluhur dari pohon DOM. Artinya
daftar tingkat itu bukan hasil membaca selector yang tersimpan; ia disusun ulang
dari pohon.

Penyebabnya `DomTreeView_SelectedItemChanged`. Saat pohon dimuat, elemen target
dipilih otomatis (`target.IsSelected = true`), dan pemilihan itu memicu
`LoadChainFor` → `LoadNodes(applyDefaults: true)`, yang **mengganti seluruh
daftar tingkat dan menebak ulang centangnya**.

Yang membuatnya terasa seperti "Save tidak menyimpan": TreeView membuat wadah
item-nya bertahap saat layout, bukan seketika. Pada pohon sedalam ini,
pemilihan otomatis tadi bisa baru benar-benar sampai BELAKANGAN — termasuk
setelah user sempat mengubah centang, karena perubahan centang sendiri memicu
layout. Urutan yang dialami user:

1. centang diubah → kotak Selector berisi `<webctrl ng-reflect-name='...' />`,
2. pemilihan otomatis pohon akhirnya sampai → tingkat disusun ulang dengan
   tebakan bawaan → kotak Selector kembali `<webctrl id='RtHBK' />`,
3. Save membaca kotak itu, jadi yang tersimpan yang lama.

Dua perbaikan, keduanya di akar yang sama:

**Simpul yang sama tidak disusun ulang.** `_loadedChainSource` mengingat simpul
pohon yang tingkatnya sudah dimuat. Peristiwa pemilihan untuk simpul yang sama
diabaikan, jadi pemilihan otomatis yang datang terlambat tidak bisa lagi
menghapus suntingan user.

**Centang lama benar-benar dipulihkan.** Rantai dari pohon tetap dipakai —
karena hanya di situ SEMUA atribut elemen tersedia, termasuk `ng-reflect-name`
yang mau dipilih user — tapi centangnya diambil dari selector yang tersimpan
lewat `SelectorDocument.ApplySaved`, bukan ditebak ulang. Pencocokan tingkat
dilakukan dari belakang (tingkat terakhir selalu elemen target), nilai
berpola `*`/`?` cukup dicocokkan namanya, dan spasi dirapikan seperti di sisi
extension. Kalau tidak satu tingkat pun cocok — halaman berubah sejak selector
disimpan — barulah jatuh ke tebakan bawaan, supaya tidak berakhir dengan
selector kosong.

Diuji dengan harness kecil terhadap rantai tiruan yang meniru layar tersebut:

| langkah | hasil |
|---|---|
| buka dengan `<webctrl id='RtHBK' />` | `<webctrl id='RtHBK' />` (bukan tebakan ulang) |
| lepas `id`, centang `ng-reflect-name` | `<webctrl ng-reflect-name='labelFirstName' />` |
| buka lagi dengan hasil suntingan | `<webctrl ng-reflect-name='labelFirstName' />` |
| selector sudah tidak cocok | jatuh ke tebakan bawaan, bukan selector kosong |

---

# Putaran keenam: 3 catatan

## 1. Tombol Run tidak berfungsi

Ada TIGA sebab yang menumpuk. Ketiganya nyata dan ketiganya harus diperbaiki.

### a. Fokus panel menentukan segalanya

`SelectedContent` itu isi panel yang TERAKHIR MENDAPAT FOKUS di docking
manager, bukan kanvas yang sedang terbuka. Begitu user mengklik Toolbox,
Properties, atau panel Project, `SelectedContent` bukan lagi `WFDesigner` —
dan semua yang bergantung padanya berhenti bekerja tanpa pesan:

| yang ikut mati | kenapa |
|---|---|
| `CanPlay` / `OnPlay` | keduanya membuka dengan `SelectedContent is WFDesigner` |
| `VisualTracking` / `SlowMotion` | setter-nya `if (Designer == null) return;` |
| nama proyek di header | `UpdateProjectHeader` membaca `SelectedContent` |
| Step, Continue, Breakpoint | lewat `ActiveDesigner` |

Sekarang ada `ResolveDesigner()` yang mencari kanvas bertingkat: yang sedang
fokus → kanvas terakhir yang dipakai (asal tab-nya masih terbuka) → tab dokumen
yang terpilih → satu-satunya kanvas yang terbuka. `FocusDesigner()`
mengaktifkan tab-nya lebih dulu supaya penelusuran menyorot di kanvas yang
terlihat. `StartRun` juga tidak lagi lewat `OnPlay` — `OnPlay` membaca
`SelectedContent` sekali lagi, jadi ia akan berhenti di tempat yang sama.

### b. Versi assembly tidak cocok dengan bindingRedirect

Ini yang paling merusak, dan tidak kelihatan sama sekali dari layar. Empat
assembly di `debug\net462` berbeda versi dari yang ditulis di
`OpenRPA.exe.config`:

| assembly | diminta redirect | yang tersalin |
|---|---|---|
| Microsoft.Bcl.AsyncInterfaces | 9.0.0.0 | 5.0.0.0 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0.0 | 5.0.0.0 |
| System.Memory | 4.0.1.2 | 4.0.1.1 |
| System.ValueTuple | 4.0.3.0 | 4.0.2.0 |

Setiap kali salah satunya dimuat, .NET melempar `FileLoadException: manifest
definition does not match`. Yang paling parah saat Studio memindai tipe
activity lewat `GetExportedTypes` — pemindaian gagal, workflow tidak bisa
disusun, dan **kanvas terbuka KOSONG**. Tombol Run pun tidak punya apa-apa
untuk dijalankan.

Penyebabnya folder keluaran yang dipakai bersama: `OpenRPA.Forms` dan
`OpenRPA.Script` menyalin versi lama dari `Microsoft.NET.Build.Extensions`,
menimpa versi yang dipakai `OpenRPA` sendiri. Perbaikannya empat
`PackageReference` eksplisit di `OpenRPA.csproj` pada versi yang persis sama
dengan redirect-nya. Bukan pustaka baru — keempatnya sudah ada di graf
dependensi, memang dari situ redirect-nya dihasilkan.

### c. Wait For Download membuat seluruh workflow ditolak

Setelah dua perbaikan di atas, Run akhirnya jalan — dan langsung gagal dalam
0,0 detik dengan:

```
The activity 'Wait For Download' cannot reference activity 'Click Downlaod Excel'
because activity 'Click Downlaod Excel' is already referenced elsewhere ...
```

`CacheMetadata` di Wait For Download memanggil `base.CacheMetadata(metadata)`
DAN `metadata.AddChild(Body)`. Padahal base sudah menemukan `Body` sendiri
lewat refleksi — properti publik bertipe `Activity` otomatis jadi anak. Jadi
activity yang sama terdaftar dua kali, dan WF menolak SELURUH workflow sebelum
baris pertama dijalankan. Baris `AddChild` itu dihapus.

## 2. Panel Project tidak pernah muncul

Panelnya memang sudah ada di `MainWindow.xaml` sejak putaran sebelumnya. Yang
membuangnya adalah `layout.config`.

`XmlLayoutSerializer.Deserialize` mengganti SELURUH pohon layout dengan isi
berkas. Panel yang baru ditambahkan di XAML — yang belum ada saat layout
terakhir disimpan — lenyap begitu saja, tanpa pesan. Layout milik user memuat
`Toolbox`, `Snippets`, `Properties`, `Output`, `Logging`, `WorkflowInstances`,
`openproject` — tidak ada `JakForgeProject`.

Sekarang panel yang dideklarasikan di XAML dicatat DULU sebelum layout dibaca
(`CaptureDeclaredPanes`), lalu yang hilang dipasang kembali di sebelah tetangga
aslinya (`RestoreDeclaredPanes`). Tetangganya ikut dicatat supaya tidak perlu
daftar posisi yang ditulis manual dan gampang basi. Tata letak yang sudah
diatur user tetap dipertahankan, dan menghapus `layout.config` tidak lagi jadi
satu-satunya jalan setiap kali ada panel baru.

## 3. Panel Debug

Panel baru `Views/JakForgeDebugView`, di sisi bawah bersama Output.

Isinya hanya yang benar-benar bisa dibaca dari mesin workflow OpenRPA:

- **Tombol** Continue, Step, Stop, Toggle Breakpoint — menempel ke perintah
  yang memang sudah ada, bukan tombol hiasan.
- **Riwayat jalan**: setiap jalannya workflow, keadaannya, dan lamanya.
- **Variabel**: nama, tipe, dan nilai, diperbarui `WorkflowTrackingParticipant`
  selama activity berjalan. Nilai panjang dipotong di 200 karakter supaya panel
  tidak tersendat.
- **Kotak kesalahan**: muncul hanya kalau jalannya gagal.

Seperti panel Output, datanya DITARIK tiap setengah detik. Berlangganan
peristiwa dari mesin workflow berarti thread pekerja menyentuh UI, dan itu
sudah pernah membuat Studio berhenti di layar Loading.

## Yang diuji

Diuji sungguhan di Studio, bukan cuma dibangun:

1. Buka proyek Tesproject — kanvas menampilkan seluruh workflow (Open Browser →
   Sequence → Wait For Download → Click). Sebelum perbaikan (b), kanvasnya
   kosong.
2. Klik activity di panel Aktivitas supaya fokus BUKAN di kanvas — persis
   keadaan yang membuat Run diam — lalu tekan Run.
3. Workflow berjalan **22,3 detik**: Chrome terbuka, berkas terunduh, dan panel
   Debug menampilkan variabelnya, termasuk
   `DownloadedFile = C:\Users\Fajar\Downloads\challenge (4).xlsx`.
4. Panel Project menampilkan Tesproject → Dependencies / Workflows / Berkas.

Jalannya berhenti di activity berikutnya dengan
`Studio Bridge: Elemen tidak ditemukan: <webctrl tag='button' aaname='START' />`.
Itu soal selector di workflow Anda sendiri — tombol START baru muncul setelah
formulirnya terisi — bukan cacat Studio.

---

# Putaran keenam: 17 catatan

Yang dikerjakan di putaran ini, beserta yang TIDAK dikerjakan dan alasannya.

## 1. Urutan eksekusi Wait For Download — diselidiki, ternyata sudah benar

Dugaannya activity di dalam "Do" dilewati dan langkah SESUDAHNYA jalan duluan.
Diuji langsung dengan harness kecil di luar Studio: satu Sequence berisi
WaitForDownload (Body = langkah A) lalu langkah B, dijalankan dengan
WorkflowInvoker.

```
1_BODY (klik download)   @15:29:03.760
2_SESUDAH (klik start)   @15:29:07.913
```

Body jalan LEBIH DULU, dan langkah berikutnya baru jalan 4 detik kemudian —
persis lama timeout yang disetel. Jadi penjadwalannya benar.

Kalau di layar yang terlihat justru tombol Start yang ditekan duluan,
penyebabnya bukan urutan melainkan klik unduhannya yang tidak menghasilkan apa
apa (mis. selector meleset), sementara unduhan sendiri berjalan diam-diam di
balik layar tanpa terlihat. Supaya itu tidak lagi jadi tebakan, activity ini
sekarang menulis jejaknya ke panel Output dan berkas log:

```
Wait For Download: menjalankan activity pemicu unduhan...
Wait For Download: pemicu selesai, mulai menunggu berkas turun...
Wait For Download: berkas selesai diunduh -> C:\...\challenge.xlsx
```

dan kalau tidak ada yang turun, satu baris peringatan menyebut folder dan lama
menunggunya.

## 2. Tombol "buka folder proyek" membuka Documents

Project.Path menunjuk ProjectsDirectory\{nama}, padahal penyimpanan
folder-per-proyek menaruh isinya satu tingkat lebih dalam:
ProjectsDirectory\offline\{nama} saat tidak tersambung ke OpenCore, atau
ProjectsDirectory\{host}\{nama} saat tersambung. Path yang tidak ada diserahkan
ke Explorer, dan Explorer membuka Documents begitu saja.

Sekarang foldernya dicari lewat FolderOnDisk, yang mencoba berurutan:
Project.Path kalau ada, lalu tebakan offline/{host}, lalu pencocokan nama folder
di dalamnya. Kalau memang belum ada, yang muncul pesan di Output — bukan jendela
Documents yang membingungkan.

## 5. Log tersimpan per tanggal

Panel Output dikosongkan tiap kali flow dijalankan, dan salinannya ditulis ke
%LOCALAPPDATA%\JakForge\Logs\yyyy-MM-dd.txt. Tiap jalan diberi kepala:

```
==============================================================================
=== MULAI 2026-09-06 15:37:01 — New_Workflow (Debug) ===
==============================================================================
```

Penulisannya lewat antrean dan satu thread latar, BUKAN File.AppendAllText
langsung di pemanggil: OpenRPA menulis ribuan baris saat memuat plugin, dan
membuka-menutup berkas seribu kali di thread yang sedang menyiapkan Studio
adalah cara yang sudah terbukti membuat Studio terasa menggantung. Baris
kategori Trace tidak ikut ditulis — isinya teknis dan hanya akan mengubur baris
yang benar-benar berasal dari workflow.

## 6. Wait For Download: output FileInfo, kartu lebih ringkas

DownloadedFile sekarang bertipe OutArgument&lt;FileInfo&gt;, jadi bisa dipakai
hasil.FullName, hasil.Name, hasil.Length, dan seterusnya.

**Perlu diperhatikan:** variabel penampungnya harus diganti tipenya dari String
menjadi System.IO.FileInfo. Workflow lama yang masih menunjuk variabel String
akan ditolak saat divalidasi.

Kartunya juga dipendekkan: lebar minimum 330 menjadi 260, kotak "Do" 60 menjadi
42, dan "Akhiran berkas sementara" pindah ke panel Properties saja karena jarang
diubah.

## 4. Tab HOME / DESIGN / DEBUG di kepala jendela

Tiga tab sebaris di sebelah kiri, persis posisi di UiPath Studio. DESIGN dan
DEBUG membuka pita pada tab yang bersangkutan; menekan tab yang sedang aktif
menutup pitanya lagi.

Baris tab bawaan milik kontrol Ribbon ikut disembunyikan saat pita dibuka lewat
tab kepala — kalau tidak, ada DUA baris tab yang isinya sama. Baris itu
dimunculkan lagi kalau pita dibuka lewat tombol hamburger, karena di sanalah tab
General, Settings, dan Tools bisa dicapai.

Label "Project Name:" dihapus dari kepala jendela: di lebar jendela sekarang ia
mendesak tab DEBUG sampai terpotong, sementara nama proyeknya sudah tampil di
panel Project.

## 7. Fokus kembali ke Studio setelah indicate

SetWindowPos(HWND_TOP) + Activate() saja kerap gagal diam-diam — Windows menolak
proses yang bukan foreground merebut fokus. Sekarang dipakai tiga hal yang
saling menutupi: kedipan TOPMOST ke NOTOPMOST (mengangkat jendela tanpa perlu
izin fokus), AttachThreadInput sesaat ke input queue jendela foreground, lalu
Activate() pada objek Window-nya.

Jendela UI Explorer juga ikut disembunyikan selama pemilihan berlangsung dan
dimunculkan lagi sesudahnya — termasuk saat dibatalkan dengan Esc.

## 8. Tombol "+" menyisipkan di tempat yang salah

IndexOfSpacer naik dari tombol sampai menemukan Panel PERTAMA, lalu menghitung
activity sebelum posisi itu. Panel pertama yang ditemui ternyata Grid milik
template tombolnya sendiri, bukan panel daftar isi Sequence — sehingga
hitungannya SELALU 0 dan setiap "+" menyisipkan di paling atas, di mana pun
tombolnya diklik.

Sekarang naiknya diteruskan sampai WorkflowItemsPresenter dan panel TERAKHIR
sebelum itu yang dipakai. Posisinya pun tidak lagi dihitung dari jumlah anak
panel melainkan dari ModelItem activity pertama yang berada SESUDAH penyisip,
jadi tetap benar walaupun susunan visual WF Designer berubah.

## 9. "Do" milik Open Browser berisi Sequence

Create() sekarang mengisi Body.Handler dengan sebuah Sequence sejak awal. Attach
Browser diberi perlakuan yang sama karena kotak "Do"-nya identik.

## 10. Menu Run/Debug: Run, Debug, Slow Motion

Isinya diganti sesuai permintaan. **Stop dibuang dari menu ini** — tombolnya ada
di tab DEBUG (grup Jalankan) bersama Restart dan Step.

## 11. Kegagalan dilaporkan ke layar dan ke log

Sebelumnya kegagalan hanya mengubah keadaan instance; kalau panel Output sedang
tertutup, workflow terlihat "selesai begitu saja" padahal berhenti di tengah.
Sekarang ReportRunFailure menulis nama activity yang gagal beserta seluruh
rantai InnerException-nya ke Output, dan memunculkan dialog di layar.

Dialognya HANYA untuk jalan lokal dari Studio. Jalan yang dipicu antrean
berjalan tanpa siapa pun di depan layar, jadi dialog di sana hanya akan
menggantung robot sampai ada yang mengklik.

## 12. Kurung dan kutip menutup sendiri

Mengetik ( menghasilkan () dengan kursor di tengah; begitu juga [, {, ", dan '.
Teks yang sedang tersorot DIBUNGKUS, bukan ditimpa. Mengetik penutup tepat di
depan penutup yang sama akan melewatinya saja, dan Backspace di antara pasangan
kosong menghapus keduanya.

Hanya berlaku di dalam kartu activity — kotak pencarian toolbox, Output, dan
Project sengaja dibiarkan apa adanya. Apostrof di tengah kata juga tidak
dipasangkan, supaya mengetik kata berapostrof tidak berubah jadi dua apostrof.

## 14. Checkbox Continue On Error

Editornya menempel pada StringValue milik panel Properties. Begitu properti
berisi InArgument yang Expression-nya kosong, StringValue jatuh ke ToString()
dan yang tampil justru nama tipenya — bukan True, bukan False, dan bukan kosong.

Sekarang kotak centang dan kotak teks sama-sama membaca dan menulis Value, yaitu
InArgument-nya sendiri, lewat dua converter. Isinya boleh tetap berupa ekspresi
VB; yang bukan True/False ditampilkan sebagai centang setengah dan tidak
dipaksakan apa pun sampai diklik.

## Yang BELUM dikerjakan di putaran ini

Lima catatan sengaja tidak disentuh, bukan karena terlewat:

**3. Format penyimpanan proyek ala UiPath** (project.json, Main.xaml, .local,
.objects, .settings, entry-points.json). Ini mengganti format proyek OpenRPA
secara menyeluruh, termasuk pemuatan, penyimpanan, dan kompatibilitas proyek
yang sudah ada. Terlalu besar untuk digabung dengan 12 perubahan lain tanpa
mempertaruhkan yang sudah jalan.

**13. Ctrl+K menghasilkan variabel bertipe salah.** Yang benar: tipe variabel
diambil dari tipe argumen Output activity-nya (mis. Read Range Workbook menjadi
DataTable), dan namanya diminta SESUDAH Ctrl+K ditekan. Ini menyentuh mekanisme
pembuatan variabel di semua activity.

**15 & 16. Kehalusan transisi** splash ke home ke canvas, dan saat masuk mode
indicate.

**17. Menyempurnakan activity database dan terminal beserta tutorialnya.**

## Yang diuji dan yang belum

Diuji: seluruh solusi dibangun tanpa error; Studio dijalankan sampai kanvas; tab
DESIGN dan DEBUG diklik dan pitanya berganti isi dengan benar (ada tangkapan
layarnya); berkas log harian terbentuk dan terisi; urutan eksekusi Wait For
Download diuji lewat harness terpisah.

Belum diuji jalan sungguhan: kurung otomatis, checkbox Continue On Error, tombol
"+" pada posisi tertentu, dialog kegagalan, dan tombol buka folder — semuanya
jalur yang perlu diklik satu per satu di dalam Studio.

---

# Putaran ketujuh: lima catatan sisa (3, 13, 15, 16, 17)

## 3. Tata letak proyek disamakan dengan UiPath

Sebelum:

```
Tesproject\
  NewWorkflow.workflow.json
  NewWorkflow.xaml
  project.rpaproj
```

Sesudah:

```
Tesproject\
  .entities\  .local\  .objects\  .project\
  .screenshots\  .settings\  .templates\  .tmh\
  entry-points.json
  NewWorkflow.xaml
  project.json
```

Yang berubah di `OpenRPA.Storage.ProjectFolders`:

**project.rpaproj menjadi project.json.** Isinya tetap memuat seluruh medan
entity Project milik OpenRPA — yang ditambahkan hanya medan bergaya UiPath di
sebelahnya: `projectId`, `description`, `main`, `schemaVersion`,
`studioVersion`, `expressionLanguage`, `targetFramework`, `projectVersion`.
Medan lama TIDAK ada yang ditimpa: berkas yang sama dibaca balik menjadi entity
Project, dan medan yang berubah bentuk akan menggagalkan pemuatan proyek.

**Metadata internal pindah ke `.project\`.** Berkas `*.workflow.json`,
`*.detector.json`, dan `*.queue.json` tidak lagi di akar, sehingga akar proyek
hanya berisi yang memang dilihat orang. Berkas `.xaml` tetap di akar — itu yang
dibuka dan di-diff.

**entry-points.json** ditulis ulang setiap proyek atau workflow disimpan.
Sumbernya `Parameters` di metadata workflow, bukan hasil menguraikan XAML —
argumennya sudah tercatat di sana, dan menguraikan XAML selain lambat juga rapuh
terhadap perubahan format.

**Migrasi otomatis dan aman.** Tata letak lama tetap DIBACA: `HasProjectFile`
menerima kedua nama berkas, dan `MetadataFilesOf` memindai `.project\` maupun
akar. Begitu proyek disimpan, berkas versi lamanya dihapus oleh
`RemoveSupersededCopy` — tanpa itu satu objek punya dua berkas dan keduanya
terindeks, sehingga workflow yang sama muncul dua kali di panel.

Diuji: proyek Tesproject asli dimigrasi, Studio ditutup dan dibuka lagi,
proyeknya termuat utuh beserta workflow dan seluruh activity di dalamnya.

## 13. Ctrl+K: tipe variabel yang benar, nama ditanyakan sesudahnya

Kode lamanya:

```csharp
string Variablename = Text;
if (expressionType == null) { expressionType = typeof(string); }
```

Dua masalah sekaligus. `expressionType` diberikan WF dari properti
`ExpressionType` milik ExpressionTextBox — dan designer kita tidak pernah
menyetelnya di XAML, jadi nilainya SELALU null dan setiap variabel jadi String.
Itu persis yang terlihat: kotak keluaran Read Range Workbook menghasilkan
variabel String, bukan DataTable.

Sekarang tipenya disimpulkan berlapis di `ResolveVariableType`:

1. `expressionType` dari WF, kalau ada isinya;
2. kalau tidak, dibaca dari **binding kotaknya**: semua designer kita memakai
   bentuk `Path=ModelItem.NamaProperti`, jadi nama propertinya diambil dari
   potongan terakhir path lalu dicari di tipe activity-nya lewat refleksi —
   `OutArgument(Of DataTable)` menjadi `DataTable`;
3. kalau tetap tidak ketemu, `ExpressionType` yang disetel designer;
4. String hanya kalau benar-benar tidak ada yang bisa disimpulkan.

Pembungkus WF dilepas oleh `Unwrap`: `InArgument(Of T)`, `OutArgument(Of T)`,
`InOutArgument(Of T)`, `Location(Of T)`, dan `Activity(Of T)` semuanya
membungkus T yang sesungguhnya.

Urutannya juga dibalik. Ctrl+K sekarang membuka kotak kecil yang menanyakan
nama, sekalian menampilkan tipe yang akan dipakai, lalu namanya diisikan ke
kotak ekspresi. Sebelumnya nama harus diketik lebih dulu, dan kotak yang kosong
menghasilkan variabel tanpa nama.

Diperiksa lewat refleksi terhadap binari yang sudah dibangun:
`ReadRangeWorkbook.DataTable` bertipe `OutArgument[System.Data.DataTable]` dan
`WaitForDownload.DownloadedFile` bertipe `OutArgument[System.IO.FileInfo]` —
keduanya bentuk yang ditangani `Unwrap`, dan binding di designer-nya memang
`Path=ModelItem.DataTable` dengan `OwnerActivity={Binding Path=ModelItem}`.

## 15. Transisi splash, Home, dan kanvas

Layar Loading sudah memudar keluar selama 260 ms, tapi layar Home muncul
seketika pada opasitas penuh — jadi yang terlihat kedipan, bukan perpindahan.

- `ShowHome` sekarang memakai `FadeIn` 200 ms. Durasinya sengaja lebih pendek
  daripada pudar keluarnya supaya keduanya bertindih dan tidak ada jeda kosong.
- `ShowCanvas` menyembunyikan layar Home pada dispatcher berikutnya, setelah
  kanvas benar-benar tergambar. Sebelumnya Home disembunyikan lebih dulu, dan
  layar sempat kosong di antaranya.
- Kalau animasinya gagal, opasitasnya disetel langsung dan jendelanya tetap
  ditampilkan — animasi yang gagal tidak boleh meninggalkan pemakainya tanpa
  jendela sama sekali.

## 16. Transisi masuk mode indicate

Overlay picker dulu muncul seketika menutupi seluruh layar. Sekarang opasitasnya
mulai dari 0 dan memudar masuk 140 ms lewat `FadeTo`. Sama seperti di atas,
kegagalan animasi menyetel opasitasnya langsung — overlay yang tidak pernah
tampil sepenuhnya masih bisa dipakai, tapi overlay yang tidak pernah menutup
akan mengunci seluruh layar.

## 17. Activity database dan terminal

**Database Scope** dan **Terminal Session** sekarang punya kotak "Do" yang sudah
berisi Sequence sejak diseret dari toolbox. Satu koneksi database maupun satu
sesi terminal hampir selalu dipakai untuk lebih dari satu perintah, jadi
memaksa user membongkar isinya dulu untuk menyisipkan Sequence sendiri tidak
masuk akal.

**Terminal Session** juga baru sekarang mengimplementasikan
`IActivityTemplateFactory`. Sebelumnya Body dibiarkan null saat diseret, jadi
kotak Do tidak punya argumen delegate dan tidak ada nama yang bisa dipakai untuk
menunjuk sesinya dari dalam blok. Argumennya dinamai `session`, sama dengan yang
sudah disebut di deskripsi properti Session milik activity terminal lainnya.

Tutorialnya ada di berkas terpisah: **TUTORIAL-DATABASE-TERMINAL.md**. Isinya
apa yang perlu disiapkan (penyedia data, connection string, driver ODBC 32/64
bit, alamat host, model layar 3270), langkah pemakaian, contoh workflow utuh,
tabel pesan gagal beserta sebabnya, dan kebiasaan yang membuat workflow terminal
tahan lama.

## Sekalian: pewarnaan panel Properties yang selama ini gagal diam-diam

Berkas log harian yang baru langsung memperlihatkan satu cacat lama:

```
[Error] JakForge property inspector: System.InvalidCastException:
        Unable to cast object of type 'System.Windows.ResourceDictionary'
        to type 'System.Collections.Hashtable'.
```

`PropertyInspectorFontAndColorData` ternyata menuntut XAML sebuah
`System.Collections.Hashtable`, bukan `ResourceDictionary` — bentuk yang paling
wajar dikira. Karena kegagalannya ditangkap dan hanya dicatat, panel Properties
tidak pernah benar-benar ikut bertema sejak putaran keempat, dan tidak ada yang
menyadarinya. Sekarang yang diserialkan Hashtable, dan panel Properties tampil
dengan warna JakForge — terlihat di tangkapan layar, sekaligus tidak ada lagi
baris Error itu di log.

## Yang diuji

| Yang diuji | Cara | Hasil |
|---|---|---|
| Seluruh solusi dibangun | Rebuild penuh | 0 error, 0 warning baru |
| Migrasi tata letak proyek | Simpan proyek asli, periksa foldernya | Sesuai gambar3 |
| Proyek tetap termuat | Studio ditutup dan dibuka lagi | Proyek, workflow, dan activity utuh |
| Tipe Ctrl+K | Refleksi terhadap binari | OutArgument(Of T) terbaca benar |
| Panel Properties bertema | Tangkapan layar + log | Bertema, tanpa Error |
| Continue On Error | Tangkapan layar | Checkbox tri-state, kotak teks kosong |
| Studio tidak jatuh | Jalankan, buka proyek, periksa log | Tidak ada crash, tidak ada Error baru |

Belum diuji dengan sistem sungguhan: activity Database (butuh server database)
dan Terminal (butuh host TN3270). Yang diperbaiki di keduanya bersifat struktur
activity, bukan logika koneksinya — logika itu tidak disentuh.

Cadangan folder proyek sebelum migrasi ada di
`Documents\OpenRPA\offline_backup_2026-09-06`.

---

# Putaran kedelapan: lima catatan Studio, JakRunner, dan ForgeHub

## 1. Transisi yang masih tersendat

Penyebabnya bukan durasi animasinya, melainkan APA yang dianimasikan.

`JakForgeFadeInContent` dulu menganimasikan opasitas ISI jendela utama. WPF
harus menggambar ulang seluruh pohon visual — pita, AvalonDock, dan kanvas WF
Designer — enam puluh kali per detik untuk itu. Sekarang yang dipudarkan
selembar tirai polos yang ditaruh di atasnya: satu bentuk, digabung di kartu
grafis, biayanya nyaris nol.

`FadeIn` untuk jendela Home juga dipindah dari `Window.Opacity` ke opasitas isi
jendelanya. Window.Opacity pada jendela biasa dikerjakan Windows lewat
`SetLayeredWindowAttributes` — per bingkai, di CPU, untuk seluruh luas jendela.

Pudar keluar layar Loading dipercepat 260 ke 180 ms supaya bertindih rapi
dengan pudar masuk Home yang 200 ms.

## 2. Tab General dibongkar

Tab itu dihapus dan isinya disebar sesuai kategorinya:

| Dari grup | Ke |
|---|---|
| Open, Import, Export, Reload | Design, grup **Proyek** |
| Copy, Delete, Permissions | Design, grup **Kelola** |
| Kotak pencarian | Design, grup **Search** |
| Child Session, Play in Child | Debug, grup **Sesi Anak** |
| Visual Tracking | Debug, grup **Penelusuran** |

New dan Save sudah ada di grup Berkas milik Design, Play dan Stop sudah ada
sebagai Debug File dan Stop, jadi keduanya tidak dipindah — hanya akan jadi
tombol kembar.

Quick Launch (`Ctrl+`) dulu memilih `tabGeneral` yang kini tidak ada. Sekarang
ia membuka pita di tab Design, tempat kotak pencariannya berada.

## 3. Ukuran kartu activity

Diseragamkan lewat satu lintasan terskrip atas **84 berkas designer**, bukan
disunting satu per satu. Aturannya dijalankan dari sumber terkecil ke terbesar
supaya nilai hasil tidak kena aturan berikutnya:

| | Sebelum | Sesudah |
|---|---|---|
| Lebar minimum | 120–300 | 105–245 (turun ~18%) |
| Ukuran huruf | 11, 12, 13, 14 | 10, 11, 12, 12 |
| Padding kepala | 10,8 / 10,7 / 10,6 | 8,5 / 8,5 / 8,4 |
| Ikon | 16×16 | 14×14 |
| Kotak jatuh | 30–60 | 26–44 |

Penyisip "+" di Sequence ikut mengecil: tinggi barisnya 34 ke 26, tombolnya 22
ke 18, dan panahnya dipendekkan.

## 4. Template ReFramework

XAML-nya TIDAK ditulis sebagai teks. Pohon activity-nya dibangun sebagai objek
C# lalu diserialkan WF sendiri lewat `ActivityXamlServices.CreateBuilderWriter`.
XAML workflow yang ditulis tangan gampang salah satu tanda, dan salahnya baru
ketahuan saat berkasnya dibuka — sebagai kotak merah di kanvas, bukan sebagai
kesalahan yang bisa dibaca.

Yang dihasilkan tujuh workflow: **Main** (state machine dengan Initialization,
Get Transaction Data, Process Transaction, End Process), **InitAllSettings**,
**InitAllApplications**, **GetTransactionData**, **Process**,
**SetTransactionStatus**, **CloseAllApplications** — plus `Data\Config.json`
dan sebuah README.

Bedanya dengan ReFramework UiPath: setelan memakai JSON, bukan Excel. Isinya
sama, tapi tidak menuntut Excel terpasang, bisa di-diff apa adanya, dan dibaca
cukup dengan satu Assign tanpa activity tambahan.

`BusinessRuleException` ditambahkan ke OpenRPA.Interfaces. Bedanya dengan
Exception biasa menentukan apa yang terjadi sesudahnya: yang satu melewati
transaksi dan lanjut, yang lain kembali ke Initialization dan mencoba lagi.

Galeri templatenya menggantikan pesan "belum ada" di menu Template layar Home.

## 5. Nama proyek

Dipindah dari deretan tombol kepala jendela ke kepala panel Project. Di lebar
jendela biasa ia berdesakan di antara tab DEBUG dan tombol Run/Debug, dan
terpotong jadi "Tesprojec". Di panel Project ruangnya lega dan letaknya tepat di
atas isi proyeknya. Bindingnya dipasang dari kode karena DataContext panel itu
bukan MainWindow.

---

# Todo Task 1: JakRunner dipisahkan

Asisten dulu hanyalah jendela lain di dalam Studio, dipilih lewat tanda
`isagent` di setting.json. Menjalankan asisten berarti memuat seluruh Studio —
pita, kanvas, WF Designer — untuk sesuatu yang cuma perlu menekan tombol Play.

**JakRunner.exe** sekarang aplikasi tersendiri yang benar-benar berdiri
sendiri: ia tidak memuat Studio, tidak memakai RobotInstance, dan tidak butuh
Studio terpasang.

- Daftar automasi dibaca LANGSUNG dari folder proyek. Tata letak JakForge sudah
  menjelaskan dirinya sendiri: satu folder per proyek, `.xaml` di akarnya,
  `project.json` sebagai penandanya.
- Menjalankannya memakai runtime WF miliknya sendiri:
  `ActivityXamlServices.Load` lalu `WorkflowApplication.Run`.
- Studio kini IDE murni. Argumen `--assistant` tetap dikenali supaya pintasan
  lama tidak buntu; yang dilakukannya menjalankan JakRunner lalu menutup Studio.

UI-nya sesuai spesifikasi: widget ringkas 380px dengan kepala, pencarian, kartu
Running Tasks berikut Pause/Stop, dan daftar Ready to Run; tampilan melebar
menambah Log Aktivitas Terbaru, Daftar Kesalahan, Parameter Input/Output, dan
Parameter Kinerja Rinci.

Dua hal yang saya sebut apa adanya di dalam produknya sendiri:

- **Bilah kemajuan itu penanda "masih hidup", bukan ukuran seberapa jauh.** WF
  tidak melaporkan kemajuan — tidak ada yang bisa dilaporkan, karena panjang
  sebuah workflow tidak diketahui sebelum dijalankan. Itu tertulis di tooltip-nya.
- **Jeda belum didukung runtime.** `WorkflowApplication` tidak punya jeda
  sungguhan tanpa penyimpanan sementara. Tombolnya menghentikan, dan mengatakan
  itu di log — bukan berpura-pura menjeda.

Diuji: JakRunner menjalankan `CloseAllApplications` dari proyek ReFramework
sampai selesai, dengan Studio TIDAK berjalan sama sekali.

---

# Todo Task 2: ForgeHub

**Perlu disebut lebih dulu:** mesin ini tidak punya Java, Maven, Node, npm,
Docker, maupun PostgreSQL — semuanya diperiksa dan tidak ada satu pun. Jadi
berbeda dengan Studio dan JakRunner yang dibangun dan diuji langsung, ForgeHub
ditulis dengan hati-hati tapi **belum pernah dikompilasi**. Itu tertulis juga di
paling atas README-nya, beserta empat hal yang paling mungkin perlu dibetulkan
pada percobaan pertama.

**Backend** — Spring Boot 3.5 / Java 25 / Maven, PostgreSQL, Flyway, JWT,
BCrypt kekuatan 12. Tabel: tenants, users, machines, environments, robots,
credentials, packages, processes, jobs, triggers, queues, queue_items, assets,
logs. API: auth, dashboard, robots (termasuk heartbeat), processes, jobs,
queues, assets, dan penerimaan log per bundel.

**Frontend** — Next.js 15, TypeScript, Tailwind, React Query. Sidebar biru tua,
isi krem `#F5F4F0`, kartu putih. Kartu ringkasan, tabel Job Management, tabel
Robot & Environment, panel Triggers, dan Real-Time Logs bergaya terminal.

**Docker** — compose tiga layanan dengan healthcheck: backend menunggu sampai
basis datanya SIAP MENERIMA, bukan sekadar sampai containernya hidup.

Tiga keputusan yang saya sebutkan alasannya di kodenya:

1. **tenant_id di setiap tabel dan di awal setiap indeks.** Pemisahan yang
   hanya diperiksa di lapisan aplikasi akan bocor pada kueri pertama yang lupa
   menyaringnya — dan bocornya baru ketahuan saat pelanggan melihat data
   pelanggan lain.
2. **Tenant dibaca dari token, tidak pernah dari badan permintaan.** Nilai yang
   datang dari klien tidak boleh menentukan data siapa yang terlihat.
3. **Log ditarik dengan `afterId`.** Menarik dua ratus baris terakhir tiap dua
   detik juga bekerja, tapi ia mengunduh hal yang sama berulang-ulang dan
   membuat panelnya berkedip.

Yang belum: formulir untuk Environments, Credentials, Packages, Libraries,
Tenants, dan Settings. Tabelnya sudah ada di skema; endpoint dan formulirnya
belum. Layarnya mengatakan itu apa adanya, bukan memajang tabel kosong yang
tampak rusak.

---

## Yang diuji dan yang belum

| Bagian | Cara | Hasil |
|---|---|---|
| Studio dibangun | Build penuh | 0 error |
| Tab Design/Debug sesudah General dibuang | Tangkapan layar | Proyek, Kelola, Search muncul |
| Kartu activity mengecil | Tangkapan layar | Terlihat lebih rapat |
| Nama proyek pindah | Tangkapan layar | Di kepala panel Project |
| ReFramework: XAML sah | `ActivityXamlServices.Load` atas 7 berkas | 7 OK, 0 gagal |
| ReFramework: dari galeri | Buat proyek, buka Main | State machine tampil di kanvas |
| JakRunner dibangun | Build | 0 error |
| JakRunner menjalankan workflow | Klik Play, Studio dimatikan | Selesai tanpa kesalahan |
| JakRunner tampilan melebar | Tangkapan layar | Empat panel terisi |
| Studio tidak jatuh | Jalankan, periksa log | Tidak ada crash, tidak ada Error baru |
| **ForgeHub** | **tidak ada** | **belum pernah dibangun — tidak ada toolchain-nya di mesin ini** |

---

# Ronde: ForgeHub yang benar-benar berjalan

Ronde sebelumnya menutup dengan kalimat "ForgeHub belum pernah dibangun —
tidak ada toolchain-nya di mesin ini". Kalimat itu ternyata separuh benar, dan
separuh yang salah itu penting: yang tidak ada memang Java, Maven, Node, npm,
dan Docker — tapi **.NET SDK 10 ada, ASP.NET Core 10 ada, dan PostgreSQL 18
sedang berjalan.** Yang hilang bukan kemampuan menjalankan server, melainkan
kemampuan menjalankan server *Java*.

Jadi ForgeHub ditulis ulang di atas tumpukan yang memang ada di mesin ini.

## `ForgeHub/server/` — ASP.NET Core 10 + SQLite

Satu proyek, tanpa langkah pembangunan untuk sisi peramban, tanpa layanan yang
perlu dipasang lebih dulu. Klik `start-forgehub.cmd`, buka `localhost:8080`,
masuk dengan `FH_Admin` / `forgehub`. Itu saja.

**SQLite, bukan PostgreSQL yang sudah berjalan itu.** PostgreSQL memang ada,
tapi kata sandinya milik Anda dan saya tidak punya — memakainya berarti
menghentikan pekerjaan untuk bertanya. SQLite tidak butuh kata sandi, tidak
butuh layanan, dan berkasnya bisa disalin atau dihapus begitu saja. Bentuk
tabelnya sengaja sama persis dengan skema PostgreSQL di `backend/`: setiap
tabel penyewa punya `tenant_id`, dan setiap indeks diawali kolom itu. Pindah ke
PostgreSQL nanti tidak mengubah Studio maupun JakRunner.

**Token ditandatangani sendiri, bukan lewat pustaka JWT.** Yang dibutuhkan
hanya satu algoritma — HS256, menandatangani dan memeriksa. Pustaka JWT umum
membawa serta penguraian banyak algoritma lain, termasuk "none", dan bagian
itulah yang paling sering menjadi lubang. Kuncinya dibuat acak saat pertama
dijalankan dan disimpan di `%LOCALAPPDATA%\JakForge\ForgeHub\signing.key`;
kunci yang ditanam di kode akan sama di semua pemasangan, dan token buatan satu
orang akan berlaku di ForgeHub milik orang lain.

**Kata sandi pengguna: PBKDF2-SHA256, 210.000 putaran, dibandingkan dengan
`FixedTimeEquals`.** Nilai yang harus bisa dibaca kembali — kredensial dan aset
rahasia — memakai AES-GCM dengan kunci yang diturunkan HKDF dari berkas kunci
yang sama, label berbeda. Satu berkas rahasia untuk dijaga, dua kunci yang
tidak saling membocorkan.

**Status robot dihitung saat dibaca, tidak dibaca mentah dari kolom.** Robot
yang mati mendadak tidak sempat melaporkan apa pun, jadi kolom statusnya
selamanya berbunyi "AVAILABLE" kalau dipercaya begitu saja. Yang dipakai adalah
selisih waktu terhadap denyut terakhir: lewat 45 detik — dua denyut yang
hilang — ia terputus.

**Pengambilan pekerjaan dan butir antrean dilakukan dalam satu perintah UPDATE
yang tak terbagi.** Kalau baca-lalu-tulis dipisah, dua robot yang bertanya
bersamaan akan sama-sama melihat pekerjaan yang sama sebagai menunggu, dan
menjalankannya dua kali.

**Penjadwal juga menutup pekerjaan yang robotnya menghilang.** Tanpa itu,
pekerjaan dari robot yang mati berstatus "berjalan" selamanya, dan kartu
"pekerjaan berjalan" di dasbor terus menghitungnya.

**Isi awal basis data hanya kerangka** — penyewa, pengguna pertama, peran,
lingkungan, mesin ini sendiri, satu antrean. Robot, proses, pekerjaan, dan log
TIDAK dipalsukan. Dasbor berisi data karangan terlihat meyakinkan pada menit
pertama, lalu menyesatkan pada menit kedua.

## Dasbornya

Satu berkas HTML, satu CSS, satu JS — tanpa kerangka kerja dan tanpa langkah
pembangunan, karena ForgeHub harus bisa dijalankan di komputer yang belum tentu
punya Node. Paletnya sama dengan Studio dan JakRunner.

Isinya mengikuti gambar1: pemilih penyewa, pencarian menyeluruh, ikon surat dan
lonceng dengan penghitung, identitas pengguna; menu samping berkelompok
(Automasi, Pemantauan, Robot, Data, Administrasi); kartu ringkasan dengan angka
rincian di bawahnya; tabel Pekerjaan Berjalan dengan bilah kemajuan; tabel
Robot Aktif; jadwal berikutnya; umpan peringatan; ringkasan antrean; dan grafik
14 hari. Halaman penuh untuk Proses, Paket, Pemicu, Pekerjaan, Log Langsung,
Peringatan, Robot, Mesin, Lingkungan, Kredensial, Antrean, Aset, Gudang Berkas,
Pengguna, Peran, Lisensi, dan Pengaturan.

Log ditarik dengan `afterId`, bukan dengan stempel waktu: jam robot yang meleset
beberapa detik membuat baris baru tampak lebih tua daripada yang sudah tampil,
lalu tidak pernah muncul. Barisnya juga hanya DITAMBAHKAN, tidak digambar ulang
— kalau tidak, guliran melompat ke atas tiap beberapa detik.

## Studio ke ForgeHub

Tab **Design** punya grup **ForgeHub**: Terbitkan, Sambungkan, Buka Dasbor.
Terbitkan mengemas folder proyek jadi zip (melewati `.git`, `bin`, `obj`,
`node_modules`), mengirimnya sebagai base64, dan ForgeHub membuat prosesnya
sekaligus. Kata sandi disimpan tersandi DPAPI di
`%LOCALAPPDATA%\JakForge\forgehub.json`.

Grup itu diberi **satu** `RibbonGroupSizeDefinition`. Tanpa itu, Ribbon
menyusun sendiri beberapa varian ukuran dan grup ini termasuk yang paling awal
diciutkan menjadi satu tombol panah — tombol Terbitkan tersembunyi meski
layarnya lebar.

## JakRunner ke ForgeHub

JakRunner menanyakan pekerjaan tiap 10 detik. Penjemputan, bukan pendorongan:
JakRunner berjalan di komputer meja di balik NAT, dan ForgeHub tidak punya
jalan untuk menghubunginya lebih dulu.

**Dan sekarang ia bisa menjalankan ReFramework.** Sebelumnya tidak: Main.xaml
memanggil sub-workflow lewat `OpenRPA.Activities.InvokeOpenRPA`, yang hidup di
dalam Studio, dan JakRunner gagal memuatnya dengan "Cannot create unknown type".
Template unggulan Studio justru menjadi satu-satunya yang tidak bisa dijalankan
asistennya.

Perbaikannya bukan menyunting teks XAML sebelum dibaca — prefiks namespace bisa
apa saja dan atributnya bisa berpindah baris, jadi tebakan yang meleset merusak
berkas yang sebetulnya sah. Yang dipakai adalah `XamlSchemaContext` yang
memetakan nama tipe itu ke `JakRunner.Core.InvokeWorkflow`: bentuk properti yang
sama, tapi Execute-nya memuat berkas yang ditunjuk dan menjalankannya dengan
runtime WF biasa, mengoper variabel masuk-keluar berdasarkan kesamaan nama —
persis cara ReFramework mengoper Config, TransactionItem, dan SystemException
antar berkas.

## Dua cacat yang ditemukan sambil jalan

**Empat grup Debug tertinggal di tab Design.** Penataan ronde lalu memindahkan
Cancel key, Tesseract, Runtime, dan Logging "ke Debug" — tapi potongannya
mendarat sebelum `</RibbonTab>` milik Design. Akibatnya Design memuat 17 grup
dan Ribbon menciutkan sebagiannya jadi tombol panah. Sekarang keempatnya benar
di Debug, dan Change Type ikut pindah ke sana: ia menukar CARA activity bekerja
saat dijalankan, jadi tempatnya memang di Debug.

**Kegagalan memuat tidak pernah dilaporkan ke ForgeHub.** Langganan perubahan
keadaan dipasang SESUDAH `_engine.Start()`, padahal kegagalan memuat berkas
terjadi di dalam Start() secara langsung — perubahan ke keadaan "gagal" sudah
lewat sebelum ada yang mendengarkan, dan pekerjaannya berstatus "berjalan"
selamanya di dasbor. Ketiga tempat yang memulai automasi kini lewat satu
`BeginRun()` yang berlangganan lebih dulu.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| ForgeHub dibangun | `dotnet build` | 0 error, 0 warning |
| Masuk dan token | curl: sandi benar, sandi salah, tanpa token | 200 / 401 / 401 |
| Denyut robot | curl, robot belum dikenal | terdaftar sendiri, CPU dan memori masuk |
| Pengambilan pekerjaan | curl dua kali berturut-turut | yang kedua null — tidak dobel |
| Antrean tolak kembar | curl referensi sama dua kali | 409 |
| Dasbor | peramban, data sungguhan | kartu, tabel, peringatan, grafik terisi |
| Studio dibangun | Build penuh solusi | 0 error |
| Grup ForgeHub di pita | Tangkapan layar | Terbitkan / Sambungkan / Buka Dasbor terlihat |
| Studio menerbitkan | Klik Terbitkan | "ReFramework 1.0.0 diterbitkan (12 KB)" |
| Paket sampai | curl `/api/packages` | ReFramework 1.0.0, 13.131 bita |
| JakRunner menyambung | jakrunner.json, jalankan | terdaftar, denyut tiap 15 detik |
| **Rantai penuh** | **ForgeHub menjadwalkan, JakRunner mengerjakan, hasil kembali** | **SUCCESSFUL** |
| ReFramework di JakRunner | Main.xaml dengan InvokeOpenRPA | dimuat dan jalan |
| Proses tak ada di mesin | Jadwalkan proses yang tidak ada | FAULTED dengan sebab yang jelas |
| Data bertahan | Matikan-hidupkan server | proses, robot, paket utuh |

## Yang BELUM selesai

**Catatan 7 baru sebagian.** Yang dikerjakan: penataan ulang pita, tombol
Terbitkan yang tidak lagi tersembunyi, dan ReFramework yang kini bisa dijalankan
JakRunner. Yang belum: pemeriksaan menyeluruh satu per satu atas SELURUH
activity di kotak alat — sebagian besar berasal dari OpenRPA hulu dan belum
pernah saya jalankan sendiri di sini.

`backend/` dan `frontend/` (Java dan Next.js) tetap belum pernah dikompilasi.
Keduanya kini tercatat di README sebagai jalur penyebaran banyak mesin, bukan
sebagai yang dipakai sekarang.

---

# Ronde: activity JakForge sendiri, antrean, dan log robot

## `Custom.Orchestrator` — kategori toolbox baru

Proyek activity baru yang menghubungkan workflow ke ForgeHub. Ia SENGAJA tidak
merujuk OpenRPA maupun OpenRPA.Interfaces: activity di dalamnya harus bisa
berjalan di Studio DAN di JakRunner, dan JakRunner dirancang berdiri sendiri
tanpa menyeret seluruh Studio.

Isinya tujuh activity: **Invoke Workflow**, **Add Queue Item**, **Get Queue
Item**, **Set Queue Item Status**, **Get Asset**, **Get Credential**, dan
**Write Robot Log**.

## Invoke Workflow menggantikan Invoke OpenRPA

Yang lama hidup di dalam Studio — Execute-nya mencari `WorkflowInstance.Instances`
dan `RobotInstance` — sehingga hanya bisa berjalan kalau Studio yang
menjalankannya. Akibatnya template ReFramework, yang seluruhnya dirangkai dari
sub-workflow, tidak bisa dijalankan JakRunner sama sekali.

Yang baru berdiri di atas runtime WF biasa. Ia juga menyimpan teks XAML yang
sudah pernah dibaca: ReFramework memanggil `Process.xaml` sekali per transaksi,
dan tanpa penyimpanan itu seribu transaksi berarti seribu kali membaca berkas
yang sama dari cakram.

Nama properti lama (`workflow`, `WaitForCompleted`, `KillIfRunning`) tetap
diterima sebagai alias tersembunyi, dan `RunnerSchemaContext` memetakan nama
TIPE lama ke yang baru. Proyek yang sudah dibuat sebelum penggantian tetap
bisa dimuat — mengganti nama tidak boleh merusak pekerjaan orang.

## Antrean bekerja penuh

`GetTransactionData.xaml` sekarang mengambil dari antrean ForgeHub kalau
`Config("OrchestratorQueueName")` terisi, dan `SetTransactionStatus.xaml`
melaporkan hasilnya di ketiga cabangnya:

- **System Exception → FAILED.** ForgeHub mengembalikan butirnya ke antrean
  selama jatah percobaannya belum habis; kegagalan sementara tidak
  menghilangkan transaksi.
- **Business Exception → ABANDONED.** Data yang memang salah tidak akan menjadi
  benar kalau dicoba lagi, dan percobaan ulang hanya menunda pekerjaan berikutnya.
- **Berhasil → SUCCESSFUL.**

`TransactionItem` kini bertipe `QueueItem`, bukan `Object`. Isinya dibaca lewat
`TransactionItem.Get("nama")` sehingga workflow tidak perlu tahu apa pun
tentang JSON.

Diuji sungguhan: enam butir dimasukkan ke antrean, pekerjaan dijadwalkan dari
ForgeHub, dan keenamnya keluar dengan `successfulCount: 6, newCount: 0`.

## JakRunner menjalankan per PROYEK

Sebelumnya daftarnya berisi satu baris per berkas .xaml, dan ReFramework muncul
sebagai tujuh baris terpisah — padahal enam di antaranya sub-workflow yang tidak
berarti apa-apa kalau dijalankan sendiri. Menjalankan `InitAllSettings` tanpa
`Main` hanya membaca berkas setelan lalu berhenti.

Sekarang satu baris per proyek, dan yang dijalankan adalah titik masuknya:
`main` dari project.json, atau `Main.xaml`, atau — kalau proyeknya cuma punya
satu berkas — berkas itu. Proyek dengan banyak berkas tapi tanpa titik masuk
yang jelas TIDAK ditebak: menjalankan berkas pertama menurut abjad adalah cara
tercepat membuat robot mengerjakan hal yang salah.

## Log robot secara langsung

`RobotLog` di `Custom.Shared` adalah saluran bersama: activity menulis ke sana,
dan yang menampungnya adalah program yang sedang menjalankannya — JakRunner ke
panel log dan ke ForgeHub, Studio ke panel Output.

`RunnerTracking` menambahkan jejak per-activity lewat TrackingParticipant WF.
Profilnya menyaring di SUMBERNYA, bukan belakangan di `Track()`: menyaring
belakangan tetap membuat runtime menyusun record untuk setiap kejadian, dan itu
pekerjaan yang seluruhnya terbuang. Activity pembungkus buatan runtime
(`VisualBasicValue`, `Literal<>`, dan sejenisnya) dilewati — namanya tidak
berarti apa pun bagi orang yang membaca log.

Setiap baris membawa `jobId`, jadi ForgeHub bisa menampilkan log SATU jalan saja.
Tombol **Log** di baris pekerjaan berjalan membuka halaman Log dengan penyaring
itu terpasang.

## Tata letak kanvas ReFramework

State ditempatkan lewat `WorkflowViewStateService.SetViewState` dengan kunci
`ShapeLocation`. Tanpa itu, perancang menumpuk semuanya di sudut yang sama dan
label transisi saling menimpa — persis yang terlihat di tangkapan layar Anda.

`End Process` digeser ke kanan supaya tiga transisi yang menuju ke sana tidak
berdesakan di lorong yang sama.

Dua hal yang TIDAK berhasil dan sebabnya dicatat di kode:

- **`VirtualizedContainerService.SetHintSize` pada State membuat perancang tidak
  menggambar satu pun kotak** — kanvas hanya menyisakan bulatan Start. Dibuang.
- **`ShapeSize` ditulis (300×120) tapi lebar kotak tetap ±175 px.** Perancang
  StateMachine bawaan WF memakai lebarnya sendiri. Karena itu nama state
  dipendekkan agar muat penuh — Inisialisasi, Ambil, Proses, Selesai — dan nama
  ReFramework yang baku (Initialization, Get Transaction Data, …) dipindah ke
  anotasi, tempat ia tetap terbaca lengkap.

## Empat cacat yang ditemukan sambil menguji

**Config.json dicari di tempat yang salah.** `InitAllSettings` memakai
`Assembly.GetEntryAssembly().Location`, yang menunjuk folder OpenRPA.exe atau
JakRunner.exe — tempat yang tidak pernah memuat `Data\Config.json`. Setiap
proyek ReFramework gagal pada langkah pertama. Sekarang memakai
`WorkflowContext.ProjectFolder`, yang disetel Studio dan JakRunner sebelum
workflow dimulai.

**JakRunner tidak bisa memuat workflow yang punya anotasi.** Namespace XML
anotasi berbentuk URI, dan namespace seperti itu hanya bisa dipetakan dari
rakitan yang SUDAH ADA di AppDomain — tidak seperti `clr-namespace:…;assembly=…`,
ia tidak menyebutkan rakitan mana yang harus dimuat. Studio memuat
System.Activities.Presentation karena menggambar kanvas; JakRunner tidak pernah
menyentuhnya. Seluruh template ReFramework gagal dimuat. Diperbaiki dengan
menyentuh satu tipe di dalamnya sebelum membaca berkas.

**Kegagalan dilaporkan sebagai keberhasilan.** Perpindahan keadaan ke *Running*
juga memicu pemberitahuan yang sama dengan keadaan akhir, sehingga jalan yang
baru DIMULAI langsung dilaporkan ke ForgeHub sebagai "berhasil" — dan kegagalan
yang menyusul beberapa milidetik kemudian tidak pernah terlihat di dasbor.
Sekarang hanya keadaan akhir yang dihitung.

**Satu proyek muncul dua kali.** Folder yang sama terlihat dari beberapa akar
sekaligus, dan penyaringan kembarnya memakai jalur folder. Sekarang memakai nama
proyek.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Custom.Orchestrator | Build | 0 error |
| Solusi penuh | Build | 0 error, 0 kunci berkas |
| ForgeHub | dotnet build | 0 error, 0 warning |
| Template dihasilkan | 7 XAML lewat ActivityXamlServices.Load | 7 sah |
| InvokeOpenRPA di Main.xaml | hitung | 0 — sudah tergantikan |
| ShapeLocation tertulis | hitung | 4 state |
| GetQueueItem / SetQueueItemStatus | hitung di berkas | 1 dan 3 |
| Kanvas ReFramework | tangkapan layar | state terpisah, nama utuh, label tidak bertindih |
| JakRunner per proyek | tangkapan layar | 5 proyek, bukan 16 workflow |
| Log per-activity | tangkapan layar panel | langkah demi langkah terlihat |
| **Antrean 6 butir** | **ForgeHub → JakRunner → hasil balik** | **successfulCount 6, newCount 0** |
| Log per pekerjaan | `/api/logs?jobId=…` | hanya baris jalan itu |

## Yang BELUM selesai — catatan 6

Dari 19 activity di `OpenRPA/Activities`, yang punya pengganti JakForge sekarang
**delapan**: InvokeOpenRPA, InvokeRemoteOpenRPA (lewat ForgeHub), ClickElement,
TypeText, HighlightElement, FocusElement, MoveElement, CloseApplication.

**Sebelas belum ada penggantinya** dan sengaja TIDAK ditandai "sudah diganti",
supaya tetap terlihat di toolbox: Break, Continue, CommentOut, CopyClipboard,
InsertClipboard, MoveMouse, ShowBalloonTip, Detector, GetWorkflowInstance,
InvokeOpenFlow, StopOpenRPA. Menandainya sudah diganti padahal penggantinya
belum ada akan menyembunyikan activity yang masih satu-satunya cara melakukan
hal itu.

---

# Ronde: `Custom.Flow` dan tiga cacat yang menyembunyikan diri

Laporan: proyek "cha" gagal di JakRunner dengan *Cannot create unknown type
'ForEachDataRow'*, padahal jalan normal di Studio.

Sebabnya sama persis dengan InvokeOpenRPA: `ForEachDataRow` hidup di dalam
rakitan Studio, dan JakRunner tidak bisa memuat rakitan itu. Ronde sebelumnya
saya menghitung 19 activity OpenRPA; hitungan itu **meleset** — grep saya tidak
menangkap kelas generik dan kelas yang deklarasinya berbeda bentuk. Yang
sebenarnya ada **24**, dan lima yang terlewat justru yang paling sering dipakai:
ForEachDataRow, ForEachOf, BreakableWhile, BreakableDoWhile, OpenApplication.

## `Custom.Flow` — kategori Kontrol Alur

Tujuh activity ditulis ulang, berdiri di atas runtime WF biasa: **For Each Data
Row**, **For Each Of**, **While**, **Do While**, **Break**, **Continue**, dan
**Comment Out**.

Dasarnya `JakForgeLoop`, tulisan ulang dari BreakableLoop milik
OpenRPA.Interfaces. Nama properti konteks `BreakBookmark` dan
`ContinueBookmark` SENGAJA dibuat sama dengan yang lama, sehingga Break JakForge
bekerja di dalam perulangan OpenRPA dan sebaliknya — workflow campuran tetap
jalan selama masa peralihan.

`RunnerSchemaContext` memetakan ketujuh nama tipe lama ke yang baru, termasuk
`ForEachOf` yang generik (nama tipenya di XAML terpisah dari argumen tipenya,
jadi tipe penggantinya dibentuk dengan `MakeGenericType`). Berkas XAML lama
tidak perlu disunting sama sekali.

## Cacat 1: `[DisplayName]` pada properti argumen

Ini yang paling banyak memakan waktu, dan pelajarannya layak dicatat.

Setelah tipenya dikenali, workflow ditolak dengan:

> RuntimeArgument 'DataTable' refers to an Argument which in turn is bound to
> RuntimeArgument named 'DataTable'.

Pesannya bicara tentang ikatan ganda, jadi yang saya curigai `CacheMetadata` —
dan saya mencoba empat pola pendaftaran argumen, semuanya salah sasaran.
Penyebabnya ternyata **`[DisplayName("Data table")]` pada properti `DataTable`**.
Refleksi metadata WF mendaftarkan argumen itu dua kali ketika nama tampilannya
berbeda dari nama propertinya.

Ditemukan dengan membangun replika kelasnya sepotong demi sepotong sampai
replikanya bersih, lalu membandingkan apa yang tersisa. Atribut yang sama ada di
seluruh `Custom.Orchestrator`; semuanya dibuang, dan alasannya ditulis sebagai
`<remarks>` di `JakForgeLoop` supaya tidak terulang.

Judul yang terbaca manusia tetap ada — di kartu canvas, tempat yang memang untuk
itu.

## Cacat 2: jembatan peramban yang gagal tanpa penjelasan

Sesudah itu "cha" berjalan sampai activity **Open Browser**, lalu gagal dengan
`NullReferenceException` — pesan yang tidak menyebut peramban, ekstensi, atau
apa pun yang bisa ditindaklanjuti.

Dua penyebab, keduanya sama bentuknya: `Plugin.client` dan `NMHook` diisi oleh
Studio saat menyala, dan JakRunner tidak pernah mengisinya. Sekarang keduanya
diperiksa lebih dulu, dan yang dilempar menyebutkan apa yang kurang dan apa yang
bisa dilakukan.

**Batasnya tetap ada dan disebut apa adanya:** activity peramban belum bisa
dijalankan dari JakRunner. Menyalakan jembatan native messaging dari JakRunner
adalah pekerjaan tersendiri — bukan penambal satu baris.

JakRunner juga mencatat enam baris teratas jejak tumpukan pada kesalahan tak
tertangani. "Object reference not set to an instance of an object" tanpa jejak
tumpukan tidak memberi tahu apa pun.

## Cacat 3: build yang berhasil tapi tidak menyalin apa-apa

Beberapa kali perbaikan terlihat tidak berpengaruh. Sebabnya bukan kodenya:
**JakRunner yang sedang berjalan mengunci berkas di folder keluaran**, penyalinan
gagal, dan MSBuild tetap melaporkan sukses untuk proyek yang tidak jadi
tersalin. Yang diuji adalah rakitan lama.

Dua hal yang sekarang saya lakukan sebelum menguji: mematikan JakRunner dan
Studio dulu, lalu memeriksa `md5sum` berkas keluaran terhadap hasil build. Kalau
sebuah berkas keluaran terlanjur dihapus, penanda `.CopyComplete` di folder obj
harus ikut dibuang — tanpa itu MSBuild menganggap salinannya masih ada.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Custom.Flow | Build | 0 error |
| Solusi penuh | Build | 0 error, 0 kunci berkas |
| ForgeHub | dotnet build | 0 error, 0 warning |
| Kelas ForEachDataRow | validasi terisolasi | 0 error, perulangannya jalan |
| **Proyek "cha" di JakRunner** | **klik Play** | **dimuat dan berjalan** |
| Batas peramban | activity Open Browser | pesan yang menyebutkan sebab dan jalan keluarnya |
| Studio | jalankan, periksa log | tidak ada `[Error]` baru |

## Yang BELUM selesai

**Sebelas activity OpenRPA masih belum punya pengganti**, dan sengaja tetap
terlihat di toolbox: CopyClipboard, InsertClipboard, MoveMouse, ShowBalloonTip,
Detector, GetWorkflowInstance, InvokeOpenFlow, StopOpenRPA, OpenApplication.

**Activity peramban belum bisa dijalankan dari JakRunner.** Jembatan native
messaging hanya dinyalakan Studio.

---

# Ronde: JakRunner menjalankan automasi peramban

Sasarannya satu: proyek "cha" — yang membuka peramban, mengunduh berkas Excel,
membacanya, lalu mengisi formulir baris demi baris — harus bisa dijalankan
JakRunner tanpa Studio menyala.

## `BrowserBridge`: menyalakan jembatan dari JakRunner

Activity peramban berbicara ke ekstensi Chrome lewat `NMHook`, dan yang
menyalakannya selama ini hanya Studio. JakRunner sekarang menyalakannya sendiri:
menyediakan `IOpenRPAClient` seperlunya, lalu menyambungkan pipanya.

`RunnerClient` sengaja minimal. Dari dua puluh anggota antarmuka itu, yang
benar-benar dibaca saat automasi berjalan hanya dua — `WorkflowInstances`, yang
dipakai membangunkan detektor URL dan unduhan, dan `isRunningInChildSession`.
Sisanya milik perancang: kanvas, jendela, daftar designer. JakRunner tidak punya
satu pun, dan mengembalikan null untuk itu adalah jawaban yang benar, bukan
penambal.

`WorkflowInstances` mengembalikan daftar KOSONG, bukan null: NMHook memutarinya
tiap kali pesan peramban tiba.

## Empat kegagalan berlapis, satu per satu

Yang menarik dari ronde ini: setiap perbaikan membuka kegagalan berikutnya yang
sebelumnya tersembunyi di belakangnya.

**1. Manifest native messaging berisi `REPLACEPATH`.** Registri menunjuk ke
`chromemanifest.json` dengan benar, tapi berkas itu sendiri — yang ikut dibangun
— masih memuat penanda, bukan jalur sungguhan. Chrome karenanya tidak pernah
menjalankan native host-nya. JakRunner sekarang memperbaikinya, tapi HANYA kalau
jalur yang tertulis menunjuk berkas yang tidak ada. Kalau sudah menunjuk berkas
yang ada — pemasangan lain, versi lain — ia dibiarkan; mengarahkannya ke salinan
kita sendiri bisa merusak jembatan yang tadinya bekerja.

**2. Penjagaan "jembatan harus tersambung" yang saya tambahkan sendiri.**
Ronde sebelumnya saya membuat Open Browser menolak berjalan kalau
`NMHook.connected` false. Itu keliru: membuka peramban tidak memerlukan ekstensi
sama sekali — yang memerlukannya hanyalah menemukan tab yang SUDAH terbuka.
Tanpa ekstensi, daftar tabnya kosong, activity meluncurkan peramban seperti
biasa, dan hasilnya benar. Penjagaan itu menolak jalan yang selama ini bekerja
di Studio. Dibuang.

**3. Newtonsoft.Json 13.0.2 versus 13.0.4.** `Custom.StudioBridge` dikompilasi
terhadap 13.0.4, yang punya `JToken.ToString(Formatting)`; yang ter-deploy
13.0.2, yang tidak punya. Hasilnya `MissingMethodException` untuk method yang
"jelas ada". Empat proyek masih menyebut 13.0.2 dan yang menyalin terakhir
menentukan versi di folder keluaran — jadi hasilnya bergantung urutan build.
Semuanya kini 13.0.4.

**4. JakRunner tidak punya berkas config.** Studio punya `OpenRPA.exe.config`
dengan 69 pengalihan versi assembly; JakRunner nol. Begitu Excel dipakai,
`ClosedXML` menarik `SixLabors.Fonts` yang meminta
`System.Numerics.Vectors 4.1.3.0`, dan .NET menolaknya. `JakRunner.exe.config`
sekarang memuat blok pengalihan yang sama persis — disalin dari milik Studio,
karena keduanya memuat rakitan yang sama dari folder yang sama. Dua daftar yang
berbeda hanya menciptakan dua tempat yang bisa saling ketinggalan zaman.

## Cacat build yang menipu berkali-kali

Beberapa perbaikan tampak tidak berpengaruh, dan sebabnya bukan kodenya:

- **JakRunner yang sedang berjalan mengunci berkas di folder keluaran.**
  Penyalinan gagal, MSBuild tetap melapor sukses, dan pengujian berikutnya
  memakai rakitan lama.
- **MSBuild kadang melewati `CoreCompile`** meski berkas sumber lebih baru
  daripada keluarannya. Terjadi berulang pada `Custom.Browser`.
- **Berkas keluaran yang dihapus tidak dipulihkan** sampai penanda
  `.CopyComplete` di folder obj ikut dibuang.

Karena itu ada `rebuild.sh` di folder kerja sesi ini: ia menutup JakRunner dan
Studio dulu, menyentuh berkas sumber yang lebih baru daripada keluarannya,
membangun, lalu MEMBANDINGKAN md5 tiap rakitan dengan hasil build dan menyalin
ulang yang basi. Tanpa langkah terakhir itu, "0 error" tidak berarti apa-apa.

## Sejauh mana "cha" berjalan sekarang

Dari JakRunner, tanpa Studio:

    Open Browser  →  Wait For Download  →  Click 'a'  →  Read Range Workbook
    →  Delete File  →  Click 'button'  →  ForEachDataRow
        →  Type Into Firstname, Last Name, dan lima isian lain
        →  Click 'input'
        →  (baris berikutnya)

Dua baris data terproses penuh. Berhenti di baris ketiga dengan
`Elemen tidak ditemukan: <webctrl ng-reflect-name='labelFirstName' />`.

Itu kegagalan tingkat HALAMAN, bukan tingkat runner: elemennya belum ada di
layar saat activity mencarinya. Jenis kegagalan yang sama bisa terjadi di
Studio, dan penyelesaiannya ada di workflow — menunggu elemen muncul sebelum
mengisinya, bukan di JakRunner.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Solusi penuh | rebuild.sh | 0 error, 0 kunci, semua rakitan mutakhir |
| ForgeHub | dotnet build | 0 error, 0 warning |
| Jembatan peramban | jalankan JakRunner | manifest diperbaiki, pipa tersambung |
| Open Browser | proyek cha | peramban terbuka |
| Wait For Download, Delete File | proyek cha | berjalan |
| Read Range Workbook | proyek cha | berjalan (setelah pengalihan versi) |
| StudioClick, StudioSetText | proyek cha | berjalan (setelah Newtonsoft 13.0.4) |
| ForEachDataRow | proyek cha | dua baris terproses penuh |
| Studio | jalankan, periksa log | tidak ada `[Error]` baru |

## Yang BELUM selesai

**Elemen yang belum muncul saat dicari.** Workflow "cha" berhenti di baris
ketiga. Yang dibutuhkan adalah penantian elemen di workflow-nya — bukan
perbaikan di JakRunner.

**Sembilan activity OpenRPA masih belum punya pengganti**: CopyClipboard,
InsertClipboard, MoveMouse, ShowBalloonTip, Detector, GetWorkflowInstance,
InvokeOpenFlow, StopOpenRPA, OpenApplication.


---

# Ronde: jeda 20 detik, hasil yang berbeda, dan berkas solusi yang rusak

## Berkas solusi rusak — dan itu menyembunyikan segalanya

Temuan terpenting ronde ini, dan kesalahan saya sendiri: `OpenRPA.sln` **tidak
bisa dibaca MSBuild sama sekali**. Penyisipan entri proyek Custom.Orchestrator
dan Custom.Flow yang saya lakukan dengan awk mendarat SEBELUM baris kepala
solusi, sehingga MSBuild menolak berkasnya dengan
`MSB5010: No file format header found`.

Akibatnya setiap "build solusi" sejak ronde sebelumnya **berhenti dalam 0,02
detik tanpa membangun apa pun**. Dan karena skrip saya menghitung galat dengan
pola `error CS|MC|XLS|XC`, galat solusi itu tidak terhitung — laporannya
"0 galat kompilasi", padahal tidak ada satu baris pun yang dikompilasi.

Selama beberapa putaran saya karena itu menguji rakitan lama dan menyimpulkan
hal yang salah tentang perbaikan yang sebenarnya benar. Kedua entri proyek kini
berada sesudah baris kepala, seluruh 44 proyek terbangun, dan `rebuild.sh`
sekarang memeriksa dua hal yang sebelumnya tidak diperiksa:

- hasil build harus LEBIH BARU daripada berkas `.cs` terbaru proyeknya —
  kalau tidak, kompilasinya dilewati dan yang diuji adalah kode lama;
- berkas di folder keluaran harus identik dengan hasil build.

Memeriksa yang kedua saja tidak berarti apa-apa: kalau kompilasinya dilewati,
kedua berkas sama-sama basi dan pemeriksaannya lolos.

## Jeda 20 detik setelah membuka peramban

Sumbernya `NMHook.openurl` di OpenRPA.NM:

```
do { Thread.Sleep(500); } while (sw.Elapsed < TimeSpan.FromSeconds(20) && !chromeconnected);
```

Setelah meluncurkan peramban, ia menunggu sampai **dua puluh detik** agar addon
OpenRPA yang lama menyambung. Pada pemasangan JakForge addon itu tidak dipakai
sama sekali — yang dipakai jembatan `com.studio.nativehost` — jadi penantiannya
selalu habis sia-sia, di setiap Open Browser, sebelum satu langkah pun
dikerjakan.

`Custom.Browser.OpenBrowser` sekarang memakai jalur itu HANYA kalau addon lamanya
memang tersambung. Kalau tidak, perambannya diluncurkan langsung, lalu yang
ditunggu adalah tanda kesiapan yang sungguh-sungguh dipakai langkah berikutnya:
jembatan JakForge melihat tab dengan host yang sama. Menunggu selang waktu tetap
hanya bisa dua-duanya salah — kelamaan saat perambannya cepat, kurang saat
halamannya berat.

**20,6 detik menjadi 2,9 detik**, diukur dari log jalan yang sama.

## Hasil berbeda antara Studio dan JakRunner

Penyebabnya bukan runner-nya, melainkan **ekstensi yang mencari elemen sekali
lalu menyerah**.

`StudioSetText`, `StudioClick`, `StudioGetText`, `StudioHighlight`,
`StudioFocusElement`, dan `StudioHoverElement` semuanya SUDAH mengirim
`timeoutMs` ke jembatan sejak awal — properti Timeout-nya ada di panel
Properties. Tapi di sisi ekstensi nilai itu tidak pernah dipakai untuk menunggu
elemen: `pageOpsFn` memanggil `resolveAll` satu kali, dan kalau kosong langsung
mengembalikan "Elemen tidak ditemukan".

Halaman seperti RPA Challenge menggambar ulang formulirnya setelah tiap
pengiriman. Robot yang mengetik lebih cepat daripada halaman menggambar akan
menemui DOM yang sedang kosong — dan siapa yang menang lomba itu bergantung pada
beban antarmuka host-nya. Studio yang menggambar kanvas lebih lambat; JakRunner
tanpa beban itu lebih cepat, dan gagal di baris yang sama. Itulah "hasilnya
berbeda".

Sekarang `pageOpsFn` menerima `timeoutMs` dan menunggu elemennya muncul,
memeriksa tiap 60 ms sampai batas waktunya. `timeoutMs` nol berarti sekali cari
— dipakai perkakas seperti UI Explorer, yang memang ingin jawaban apa adanya
saat itu juga.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Berkas solusi | MSBuild | MSB5010 hilang, 44 proyek terbangun |
| Solusi penuh | rebuild.sh | 0 galat kompilasi |
| Metode baru benar-benar ter-deploy | cari nama metode di DLL | LaunchBrowser, WaitForPage, HostOf ADA |
| **Jeda setelah Open Browser** | **log jalan cha** | **20,6 detik → 2,9 detik** |
| Penantian elemen | dijalankan di peramban, 4 kasus | 4/4 lulus (lihat di bawah) |
| Pesan galat saat timeout kosong | uji yang sama | "Elemen tidak ditemukan: ..." tanpa "0 detik" |

## Yang BELUM selesai

**Ekstensi Chrome perlu dimuat ulang sekali.** Ia dimuat sebagai unpacked
extension langsung dari `Studio\Extension`, jadi berkasnya sudah berubah — tapi
Chrome baru membaca ulang setelah tombol Reload ditekan di `chrome://extensions`
(atau Chrome dijalankan ulang). Sampai itu dilakukan, penantian elemen belum
aktif dan RPA Challenge masih bisa berhenti di tengah.

## Cara penantian elemen diuji tanpa memuat ulang ekstensi

`pageOpsFn` adalah fungsi mandiri — ia disuntikkan ke halaman dan hanya
memakai `document`, tidak menyentuh API `chrome` sama sekali. Jadi ia bisa
dijalankan di halaman biasa: `background.js` disisipkan ke sebuah halaman uji,
`chrome` diganti boneka seperlunya, lalu `pageOpsFn` dipanggil langsung.

Halaman ujinya meniru persis yang dilakukan RPA Challenge: mengosongkan
formulir, lalu menggambar ulang isiannya setelah sekian milidetik.

| Kasus | Hasil |
|---|---|
| elemen muncul telat 1,5 dtk, timeout 5 dtk | berhasil setelah 1533 ms |
| elemen tak pernah muncul, timeout 0 | gagal dalam 0 ms |
| elemen tak pernah muncul, timeout 1,5 dtk | gagal setelah 1522 ms |
| elemen sudah ada, timeout 5 dtk | berhasil dalam 1 ms |

Baris terakhir yang paling penting: menunggu TIDAK menambah jeda apa pun kalau
elemennya memang sudah ada. Penantiannya hanya terjadi saat dibutuhkan.

Keenam activity yang mengirim `timeoutMs` — StudioClick, StudioSetText,
StudioGetText, StudioHighlight, StudioFocusElement, StudioHoverElement —
semuanya memakai `if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(10)`.
Artinya workflow yang ada TIDAK perlu diubah: properti Timeout yang dibiarkan
kosong tetap berarti menunggu sampai sepuluh detik.

Berkas ujinya sengaja dihapus setelah dipakai — apa pun yang tertinggal di
folder `Extension` ikut terbawa saat ekstensinya dikemas.

---

# Ronde: satu kolom, dua arti — dan cacat yang saya buat sendiri

Penantian elemen berjalan, tapi "cha" justru gagal dengan pesan BARU:

    Studio Bridge: Timeout menunggu balasan dari extension
    (browser mungkin tidak aktif).

Padahal browsernya aktif. Pesan itu menutupi sebab sebenarnya, dan sebabnya
adalah perubahan saya sendiri di ronde sebelumnya.

## Tiga lapisan menunggu, dua di antaranya memakai angka yang sama

Satu perintah melewati tiga penantian bersarang:

| Lapisan | Membaca | Artinya |
|---|---|---|
| Extension | `message.timeoutMs` | berapa lama menunggu **elemen** |
| Native host | `request["timeoutMs"]` | berapa lama menunggu **extension** |
| Klien (activity) | `responseTimeoutMs` | berapa lama menunggu **native host** |

Dua baris pertama membaca KOLOM YANG SAMA. `StudioPipeClient` memang mengisi
`timeoutMs` dengan `responseTimeoutMs`, tapi activity menimpanya lewat
`extraParams` dengan timeout ELEMEN — dan nilai itulah yang akhirnya dibaca
native host sebagai anggarannya sendiri.

Jadi keduanya sepuluh detik. Selama extension menjawab dalam milidetik, tabrakan
itu tidak pernah terlihat. Begitu extension benar-benar memakai sepuluh detik
untuk menunggu elemen, native host menyerah pada detik yang sama persis — dan
yang sampai ke user adalah pesan lapisan LUAR, bukan sebab sebenarnya.

Aturannya sederhana dan sekarang ditegakkan: **lapisan yang menunggu harus
selalu hidup lebih lama daripada yang ditunggunya.** Kalau tidak, pesan galat
yang berguna selalu kalah cepat oleh pesan galat yang tidak berguna.

Anggaran host kini dikirim di kolomnya sendiri, `hostTimeoutMs`, dan selalu
lebih besar daripada operasi terlama yang mungkin dijalankan extension:

    operasi extension  10 dtk  <  native host  30 dtk  <  klien  33 dtk

Kolom lama tetap dibaca sebagai cadangan, jadi klien versi lama tidak berubah
perilakunya.

## Cacat yang sama ternyata sudah lama ada di Open Tab

`StudioOpenTab` memanggil `SendCommand("openTab", request)` dengan
`responseTimeoutMs` bawaan 10 detik, sementara extension menunggu halaman
selesai dimuat juga sampai `message.timeoutMs`. Angka yang sama lagi. Halaman
yang butuh sepuluh detik penuh akan membuat keduanya kedaluwarsa bersamaan.
Ini bukan cacat baru — hanya belum pernah kebetulan terpicu. Perbaikan yang
sama menutupnya sekalian.

## Yang diuji ronde ini

Native host diuji TERPISAH, tanpa browser: stdin-nya dibiarkan terbuka supaya
prosesnya hidup, tapi tidak ada extension yang menjawab — jadi setiap permintaan
pasti berakhir timeout, dan yang diukur adalah KAPAN.

| Permintaan | Balasan | Hasil |
|---|---|---|
| `hostTimeoutMs` 12 dtk, `timeoutMs` 3 dtk | 12,1 dtk | LULUS — kolom baru yang menentukan |
| hanya `timeoutMs` 4 dtk (klien lama) | 4,0 dtk | LULUS — cadangan tetap jalan |
| tanpa kolom timeout | 10,0 dtk | LULUS — bawaan |

Baris kedua sekaligus memperagakan cacat lamanya: dulu activity mengirim persis
bentuk itu, dan host menyerah pada angka timeout ELEMEN.

| Bagian | Cara | Hasil |
|---|---|---|
| Solusi penuh | MSBuild | 0 galat, 0 kunci berkas |
| Custom.StudioBridge, Custom.Browser, Custom.Flow, Custom.Orchestrator | bandingkan md5 build vs keluaran | mutakhir & cocok |
| JakRunner.exe | bandingkan dengan sumbernya | mutakhir |

## Catatan tentang angka "7 out of 70"

Halaman RPA Challenge menghitung TERUS sampai sepuluh kali submit, lalu baru
menampilkan hasilnya — hitungannya tidak berhenti hanya karena robotnya gagal.
Beberapa percobaan yang masing-masing berhenti di baris kedua tetap menambah
putaran, sementara datanya selalu diulang dari baris pertama. Putaran kedua dan
seterusnya karena itu diisi data yang salah.

Jadi 7 dari 70 adalah hasil beberapa percobaan yang tertumpuk, bukan ukuran satu
jalan yang bersih. Supaya angkanya berarti, tombol RESET di halaman itu harus
ditekan dulu sebelum menjalankan ulang.

---

# Ronde: elemen yang ketemu TERLALU CEPAT

Perbaikan timeout berhasil: pesan galatnya sekarang pesan lapisan dalam, bukan
lagi "browser mungkin tidak aktif". Dan pesan itu langsung menunjukkan bahwa
diagnosis saya dua ronde lalu SALAH.

## Log jalan menutup kasusnya

    Klik Start    10:59:41.336
    Submit ke-10  10:59:42.179

Sepuluh putaran dalam **843 milidetik** — sekitar 85 ms per putaran, dan hanya
16 ms antara klik Submit dan pengetikan berikutnya.

Halaman RPA Challenge tidak MENGOSONGKAN formulir setelah dikirim; ia MENGGANTI
elemennya dengan yang baru, dan penggantian itu datang lebih lambat daripada
satu putaran robot. Jadi:

1. Robot mengetik tujuh isian dan menekan Submit. Putaran itu benar.
2. Angular mulai menggambar ulang.
3. 16 ms kemudian robot mencari `labelFirstName` lagi. Elemen LAMA masih ada,
   jadi pencariannya **berhasil**, dan teks putaran berikutnya masuk ke situ.
4. Penggambaran ulang selesai. Seluruh isian itu ikut terbuang.
5. Submit berikutnya mengirim formulir kosong.

Hanya putaran pertama yang selamat — karena hanya di situ halamannya sudah
tenang lebih dulu. **Tujuh dari tujuh puluh.**

Menunggu elemen MUNCUL, yang saya tambahkan ronde lalu, tidak bisa menolong sama
sekali di sini: elemennya tidak hilang, ia justru ada — hanya saja yang salah.
Penantian itu baru bekerja setelah putaran kesepuluh, ketika formulirnya diganti
layar "Congratulations" dan pencariannya akhirnya benar-benar kosong. Itulah
galat yang terlihat.

## Tereproduksi lebih dulu, baru diperbaiki

Saya sudah sekali membangun perbaikan di atas dugaan yang keliru, jadi kali ini
mekanismenya dibuktikan dulu di halaman tiruan: formulir dengan `ng-reflect-name`
yang, setelah dikirim, elemennya diganti setelah jeda D — persis seperti aslinya.
`pageOpsFn` yang asli dipakai apa adanya, dengan jeda 10 ms per operasi seperti
di log jalan sungguhan.

Kode LAMA, disapu di berbagai kecepatan render:

|   D | isian benar dari 21 |
|---:|---|
|   0 ms | 21 / 21 |
|   5 ms | 21 / 21 |
|  15 ms | 19 / 21 |
|  30 ms | 17 / 21 |
|  50 ms | 11 / 21 |
|  80 ms | 21 / 21 |
| 120 ms | 15 / 21 |

Isian hilang tanpa satu pun galat — dan perhatikan 80 ms lulus sementara 120 ms
gagal. Hasilnya **tidak menentu**. Itulah "hasilnya berbeda di Studio dan di
JakRunner": keduanya membalap penggambaran yang sama, dan yang menang bergantung
beban antarmuka host.

## Perbaikannya: tunggu halaman selesai bereaksi

`actAndSettle` membungkus tindakan yang mengubah halaman. Ia mengamati DOM,
lalu menunggu sampai perubahannya berhenti — dengan satu sinyal tambahan yang
jauh lebih tegas daripada sekadar "ada perubahan": **kalau elemen yang ditindak
LEPAS dari dokumen, itu bukti pasti halamannya menggambar ulang.**

Tenggangnya berbeda untuk mengetik dan mengklik, dan itu diukur, bukan dikira:

|  | tenggang | 3 putaran | hasil |
|---|---|---|---|
| ketik 32 ms, klik 400 ms | | 1309-1933 ms | 21/21 |
| **ketik 0 ms, klik 400 ms** | | **574-1191 ms** | **21/21** |

Memberi pengetikan tenggang tidak membeli kebenaran apa pun, hanya waktu — jadi
tidak dipakai. Reaksi yang memang ada tetap tertangkap, karena deteksi perubahan
kerangka kerja modern selesai di akhir event dan sudah terlihat begitu
microtask-nya beres.

Sesudah perbaikan, seluruh sapuan lulus — termasuk 200 ms yang saya tambahkan
di luar rentang aslinya:

    0, 5, 15, 30, 50, 80, 120, 200 ms   ->   21/21 semua

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Mekanisme kegagalan | reproduksi halaman tiruan | tereproduksi, 11-19 dari 21 |
| Perbaikan | sapuan yang sama | 8 dari 8 kecepatan render lulus 21/21 |
| Biaya perbaikan | waktu 3 putaran | +0 sampai +600 ms |
| Tenggang pengetikan perlu? | bandingkan 32 ms vs 0 | tidak perlu; dibuang, hemat separuh |

## Yang BELUM diuji

Belum dijalankan terhadap rpachallenge.com yang sungguhan. Yang dibuktikan
adalah mekanismenya dan bahwa perbaikannya menutup seluruh rentang kecepatan
render yang sebelumnya bocor.

**Ekstensi perlu dimuat ulang lagi**, karena `background.js` berubah.

---

# Ronde: satu baris dikerjakan dua kali

Petunjuk yang menutup kasusnya datang dari satu kalimat: **"kalau dijalankan di
Studio bisa 100%"**. Ekstensinya sama, activity-nya sama, halamannya sama. Yang
BERBEDA cuma satu hal — dan itu memang sengaja dibuat berbeda:

    Studio     -> ForEachDataRow milik OpenRPA
    JakRunner  -> ForEachDataRow milik Custom.Flow (pemetaan di RunnerSchemaContext)

Jadi bugnya ada di milik kita.

## `List<T>.GetEnumerator()` mengembalikan STRUCT

```csharp
var enumerator = rows.GetEnumerator();   // struct List<T>.Enumerator
context.SetValue(_rows, enumerator);     // MENGOTAKKAN SALINANNYA
...
if (!enumerator.MoveNext()) return;      // menggerakkan LOKAL
context.ScheduleAction(Body, enumerator.Current, OnBodyComplete);
```

lalu di `OnBodyComplete`:

```csharp
var enumerator = _rows.Get(context);     // ini KOTAKNYA, masih di posisi awal
if (!enumerator.MoveNext()) return;      // menggerakkan KOTAK
```

Dua posisi yang berbeda digerakkan bergantian. Diperagakan tersendiri, tanpa WF:

    pola LAMA  (11 putaran): r0,r0,r1,r2,r3,r4,r5,r6,r7,r8,r9
    pola BARU  (10 putaran): r0,r1,r2,r3,r4,r5,r6,r7,r8,r9

Baris pertama dikerjakan DUA KALI, seluruh baris sesudahnya bergeser satu, dan
perulangannya berjalan sekali lebih banyak daripada jumlah barisnya. Kompilernya
tidak mengeluh sedikit pun.

Semua gejalanya langsung terjelaskan:

- putaran 1 dapat baris 1 -> **benar**; putaran 2 dapat baris 1 lagi -> salah;
  putaran 3 dapat baris 2 -> salah; dan seterusnya. **Tujuh dari tujuh puluh.**
- log jalan menunjukkan **sebelas** putaran untuk sepuluh baris;
- putaran kesebelas berjalan setelah Submit terakhir, ketika formulirnya sudah
  diganti layar "Congratulations" — itulah "Elemen tidak ditemukan" yang
  terlihat di JakRunner.

Bug yang sama ada di `ForEachOf<T>`. `BreakableWhile` dan `BreakableDoWhile`
tidak memakai enumerator, jadi aman.

Perbaikannya: simpan DAFTARNYA dan sebuah INDEKS, bukan enumerator. Tidak ada
jebakan nilai-versus-acuan, dan langsung sejalan dengan Index/Total yang memang
sudah dipakai.

## Diagnosis saya sebelumnya keliru

Ronde lalu saya menyimpulkan penyebabnya "elemen ketemu terlalu cepat" dan
menambahkan penantian sesudah klik. Balapan itu NYATA — saya mereproduksinya —
tapi ia BUKAN penyebab 7 dari 70. Penyebabnya perulangan yang melewatkan baris.

Penantian sesudah klik tetap dipertahankan: ia menutup kelas kegagalan yang
sudah dibuktikan bisa menghilangkan isian tanpa galat. Biayanya sekitar 5 detik
untuk sepuluh putaran (1173 ms menjadi 6480 ms pada jalan sungguhan). Kalau
kecepatan lebih penting daripada jaminan itu, `SETTLE_GRACE_CLICK_MS` di
`Extension/background.js` tinggal dijadikan 0.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Mekanisme boxing | program kecil tersendiri | LAMA 11 putaran r0,r0,r1..; BARU 10 putaran r0..r9 |
| ForEachDataRow, 10 baris | WorkflowInvoker | r0..r9, tepat 10 putaran |
| ForEachDataRow, 1 baris | WorkflowInvoker | r0 |
| ForEachDataRow, 0 baris | WorkflowInvoker | kosong, tidak melempar |
| ForEachOf, 5 item | WorkflowInvoker | x0..x4 |
| ForEachOf, 0 item | WorkflowInvoker | kosong, tidak melempar |
| Solusi penuh | MSBuild berurutan | 0 galat |
| Custom.Flow ter-deploy | md5 build vs keluaran | cocok, ScheduleNext ada di DLL |

## Dua jebakan perkakas yang muncul lagi

**Build paralel kehabisan memori.** `-m` pada mesin yang sedang menjalankan
Chrome dan ForgeHub menghasilkan 305 baris `MSB4018` yang sebab dalamnya
`System.OutOfMemoryException` — bukan masalah kode sama sekali. `-m:1` bersih.

**Pola hitung galat saya buta lagi.** `grep -cE 'error (CS|MC|XLS|XC)'`
melaporkan "0 galat" untuk build yang gagal total, persis seperti waktu berkas
solusi rusak dulu. Yang benar adalah menghitung `error [A-Z]+[0-9]+` apa pun
kodenya.

## Yang BELUM diuji

Belum dijalankan terhadap rpachallenge.com yang sungguhan. Yang dibuktikan
adalah urutan baris yang diterima badan perulangan kini benar untuk 0, 1, 5,
dan 10 elemen.

---

# Ronde: ValueTuple, dan log yang disarangkan ke dalam jalannya

## `System.ValueTuple` 4.0.2.0 versus 4.0.3.0

Gejalanya muncul SEBELUM baris log pertama ditulis — berkas log tidak bertambah
sama sekali — jadi kegagalannya saat MEMUAT, bukan saat menjalankan:

    Could not load file or assembly 'System.ValueTuple, Version=4.0.3.0'
    ... The located assembly's manifest definition does not match

Berkas config mengalihkan SEMUANYA ke 4.0.3.0, tapi yang mendarat di folder
keluaran 4.0.2.0. Sumbernya dua permintaan yang berbeda:

    OpenRPA              -> System.ValueTuple 4.5.0  (rakitan 4.0.3.0)
    OpenRPA.CodeEditor   -> ditarik Roslyn di 4.4.0  (rakitan 4.0.2.0)

Seluruh proyek menyalin ke SATU folder, jadi yang menyalin terakhir menentukan
versi yang benar-benar dipakai — dan hasilnya bergantung urutan build. Persis
penyakit Newtonsoft.Json 13.0.2/13.0.4 dulu, dan perbaikannya sama: versinya
disamakan, kali ini dengan menyematkan 4.5.0 di `OpenRPA.CodeEditor`.

Diuji dengan memuat XAML "cha" memakai `JakRunner.Core.WorkflowLoader` yang
sama persis dipakai JakRunner, dengan berkas config JakRunner:

    LULUS  termuat: DynamicActivity

## Log ForgeHub disarangkan ke dalam jalannya

Sebelumnya satu aliran datar berisi semua robot sekaligus. Begitu ada lebih dari
satu pekerjaan berjalan, barisnya berselang-seling dan tidak ada cara melihat
"apa yang terjadi pada jalan ini" selain memindai dengan mata.

Sekarang bentuknya seperti orkestrator RPA pada umumnya: daftar JALAN, masing-
masing bisa dibuka untuk melihat langkahnya sendiri.

- Jalan terbaru di ATAS, dan hanya jalan terbaru yang terbuka. Membuka semuanya
  mengembalikan persis dinding teks yang ingin dihindari.
- Di DALAM satu jalan, langkahnya terlama di atas — itulah urutan kejadiannya.
- Kepala tiap jalan sudah cukup untuk tahu perlu dibuka atau tidak: nama proses,
  robotnya, jumlah baris, jumlah galat dan peringatan, rentang waktunya, dan
  penanda merah kalau ada galat.
- Tombol "Hanya ini" menyaring ke satu pekerjaan, memakai penyaring `jobId` yang
  memang sudah ada di API.
- Baris yang datang tanpa pekerjaan maupun nama proses masuk kelompok "lain",
  dan kelompok itu SELALU paling bawah betapapun barunya — ia bukan sebuah
  jalan, cuma penampungan, dan satu baris nyasar tidak boleh mendorong jalan
  yang sedang berlangsung ke bawah layar.

Pembaruan langsung tidak menggambar ulang daftar: baris baru dimasukkan ke
kelompoknya masing-masing, dan jalan yang baru muncul disisipkan di atas. Kalau
digambar ulang, jalan yang sedang dibaca orang akan tertutup kembali dan
gulirannya melompat.

Guliran juga tidak lagi dipaksa ke bawah. Dulu masuk akal karena baris terbaru
memang di dasar; sekarang jalan terbaru ada di atas dan sudah terbuka, jadi
menyeret ke ujung bawah justru memperlihatkan jalan yang paling lama.

## Cacat gaya yang ikut ketahuan

Nama robot di baris log memakai kelas `who` — yang ternyata milik chip pengguna
di bilah atas, lengkap dengan border, latar, dan padding. Nama robot karena itu
tampil sebagai KOTAK yang meremas barisnya. Cacat ini sudah ada sejak tampilan
datar, hanya tidak kentara. Sekarang punya kelas sendiri, `log-who`.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Versi ValueTuple yang mendarat | baca versi rakitan di folder keluaran | 4.0.3.0, cocok dengan redirect |
| Memuat XAML "cha" | WorkflowLoader milik JakRunner + config-nya | termuat, DynamicActivity |
| Solusi penuh | MSBuild berurutan | 0 galat |
| ForgeHub | dotnet build | 0 galat, 0 peringatan |
| Pengelompokan log | halaman uji, 9 pemeriksaan | 9/9 lulus |
| Tampilan log berkelompok | tangkapan layar | benar; kotak `who` hilang |
| app.js sesungguhnya | muat dasbor, baca console | tidak ada galat |

---

# Ronde: ClosedXML, dan berhenti menambal satu per satu

Ini kali KETIGA penyakit yang sama muncul dengan wajah berbeda — Newtonsoft.Json,
lalu System.ValueTuple, sekarang ClosedXML — dan tiap kali baru ketahuan saat
robot berhenti. Jadi ronde ini bukan cuma menambal yang ketiga, tapi mencari
semuanya sekaligus.

## Penyakitnya

Seluruh 44 proyek menyalin ke SATU folder keluaran. Kalau dua proyek meminta
versi berbeda dari pustaka yang sama, yang menyalin TERAKHIR menentukan versi
yang benar-benar dipakai — dan berkas config sudah terlanjur mengalihkan
semuanya ke satu versi. Begitu yang mendarat bukan versi itu, pemuatannya gagal
dengan "manifest definition does not match".

Hasilnya bergantung urutan build, jadi gejalanya datang dan pergi.

## ClosedXML

    OpenRPA            -> 0.105.1
    Custom.Excel       -> 0.105.1
    OpenRPA.Utilities  -> 0.95.4     <- tertinggal

Disamakan ke 0.105.1. Dua jebakan menghadang di jalan:

**Berkas keluaran bertanggal masa depan.** `debug/net462/ClosedXML.dll` tertanggal
16 Desember. MSBuild menyalin "kalau sumbernya lebih baru", jadi berkas itu tidak
pernah tertimpa. Harus dihapus dulu.

**Restore memakai jawaban lama.** Mengubah `.csproj` saja tidak cukup:
`obj/project.assets.json` masih memuat 0.95.4 dan build memakainya. Folder `obj`
proyek itu harus dibuang supaya restore-nya diulang.

## Pemeriksa, supaya tidak menambal satu per satu lagi

`tools-periksa-rakitan.ps1` membaca SETIAP rakitan di folder keluaran, mengambil
seluruh rujukannya, menerapkan pengalihan versi dari berkas config, lalu
membandingkannya dengan berkas yang BENAR-BENAR ada di sana.

Hasil pertama: **26 rujukan tidak cocok.** Termasuk dua yang belum ketahuan:

- `System.ValueTuple` kembali ke 4.0.2.0 — perbaikan sebelumnya tertimpa;
- `System.Runtime.CompilerServices.Unsafe` 4.0.6.0 padahal redirect menunjuk
  6.0.0.0, dan **sebelas** rakitan memintanya, termasuk Roslyn.

Keduanya ranjau yang belum meledak.

## Sumber sebenarnya: shim netstandard bawaan MSBuild

Melacak `-v:detailed` menunjukkan `System.ValueTuple.dll` disalin TIGA kali ke
tujuan yang sama, dari dua sumber berbeda:

    OpenRPA.Forms       <- Microsoft.NET.Build.Extensions\net461\lib   (4.0.2.0)
    OpenRPA.Script      <- paket NuGet 4.5.0                           (4.0.3.0)
    OpenRPA.Utilities   <- Microsoft.NET.Build.Extensions\net461\lib   (4.0.2.0)  <- terakhir

Proyek .NET Framework yang memakai pustaka netstandard2.0 dilengkapi MSBuild
dengan shim-nya sendiri. Shim itu tidak kelihatan di berkas proyek mana pun,
jadi tidak akan pernah ditemukan dengan membaca `.csproj`. Obatnya: sebut
paketnya EKSPLISIT di proyek-proyek itu, supaya siapa pun yang membangun
terakhir hasilnya sama.

`System.Runtime.CompilerServices.Unsafe` persis sama, dengan tiga versi
(5.0.0, 6.0.0, 4.7.0) dan yang terakhir menang.

## Hasilnya

    26 rujukan tidak cocok  ->  7

Ketiga yang punya pengalihan versi — ValueTuple, ClosedXML, Unsafe — sudah
bersih. Tujuh sisanya SENGAJA dibiarkan:

| Rakitan | Di folder | Diminta | Peminta |
|---|---|---|---|
| Humanizer | 2.2.0.0 | 2.4.0.0 | Forge.Forms |
| BouncyCastle.Crypto | 1.8.6.0 | 1.9.0.0 | MailKit, MimeKit |
| MaterialDesignColors | 1.1.3.0 | 2.5.0.1205 | MaterialDesignThemes.Wpf |
| office | 14.0.0.0 | 12.0.0.0 | Office interop (3) |

Semuanya selisih versi ANTAR PUSTAKA PIHAK KETIGA, tidak punya pengalihan versi,
dan sudah begitu sejak sebelum ronde ini — Studio berjalan normal dengannya,
yang berarti jalur kodenya memang tidak pernah dimuat. Mengubahnya berarti
mengutak-atik yang sedang bekerja demi masalah yang belum ada. Dibiarkan, tapi
sekarang TERCATAT dan bisa diperiksa kapan saja.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| ClosedXML yang mendarat | baca versi rakitan | 0.105.1.0 |
| ValueTuple yang mendarat | baca versi rakitan | 4.0.3.0 |
| Unsafe yang mendarat | baca versi rakitan | 6.0.0.0 |
| Seluruh folder keluaran | tools-periksa-rakitan.ps1 | 26 -> 7, semua sisanya tanpa redirect |
| Memuat XAML "cha" | WorkflowLoader milik JakRunner + config-nya | LULUS, DynamicActivity |
| Solusi penuh | MSBuild berurutan | 0 galat |

## Dua lolos palsu yang hampir saya laporkan sebagai keberhasilan

**Pemeriksa melaporkan "BERSIH" untuk 0 rakitan.** `Get-ChildItem -Include`
tanpa wildcard pada path tidak cocok apa pun. Baris "rakitan di folder : 0" yang
menyelamatkan; tanpa angka itu saya akan melaporkan folder yang bersih padahal
tidak ada yang diperiksa.

**Komentar XML memuat `--`.** Kedua berkas proyek gagal DIMUAT, jadi kedua
proyek itu tidak ikut dibangun — dan karena mereka yang menimpa ValueTuple,
versinya jadi tampak benar. "Berhasil" yang sebenarnya adalah dua proyek yang
tidak jalan.

Keduanya jenis kegagalan yang sama: pemeriksaan yang lolos karena tidak
memeriksa apa pun.

---

# Ronde: JakRunner lebih lambat daripada Studio

Keluhannya wajar: JakRunner tidak menggambar kanvas, jadi ia SEHARUSNYA lebih
cepat. Dua dugaan pertama saya salah, dan pengukurannya yang membuktikan.

## Dugaan 1: pencatatan JakRunner — SALAH

JakRunner memang melakukan sesuatu yang Studio tidak: `RunLog.WriteToFile`
menulis ke berkas untuk SETIAP baris, sinkron di thread automasi, buka-tulis-
tutup tiap kali. Studio hanya menaruhnya di panel Output.

Diukur dengan workflow 400 Assign, tanpa peramban sama sekali:

    polos (tanpa apa pun)      :  66,5 ms
    + pelacakan saja           :  73,8 ms   (+7,3)
    + pelacakan & tulis berkas : 234,6 ms   (+168,1)

    biaya per activity         : 0,42 ms

Untuk "cha" yang sekitar 80 activity, itu 34 ms. Bukan penyebabnya.

Satu jebakan hampir membuat saya menyimpulkan lebih parah lagi: pengukuran
PERTAMA memberi 0,018 ms per activity, dan itu salah — `RobotLog` diam total
kalau tidak ada yang berlangganan (`if (handler == null) return`), jadi yang
terukur adalah ketiadaan pencatatan, bukan pencatatan yang murah.

## Dugaan 2: Chrome memperlambat timer halaman tersembunyi — TIDAK TERBUKTI

`waitForElements` dan `actAndSettle` sama-sama memakai `setTimeout` DI DALAM
halaman. Chrome memperlambat timer halaman yang tersembunyi, dan jendela
JakRunner yang menutupi Chrome membuat halamannya tersembunyi — itu akan
menjelaskan persis "lebih cepat lewat Studio".

Tapi saya tidak bisa membuktikannya: di panel pratinjau, tab latar tidak pernah
ditandai tersembunyi (634 sampel, `document.hidden` tetap false, 66 ms untuk
permintaan 60 ms). Jadi dugaan ini DIBIARKAN sebagai dugaan, tidak dibangun di
atasnya.

## Yang sebenarnya: penantian yang saya tambahkan sendiri

Aritmetikanya menutup kasusnya tanpa perlu jalan baru:

    jalan yang sama SEBELUM penantian ditambahkan :  843 ms
    jalan yang dilaporkan RPA Challenge           : 6480 ms

    10 klik x SETTLE_GRACE_CLICK_MS 400 ms        = 4000 ms

Tenggang yang SELALU habis penuh berarti tidak ada perubahan DOM yang terdeteksi
sesudah klik. Penantian itu membayar empat detik untuk sesuatu yang tidak pernah
ia temukan.

Dan penantian itu memang ditambahkan di atas diagnosis yang keliru: penyebab
"7 dari 70" ternyata perulangan yang melewatkan baris, bukan balapan dengan
penggambaran ulang.

Jadi tenggangnya dinolkan. Yang tetap ada: kalau halaman bereaksi SEKETIKA —
dan kerangka kerja modern menjalankan deteksi perubahannya di akhir event —
perubahan itu tetap ditunggu sampai tenang.

## Yang ditukar, dengan angkanya

Sapuan reproduksi yang sama, halaman tiruan yang MENUNDA penggambaran ulangnya:

| jeda render | tenggang 400 ms | tenggang 0 |
|---:|---|---|
|   0 ms | 21/21 | 21/21 |
|   5 ms | 21/21 | 21/21 |
|  15 ms | 21/21 | 20/21 |
|  30 ms | 21/21 | 19/21 |
|  50 ms | 21/21 | 15/21 |
| 120 ms | 21/21 | 5/21 |
| 200 ms | 21/21 | 12/21 |
| lama 3 putaran | 1309-1933 ms | 392-573 ms |

Perlindungan terhadap halaman yang MENUNDA penggambaran ulangnya memang hilang.
Yang membuat penukaran ini masuk akal: halaman tiruan itu sengaja menunda lewat
`setTimeout`, sementara bukti dari halaman SUNGGUHAN mengatakan sebaliknya —
tenggang 400 ms di sana tidak pernah menangkap apa pun.

Kalau suatu saat muncul lagi gejala isian hilang diam-diam, `SETTLE_GRACE_CLICK_MS`
di `Extension/background.js` adalah tombolnya. Menaikkannya menagih biaya itu
pada SETIAP klik di setiap workflow, jadi jangan dinaikkan tanpa gejala.

## Yang diuji ronde ini

| Bagian | Cara | Hasil |
|---|---|---|
| Biaya pencatatan JakRunner | 400 activity, tanpa peramban | 0,42 ms per activity — bukan penyebab |
| RobotLog tanpa pelanggan | baca kodenya | no-op; pengukuran pertama tidak sah |
| Pelambatan timer | tab latar, 634 sampel | tidak tereproduksi di sini |
| Biaya tenggang klik | aritmetika dari dua jalan sungguhan | ~4000 ms dari 6480 ms |
| Perlindungan yang tersisa | sapuan reproduksi | hilang untuk render tertunda; dicatat |

---

# Ronde: proses yang bisa dibuka

Permintaannya dua kalimat, tapi jawabannya satu halaman: **detail proses**.
Daftar hanya menjawab "proses apa saja yang ada"; yang orang butuhkan
berikutnya selalu "apa yang terjadi waktu proses ini dijalankan" — dan
jawabannya tidak boleh berada di halaman lain yang harus disaring sendiri.
Itulah yang membedakan daftar dari orkestrator.

## Isinya

Klik ganda barisnya (atau klik namanya) membuka satu proses:

- **Ringkasan**: paket dan versinya, lingkungan, berapa kali dijalankan, dan
  berapa yang berhasil versus gagal.
- **Riwayat jalan**: tiap jalan dengan keadaannya, robotnya, sumbernya
  (Manual / Pemicu / Antrean), kapan mulai, dan berapa lama.
- **Log**, dikelompokkan per jalan — memakai penyaji yang SAMA dengan halaman
  Log Langsung, jadi tidak ada dua cara menampilkan log yang bisa saling
  ketinggalan.
- Tombol Jalankan dan Jadwalkan langsung di tempat.

Log di halaman ini ikut diperbarui sendiri tiap lima detik, dengan penyaring
yang sama dengan gambar pertamanya — kalau penjemputan berkala memakai
penyaring lain, daftar yang tampil perlahan berisi baris dari proses lain.

## Klik GANDA, bukan klik biasa

Seluruh baris memang bisa dibuka, tapi hanya dengan klik ganda. Kalau satu klik
sudah membuka, tombol Hapus di ujung baris jadi berbahaya — meleset sedikit
langsung berpindah halaman. Klik ganda tidak pernah terjadi karena tidak
sengaja.

Karena klik ganda juga tidak pernah ditemukan orang dengan sendirinya, nama
prosesnya dibuat bisa diklik biasa dan barisnya berubah warna saat disentuh
kursor. Petunjuknya harus ada di layar, bukan di kepala orang yang membuatnya.

## Satu penyaring, bukan empat cabang

`/api/jobs` sebelumnya hanya bisa disaring per keadaan, dengan dua query yang
ditulis lengkap masing-masing. Menambahkan penyaring proses sebagai cabang
ketiga berarti empat kombinasi yang semuanya harus dijaga tetap sama isinya.
Penyaringnya sekarang dirangkai, jadi kolom yang dipilih hanya ditulis sekali.

## Jebakan yang ditemukan pengujian

`PAGES.process.name = 'cha'` **tidak pernah tersimpan.** `PAGES.process` adalah
sebuah fungsi, dan setiap fungsi sudah punya properti bawaan `name` yang HANYA
BACA. Penugasannya gagal tanpa suara sedikit pun — tanpa galat, tanpa
peringatan — dan halamannya terbuka kosong.

Ini persis jenis cacat yang lolos dari tinjauan kode: barisnya benar secara
tata bahasa, dan salahnya hanya kelihatan kalau dijalankan. Namanya sekarang
`selected`, dengan alasannya ditulis di sebelahnya.

## Yang diuji ronde ini

Halaman detailnya dipanggil dengan `api()` diganti data contoh, lalu hasil
render-nya diperiksa:

| Pemeriksaan | Hasil |
|---|---|
| Judul memuat nama proses | LULUS |
| Kartu paket dan versi | LULUS |
| Hitungan berhasil / gagal | LULUS |
| Riwayat jalan: 2 baris | LULUS |
| Lama jalan dalam detik | LULUS |
| Lama jalan dalam menit | LULUS |
| Log dikelompokkan jadi 2 jalan | LULUS |
| Jalan gagal ditandai merah | LULUS |
| Tombol kembali dan jalankan | LULUS |

Ditambah: ForgeHub `dotnet build` 0 galat, dan dasbor sungguhannya dimuat tanpa
galat console.

---

# Ronde: penutup otomatis, dan inventaris untuk pekerjaan berikutnya

Permintaan ronde ini ada tiga belas butir. Beberapa di antaranya — bahasa untuk
tiga aplikasi, mode gelap, mengganti seluruh activity OpenRPA — masing-masing
pekerjaan tersendiri. Yang dikerjakan penuh dan diuji di sini SATU butir; sisanya
disiapkan dasarnya, dan itu dicatat apa adanya di bawah.

## Catatan 8: penutup otomatis yang tidak pernah aktif

`JakForgeAutoPair` sudah ada, sudah dipanggil dari `App.xaml.cs`, dan isinya
benar. Ia tetap tidak berfungsi karena satu hal:

    WorkflowDesigner.Context.Services.Publish<IExpressionEditorService>(
        new CodeEditor.EditorService(this));

Kanvas menerbitkan editor ekspresinya sendiri. Kotak ekspresi di kartu activity
karena itu bukan `TextBox` melainkan **AvalonEdit** — dan `JakForgeAutoPair`
memasang class handler untuk `TextBox`. Handler-nya tidak pernah kena pada kotak
yang justru paling sering dipakai orang.

Logikanya sekarang ada di `OpenRPA.CodeEditor.AutoPair`, dipasang ke `TextArea`
editor itu. Kelas lama TETAP ada dan tetap berguna: properti bertipe string di
designer buatan sendiri memang TextBox biasa. Keduanya sekarang saling menunjuk
lewat komentar, karena perilakunya harus tetap sama.

Dipisahkan jadi kelas sendiri supaya bisa diuji: seluruh keputusannya hanya
bergantung pada dokumen dan posisi kursor, jadi tidak perlu menghidupkan editor
lengkap beserta Roslyn dan kamus sumber dayanya.

### Diuji pada TextArea AvalonEdit yang sungguhan

| Perilaku | Hasil |
|---|---|
| `(` `[` `{` `"` menghasilkan pasangannya, kursor di tengah | LULUS |
| `(` sesudah nama fungsi: `Trim(|)` | LULUS |
| Mengetik `)` tepat di depan `)` melewatinya, tidak menumpuk | LULUS |
| Mengetik kutip penutup melewatinya | LULUS |
| Apostrof di tengah kata TIDAK dipasangkan (`user's`) | LULUS |
| Kutip sesudah huruf dibiarkan editor | LULUS |
| Teks tersorot DIBUNGKUS, bukan ditimpa | LULUS |
| Sorotannya tetap di dalam pasangan | LULUS |
| Backspace menghapus pasangan KOSONG sekaligus | LULUS |
| Backspace pada pasangan BERISI dibiarkan | LULUS |
| Huruf biasa tidak disentuh | LULUS |

15 pemeriksaan, semuanya lulus. Dua di antaranya sempat "gagal" karena
HARAPANNYA yang keliru: saat AutoPair menolak menangani, editor sendiri yang
menyisipkan karakternya — dan itu di luar jangkauan uji. Yang benar diperiksa
nilai kembaliannya, bukan teks hasilnya.

## Inventaris activity — dasar untuk catatan 4 dan 7

Perbandingan NAMA saja menyesatkan. Ada 37 nama activity OpenRPA yang tidak
punya kembaran bernama sama di `Custom.*`, tapi sebagian besar sebenarnya sudah
ada dengan nama yang lebih baik — `ReadExcel` menjadi `Read Range Workbook`,
`CreateDataTable` dan `AddDataRow` ada di `Custom.Data`, `StartProcess` di
`Custom.System`. Jadi angka 37 itu bukan ukuran pekerjaan yang tersisa.

Yang sudah ada, dihitung dari berkasnya:

| Proyek | Activity |
|---|---|
| Custom.StudioBridge | 28 |
| Custom.Data | 11 |
| Custom.Files | 11 |
| CustomBrowser | 11 |
| Custom.Excel | 8 |
| Custom.Mail | 6 |
| Custom.Terminal | 5 |
| Custom.System | 4 |
| Custom.Flow | 3 |
| Custom.Orchestrator | 3 |
| Custom.Window | 2 |
| **Total** | **92** |

Pemetaan yang benar harus per FUNGSI, bukan per nama, dan itu langkah pertama
catatan 7.

---

# Ronde: activity yang benar-benar kurang

Langkah pertama catatan 7 adalah memetakan per FUNGSI, bukan per nama — dan
pemetaan itu mengubah pertanyaannya.

## Inventaris dibaca dari rakitan, bukan dari sumbernya

Grep tidak bisa diandalkan: deklarasi kelas kadang memanjang ke baris
berikutnya, atributnya mendahului kelasnya, dan sebagian activity generik.
Perkakas refleksi kecil menjawab pertanyaan yang sebenarnya — apa yang BENAR-
BENAR terdaftar sebagai activity:

    Custom.*        97
    OpenRPA.*      106  (di luar OpenRPA.exe)
    OpenRPA.exe     30

Perbandingan NAMA menghasilkan 37 "yang belum ada". Angka itu menyesatkan:
`ReadExcel` sudah menjadi **Read Range Workbook**, `CreateDataTable` dan
`AddDataRow` ada di **Custom.Data**, `StartProcess` di **Custom.System**. Yang
benar-benar tidak punya padanan jauh lebih sedikit.

## Tujuh activity baru

| Baru | Menggantikan | Di mana |
|---|---|---|
| Copy To Clipboard | Copy Clipboard | Custom.System |
| Get Clipboard Text | Insert Clipboard | Custom.System |
| Move Mouse | Move Mouse | Custom.System |
| Show Notification | Show Balloon Tip | Custom.System |
| Open Application | Open Application | Custom.System |
| Close Application | Close Application | Custom.System |
| Get Job Info | Get Workflow Instance | Custom.Orchestrator |

Beberapa keputusan yang perlu dicatat:

**"Get Clipboard Text", bukan "Insert Clipboard".** Yang dikerjakan hanya
MEMBACA isinya; menempelkannya urusan Type Into atau Send Hotkey. Nama lama
menyiratkan dua hal sekaligus.

**Papan klip dijalankan di thread STA tersendiri**, bukan lewat Dispatcher
aplikasi — supaya tetap bekerja di JakRunner yang jendelanya bisa saja tidak
ada. Papan klip juga sering dikunci sesaat oleh Office atau peramban, jadi
dicoba lima kali sebelum menyerah.

**Move Mouse bisa bertahap.** Sebagian antarmuka baru memunculkan menu kalau
kursornya benar-benar melintas; lompatan satu langkah tidak menghasilkan
mousemove di jalurnya.

**Show Notification tidak memblokir**, dan ikon bakinya dibuang lagi di
activity yang sama. Ikon yatim akan menumpuk di baki setiap kali workflow
berjalan.

**Close Application menutup dengan sopan lebih dulu.** CloseMainWindow memberi
program kesempatan menyimpan pekerjaannya; Kill baru dipakai kalau diminta
tegas atau kalau permintaan sopan itu tidak digubris.

**Get Job Info tidak punya ContinueOnError.** Membaca nilai yang sudah ada di
memori tidak punya cara untuk gagal, dan menyediakan tombolnya hanya akan
menyiratkan sebaliknya.

## Cacat yang ditemukan pengujian: peluncur

Uji pertama gagal di dua tempat:

    GAGAL  prosesnya benar-benar berjalan          -> False
    GAGAL  Close Application menutup 1 proses      -> 0

Penyebabnya bukan uji yang salah, melainkan **rancangan yang salah**. Di
Windows 11, `notepad.exe` cuma peluncur: prosesnya langsung keluar dan
jendelanya milik proses lain — Notepad bahkan hanya menambah TAB di jendela
yang sudah terbuka. Open Application mengembalikan nomor proses peluncur yang
sudah mati, sehingga Close Application sesudahnya tidak menutup apa pun.

Sekarang, kalau proses yang dijalankan keburu keluar, dicari proses lain
bernama sama yang PUNYA jendela, dan yang paling baru dibuka itulah yang
nomornya dikembalikan. Yang dijanjikan `Process Id` sekarang adalah proses yang
memiliki jendelanya, bukan yang kebetulan dijalankan.

## Yang diuji ronde ini

Dijalankan sungguhan lewat WorkflowInvoker:

| Pemeriksaan | Hasil |
|---|---|
| Copy To Clipboard lalu Get Clipboard Text | LULUS |
| Teks kosong mengosongkan papan klip | LULUS |
| Open Application memberi Process Id | LULUS |
| Prosesnya benar-benar berjalan | LULUS |
| Close Application menutup 1 proses | LULUS |
| Prosesnya sudah tidak ada sesudahnya | LULUS |
| Menutup proses yang sudah hilang: 0, tanpa melempar | LULUS |

Ditambah: solusi penuh 0 galat, dan ketujuh activity terbukti terdaftar lewat
refleksi atas rakitan yang ter-deploy.

Papan klip pengguna DISIMPAN dan dikembalikan oleh ujinya sendiri.

**Move Mouse dan Show Notification tidak dijalankan dalam uji.** Keduanya
terlihat dan terasa oleh orang yang sedang memakai komputernya — menggeser
kursor di tengah pekerjaan bukan harga yang pantas untuk sebuah uji asap.
Keduanya terkompilasi dan terdaftar, tapi belum pernah dijalankan.

## Yang sengaja TIDAK dibuat

`InvokeOpenFlow` dan `InvokeRemoteOpenRPA` khusus untuk OpenFlow, layanan yang
tidak dipakai JakForge; padanannya adalah **Invoke Workflow** yang sudah ada.
`Detector` bukan activity biasa melainkan sumber pemicu, jadi tempatnya di
Pemicu & Jadwal ForgeHub, bukan di kanvas. `StopOpenRPA` belum dibuat karena
menghentikan robot dari dalam workflow perlu jalur ke runner-nya dulu.

---

# Ronde: Terminal diuji, Database dibuat

## Catatan 5 — Terminal

Semua activity terminal lolos validator WF tanpa cacat struktur. Yang ditemukan
justru dua hal lain.

**Koordinat terbalik di Terminal Session.** `TerminalCoordinates` mendokumentasikan
bahwa Open3270 memakai `x = KOLOM` lebih dulu dan keduanya berbasis 0, sementara
properti activity berbasis 1. Aturan itu sudah dipatuhi Wait For Terminal Text —
di sana bahkan ada komentar "PERBAIKAN: sebelumnya TERBALIK". Tapi Terminal
Session masih memanggil:

    emulator.WaitForText(waitRow, waitCol, ...)

Salah dua kali: urutannya tertukar, dan tidak ada konversi basis. Menunggu teks
di baris 5 kolom 20 sebenarnya memeriksa baris 20 kolom 5.

Kesalahan yang sama pernah diperbaiki di satu tempat dan tertinggal di tempat
lain — akibat aturannya hidup di KOMENTAR, bukan di fungsi yang dipanggil semua
orang. Sekarang Terminal Session memakai `TerminalCoordinates` seperti yang lain.

**PF/PA tidak divalidasi.** `PF25` menghasilkan nama tombol yang ditolak jauh di
dalam Open3270, dengan pesan yang tidak menyebut PF Number sama sekali. Sekarang
ditolak di activity-nya, dengan batas yang jelas: PF 1-24, PA 1-3.

Dan satu kecurigaan yang TIDAK terbukti: `CacheMetadata` di Terminal Session
mendaftarkan RuntimeArgument secara manual tanpa `metadata.Bind`, yang biasanya
membuat argumennya tidak tersambung. Diuji dengan menyambung ke alamat lokal
yang pasti menolak, lalu membaca pesan galatnya:

    LULUS  Host terikat (alamatnya muncul di galat)  -> connect to 127.0.0.1 on port 9 failed
    LULUS  ContinueOnError menelan gagal koneksi     -> tidak melempar
    LULUS  Host kosong ditolak                       -> ... connecting to the host ''

Pengikatannya benar. Dugaan saya salah, dan pengukurannya yang membuktikan.

**Yang BELUM diuji**: sesi TN3270 sungguhan ke mainframe. Yang teruji adalah
pengikatan argumen, jalur kegagalan, dan metadata.

## Catatan 6 — Database

Tidak ada activity basis data JakForge sama sekali; yang ada masih milik
OpenRPA. Jadi `Custom.Database` dibuat: Database Scope, Execute Query, Execute
Non Query, Execute Scalar, dan Write DataTable To Database.

**Perbedaan pokoknya: parameter.** Di activity lama, dukungan parameter tidak
pernah ada — barisnya tertulis tapi dikomentari:

    // if (Params != null) cmd.Parameters.AddRange(Params);

Sehingga satu-satunya cara menyusun kueri adalah menyambung string. Itu tiga
kegagalan sekaligus: nilai dengan tanda kutip merusak kuerinya, tanggal
bergantung pada pengaturan wilayah, dan isian dari luar bisa menjadi perintah.
Semua kueri di sini berparameter.

**Transaksi.** Database Scope bisa membungkus seluruh Body dalam satu transaksi:
berhasil semua, atau tidak sama sekali. Itu yang membuat robot aman diulang —
jalan yang gagal di tengah tidak meninggalkan separuh perubahan.

**Pesan galat yang menjawab pertanyaan berikutnya.** Provider yang tidak
terdaftar dulu hanya menghasilkan "unable to find the requested provider" tanpa
memberi tahu apa yang tersedia. Sekarang daftarnya ikut disebut.

### Diuji terhadap SQL Server LocalDB yang sungguhan

Tabelnya dibuat di tempdb dan dibuang lagi oleh ujinya sendiri.

| Pemeriksaan | Hasil |
|---|---|
| INSERT berparameter tidak melempar | LULUS |
| Apostrof dan kutip ganda tersimpan apa adanya | LULUS |
| Execute Query: 1 baris, 3 kolom | LULUS |
| Body yang gagal tetap melempar | LULUS |
| Transaksi dibatalkan: tetap 1 baris | LULUS |
| Write DataTable menulis 1 baris | LULUS |
| Provider salah menyebut yang tersedia | LULUS |
| ContinueOnError menelan kueri yang gagal | LULUS |

Nilai ujinya sengaja `O'Brien "Bos"` — persis bentuk yang merusak kueri kalau
disambung sebagai string.

Satu pemeriksaan sempat gagal, dan itu UJINYA yang salah: `Akar()` mengambil
galat terdalam, sementara pesan kita ada di pembungkusnya.

### Yang diperiksa selain itu

    validator WF atas seluruh Custom.*  : 108 activity, 0 bermasalah
    solusi penuh                        : 0 galat
    Custom.Database.dll ter-deploy      : md5 cocok dengan hasil build
    lima activity terdaftar             : terbukti lewat refleksi

`Custom.Database` sudah masuk ke berkas solusi dan dirujuk OpenRPA, jadi ia ikut
muncul di toolbox sebagai kategorinya sendiri.

---

# Ronde: orchestrator yang bisa dipakai untuk daftar panjang

## Catatan 1 dan 13 — tabel data

Yang paling kurang dari daftar di ForgeHub bukan tampilannya, melainkan
fungsinya: tidak ada pengurutan, tidak ada penomoran halaman, tidak ada
pemilihan baris. Daftar dengan dua ratus pekerjaan tidak bisa dibaca tanpa itu.

Sebelumnya tiap halaman menyusun `<table>`-nya sendiri, jadi ketiganya harus
ditulis ulang tiap kali — dan karena mahal, tidak pernah ditulis sama sekali.
Sekarang ada SATU komponen `dataTable(key, columns, rows, opts)` yang dipakai
bersama.

Keadaannya disimpan DI LUAR fungsi render, karena render menggambar ulang
seluruh halaman: kalau keadaannya ikut digambar ulang, urutan dan halaman yang
sedang dilihat orang akan kembali ke awal tiap kali daftarnya disegarkan
sendiri.

Beberapa keputusan yang perlu dicatat:

**Angka diurutkan sebagai ANGKA.** Tanpa memisahkannya dari teks, "10" berada
sebelum "9" — dan kolom Lama serta Dijalankan seluruhnya angka.

**Panah urut selalu terlihat**, cuma pudar saat kolomnya tidak sedang
mengurutkan. Kalau baru muncul ketika aktif, tidak ada yang menyangka kolomnya
bisa diklik sama sekali.

**"Pilih semua" hanya untuk baris yang TAMPIL.** Memilih seluruh isi tabel dari
satu kotak centang membuat orang menghapus lebih banyak daripada yang mereka
lihat.

**Kotak cari ditunda 350 ms**, dan fokusnya dikembalikan sesudah render. Tanpa
itu kursor melompat keluar dari kotaknya tiap ketukan tombol.

### Kolom Lama, yang sebelumnya tidak ada

Halaman Pekerjaan sekarang punya kolom Lama — dan diurutkan menurut DETIK, bukan
menurut teksnya. Kalau menurut teks, "9 dtk" berada sesudah "10 mnt".

### Sub-tab

Proses, Pekerjaan, Pemicu, Paket, dan Log adalah satu alur kerja yang sama.
Sebagai lima butir terpisah di menu samping, orang harus mengingat di mana
masing-masing berada; sebagai tab, hubungannya terlihat sendiri.

### Tindakan borongan

Baris terpilih bisa dijalankan atau dihentikan sekaligus. Kegagalan satu tidak
menghentikan sisanya — pekerjaan yang sudah selesai sendiri di antaranya tidak
boleh membatalkan penghentian yang lain — dan yang dilaporkan adalah berapa
yang benar-benar berhasil.

## Catatan 2 — dialog pemicu

Dialognya sekarang dua kolom dengan bagian yang dipisahkan: kiri tentang APA
yang dijalankan, kanan tentang KAPAN. Isinya: nama, proses, robot, prioritas
pekerjaan, jenis runtime, zona waktu, frekuensi, dan pengulangan.

**Frekuensi disimpan sebagai MENIT**, satu angka, bukan pasangan (satuan,
jumlah). Penjadwal hanya perlu tahu berapa lama jaraknya; menyimpan satuannya
juga berarti ada dua sumber kebenaran yang bisa saling berbeda.

Tiga kolom baru — `priority`, `timezone`, `runtime_type` — perlu migrasi.
"CREATE TABLE IF NOT EXISTS" tidak menyentuh tabel yang sudah ada, jadi
pemasangan lama tidak akan pernah mendapatkannya dari sana. SQLite juga tidak
punya "ADD COLUMN IF NOT EXISTS", jadi kolomnya diperiksa dulu lewat PRAGMA —
tanpa itu, menjalankan ForgeHub kedua kali gagal dengan "duplicate column name".

## Yang diuji ronde ini

| Pemeriksaan | Hasil |
|---|---|
| Halaman pertama menampilkan 3 baris | LULUS |
| Kaki menyebut 1 – 3 / 7 | LULUS |
| Tombol halaman pertama nonaktif di halaman 1 | LULUS |
| Urut angka menaik dan menurun | LULUS |
| 10 tidak mendahului 9 | LULUS |
| Cari menyisakan 1 baris | LULUS |
| Hasil kosong: pesan, bukan tabel kosong | LULUS |
| Bilah pilih menyebut jumlah terpilih | LULUS |
| Kotak baris terpilih tercentang | LULUS |
| Halaman berlebih dijepit ke halaman terakhir | LULUS |
| Dialog memakai grid dua kolom | LULUS |
| Judul bagian digambar dan BUKAN isian | LULUS |
| Isian wide menutupi dua kolom | LULUS |

16 pemeriksaan, semuanya lulus. Satu sempat gagal karena harapan UJINYA yang
keliru: kotak centang hanya digambar untuk baris yang tampil, sementara
pengurutan saat itu menaruh baris terpilihnya di halaman lain.

Ditambah: ForgeHub `dotnet build` 0 galat, dan dasbornya dimuat tanpa galat
console.

---

# Ronde: ReFramework diuji, dan bahasa untuk orchestrator

## Catatan 3 — ReFramework

Diuji pada tingkat yang benar-benar penting: berkas yang DIHASILKAN template
harus bisa dimuat dan lolos validasi. Template yang terkompilasi tapi
menghasilkan XAML yang tidak bisa dibuka adalah template yang gagal, dan
kegagalannya baru terlihat sebagai kotak merah di kanvas.

Ketujuh berkas — Main, InitAllSettings, InitAllApplications, GetTransactionData,
Process, SetTransactionStatus, CloseAllApplications — dimuat oleh
`WorkflowLoader` milik JakRunner dan lolos validasi WF tanpa satu pun cacat
struktur. Config.json sah sebagai JSON, README ada.

Sub-workflow-nya juga sudah mendeklarasikan argumen yang benar: Config,
TransactionNumber, TransactionItem, SystemException, BusinessException,
RetryNumber.

### Dua cacat nyata yang ditemukan pengujian

**Berkas tetap terkunci sesudah dimuat.** Uji ini gagal membuang folder
sementaranya, dan itu bukan kesalahan uji:

    LULUS  sebelum dimuat, berkas bisa ditulis   -> bisa
    GAGAL  sesudah dimuat, berkas bisa ditulis   -> TERKUNCI
    LULUS  sesudah GC, berkas bisa ditulis       -> bisa

Penyebabnya overload `XamlXmlReader(path, ...)`: ia membuka berkasnya sendiri
dan TIDAK menutupnya saat reader-nya dibuang — berkasnya baru terlepas ketika
pengumpul sampah kebetulan berjalan. Akibatnya nyata: JakRunner yang pernah
memuat sebuah proyek menahan berkas .xaml-nya, sehingga penerbitan ulang dari
Studio bisa gagal karena berkasnya sedang dipakai.

Alirannya sekarang dibuka sendiri di dalam `using`, jadi penutupannya pasti.
Sesudah perbaikan: tiga-tiganya lulus.

**Argumen pekerjaan tidak pernah sampai ke workflow.** ForgeHub sudah lama
mengirim `input_json` di muatan penugasannya, tapi rantainya putus di dua
tempat: `ClaimedJob` hanya membaca Id dan ProcessName, dan `RunnerEngine`
memanggil `new WorkflowApplication(activity)` tanpa argumen sama sekali.

Pekerjaan yang dijadwalkan dengan argumen karena itu selalu berjalan dengan
nilai bawaan — tanpa satu pun tanda bahwa argumennya hilang.

Rantainya sekarang tersambung, dengan satu penjagaan: hanya argumen yang MEMANG
dideklarasikan workflow-nya yang diteruskan. WorkflowApplication melempar kalau
diberi nama yang tidak dikenal, dan menolak seluruh jalan karena satu nama yang
tidak cocok adalah hukuman yang tidak sebanding — jadi yang tidak dikenal
dicatat di log dan sisanya tetap dijalankan. Argumen KELUARAN dilewati, karena
mengisinya sebagai masukan ditolak runtime.

Diuji dengan workflow kecil yang punya `in_Nama` dan `out_Sapaan`:

| Pemeriksaan | Hasil |
|---|---|
| Workflow dimuat sebagai DynamicActivity | LULUS |
| Argumen yang dideklarasikan terbaca | LULUS |
| Argumen masukan sampai ke workflow ("Halo Fajar") | LULUS |
| Tanpa penyaringan, nama asing menjatuhkan jalan | LULUS |

Baris terakhir itu yang membuktikan penyaringannya perlu.

## Catatan 10 — bahasa untuk orchestrator

Indonesia, Inggris, dan Jawa. Yang perlu dicatat adalah bentuk KUNCI-nya:

    t('Jalankan')       bukan     t('btn.run')

Kuncinya teks Indonesianya sendiri. Dua alasan. Teks yang belum diterjemahkan
jatuh kembali ke bahasa Indonesia yang BENAR — bukan menjadi "btn.run" yang
bocor ke layar. Dan sumbernya tetap bisa dibaca: `t('Jalankan')` sudah
menjelaskan dirinya, sementara `t('btn.run')` menuntut orang membuka kamusnya
dulu.

Kamus yang belum lengkap karena itu tidak pernah merusak antarmuka; ia hanya
membuat sebagian tetap berbahasa Indonesia.

107 kunci per bahasa, dan kedua kamus menutupi kunci yang SAMA — diperiksa oleh
ujinya, supaya tidak ada bahasa yang diam-diam tertinggal.

Terjemahannya dipasang di tempat yang dipakai bersama — `head()`, `panel()`,
`stateTag()`, kepala kolom tabel, label dialog, dan menu — sehingga satu
sentuhan berlaku untuk semua halaman sekaligus.

**Pemilihnya ada di layar masuk, bukan hanya di bilah atas.** Orang yang tidak
membaca bahasa Indonesia harus bisa mengganti bahasanya SEBELUM masuk; bilah
atas baru terlihat sesudah berhasil masuk. Keduanya disamakan saat salah
satunya diubah.

Pilihannya disimpan di peramban, per orang — bukan di server: bahasa adalah
preferensi orang yang sedang melihat layarnya, bukan setelan penyewa.

### Yang diuji

| Pemeriksaan | Hasil |
|---|---|
| id: teks dikembalikan apa adanya | LULUS |
| en / jv: kata dikenal diterjemahkan | LULUS |
| en / jv: kata TAK dikenal jatuh ke Indonesia | LULUS |
| Tidak ada nilai berbentuk kode yang bocor | LULUS |
| Kamus en dan jv punya kunci yang sama | LULUS |
| Menu benar-benar berubah, dan bisa kembali | LULUS |
| Judul dan keterangan halaman diterjemahkan | LULUS |
| Tombol Masuk: Sign in / Mlebu / Masuk | LULUS |
| Pemilih atas ikut saat diubah dari layar masuk | LULUS |
| Pilihan tersimpan di peramban | LULUS |

17 pemeriksaan bahasa, semuanya lulus. Solusi penuh 0 galat; ForgeHub 0 galat.

### Yang BELUM diterjemahkan

Kamusnya menutupi menu, judul halaman, kolom tabel, tombol, label keadaan,
dialog pemicu, dan layar masuk. Pesan toast, teks bantuan, dan isi dialog
lain masih berbahasa Indonesia — dan karena kuncinya teks Indonesianya sendiri,
semuanya tetap terbaca utuh, hanya belum berganti bahasa.

---

# Ronde: bahasa dan mode gelap untuk Studio dan Assistant

## Setelannya SATU, dipakai berdua

Permintaannya tegas: asisten mengikuti setelan Studio. Kalau keduanya menyimpan
sendiri-sendiri, "mengikuti" berarti menyalin, dan salinan selalu bisa
ketinggalan. Jadi setelannya satu berkas:

    %LOCALAPPDATA%\JakForge\ui.json

Studio menulisnya, JakRunner membacanya. JakRunner sengaja TIDAK punya pemilih
sendiri: dua pemilih untuk satu nilai hanya menciptakan pertanyaan "yang mana
yang menang".

Berkasnya JSON yang ditulis tangan tanpa pustaka: isinya dua kata, dan menarik
seluruh serializer untuk dua kata bukan pertukaran yang masuk akal. Berkas yang
rusak atau tidak ada menghasilkan NILAI BAWAAN, bukan kegagalan — setelan
tampilan tidak boleh menghalangi program berjalan.

JakRunner memeriksanya ulang tiap kali jendelanya kembali aktif. Orang yang baru
mengganti tema di Studio lalu berpindah ke asisten langsung melihat hasilnya,
tanpa menjalankan ulang apa pun.

## Catatan 9 dan 11 — bahasa

Indonesia, Inggris, Jawa. Kuncinya teks Indonesianya sendiri, sama seperti di
ForgeHub — jadi teks yang belum diterjemahkan jatuh kembali ke kalimat yang
BENAR, bukan menjadi kode yang bocor ke layar.

43 kunci per bahasa, dan kedua kamus menutupi kunci yang sama.

## Catatan 12 — mode gelap

Warna dipisahkan dari gaya. Tiap program sekarang punya dua palet dengan kunci
yang SAMA PERSIS, dan rujukan warnanya diubah dari `StaticResource` menjadi
`DynamicResource` — itulah yang membuat tema bisa berganti tanpa menjalankan
ulang program.

    JakRunner : RunnerPalette.Light.xaml / .Dark.xaml     16 kuas
    Studio    : JakForgePalette.Light.xaml / .Dark.xaml   17 warna + 17 kuas

Rujukan GAYA sengaja dibiarkan statis: setter di dalamnya sudah dinamis, jadi
warnanya tetap ikut berganti.

Palet gelapnya bukan warna terang yang dibalik. Cokelat tua yang di mode terang
dipakai untuk TEKS menjadi krem di mode gelap, dan krem yang dipakai untuk LATAR
menjadi cokelat sangat tua: PERANANNYA yang ditukar, bukan angkanya. Merah dan
hijau penanda status dinaikkan kecerahannya dan diturunkan kejenuhannya supaya
terbaca di atas latar gelap tanpa menyilaukan.

### Satu keputusan yang diubah oleh pengujian

Mula-mula kuas Studio merujuk warnanya lewat `DynamicResource` ke entri Color di
kamus yang sama. Ujinya gagal: kamus itu tidak bisa diperiksa berdiri sendiri,
karena rujukannya belum terpecahkan di luar aplikasi.

Kuasnya sekarang membawa warnanya LANGSUNG. Warnanya sama, tapi kamus yang tidak
bergantung pada apa pun bisa diperiksa sendiri — dan yang bisa diperiksa itulah
yang bisa dijamin benar. Entri Color tetap ada karena `GradientStop` menuntut
Color, bukan Brush.

## Yang diuji

| Pemeriksaan | Hasil |
|---|---|
| Setelan tersimpan ke berkasnya | LULUS |
| Bahasa dan tema bertahan sesudah dibaca ulang | LULUS |
| id: teks apa adanya | LULUS |
| en / jv: kata dikenal diterjemahkan | LULUS |
| en / jv: kata TAK dikenal jatuh ke Indonesia | LULUS |
| Kamus en dan jv punya kunci yang sama (43) | LULUS |
| JakRunner: kedua palet punya kunci sama (16) | LULUS |
| JakRunner: semua kuas benar-benar berbeda warnanya | LULUS |
| Studio: kedua palet punya kunci sama (34) | LULUS |
| Studio: semua kuas benar-benar berbeda warnanya | LULUS |

19 pemeriksaan, semuanya lulus. Ujinya mengembalikan setelan pengguna seperti
semula sesudah selesai.

Ditambah: solusi penuh 0 galat, dan validator activity tetap 108 diperiksa,
0 bermasalah — perubahan tema tidak menyenggol satu pun activity.

## BATAS yang perlu diketahui

Mode gelap Studio berlaku untuk permukaan JakForge: layar mulai, daftar proyek,
panel Output, kanvas, dan kartu activity. **Jendela bawaan OpenRPA tetap
terang**, karena tema JakForge sengaja tidak memuat gaya implisit — komentar di
App.xaml sudah menyatakannya sejak awal. Memaksakan gaya implisit akan mengubah
tampilan kontrol yang sudah bekerja, dan itu risiko yang tidak sebanding dengan
warnanya.

Kamus bahasanya menutupi menu dan teks tetap JakRunner, jendela Tampilan, dan
label keadaan. Pesan log, keterangan kesalahan, dan pita Studio masih berbahasa
Indonesia — dan karena kuncinya teks Indonesianya sendiri, semuanya tetap
terbaca utuh.

---

# Ronde: tombolnya tidak ketemu

Setelan tampilan dipasang, terbangun, dan ter-deploy dengan benar — tapi tidak
ada yang bisa menemukannya. Itu bukan keluhan kecil: **setelan yang tidak bisa
ditemukan sama saja dengan tidak ada.**

## Sebabnya

Saya menaruhnya HANYA di menu aplikasi pita, yang dibuka dengan mengklik logo
kecil di pojok kiri-atas. Logo itu tanpa tulisan, dan tidak ada apa pun yang
menyiratkan bahwa ia sebuah menu.

Alasan saya menaruhnya di sana masuk akal di atas kertas — setelan yang diubah
sekali lalu ditinggalkan tidak pantas memakan tempat di pita. Tapi alasan itu
hanya berlaku kalau orangnya SUDAH tahu setelan itu ada. Untuk sesuatu yang
baru, tempat yang benar adalah tempat yang terlihat.

## Yang diperbaiki

Tombol **Tampilan** sekarang ada di pita, di tab Design, di dalam grup bahasa
yang sudah ada — bersebelahan dengan pemilih locale bawaan OpenRPA. Keduanya
urusan yang sama di kepala orang yang mencarinya, jadi tempatnya juga sama.

Ikonnya dibuat sendiri: lingkaran separuh terang separuh gelap, penanda yang
sudah lazim untuk pemilihan tema.

Butir di menu aplikasi TETAP ada. Yang satu untuk ditemukan, yang satu untuk
dipakai orang yang sudah hafal tempatnya.

## Yang diuji

Bukan dengan membaca sumbernya: yang dijalankan orang adalah BAML di dalam exe.
Jendelanya dimuat sungguhan dari rakitan yang ter-deploy, lalu pohon logisnya
ditelusuri.

| Pemeriksaan | Hasil |
|---|---|
| MainWindow.xaml bisa dimuat dari rakitan ter-deploy | LULUS |
| Tombol pita "Tampilan" ada, tepat satu | LULUS |
| Tombolnya punya ikon besar | LULUS |
| Tombolnya punya keterangan | LULUS |
| Butir menu aplikasi "Tampilan" juga masih ada | LULUS |
| Pemilih locale bawaan OpenRPA MASIH ada | LULUS |

Baris terakhir yang penting: tombol baru tidak boleh menggeser apa yang sudah
bekerja.

## Satu jebakan perkakas lagi

Uji versi pertama tidak menghasilkan apa pun — keluar dengan kode 0, tanpa satu
baris pun. Dua sebabnya sekaligus:

- ia merujuk `System.Windows.Controls.Ribbon`, yang TIDAK ada di folder keluaran
  (Studio mengambilnya dari GAC), sehingga uji itu gagal dimuat sebelum satu
  baris pun berjalan;
- proses WPF tidak selalu punya konsol yang tersambung, jadi kalaupun berjalan,
  hasilnya tidak akan terlihat.

Versinya sekarang menyentuh tipe pita lewat REFLEKSI — dikenali dari nama
tipenya, bukan dari tipenya — dan menulis hasilnya ke berkas selain ke konsol.
Hasil uji yang tidak terlihat sama saja dengan uji yang tidak dijalankan.

---

# Ronde: tombol yang tidak ada, cron, X, dan versi paket

## Catatan 1 — tidak ada tombol Apply

Tombolnya ADA. Yang tidak ada adalah ruangnya.

Jendela Tampilan memakai `Height="330"`. Pada Window, angka itu tinggi TOTAL —
termasuk bilah judul dan bingkainya. Diukur di mesin ini:

    tinggi isi yang diminta : 326 px
    tinggi bingkai          :  37 px
    ruang tersisa untuk isi : 293 px

Baris tombol karena itu jatuh 33 piksel di bawah tepi jendela: terlihat benar di
kode, tidak terlihat sama sekali di layar. Dan karena `ResizeMode="NoResize"`,
jendelanya bahkan tidak bisa ditarik supaya muat.

Sekarang `SizeToContent="Height"`. Angka tetap akan memotong LAGI begitu
bahasanya diganti, karena panjang keterangannya berbeda antar bahasa — jadi
yang benar bukan angka yang lebih besar, melainkan tidak ada angka sama sekali.

## Catatan 4 — versi paket tidak pernah naik

Penyebabnya satu baris di tempat pemanggilnya:

    result = client.PublishProject(folder, project.name, "1.0.0", ...)

Versinya dipatok mati. Setiap penerbitan menimpa versi yang sama, riwayatnya
tidak pernah bertambah, dan tidak ada cara mengetahui paket mana yang sedang
dijalankan robot.

Sekarang Studio menanyakan daftar paket, mencari versi tertinggi untuk nama itu,
lalu menaikkan angka KETIGA. Angka pertama dan kedua urusan orang yang
memutuskan sesuatu berubah besar; menerbitkan ulang dari Studio bukan keputusan
seperti itu.

Gagal menanyakan daftarnya TIDAK menghalangi penerbitan: yang dipakai "1.0.0".
Menolak menerbitkan hanya karena nomor versinya tidak bisa dihitung adalah
hukuman yang tidak sebanding.

## Catatan 2 — cron untuk pemicu

Penilai cron lima ruas ditulis sendiri: `*`, angka, daftar `1,15`, rentang
`1-5`, dan langkah `*/15`. Ditulis sendiri karena yang dibutuhkan hanya "kapan
berikutnya", dan menambah paket untuk seratus baris yang bisa diuji tuntas bukan
pertukaran yang masuk akal.

Satu aturan yang mudah terlewat dan sengaja diuji: **kalau TANGGAL dan HARI
dua-duanya dibatasi, yang berlaku adalah salah satu cocok, bukan keduanya.** Itu
perilaku cron asli, dan orang yang menulis `0 0 1 * 3` mengharapkan tanggal 1
ATAU tiap Rabu.

Ekspresinya divalidasi saat pemicunya dibuat, bukan dibiarkan sampai penjadwal:
ekspresi yang salah baru ketahuan pada putaran berikutnya, dan orang yang
menekan "Buat pemicu" sudah pergi. Kalau toh lolos, penjadwal MEMATIKAN
pemicunya dan menerbitkan peringatan — bukan mencoba lagi tiap 30 detik
selamanya tanpa penjelasan.

Prioritas pekerjaan sekarang juga diambil dari pemicunya; sebelumnya selalu
'Normal' betapapun pemicunya disetel.

### Cron diuji: 24 pemeriksaan, semuanya lulus

| Contoh | Hasil |
|---|---|
| `* * * * *` dari 10:15:30 | 10:16 |
| `0 * * * *` dari 10:15 | 11:00 |
| `*/15 * * * *` dari 10:16 | 10:30 |
| `0 7 * * 1-5` dari Jumat 10:00 | Senin 07:00 |
| `0 8 * * 0` dan `0 8 * * 7` | sama-sama Minggu |
| `0 0 1 * 3` | tanggal 1 ATAU Rabu |
| `0 0 29 2 *` | 2028-02-29 |
| empat ruas, menit 60, bulan 13, rentang terbalik | ditolak |

## Catatan 3 — tombol X di dialog

Ditambahkan ke ketiga dialog. Tapi yang penting BUKAN tombolnya:

**Menutup dialog dari luar harus MENYELESAIKAN janjinya.** Tanpa itu,
pemanggilnya menunggu selamanya dan halaman berhenti bereaksi tanpa satu pun
pesan. Klik latar yang sudah ada sejak dulu punya bocor persis itu — sekarang
ikut diperbaiki. Esc juga menutup, karena dialog yang tidak bisa ditutup dengan
Esc terasa seperti jebakan.

Ditutup dari luar berarti **Batal** pada form, dan **TIDAK** pada konfirmasi.
Menganggapnya "ya" akan menghapus sesuatu yang tidak diminta.

## Satu cacat yang menjelaskan kenapa X-nya diam

Uji pertama: tombolnya ada, tapi diklik tidak terjadi apa-apa.

Sebabnya bukan tombolnya. Saya menaruh `ACTIONS.closeDialog = ...` di baris 576,
sementara `const ACTIONS = {}` ada di baris 748 — **zona mati temporal**. app.js
melempar saat dimuat, dan SELURUH pendaftaran sesudah baris itu tidak pernah
berjalan. Yang terlihat cuma satu tombol yang diam.

Fungsi yang dideklarasikan dengan `function` tetap terangkat, jadi dialognya
masih bisa dibuka — itulah yang membuat cacatnya terlihat kecil padahal
separuh berkasnya mati.

## Yang diuji ronde ini

| Bagian | Hasil |
|---|---|
| Cron: 24 pemeriksaan | 24/24 |
| Dialog X / Esc / klik latar menyelesaikan janjinya | 12/12 |
| X pada konfirmasi berarti TIDAK | LULUS |
| Simpan tetap mengembalikan nilainya | LULUS |
| Dialog pemicu memuat pilihan cara dan isian cron | LULUS |
| Jendela Tampilan: Height=330 memang tidak cukup | LULUS |
| Jendela Tampilan: SizeToContent dan MinHeight benar | LULUS |
| Solusi penuh | 0 galat |
| ForgeHub | 0 galat |

---

# Ronde: tiga activity yang dulu sengaja dilewati

Ronde-ronde lalu saya melewati `Detector`, `StopOpenRPA`, dan `InvokeOpenFlow`
dengan alasan yang masuk akal masing-masing. Diminta mengerjakannya, dan
ternyata ketiganya memang punya padanan yang jujur di JakForge — bukan salinan
buta, melainkan bentuk yang sesuai dengan cara JakForge bekerja.

## Stop Robot — pengganti Stop OpenRPA

Menghentikan jalan dari DALAM workflow, ketika workflow sendiri menyimpulkan
tidak ada gunanya melanjutkan: berkas masukan kosong, antrean habis, prasyarat
tidak terpenuhi.

Bedanya dengan melempar pengecualian: berhenti TANPA menandai jalannya gagal,
karena berhenti lebih awal bukan kegagalan.

Kalau tidak ada host yang bisa menghentikan, permintaannya TIDAK diabaikan
diam-diam — pengecualian dilempar supaya jalannya tetap berhenti. Robot yang
diminta berhenti lalu terus berjalan adalah hasil paling buruk dari ketiga
kemungkinan.

## Wait For Signal dan Raise Signal — pengganti Detector

Detector milik OpenRPA memarkir workflow pada sebuah bookmark sampai plugin
detektor menembakkannya. Prinsipnya dipertahankan; yang dibuka adalah
sumbernya: siapa pun yang bisa memanggil `RobotControl.Raise` bisa
membangunkannya — cabang lain dari workflow yang sama, atau program yang
menjalankannya.

Workflow yang menunggu TIDAK memakai prosesor: ia benar-benar diparkir runtime,
bukan berputar memeriksa. Itulah kenapa activity ini NativeActivity dengan
bookmark, bukan CodeActivity yang tidur.

## Start Job — pengganti Invoke OpenFlow dan Invoke Remote OpenRPA

Keduanya menjalankan workflow di layanan OpenFlow; padanannya di JakForge
adalah menitipkan pekerjaan ke ForgeHub, yang lalu memilih robot.

Bedanya dengan Invoke Workflow: Invoke Workflow menjalankan .xaml DI SINI dan
menunggu. Start Job menitipkannya, bisa ke mesin lain, bisa nanti. Menunggu
selesai bersifat opsional dan bawaannya TIDAK menunggu — robot yang menahan
dirinya sampai robot lain selesai adalah dua robot yang sibuk untuk satu
pekerjaan.

Kalau memang ditunggu dan pekerjaannya GAGAL, yang menunggu ikut gagal.
Menunggu lalu mengabaikan hasilnya membuat penantiannya tidak ada artinya.

## Cacat rancangan yang ditemukan pengujian

Versi pertama `Wait For Signal` mengambil `WorkflowInstanceProxy` lewat
`context.GetExtension`, lalu memakainya untuk melanjutkan bookmark-nya sendiri.

Itu keliru: proxy itu BUKAN ekstensi. Yang didapat null, dan sinyalnya tidak
pernah sampai — `Raise` melaporkan "berhasil" karena penunggunya memang ketemu,
tapi workflow-nya tetap terparkir selamanya.

    LULUS  Raise menemukan penunggunya      -> True
    GAGAL  Payload sampai ke workflow       -> (kosong)
    GAGAL  penunggu dilepas sesudah bangun  -> mulai

Dua baris terakhir itu yang membongkarnya. Kalau ujinya berhenti pada "Raise
menemukan penunggunya", cacat ini akan lolos sebagai keberhasilan.

Rancangannya diperbaiki: yang bisa melanjutkan bookmark HANYA pemilik
WorkflowApplication, jadi caranya dititipkan host lewat
`RobotControl.BookmarkResumer`. Activity hanya mendaftarkan NAMA bookmark-nya.

## Yang diuji

| Pemeriksaan | Hasil |
|---|---|
| Raise tanpa penunggu: Delivered false | LULUS |
| Wait mendaftarkan nama sinyalnya | LULUS |
| Raise menemukan penunggunya | LULUS |
| Payload sampai ke workflow | LULUS |
| Penunggu dilepas sesudah bangun | LULUS |
| Stop Robot tanpa host: melempar | LULUS |
| Stop Robot tanpa host: activity sesudahnya TIDAK jalan | LULUS |
| Stop Robot dengan host: tidak melempar | LULUS |
| Host menerima alasannya | LULUS |

Ditambah: solusi penuh 0 galat, keempat activity terbukti terdaftar lewat
refleksi, dan validator WF naik dari 108 menjadi **112 activity, 0 bermasalah**.

## Yang masih BELUM diuji

`Start Job` belum dijalankan terhadap ForgeHub yang sungguhan — ia butuh proses
yang sudah diterbitkan dan robot yang siap. Yang sudah dipastikan: ia
terkompilasi, terdaftar, dan lolos validasi struktur.
