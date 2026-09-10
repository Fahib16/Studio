# Memasang jembatan Chrome untuk Studio di mesin ini.
#
# Yang dikerjakan:
#   1. menulis manifest native messaging host, dengan jalur .exe DIHITUNG
#      dari letak skrip ini — bukan ditulis tangan;
#   2. mendaftarkannya ke registry untuk Chrome, Edge, dan Brave;
#   3. memeriksa bahwa berkas yang ditunjuknya benar-benar ada.
#
# Yang TIDAK bisa dikerjakan skrip: menekan "Load unpacked" di Chrome.
# Chrome memang tidak menyediakan jalan otomatis untuk ekstensi yang belum
# dipaketkan, dan itu disengaja oleh Google. Tapi karena manifest ekstensi
# sekarang memuat "key", ID-nya TETAP di mesin mana pun — jadi langkah itu
# cukup sekali, dan tidak ada lagi yang perlu disesuaikan sesudahnya.
#
# Jalankan TANPA hak administrator. Semuanya di HKCU.
#
#     powershell -ExecutionPolicy Bypass -File pasang-jembatan-chrome.ps1
#     powershell -ExecutionPolicy Bypass -File pasang-jembatan-chrome.ps1 -Periksa
#
# CATATAN BAGI YANG MENYUNTING BERKAS INI: simpan sebagai UTF-8 DENGAN BOM.
#
# Windows PowerShell 5.1 membaca skrip tanpa BOM sebagai ANSI. Tanda pisah "—"
# (UTF-8: e2 80 94) lalu terbaca sebagai tiga karakter, dan yang ketiga adalah
# tanda kutip penutup di Windows-1252. Di dalam string, kutip itu MENUTUP
# string-nya lebih awal, dan sisa berkas diurai sebagai kode. Galatnya muncul
# puluhan baris di bawah, di tempat yang sama sekali tidak bersalah.

[CmdletBinding()]
param(
    # Dipakai kalau .exe-nya ada di tempat lain, mis. hasil rilis.
    [string] $JalurExe,

    # Membatalkan pemasangan: manifest dan kunci registry dibuang.
    [switch] $Batalkan,

    # Memeriksa saja, tidak mengubah apa pun. Lihat bagian "Memeriksa".
    [switch] $Periksa,

    # ID ekstensi tambahan yang boleh menyambung. Dipakai kalau Chrome menolak
    # dengan "forbidden": console ekstensi mencetak ID yang sebenarnya
    # memanggil, dan ID itu dimasukkan lewat parameter ini.
    [string[]] $Izinkan,

    # Memasang EKSTENSINYA sendiri lewat berkas .crx, tanpa Load unpacked dan
    # tanpa Developer mode. Cadangan kalau pemasangan lewat installer gagal.
    [switch] $PasangEkstensi,

    # Membatalkan pemasangan ekstensi di atas.
    [switch] $CopotEkstensi
)

$ErrorActionPreference = 'Stop'

$akar     = Split-Path -Parent $MyInvocation.MyCommand.Path
$namaHost = 'com.jakforge.studiobridge'

# ---------------------------------------------------------------------
# Di mana berkasnya
# ---------------------------------------------------------------------

# Manifest ditaruh di LocalAppData, bukan di Program Files.
#
# Program Files butuh hak administrator untuk ditulisi, dan pemasangan yang
# menuntut "Run as administrator" adalah pemasangan yang gagal di komputer
# kantor. LocalAppData selalu bisa ditulis pemiliknya.
$folderData   = Join-Path $env:LOCALAPPDATA 'JakForge\ChromeBridge'
$jalurManifest = Join-Path $folderData "$namaHost.json"

# Folder yang dipilih orang lewat "Load unpacked". Dipakai untuk mencocokkan
# catatan ekstensi di profil peramban dengan pemasangan ini.
#
# Ada DUA yang sah: folder di akar repositori, dan salinannya di folder
# keluaran build. Keduanya memuat manifest yang sama, jadi ID-nya sama — tapi
# orang bisa memuat yang mana saja, dan keduanya harus dikenali.
$folderEkstensi = Join-Path $akar 'Extension'
$folderEkstensiSemua = @(
    $folderEkstensi,
    (Join-Path $akar 'debug\net462\Extension')
)

function Baca-IdEkstensi {
    $manifestEkstensi = Join-Path $akar 'Extension\manifest.json'

    if (-not (Test-Path $manifestEkstensi)) {
        throw "Tidak menemukan Extension\manifest.json di $akar."
    }

    $m = Get-Content $manifestEkstensi -Raw | ConvertFrom-Json

    if (-not $m.key) {
        throw @'
Extension\manifest.json tidak punya field "key".

Tanpa itu Chrome menurunkan ID ekstensi dari JALUR FOLDERNYA, jadi ID-nya
berbeda di tiap mesin dan allowed_origins di sini tidak akan pernah cocok.
Gejalanya sunyi: ekstensi terpasang, Studio jalan, tapi setiap perintah
browser gagal tanpa pesan.
'@
    }

    # ID Chrome: SHA-256 dari kunci publik (DER), 16 bita pertama, tiap
    # nibble 0-f dipetakan ke a-p.
    $der  = [Convert]::FromBase64String($m.key)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($der)

    $sb = [System.Text.StringBuilder]::new()
    foreach ($b in $hash[0..15]) {
        [void]$sb.Append([char](97 + ($b -shr 4)))
        [void]$sb.Append([char](97 + ($b -band 0x0F)))
    }

    return $sb.ToString()
}

function Cari-Exe {
    if ($JalurExe) {
        if (-not (Test-Path $JalurExe)) { throw "Tidak menemukan: $JalurExe" }
        return (Resolve-Path $JalurExe).Path
    }

    # Yang dipilih adalah yang PALING BARU, bukan urutan tetap.
    #
    # Percobaan pertama memakai urutan Release-dulu-baru-Debug, dengan
    # anggapan Release lebih "resmi". Itu keliru dan akibatnya buruk: di
    # repositori ini Release tertinggal sepuluh hari, jadi yang terdaftar
    # adalah biner lama yang protokolnya sudah berbeda — jembatannya
    # terpasang rapi dan tetap tidak bisa terhubung.
    #
    # Tanggal tidak bisa salah tebak seperti itu. Semua calon dicetak
    # beserta tanggalnya, supaya pilihannya bisa diperiksa, bukan dipercaya.
    $calon = @(
        'Studio.NativeHost\bin\Debug\Studio.NativeHost.exe',
        'Studio.NativeHost\bin\Release\Studio.NativeHost.exe',
        'debug\net462\Studio.NativeHost.exe',
        'Studio.NativeHost.exe'
    ) | ForEach-Object { Join-Path $akar $_ } | Where-Object { Test-Path $_ } |
        ForEach-Object { Get-Item $_ } | Sort-Object LastWriteTime -Descending

    if (-not $calon) {
        throw @"
Tidak menemukan Studio.NativeHost.exe.

Bangun dulu proyek Studio.NativeHost, atau sebutkan jalurnya:
  .\pasang-jembatan-chrome.ps1 -JalurExe "D:\Studio\Studio.NativeHost.exe"
"@
    }

    if ($calon.Count -gt 1) {
        Write-Host "  Calon yang ditemukan (dipilih yang paling baru):"
        foreach ($c in $calon) {
            $tanda = if ($c.FullName -eq $calon[0].FullName) { '->' } else { '  ' }
            Write-Host ("   {0} {1:yyyy-MM-dd HH:mm}  {2,7} bita  {3}" -f
                $tanda, $c.LastWriteTime, $c.Length, $c.FullName.Replace("$akar\", ''))
        }
        Write-Host ""
    }

    return $calon[0].FullName
}

# Ketiganya memakai mekanisme native messaging yang sama; mendaftarkan ke
# semuanya lebih murah daripada menebak peramban mana yang dipakai orang.
$peramban = @{
    'Chrome' = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts'
    'Edge'   = 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts'
    'Brave'  = 'HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts'
}

# ---------------------------------------------------------------------
# Memasang ekstensinya sendiri, lewat .crx
# ---------------------------------------------------------------------

# Cabang registry tempat peramban mencari ekstensi yang dipasang dari luar.
# Berbeda dari cabang NativeMessagingHosts di atas, dan sering tertukar:
# yang satu memberitahu di mana .exe-nya, yang ini memasang ekstensinya.
$cabangEkstensi = @{
    'Chrome' = 'HKCU:\Software\Google\Chrome\Extensions'
    'Edge'   = 'HKCU:\Software\Microsoft\Edge\Extensions'
    'Brave'  = 'HKCU:\Software\BraveSoftware\Brave-Browser\Extensions'
}

function Cari-Crx {
    # Dicari di beberapa tempat karena skrip ini dipakai di dua keadaan yang
    # berbeda: di repositori saat mengembangkan, dan di folder pemasangan di
    # komputer lain. Keduanya harus bekerja tanpa disuruh.
    $calon = @(
        (Join-Path $akar 'Extension\JakForgeBridge.crx'),
        (Join-Path $akar 'debug\net462\Extension\JakForgeBridge.crx'),
        (Join-Path $akar 'JakForgeBridge.crx')
    )

    return $calon | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Baca-VersiEkstensi {
    $m = Join-Path $akar 'Extension\manifest.json'
    if (-not (Test-Path $m)) { $m = Join-Path $akar 'debug\net462\Extension\manifest.json' }
    if (-not (Test-Path $m)) { return '1.0' }

    try { return (Get-Content $m -Raw | ConvertFrom-Json).version } catch { return '1.0' }
}

if ($CopotEkstensi) {
    $id = Baca-IdEkstensi

    foreach ($nama in ($cabangEkstensi.Keys | Sort-Object)) {
        $k = Join-Path $cabangEkstensi[$nama] $id
        if (Test-Path $k) { Remove-Item $k -Recurse -Force; Write-Host "  dicabut  $nama" }
    }

    Write-Host ""
    Write-Host "Pendaftaran ekstensi dicabut. Peramban akan mencopotnya sendiri saat dijalankan lagi."
    Write-Host ""
    return
}

if ($PasangEkstensi) {
    $id  = Baca-IdEkstensi
    $crx = Cari-Crx

    if (-not $crx) {
        throw @"
Tidak menemukan JakForgeBridge.crx.

Berkas itu dibuat oleh buat-installer.ps1, dan ikut di dalam folder pemasangan
kalau Studio dipasang lewat JakForgeStudioSetup.exe.

Tanpa .crx, ekstensinya masih bisa dipasang secara manual:
  1. buka  chrome://extensions
  2. nyalakan Developer mode
  3. Load unpacked -> $folderEkstensi
"@
    }

    $versi = Baca-VersiEkstensi

    Write-Host ""
    Write-Host "  Berkas .crx  : $crx"
    Write-Host "  ID ekstensi  : $id"
    Write-Host "  Versi        : $versi"
    Write-Host ""

    foreach ($nama in ($cabangEkstensi.Keys | Sort-Object)) {
        $induk = $cabangEkstensi[$nama]
        if (-not (Test-Path $induk)) { New-Item -Path $induk -Force | Out-Null }

        $k = Join-Path $induk $id
        if (-not (Test-Path $k)) { New-Item -Path $k | Out-Null }

        Set-ItemProperty -Path $k -Name 'path'    -Value $crx
        Set-ItemProperty -Path $k -Name 'version' -Value $versi

        Write-Host "  terdaftar  $nama"
    }

    Write-Host ""
    Write-Host "Selesai. Tutup peramban Anda, lalu buka lagi."
    Write-Host ""
    Write-Host "  Peramban akan memasang ekstensinya sendiri, lalu menampilkan"
    Write-Host "  'Ekstensi baru ditambahkan' — tekan Aktifkan."
    Write-Host ""
    Write-Host "  Konfirmasi itu tidak bisa dilewati tanpa hak administrator, dan memang"
    Write-Host "  disengaja: kalau ada jalannya, program apa pun bisa menanam ekstensi di"
    Write-Host "  peramban Anda tanpa sepengetahuan Anda."
    Write-Host ""
    return
}

# ---------------------------------------------------------------------
# Memeriksa
# ---------------------------------------------------------------------

# Kenapa mode ini ada.
#
# Jembatan ini punya enam sambungan yang harus benar BERSAMAAN: .exe-nya,
# manifest native host, kunci registry, ID ekstensi, catatan ekstensi di profil
# peramban, dan service worker yang benar-benar berjalan. Kalau satu saja
# meleset, gejalanya satu dan sama: Studio bilang "tidak bisa terhubung ke
# native host dalam 5 detik". Tanpa alat, membedakan keenamnya berarti menebak,
# dan menebak di sini memakan waktu berjam-jam.
#
# Yang paling mahal ditemukan adalah sambungan kelima. Satu folder unpacked
# hanya boleh dipegang SATU catatan di profil peramban. Kalau ada dua — dan itu
# yang terjadi kalau "key" ditambahkan ke manifest setelah foldernya pernah
# dimuat, karena ID-nya berubah sementara catatan lama tetap memegang folder
# yang sama — Chrome gagal memuat foldernya dan hanya menulis satu baris ke
# chrome_debug.log:
#
#   WARNING:load_error_reporter.cc] Failed to load extension from: <folder>.
#
# Sementara di chrome://extensions ekstensinya tetap tampak terpasang dan
# aktif. Registry benar, manifest benar, .exe benar, dan tidak ada yang jalan.

$kodeNonaktif = @{
    1   = 'dimatikan pengguna'
    2   = 'izin bertambah, menunggu persetujuan'
    4   = 'gagal saat dimuat ulang'
    8   = 'syarat lingkungan tidak terpenuhi'
    128 = 'berkasnya dianggap rusak'
}

function Baca-CatatanEkstensi {
    param([string] $folderUserData)

    $hasil = @()
    if (-not (Test-Path $folderUserData)) { return $hasil }

    foreach ($profil in Get-ChildItem $folderUserData -Directory -ErrorAction SilentlyContinue) {
        foreach ($berkas in 'Secure Preferences', 'Preferences') {
            $p = Join-Path $profil.FullName $berkas
            if (-not (Test-Path $p)) { continue }

            try { $j = Get-Content $p -Raw -ErrorAction Stop | ConvertFrom-Json } catch { continue }
            if (-not $j.extensions) { continue }
            if (-not $j.extensions.settings) { continue }

            foreach ($e in $j.extensions.settings.PSObject.Properties) {
                # Hanya catatan yang menunjuk folder ekstensi KITA yang menarik.
                if ($folderEkstensiSemua -notcontains $e.Value.path) { continue }

                # disable_reasons bisa berupa satu angka (Chrome lama) atau
                # larik (Chrome baru). @() menyamakan keduanya — tanpa itu,
                # .Count pada satu angka menghasilkan kosong, bukan 1.
                $alasan = @($e.Value.disable_reasons) | Where-Object { $null -ne $_ }

                # Versinya dicari di dua tempat. Catatan ekstensi unpacked
                # sering TIDAK menyimpan salinan manifest sama sekali; yang
                # tersisa cuma jejak pendaftaran service worker. Kalau dua-duanya
                # kosong, itu sendiri sebuah petunjuk: catatannya ada, tapi
                # peramban tidak pernah berhasil membaca isi foldernya.
                $versi = $e.Value.manifest.version
                if (-not $versi) { $versi = $e.Value.service_worker_registration_info.version }
                if (-not $versi) { $versi = '?' }

                $hasil += [pscustomobject]@{
                    Profil = $profil.Name
                    Id     = $e.Name
                    Versi  = $versi
                    Alasan = @($alasan)
                }
            }
        }
    }

    return $hasil
}

function Periksa-Semua {
    $bermasalah = @()

    Write-Host ""
    Write-Host "  === berkas ==="

    $exe = $null
    try {
        $exe = Cari-Exe
        $fi  = Get-Item $exe
        Write-Host ("  [ok]    .exe             {0:yyyy-MM-dd HH:mm}  {1} bita" -f $fi.LastWriteTime, $fi.Length)
        Write-Host ("                           {0}" -f $exe)
    } catch {
        Write-Host "  [GAGAL] .exe             tidak ditemukan"
        $bermasalah += 'Studio.NativeHost.exe belum dibangun.'
    }

    $id = $null
    try {
        $id = Baca-IdEkstensi
        Write-Host "  [ok]    ID ekstensi      $id"
    } catch {
        Write-Host "  [GAGAL] ID ekstensi      manifest ekstensi tidak punya 'key'"
        $bermasalah += 'Extension\manifest.json tidak punya field "key", jadi ID-nya ikut jalur folder.'
    }

    Write-Host ""
    Write-Host "  === manifest native host ==="

    if (Test-Path $jalurManifest) {
        # BOM diperiksa sebagai BITA, bukan lewat pembacaan teks: pembacaan
        # teks justru menelan BOM diam-diam, padahal itu persis yang membuat
        # Chrome menolak manifest tanpa pesan apa pun.
        $bita = [System.IO.File]::ReadAllBytes($jalurManifest)
        if ($bita.Length -ge 3 -and $bita[0] -eq 0xEF -and $bita[1] -eq 0xBB -and $bita[2] -eq 0xBF) {
            Write-Host "  [GAGAL] BOM              ada — Chrome menolak manifest ini diam-diam"
            $bermasalah += 'Manifest native host ber-BOM. Jalankan skrip ini tanpa -Periksa untuk menulisnya ulang.'
        } else {
            Write-Host "  [ok]    BOM              tidak ada"
        }

        try {
            $m = Get-Content $jalurManifest -Raw | ConvertFrom-Json
            Write-Host "  [ok]    JSON             sah"

            if ($exe -and $m.path -ne $exe) {
                Write-Host "  [GAGAL] path             menunjuk $($m.path)"
                $bermasalah += 'Manifest menunjuk .exe yang bukan yang terbaru. Jalankan ulang skrip ini.'
            } elseif ($m.path -and -not (Test-Path $m.path)) {
                Write-Host "  [GAGAL] path             berkasnya tidak ada: $($m.path)"
                $bermasalah += 'Manifest menunjuk .exe yang tidak ada.'
            } else {
                Write-Host "  [ok]    path             cocok dengan .exe terbaru"
            }

            # allowed_origins dibandingkan dengan SETIAP ID yang benar-benar
            # terpasang, bukan hanya dengan ID yang dihitung dari "key".
            #
            # Inilah yang menghasilkan pesan paling membingungkan di seluruh
            # rantai ini, karena Chrome tidak menyebut ID mana yang ditolak:
            #
            #   Access to the specified native messaging host is forbidden.
            #
            # Artinya selalu sama: ekstensi yang berjalan punya ID yang tidak
            # ada di daftar ini. Menjalankan skrip ini tanpa -Periksa akan
            # memasukkan semua ID yang terpasang.
            $asal = @($m.allowed_origins)
            Write-Host ("  [..]    allowed_origins  {0} asal terdaftar" -f $asal.Count)
            foreach ($a in $asal) { Write-Host "                           $a" }

            $idTerpasang = @()
            foreach ($ud in @("$env:LOCALAPPDATA\Google\Chrome\User Data",
                              "$env:LOCALAPPDATA\Microsoft\Edge\User Data",
                              "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\User Data")) {
                foreach ($c in @(Baca-CatatanEkstensi $ud)) {
                    if ($idTerpasang -notcontains $c.Id) { $idTerpasang += $c.Id }
                }
            }
            if ($id -and ($idTerpasang -notcontains $id)) { $idTerpasang += $id }

            $kurang = @($idTerpasang | Where-Object { $asal -notcontains "chrome-extension://$_/" })
            if ($kurang.Count -gt 0) {
                foreach ($k in $kurang) { Write-Host "  [GAGAL] tidak diizinkan   $k" }
                $bermasalah += "allowed_origins belum memuat $($kurang.Count) ID yang terpasang. Jalankan skrip ini tanpa -Periksa; ia akan memasukkan semuanya."
            } else {
                Write-Host "  [ok]    cocok            semua ID yang terpasang ada di daftar"
            }
        } catch {
            Write-Host "  [GAGAL] JSON             tidak bisa diurai"
            $bermasalah += 'Manifest native host bukan JSON yang sah.'
        }
    } else {
        Write-Host "  [GAGAL] manifest         tidak ada di $jalurManifest"
        $bermasalah += 'Manifest native host belum pernah ditulis. Jalankan skrip ini tanpa -Periksa.'
    }

    Write-Host ""
    Write-Host "  === registry ==="

    foreach ($nama in ($peramban.Keys | Sort-Object)) {
        $k = Join-Path $peramban[$nama] $namaHost
        if (Test-Path $k) {
            $v = (Get-ItemProperty $k).'(default)'
            if ($v -eq $jalurManifest) {
                Write-Host ("  [ok]    {0,-6}           menunjuk manifest yang benar" -f $nama)
            } else {
                Write-Host ("  [GAGAL] {0,-6}           menunjuk {1}" -f $nama, $v)
                $bermasalah += "Registry $nama menunjuk manifest lain. Jalankan ulang skrip ini."
            }
        } else {
            Write-Host ("  [-]     {0,-6}           belum terdaftar" -f $nama)
        }
    }

    # Manifest lama dari pemasangan versi sebelumnya. Tidak ada yang menunjuknya
    # lagi, tapi ia mengizinkan ID yang sudah usang — kalau suatu saat ada yang
    # mendaftarkannya kembali, gejalanya adalah "forbidden" yang sama.
    $manifestLama = 'C:\Program Files\Studio\NativeHost\nativehost-manifest.json'
    if (Test-Path $manifestLama) {
        Write-Host "  [!]     manifest lama    masih ada tapi tidak dipakai:"
        Write-Host "                           $manifestLama"
    }

    Write-Host ""
    Write-Host "  === pemasangan ekstensi lewat .crx (cadangan) ==="

    $crx = Cari-Crx
    if ($crx) {
        Write-Host ("  [ok]    berkas .crx      {0:n0} bita" -f (Get-Item $crx).Length)
        Write-Host ("                           {0}" -f $crx)
    } else {
        Write-Host "  [-]     berkas .crx      tidak ada (dibuat oleh buat-installer.ps1)"
    }

    $adaDaftarCrx = $false
    foreach ($nama in ($cabangEkstensi.Keys | Sort-Object)) {
        $k = Join-Path $cabangEkstensi[$nama] $id
        if (Test-Path $k) {
            $adaDaftarCrx = $true
            $v = Get-ItemProperty $k
            Write-Host ("  [ok]    {0,-6}           terdaftar, versi {1}" -f $nama, $v.version)

            # Jalur yang menunjuk berkas yang sudah tidak ada adalah penyebab
            # ekstensi "hilang sendiri" sesudah folder pemasangan dipindah:
            # peramban mencabutnya diam-diam pada penyalaan berikutnya.
            if ($v.path -and -not (Test-Path $v.path)) {
                Write-Host ("  [GAGAL] {0,-6}           .crx yang ditunjuk tidak ada: {1}" -f $nama, $v.path)
                $bermasalah += "Pendaftaran ekstensi $nama menunjuk .crx yang sudah tidak ada. Jalankan: .\pasang-jembatan-chrome.ps1 -PasangEkstensi"
            }
        }
    }

    if (-not $adaDaftarCrx) {
        Write-Host "  [-]     registry         belum ada; ekstensinya dipasang manual atau lewat installer"
    }

    Write-Host ""
    Write-Host "  === catatan ekstensi di profil peramban ==="

    $userData = @{
        'Chrome' = "$env:LOCALAPPDATA\Google\Chrome\User Data"
        'Edge'   = "$env:LOCALAPPDATA\Microsoft\Edge\User Data"
        'Brave'  = "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\User Data"
    }

    $adaCatatan = $false

    foreach ($nama in ($userData.Keys | Sort-Object)) {
        foreach ($grup in (@(Baca-CatatanEkstensi $userData[$nama]) | Group-Object Profil)) {
            $adaCatatan = $true
            $anggota = @($grup.Group)

            Write-Host ("  {0} / {1} — {2} catatan untuk folder yang sama" -f $nama, $grup.Name, $anggota.Count)

            foreach ($c in $anggota) {
                if ($c.Id -eq $id) { $tanda = 'ID cocok' } else { $tanda = 'ID LAMA ' }
                if ($c.Alasan.Count -eq 0) {
                    $ket = 'aktif'
                } else {
                    $ket = (($c.Alasan | ForEach-Object {
                        if ($kodeNonaktif.ContainsKey([int]$_)) { $kodeNonaktif[[int]$_] } else { "kode $_" }
                    }) -join ', ')
                }
                Write-Host ("     {0}  v{1,-8} {2}  {3}" -f $c.Id, $c.Versi, $tanda, $ket)
            }

            if ($anggota.Count -gt 1) {
                # Yang dibuang HANYA yang ID-nya tidak cocok.
                #
                # Nasihat sebelumnya di sini berbunyi "Remove SEMUA entri, lalu
                # Load unpacked". Itu memang menyelesaikan masalahnya, tapi
                # menyuruh orang membuang catatan yang justru sehat lalu
                # memuatnya kembali — dan kalau yang dibuang cuma yang aktif,
                # pasangannya malah terbentuk lagi dan keadaannya kembali persis
                # seperti semula. Yang salah cuma catatan yang ID-nya sudah
                # tidak cocok dengan "key" di manifest; sisanya tidak perlu
                # disentuh sama sekali.
                $basi = @($anggota | Where-Object { $_.Id -ne $id })
                $sehat = @($anggota | Where-Object { $_.Id -eq $id -and $_.Alasan.Count -eq 0 })

                $langkah = "$nama / $($grup.Name): ADA $($anggota.Count) CATATAN untuk satu folder ekstensi.`n"
                $langkah += "     Setiap kali peramban dinyalakan, ia mencoba memuat folder itu sekali`n"
                $langkah += "     untuk TIAP catatan. Yang ID-nya tidak cocok gagal, dan kegagalan itu`n"
                $langkah += "     hanya tercatat di log — di layar, ekstensinya seperti hilang sendiri.`n"
                $langkah += "     Buka chrome://extensions, lalu Remove entri berikut:`n"

                foreach ($b in $basi) { $langkah += "       - $($b.Id)`n" }

                if ($sehat.Count -gt 0) {
                    $langkah += "     JANGAN sentuh $($sehat[0].Id) — catatan itu sehat.`n"
                    $langkah += "     Tidak perlu Load unpacked lagi sesudahnya."
                } else {
                    $langkah += "     Lalu Load unpacked -> $folderEkstensi"
                }

                $bermasalah += $langkah
            } elseif ($anggota[0].Id -ne $id) {
                $bermasalah += "$nama / $($grup.Name): ekstensinya dimuat dengan ID lama. Remove entrinya, lalu Load unpacked sekali lagi."
            } elseif ($anggota[0].Alasan.Count -gt 0) {
                $bermasalah += "$nama / $($grup.Name): ekstensinya nonaktif. Aktifkan lewat chrome://extensions."
            }
        }
    }

    if (-not $adaCatatan) {
        Write-Host "  [-]     belum ada peramban yang memuat $folderEkstensi"
        $bermasalah += "Ekstensinya belum dimuat: chrome://extensions -> Developer mode -> Load unpacked -> $folderEkstensi"
    }

    Write-Host ""
    Write-Host "  === keadaan saat ini ==="

    # Peramban membaca daftar native messaging host SEKALI saat dijalankan.
    #
    # Pendaftaran yang dibuat selagi peramban sudah berjalan tidak terlihat
    # olehnya, dan gejalanya menyesatkan: "Specified native messaging host not
    # found." padahal registry dan manifestnya sudah benar dan bisa dibuktikan
    # ada. Sudah terbukti di mesin ini — nama host yang sama berubah dari
    # "not found" menjadi tersambung hanya dengan menutup dan membuka peramban.
    if (Test-Path $jalurManifest) {
        $waktuManifest = (Get-Item $jalurManifest).LastWriteTime
        $perambanLama = @(Get-Process chrome, msedge, brave -ErrorAction SilentlyContinue |
                          Where-Object { $_.StartTime -lt $waktuManifest })

        if ($perambanLama.Count -gt 0) {
            Write-Host "  [GAGAL] urutan          peramban dijalankan SEBELUM jembatan didaftarkan"
            $bermasalah += @"
Peramban yang sedang berjalan lebih tua daripada pendaftaran jembatan
     (peramban mulai sebelum $($waktuManifest.ToString('HH:mm:ss'))). Ia membaca daftar native
     messaging host sekali saat start, jadi pendaftaran ini belum terlihat.
     TUTUP peramban sepenuhnya — semua jendela, semua profil — lalu buka lagi.
"@
        } else {
            Write-Host "  [ok]    urutan          peramban dijalankan sesudah pendaftaran"
        }
    }

    $perambanJalan = @(Get-Process chrome, msedge, brave -ErrorAction SilentlyContinue).Count -gt 0
    if ($perambanJalan) { Write-Host "  [ok]    peramban         berjalan" } else { Write-Host "  [-]     peramban         tidak berjalan" }

    if ($perambanJalan) {
        # Service worker MV3 hidup di renderer bertanda --extension-process.
        # Kalau tidak ada satu pun, tidak ada ekstensi yang benar-benar jalan,
        # apa pun yang ditampilkan chrome://extensions.
        $adaRenderer = @(Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" -ErrorAction SilentlyContinue |
                         Where-Object { $_.CommandLine -match '--extension-process' }).Count -gt 0
        if ($adaRenderer) {
            Write-Host "  [ok]    service worker   ada renderer ekstensi"
        } else {
            Write-Host "  [GAGAL] service worker   TIDAK ADA renderer ekstensi — tidak ada ekstensi yang jalan"
        }
    }

    $hostJalan = @(Get-Process Studio.NativeHost -ErrorAction SilentlyContinue).Count -gt 0
    if ($hostJalan) { Write-Host "  [ok]    native host      berjalan" } else { Write-Host "  [-]     native host      tidak berjalan" }

    $adaPipe = @([System.IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { $_ -match 'StudioNativeHostPipe' }).Count -gt 0
    if ($adaPipe) { Write-Host "  [ok]    named pipe       siap menerima perintah Studio" } else { Write-Host "  [-]     named pipe       belum ada" }

    if ($perambanJalan -and -not $hostJalan) {
        $bermasalah += 'Peramban berjalan tapi native host tidak: ekstensinya tidak pernah memanggil connectNative.'
    }

    Write-Host ""
    if ($bermasalah.Count -eq 0) {
        Write-Host "  Semua sambungan benar."
        if (-not $perambanJalan) {
            Write-Host "  Buka peramban, tunggu setengah menit, lalu jalankan ini lagi untuk"
            Write-Host "  memastikan native host-nya ikut hidup."
        }
    } else {
        Write-Host "  Yang perlu dibereskan:"
        Write-Host ""
        $n = 1
        foreach ($b in $bermasalah) {
            Write-Host ("  {0}. {1}" -f $n, $b)
            $n++
        }
    }
    Write-Host ""
}

if ($Periksa) {
    Periksa-Semua
    return
}

# ---------------------------------------------------------------------
# Membatalkan
# ---------------------------------------------------------------------

if ($Batalkan) {
    foreach ($nama in $peramban.Keys) {
        $k = Join-Path $peramban[$nama] $namaHost
        if (Test-Path $k) {
            Remove-Item $k -Force
            Write-Host "  dihapus  $nama"
        }
    }

    if (Test-Path $folderData) {
        Remove-Item $folderData -Recurse -Force
        Write-Host "  dihapus  $folderData"
    }

    Write-Host ""
    Write-Host "Pendaftaran dibatalkan. Ekstensinya sendiri masih terpasang di Chrome;"
    Write-Host "buang lewat chrome://extensions kalau memang tidak dipakai lagi."
    return
}

# ---------------------------------------------------------------------
# Memasang
# ---------------------------------------------------------------------

$idEkstensi = Baca-IdEkstensi
$exe        = Cari-Exe

Write-Host ""
Write-Host "  Studio.NativeHost : $exe"
Write-Host "  ID ekstensi       : $idEkstensi"
Write-Host "  Manifest          : $jalurManifest"
Write-Host ""

New-Item -ItemType Directory -Force -Path $folderData | Out-Null

# Manifest disusun sebagai objek lalu diserialkan, bukan dirangkai sebagai
# teks: jalur Windows memuat backslash, dan backslash yang tidak diloloskan
# menghasilkan JSON yang sah tapi menunjuk berkas yang salah.
# allowed_origins tidak boleh berisi tebakan.
#
# ID yang dihitung dari "key" memang ID yang SEHARUSNYA. Tapi yang menentukan
# boleh-tidaknya menyambung adalah ID yang BENAR-BENAR dipakai peramban saat
# itu, dan keduanya bisa berbeda: entri lama yang diaktifkan kembali, folder
# yang dimuat sebelum "key" ada, atau salinan di folder keluaran yang dimuat
# terpisah. Kalau meleset, Chrome menolak dengan satu kalimat yang tidak
# menyebut ID mana pun:
#
#   Access to the specified native messaging host is forbidden.
#
# Jadi ID yang dihitung DIGABUNG dengan setiap ID yang benar-benar terdaftar
# untuk folder ekstensi ini di profil peramban mana pun. Semuanya menunjuk
# folder yang sama di komputer ini, jadi tidak ada yang dilonggarkan —
# yang hilang hanya ketergantungan pada tebakan.
$idTerpakai = @($idEkstensi)

foreach ($ud in @("$env:LOCALAPPDATA\Google\Chrome\User Data",
                  "$env:LOCALAPPDATA\Microsoft\Edge\User Data",
                  "$env:LOCALAPPDATA\BraveSoftware\Brave-Browser\User Data")) {
    foreach ($c in @(Baca-CatatanEkstensi $ud)) {
        if ($idTerpakai -notcontains $c.Id) { $idTerpakai += $c.Id }
    }
}

# ID yang sudah pernah diizinkan tidak dibuang begitu saja.
#
# Tanpa ini, menjalankan skrip sekali lagi untuk alasan lain — mengganti .exe,
# misalnya — diam-diam mencabut ID yang tadi ditambahkan lewat -Izinkan, dan
# jembatannya mati lagi tanpa ada yang menyentuh apa pun yang berhubungan.
if (Test-Path $jalurManifest) {
    try {
        foreach ($a in @((Get-Content $jalurManifest -Raw | ConvertFrom-Json).allowed_origins)) {
            if ($a -match '^chrome-extension://([a-p]{32})/$') {
                if ($idTerpakai -notcontains $Matches[1]) { $idTerpakai += $Matches[1] }
            }
        }
    } catch { }
}

foreach ($tambahan in @($Izinkan)) {
    if (-not $tambahan) { continue }
    $bersih = $tambahan.Trim()
    # Dibersihkan dulu: orang cenderung menempel seluruh "chrome-extension://x/".
    if ($bersih -match '([a-p]{32})') { $bersih = $Matches[1] }
    if ($bersih -notmatch '^[a-p]{32}$') {
        throw "ID ekstensi tidak sah: '$tambahan'. ID Chrome selalu 32 huruf a-p."
    }
    if ($idTerpakai -notcontains $bersih) {
        $idTerpakai += $bersih
        Write-Host "  ditambahkan lewat -Izinkan: $bersih"
    }
}

if ($idTerpakai.Count -gt 1) {
    Write-Host "  ID lain yang juga terdaftar untuk folder ini:"
    foreach ($lain in ($idTerpakai | Where-Object { $_ -ne $idEkstensi })) {
        Write-Host "    $lain"
    }
    Write-Host ""
}

# Manifest disusun sebagai TEKS, bukan lewat ConvertTo-Json.
#
# ConvertTo-Json di Windows PowerShell 5.1 menghasilkan JSON yang sah tapi
# berbentuk tidak lazim: dua spasi sesudah titik dua, dan larik yang diberi
# lekukan sangat dalam sampai sejajar dengan kurung pembukanya. Satu-satunya
# manifest yang pernah benar-benar diterima peramban di mesin ini ditulis
# tangan dalam bentuk sederhana; yang ditulis ConvertTo-Json tidak pernah
# berhasil. Apakah bentuknya benar-benar sebabnya belum terbukti — tapi
# menyamakannya dengan bentuk yang terbukti bekerja tidak ada ruginya, dan
# bentuk yang tidak lazim tidak ada gunanya.
#
# Backslash tetap diloloskan secara eksplisit: jalur Windows penuh backslash,
# dan backslash yang lolos begitu saja menghasilkan JSON yang sah tapi
# menunjuk berkas yang salah.
$asalJson = ($idTerpakai | ForEach-Object { '    "chrome-extension://' + $_ + '/"' }) -join ",`n"

$isiManifest = @"
{
  "name": "$namaHost",
  "description": "Studio Native Messaging Host",
  "path": "$($exe.Replace('\', '\\'))",
  "type": "stdio",
  "allowed_origins": [
$asalJson
  ]
}
"@

# Ditulis TANPA BOM, dan itu bukan kerewelan.
#
# Set-Content -Encoding UTF8 di Windows PowerShell 5.1 SELALU menambahkan BOM
# (ef bb bf) di awal berkas. Pengurai manifest native host Chrome adalah
# pengurai JSON yang ketat dan menolak berkas yang tidak dimulai dengan "{",
# jadi manifest ber-BOM membuat native host tidak pernah dijalankan — tanpa
# pesan apa pun di sisi Chrome maupun Studio. Gejalanya persis sama dengan
# "extension tidak terpasang", padahal semuanya sudah benar.
[System.IO.File]::WriteAllText(
    $jalurManifest,
    $isiManifest,
    (New-Object System.Text.UTF8Encoding $false))

foreach ($nama in $peramban.Keys) {
    $induk = $peramban[$nama]

    # Diperiksa dulu, bukan langsung New-Item -Force.
    #
    # Pada kunci registry yang SUDAH ADA dan punya subkunci, -Force berarti
    # "hapus lalu buat ulang", dan itu gagal dengan "Cannot delete a subkey
    # tree". Gejalanya menyesatkan: peramban yang belum pernah didaftarkan
    # berhasil, sedangkan yang sudah pernah justru menggagalkan skripnya.
    if (-not (Test-Path $induk)) { New-Item -Path $induk -Force | Out-Null }

    $k = Join-Path $induk $namaHost
    if (-not (Test-Path $k)) { New-Item -Path $k | Out-Null }

    Set-ItemProperty -Path $k -Name '(default)' -Value $jalurManifest

    Write-Host "  terdaftar  $nama"
}

Write-Host ""

# Peramban yang sudah berjalan TIDAK akan melihat pendaftaran ini.
#
# Ini jebakan paling mahal di seluruh pemasangan, karena gejalanya terlihat
# seperti pendaftaran yang gagal padahal pendaftarannya sempurna: peramban
# menjawab "Specified native messaging host not found." untuk nama yang jelas-
# jelas ada di registry dan manifestnya sah. Daftar native messaging host
# dibaca SEKALI saat peramban dijalankan.
#
# Menekan Reload pada ekstensi tidak menolong — yang perlu dimuat ulang bukan
# ekstensinya, melainkan peramban itu sendiri.
$perambanJalan = @(Get-Process chrome, msedge, brave -ErrorAction SilentlyContinue)
if ($perambanJalan.Count -gt 0) {
    $namaJalan = ($perambanJalan | ForEach-Object { $_.ProcessName } | Sort-Object -Unique) -join ', '

    Write-Host "  ============================================================"
    Write-Host "   TUTUP PERAMBAN ANDA SEKARANG, LALU BUKA LAGI."
    Write-Host ""
    Write-Host "   Sedang berjalan: $namaJalan"
    Write-Host ""
    Write-Host "   Peramban membaca daftar native messaging host sekali saat"
    Write-Host "   dijalankan. Yang sedang berjalan tidak akan melihat"
    Write-Host "   pendaftaran ini, dan akan menjawab 'not found' walaupun"
    Write-Host "   semuanya sudah benar. Menekan Reload pada ekstensi TIDAK"
    Write-Host "   menolong; yang perlu dimuat ulang adalah perambannya."
    Write-Host ""
    Write-Host "   Tutup semua jendela, semua profil, lalu buka lagi."
    Write-Host "  ============================================================"
    Write-Host ""
}

Write-Host "Selesai. Satu langkah tersisa, dan hanya sekali per mesin:"
Write-Host ""
Write-Host "  1. Buka  chrome://extensions"
Write-Host "  2. Nyalakan 'Developer mode' di pojok kanan atas"
Write-Host "  3. 'Load unpacked' -> pilih folder:"
Write-Host "     $akar\Extension"
Write-Host ""
Write-Host "  Setelah dimuat, ID-nya harus $idEkstensi."
Write-Host "  Kalau berbeda, berarti field 'key' di manifest.json ikut terubah."
Write-Host ""
