namespace Custom.Data.Design
{
    /// <summary>
    /// Kartu canvas ExtractDataTable. Variabel hasil inline; Html, Table Index, dan Add Headers di Properties panel.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class ExtractDataTableDesigner
    {
        public ExtractDataTableDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Extract Data Table"; }
        }
    }
}
