using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Satu baris atribut di panel kanan UI Explorer.
    /// </summary>
    public class SelectorAttribute : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Value { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get { return _isChecked; }
            set { if (_isChecked == value) return; _isChecked = value; Notify("IsChecked"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string p)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(p));
        }
    }

    /// <summary>
    /// Satu TINGKAT selector, sepadan dengan satu baris
    /// &lt;webctrl ... /&gt; di Selector Editor UiPath.
    ///
    /// Selector akhir adalah gabungan tingkat-tingkat yang dicentang, masing
    /// masing hanya memuat atribut yang dicentang di tingkat itu.
    /// </summary>
    public class SelectorNode : INotifyPropertyChanged
    {
        /// <summary>
        /// Nama simpul XML: "webctrl" untuk elemen di dalam halaman web,
        /// "wnd" untuk jendela desktop, "ctrl" untuk kontrol di dalamnya.
        /// Inilah yang membedakan selector web dan desktop, sekaligus yang
        /// membuat Selector Editor yang sama bisa dipakai untuk keduanya.
        /// </summary>
        public string NodeName { get; set; } = "webctrl";

        /// <summary>Nama tag elemen ini di halaman (button, div, ...).</summary>
        public string Tag { get; set; }

        /// <summary>Path CSS penuh ke elemen ini; dipakai atribut css-selector.</summary>
        public string CssPath { get; set; }

        public ObservableCollection<SelectorAttribute> Attributes { get; } =
            new ObservableCollection<SelectorAttribute>();

        private bool _isIncluded;
        public bool IsIncluded
        {
            get { return _isIncluded; }
            set { if (_isIncluded == value) return; _isIncluded = value; Notify("IsIncluded"); Notify("Display"); }
        }

        /// <summary>Baris XML untuk tingkat ini, seperti yang tampil di editor.</summary>
        public string Display
        {
            get { return ToXml(); }
        }

        public string ToXml()
        {
            var sb = new StringBuilder("<").Append(NodeName ?? "webctrl");
            foreach (var a in Attributes.Where(x => x.IsChecked))
                sb.Append(" ").Append(a.Name).Append("='").Append(EscapeValue(a.Value)).Append("'");
            sb.Append(" />");
            return sb.ToString();
        }

        /// <summary>
        /// Nilai atribut dibungkus kutip tunggal (konvensi UiPath), jadi kutip
        /// tunggal DI DALAM nilai harus diganti. Tidak ada aturan escape resmi
        /// di format ini, jadi dipakai kutip ganda sebagai pengganti — lebih
        /// aman daripada menghasilkan baris yang tidak bisa di-parse balik.
        /// </summary>
        private static string EscapeValue(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return v.Replace("'", "\"").Replace("\r", " ").Replace("\n", " ");
        }

        public void RefreshDisplay() { Notify("Display"); }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string p)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(p));
        }

        /// <summary>
        /// Bangun satu tingkat dari data "levels" yang dikirim extension.
        ///
        /// Selain atribut HTML asli, ditambahkan atribut TURUNAN yang tidak ada
        /// di DOM tapi berguna sebagai penanda dan memang dipakai UiPath:
        /// tag, innertext, parentclass, isleaf, css-selector. Tanpa ini,
        /// elemen tanpa id/name (sangat umum di aplikasi modern) praktis tidak
        /// punya apa pun yang bisa dicentang.
        /// </summary>
        public static SelectorNode FromJson(JToken level)
        {
            var node = new SelectorNode
            {
                Tag = level["tag"]?.Value<string>() ?? "",
                CssPath = level["css"]?.Value<string>() ?? ""
            };

            void Add(string name, string value, bool preferred)
            {
                if (string.IsNullOrEmpty(value)) return;
                node.Attributes.Add(new SelectorAttribute { Name = name, Value = value, IsChecked = preferred });
            }

            Add("tag", node.Tag, false);

            var attrs = level["attrs"] as JObject;
            if (attrs != null)
            {
                foreach (var p in attrs.Properties())
                    Add(p.Name, p.Value?.Value<string>(), false);
            }

            // Kumpulan atribut turunan disamakan dengan UI Explorer UiPath,
            // supaya orang yang terbiasa di sana menemukan penanda yang sama:
            //
            //   aaname            nama yang dibacakan pembaca layar
            //   innertext         teks yang terlihat, spasinya sudah dirapikan
            //   visibleinnertext  sama dengan innertext untuk elemen web
            //   owntext           teks milik elemen itu sendiri, bukan anaknya
            //   parentclass       class milik induk, penanda yang stabil
            //   isleaf            1 kalau elemen tidak punya anak
            //   css-selector      jalur CSS lengkap, penanda paling tepat
            Add("aaname", level["aaname"]?.Value<string>(), false);
            Add("innertext", level["innertext"]?.Value<string>(), false);
            Add("visibleinnertext", level["visibleinnertext"]?.Value<string>(), false);
            Add("owntext", level["owntext"]?.Value<string>(), false);
            Add("parentclass", level["parentclass"]?.Value<string>(), false);
            Add("css-selector", node.CssPath, false);

            var isleaf = level["isleaf"]?.Value<bool>() ?? false;
            if (isleaf) Add("isleaf", "1", false);

            return node;
        }
    }

    /// <summary>
    /// Menyusun, membaca, dan menebak-default selector berjenjang.
    /// </summary>
    public static class SelectorDocument
    {
        /// <summary>Selector diawali '&lt;' berarti format XML berjenjang.</summary>
        public static bool IsXml(string selector)
        {
            return !string.IsNullOrWhiteSpace(selector) && selector.TrimStart().StartsWith("<");
        }

        public static string Build(IEnumerable<SelectorNode> nodes)
        {
            var lines = nodes.Where(n => n.IsIncluded).Select(n => n.ToXml()).ToList();
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Tentukan tingkat dan atribut mana yang dicentang saat elemen baru
        /// dipilih.
        ///
        /// Aturannya meniru perilaku UiPath: pakai SESEDIKIT mungkin tingkat.
        /// Tingkat terakhir (elemen target) selalu ikut; tingkat leluhur hanya
        /// ikut kalau punya id — karena id itulah satu-satunya penanda leluhur
        /// yang biasanya bertahan saat halaman berubah.
        ///
        /// "class" SENGAJA tidak dicentang otomatis: di aplikasi modern nama
        /// class kerap dihasilkan build tool dan berganti tiap deploy, jadi
        /// mencentangnya otomatis menghasilkan selector yang cepat rusak tanpa
        /// user sadar.
        /// </summary>
        public static void ApplyDefaults(IList<SelectorNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var isTarget = i == nodes.Count - 1;

                foreach (var a in node.Attributes) a.IsChecked = false;

                var id = node.Attributes.FirstOrDefault(a => a.Name == "id");
                var name = node.Attributes.FirstOrDefault(a => a.Name == "name");
                var testid = node.Attributes.FirstOrDefault(a =>
                    a.Name == "data-testid" || a.Name == "data-test" || a.Name == "data-cy");
                var aria = node.Attributes.FirstOrDefault(a => a.Name == "aria-label");
                var tag = node.Attributes.FirstOrDefault(a => a.Name == "tag");
                var innertext = node.Attributes.FirstOrDefault(a => a.Name == "innertext");
                var aaname = node.Attributes.FirstOrDefault(a => a.Name == "aaname");

                if (isTarget)
                {
                    node.IsIncluded = true;

                    if (id != null) { id.IsChecked = true; }
                    else if (testid != null) { testid.IsChecked = true; if (tag != null) tag.IsChecked = true; }
                    else if (name != null) { name.IsChecked = true; if (tag != null) tag.IsChecked = true; }
                    else if (aria != null) { aria.IsChecked = true; if (tag != null) tag.IsChecked = true; }
                    else if (aaname != null && aaname.Value.Length > 0 && aaname.Value.Length <= 60)
                    {
                        // Pilihan bawaan yang sama dengan UiPath untuk tombol
                        // dan tautan: nama aksesibilitas + tag. Lebih tahan
                        // banting daripada teks mentah, karena tidak terpengaruh
                        // huruf besar-kecil hasil CSS maupun ikon di dalamnya.
                        aaname.IsChecked = true;
                        if (tag != null) tag.IsChecked = true;
                    }
                    else
                    {
                        if (tag != null) tag.IsChecked = true;

                        // Teks jadi penanda terakhir kalau tidak ada atribut
                        // stabil apa pun. Dibatasi teks pendek: teks panjang
                        // biasanya konten yang berubah-ubah, bukan label.
                        if (innertext != null && innertext.Value.Length <= 40) innertext.IsChecked = true;
                    }
                }
                else
                {
                    node.IsIncluded = id != null;
                    if (id != null) id.IsChecked = true;
                }

                node.RefreshDisplay();
            }
        }

        /// <summary>
        /// Pulihkan centang dari selector yang TERSIMPAN ke rantai tingkat yang
        /// baru dibaca dari pohon.
        ///
        /// Kenapa perlu: selector yang tersimpan hanya memuat atribut yang
        /// dipilih user (mis. satu baris <c>&lt;webctrl id='RtHBK' /&gt;</c>),
        /// sedangkan rantai dari pohon memuat SELURUH atribut elemen beserta
        /// leluhurnya. Yang dipakai adalah rantai lengkapnya — supaya user bisa
        /// berpindah ke atribut lain — tapi centangnya harus tetap yang dipilih
        /// user, bukan tebakan bawaan.
        ///
        /// Pencocokan dilakukan dari BELAKANG karena tingkat terakhir selalu
        /// elemen target, sementara tingkat leluhur boleh melompat: selector
        /// jarang menyebut setiap tingkat DOM.
        /// </summary>
        public static void ApplySaved(IList<SelectorNode> nodes, IList<SelectorNode> saved)
        {
            if (nodes == null || nodes.Count == 0 || saved == null || saved.Count == 0) return;

            foreach (var n in nodes)
            {
                n.IsIncluded = false;
                foreach (var a in n.Attributes) a.IsChecked = false;
            }

            var cursor = nodes.Count - 1;

            for (int i = saved.Count - 1; i >= 0 && cursor >= 0; i--)
            {
                var want = saved[i];

                var found = -1;
                for (int j = cursor; j >= 0; j--)
                {
                    if (LevelMatches(nodes[j], want)) { found = j; break; }
                }
                if (found < 0) continue;

                var level = nodes[found];
                level.IsIncluded = true;

                foreach (var a in want.Attributes)
                {
                    var same = FindAttribute(level, a.Name);
                    if (same != null) same.IsChecked = true;
                }

                cursor = found - 1;
            }

            // Tidak satu tingkat pun cocok — mis. halaman sudah berubah sejak
            // selector disimpan. Lebih baik kembali ke tebakan bawaan daripada
            // meninggalkan selector kosong yang tidak cocok ke apa pun.
            if (!nodes.Any(n => n.IsIncluded)) ApplyDefaults(nodes);

            foreach (var n in nodes) n.RefreshDisplay();
        }

        private static SelectorAttribute FindAttribute(SelectorNode node, string name)
        {
            return node.Attributes.FirstOrDefault(
                x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Satu tingkat pohon dianggap tingkat yang dimaksud selector kalau
        /// SEMUA atribut yang disebut selector ada di sana dengan nilai yang
        /// sama. Nilai berpola (mengandung * atau ?) cukup dicocokkan namanya:
        /// membandingkannya secara harfiah justru selalu gagal.
        /// </summary>
        private static bool LevelMatches(SelectorNode node, SelectorNode want)
        {
            foreach (var a in want.Attributes)
            {
                var same = FindAttribute(node, a.Name);
                if (same == null) return false;

                var value = a.Value ?? "";
                if (value.IndexOf('*') >= 0 || value.IndexOf('?') >= 0) continue;

                if (!string.Equals(CollapseSpace(same.Value), CollapseSpace(value),
                                   StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        /// <summary>
        /// Rentetan spasi jadi satu spasi — sama seperti perapian di sisi
        /// extension, supaya atribut teks yang di DOM-nya mengandung baris baru
        /// tetap dikenali sama dengan yang tersimpan di selector.
        /// </summary>
        private static string CollapseSpace(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return Regex.Replace(v, @"\s+", " ").Trim();
        }

        private static readonly Regex NodeRe =
            new Regex(@"<\s*([\w:-]+)([^>]*?)/?>", RegexOptions.Compiled);
        private static readonly Regex AttrRe =
            new Regex(@"([\w:.\-]+)\s*=\s*(['""])([\s\S]*?)\2", RegexOptions.Compiled);

        /// <summary>
        /// Baca selector XML jadi daftar tingkat. Dipakai saat UI Explorer
        /// dibuka atas selector yang sudah tersimpan di activity, supaya
        /// centangnya bisa dipulihkan alih-alih dimulai dari nol.
        /// </summary>
        public static List<SelectorNode> Parse(string selector)
        {
            var result = new List<SelectorNode>();
            if (string.IsNullOrWhiteSpace(selector)) return result;

            foreach (Match m in NodeRe.Matches(selector))
            {
                var node = new SelectorNode { IsIncluded = true };

                foreach (Match a in AttrRe.Matches(m.Groups[2].Value))
                {
                    var name = a.Groups[1].Value.ToLowerInvariant();
                    var value = a.Groups[3].Value;

                    node.Attributes.Add(new SelectorAttribute { Name = name, Value = value, IsChecked = true });

                    if (name == "tag") node.Tag = value;
                    if (name == "css-selector") node.CssPath = value;
                }

                node.NodeName = m.Groups[1].Value.ToLowerInvariant();
                if (string.IsNullOrEmpty(node.Tag)) node.Tag = node.NodeName;
                result.Add(node);
            }

            return result;
        }
    }
}
