using System;
using System.Activities;
using System.ComponentModel;
using System.Data;

namespace Custom.Data
{
    /// <summary>
    /// Membuat DataTable kosong dengan kolom yang ditentukan sendiri.
    ///
    /// Definisi kolom disimpan sebagai SATU STRING, mis:
    ///     Nama:String; Umur:Int32; Aktif:Boolean
    /// Bentuk ini dipilih supaya definisinya ikut tersimpan apa adanya di
    /// berkas .xaml workflow dan bisa dibaca/diedit tanpa membuka dialog —
    /// termasuk lewat diff di git. Tombol "Edit columns" di kartu membuka
    /// editor kecil yang menulis kembali ke string yang sama.
    ///
    /// Tipe yang dikenali: String (default kalau tidak ditulis), Int32, Int64,
    /// Double, Decimal, Boolean, DateTime, Object.
    /// </summary>
    [Designer(typeof(Design.BuildDataTableDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Build Data Table")]
    [Description("Membuat DataTable kosong dengan kolom yang ditentukan.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.builddatatable.png")]
    public sealed class BuildDataTable : CodeActivity
    {
        public BuildDataTable()
        {
            DisplayName = "Build Data Table";
        }

        /// <summary>
        /// Plain property, bukan InArgument: susunan kolom ditentukan saat
        /// mendesain workflow dan tidak pernah dihitung saat runtime.
        /// </summary>
        [Category("Input")]
        [DisplayName("Columns")]
        [Description("Definisi kolom, mis. Nama:String; Umur:Int32. Tipe boleh dikosongkan (jadi String).")]
        public string Columns { get; set; }

        [Category("Input")]
        [DisplayName("Table Name")]
        [Description("Opsional. Nama tabel, ikut terbawa ke Write CSV dan Output Data Table.")]
        public string TableName { get; set; }

        [Category("Output")]
        [DisplayName("Data Table")]
        public OutArgument<DataTable> DataTable { get; set; }

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
                var table = Build(Columns, TableName);
                if (DataTable != null) DataTable.Set(context, table);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        /// <summary>
        /// Dipakai bersama oleh Execute dan editor kolom di designer, supaya
        /// yang divalidasi saat mendesain sama persis dengan yang dijalankan.
        /// </summary>
        public static DataTable Build(string definitions, string tableName)
        {
            var table = new DataTable(string.IsNullOrWhiteSpace(tableName) ? "DataTable" : tableName.Trim());

            if (string.IsNullOrWhiteSpace(definitions)) return table;

            var parts = definitions.Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in parts)
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;

                var colon = part.IndexOf(':');
                var name = (colon < 0 ? part : part.Substring(0, colon)).Trim();
                var typeName = colon < 0 ? "String" : part.Substring(colon + 1).Trim();

                if (name.Length == 0) throw new ArgumentException("Ada kolom tanpa nama di: " + raw);
                if (table.Columns.Contains(name)) throw new ArgumentException("Nama kolom kembar: " + name);

                table.Columns.Add(name, ResolveType(typeName));
            }

            return table;
        }

        public static Type ResolveType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return typeof(string);

            switch (name.Trim().ToLowerInvariant())
            {
                case "string": case "text": return typeof(string);
                case "int": case "int32": case "integer": return typeof(int);
                case "long": case "int64": return typeof(long);
                case "double": return typeof(double);
                case "decimal": return typeof(decimal);
                case "bool": case "boolean": return typeof(bool);
                case "date": case "datetime": return typeof(DateTime);
                case "object": return typeof(object);
                default:
                    throw new ArgumentException("Tipe kolom tidak dikenali: " + name +
                                                ". Yang dikenali: String, Int32, Int64, Double, Decimal, " +
                                                "Boolean, DateTime, Object.");
            }
        }

        /// <summary>Nama tipe yang ditulis kembali ke string definisi.</summary>
        public static string TypeName(Type type)
        {
            if (type == typeof(int)) return "Int32";
            if (type == typeof(long)) return "Int64";
            if (type == typeof(double)) return "Double";
            if (type == typeof(decimal)) return "Decimal";
            if (type == typeof(bool)) return "Boolean";
            if (type == typeof(DateTime)) return "DateTime";
            if (type == typeof(object)) return "Object";
            return "String";
        }
    }
}
