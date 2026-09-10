using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using MimeKit;

namespace Custom.Mail
{
    /// <summary>
    /// Satu email hasil Get Mail.
    ///
    /// Bukan MimeMessage mentah, karena MimeMessage TIDAK MEMBAWA identitas
    /// server-nya: tanpa UID dan nama folder, activity lanjutan seperti Move
    /// Mail To Folder tidak punya cara menunjuk pesan mana yang dimaksud.
    /// Pesan aslinya tetap disertakan di properti Message untuk keperluan yang
    /// tidak tercakup properti ringkas di sini.
    ///
    /// Properti dibuat read-only lewat konstruktor supaya isinya tidak
    /// berubah setelah diambil dari server — kalau berubah, Id-nya bisa tidak
    /// lagi cocok dengan pesan di server.
    /// </summary>
    public class MailItem
    {
        public MailItem(string id, string folder, MimeMessage message, bool seen)
        {
            Id = id;
            Folder = folder;
            Message = message;
            IsRead = seen;

            Subject = message != null ? (message.Subject ?? "") : "";
            From = message != null ? Join(message.From) : "";
            To = message != null ? Split(message.To) : new string[0];
            Cc = message != null ? Split(message.Cc) : new string[0];
            Date = message != null ? message.Date.LocalDateTime : DateTime.MinValue;
            TextBody = message != null ? (message.TextBody ?? "") : "";
            HtmlBody = message != null ? (message.HtmlBody ?? "") : "";
            AttachmentCount = message != null ? message.Attachments.Count() : 0;
        }

        /// <summary>UID pesan di folder IMAP, atau nomor urut untuk POP3.</summary>
        public string Id { get; private set; }

        /// <summary>Nama folder IMAP asal pesan; kosong untuk POP3.</summary>
        public string Folder { get; private set; }

        public string Subject { get; private set; }
        public string From { get; private set; }
        public string[] To { get; private set; }
        public string[] Cc { get; private set; }
        public DateTime Date { get; private set; }
        public string TextBody { get; private set; }
        public string HtmlBody { get; private set; }
        public int AttachmentCount { get; private set; }
        public bool HasAttachments { get { return AttachmentCount > 0; } }
        public bool IsRead { get; private set; }

        /// <summary>Pesan aslinya, untuk hal yang tidak tercakup properti di atas.</summary>
        public MimeMessage Message { get; private set; }

        public override string ToString()
        {
            return "[" + Date.ToString("yyyy-MM-dd HH:mm") + "] " + From + " - " + Subject;
        }

        private static string Join(InternetAddressList list)
        {
            return list == null ? "" : string.Join("; ", Split(list));
        }

        private static string[] Split(InternetAddressList list)
        {
            if (list == null) return new string[0];

            return list.Mailboxes.Select(m => m.Address)
                       .Concat(list.OfType<GroupAddress>().Select(g => g.Name))
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToArray();
        }
    }

    internal static class SecureStrings
    {
        /// <summary>
        /// Ubah SecureString menjadi string biasa TEPAT saat dibutuhkan
        /// MailKit, lalu buang salinannya dari memori tak terkelola.
        ///
        /// MailKit hanya menerima string biasa, jadi konversi ini tidak bisa
        /// dihindari; yang bisa dilakukan adalah membuat rentang hidupnya
        /// sependek mungkin — itulah sebabnya konversi dilakukan di sini,
        /// bukan disimpan di properti activity.
        /// </summary>
        public static string ToPlain(SecureString value)
        {
            if (value == null) return "";

            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(ptr) ?? "";
            }
            finally
            {
                if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }

        public static SecureString FromPlain(string value)
        {
            var secure = new SecureString();
            if (!string.IsNullOrEmpty(value))
                foreach (var c in value) secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }
    }
}
