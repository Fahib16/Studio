# TestClient.ps1 -- kirim satu command ke native host lewat Named Pipe,
# tanpa perlu Studio activity sungguhan dulu. Berguna buat test Batch 2
# secara terisolasi sebelum diintegrasikan ke Studio beneran.
#
# CARA PAKAI:
#   .\TestClient.ps1 -Action listTabs
#   .\TestClient.ps1 -Action openTab -Url "https://google.com"
#   .\TestClient.ps1 -Action activateTab -TabId 123
#   .\TestClient.ps1 -Action closeTab -TabId 123
#
# SYARAT: browser (Chrome/Edge) harus SEDANG TERBUKA dengan extension Studio
# Automation Bridge aktif -- karena proses native host cuma hidup selama
# ada koneksi connectNative dari extension itu.

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("ping", "listTabs", "openTab", "activateTab", "closeTab")]
    [string]$Action,

    [string]$Url,
    [int]$TabId
)

$pipeName = "StudioNativeHostPipe"

$request = @{ action = $Action }
if ($Url) { $request.url = $Url }
if ($TabId) { $request.tabId = $TabId }
$json = $request | ConvertTo-Json -Compress

Write-Host "Mengirim: $json"

try {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(5000)  # timeout 5 detik kalau native host belum siap/browser belum kebuka

    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    $writer.WriteLine($json)

    $reader = New-Object System.IO.StreamReader($pipe)
    $response = $reader.ReadLine()

    Write-Host "Balasan: $response"

    $pipe.Close()
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Kemungkinan penyebab: browser belum terbuka, extension belum aktif, atau native host belum ke-spawn." -ForegroundColor Yellow
}
