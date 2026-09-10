# Indeks File Proyek Studio Extension

Disusun 31 Agustus 2026. Selama pengembangan ada banyak folder batch, dan
tidak semua isinya masih berlaku. Ini peta mana yang harus dipakai.

---

## ⚠️ DUA PERINGATAN — baca dulu

### 1. Folder `StudioExtensionBatch4/CustomStudioBridge/` (tanggal 31 Agustus) JANGAN DIPAKAI

Meski tanggalnya paling baru, isinya **mundur**. Itu hasil membangun ulang
Batch 4 dari nol di percakapan terpisah, tanpa membawa perbaikan Batch 5–12
(XPath, retry timeout, TabId opsional, perbaikan fokus, screenshot).
Kalau dipakai, fitur-fitur itu hilang. Abaikan folder tersebut.

### 2. Pekerjaan 31 Agustus tidak ada di sini

Yang dikerjakan hari ini di percakapan lain — `ScreenshotCropper.cs`,
`RestoreFocus` (AttachThreadInput), `PickTabForAttach`/`TabPickerWindow`
versi terbaru — **tidak tersimpan di sini**, karena tiap percakapan punya
penyimpanan file sendiri. Ambil langsung dari percakapan itu, atau dari
`C:\Users\Fajar\source\repos\Studio` yang sudah kamu pasang.

Versi terbaru yang ADA di sini: sampai 28 Agustus (folder `StudioScreenshot/`).

---

## Versi terbaru per file

### Extension (`Studio\Extension\`)

| File | Ambil dari |
|---|---|
| `background.js` | `StudioScreenshot/extension/` |
| `manifest.json` | `StudioScreenshot/extension/` |

### Native Host (`Studio\Studio.NativeHost\`)

| File | Ambil dari |
|---|---|
| `Program.cs` | `StudioExtensionBatch4/NativeHost/` — versi dengan `timeoutMs` per-request. Bandingkan dulu dengan yang di disk kamu sebelum menimpa. |

### Custom.StudioBridge — interaksi elemen

| File | Ambil dari |
|---|---|
| `StudioClick.cs`, `StudioSetText.cs`, `StudioGetText.cs`, `StudioHighlight.cs` | `StudioScreenshot/Activities/` |
| 4 designer `.xaml` + `.xaml.cs` | `StudioScreenshot/Design/` |
| `IndicateHelper.cs` | `StudioScreenshot/Design/` — **tapi** versi 31 Agustus lebih baru lagi (ada `RestoreFocus`), ambil dari percakapan lain |
| `Base64ToImageSourceConverter.cs` | `StudioScreenshot/Design/` |
| `ScreenshotCropper.cs` | **tidak ada di sini** — ambil dari percakapan lain |

### Custom.StudioBridge — tab management

| File | Ambil dari |
|---|---|
| `StudioOpenTab.cs` + designer | `StudioOpenTabFix/` |
| `StudioAttachTab.cs` | `CustomStudioBridge_AttachTab/` |
| `StudioAttachTabDesigner.xaml` + `.xaml.cs` | `CustomStudioBridge_AttachTabIndicate/` |
| `TabPickerWindow.xaml` | `StudioExtensionBatch4/CustomStudioBridge_Updated/` |
| `TabPickerWindow.xaml.cs` | `CustomStudioBridge_AttachTabIndicate/` |
| `StudioListTabs.cs`, `StudioActivateTab.cs`, `StudioCloseTab.cs` + designer | `CustomStudioBridge/` (belum pernah diubah sejak dibuat) |
| `StudioPipeClient.cs` | `StudioExtensionBatch4/CustomStudioBridge_Updated/` (27 Agustus) |

### Custom.StudioBridge — lainnya

| File | Ambil dari |
|---|---|
| `StudioLaunchChrome.cs` + designer | `StudioLaunchChrome/` |

### Installer

| File | Ambil dari |
|---|---|
| `StudioSetup.wixproj`, `Package.wxs` | `StudioWixInstaller/` |

---

## Folder usang (sudah digantikan, abaikan saja)

`StudioExtension/`, `StudioExtensionBatch2/`, `StudioExtensionBatch3/`,
`StudioExtensionBatch5/` … `StudioExtensionBatch12/`,
`StudioExtensionBatch10_Full/`, `StudioBridgeBatch12/`,
`StudioDesignerFix/`, `StudioDesignerFix2/`, `StudioIndicateFocusFix/`,
`StudioExtensionBatch4/CustomStudioBridge/`

Juga semua folder era OpenRPA lama: `OpenRPA*Activity/`, `Activities/Custom/`,
`CustomBrowserProject/`, `TypeIntoV2/` — masih berguna sebagai arsip
(Terminal dan MaximizeWindow masih dipakai), tapi bukan bagian dari sistem
extension ini.

---

## Sumber paling bisa dipercaya

Yang paling akurat sebenarnya **kode di disk kamu sendiri**
(`C:\Users\Fajar\source\repos\Studio`) — itu satu-satunya tempat semua
batch sudah tergabung dan terbukti build. Folder-folder di sini adalah
potongan per-batch, bukan salinan utuh yang sinkron.
