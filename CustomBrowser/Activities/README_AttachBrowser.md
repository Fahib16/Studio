# Custom Activity: Attach Browser ala UiPath untuk Custom.Browser (OpenRPA)

## Beda dari Open Browser

| | Open Browser | Attach Browser |
|---|---|---|
| Tab belum ada | Buka baru | GAGAL (throw) |
| Tab sudah ada & cocok | Pakai yang ada | Pakai yang ada (selecttab) |

## API yang dipakai — dikonfirmasi, bukan tebakan

`NMHook.enumtabs()`, `NMHook.tabs` (koleksi `NativeMessagingMessageTab`),
`NMHook.selecttab(browser, id)` — semua sudah terkonfirmasi dari
`OpenURL.cs`/`GetTab.cs`/`GetElement.cs` (NM) yang sebelumnya kamu share.

## Zero-config Maximize Window / activity lain

Sama seperti `Open Browser`: implementasi `IActivityTemplateFactory`
(`Create()`) bikin delegate argument `Body` otomatis bernama `"browser"`
kalau di-drag **fresh dari toolbox** — jadi `Maximize Window` (yang
default-nya merujuk ke `"browser"`) otomatis jalan tanpa setting apa pun
kalau di-nest di sini juga.

## Yang PERLU disadari

- Pencarian tab berbasis **Url/Title pattern** (string matching), **bukan**
  "Indicate on screen" seperti `Click`/`TypeInto` v4. `NMSelector` itu untuk
  elemen DI DALAM halaman, bukan untuk memilih tab/window-nya sendiri — jadi
  belum ada tombol Indicate di activity ini. Kalau kamu mau itu juga, kasih
  tahu — perlu saya explore dulu apakah `SelectorWindow` bisa dipakai untuk
  kasus "pilih tab", karena saya belum pernah coba pola itu.
- Isi minimal salah satu dari `Url` atau `Title` (boleh dua-duanya sekaligus
  untuk lebih presisi) — kalau dua-duanya kosong, activity akan error di awal.
- Tampilan **sengaja minimal** (cuma header + drop-zone "Do") — semua
  property diatur lewat Properties panel, sesuai arah yang sudah disepakati
  di `Maximize Window` sebelumnya.

## Cara pakai

1. Taruh 3 file ini (`AttachBrowser.cs` + designer) di project `Custom.Browser`
   (sama seperti `OpenBrowser`, drag & drop lewat Solution Explorer + Add
   Existing Item, JANGAN cuma copy lewat File Explorer).
2. Rebuild Solution.
3. Drag `Attach Browser` **fresh dari toolbox**, isi `Url`/`Title` di
   Properties panel, drop activity lain (termasuk `Maximize Window`) di
   dalam `Do`.
