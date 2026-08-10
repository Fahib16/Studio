using System;
using System.Activities;
using System.Activities.Presentation.PropertyEditing;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenRPA.Interfaces;

namespace OpenRPA.Activities.Custom
{
    /// <summary>
    /// Activity kustom: Click ala UiPath untuk OpenRPA — VERSI 4.
    ///
    /// PERUBAHAN DARI v3:
    ///   1. Property "Target" (Windows/Web) DIHAPUS. Sekarang auto-detect
    ///      dari isi Selector JSON itu sendiri saat Execute() — field
    ///      "Selector":"Windows" atau "Selector":"NM" di item pertama JSON
    ///      sudah cukup jadi penanda (lihat DetectIsWeb). Designer tetap
    ///      punya 2 tombol Indicate terpisah (Desktop/Web) karena SelectorWindow
    ///      butuh tahu jenisnya SEBELUM dialog dibuka -- itu satu-satunya
    ///      bagian yang tidak bisa auto, karena saya tidak punya source
    ///      SelectorWindow.cs untuk tahu apakah dia bisa auto-detect sendiri.
    ///   2. TAMBAH AnchorSelector (InArgument<string>) -- untuk kasus tombol
    ///      yang posisinya berubah-ubah (dynamic). Elemen anchor di-resolve
    ///      dulu, lalu Selector utama dicari RELATIF ke anchor itu (parameter
    ///      "from" di GetElementsWithuiSelector) -- pola sama persis dengan
    ///      From di GetElement.cs.
    ///   3. Property dikelompokkan pakai [Category] supaya Properties panel
    ///      tersusun mirip UiPath (Target / Input / Options / Common).
    /// </summary>
    [Designer(typeof(Design.UiPathStyleClickDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class UiPathStyleClick : CodeActivity
    {
        public UiPathStyleClick()
        {
            VirtualClick = Config.local.use_virtual_click;
            AnimateMouse = Config.local.use_animate_mouse;
            PostWait = Config.local.use_postwait;
        }

        // ===================== TARGET =====================

        [Category("Target")]
        [DisplayName("Selector")]
        [Description("Selector elemen target. Diisi otomatis lewat tombol Indicate di designer.")]
        public InArgument<string> Selector { get; set; }

        [Category("Target")]
        [DisplayName("Anchor Selector")]
        [Description("OPSIONAL. Kalau diisi, Selector di atas dicari RELATIF terhadap elemen anchor ini -- " +
                      "berguna untuk tombol yang posisinya berpindah-pindah tapi tetap berdekatan dengan " +
                      "elemen lain yang posisinya stabil (mis. label di sampingnya).")]
        public InArgument<string> AnchorSelector { get; set; }

        [Category("Target")]
        [DisplayName("Element")]
        [Description("Fallback opsional kalau Selector kosong (mis. dari variabel \"item\" hasil GetElement)")]
        public InArgument<IElement> Element { get; set; }

        [Category("Target")]
        [DisplayName("Timeout")]
        [Description("Batas waktu retry mencari elemen (default 3 detik kalau kosong)")]
        public InArgument<TimeSpan> Timeout { get; set; }

        [Browsable(false)]
        public string Image { get; set; }

        [Browsable(false)]
        public string AnchorImage { get; set; }

        // ===================== INPUT =====================

        [Category("Input")]
        [DisplayName("Click Type")]
        public ClickTypeAlias ClickType { get; set; } = ClickTypeAlias.Single;

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Mouse Button")]
        [Description("0 = Left, 1 = Right, 2 = Middle")]
        public InArgument<int> Button { get; set; } = (int)Input.MouseButton.Left;

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Double Click")]
        public InArgument<bool> DoubleClick { get; set; } = false;

        [Category("Input")]
        [Editor(typeof(KeyModifiersOptionsEditor), typeof(ExtendedPropertyValueEditor))]
        [DisplayName("Key Modifiers")]
        public InArgument<string> KeyModifiers { get; set; }

        // ===================== OPTIONS =====================

        [Category("Options")]
        [RequiredArgument]
        [DisplayName("Offset X")]
        public InArgument<int> OffsetX { get; set; } = 5;

        [Category("Options")]
        [RequiredArgument]
        [DisplayName("Offset Y")]
        public InArgument<int> OffsetY { get; set; } = 5;

        [Category("Options")]
        [DisplayName("Virtual Click")]
        [Description("True: kirim event klik langsung tanpa gerakkan kursor fisik (setara SimulateClick UiPath)")]
        public InArgument<bool> VirtualClick { get; set; }

        [Category("Options")]
        [DisplayName("Animate Mouse")]
        public InArgument<bool> AnimateMouse { get; set; }

        [Category("Options")]
        [RequiredArgument]
        [DisplayName("Focus")]
        public InArgument<bool> Focus { get; set; } = false;

        // ===================== COMMON =====================

        [Category("Common")]
        [DisplayName("Post Wait")]
        public InArgument<TimeSpan> PostWait { get; set; }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            metadata.RequireExtension<OpenRPA.Windows.WindowsCacheExtension>();
            base.CacheMetadata(metadata);
        }

        protected override void Execute(CodeActivityContext context)
        {
            var selectorStr = Selector != null ? Selector.Get(context) : null;
            var anchorStr = AnchorSelector != null ? AnchorSelector.Get(context) : null;
            var timeout = Timeout != null ? Timeout.Get(context) : TimeSpan.Zero;
            if (timeout == TimeSpan.Zero) timeout = TimeSpan.FromSeconds(3);

            IElement el;
            if (!string.IsNullOrEmpty(selectorStr))
            {
                selectorStr = OpenRPA.Interfaces.Selector.Selector.ReplaceVariables(selectorStr, context.DataContext);
                if (!string.IsNullOrEmpty(anchorStr))
                    anchorStr = OpenRPA.Interfaces.Selector.Selector.ReplaceVariables(anchorStr, context.DataContext);

                el = ResolveElement(selectorStr, anchorStr, timeout, context);
            }
            else
            {
                el = Element != null ? Element.Get(context) : null;
                if (el == null) throw new ArgumentException("Element atau Selector harus diisi salah satu");
            }

            bool doubleclick = DoubleClick != null ? DoubleClick.Get(context) : false;
            if (ClickType == ClickTypeAlias.Double) doubleclick = true;

            var button = Button.Get(context);
            var virtualClick = VirtualClick != null ? VirtualClick.Get(context) : false;
            var animatemouse = AnimateMouse != null ? AnimateMouse.Get(context) : false;
            var focus = Focus != null ? Focus.Get(context) : false;
            var keymodifiers = KeyModifiers != null ? KeyModifiers.Get(context) : "";

            if (focus)
            {
                el.Focus();
                el.Refresh();
            }

            var disposes = new System.Collections.Generic.List<IDisposable>();
            var keys = TypeText.GetKeys(keymodifiers);
            foreach (var vk in keys) disposes.Add(FlaUI.Core.Input.Keyboard.Pressing(vk));

            var _button = (Input.MouseButton)button;
            el.Click(virtualClick, _button, OffsetX.Get(context), OffsetY.Get(context), doubleclick, animatemouse);

            TimeSpan postwait = PostWait != null ? PostWait.Get(context) : TimeSpan.Zero;
            if (postwait != TimeSpan.Zero)
            {
                System.Threading.Thread.Sleep(postwait);
            }

            disposes.ForEach(x => x.Dispose());
        }

        /// <summary>
        /// Auto-detect: baca field "Selector" di item PERTAMA JSON selector.
        /// "NM" = web (native messaging), selain itu (termasuk "Windows"
        /// atau kalau gagal parse) dianggap Windows/desktop.
        /// </summary>
        internal static bool DetectIsWeb(string selectorJson)
        {
            try
            {
                var arr = JArray.Parse(selectorJson);
                if (arr.Count > 0 && arr[0] is JObject first)
                {
                    var kind = first["Selector"]?.ToString();
                    return string.Equals(kind, "NM", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Parse gagal -> fallback ke Windows, jangan sampai activity crash
                // cuma gara-gara deteksi jenis selector.
            }
            return false;
        }

        /// <summary>
        /// Resolve elemen target, dengan dukungan anchor opsional. Retry
        /// sampai timeout habis, sama pola dengan do-while di GetElement.cs.
        /// </summary>
        private static IElement ResolveElement(string selectorJson, string anchorJson, TimeSpan timeout, CodeActivityContext context)
        {
            bool isWeb = DetectIsWeb(selectorJson);
            var sw = Stopwatch.StartNew();

            if (isWeb)
            {
                OpenRPA.NM.NMElement anchorEl = ResolveAnchorWeb(anchorJson, timeout, sw);
                var sel = new OpenRPA.NM.NMSelector(selectorJson);
                OpenRPA.NM.NMElement[] elements;
                do
                {
                    elements = OpenRPA.NM.NMSelector.GetElementsWithuiSelector(sel, anchorEl, 1);
                } while ((elements == null || elements.Length == 0) && sw.Elapsed < timeout);

                if (elements == null || elements.Length == 0)
                    throw new ElementNotFoundException("Click: elemen (Web) tidak ditemukan untuk Selector yang diisi");
                return elements[0];
            }
            else
            {
                var ext = context.GetExtension<OpenRPA.Windows.WindowsCacheExtension>();
                OpenRPA.UIElement anchorEl = ResolveAnchorWindows(anchorJson, timeout, sw, ext);
                var sel = new OpenRPA.Windows.WindowsSelector(selectorJson);
                OpenRPA.UIElement[] elements;
                do
                {
                    elements = OpenRPA.Windows.WindowsSelector.GetElementsWithuiSelector(sel, anchorEl, 1, ext);
                } while ((elements == null || elements.Length == 0) && sw.Elapsed < timeout);

                if (elements == null || elements.Length == 0)
                    throw new ElementNotFoundException("Click: elemen (Windows) tidak ditemukan untuk Selector yang diisi");
                return elements[0];
            }
        }

        private static OpenRPA.NM.NMElement ResolveAnchorWeb(string anchorJson, TimeSpan timeout, Stopwatch sw)
        {
            if (string.IsNullOrEmpty(anchorJson)) return null;
            var anchorSel = new OpenRPA.NM.NMSelector(anchorJson);
            OpenRPA.NM.NMElement[] elements;
            do
            {
                elements = OpenRPA.NM.NMSelector.GetElementsWithuiSelector(anchorSel, null, 1);
            } while ((elements == null || elements.Length == 0) && sw.Elapsed < timeout);

            if (elements == null || elements.Length == 0)
                throw new ElementNotFoundException("Click: elemen ANCHOR (Web) tidak ditemukan");
            return elements[0];
        }

        private static OpenRPA.UIElement ResolveAnchorWindows(string anchorJson, TimeSpan timeout, Stopwatch sw, OpenRPA.Windows.WindowsCacheExtension ext)
        {
            if (string.IsNullOrEmpty(anchorJson)) return null;
            var anchorSel = new OpenRPA.Windows.WindowsSelector(anchorJson);
            OpenRPA.UIElement[] elements;
            do
            {
                elements = OpenRPA.Windows.WindowsSelector.GetElementsWithuiSelector(anchorSel, null, 1, ext);
            } while ((elements == null || elements.Length == 0) && sw.Elapsed < timeout);

            if (elements == null || elements.Length == 0)
                throw new ElementNotFoundException("Click: elemen ANCHOR (Windows) tidak ditemukan");
            return elements[0];
        }
    }

    public enum ClickTypeAlias
    {
        Single,
        Double
    }

    class KeyModifiersOptionsEditor : CustomSelectEditor
    {
        public override System.Data.DataTable options
        {
            get
            {
                var lst = new System.Data.DataTable();
                lst.Columns.Add("ID", typeof(string));
                lst.Columns.Add("TEXT", typeof(string));
                lst.Rows.Add("{LCONTROL}", "Left Control");
                lst.Rows.Add("{LMENU}", "Left Menu\\Alt");
                lst.Rows.Add("{LSHIFT}", "Left Shift");
                return lst;
            }
        }
    }
}
