# Membangun JakForgeStudioSetup.exe — satu berkas, tanpa hak administrator.
#
# Urutannya penting dan tidak bisa dibalik:
#
#   1. bangun seluruh solution                -> debug\net462
#   2. paketkan ekstensi jadi .crx            -> perlu Chrome + kunci privat
#   3. kemas semuanya jadi payload.zip        -> JakForge.Installer\payload.zip
#   4. bangun JakForge.Installer              -> zip-nya ikut sebagai resource
#
# Langkah 4 harus TERPISAH dari langkah 1, karena muatannya baru ada sesudah
# langkah 3. Membangun keduanya sekaligus menghasilkan pemasang tanpa isi —
# yang tetap terbentuk, tetap bisa dijalankan, dan baru ketahuan salah di
# komputer tujuan.
#
#     powershell -ExecutionPolicy Bypass -File buat-installer.ps1
#
# CATATAN BAGI YANG MENYUNTING: simpan sebagai UTF-8 DENGAN BOM. Windows
# PowerShell 5.1 membaca berkas tanpa BOM sebagai ANSI, dan tanda pisah "—"
# di dalam string lalu terurai jadi tiga karakter yang berakhir dengan tanda
# kutip penutup — string tertutup lebih awal dan skripnya gagal diurai di
# tempat yang sama sekali tidak bersalah.

[CmdletBinding()]
param(
    # Melewati build solution, memakai debug\net462 yang sudah ada.
    [switch] $LewatiBuild,

    # Kunci privat untuk menandatangani .crx. Kalau tidak disebut, dicari di
    # tempat bakunya; kalau tidak ada juga, .crx dilewati dan pemasang tetap
    # dibuat — ekstensinya nanti dipasang lewat "Load unpacked".
    [string] $KunciPrivat
)

$ErrorActionPreference = 'Stop'

$akar      = Split-Path -Parent $MyInvocation.MyCommand.Path
$keluaran  = Join-Path $akar 'debug\net462'
$proyekIns = Join-Path $akar 'JakForge.Installer'
$payload   = Join-Path $proyekIns 'payload.zip'
$staging   = Join-Path $env:TEMP ('jakforge-payload-' + [guid]::NewGuid().ToString('N').Substring(0, 8))

function Langkah($n, $teks) { Write-Host ""; Write-Host "[$n] $teks" -ForegroundColor Cyan }

# ---------------------------------------------------------------------
# 1. Bangun solution
# ---------------------------------------------------------------------

function Cari-MSBuild {
    $calon = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe `
                -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -match 'Current\\Bin\\MSBuild\.exe$' }

    if (-not $calon) { throw "MSBuild tidak ditemukan. Pasang Visual Studio atau Build Tools." }
    return ($calon | Select-Object -First 1).FullName
}

$msbuild = Cari-MSBuild
Write-Host "MSBuild: $msbuild"

if (-not $LewatiBuild) {
    Langkah 1 "Membangun OpenRPA.sln"

    # Proses yang sedang jalan mengunci DLL keluaran, dan MSBuild menyerah
    # sesudah sepuluh kali coba dengan pesan yang menyebut "another process"
    # tanpa menyebut proses mana. Diperiksa lebih dulu supaya jelas.
    $mengganggu = @(Get-Process OpenRPA, JakRunner, Studio.NativeHost -ErrorAction SilentlyContinue)
    if ($mengganggu.Count -gt 0) {
        throw ("Tutup dulu: " + (($mengganggu | ForEach-Object { $_.ProcessName }) -join ', ') +
               ". Proses ini mengunci DLL di debug\net462 dan membuat build gagal menyalin.")
    }

    & $msbuild (Join-Path $akar 'OpenRPA.sln') -m:1 -v:minimal -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build solution gagal (exit $LASTEXITCODE)." }
} else {
    Langkah 1 "Build dilewati (-LewatiBuild)"
}

if (-not (Test-Path (Join-Path $keluaran 'OpenRPA.exe'))) {
    throw "Tidak menemukan OpenRPA.exe di $keluaran. Bangun dulu solution-nya."
}

# ---------------------------------------------------------------------
# 2. Paketkan ekstensi jadi .crx
# ---------------------------------------------------------------------

Langkah 2 "Memaketkan ekstensi jadi .crx"

$folderExt = Join-Path $keluaran 'Extension'
$crxJadi   = $null

if (-not $KunciPrivat) {
    $KunciPrivat = Join-Path $env:LOCALAPPDATA 'JakForge\kunci-penandatangan\ekstensi-studio-bridge.pem'
}

$chrome = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not (Test-Path $folderExt)) {
    Write-Host "  folder Extension tidak ada di keluaran build — .crx dilewati" -ForegroundColor Yellow
} elseif (-not (Test-Path $KunciPrivat)) {
    Write-Host "  kunci privat tidak ada di $KunciPrivat — .crx dilewati" -ForegroundColor Yellow
    Write-Host "  (ekstensinya masih bisa dipasang lewat Load unpacked)"
} elseif (-not $chrome) {
    Write-Host "  chrome.exe tidak ditemukan — .crx dilewati" -ForegroundColor Yellow
} else {
    # Chrome menaruh hasilnya di SEBELAH folder yang dipaketkan, dengan nama
    # <namafolder>.crx, dan menimpa .pem yang sudah ada kalau tidak diberi
    # --pack-extension-key. Kuncinya disebutkan supaya ID ekstensinya tetap.
    $crxMentah = Join-Path $keluaran 'Extension.crx'
    if (Test-Path $crxMentah) { Remove-Item $crxMentah -Force }

    # .crx hasil build SEBELUMNYA dibuang dulu dari folder ekstensi.
    #
    # Kalau tidak, ia ikut dipaketkan ke dalam .crx yang baru — ekstensi yang
    # memuat salinan dirinya sendiri. Ukurannya berlipat tiap kali skrip ini
    # dijalankan (20 KB, lalu 41 KB, lalu 83 KB…) dan tidak ada yang
    # memberitahu; yang terlihat cuma angka yang naik.
    Get-ChildItem $folderExt -Filter '*.crx' -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Force }

    $p = Start-Process $chrome -PassThru -Wait -WindowStyle Hidden -ArgumentList @(
        "--pack-extension=$folderExt",
        "--pack-extension-key=$KunciPrivat",
        "--no-message-box"
    )

    if (Test-Path $crxMentah) {
        $crxJadi = Join-Path $folderExt 'JakForgeBridge.crx'
        Move-Item $crxMentah $crxJadi -Force
        Write-Host "  .crx dibuat: $((Get-Item $crxJadi).Length) bita"
    } else {
        Write-Host "  Chrome tidak menghasilkan .crx (exit $($p.ExitCode)) — dilewati" -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------------
# 3. Kemas jadi payload.zip
# ---------------------------------------------------------------------

Langkah 3 "Mengemas muatan"

# Yang TIDAK ikut. Bukan sekadar penghematan ukuran: berkas .pdb dan .xml
# tidak berguna di komputer tujuan, dan kunci privat ekstensi TIDAK BOLEH
# ikut tersebar bersama pemasang.
$abaikan = @('*.pdb', '*.xml.bak', 'portable.txt', '*.pem', 'chrome_debug.log', '*.log')

New-Item -ItemType Directory -Force -Path $staging | Out-Null

$semua = Get-ChildItem $keluaran -Recurse -File
$ikut  = $semua | Where-Object {
    $nama = $_.Name
    -not ($abaikan | Where-Object { $nama -like $_ })
}

Write-Host ("  {0} berkas dari {1} ikut dikemas" -f $ikut.Count, $semua.Count)

foreach ($f in $ikut) {
    $relatif = $f.FullName.Substring($keluaran.Length).TrimStart('\')
    $tujuan  = Join-Path $staging $relatif
    New-Item -ItemType Directory -Force -Path (Split-Path $tujuan -Parent) | Out-Null
    Copy-Item $f.FullName $tujuan -Force
}

# Studio.NativeHost DISALIN TERPISAH, dan ini bukan kerapian.
#
# Proyek itu membangun ke bin\Debug miliknya sendiri, bukan ke debug\net462
# seperti proyek lain. Jadi ia TIDAK ikut tersapu oleh penyalinan di atas —
# dan pemasang yang kehilangan berkas ini tetap terbentuk, tetap terpasang
# rapi, lalu setiap perintah browser gagal tanpa pesan karena Chrome tidak
# punya apa pun untuk dijalankan. Persis gejala yang paling sulit dilacak.
#
# Dependensinya, Newtonsoft.Json.dll, sudah ada di akar keluaran dengan versi
# yang sama, jadi cukup .exe dan .config-nya.
$nativeHost = Join-Path $akar 'Studio.NativeHost\bin\Debug'
if (-not (Test-Path (Join-Path $nativeHost 'Studio.NativeHost.exe'))) {
    $nativeHost = Join-Path $akar 'Studio.NativeHost\bin\Release'
}

# Skrip jembatan ikut dikemas, dan itu bukan pelengkap.
#
# Pemasangan lewat installer bisa gagal karena hal-hal di luar kendalinya:
# peramban yang dipasang sesudahnya, kebijakan kantor, profil yang dibuat
# belakangan. Kalau itu terjadi di komputer lain, orang di sana tidak punya
# repositori ini — jadi tanpa skrip ini, tidak ada cara memperbaiki apa pun
# selain mengulang seluruh pemasangan dan berharap.
#
# Dengan skrip ini di folder pemasangan, dua perintah menyelesaikan hampir
# semua kasus:
#     .\pasang-jembatan-chrome.ps1 -Periksa
#     .\pasang-jembatan-chrome.ps1 -PasangEkstensi
foreach ($skrip in 'pasang-jembatan-chrome.ps1') {
    $asalSkrip = Join-Path $akar $skrip
    if (Test-Path $asalSkrip) {
        Copy-Item $asalSkrip (Join-Path $staging $skrip) -Force
        Write-Host "  $skrip ikut"
    }
}

$exeHost = Join-Path $nativeHost 'Studio.NativeHost.exe'
if (Test-Path $exeHost) {
    Copy-Item $exeHost (Join-Path $staging 'Studio.NativeHost.exe') -Force

    $cfg = Join-Path $nativeHost 'Studio.NativeHost.exe.config'
    if (Test-Path $cfg) { Copy-Item $cfg (Join-Path $staging 'Studio.NativeHost.exe.config') -Force }

    Write-Host ("  Studio.NativeHost.exe ikut ({0:yyyy-MM-dd HH:mm})" -f (Get-Item $exeHost).LastWriteTime)
} else {
    throw @"
Studio.NativeHost.exe tidak ditemukan di $nativeHost.

Tanpa berkas ini jembatan peramban tidak akan pernah jalan, dan gagalnya
diam-diam. Bangun dulu proyek Studio.NativeHost.
"@
}

if (Test-Path $payload) { Remove-Item $payload -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $staging, $payload,
    [System.IO.Compression.CompressionLevel]::Optimal, $false)

Remove-Item $staging -Recurse -Force
Write-Host ("  payload.zip: {0:n1} MB" -f ((Get-Item $payload).Length / 1MB))

# ---------------------------------------------------------------------
# 4. Bangun pemasangnya
# ---------------------------------------------------------------------

Langkah 4 "Membangun pemasang"

# Rebuild, bukan Build. Muatannya berubah tapi berkas .cs-nya tidak, dan
# MSBuild yang menganggap proyeknya "sudah terbaru" akan menghasilkan .exe
# lama dengan muatan lama di dalamnya — tanpa satu pun pesan.
& $msbuild (Join-Path $proyekIns 'JakForge.Installer.csproj') `
    -t:Rebuild -p:Configuration=Release -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { throw "Build pemasang gagal (exit $LASTEXITCODE)." }

$hasil = Join-Path $proyekIns 'bin\Release\net462\JakForgeStudioSetup.exe'
if (-not (Test-Path $hasil)) {
    $hasil = Join-Path $proyekIns 'bin\Release\JakForgeStudioSetup.exe'
}
if (-not (Test-Path $hasil)) { throw "Pemasang tidak ditemukan sesudah build." }

$tujuanAkhir = Join-Path $akar 'JakForgeStudioSetup.exe'
Copy-Item $hasil $tujuanAkhir -Force

Write-Host ""
Write-Host "Selesai." -ForegroundColor Green
Write-Host ("  {0}  ({1:n1} MB)" -f $tujuanAkhir, ((Get-Item $tujuanAkhir).Length / 1MB))
Write-Host ""
Write-Host "  Salin satu berkas itu ke komputer lain dan jalankan. Tanpa hak administrator."
if (-not $crxJadi) {
    Write-Host "  Tanpa .crx: ekstensinya perlu dipasang lewat Load unpacked di komputer tujuan." -ForegroundColor Yellow
}
Write-Host ""
