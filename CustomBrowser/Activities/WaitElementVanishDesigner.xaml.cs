namespace Custom.Browser.Design
{
    /// <summary>
    /// Kartu canvas WaitElementVanish. Timeout inline karena selalu disesuaikan per pemakaian.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class WaitElementVanishDesigner
    {
        public WaitElementVanishDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Wait Element Vanish"; }
        }
    }
}
