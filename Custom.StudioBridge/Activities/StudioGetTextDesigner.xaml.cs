namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kartu canvas StudioGetText. Variabel hasil inline (padanan Save to UiPath).
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class StudioGetTextDesigner
    {
        public StudioGetTextDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Get Text"; }
        }
    }
}
