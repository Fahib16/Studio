# Memasang JakForge Studio di komputer lain

## Cara singkat: pakai installer

Salin **`JakForgeStudioSetup.exe`** ke komputer tujuan dan jalankan. Tanpa hak
administrator, tanpa Visual Studio, tanpa git.

Yang dikerjakannya sendiri:

- menyalin program ke `%LOCALAPPDATA%\Programs\JakForge Studio`
- mendaftarkan native messaging host untuk **Chrome, Edge, dan Brave** (HKCU)
- mendaftarkan ekstensinya lewat `.crx` yang sudah ditandatangani
- membuat pintasan Start Menu dan Desktop
- mendaftarkan entri di Settings → Apps, lengkap dengan pencopotnya

Yang tersisa untuk Anda: **satu klik**. Saat peramban dijalankan berikutnya, ia
menampilkan gelembung "Ekstensi baru ditambahkan" — tekan **Aktifkan**.

Konfirmasi itu tidak bisa dilewati tanpa hak administrator, dan itu memang
disengaja Google: kalau ada jalannya, program apa pun bisa menanam ekstensi di
peramban orang tanpa sepengetahuannya. Yang berhasil dihilangkan adalah
Developer mode dan "Load unpacked" — dulu keduanya wajib.

### Membuat installernya

Di komputer pengembangan, satu perintah:

```powershell
powershell -ExecutionPolicy Bypass -File .\buat-installer.ps1
```

Skripnya membangun solution, memaketkan ekstensi jadi `.crx` dengan kunci
privat Anda, mengemas seluruh keluaran build jadi `payload.zip`, lalu
menanamnya sebagai resource di dalam `JakForgeStudioSetup.exe`.

Kunci privatnya dicari di `%LOCALAPPDATA%\JakForge\kunci-penandatangan\`.
Kunci itu **tidak** ikut dikemas — hanya `.crx` hasilnya yang ikut.

### Di mana semuanya disimpan

| Isi | Tempat |
|---|---|
| Program | `%LOCALAPPDATA%\Programs\JakForge Studio` |
| Setelan, basis data, tata letak, plugin | `%LOCALAPPDATA%\JakForge\Studio` |
| **Project Anda** | `Documents\JakForge\<Nama Project>` |

Tidak ada satu pun yang menyentuh `Program Files` atau `HKEY_LOCAL_MACHINE`,
dan itulah satu-satunya alasan pemasangan ini tidak meminta admin.

Documents sekarang berisi **project saja**. Setelan, `_shared`, dan `_orphans`
pindah ke folder data — dulu semuanya bercampur di satu folder, dan yang
tenggelam justru satu-satunya isi yang benar-benar milik Anda.

Data dari pemasangan lama di `Documents\OpenRPA` **disalin** otomatis saat
Studio pertama kali dijalankan. Disalin, bukan dipindah: kalau ada yang
meleset, aslinya masih utuh.

### Mencopot

Settings → Apps → JakForge Studio → Uninstall. Yang dihapus: program,
pintasan, pendaftaran peramban. Yang **tidak** dihapus: project dan setelan
Anda — keduanya berisi pekerjaan, jadi penghapusannya diserahkan kepada Anda.

---

# Cara panjang: memasang dari sumber

Berguna kalau Anda memang sedang mengembangkan Studio di komputer itu.

## 1. Bangun

```powershell
git clone <repo> Studio
cd Studio
```

Buka `OpenRPA.sln` di Visual Studio dan bangun, atau lewat baris perintah:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" OpenRPA.sln -m:1
```

Hasilnya ada di `debug\net462\`.

---

## 2. Setelan — tidak perlu disetel apa-apa

Studio menyalin `settings.default.json` (ikut di dalam proyek ini) ke folder
datanya sendiri pada saat pertama kali dijalankan di mesin yang belum punya
`settings.json`. Jadi pilihan seperti tombol batal, tingkat pencatatan,
klik virtual, dan setelan Windows-detector sudah benar sejak awal.

Salinannya hanya terjadi **sekali**. Sesudah itu berkas benihnya tidak
pernah dilirik lagi — perubahan yang Anda buat di komputer itu tidak akan
tertimpa saat Studio dijalankan lagi.

### Mode biasa (bawaan)

Data disimpan di `Documents\OpenRPA`, sama seperti sebelumnya:

```
Documents\OpenRPA\
  settings.json        <- disalin dari benih pada jalan pertama
  offline.db           <- proyek dan workflow Anda
  layout.config        <- tata letak jendela
  jakforge-crash.log
```

### Mode portabel (opsional)

Buat berkas kosong bernama `portable.txt` di sebelah `OpenRPA.exe`:

```powershell
New-Item -ItemType File debug\net462\portable.txt
```

Seluruh data pindah ke `debug\net462\jakforge-data\`, dan `Documents` tidak
disentuh sama sekali. Gunanya: **menyalin foldernya berarti menyalin seluruh
pemasangannya** — termasuk workflow, bukan cuma setelannya.

Cocok untuk membawa Studio di flashdisk, atau untuk memindahkan pekerjaan
antar komputer tanpa menyalin dua tempat yang berbeda.

Menghapus `portable.txt` mengembalikannya ke `Documents`. Datanya tidak ikut
berpindah sendiri — folder `jakforge-data` tetap di tempatnya.

---

## 3. Jembatan Chrome

```powershell
powershell -ExecutionPolicy Bypass -File .\pasang-jembatan-chrome.ps1
```

**Tanpa hak administrator.** Skripnya:

- mencari `Studio.NativeHost.exe` dan memilih yang **paling baru**, bukan
  urutan tetap — mencetak semua calon beserta tanggalnya supaya pilihannya
  bisa diperiksa;
- menulis manifest native messaging dengan jalur yang **dihitung**, bukan
  ditulis tangan;
- mendaftarkannya untuk Chrome, Edge, dan Brave sekaligus.

Lalu satu langkah yang memang tidak bisa diotomatiskan tanpa hak
administrator — lihat "Kenapa tidak bisa otomatis" di bawah:

1. Buka `chrome://extensions`
2. Nyalakan **Developer mode** di pojok kanan atas
3. **Load unpacked** → pilih folder `debug\net462\Extension`

> Pilih folder di **`debug\net462\Extension`**, bukan `Extension` di akar
> repositori. Build menyalinnya ke sana, jadi folder keluaran sudah lengkap
> dengan sendirinya — menyalin `debug\net462` ke komputer lain berarti
> ekstensinya ikut, tanpa perlu membawa repositorinya.

### Kalau setelah reload ID-nya masih yang lama

Itu normal. Chrome menetapkan ID ekstensi unpacked pada saat **dimuat
pertama kali**, lalu menyimpannya. Tombol **Reload** hanya memuat ulang
berkasnya — ID-nya tidak dihitung ulang, jadi field `key` yang baru tidak
dilirik.

Yang terjadi kemudian: Chrome membuat entri **kedua** dengan ID yang benar,
sementara entri lama tetap terdaftar dalam keadaan nonaktif.

Membuang yang lama saja **tidak cukup**, dan ini sudah terbukti di lapangan.
Selama masih ada dua entri yang menunjuk satu folder, Chrome gagal memuat
folder itu sama sekali — lihat "Dua catatan untuk satu folder" di bawah.
**Remove keduanya**, lalu **Load unpacked** sekali lagi.

ID-nya harus **`ofhgclecgpimnjjnegogaeikmfjicnib`**. Kalau berbeda, berarti
field `key` di `Extension\manifest.json` ikut terubah.

### Kenapa ID-nya sekarang tetap

Dulu `manifest.json` tidak punya field `key`, jadi Chrome menurunkan ID
ekstensi dari **jalur foldernya**. Di komputer lain jalurnya berbeda → ID
berbeda → `allowed_origins` di manifest native host tidak lagi cocok, dan
native messaging gagal **tanpa pesan apa pun**: ekstensi terpasang, Studio
jalan, tapi setiap perintah browser diam saja.

Sekarang `key` berisi kunci publik tetap, jadi ID-nya sama di mesin mana pun.

### Membatalkan

```powershell
powershell -ExecutionPolicy Bypass -File .\pasang-jembatan-chrome.ps1 -Batalkan
```

---

## Kenapa "Load unpacked" tidak bisa dihilangkan tanpa admin

Empat jalan diuji di Chrome **152.0.7977.76**. Hasilnya:

| Cara | Admin | Klik | Hasil |
|---|---|---|---|
| **Load unpacked** | tidak | 1 + Developer mode | **berhasil**, ID stabil berkat `key` |
| `chrome --load-extension=...` | tidak | 0 | **gagal** — argumennya diabaikan; Chrome mencabut dukungannya karena banyak disalahgunakan malware. Tetap gagal walau ditambah `--enable-unsafe-extension-debugging` |
| Kebijakan di `HKCU\Software\Policies\Google\Chrome` | tidak | 0 | **gagal** — Windows hanya memberi `ReadKey` pada cabang `Policies` untuk pengguna biasa, justru supaya kebijakan tidak bisa dipasang sendiri |
| Registry `HKCU\Software\Google\Chrome\Extensions` + `.crx` | tidak | 1 (aktifkan) | **terpasang tapi dinonaktifkan** Chrome (`disable_reasons: 256`) karena bukan dari Web Store |

Jadi tanpa hak administrator, satu langkah manual tidak bisa dihindari —
dan "Load unpacked" adalah yang paling sederhana di antaranya.

### Kalau boleh pakai admin sekali saat pemasangan

Kebijakan di **HKLM** (`ExtensionSettings` dengan `installation_mode:
normal_installed`) memasang ekstensi secara diam-diam dan tidak bisa
dimatikan pengguna. Itu mekanisme resmi Chrome Enterprise.

Belum diuji di sini karena butuh elevasi. Konsekuensinya juga perlu
ditimbang: ekstensinya harus dipaketkan jadi `.crx`, dan **setiap perubahan
`background.js` menuntut paket ulang plus versi dinaikkan** — jauh lebih
merepotkan daripada Load unpacked untuk komputer tempat Anda masih
mengembangkan.

### Kalau nanti dipakai banyak orang

Terbitkan ke Chrome Web Store sebagai **Unlisted**. Pemasangan di komputer
baru menjadi: buka tautannya, klik **Add to Chrome**. Tanpa Developer mode,
tanpa `.crx`, dan pembaruannya otomatis. Biayanya pendaftaran pengembang
satu kali, dan ada proses tinjauan — untuk `host_permissions: <all_urls>`
Google akan meminta penjelasan tertulis, yang normal untuk alat automasi.

---

## Yang TIDAK ikut berpindah, dan memang tidak seharusnya

| Berkas | Kenapa |
|---|---|
| `offline.db` | Proyek dan workflow — milik mesin itu, bukan bagian dari kode |
| `jwt`, `password`, `entropy` | Kredensial; `entropy` terikat ke akun Windows yang membuatnya |
| `mainwindow_position` | Ukuran layar berbeda; jendelanya bisa muncul di luar layar |
| `designerlayout`, `layout.config` | Tata letak panel, milik selera masing-masing |
| Kunci privat ekstensi | Kunci penanda tangan; hanya perlu kalau nanti dipaketkan jadi `.crx`. Sudah dipindah **keluar** dari folder `Extension`, ke `%LOCALAPPDATA%\JakForge\kunci-penandatangan\` — folder `Extension` itulah yang dimuat Chrome dan yang disalin orang ke komputer lain, jadi kunci yang menganggur di dalamnya ikut tersalin ke mana-mana |

Untuk memindahkan **workflow**, ekspor proyeknya dari Studio
(Design → Proyek → Export), atau salin `offline.db` secara sadar — bukan
lewat git.

---

## Kalau perintah browser diam saja

### Yang PERTAMA harus dicurigai: peramban belum dijalankan ulang

Peramban membaca daftar native messaging host **sekali saat dijalankan**.
Pendaftaran yang dibuat selagi peramban berjalan tidak terlihat olehnya, dan
jawabannya menyesatkan:

```
Specified native messaging host not found.
```

— untuk nama yang jelas-jelas ada di registry, dengan manifest yang sah.
**Menekan Reload pada ekstensi tidak menolong**; yang perlu dimuat ulang adalah
perambannya. Tutup semua jendela, semua profil, lalu buka lagi.

Skrip pemasangan sekarang memperingatkan ini sendiri kalau peramban sedang
berjalan, dan `-Periksa` menandainya sebagai `[GAGAL] urutan`.

### Cara tercepat mendapat jawaban: tanya Chrome

Menebak dari sisi ekstensi memakan waktu berjam-jam; Chrome bisa ditanya
langsung. Tutup Chrome, lalu jalankan:

```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --enable-logging --v=1
```

Sesudah ekstensinya mencoba menyambung, alasannya ada di
`%LOCALAPPDATA%\Google\Chrome\User Data\chrome_debug.log`:

```
WARNING:launch_context.cc] Can't find manifest for native messaging host <nama>
```

Itu memberi tahu apakah Chrome menemukan manifestnya atau tidak — perbedaan
yang tidak bisa disimpulkan dari pesan di console ekstensi.

### Kalau Chrome tetap "Can't find manifest" padahal registry benar

Ini pernah terjadi, dan sampai sekarang belum ada penjelasannya. Pada satu
mesin, Chrome 152 berhenti membaca pendaftaran **tingkat-pengguna** (HKCU)
sama sekali: satu nama host yang didaftarkan ke lima cabang registry sekaligus
tetap dijawab "Can't find manifest", sementara HKLM — satu-satunya tempat yang
masih dibacanya — tidak bisa ditulis tanpa hak administrator.

Yang sudah diperiksa dan TIDAK menjelaskannya: nama host, bentuk dan
penyandian manifest, ACL berkas, akun Windows dan SID proses Chrome, urutan
penyalaan, kebijakan (registry, cloud, kedua tampilan registry — log Chrome
sendiri berbunyi *"No machine level policy manager exists"* dan *"No policy
found on disk"*), catatan ekstensi di profil, dan versi Chrome.

Kalau ini terjadi, jangan habiskan waktu di mesin itu. Uji di komputer lain
lebih dulu — gejalanya tidak muncul di mesin yang belum pernah dipakai
bereksperimen dengan pendaftaran native messaging.

## Kalau perintah browser masih diam juga

Jangan menebak. Jalankan:

```powershell
powershell -ExecutionPolicy Bypass -File .\pasang-jembatan-chrome.ps1 -Periksa
```

Mode ini tidak mengubah apa pun. Ia memeriksa keenam sambungan yang harus
benar bersamaan — `.exe`, manifest native host, kunci registry, ID ekstensi,
catatan ekstensi di profil peramban, dan service worker yang benar-benar
berjalan — lalu menuliskan apa yang perlu dibereskan.

Alasan mode ini ada: keenamnya punya gejala yang **sama persis**, yaitu
Studio berkata "tidak bisa terhubung ke native host dalam 5 detik". Tanpa
alat, membedakannya berarti menebak satu per satu.

### Dua catatan untuk satu folder

Ini yang paling sulit dikenali, dan yang paling mungkin terjadi tepat setelah
`key` ditambahkan ke `Extension\manifest.json`.

Satu folder unpacked hanya boleh dipegang **satu** catatan di profil Chrome.
Menambahkan `key` mengubah ID ekstensinya, tetapi catatan lama tetap memegang
folder yang sama. Sejak itu Chrome gagal memuat foldernya dan hanya menulis
satu baris ke `chrome_debug.log`:

```
WARNING:load_error_reporter.cc] Failed to load extension from: ...\Extension.
```

Di layar `chrome://extensions`, ekstensinya tetap tampak terpasang dan aktif.
Registry benar, manifest benar, `.exe` benar — dan tidak ada yang berjalan.
`Studio.NativeHost.exe` tidak pernah dijalankan Chrome, jadi named pipe-nya
tidak pernah ada, jadi setiap activity browser gagal.

Perbaikannya manual dan hanya sekali:

1. buka `chrome://extensions`
2. **Remove** *semua* entri "Studio Automation Bridge" — termasuk yang
   kelihatan aktif, bukan cuma yang nonaktif
3. **Load unpacked** → pilih folder `Extension`
4. jalankan lagi `-Periksa` untuk memastikan semuanya hijau

### Penyebab lain yang pernah benar-benar terjadi

| Gejala | Sebab |
|---|---|
| Terpasang rapi, tetap tidak nyambung | manifest native host ditulis dengan **BOM**; pengurai JSON Chrome menolaknya tanpa pesan |
| Nyambung tapi perintahnya aneh | yang terdaftar `.exe` **Release** yang tertinggal berminggu-minggu, bukan Debug yang baru dibangun |
| Berubah setelah `background.js` disunting | Chrome tidak memuat ulang service worker sendiri; tekan **Reload** di `chrome://extensions` |
