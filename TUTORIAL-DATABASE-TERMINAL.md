# Tutorial: Database dan Terminal di JakForge Studio

Dokumen ini dua bagian yang berdiri sendiri. Bagian A untuk activity Database,
bagian B untuk activity Terminal (TN3270). Masing-masing dimulai dari apa yang
perlu disiapkan lebih dulu, karena keduanya butuh sesuatu di luar Studio yang
kalau belum ada, activity-nya tidak akan pernah jalan sebaik apa pun
workflow-nya disusun.

---

# A. Database

Kelompok **Database** di panel Aktivitas berisi lima activity:

| Activity | Gunanya |
|---|---|
| **Database Scope** | Membuka koneksi, menjalankan isinya, lalu menutup koneksi |
| **Execute Query** | SELECT — hasilnya `DataTable` |
| **Execute Non Query** | INSERT / UPDATE / DELETE — hasilnya jumlah baris terpengaruh |
| **Execute Scalar** | Mengambil SATU nilai, mis. `SELECT COUNT(*)` |
| **Update From DataTable** | Menuliskan balik perubahan sebuah DataTable ke tabel asalnya |

## A.1 Yang perlu disiapkan

### Satu: pilih penyedia data (Data Provider)

JakForge memakai `DbProviderFactories` bawaan .NET Framework. Yang tersedia
tanpa memasang apa pun:

| Data Provider | Untuk |
|---|---|
| `System.Data.SqlClient` | Microsoft SQL Server, Azure SQL |
| `System.Data.OleDb` | Access, Excel, dan sumber OLE DB lain |
| `System.Data.Odbc` | Apa pun yang punya driver ODBC (MySQL, PostgreSQL, DB2, …) |
| `System.Data.OracleClient` | Oracle (usang, sebaiknya pakai ODBC) |

Kalau **Data Provider dikosongkan**, yang dipakai `System.Data.OleDb`.

Untuk MySQL atau PostgreSQL, pasang driver ODBC-nya lebih dulu
(MySQL Connector/ODBC, psqlODBC), lalu pakai `System.Data.Odbc`.

**Penting soal 32/64 bit.** Studio berjalan sebagai proses 64-bit, jadi driver
ODBC-nya juga harus 64-bit. Driver 32-bit tidak akan terlihat, dan pesan
gagalnya berbunyi "Data source name not found" — yang menyesatkan, karena
DSN-nya sebenarnya ada, cuma di daftar yang salah. Periksa lewat
`C:\Windows\System32\odbcad32.exe` (itu yang 64-bit), bukan
`C:\Windows\SysWOW64\odbcad32.exe`.

### Dua: siapkan connection string

Beberapa contoh yang sering dipakai:

```
SQL Server, autentikasi Windows
  Server=localhost;Database=NamaDB;Integrated Security=True;

SQL Server, username dan password
  Server=localhost;Database=NamaDB;User Id=sa;Password=rahasia;

MySQL lewat ODBC
  Driver={MySQL ODBC 8.0 Unicode Driver};Server=localhost;Database=NamaDB;User=root;Password=rahasia;

PostgreSQL lewat ODBC
  Driver={PostgreSQL Unicode(x64)};Server=localhost;Port=5432;Database=NamaDB;Uid=postgres;Pwd=rahasia;

Excel lewat OLE DB (baca .xlsx sebagai tabel)
  Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\data\buku.xlsx;Extended Properties="Excel 12.0 Xml;HDR=YES";
```

Jangan menulis password langsung di kartu activity kalau workflow-nya akan
dibagikan. Simpan connection string sebagai **argumen workflow** atau ambil
dari Credential Manager, lalu tunjuk namanya dari kartu.

## A.2 Langkah pemakaian

1. Seret **Database Scope** ke kanvas. Kotak **Do**-nya sudah berisi Sequence,
   jadi langsung bisa diisi beberapa perintah.
2. Isi propertinya:
   - **Data Provider** — mis. `"System.Data.SqlClient"` (pakai tanda kutip:
     isinya ekspresi VB, bukan teks polos)
   - **Data Source** — nama server, boleh dikosongkan kalau sudah ada di
     connection string
   - **Connection String** — seperti contoh di atas
3. Di dalam **Do**, seret **Execute Query**.
4. Isi **Query**, mis. `"SELECT id, nama FROM pegawai WHERE aktif = 1"`.
5. Klik kotak **DataTable**, tekan **Ctrl+K**, beri nama mis. `dt_pegawai`.
   Variabelnya otomatis bertipe `System.Data.DataTable` — tipe itu diambil dari
   properti activity-nya, jadi tidak perlu dibetulkan manual.
6. Setelah itu `dt_pegawai` bisa dipakai **For Each Data Row**, ditulis ke Excel
   lewat **Write Range**, dan seterusnya.

Koneksinya ditutup sendiri saat Do selesai — termasuk kalau ada langkah di
dalamnya yang gagal.

## A.3 Yang sering menyebabkan gagal

| Pesan | Sebabnya |
|---|---|
| `Unable to find the requested .Net Framework Data Provider` | Nama Data Provider salah ketik, atau providernya belum terpasang |
| `Data source name not found and no default driver specified` | Driver ODBC-nya 32-bit, sedangkan Studio 64-bit |
| `Login failed for user` | Username/password salah, atau SQL Server belum mengizinkan autentikasi campuran |
| `A network-related or instance-specific error` | Nama server salah, atau TCP/IP belum dinyalakan di SQL Server Configuration Manager |

Semua pesan itu masuk panel **Output** dan tersimpan di
`%LOCALAPPDATA%\JakForge\Logs\<tanggal>.txt`.

## A.4 Contoh utuh

```
Database Scope
  Data Provider     : "System.Data.SqlClient"
  Connection String : "Server=localhost;Database=HR;Integrated Security=True;"
  Do
    └─ Sequence
       ├─ Execute Query
       │    Query     : "SELECT nip, nama FROM pegawai WHERE aktif = 1"
       │    DataTable : dt_pegawai          (Ctrl+K, otomatis DataTable)
       ├─ For Each Data Row  in dt_pegawai
       │    └─ Log Message : row("nama").ToString()
       └─ Execute Non Query
            Query : "UPDATE pegawai SET disinkron = 1 WHERE aktif = 1"
```

---

# B. Terminal (TN3270)

Kelompok **Terminal** berisi tujuh activity:

| Activity | Gunanya |
|---|---|
| **Terminal Session** | Membuka koneksi TN3270 dan menjalankan isinya |
| **Type Into Terminal** | Mengetik teks ke posisi kursor atau ke baris/kolom tertentu |
| **Send Terminal Key** | Mengirim tombol AID: Enter, Tab, Clear, Home, Reset, SysReq, dan lainnya |
| **Get Terminal Text** | Membaca teks dari baris/kolom tertentu sepanjang N karakter |
| **Get Terminal Field** | Membaca isi sebuah field, dicari lewat indeks, label, atau posisi |
| **Set Terminal Field** | Mengisi sebuah field, dicari dengan cara yang sama |
| **Wait For Terminal Text** | Menunggu teks tertentu muncul sebelum lanjut |

Semuanya memakai pustaka **Open3270**, jadi yang didukung protokol **TN3270**
(mainframe IBM z/OS, AS/400 lewat gateway TN3270). Terminal VT100/SSH TIDAK
termasuk.

## B.1 Yang perlu disiapkan

1. **Alamat host dan port.** Port bakunya `23`. Banyak lingkungan memakai port
   lain (`992` untuk TLS, atau port khusus) — tanyakan ke administrator sistem.
2. **LU Name**, kalau host mewajibkan sesi dinamai. Kosongkan kalau tidak.
3. **Terminal Type.** Bakunya `IBM-3278-2-E` (layar 24×80). Kalau layar aplikasi
   Anda lebih besar, tipenya harus disesuaikan — model 3 (32×80), 4 (43×80),
   atau 5 (27×132). Salah model membuat pembacaan baris/kolom meleset semua.
4. **Akses jaringan.** Pastikan port host-nya tidak diblokir firewall. Uji cepat
   lewat PowerShell:

   ```powershell
   Test-NetConnection -ComputerName host.contoh.co.id -Port 23
   ```

5. **Peta layar.** Ini yang paling sering dilewati. Catat lebih dulu baris dan
   kolom setiap field yang mau dibaca atau diisi. Nomor baris dan kolom di
   activity ini dimulai dari **1**, bukan 0.

## B.2 Langkah pemakaian

1. Seret **Terminal Session** ke kanvas. Kotak **Do**-nya sudah berisi Sequence,
   dan argumennya bernama `session` — itu yang diisikan ke properti **Session**
   milik activity di dalamnya.
2. Isi **Host**, **Port**, dan bila perlu **LU Name** serta **Terminal Type**.
3. Isi **Wait For Text (opsional)** dengan teks yang pasti muncul di layar login,
   mis. `"LOGON"`. Tanpa ini, langkah pertama bisa jalan sebelum layarnya siap —
   penyebab paling umum workflow terminal yang "kadang jalan kadang tidak".
4. Di dalam **Do**:
   - **Set Terminal Field** untuk mengisi user dan password
   - **Send Terminal Key** dengan **Key** = `Enter`
   - **Wait For Terminal Text** untuk menunggu layar berikutnya
   - **Get Terminal Text** untuk membaca hasilnya

## B.3 Contoh utuh

```
Terminal Session
  Host           : "mainframe.contoh.co.id"
  Port           : 23
  Terminal Type  : "IBM-3278-2-E"
  Wait For Text  : "LOGON"
  Do
    └─ Sequence
       ├─ Set Terminal Field   Label: "Userid"    Text: "USER01"
       ├─ Set Terminal Field   Label: "Password"  Text: in_password
       ├─ Send Terminal Key    Key: Enter
       ├─ Wait For Terminal Text
       │     Text    : "MENU UTAMA"
       │     Timeout : 00:00:20
       │     Found   : sudahMasuk        (Ctrl+K, otomatis Boolean)
       ├─ Type Into Terminal   Row: 10  Column: 20  Text: "3"
       ├─ Send Terminal Key    Key: Enter
       └─ Get Terminal Text
             Row: 12  Column: 5  Length: 40
             Text: saldo                (Ctrl+K, otomatis String)
```


## B.4 Tombol PF dan PA

Tombol fungsi tidak dipilih sebagai `PF1`, `PF2`, dan seterusnya. **Key** disetel
ke `PF`, lalu nomornya diisi di **PF Number** (1–24). Begitu juga `PA` dengan
**PA Number** (1–3).

```
Send Terminal Key
  Key       : PF
  PF Number : 3          ' PF3, biasanya "kembali"
```

Selain itu tersedia tombol layar penuh seperti `Clear`, `Home`, `Reset`,
`EraseEOF`, `EraseInput`, `Tab`, `BackTab`, `FieldExit`, `SysReq`, dan `Attn`.
Untuk mengetik teks biasa, jangan pakai activity ini — pakai **Type Into
Terminal**.

## B.5 Cara menunjuk field

**Get/Set Terminal Field** bisa menemukan field dengan tiga cara. Pakai yang
paling atas yang tersedia — makin ke bawah makin rapuh terhadap perubahan layar.

1. **Label** — teks yang tercetak tepat sebelum field-nya, mis. `"Userid"`.
   Paling tahan banting: kalau layarnya bergeser, labelnya ikut bergeser.
2. **Row + Column** — posisi field di layar. Jelas, tapi patah begitu tata letak
   layarnya berubah.
3. **Index** — nomor urut field di layar. Paling rapuh: menambah satu field di
   mana pun sebelumnya menggeser semua nomor sesudahnya.

## B.6 Yang sering menyebabkan gagal

| Gejala | Sebabnya |
|---|---|
| Gagal connect, tidak ada pesan dari host | Port salah, atau diblokir firewall |
| Terhubung tapi layarnya kosong | Terminal Type tidak cocok dengan yang diminta host |
| Teks terbaca terpotong atau bergeser | Model layar salah (24×80 vs 32×80), atau baris/kolom dihitung dari 0 |
| Langkah pertama gagal padahal manual bisa | Belum memakai **Wait For Text**; layar belum siap saat langkah dijalankan |
| Kadang jalan kadang tidak | Sama seperti di atas — ganti jeda tetap dengan **Wait For Terminal Text** |

## B.7 Kebiasaan yang membuat workflow terminal tahan lama

- **Selalu tunggu, jangan pernah menjeda.** `Wait For Terminal Text` menunggu
  tepat sampai layarnya siap; `Delay 3 detik` menebak, dan tebakannya salah
  begitu jaringan sedang lambat.
- **Satu Terminal Session untuk satu urutan kerja.** Membuka-menutup koneksi
  per langkah membuat host mencatat puluhan sesi login.
- **Isi Timeout secara sadar.** Bakunya cukup untuk layar biasa, tapi laporan
  yang perlu diproses host bisa butuh menit — naikkan Timeout untuk langkah itu
  saja, bukan untuk semuanya.
- **Continue On Error hanya untuk langkah yang memang boleh gagal.**
  Mengaktifkannya di mana-mana membuat workflow tetap berjalan di atas layar
  yang salah, dan hasilnya baru ketahuan jauh di belakang.

---

# Kalau macet

Setiap kegagalan run sekarang menampilkan dialog yang menyebut **nama activity**
yang berhenti, dan rinciannya tersimpan di:

```
%LOCALAPPDATA%\JakForge\Logs\<tanggal>.txt
```

Berkas itu satu per tanggal dan tidak ikut terhapus saat panel Output
dikosongkan, jadi jalan yang gagal kemarin masih bisa dibaca ulang hari ini.
