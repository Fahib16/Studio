using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.AutomationElements.Infrastructure;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Satu tingkat selector desktop hasil pembacaan elemen, sepadan dengan
    /// "levels" yang dikirim extension untuk sisi web.
    /// </summary>
    public class DesktopLevel
    {
        public string NodeName { get; set; }          // wnd atau ctrl
        public Dictionary<string, string> Attributes { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Menyelesaikan selector desktop bergaya UiPath:
    ///
    ///   &lt;wnd app='notepad' cls='Notepad' title='Untitled - Notepad' /&gt;
    ///   &lt;ctrl name='Text Editor' ctrltype='Edit' /&gt;
    ///
    /// Memakai instance UI Automation sendiri lewat AutomationProvider
    /// (lihat catatan di kelas itu soal kenapa AutomationUtil milik OpenRPA
    /// tidak bisa dipakai dari project ini).
    ///
    /// Pembacaan properti selalu lewat pola Properties.X.TryGetValue(out ...)
    /// — sama seperti yang dipakai WindowsSelector — karena banyak properti UIA
    /// TIDAK didukung oleh sebagian kontrol, dan mengaksesnya langsung akan
    /// melempar exception alih-alih mengembalikan nilai kosong.
    /// </summary>
    /// <summary>
    /// Menyediakan instance UI Automation untuk resolver desktop.
    ///
    /// KENAPA PUNYA SENDIRI, BUKAN MEMAKAI AutomationUtil MILIK OPENRPA:
    /// AutomationUtil tidak dapat diakses dari sini — kalau ia berada di
    /// project OpenRPA utama, mereference-nya akan membuat circular reference
    /// (project OpenRPA sudah mereference Custom.StudioBridge); kalau ia
    /// internal di OpenRPA.Windows, ia memang tidak diekspos ke luar assembly.
    ///
    /// Instance dibuat SEKALI dan tidak pernah di-dispose. Membuat dan
    /// membuang UIA3Automation di setiap panggilan sangat mahal, dan
    /// membuangnya juga membatalkan semua AutomationElement yang sudah
    /// dikembalikan ke pemanggil.
    ///
    /// Konsekuensi yang perlu disadari: ada dua instance UIA hidup
    /// berdampingan (milik OpenRPA dan milik kita). Ini didukung dan aman,
    /// tapi cache OpenRPA tidak dipakai bersama, jadi penelusuran pohon di
    /// UI Explorer tidak ikut mempercepat activity saat runtime.
    /// </summary>
    internal static class AutomationProvider
    {
        private static readonly object _lock = new object();
        private static AutomationBase _automation;

        public static AutomationBase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_automation == null) _automation = new FlaUI.UIA3.UIA3Automation();
                    return _automation;
                }
            }
        }
    }

    public static class DesktopSelectorResolver
    {
        private const string Wnd = "wnd";
        private const string Ctrl = "ctrl";

        // ---------------- Resolusi ----------------

        /// <summary>
        /// Cari elemen yang cocok dengan selector. Mengembalikan array kosong
        /// kalau tidak ada yang cocok (bukan exception) — pemanggil yang
        /// memutuskan apakah itu kegagalan, karena UI Explorer justru wajar
        /// menemui nol hasil saat user sedang menyunting.
        /// </summary>
        public static AutomationElement[] Resolve(string xmlSelector, int maxResults = 1)
        {
            var nodes = SelectorDocument.Parse(xmlSelector);
            if (nodes.Count == 0) return new AutomationElement[0];

            var automation = AutomationProvider.Instance;
            {
                var scopes = new List<AutomationElement> { automation.GetDesktop() };

                foreach (var node in nodes)
                {
                    var isWindowLevel = string.Equals(node.NodeName, Wnd, StringComparison.OrdinalIgnoreCase);
                    var wanted = node.Attributes.Where(a => a.IsChecked)
                        .ToDictionary(a => a.Name, a => a.Value, StringComparer.OrdinalIgnoreCase);

                    var found = new List<AutomationElement>();

                    foreach (var scope in scopes)
                    {
                        AutomationElement[] candidates;
                        try
                        {
                            // Tingkat <wnd> dicari di ANAK LANGSUNG desktop
                            // (jendela tingkat atas). Menelusuri seluruh
                            // keturunan desktop akan menyapu isi semua aplikasi
                            // yang terbuka dan sangat lambat.
                            candidates = isWindowLevel
                                ? scope.FindAllChildren()
                                : scope.FindAllDescendants();
                        }
                        catch (Exception) { continue; }

                        foreach (var el in candidates)
                        {
                            if (Matches(el, wanted)) found.Add(el);
                        }
                    }

                    string idxRaw;
                    if (wanted.TryGetValue("idx", out idxRaw))
                    {
                        int idx;
                        if (int.TryParse(idxRaw, out idx) && idx >= 1)
                            found = found.Count >= idx ? new List<AutomationElement> { found[idx - 1] } : new List<AutomationElement>();
                    }

                    scopes = found;
                    if (scopes.Count == 0) break;
                }

                return scopes.Take(maxResults <= 0 ? scopes.Count : maxResults).ToArray();
            }
        }

        /// <summary>Jumlah elemen yang cocok — untuk tombol Validate.</summary>
        public static int Count(string xmlSelector)
        {
            return Resolve(xmlSelector, 0).Length;
        }

        private static bool Matches(AutomationElement el, Dictionary<string, string> wanted)
        {
            foreach (var kv in wanted)
            {
                if (string.Equals(kv.Key, "idx", StringComparison.OrdinalIgnoreCase)) continue;

                var actual = ReadAttribute(el, kv.Key);
                if (!WildcardMatch(kv.Value, actual)) return false;
            }
            return true;
        }

        /// <summary>
        /// Nama atribut mengikuti kosakata UiPath (app/cls/title/name/idx),
        /// dengan tambahan ctrltype dan automationid yang tidak punya padanan
        /// langsung di UiPath tapi jauh lebih stabil di aplikasi WPF/WinForms
        /// modern. "role" diterima sebagai alias ctrltype supaya selector yang
        /// disalin dari UiPath tidak langsung gagal dibaca.
        /// </summary>
        private static string ReadAttribute(AutomationElement el, string key)
        {
            try
            {
                switch (key.ToLowerInvariant())
                {
                    case "app":
                        return ProcessNameOf(el);
                    case "title":
                    case "name":
                        return ReadString(el, () => { string v; return el.Properties.Name.TryGetValue(out v) ? v : null; });
                    case "cls":
                    case "classname":
                        return ReadString(el, () => { string v; return el.Properties.ClassName.TryGetValue(out v) ? v : null; });
                    case "automationid":
                    case "aid":
                        return ReadString(el, () => { string v; return el.Properties.AutomationId.TryGetValue(out v) ? v : null; });
                    case "ctrltype":
                    case "role":
                        {
                            FlaUI.Core.Definitions.ControlType ct;
                            if (el.Properties.ControlType.TryGetValue(out ct)) return ct.ToString();
                            return null;
                        }
                    default:
                        return null;
                }
            }
            catch (Exception) { return null; }
        }

        private static string ReadString(AutomationElement el, Func<string> read)
        {
            try { return read(); } catch (Exception) { return null; }
        }

        private static string ProcessNameOf(AutomationElement el)
        {
            try
            {
                int pid;
                if (!el.Properties.ProcessId.TryGetValue(out pid)) return null;
                return Process.GetProcessById(pid).ProcessName;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Wildcard ala UiPath: '*' banyak karakter, '?' satu karakter.</summary>
        private static bool WildcardMatch(string pattern, string value)
        {
            if (pattern == null) return true;
            value = value ?? "";

            if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
                return string.Equals(pattern, value, StringComparison.Ordinal);

            var esc = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
            try { return Regex.IsMatch(value, "^" + esc + "$", RegexOptions.Singleline); }
            catch (Exception) { return false; }
        }

        // ---------------- Membaca elemen jadi tingkat ----------------

        /// <summary>
        /// Susun rantai tingkat dari elemen sampai jendela tingkat atas —
        /// bahan Selector Editor, sepadan dengan "levels" milik sisi web.
        ///
        /// Hanya jendela tingkat atas dan elemen target yang dipakai sebagai
        /// default; tingkat perantara ikut dikirim supaya bisa dicentang
        /// manual kalau selector pendeknya ternyata tidak unik.
        /// </summary>
        public static List<DesktopLevel> BuildChain(AutomationElement raw)
        {
            var levels = new List<DesktopLevel>();
            if (raw == null) return levels;

            var chain = new List<AutomationElement>();
            var cur = raw;

            {
                var desktop = AutomationProvider.Instance.GetDesktop();

                // Berhenti di bawah desktop: desktop sendiri bukan jendela dan
                // tidak pernah jadi bagian selector.
                while (cur != null && !cur.Equals(desktop))
                {
                    chain.Insert(0, cur);
                    try { cur = cur.Parent; } catch (Exception) { break; }
                }
            }

            for (int i = 0; i < chain.Count; i++)
            {
                var el = chain[i];
                var isWindow = i == 0;

                var level = new DesktopLevel { NodeName = isWindow ? Wnd : Ctrl };

                if (isWindow)
                {
                    Put(level, "app", ProcessNameOf(el));
                    Put(level, "cls", ReadAttribute(el, "cls"));
                    Put(level, "title", ReadAttribute(el, "title"));
                }
                else
                {
                    Put(level, "name", ReadAttribute(el, "name"));
                    Put(level, "automationid", ReadAttribute(el, "automationid"));
                    Put(level, "cls", ReadAttribute(el, "cls"));
                    Put(level, "ctrltype", ReadAttribute(el, "ctrltype"));
                }

                levels.Add(level);
            }

            return levels;
        }

        private static void Put(DesktopLevel level, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) level.Attributes[key] = value;
        }

        /// <summary>
        /// Elemen terdalam yang berada tepat di bawah kursor.
        ///
        /// SENGAJA TIDAK memakai automation.FromPoint(). Hit-testing milik UI
        /// Automation tidak melewati jendela WS_EX_TRANSPARENT, sehingga
        /// overlay picker kita sendiri yang dikembalikan — dan karena overlay
        /// itu menutupi seluruh layar, kotak sorotannya jadi sebesar layar
        /// alih-alih sebesar textbox yang ditunjuk.
        ///
        /// WindowFromPoint milik Win32 memang melewati jendela transparan,
        /// jadi jendela aplikasi yang benar yang didapat; dari situ pohon UIA
        /// ditelusuri turun ke elemen terkecil yang masih memuat titik kursor.
        /// </summary>
        public static AutomationElement ElementFromPoint(int x, int y)
        {
            try
            {
                var pt = new POINT { X = x, Y = y };
                var hwnd = WindowFromPoint(pt);
                if (hwnd == IntPtr.Zero) return null;

                var root = GetAncestor(hwnd, GA_ROOT);
                if (root == IntPtr.Zero) root = hwnd;

                var element = AutomationProvider.Instance.FromHandle(root);
                if (element == null) return null;

                return Descend(element, x, y, 0);
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Turun ke anak TERKECIL yang memuat titik kursor, berulang.
        ///
        /// Dipilih yang terkecil, bukan yang pertama ketemu: kontrol sering
        /// bertumpuk (panel memuat grup memuat textbox), dan yang diinginkan
        /// user hampir selalu yang paling dalam. Kedalaman dibatasi karena
        /// aplikasi berbasis web-view seperti Postman punya pohon yang sangat
        /// dalam, dan penelusuran tanpa batas membuat kursor terasa berat.
        /// </summary>
        private static AutomationElement Descend(AutomationElement element, int x, int y, int depth)
        {
            if (depth > 25) return element;

            AutomationElement best = null;
            double bestArea = double.MaxValue;

            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    System.Drawing.Rectangle r;
                    try { r = child.BoundingRectangle; } catch (Exception) { continue; }

                    if (r.Width <= 0 || r.Height <= 0) continue;
                    if (x < r.Left || x >= r.Right || y < r.Top || y >= r.Bottom) continue;

                    var area = (double)r.Width * r.Height;
                    if (area < bestArea) { bestArea = area; best = child; }
                }
            }
            catch (Exception) { return element; }

            return best == null ? element : Descend(best, x, y, depth + 1);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        /// <summary>
        /// Jendela tingkat atas yang memuat elemen ini.
        ///
        /// Pohon Visual Tree dibangun MULAI DARI JENDELA, bukan dari desktop:
        /// menelusuri seluruh keturunan desktop berarti membaca isi setiap
        /// aplikasi yang terbuka, dan itu bisa memakan puluhan detik.
        /// </summary>
        public static AutomationElement TopLevelWindowOf(AutomationElement element)
        {
            if (element == null) return null;

            try
            {
                var desktop = AutomationProvider.Instance.GetDesktop();
                var cur = element;
                AutomationElement last = element;

                while (cur != null && !cur.Equals(desktop))
                {
                    last = cur;
                    cur = cur.Parent;
                }

                return last;
            }
            catch (Exception) { return element; }
        }

        /// <summary>
        /// Susun pohon elemen mulai dari sebuah jendela.
        ///
        /// Dibatasi jumlah node dan kedalamannya: aplikasi berbasis web-view
        /// (Postman, VS Code) bisa punya puluhan ribu elemen, dan membacanya
        /// seluruhnya membuat UI Explorer terasa menggantung tanpa penjelasan.
        /// </summary>
        public static DesktopTreeNode BuildTree(AutomationElement root, out int nodeCount,
            out bool truncated, int maxNodes = 5000, int maxDepth = 30)
        {
            nodeCount = 0;
            truncated = false;

            if (root == null) return null;

            var counter = 0;
            var cut = false;
            var tree = Build(root, null, 0, maxDepth, maxNodes, ref counter, ref cut);

            nodeCount = counter;
            truncated = cut;
            return tree;
        }

        private static DesktopTreeNode Build(AutomationElement element, DesktopTreeNode parent,
            int depth, int maxDepth, int maxNodes, ref int counter, ref bool truncated)
        {
            counter++;
            if (counter > maxNodes) { truncated = true; return null; }

            var node = new DesktopTreeNode
            {
                Element = element,
                Parent = parent,
                ControlType = ReadAttribute(element, "ctrltype"),
                Name = ReadAttribute(element, "name"),
                AutomationId = ReadAttribute(element, "automationid"),
                ClassName = ReadAttribute(element, "cls")
            };

            if (depth < maxDepth)
            {
                AutomationElement[] children;
                try { children = element.FindAllChildren(); }
                catch (Exception) { children = new AutomationElement[0]; }

                foreach (var child in children)
                {
                    var built = Build(child, node, depth + 1, maxDepth, maxNodes, ref counter, ref truncated);
                    if (built != null) node.Children.Add(built);
                    if (truncated) break;
                }
            }

            return node;
        }

        /// <summary>
        /// Potret area di sekitar elemen desktop, dengan kotak merah menandai
        /// elemennya — padanan "Informative Screenshot" yang sudah ada untuk
        /// sisi web.
        ///
        /// HARUS dipanggil selagi aplikasi target masih terlihat di layar,
        /// yaitu setelah overlay picker ditutup tapi SEBELUM jendela Studio
        /// dipulihkan. Kalau Studio sudah naik lebih dulu, yang terpotret
        /// justru jendela Studio.
        ///
        /// Mengembalikan null kalau gagal; screenshot itu pemanis, bukan
        /// syarat, jadi kegagalannya tidak boleh menggagalkan Indicate.
        /// </summary>
        public static string CaptureElement(AutomationElement element)
        {
            if (element == null) return null;

            // Angka-angka ini SENGAJA sama dengan yang dipakai cropScreenshot
            // di background.js, supaya screenshot dari web dan dari desktop
            // tampil seragam di canvas.
            const int pad = 50;
            const int baseRegionW = 520;
            const int baseRegionH = 260;

            // Ukuran keluaran TETAP, sama persis dengan cropScreenshot di
            // background.js, supaya kartu di canvas selalu setinggi sama dan
            // hasil desktop tidak terlihat lebih besar/kecil dari hasil web.
            const int OUT_W = 240;
            const int OUT_H = 120;

            try
            {
                var r = element.BoundingRectangle;
                if (r.Width <= 0 || r.Height <= 0) return null;

                int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
                int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                // Area tangkap dibatasi dan dipusatkan ke elemen — lihat
                // catatan di cropScreenshot (background.js): tanpa batas ini,
                // elemen besar seperti area teks Notepad menghasilkan potret
                // seluruh jendela.
                // Batas area dikalikan skala DPI, sama seperti sisi web yang
                // mengalikannya dengan devicePixelRatio. Tanpa ini, di layar
                // ber-scaling 125% bingkai desktop jadi lebih rapat daripada
                // bingkai web walau angkanya sama.
                var dpi = DpiScale();
                int maxRegionW = (int)Math.Round(baseRegionW * dpi);
                int maxRegionH = (int)Math.Round(baseRegionH * dpi);
                int padPx = (int)Math.Round(pad * dpi);

                int w = Math.Min(r.Width + padPx * 2, maxRegionW);
                int h = Math.Min(r.Height + padPx * 2, maxRegionH);

                int cx = r.X + r.Width / 2;
                int cy = r.Y + r.Height / 2;
                int x = cx - w / 2;
                int y = cy - h / 2;

                // Jepit ke dalam batas layar: elemen di tepi menghasilkan area
                // yang sebagian di luar layar, dan CopyFromScreen akan gagal.
                if (x < vx) x = vx;
                if (y < vy) y = vy;
                if (x + w > vx + vw) x = Math.Max(vx, vx + vw - w);
                if (y + h > vy + vh) y = Math.Max(vy, vy + vh - h);
                w = Math.Min(w, vx + vw - x);
                h = Math.Min(h, vy + vh - y);
                if (w <= 0 || h <= 0) return null;

                using (var shot = new System.Drawing.Bitmap(w, h))
                {
                    using (var g = System.Drawing.Graphics.FromImage(shot))
                    {
                        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));

                        using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(229, 57, 53), 2))
                            g.DrawRectangle(pen, r.X - x, r.Y - y, r.Width, r.Height);
                    }

                    var scale = Math.Min((double)OUT_W / w, (double)OUT_H / h);
                    var drawW = Math.Max(1, (int)Math.Round(w * scale));
                    var drawH = Math.Max(1, (int)Math.Round(h * scale));
                    var offX = (OUT_W - drawW) / 2;
                    var offY = (OUT_H - drawH) / 2;

                    using (var resized = new System.Drawing.Bitmap(OUT_W, OUT_H))
                    {
                        using (var g2 = System.Drawing.Graphics.FromImage(resized))
                        {
                            g2.Clear(System.Drawing.Color.White);
                            g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g2.DrawImage(shot, offX, offY, drawW, drawH);
                        }

                        using (var ms = new System.IO.MemoryStream())
                        {
                            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                            return Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Skala DPI layar utama (1.0 = 100%, 1.25 = 125%).
        /// Dibaca dari Graphics karena tidak butuh API khusus versi Windows
        /// tertentu, jadi tetap jalan di Windows 8.1 maupun 11.
        /// </summary>
        private static double DpiScale()
        {
            try
            {
                using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX / 96.0;
            }
            catch (Exception) { return 1.0; }
        }

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        /// <summary>
        /// Sorot elemen desktop.
        ///
        /// Memakai DrawHighlight bawaan FlaUI, BUKAN UIElement.Highlight()
        /// milik OpenRPA: UIElement berada di project OpenRPA utama, dan
        /// project itu sudah mereference Custom.StudioBridge — mereference
        /// balik akan membuat circular reference yang tidak bisa di-build.
        /// </summary>
        public static void Highlight(AutomationElement element)
        {
            if (element == null) return;
            try
            {
                element.DrawHighlight(false,
                    System.Drawing.Color.FromArgb(0, 176, 255),
                    TimeSpan.FromMilliseconds(1500));
            }
            catch (Exception)
            {
                // Elemen bisa hilang atau di luar layar; sorotan gagal bukan
                // alasan menggagalkan alur yang sedang berjalan.
            }
        }

        // ---------------- Ubah tingkat jadi SelectorNode ----------------

        /// <summary>
        /// Ubah rantai desktop jadi SelectorNode, supaya Selector Editor yang
        /// sama dengan sisi web bisa dipakai tanpa UI terpisah.
        /// </summary>
        public static List<SelectorNode> ToSelectorNodes(IList<DesktopLevel> levels)
        {
            var nodes = new List<SelectorNode>();

            foreach (var level in levels)
            {
                var node = new SelectorNode { NodeName = level.NodeName, Tag = level.NodeName };
                foreach (var kv in level.Attributes)
                    node.Attributes.Add(new SelectorAttribute { Name = kv.Key, Value = kv.Value, IsChecked = false });
                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Centang default untuk desktop.
        ///
        /// Jendela tingkat atas selalu ikut dengan "app" — nama proses adalah
        /// penanda paling stabil yang dipunya sebuah aplikasi. "title" TIDAK
        /// dicentang otomatis karena judul jendela sangat sering memuat nama
        /// dokumen atau status yang berubah tiap pemakaian.
        ///
        /// Elemen target memakai automationid kalau ada (paling stabil, memang
        /// dirancang sebagai pengenal), lalu name, baru ctrltype sebagai
        /// pelengkap.
        /// </summary>
        public static void ApplyDesktopDefaults(IList<SelectorNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var isWindow = string.Equals(node.NodeName, Wnd, StringComparison.OrdinalIgnoreCase);
                var isTarget = i == nodes.Count - 1;

                foreach (var a in node.Attributes) a.IsChecked = false;

                if (isWindow)
                {
                    node.IsIncluded = true;
                    Check(node, "app");
                    Check(node, "cls");
                }
                else if (isTarget)
                {
                    node.IsIncluded = true;

                    if (!Check(node, "automationid"))
                    {
                        if (!Check(node, "name")) Check(node, "ctrltype");
                        else Check(node, "ctrltype");
                    }
                }
                else
                {
                    // Tingkat perantara sengaja tidak ikut: selector pendek
                    // lebih tahan perubahan tata letak. Bisa dicentang manual
                    // kalau ternyata hasilnya tidak unik.
                    node.IsIncluded = false;
                }

                node.RefreshDisplay();
            }
        }

        private static bool Check(SelectorNode node, string name)
        {
            var a = node.Attributes.FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (a == null || string.IsNullOrEmpty(a.Value)) return false;
            a.IsChecked = true;
            return true;
        }
    }
}
