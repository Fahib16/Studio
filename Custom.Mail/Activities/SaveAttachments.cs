using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using MimeKit;

namespace Custom.Mail
{
    /// <summary>
    /// Menyimpan lampiran sebuah email ke folder.
    ///
    /// Tidak menghubungi server sama sekali: bekerja pada MailItem yang sudah
    /// diambil Get Mail. Karena itu activity ini tidak mewarisi
    /// MailServerActivity — tidak ada Host/Username yang benar-benar dipakai,
    /// dan menampilkannya hanya akan membuat orang mengira harus diisi.
    ///
    /// Nama berkas yang bentrok TIDAK ditimpa secara diam-diam: berkas kedua
    /// diberi akhiran (1), (2), dst. Lampiran email sering bernama sama
    /// ("invoice.pdf") padahal isinya berbeda.
    /// </summary>
    [Designer(typeof(Design.SaveAttachmentsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Save Attachments")]
    [Description("Menyimpan lampiran email ke sebuah folder.")]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.saveattachments.png")]
    public sealed class SaveAttachments : CodeActivity
    {
        public SaveAttachments()
        {
            DisplayName = "Save Attachments";
        }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Message")]
        [Description("Email sumber, dari output Get Mail.")]
        public InArgument<MailItem> Message { get; set; }

        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Folder")]
        [Description("Folder tujuan. Dibuat otomatis kalau belum ada.")]
        public InArgument<string> Folder { get; set; }

        [Category("Options")]
        [DisplayName("Filter")]
        [Description("Opsional. Pola nama berkas, mis. *.pdf. Kosong berarti semua lampiran.")]
        public InArgument<string> Filter { get; set; }

        [Category("Options")]
        [DisplayName("Overwrite")]
        [Description("Kalau true, berkas yang sudah ada ditimpa. Default false: diberi akhiran (1), (2), dst.")]
        public InArgument<bool> Overwrite { get; set; }

        [Category("Output")]
        [DisplayName("Files")]
        [Description("Path lengkap setiap berkas yang tersimpan.")]
        public OutArgument<string[]> Files { get; set; }

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
                var item = Message.Get(context);
                if (item == null || item.Message == null) throw new ArgumentException("Message kosong.");

                var folder = Folder.Get(context);
                if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Folder kosong.");
                Directory.CreateDirectory(folder);

                var filter = Filter != null ? Filter.Get(context) : null;
                var overwrite = Overwrite != null && Overwrite.Get(context);

                var saved = new List<string>();

                foreach (var attachment in item.Message.Attachments)
                {
                    var name = GetFileName(attachment);
                    if (string.IsNullOrWhiteSpace(name)) name = "attachment";
                    if (!MatchesFilter(name, filter)) continue;

                    var path = Path.Combine(folder, SafeFileName(name));
                    if (!overwrite) path = Unique(path);

                    using (var stream = File.Create(path))
                    {
                        var part = attachment as MimePart;
                        if (part != null) part.Content.DecodeTo(stream);
                        else ((MessagePart)attachment).Message.WriteTo(stream);
                    }

                    saved.Add(path);
                }

                if (Files != null) Files.Set(context, saved.ToArray());
            }
            catch (Exception) when (continueOnError)
            {
            }
        }

        private static string GetFileName(MimeEntity entity)
        {
            var part = entity as MimePart;
            if (part != null) return part.FileName;

            var message = entity as MessagePart;
            if (message != null && message.Message != null)
            {
                var subject = message.Message.Subject;
                return (string.IsNullOrWhiteSpace(subject) ? "pesan" : subject) + ".eml";
            }

            return null;
        }

        /// <summary>Pola sederhana ala nama berkas: * banyak karakter, ? satu karakter.</summary>
        private static bool MatchesFilter(string name, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;

            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter)
                                    .Replace("\\*", ".*").Replace("\\?", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(
                name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Buang karakter yang tidak boleh ada di nama berkas Windows.
        /// Nama lampiran datang dari luar dan tidak bisa dipercaya bersih —
        /// termasuk bisa memuat pemisah path.
        /// </summary>
        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return cleaned.Length == 0 ? "attachment" : cleaned;
        }

        private static string Unique(string path)
        {
            if (!File.Exists(path)) return path;

            var folder = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            var i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(folder, name + " (" + i + ")" + ext);
                i++;
            } while (File.Exists(candidate));

            return candidate;
        }
    }
}
