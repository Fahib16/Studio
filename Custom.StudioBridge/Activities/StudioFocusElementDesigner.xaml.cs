namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kartu canvas StudioFocusElement. Menaruh fokus keyboard tanpa mengklik.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class StudioFocusElementDesigner
    {
        public StudioFocusElementDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Focus Element"; }
        }
    }
}
