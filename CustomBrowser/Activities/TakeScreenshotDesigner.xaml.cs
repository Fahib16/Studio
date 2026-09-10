namespace Custom.Browser.Design
{
    /// <summary>
    /// Kartu canvas TakeScreenshot. Path berkas (dengan tombol pemilih) dan variabel gambar inline; Selector opsional.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class TakeScreenshotDesigner
    {
        public TakeScreenshotDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Take Screenshot"; }
        }
    }
}
