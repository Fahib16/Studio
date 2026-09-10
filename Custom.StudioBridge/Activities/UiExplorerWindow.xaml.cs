using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// UI Explorer: telusuri pohon DOM, pilih elemen, susun selector
    /// BERJENJANG dari atribut tiap tingkat, uji hasilnya, simpan.
    ///
    /// TabId ditentukan SEKALI saat window dibuka dan dipegang seumur window.
    /// Kalau tiap aksi memanggil "tab aktif", user yang berpindah tab di
    /// tengah jalan akan diam-diam memvalidasi selector terhadap halaman lain
    /// daripada yang ada di pohon.
    /// </summary>
    public partial class UiExplorerWindow : Window
    {
        private readonly int _tabId;

        /// <summary>
        /// True kalau Explorer sedang menangani target DESKTOP. Ditentukan
        /// sekali saat window dibuka dan tidak berubah: pohon, Validate, dan
        /// Highlight semuanya memakai jalur yang berbeda, jadi mencampurnya di
        /// satu sesi hanya akan membingungkan.
        /// </summary>
        private readonly bool _desktop;

        private DomNode _root;
        private DesktopTreeNode _desktopRoot;

        private readonly ObservableCollection<SelectorNode> _nodes =
            new ObservableCollection<SelectorNode>();
        private readonly ObservableCollection<SelectorAttribute> _selectedAttrs =
            new ObservableCollection<SelectorAttribute>();
        private readonly ObservableCollection<SelectorAttribute> _unselectedAttrs =
            new ObservableCollection<SelectorAttribute>();

        /// <summary>
        /// Simpul pohon yang tingkat-tingkatnya SUDAH dimuat ke Selector Editor.
        ///
        /// Dipakai supaya peristiwa "pilihan pohon berubah" yang menunjuk simpul
        /// yang sama TIDAK menyusun ulang daftar tingkat. Tanpa penjaga ini,
        /// centang yang baru saja diubah user tersapu balik ke tebakan bawaan:
        /// TreeView membuat wadah item-nya secara bertahap saat layout, jadi
        /// pemilihan otomatis waktu memuat pohon bisa baru sampai BELAKANGAN —
        /// termasuk sesudah user sempat mengubah centang. Yang terlihat oleh
        /// user: selector kembali ke bentuk semula, dan Save menyimpan yang
        /// semula itu.
        /// </summary>
        private object _loadedChainSource;

        /// <summary>
        /// Tingkat-tingkat hasil membaca selector yang dibawa saat Explorer
        /// dibuka. Dipakai SEKALI: begitu rantai elemen dibaca dari pohon
        /// (yang memuat SEMUA atribut, bukan cuma yang tersimpan), centangnya
        /// dipulihkan dari sini alih-alih ditebak ulang dari nol.
        /// </summary>
        private System.Collections.Generic.List<SelectorNode> _savedNodes;

        /// <summary>
        /// Menahan pembangunan ulang selector saat kita sendiri yang sedang
        /// mengubah centang secara massal (mis. ApplyDefaults). Tanpa ini,
        /// setiap centang memicu satu validasi ke browser — puluhan
        /// round-trip untuk satu kali pilih elemen.
        /// </summary>
        private bool _suspend;

        public string ResultSelector { get; private set; }
        public string ResultScreenshotBase64 { get; private set; }

        public UiExplorerWindow(int tabId, string currentSelector)
            : this(tabId, currentSelector, false) { }

        public UiExplorerWindow(int tabId, string currentSelector, bool desktopMode)
        {
            InitializeComponent();

            _tabId = tabId;
            _desktop = desktopMode;
            TabLabel.Text = desktopMode ? "Target: aplikasi desktop" : "Tab #" + tabId;

            NodeList.ItemsSource = _nodes;
            SelectedAttrList.ItemsSource = _selectedAttrs;
            UnselectedAttrList.ItemsSource = _unselectedAttrs;

            SelectorTextBox.Text = currentSelector ?? "";

            Loaded += (s, e) =>
            {
                // Selector lama dibaca balik jadi tingkat-tingkat, supaya
                // centangnya dipulihkan alih-alih dimulai dari nol.
                if (SelectorDocument.IsXml(SelectorTextBox.Text))
                {
                    // Disimpan juga sebagai _savedNodes: rantai dari pohon
                    // nanti membawa SEMUA atribut elemen, dan centangnya
                    // dipulihkan dari sini supaya pilihan lama tidak hilang.
                    _savedNodes = SelectorDocument.Parse(SelectorTextBox.Text);
                    LoadNodes(_savedNodes, applyDefaults: false);
                }

                LoadTree(SelectorTextBox.Text);
            };
        }

        // ---------------- Pohon ----------------

        private void LoadTree(string locateSelector)
        {
            if (_desktop) { LoadDesktopTree(locateSelector); return; }

            Mouse.OverrideCursor = Cursors.Wait;
            StatusLabel.Text = "Memuat pohon DOM...";

            try
            {
                var data = StudioPipeClient.GetDomTree(_tabId, locateSelector);
                if (data == null) { StatusLabel.Text = "Tidak ada balasan dari browser."; return; }

                _root = DomNode.FromJson(data["tree"]);
                if (_root == null) { StatusLabel.Text = "Pohon DOM kosong."; return; }

                DomTreeView.ItemsSource = new List<DomNode> { _root };
                _root.IsExpanded = true;

                var truncated = data["truncated"]?.Value<bool>() ?? false;
                var nodeCount = data["nodeCount"]?.Value<int>() ?? 0;
                StatusLabel.Text = truncated
                    ? $"Pohon dipotong pada {nodeCount} node (halaman sangat besar)."
                    : $"{nodeCount} node dimuat.";

                var path = data["path"] as JArray;
                if (path != null)
                {
                    var target = _root.Descend(path.Select(p => p.Value<int>()));
                    if (target != null) { target.ExpandAncestors(); target.IsSelected = true; }
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Gagal memuat pohon.";
                MessageBox.Show(ex.Message, "UI Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { Mouse.OverrideCursor = null; }
        }

        /// <summary>
        /// Muat pohon UI Automation, berakar pada JENDELA yang memuat elemen
        /// dari selector saat ini.
        /// </summary>
        private void LoadDesktopTree(string locateSelector)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            StatusLabel.Text = "Membaca pohon elemen aplikasi...";

            try
            {
                var found = DesktopSelectorResolver.Resolve(locateSelector, 1);
                var target = found.Length > 0 ? found[0] : null;

                if (target == null)
                {
                    StatusLabel.Text = "Elemen tidak ditemukan — pakai Indicate Element untuk memilih ulang.";
                    DomTreeView.ItemsSource = null;
                    return;
                }

                var window = DesktopSelectorResolver.TopLevelWindowOf(target);

                int count; bool truncated;
                _desktopRoot = DesktopSelectorResolver.BuildTree(window, out count, out truncated);

                if (_desktopRoot == null) { StatusLabel.Text = "Pohon elemen kosong."; return; }

                DomTreeView.ItemsSource = new List<DesktopTreeNode> { _desktopRoot };
                _desktopRoot.IsExpanded = true;

                StatusLabel.Text = truncated
                    ? $"Pohon dipotong pada {count} node (aplikasi sangat besar)."
                    : $"{count} node dimuat.";

                var node = _desktopRoot.Find(target);
                if (node != null) { node.ExpandAncestors(); node.IsSelected = true; }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Gagal membaca pohon: " + ex.Message;
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private void DomTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_desktop)
            {
                var dnode = e.NewValue as DesktopTreeNode;
                if (dnode == null || dnode.Element == null) return;
                if (ReferenceEquals(dnode, _loadedChainSource)) return;
                _loadedChainSource = dnode;

                var levels = DesktopSelectorResolver.BuildChain(dnode.Element);
                var nodes = DesktopSelectorResolver.ToSelectorNodes(levels);

                if (_savedNodes != null && _savedNodes.Count > 0)
                {
                    SelectorDocument.ApplySaved(nodes, _savedNodes);
                    _savedNodes = null;
                }
                else
                {
                    DesktopSelectorResolver.ApplyDesktopDefaults(nodes);
                }

                LoadNodes(nodes, applyDefaults: false);
                return;
            }

            var node = e.NewValue as DomNode;
            if (node == null) return;

            // Simpul yang SAMA tidak disusun ulang. TreeView bisa mengirim
            // peristiwa ini lagi untuk simpul yang sudah terpilih — mis. saat
            // wadah item-nya baru dibuat waktu layout — dan menyusun ulang di
            // situ akan menghapus centang yang baru diubah user.
            if (ReferenceEquals(node, _loadedChainSource)) return;
            _loadedChainSource = node;

            LoadChainFor(node.BuildPathSelector());
        }

        // ---------------- Rantai tingkat ----------------

        private void LoadChainFor(string cssPath)
        {
            if (string.IsNullOrEmpty(cssPath)) return;

            try
            {
                var data = StudioPipeClient.GetElementChain(_tabId, cssPath);
                var levels = data?["levels"] as JArray;
                if (levels == null) return;

                var nodes = levels.Select(SelectorNode.FromJson).ToList();

                if (_savedNodes != null && _savedNodes.Count > 0)
                {
                    // Rantai dari pohon membawa SEMUA atribut elemen; centangnya
                    // diambil dari selector yang tersimpan, bukan ditebak ulang.
                    // Inilah yang membuat selector lama benar-benar dipulihkan —
                    // sebelumnya ia selalu tertimpa tebakan bawaan begitu pohon
                    // selesai memilih elemen targetnya.
                    SelectorDocument.ApplySaved(nodes, _savedNodes);
                    _savedNodes = null;
                    LoadNodes(nodes, applyDefaults: false);
                }
                else
                {
                    LoadNodes(nodes, applyDefaults: true);
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Gagal ambil rantai elemen: " + ex.Message;
            }
        }

        private void LoadNodes(IList<SelectorNode> nodes, bool applyDefaults)
        {
            _suspend = true;
            try
            {
                foreach (var old in _nodes) Unsubscribe(old);
                _nodes.Clear();

                foreach (var n in nodes) { Subscribe(n); _nodes.Add(n); }

                if (applyDefaults) SelectorDocument.ApplyDefaults(_nodes.ToList());
            }
            finally { _suspend = false; }

            // Tingkat target (paling bawah) yang disorot lebih dulu — itu yang
            // hampir selalu ingin disetel user.
            if (_nodes.Count > 0) NodeList.SelectedIndex = _nodes.Count - 1;

            RebuildSelector();
            UpdateTargetLabel();
        }

        private void Subscribe(SelectorNode node)
        {
            node.PropertyChanged += Node_PropertyChanged;
            foreach (var a in node.Attributes) a.PropertyChanged += Attr_PropertyChanged;
        }

        private void Unsubscribe(SelectorNode node)
        {
            node.PropertyChanged -= Node_PropertyChanged;
            foreach (var a in node.Attributes) a.PropertyChanged -= Attr_PropertyChanged;
        }

        private void Node_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suspend) return;
            if (e.PropertyName == "IsIncluded") RebuildSelector();
        }

        private void Attr_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suspend || e.PropertyName != "IsChecked") return;

            var node = _nodes.FirstOrDefault(n => n.Attributes.Contains(sender as SelectorAttribute));
            if (node != null)
            {
                // Mencentang atribut di tingkat yang belum ikut jelas berarti
                // user ingin tingkat itu ikut — daripada memaksa dia mencentang
                // dua tempat untuk satu niat.
                var attr = sender as SelectorAttribute;
                if (attr != null && attr.IsChecked && !node.IsIncluded)
                {
                    _suspend = true;
                    node.IsIncluded = true;
                    _suspend = false;
                }
                node.RefreshDisplay();
            }

            RefreshAttributePanels();
            RebuildSelector();
        }

        private void NodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshAttributePanels();

            var node = NodeList.SelectedItem as SelectorNode;
            PropertyGrid.ItemsSource = node?.Attributes;
        }

        private void RefreshAttributePanels()
        {
            var node = NodeList.SelectedItem as SelectorNode;

            _selectedAttrs.Clear();
            _unselectedAttrs.Clear();
            if (node == null) return;

            foreach (var a in node.Attributes)
            {
                if (a.IsChecked) _selectedAttrs.Add(a);
                else _unselectedAttrs.Add(a);
            }
        }

        private void RebuildSelector()
        {
            if (_nodes.Count == 0) return;

            foreach (var n in _nodes) n.RefreshDisplay();

            SelectorTextBox.Text = SelectorDocument.Build(_nodes);
            Validate();
        }

        private void UpdateTargetLabel()
        {
            var last = _nodes.LastOrDefault();
            TargetLabel.Text = last == null
                ? "Target Element: (belum ada)"
                : "Target Element: '" + (last.Tag ?? "?").ToUpperInvariant() + "'";
        }

        // ---------------- Tombol ----------------

        private void Indicate_Click(object sender, RoutedEventArgs e)
        {
            if (_desktop) { IndicateDesktop(); return; }

            // Jendela Explorer disingkirkan selama pemilihan berlangsung: ia
            // lebar dan selalu di depan, jadi kalau dibiarkan ia menutupi
            // halaman yang justru sedang mau ditunjuk. Dikembalikan lagi di
            // blok finally, apa pun hasilnya — termasuk saat user menekan Esc.
            Visibility = Visibility.Hidden;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var picked = StudioPipeClient.StartIndicate(_tabId);
                if (picked == null) return; // Escape

                // Elemen baru ditunjuk sendiri oleh user: selector lama tidak lagi
                // relevan, jadi centangnya jangan dipulihkan ke elemen ini.
                _savedNodes = null;

                var shot = picked["screenshotBase64"]?.Value<string>();
                if (!string.IsNullOrEmpty(shot)) ResultScreenshotBase64 = shot;

                var levels = picked["levels"] as JArray;
                if (levels != null)
                    LoadNodes(levels.Select(SelectorNode.FromJson).ToList(), applyDefaults: true);

                var cssPath = picked["cssPath"]?.Value<string>();
                if (!string.IsNullOrEmpty(cssPath)) LoadTree(cssPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "UI Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                Visibility = Visibility.Visible;
                IndicateHelper.FocusWindow(this); // rebut kembali fokus dari browser
            }
        }

        /// <summary>
        /// Pilih ulang elemen desktop dari dalam Explorer.
        ///
        /// Jendela Explorer disembunyikan selama pemilihan: dia menutupi
        /// sebagian layar, dan picker akan menyorot Explorer itu sendiri kalau
        /// kursor melintasinya.
        /// </summary>
        private void IndicateDesktop()
        {
            Visibility = Visibility.Hidden;

            try
            {
                var picker = new DesktopPickerWindow();
                picker.ShowDialog();

                if (picker.SelectedElement == null) return;

                _savedNodes = null;

                var levels = DesktopSelectorResolver.BuildChain(picker.SelectedElement);
                var nodes = DesktopSelectorResolver.ToSelectorNodes(levels);
                DesktopSelectorResolver.ApplyDesktopDefaults(nodes);
                LoadNodes(nodes, applyDefaults: false);

                LoadDesktopTree(SelectorTextBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "UI Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Visibility = Visibility.Visible;
                IndicateHelper.FocusWindow(this);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadTree(SelectorTextBox.Text);
        }

        private void Validate_Click(object sender, RoutedEventArgs e) { Validate(); }

        private async void Highlight_Click(object sender, RoutedEventArgs e)
        {
            var selector = SelectorTextBox.Text;
            if (string.IsNullOrWhiteSpace(selector)) return;

            if (_desktop)
            {
                try
                {
                    var found = DesktopSelectorResolver.Resolve(selector, 1);
                    if (found.Length == 0) { StatusLabel.Text = "Elemen tidak ditemukan."; return; }

                    DesktopSelectorResolver.Highlight(found[0]);
                    StatusLabel.Text = "Disorot di aplikasi.";
                }
                catch (Exception ex) { StatusLabel.Text = "Gagal highlight: " + ex.Message; }
                return;
            }

            try
            {
                // Aksi khusus Explorer: bawa tab ke depan, gulir ke tengah,
                // sorot biru terang. Terpisah dari aksi highlight runtime.
                StudioPipeClient.ExplorerHighlight(_tabId, selector);
                StatusLabel.Text = "Disorot di browser...";

                // Sorotan di halaman hidup 1500ms. Ditunggu sedikit lebih lama
                // supaya user sempat melihatnya utuh sebelum jendela ini naik
                // lagi dan menutupi browser.
                await Task.Delay(2100);

                IndicateHelper.FocusWindow(this);
                StatusLabel.Text = "Selesai disorot.";
            }
            catch (Exception ex)
            {
                IndicateHelper.FocusWindow(this);
                StatusLabel.Text = "Gagal highlight: " + ex.Message;
            }
        }

        private void SelectorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Sengaja TIDAK validasi otomatis tiap ketikan: satu validasi itu
            // satu round-trip ke browser, jadi mengetik selector panjang akan
            // memicu puluhan panggilan. Validasi jalan lewat tombol, atau
            // otomatis saat selector disusun ulang dari centang.
        }

        private void Validate()
        {
            var selector = SelectorTextBox.Text;
            if (string.IsNullOrWhiteSpace(selector)) { StatusLabel.Text = "Selector kosong."; return; }

            if (_desktop)
            {
                try
                {
                    var count = DesktopSelectorResolver.Count(selector);
                    StatusLabel.Text = count == 0 ? "Tidak ada elemen yang cocok."
                        : count == 1 ? "Cocok ke 1 elemen (unik)."
                        : $"Cocok ke {count} elemen — yang PERTAMA yang akan kena.";
                }
                catch (Exception ex) { StatusLabel.Text = "Gagal validasi: " + ex.Message; }
                return;
            }

            try
            {
                var data = StudioPipeClient.ValidateSelector(_tabId, selector);
                if (data == null) { StatusLabel.Text = "Tidak ada balasan."; return; }

                var valid = data["valid"]?.Value<bool>() ?? false;
                if (!valid)
                {
                    StatusLabel.Text = "Tidak valid: " + (data["error"]?.Value<string>() ?? "");
                    return;
                }

                var count = data["count"]?.Value<int>() ?? 0;
                if (count == 0) StatusLabel.Text = "Tidak ada elemen yang cocok.";
                else if (count == 1) StatusLabel.Text = "Cocok ke 1 elemen (unik).";
                else StatusLabel.Text = $"Cocok ke {count} elemen — yang PERTAMA yang akan kena.";
            }
            catch (Exception ex) { StatusLabel.Text = "Gagal validasi: " + ex.Message; }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var selector = SelectorTextBox.Text;
            if (string.IsNullOrWhiteSpace(selector))
            {
                MessageBox.Show("Selector masih kosong.", "UI Explorer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultSelector = selector.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    }
}
