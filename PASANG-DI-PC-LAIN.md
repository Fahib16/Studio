# Memasang JakForge Studio di komputer lain

Tiga langkah. Dua di antaranya satu perintah, satu perlu satu klik di Chrome.

---

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

- mencari `Studio.NativeHost.exe` (Release, lalu Debug, lalu `debug\net462`);
- menulis manifest native messaging dengan jalur yang **dihitung**, bukan
  ditulis tangan;
- mendaftarkannya untuk Chrome, Edge, dan Brave sekaligus.

Lalu satu langkah yang memang tidak bisa diotomatiskan — Chrome tidak
menyediakan jalan otomatis untuk ekstensi yang belum dipaketkan, dan itu
disengaja oleh Google:

1. Buka `chrome://extensions`
2. Nyalakan **Developer mode** di pojok kanan atas
3. **Load unpacked** → pilih folder `Extension`

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

## Yang TIDAK ikut berpindah, dan memang tidak seharusnya

| Berkas | Kenapa |
|---|---|
| `offline.db` | Proyek dan workflow — milik mesin itu, bukan bagian dari kode |
| `jwt`, `password`, `entropy` | Kredensial; `entropy` terikat ke akun Windows yang membuatnya |
| `mainwindow_position` | Ukuran layar berbeda; jendelanya bisa muncul di luar layar |
| `designerlayout`, `layout.config` | Tata letak panel, milik selera masing-masing |
| `Extension\kunci-privat.pem` | Kunci penanda tangan; hanya perlu kalau nanti dipaketkan jadi `.crx` |

Untuk memindahkan **workflow**, ekspor proyeknya dari Studio
(Design → Proyek → Export), atau salin `offline.db` secara sadar — bukan
lewat git.

---

## Kalau perintah browser diam saja

Urutan pemeriksaan, dari yang paling sering:

1. **ID ekstensi tidak cocok.** Buka `chrome://extensions`, bandingkan
   dengan ID di atas.
2. **Native host tidak terdaftar.** Jalankan ulang skrip pemasangannya;
   ia mencetak jalur yang dipakainya.
3. **`.exe`-nya belum dibangun.** Manifest menunjuk berkas yang tidak ada,
   dan Chrome gagal tanpa keterangan yang berguna.
4. **Ekstensi belum di-reload** sesudah `background.js` berubah. Chrome
   tidak memuat ulang service worker sendiri.
