using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRPA.Views
{
    /// <summary>
    /// Aturan penyusunan toolbox JakForge: mana activity bawaan yang
    /// disembunyikan, dan activity mana masuk kelompok mana.
    ///
    /// Kenapa perlu:
    ///
    /// 1. Setelah Studio ini punya activity sendiri, sebagian activity bawaan
    ///    menjadi kembar — dua cara mengerjakan hal yang sama, dengan dua
    ///    bentuk selector yang berbeda. Yang kembar disembunyikan supaya tidak
    ///    ada yang salah pilih. Tipenya TIDAK dihapus dari kode: workflow lama
    ///    yang sudah memakainya tetap bisa dibuka dan dijalankan, hanya tidak
    ///    bisa ditambahkan lagi dari toolbox.
    ///
    /// 2. Bawaan OpenRPA mengelompokkan activity menurut NAMA ASSEMBLY, jadi
    ///    activity yang saling berkaitan bisa terpisah hanya karena kebetulan
    ///    berada di project yang berbeda. Di sini pengelompokannya menurut
    ///    FUNGSI.
    /// </summary>
    internal static class JakForgeToolbox
    {
        public const string UI = "Otomasi UI";
        public const string Browser = "Browser";
        public const string Window = "Jendela & Aplikasi";
        public const string Files = "Manajemen File";
        public const string Data = "Data & Tabel";
        public const string Excel = "Excel";
        public const string Mail = "Email";
        public const string Terminal = "Terminal";
        public const string System = "Sistem & Kontrol Alur";
        public const string Orchestration = "Orkestrasi";
        public const string Workitem = "Workitem";

        /// <summary>
        /// Activity bawaan yang sudah ada penggantinya di Studio ini.
        /// Nilai kamus hanya sebagai catatan penggantinya, dipakai di tooltip
        /// pesan kalau suatu saat diperlukan.
        /// </summary>
        private static readonly Dictionary<string, string> superseded = new Dictionary<string, string>
        {
            // Orkestrasi: Invoke milik Studio ini menggantikan yang lama, dan
            // yang lama hanya jalan di dalam Studio — JakRunner tidak bisa memuatnya.
            { "OpenRPA.Activities.InvokeOpenRPA", "Invoke Workflow (Orkestrasi)" },

            // InvokeRemoteOpenRPA menjalankan workflow di robot LAIN. Di JakForge
            // itu dilakukan lewat ForgeHub — jadwalkan pekerjaan dari dasbor,
            // atau dari Studio lewat Design → ForgeHub → Terbitkan — bukan lewat
            // activity. Robot yang memerintah robot lain secara langsung tidak
            // meninggalkan jejak di mana pun.
            { "OpenRPA.Activities.InvokeRemoteOpenRPA", "jadwalkan lewat ForgeHub" },

            // Jendela dan aplikasi
            { "OpenRPA.Activities.CloseApplication", "Close Window (Jendela & Aplikasi)" },

            // Kendali alur: semuanya ditulis ulang di Custom.Flow. Yang lama
            // hidup di dalam rakitan Studio, sehingga workflow yang memakainya
            // gagal dimuat JakRunner dengan "Cannot create unknown type" —
            // padahal jalan normal di kanvas.
            { "OpenRPA.Activities.ForEachDataRow",   "For Each Data Row (Kontrol Alur)" },
            { "OpenRPA.Activities.ForEachOf",        "For Each Of (Kontrol Alur)" },
            { "OpenRPA.Activities.BreakableWhile",   "While (Kontrol Alur)" },
            { "OpenRPA.Activities.BreakableDoWhile", "Do While (Kontrol Alur)" },
            { "OpenRPA.Activities.Break",            "Break (Kontrol Alur)" },
            { "OpenRPA.Activities.Continue",         "Continue (Kontrol Alur)" },
            { "OpenRPA.Activities.CommentOut",       "Comment Out (Kontrol Alur)" },

            // ----------------------------------------------------------------
            // BELUM ada penggantinya di JakForge, dan sengaja tidak didaftarkan
            // di sini supaya tetap terlihat di toolbox:
            //
            //   CopyClipboard, InsertClipboard    — papan klip
            //   MoveMouse, ShowBalloonTip         — masukan dan pemberitahuan
            //   Detector, GetWorkflowInstance     — runtime OpenRPA
            //   InvokeOpenFlow, StopOpenRPA       — integrasi OpenFlow
            //   OpenApplication                   — peluncuran aplikasi
            //
            // Menandainya "sudah diganti" padahal penggantinya belum ada akan
            // menyembunyikan activity yang masih satu-satunya cara melakukan
            // hal itu — lebih buruk daripada membiarkannya terlihat.
            // ----------------------------------------------------------------

            // Otomasi UI: semuanya memakai selector JSON lama
            { "OpenRPA.Activities.ClickElement",      "Click" },
            { "OpenRPA.Activities.TypeText",          "Type Into" },
            { "OpenRPA.Activities.HighlightElement",  "Highlight" },
            { "OpenRPA.Activities.FocusElement",      "Focus Element" },
            { "OpenRPA.Activities.MoveElement",       "Hover Element" },

            // Browser lama lewat native messaging
            { "OpenRPA.NM.OpenURL",       "Open Browser / Navigate URL" },
            { "OpenRPA.NM.CloseTab",      "Close Tab" },
            { "OpenRPA.NM.GetTab",        "Attach Tab / List Tabs" },
            { "OpenRPA.NM.GetElement",    "selector JakForge" },
            { "OpenRPA.NM.GetTable",      "Extract Data Table" },
            { "OpenRPA.NM.WaitForDownload", "Wait For Download (Manajemen File)" },

            // Internet Explorer sudah tidak dipakai lagi di Windows 11
            { "OpenRPA.IE.OpenURL",       "Open Browser" },
            { "OpenRPA.IE.GetElement",    "selector JakForge" },

            // Windows UI Automation lama
            { "OpenRPA.Windows.GetElement",  "selector JakForge" },
            { "OpenRPA.Windows.CloseWindow", "Close Window" },

            // Utilities yang kembar dengan Custom.Data / Custom.Files / Custom.Excel
            { "OpenRPA.Utilities.AddDataRow",       "Add Data Row" },
            { "OpenRPA.Utilities.CreateDataTable",  "Build Data Table" },
            { "OpenRPA.Utilities.ReadCSV",          "Read CSV" },
            { "OpenRPA.Utilities.WriteCSV",         "Write CSV" },
            { "OpenRPA.Utilities.ReadExcel",        "Read Range Workbook" },
            { "OpenRPA.Utilities.WriteExcel",       "Write Range" },
            { "OpenRPA.Utilities.CompressArchive",  "Zip Files" },
            { "OpenRPA.Utilities.ExpandArchive",    "Unzip" },

            // Kembar dengan activity buatan sendiri di kelompok Sistem
            { "System.Activities.Statements.Delay",          "Delay" },
            { "System.Activities.Statements.WriteLine",      "Log Message" },
            { "OpenRPA.Script.Activities.CustomLogMessage",  "Log Message" },
        };

        /// <summary>Activity bawaan yang dipertahankan, dipindah ke kelompok yang cocok.</summary>
        private static readonly Dictionary<string, string> regrouped = new Dictionary<string, string>
        {
            // --- dari assembly OpenRPA ---
            { "OpenRPA.Activities.Break",              System },
            { "OpenRPA.Activities.Continue",           System },
            { "OpenRPA.Activities.BreakableDoWhile",   System },
            { "OpenRPA.Activities.BreakableWhile",     System },
            { "OpenRPA.Activities.ForEachOf",          System },
            { "OpenRPA.Activities.ForEachDataRow",     Data },
            { "OpenRPA.Activities.CommentOut",         System },
            { "OpenRPA.Activities.CopyClipboard",      System },
            { "OpenRPA.Activities.InsertClipboard",    System },
            { "OpenRPA.Activities.ShowBalloonTip",     System },
            { "OpenRPA.Activities.GetWorkflowInstance", Orchestration },
            { "OpenRPA.Activities.CloseApplication",   Window },
            { "OpenRPA.Activities.OpenApplication",    Window },
            { "OpenRPA.Activities.MoveMouse",          UI },
            { "OpenRPA.Activities.Detector",           Orchestration },
            { "OpenRPA.Activities.InvokeOpenFlow",     Orchestration },
            { "OpenRPA.Activities.InvokeOpenRPA",      Orchestration },
            { "OpenRPA.Activities.InvokeRemoteOpenRPA", Orchestration },
            { "OpenRPA.Activities.StopOpenRPA",        Orchestration },
            { "OpenRPA.Activities.Workitems.AddWorkitem",       Workitem },
            { "OpenRPA.Activities.Workitems.BulkAddWorkitems",  Workitem },
            { "OpenRPA.Activities.Workitems.DeleteWorkitem",    Workitem },
            { "OpenRPA.Activities.Workitems.UpdateWorkitem",    Workitem },
            { "OpenRPA.Activities.Workitems.PopWorkitem",       Workitem },
            { "OpenRPA.Activities.Workitems.ThrowBusinessRuleException", Workitem },

            // --- dari OpenRPA.Utilities ---
            { "OpenRPA.Utilities.AddDataColumn",     Data },
            { "OpenRPA.Utilities.DeleteRow",         Data },
            { "OpenRPA.Utilities.DeleteAllRows",     Data },
            { "OpenRPA.Utilities.SetAllRowsState",   Data },
            { "OpenRPA.Utilities.JArrayToDataTable", Data },
            { "OpenRPA.Utilities.ReadJSON",          Data },
            { "OpenRPA.Utilities.Match",             Data },
            { "OpenRPA.Utilities.Matches",           Data },
            { "OpenRPA.Utilities.Replace",           Data },
            { "OpenRPA.Utilities.SelectFile",        Files },
            { "OpenRPA.Utilities.SelectFolder",      Files },
            { "OpenRPA.Utilities.DownloadFile",      Files },
            { "OpenRPA.Utilities.ReadPDF",           Files },
            { "OpenRPA.Utilities.StartProcess",      Window },
            { "OpenRPA.Utilities.KillProcess",       Window },
            { "OpenRPA.Utilities.GetCredentials",    System },
            { "OpenRPA.Utilities.SetCredentials",    System },
            { "OpenRPA.Utilities.SetAutoLogin",      System },

            // --- dari OpenRPA.NM (yang tidak memakai selector) ---
            { "OpenRPA.NM.ExecuteScript",    Browser },

            // --- dari OpenRPA.Windows ---
            { "OpenRPA.Windows.GetWindows",  Window },
        };

        /// <summary>Nama kelompok untuk assembly buatan sendiri.</summary>
        private static readonly Dictionary<string, string> byAssembly = new Dictionary<string, string>
        {
            { "Custom.StudioBridge", UI },
            { "Custom.Browser",      Browser },
            { "Custom.Window",       Window },
            { "Custom.Files",        Files },
            { "Custom.Data",         Data },
            { "Custom.Excel",        Excel },
            { "Custom.Mail",         Mail },
            { "Custom.Terminal",     Terminal },
            { "Custom.System",       System },
            { "Custom.Orchestrator", Orchestration },
            { "Custom.Flow",         Flow },
        };

        /// <summary>Urutan tampil kelompok. Yang tidak terdaftar muncul sesudahnya, urut abjad.</summary>
        private static readonly string[] order =
        {
            UI, Browser, Window, Files, Data, Excel, Mail, Terminal, System,
            Orchestration, Workitem, OpenFlow, Office, Vision, Database, Recorder, Rossum, Java
        };

        // ================= Kelompok sisa activity bawaan =================

        public const string Flow = "Sistem & Kontrol Alur";
        public const string OpenFlow = "OpenFlow";
        public const string Office = "Office (COM)";
        public const string Vision = "Gambar & OCR";
        public const string Database = "Database";
        public const string Rossum = "Rossum";
        public const string Recorder = "Perekaman Layar";
        public const string Java = "Java";

        /// <summary>
        /// Kelompok untuk assembly yang seluruh isinya sejenis. Dipakai kalau
        /// tipe tertentu tidak punya aturan sendiri di tabel regrouped.
        /// </summary>
        private static readonly Dictionary<string, string> assemblyGroup = new Dictionary<string, string>
        {
            { "System.Activities", Flow },
            { "System.Activities.Core.Presentation", Flow },
            { "OpenRPA.Script", Flow },
            { "OpenRPA.Forms", Flow },
            { "OpenRPA.MSSpeech", Flow },
            { "OpenRPA.OpenFlowDB", OpenFlow },
            { "OpenRPA.Office", Office },
            { "OpenRPA.Image", Vision },
            { "OpenRPA.Database", Database },
            { "OpenRPA.Elis.Rossum", Rossum },
            { "OpenRPA.AviRecorder", Recorder },
            { "OpenRPA.Java", Java },
        };

        /// <summary>
        /// Ikon bertema untuk activity yang tipenya TIDAK bisa kita sentuh —
        /// terutama activity milik .NET sendiri, yang tentu saja tidak bisa
        /// diberi atribut ToolboxBitmap dari luar.
        ///
        /// SEDANG TIDAK DIPAKAI. Percobaan mengoper hasilnya sebagai parameter
        /// bitmap ke ToolboxItemWrapper membuat Studio mati dengan
        /// StackOverflowException saat memuat toolbox — dibuktikan dengan
        /// membandingkan dua kali menjalankan aplikasi, dengan dan tanpa
        /// parameter itu. Karena StackOverflow tidak bisa ditangkap, prosesnya
        /// langsung mati dan Windows Error Reporting membekukan seluruh
        /// thread-nya, sehingga aplikasi tampak menggantung di layar Loading.
        ///
        /// Dipertahankan di sini karena pemetaan tipe-ke-ikonnya sendiri benar
        /// dan siap dipakai lewat jalur yang aman: template item toolbox milik
        /// kita sendiri (bukan mekanisme bitmap bawaan WF).
        /// </summary>
        public static string IconFor(Type type, string category)
        {
            if (type == null) return null;

            const string root = "pack://application:,,,/OpenRPA;component/Resources/JakForge/tools/";

            switch (type.Name)
            {
                case "If": return root + "if.png";
                case "While":
                case "DoWhile": return root + "while.png";
                case "ForEach`1":
                case "ParallelForEach`1": return root + "foreach.png";
                case "Switch`1": return root + "switch.png";
                case "TryCatch": return root + "trycatch.png";
                case "Assign":
                case "Assign`1": return root + "assign.png";
                case "Parallel": return root + "parallel.png";
                case "Pick":
                case "PickBranch": return root + "pick.png";
                case "Sequence": return root + "sequence.png";
                case "Throw":
                case "Rethrow": return root + "throw.png";
                case "TerminateWorkflow": return root + "terminate.png";
                case "Flowchart":
                case "FlowDecision":
                case "FlowSwitch`1": return root + "flow.png";
                case "State":
                case "StateMachine":
                case "FinalState": return root + "state.png";
                case "AddToCollection`1":
                case "RemoveFromCollection`1":
                case "ClearCollection`1":
                case "ExistsInCollection`1": return root + "collection.png";
            }

            // Sisanya: satu ikon per kelompok. Lebih baik satu lambang yang
            // benar untuk seluruh kelompok daripada gerigi bawaan yang tidak
            // mengatakan apa-apa.
            switch (category)
            {
                case OpenFlow: return root + "openflow.png";
                case Office: return root + "office.png";
                case Vision: return root + "image.png";
                case Database: return root + "database.png";
                case Rossum: return root + "rossum.png";
                case Recorder: return root + "recorder.png";
                case Java: return root + "java.png";
            }

            if (type.Namespace != null)
            {
                if (type.Namespace.StartsWith("OpenRPA.Script")) return root + "script.png";
                if (type.Namespace.StartsWith("OpenRPA.Forms")) return root + "form.png";
                if (type.Namespace.StartsWith("OpenRPA.MSSpeech")) return root + "speech.png";
            }

            return null;
        }

        public static bool Hidden(Type type)
        {
            return type != null && type.FullName != null && superseded.ContainsKey(type.FullName);
        }

        public static string Replacement(Type type)
        {
            string value;
            if (type != null && type.FullName != null && superseded.TryGetValue(type.FullName, out value)) return value;
            return null;
        }

        public static string CategoryOf(Type type, string assemblyName)
        {
            string category;

            if (type != null && type.FullName != null && regrouped.TryGetValue(type.FullName, out category))
                return category;

            if (assemblyName != null && byAssembly.TryGetValue(assemblyName, out category))
                return category;

            if (assemblyName != null && assemblyGroup.TryGetValue(assemblyName, out category))
                return category;

            return assemblyName;
        }

        public static int Rank(string category)
        {
            var index = Array.IndexOf(order, category);
            return index < 0 ? order.Length : index;
        }

        public static IEnumerable<string> Sort(IEnumerable<string> categories)
        {
            return categories.OrderBy(Rank).ThenBy(c => c, StringComparer.OrdinalIgnoreCase);
        }
    }
}
