namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kartu canvas StudioClick. Tidak ada isian inline: elemen dipilih lewat Indicate, sisanya di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class StudioClickDesigner
    {
        public StudioClickDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Click"; }
        }
    }
}
