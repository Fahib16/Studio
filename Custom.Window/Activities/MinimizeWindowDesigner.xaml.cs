namespace Custom.Window.Design
{
    /// <summary>
    /// Kartu canvas MinimizeWindow. Tunjuk jendelanya lewat Indicate, atau isi Window Title di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class MinimizeWindowDesigner
    {
        public MinimizeWindowDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Minimize Window"; }
        }
    }
}
