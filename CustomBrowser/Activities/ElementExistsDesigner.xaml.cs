namespace Custom.Browser.Design
{
    /// <summary>
    /// Kartu canvas ElementExists. Variabel hasil inline; Timeout dan TabId di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class ElementExistsDesigner
    {
        public ElementExistsDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Element Exists"; }
        }
    }
}
