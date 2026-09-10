namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kartu canvas StudioHoverElement. Mengarahkan mouse ke elemen tanpa mengklik.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class StudioHoverElementDesigner
    {
        public StudioHoverElementDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Hover Element"; }
        }
    }
}
