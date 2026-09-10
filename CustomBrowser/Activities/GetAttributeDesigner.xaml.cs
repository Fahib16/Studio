namespace Custom.Browser.Design
{
    /// <summary>
    /// Kartu canvas GetAttribute. Nama atribut dan variabel hasil inline.
    ///
    /// Tombol Indicate/Edit selector (dan tombol berkas, kalau ada)
    /// diwarisi dari Custom.StudioBridge.Design.IndicateDesignerBase.
    /// </summary>
    public partial class GetAttributeDesigner
    {
        public GetAttributeDesigner()
        {
            InitializeComponent();
        }

        protected override string ActivityLabel
        {
            get { return "Get Attribute"; }
        }
    }
}
