#!/usr/bin/env bash
# Bangun ulang dengan benar, lalu BUKTIKAN hasilnya benar-benar tersalin.
#
# Ada satu jebakan yang berulang kali menipu: JakRunner atau Studio yang sedang
# berjalan mengunci berkas di folder keluaran. Penyalinan gagal, MSBuild tetap
# melaporkan sukses untuk proyek yang bersangkutan, dan pengujian berikutnya
# memakai rakitan LAMA — sehingga perbaikan yang benar terlihat tidak berpengaruh.
#
# Karena itu skrip ini menutup semuanya dulu, lalu membandingkan berkas keluaran
# dengan hasil build, dan berteriak kalau ada yang tidak cocok.
set -u
cd /c/Users/Fajar/source/repos/Studio

MSB="/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
OUT=debug/net462

echo "1. menutup program yang mengunci berkas"
powershell -NoProfile -Command \
  "Get-Process -Name JakRunner,OpenRPA,OpenRPA.NativeMessagingHost -ErrorAction SilentlyContinue | Stop-Process -Force" \
  >/dev/null 2>&1
sleep 3

sisa=$(powershell -NoProfile -Command \
  "(Get-Process -Name JakRunner,OpenRPA,OpenRPA.NativeMessagingHost -ErrorAction SilentlyContinue | Measure-Object).Count" \
  | tr -d '\r')

if [ "$sisa" != "0" ]; then
  echo "   GAGAL: masih ada $sisa proses yang berjalan"
  exit 1
fi
echo "   bersih"

# MSBuild kadang melewati CoreCompile meski sumbernya lebih baru daripada
# keluarannya — terlihat sendiri pada Custom.Browser. Menyentuh berkas sumber
# yang lebih baru memaksanya memeriksa ulang, dan biayanya nol kalau memang
# sudah mutakhir.
echo "2. menyegarkan proyek yang sumbernya lebih baru dari keluarannya"
for pair in \
  "Custom.Browser:CustomBrowser" \
  "Custom.Flow:Custom.Flow" \
  "Custom.Orchestrator:Custom.Orchestrator" \
  "Custom.Shared:Custom.Shared" \
  "Custom.StudioBridge:Custom.StudioBridge" \
  "Custom.Files:Custom.Files"
do
  name="${pair%%:*}"; dir="${pair#*:}"
  dll="$dir/bin/Debug/$name.dll"
  [ -f "$dll" ] || continue

  newest=$(find "$dir" -name '*.cs' -newer "$dll" 2>/dev/null | head -1)

  if [ -n "$newest" ]; then
    echo "   $name: sumber lebih baru, disentuh"
    powershell -NoProfile -Command "(Get-Item '$(echo "$newest" | sed 's|/|\\|g')').LastWriteTime = Get-Date" >/dev/null 2>&1
  fi
done

echo "3. membangun solusi"
# Berurutan, BUKAN paralel.
#
# "-m" memakai semua inti sekaligus, dan pada mesin yang juga menjalankan
# Chrome, Studio, dan ForgeHub itu pernah menghabiskan memori: MSBuild
# memuntahkan 305 baris MSB4018 yang sebab dalamnya OutOfMemoryException,
# sama sekali bukan masalah kode. Membangun berurutan lebih lambat beberapa
# puluh detik dan tidak pernah gagal karena sebab itu.
log=$("$MSB" OpenRPA.sln -t:Build -p:Configuration=Debug -v:minimal -nologo -m:1 2>&1)

# Hitung SEMUA kode galat, bukan cuma galat kompilasi.
#
# Pola lama 'error (CS|MC|XLS|XC)' sudah dua kali melaporkan "0 galat" untuk
# build yang gagal total: sekali waktu berkas solusinya rusak (MSB5010), sekali
# waktu memorinya habis (MSB4018). Galat yang tidak dihitung adalah galat yang
# tidak terlihat, dan yang diuji berikutnya jadi rakitan lama.
compile=$(echo "$log" | grep -cE 'error [A-Z]+[0-9]+')
locks=$(echo "$log" | grep -cE 'MSB302[17]')

echo "   galat kompilasi : $compile"
echo "   kunci berkas    : $locks"

if [ "$compile" != "0" ]; then
  echo "$log" | grep -oE 'error [A-Z]+[0-9]+.*' | sort -u | head -8
  exit 1
fi
[ "$locks" != "0" ] && echo "$log" | grep -E 'MSB302[17]' | head -2

echo "4. memeriksa rakitan yang tersalin"
beda=0
for pair in \
  "Custom.Browser:CustomBrowser/bin/Debug" \
  "Custom.Flow:Custom.Flow/bin/Debug" \
  "Custom.Orchestrator:Custom.Orchestrator/bin/Debug" \
  "Custom.Shared:Custom.Shared/bin/Debug" \
  "Custom.StudioBridge:Custom.StudioBridge/bin/Debug" \
  "Custom.Files:Custom.Files/bin/Debug"
do
  name="${pair%%:*}"; dir="${pair#*:}"
  built="$dir/$name.dll"
  shipped="$OUT/$name.dll"

  [ -f "$built" ] || continue

  if [ ! -f "$shipped" ]; then
    echo "   HILANG di keluaran: $name.dll"; beda=$((beda+1)); continue
  fi

  a=$(md5sum "$built" | cut -d' ' -f1)
  b=$(md5sum "$shipped" | cut -d' ' -f1)

  if [ "$a" != "$b" ]; then
    echo "   BASI: $name.dll"
    cp -f "$built" "$shipped" && echo "        disalin ulang"
    beda=$((beda+1))
  fi
done

[ "$beda" = "0" ] && echo "   semua rakitan mutakhir"
echo "selesai"
