namespace Custom.Excel.Design
{
    /// <summary>
    /// Kartu canvas ReadRange. Workbook Path (dengan tombol pemilih), Sheet, Range, dan variabel hasil inline. Kosongkan Workbook Path kalau activity ini berada di dalam Excel Application Scope.
    ///
    /// Tombol pemilih berkas/folder diwarisi dari Custom.Shared.PathDesignerBase.
    /// </summary>
    public partial class ReadRangeDesigner
    {
        public ReadRangeDesigner()
        {
            InitializeComponent();
        }
    }
}
