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

[CmdletBinding()]
param(
    # Dipakai kalau .exe-nya ada di tempat lain, mis. hasil rilis.
    [string] $JalurExe,

    # Membatalkan pemasangan: manifest dan kunci registry dibuang.
    [switch] $Batalkan
)

$ErrorActionPreference = 'Stop'

$akar     = Split-Path -Parent $MyInvocation.MyCommand.Path
$namaHost = 'com.studio.nativehost'

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

    # Urutannya sengaja: hasil build Release lebih dulu, lalu Debug, lalu
    # folder keluaran bersama. Yang PERTAMA ADA yang dipakai, dan jalurnya
    # dicetak — supaya tidak pernah ada keraguan mana yang terdaftar.
    $calon = @(
        'Studio.NativeHost\bin\Release\Studio.NativeHost.exe',
        'Studio.NativeHost\bin\Debug\Studio.NativeHost.exe',
        'debug\net462\Studio.NativeHost.exe',
        'Studio.NativeHost.exe'
    )

    foreach ($c in $calon) {
        $p = Join-Path $akar $c
        if (Test-Path $p) { return (Resolve-Path $p).Path }
    }

    throw @"
Tidak menemukan Studio.NativeHost.exe.

Dicari di:
$($calon | ForEach-Object { "  $akar\$_" } | Out-String)
Bangun dulu proyek Studio.NativeHost, atau sebutkan jalurnya:
  .\pasang-jembatan-chrome.ps1 -JalurExe "D:\Studio\Studio.NativeHost.exe"
"@
}

# Ketiganya memakai mekanisme native messaging yang sama; mendaftarkan ke
# semuanya lebih murah daripada menebak peramban mana yang dipakai orang.
$peramban = @{
    'Chrome' = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts'
    'Edge'   = 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts'
    'Brave'  = 'HKCU:\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts'
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
$manifest = [ordered]@{
    name            = $namaHost
    description     = 'Studio Native Messaging Host'
    path            = $exe
    type            = 'stdio'
    allowed_origins = @("chrome-extension://$idEkstensi/")
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $jalurManifest -Encoding UTF8

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
