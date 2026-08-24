namespace Custom.Browser.Design
{
    /// <summary>
    /// Code-behind minimal — tidak ada tombol Indicate/Highlight (tidak
    /// relevan, OpenBrowser bukan activity yang mencari elemen). Drop-zone
    /// Body ditangani otomatis lewat WorkflowItemPresenter di XAML.
    /// Checkbox ContinueOnError TIDAK ditaruh di canvas (sesuai keputusan
    /// terakhir) — solusinya ada di Properties panel, lihat catatan di
    /// OpenBrowser.cs.
    /// </summary>
    public partial class OpenBrowserDesigner
    {
        public OpenBrowserDesigner()
        {
            InitializeComponent();
        }
    }
}
