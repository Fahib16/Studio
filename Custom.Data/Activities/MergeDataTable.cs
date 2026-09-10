using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Data
{
    /// <summary>
    /// Menyalin isi Source ke Destination.
    ///
    /// BERBEDA dari activity lain di project ini: Destination memang DIUBAH di
    /// tempat, mengikuti DataTable.Merge bawaan .NET. Kalau yang dibutuhkan
    /// tabel baru, salin dulu Destination lewat .Copy() di ekspresi.
    ///
    /// MissingSchemaAction menentukan apa yang terjadi pada kolom yang ada di
    /// Source tapi tidak ada di Destination.
    /// </summary>
    [Designer(typeof(Design.MergeDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Merge Data Table")]
    [Description("Menggabungkan isi satu DataTable ke DataTable lain.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.mergedatatable.png")]
    public sealed class MergeDataTable : CodeActivity
    {
        public MergeDataTable()
        {
            DisplayName = "Merge Data Table";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Source")]
        [Description("Tabel yang isinya disalin.")]
        public InArgument<DataTable> Source { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Destination")]
        [Description("Tabel tujuan. Isinya DITAMBAH di tempat.")]
        public InArgument<DataTable> Destination { get; set; }

        [Category("Options")]
        [DisplayName("Missing Schema Action")]
        [Description("Perlakuan untuk kolom yang hanya ada di Source: Add (default) menambahkannya, " +
                     "Ignore mengabaikannya, Error menggagalkan, AddWithKey ikut menyalin primary key.")]
        [DefaultValue(MissingSchemaAction.Add)]
        public MissingSchemaAction MissingSchemaAction { get; set; } = MissingSchemaAction.Add;

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [Description("Kalau true, kegagalan activity ini tidak menghentikan workflow (default false).")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null && ContinueOnError.Get(context);

            try
            {
                var source = Source.Get(context);
                var destination = Destination.Get(context);

                if (source == null) throw new ArgumentException("Source kosong.");
                if (destination == null) throw new ArgumentException("Destination kosong.");

                destination.Merge(source, false, MissingSchemaAction);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }
    }
}
