# Custom.System — utilitas alur kerja

Kategori toolbox: **Custom.System** (nama kategori = `AssemblyName`, karena
`wfToolbox.xaml.cs` membuat satu `ToolboxCategory` per assembly yang ke-load).

| Activity | Isi kartu | Sisanya di Properties |
|---|---|---|
| **Log Message** | Message *, Level | ContinueOnError |
| **Delay** | Duration * | ContinueOnError |
| **Comment** | Text (kotak bebas) | — |
| **Retry Scope** | Retries, Interval, Condition, drop-zone "Do" | ContinueOnError |

Warna header kategori ini: indigo `#FF3F51B5`.

## Keputusan yang perlu diketahui

**Namespace `CustomSystem`, bukan `Custom.System`.** Kalau namespace-nya
`Custom.System`, setiap nama berkualifikasi `System.xxx` yang ditulis di dalam
namespace itu akan dicari lebih dulu sebagai `Custom.System.xxx` dan gagal
compile. `AssemblyName` tetap `Custom.System` supaya nama kategori di toolbox
sesuai rencana. Pola RootNamespace ≠ AssemblyName ini sudah dipakai juga oleh
`CustomBrowser` (assembly `Custom.Browser`).

**Invoke Code tidak dibuat di sini.** `OpenRPA.Script` sudah punya
`InvokeCode` lengkap dengan editor kode dan pilihan bahasa. Membuat yang kedua
hanya akan memecah tempat orang mencarinya.

**Throw / Rethrow tidak dibuat.** `System.Activities.Statements.Throw` dan
`Rethrow` sudah muncul di toolbox pada kategori `System.Activities`.

**Log Message memakai `OpenRPA.Interfaces.Log`,** bukan `Console.WriteLine`.
`OpenRPA.Script.CustomLogMessage` yang sudah ada hanya menulis ke Console,
jadi pesannya tidak masuk log OpenRPA dan tidak bisa disaring per level.
Level `Info` dipetakan ke `Log.Output` (bukan `Log.Information`) karena
`Log.Information` disaring oleh `Config.local.log_information`, sedangkan
`Log.Output` selalu tampil.

**Delay mendelegasikan ke `System.Activities.Statements.Delay`,** bukan
`Thread.Sleep`, supaya thread workflow tidak diblokir dan Stop tetap bisa
memutus penungguan. Padanan bawaan .NET tetap ada di kategori
`System.Activities`; yang di sini hanya menyediakan kartu seragam untuk
kategori ini.

**Comment tidak punya ContinueOnError.** `Execute`-nya kosong, tidak ada yang
bisa gagal — properti itu hanya akan jadi hiasan.

**Retry Scope: nilai awal ditulis sebagai ekspresi VB**
(`VisualBasicValue<int>("3")`, `VisualBasicValue<TimeSpan>("TimeSpan.FromSeconds(5)")`),
bukan `Literal`. `Literal` tersimpan benar tapi tidak bisa ditampilkan
`ExpressionTextBox` (bukan `ITextExpression`), sehingga kotaknya terlihat
kosong padahal berisi 3.

**Retry Scope: penjadwalan ulang tidak dilakukan di fault handler.**
`NativeActivityFaultContext` tidak punya `ScheduleActivity`. Jadi fault
handler hanya `CancelChild` + `HandleFault`, dan percobaan berikutnya
dijadwalkan dari completion callback — yang tetap dipanggil setelah child
dibatalkan.

## Status pengujian

Diuji dengan `WorkflowInvoker` (harness terpisah, 10 skenario) dan semuanya
lulus:

- Retry Scope: validasi default bersih; gagal 2× lalu sukses (3 percobaan);
  selalu gagal → exception ASLI yang naik (bukan exception bungkus);
  ContinueOnError menelan kegagalan terakhir; Condition False → Body diulang
  walau tidak melempar; Condition True → berhenti setelah 1×; RetryInterval
  2 detik benar-benar ditunggu.
- Delay 2 detik benar-benar menunda.
- Comment jalan tanpa efek. Log Message jalan.

Kategori toolbox + keempat activity sudah dipastikan MUNCUL di Studio yang
berjalan (bukan hanya dari hasil build). Kartu designer sudah dipastikan
ter-render (Retry Scope di-render lewat `WorkflowDesigner` headless).

Yang BELUM diuji: perilaku Stop/Cancel di tengah Delay dan di tengah jeda
Retry Scope saat dijalankan dari tombol Stop Studio.

---

## Ikon toolbox

4 activity di project ini punya ikonnya sendiri di folder `Resources/`,
dipasang lewat `[System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.<nama>.png")]`.
Ikonnya digambar dengan warna kategori tema JakForge dan lambang putih sesuai
fungsi activity. Penjelasan lengkap mekanismenya ada di `JAKFORGE.md` di akar
repositori.
