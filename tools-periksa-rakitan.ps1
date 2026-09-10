# Cari SEMUA ketidakcocokan versi rakitan di folder keluaran sekaligus.
#
# Penyakitnya sudah muncul tiga kali dengan wajah berbeda -- Newtonsoft.Json,
# System.ValueTuple, ClosedXML -- dan tiap kali baru ketahuan saat robot
# berhenti di tengah jalan. Semua proyek menyalin ke SATU folder, jadi satu
# proyek yang tertinggal versi sudah cukup menjatuhkan yang lain.
#
# Yang diperiksa: untuk tiap rakitan di folder, setiap rujukannya dibandingkan
# dengan berkas yang BENAR-BENAR ada di sana, sesudah pengalihan versi dari
# berkas config diterapkan.
param(
    [string]$Folder = "C:\Users\Fajar\source\repos\Studio\debug\net462",
    [string]$Config = "C:\Users\Fajar\source\repos\Studio\debug\net462\JakRunner.exe.config"
)

# --- versi yang benar-benar ada di folder ---
$ada = @{}
Get-ChildItem -Path (Join-Path $Folder '*') -Include *.dll,*.exe -File | ForEach-Object {
    try {
        $n = [Reflection.AssemblyName]::GetAssemblyName($_.FullName)
        $ada[$n.Name] = $n.Version
    } catch { }   # bukan rakitan .NET (mis. DLL native) -- lewati
}

# --- pengalihan versi dari config ---
$alih = @{}
if (Test-Path $Config) {
    [xml]$x = Get-Content $Config
    $ns = New-Object Xml.XmlNamespaceManager $x.NameTable
    $ns.AddNamespace('a', 'urn:schemas-microsoft-com:asm.v1')
    foreach ($d in $x.SelectNodes('//a:dependentAssembly', $ns)) {
        $nama = $d.assemblyIdentity.name
        $r = $d.bindingRedirect
        if ($nama -and $r) { $alih[$nama] = [Version]$r.newVersion }
    }
}

Write-Output ("rakitan di folder : " + $ada.Count)
Write-Output ("pengalihan versi  : " + $alih.Count)
Write-Output ""

$masalah = @()

Get-ChildItem -Path (Join-Path $Folder '*') -Include *.dll,*.exe -File | ForEach-Object {
    $file = $_
    try { $asm = [Reflection.Assembly]::ReflectionOnlyLoadFrom($file.FullName) } catch { return }

    foreach ($r in $asm.GetReferencedAssemblies()) {
        # Hanya rujukan yang berkasnya memang dilayani dari folder ini.
        # Rakitan kerangka kerja diambil dari GAC dan bukan urusan kita.
        if (-not $ada.ContainsKey($r.Name)) { continue }

        $diminta = $r.Version
        if ($alih.ContainsKey($r.Name)) { $diminta = $alih[$r.Name] }

        if ($ada[$r.Name] -ne $diminta) {
            $masalah += [pscustomobject]@{
                Rakitan  = $r.Name
                DiFolder = $ada[$r.Name].ToString()
                Diminta  = $diminta.ToString()
                Oleh     = $file.Name
            }
        }
    }
}

if ($masalah.Count -eq 0) {
    Write-Output "BERSIH -- tidak ada rujukan yang gagal dipenuhi folder ini."
} else {
    Write-Output ("KETIDAKCOCOKAN: " + $masalah.Count + " rujukan")
    Write-Output ""
    $masalah | Group-Object Rakitan | ForEach-Object {
        $g = $_.Group[0]
        $pemanggil = ($_.Group | Select-Object -ExpandProperty Oleh -Unique)
        Write-Output ("  " + $_.Name)
        Write-Output ("      di folder : " + $g.DiFolder + "   diminta : " + (($_.Group | Select-Object -ExpandProperty Diminta -Unique) -join ', '))
        Write-Output ("      diminta oleh (" + $pemanggil.Count + ") : " + (($pemanggil | Select-Object -First 6) -join ', '))
    }
}
