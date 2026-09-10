namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Kartu canvas StudioHighlight. Tidak ada isian inline; elemen dipilih lewat Indicate.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class StudioHighlightDesigner
    {
        public StudioHighlightDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Highlight"; }
        }
    }
}
