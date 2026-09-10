namespace Custom.Window.Design
{
    /// <summary>
    /// Kartu canvas CloseWindow. Tunjuk jendelanya lewat Indicate; Wait For Close dan Timeout di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class CloseWindowDesigner
    {
        public CloseWindowDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Close Window"; }
        }
    }
}
