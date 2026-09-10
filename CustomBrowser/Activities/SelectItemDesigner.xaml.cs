namespace Custom.Browser.Design
{
    /// <summary>
    /// Kartu canvas SelectItem. Item inline; By Index dan Timeout di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class SelectItemDesigner
    {
        public SelectItemDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Select Item"; }
        }
    }
}
