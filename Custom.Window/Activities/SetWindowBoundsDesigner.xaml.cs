namespace Custom.Window.Design
{
    /// <summary>
    /// Kartu canvas SetWindowBounds. Keempat nilai inline; penentuan jendela lewat Indicate atau Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class SetWindowBoundsDesigner
    {
        public SetWindowBoundsDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Set Window Bounds"; }
        }
    }
}
