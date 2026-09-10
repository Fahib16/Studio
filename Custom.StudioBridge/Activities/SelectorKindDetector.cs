using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Custom.StudioBridge.Design
{
    public enum SelectorKind
    {
        /// <summary>Tidak dikenali atau kosong.</summary>
        Unknown,

        /// <summary>XML &lt;webctrl ... /&gt; — elemen di dalam halaman web.</summary>
        Web,

        /// <summary>XML &lt;wnd ... /&gt; / &lt;ctrl ... /&gt; — aplikasi desktop.</summary>
        Desktop,

        /// <summary>CSS polos, format Studio Bridge sebelum pindah ke XML.</summary>
        LegacyCss,

        /// <summary>
        /// JSON array bawaan OpenRPA, mis. [{"Selector":"Windows"}] atau
        /// [{"Selector":"NM"}] — dipakai activity lama seperti UiPathStyleClick
        /// dan TypeInto.
        /// </summary>
        LegacyOpenRpa
    }

    /// <summary>
    /// Menentukan jenis target dari BENTUK selector-nya, bukan dari properti
    /// terpisah yang harus diisi user.
    ///
    /// Inilah yang membuat satu activity bisa menangani web dan desktop tanpa
    /// pilihan Technology manual: bentuk selector sudah membawa informasinya
    /// sendiri, jadi tidak ada lagi kemungkinan properti dan isi selector
    /// saling bertentangan (kasus yang gampang terjadi kalau user mengganti
    /// selector tapi lupa mengganti Technology).
    /// </summary>
    public static class SelectorKindDetector
    {
        private static readonly Regex FirstNode =
            new Regex(@"<\s*([\w:-]+)", RegexOptions.Compiled);

        public static SelectorKind Detect(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector)) return SelectorKind.Unknown;

            var s = selector.TrimStart();

            if (s.StartsWith("["))
            {
                // Format lama OpenRPA. Jenisnya ada di field "Selector" pada
                // item pertama ("NM" = web, selain itu desktop), tapi untuk
                // keperluan routing cukup ditandai sebagai format lama —
                // penanganannya diserahkan ke jalur OpenRPA yang sudah ada.
                return SelectorKind.LegacyOpenRpa;
            }

            if (s.StartsWith("<"))
            {
                var m = FirstNode.Match(s);
                if (!m.Success) return SelectorKind.Unknown;

                switch (m.Groups[1].Value.ToLowerInvariant())
                {
                    case "webctrl":
                    case "html":
                        return SelectorKind.Web;
                    case "wnd":
                    case "ctrl":
                        return SelectorKind.Desktop;
                    default:
                        return SelectorKind.Unknown;
                }
            }

            return SelectorKind.LegacyCss;
        }

        /// <summary>
        /// Apakah selector ini dijalankan lewat Studio Bridge (extension +
        /// native host)? Web XML dan CSS lama sama-sama iya.
        /// </summary>
        public static bool IsBridgeSelector(string selector)
        {
            var kind = Detect(selector);
            return kind == SelectorKind.Web || kind == SelectorKind.LegacyCss;
        }
    }
}
