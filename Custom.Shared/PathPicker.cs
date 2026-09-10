using System;
using System.IO;
using System.Linq;

namespace Custom.Shared
{
    /// <summary>
    /// Dialog pemilih berkas/folder untuk tombol browse di kartu activity.
    ///
    /// Dipakai bersama oleh semua project Custom.* yang punya properti path,
    /// supaya perilakunya satu: filter yang sama bentuknya, folder awal yang
    /// diambil dari isi kotak saat itu, dan hasil yang selalu ditulis sebagai
    /// string VB berkutip.
    /// </summary>
    public static class PathPicker
    {
        /// <summary>Filter bawaan kalau activity tidak menyebut jenis berkasnya.</summary>
        public const string AllFilesFilter = "Semua berkas (*.*)|*.*";

        /// <summary>
        /// Pilih satu berkas yang SUDAH ADA.
        /// Mengembalikan null kalau dialog dibatalkan.
        /// </summary>
        public static string OpenFile(string title, string filter, string current)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = string.IsNullOrEmpty(filter) ? AllFilesFilter : filter,
                CheckFileExists = true,
                Multiselect = false
            };

            Prime(dialog, current);
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Pilih beberapa berkas sekaligus. Mengembalikan null kalau dibatalkan.
        /// </summary>
        public static string[] OpenFiles(string title, string filter, string current)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = string.IsNullOrEmpty(filter) ? AllFilesFilter : filter,
                CheckFileExists = true,
                Multiselect = true
            };

            Prime(dialog, current);
            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        /// <summary>
        /// Tentukan berkas TUJUAN — boleh yang belum ada, karena namanya
        /// memang baru diketik user. Dipakai untuk properti keluaran seperti
        /// Zip Path, Path pada Write CSV, dan Path pada Take Screenshot.
        /// </summary>
        public static string SaveFile(string title, string filter, string current)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = string.IsNullOrEmpty(filter) ? AllFilesFilter : filter,
                OverwritePrompt = false   // activity-nya sendiri yang punya opsi Overwrite
            };

            Prime(dialog, current);
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Pilih folder.
        ///
        /// Memakai FolderBrowserDialog milik WinForms, bukan dialog WPF:
        /// .NET Framework 4.6.2 tidak punya pemilih folder di
        /// Microsoft.Win32, dan menulis pembungkus IFileDialog sendiri lewat
        /// COM jauh lebih banyak kode daripada nilai yang didapat.
        /// </summary>
        public static string Folder(string title, string current)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = true;

                var start = StartFolder(current);
                if (!string.IsNullOrEmpty(start)) dialog.SelectedPath = start;

                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                    ? dialog.SelectedPath
                    : null;
            }
        }

        /// <summary>
        /// Bungkus path menjadi string VB yang siap ditaruh di kotak ekspresi:
        /// C:\data\a.xlsx menjadi "C:\data\a.xlsx".
        ///
        /// Kutip di dalam path digandakan sesuai aturan VB. Backslash TIDAK
        /// perlu di-escape — string VB memang tidak mengenal escape backslash,
        /// dan itu justru yang membuat path Windows enak dibaca di sana.
        /// </summary>
        public static string ToVbLiteral(string path)
        {
            return "\"" + (path ?? "").Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Bungkus beberapa path menjadi array VB: {"a","b"}.
        /// </summary>
        public static string ToVbArrayLiteral(string[] paths)
        {
            if (paths == null || paths.Length == 0) return "New String() {}";
            return "{" + string.Join(", ", paths.Select(ToVbLiteral)) + "}";
        }

        /// <summary>
        /// Kebalikan ToVbLiteral: ambil path polos dari isi kotak ekspresi,
        /// supaya dialog bisa dibuka di folder yang sedang dipakai user.
        /// Ekspresi yang bukan sekadar string berkutip diabaikan (nilainya
        /// baru ada saat runtime).
        /// </summary>
        public static string FromVbLiteral(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            var text = expression.Trim();
            if (text.Length < 2 || text[0] != '"' || text[text.Length - 1] != '"') return null;

            return text.Substring(1, text.Length - 2).Replace("\"\"", "\"");
        }

        private static void Prime(Microsoft.Win32.FileDialog dialog, string current)
        {
            var path = FromVbLiteral(current) ?? current;
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) dialog.InitialDirectory = folder;

                var name = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(name) && !Directory.Exists(path)) dialog.FileName = name;
            }
            catch (Exception)
            {
                // Isi kotak belum berupa path yang sah (mis. masih ekspresi):
                // dialog dibuka di folder bawaan, bukan gagal.
            }
        }

        private static string StartFolder(string current)
        {
            var path = FromVbLiteral(current) ?? current;
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                if (Directory.Exists(path)) return path;
                var folder = Path.GetDirectoryName(path);
                return Directory.Exists(folder) ? folder : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
