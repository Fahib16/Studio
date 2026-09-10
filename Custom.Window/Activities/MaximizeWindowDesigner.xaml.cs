namespace Custom.Window.Design
{
    /// <summary>
    /// Kartu canvas MaximizeWindow. Tunjuk jendelanya lewat Indicate, atau isi Window Title di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class MaximizeWindowDesigner
    {
        public MaximizeWindowDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Maximize Window"; }
        }
    }
}
