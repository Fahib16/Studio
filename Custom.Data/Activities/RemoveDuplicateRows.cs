using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace Custom.Data
{
    /// <summary>
    /// Membuang baris kembar dari DataTable. Tabel sumber tidak diubah.
    ///
    /// Baris dianggap kembar kalau SELURUH nilai kolomnya sama, kecuali kalau
    /// Columns diisi — lalu hanya kolom itu yang dibandingkan. Perbandingan
    /// memakai nilai hasil konversi ke string dengan InvariantCulture supaya
    /// angka yang sama tidak dianggap berbeda hanya karena format lokal.
    ///
    /// Baris pertama yang muncul dipertahankan, sisanya dibuang — urutan asli
    /// tetap terjaga.
    /// </summary>
    [Designer(typeof(Design.RemoveDuplicateRowsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Remove Duplicate Rows")]
    [Description("Membuang baris kembar dari DataTable.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.removeduplicaterows.png")]
    public sealed class RemoveDuplicateRows : CodeActivity
    {
        public RemoveDuplicateRows()
        {
            DisplayName = "Remove Duplicate Rows";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Data Table")]
        [Description("Tabel sumber. Tidak diubah.")]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Options")]
        [DisplayName("Columns")]
        [Description("Opsional. Kolom yang dibandingkan. Kosong berarti semua kolom.")]
        public InArgument<string[]> Columns { get; set; }

        [Category("Output")]
        [DisplayName("Result")]
        public OutArgument<DataTable> Result { get; set; }

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
                var source = DataTable.Get(context);
                if (source == null) throw new ArgumentException("Data Table kosong.");

                var names = Columns != null ? Columns.Get(context) : null;
                names = names != null ? names.Where(n => !string.IsNullOrWhiteSpace(n)).ToArray() : null;

                if (names != null && names.Length > 0)
                {
                    foreach (var n in names)
                        if (!source.Columns.Contains(n))
                            throw new ArgumentException("Kolom tidak ada di tabel: " + n);
                }
                else
                {
                    names = source.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
                }

                var result = source.Clone();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (DataRow row in source.Rows)
                {
                    var key = string.Join("\u001f", names.Select(n => Stringify(row[n])));
                    if (!seen.Add(key)) continue;
                    result.ImportRow(row);
                }

                result.TableName = source.TableName;
                if (Result != null) Result.Set(context, result);
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static string Stringify(object value)
        {
            if (value == null || value == DBNull.Value) return "\u0000null";
            var convertible = value as IConvertible;
            return convertible != null
                ? convertible.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString();
        }
    }
}
