namespace Custom.Browser.Design
{
    /// <summary>
    /// Kartu canvas CheckElement. Tidak ada isian teks; Action dipilih di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class CheckElementDesigner
    {
        public CheckElementDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Check / Uncheck"; }
        }
    }
}
