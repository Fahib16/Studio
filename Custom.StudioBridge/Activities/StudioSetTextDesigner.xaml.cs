namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kartu canvas StudioSetText. Hanya Text yang inline — satu-satunya nilai yang selalu diisi ulang tiap pemakaian.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class StudioSetTextDesigner
    {
        public StudioSetTextDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Type Into"; }
        }
    }
}
