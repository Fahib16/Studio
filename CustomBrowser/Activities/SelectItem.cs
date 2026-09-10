using System;
using System.Activities;
using System.ComponentModel;
using Custom.StudioBridge;

namespace Custom.Browser
{
    /// <summary>
    /// Memilih satu item pada dropdown.
    ///
    /// Item dicocokkan ke TEKS yang terlihat lebih dulu, baru ke value —
    /// karena yang dilihat orang saat menyusun workflow adalah teksnya,
    /// sedangkan value sering berupa kode yang tidak muncul di layar.
    /// By Index mengubahnya menjadi pemilihan berdasarkan nomor urut (mulai 0).
    ///
    /// Bekerja untuk &lt;select&gt; di halaman web maupun combo box/list
    /// aplikasi desktop.
    /// </summary>
    [Designer(typeof(Design.SelectItemDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Select Item")]
    [Description("Memilih item pada dropdown web atau desktop.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.selectitem.png")]
    public sealed class SelectItem : ElementActivityBase
    {
        public SelectItem()
        {
            DisplayName = "Select Item";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Item")]
        [Description("Teks item yang dipilih (atau nomor urut kalau By Index true).")]
        public InArgument<string> Item { get; set; }

        [Category("Options")]
        [DisplayName("By Index")]
        [Description("Kalau true, Item diperlakukan sebagai nomor urut mulai 0 (default false). " +
                     "Hanya berlaku untuk target web.")]
        public InArgument<bool> ByIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = GetContinueOnError(context);

            try
            {
                var selector = GetSelector(context);
                var timeout = GetTimeout(context);

                var item = Item.Get(context);
                if (item == null) throw new ArgumentException("Item kosong.");

                var byIndex = ByIndex != null && ByIndex.Get(context);

                if (IsDesktop(selector))
                {
                    if (byIndex)
                        throw new NotSupportedException(
                            "By Index belum didukung untuk target desktop; isi teks itemnya.");

                    DesktopActions.SelectItem(selector, item, timeout);
                    return;
                }

                SendWeb(context, "selectItem", selector, timeout, request =>
                {
                    request["item"] = item;
                    request["byIndex"] = byIndex;
                });
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}
