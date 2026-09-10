using System;
using System.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Menyusun nama activity dari selector, meniru UiPath yang menamai
    /// activity sesuai elemen targetnya ("Type Into 'First Name'").
    ///
    /// Nama bawaan seperti "StudioSetText" tidak membedakan apa pun begitu ada
    /// lima activity sejenis di satu workflow, dan itu justru kondisi paling
    /// umum. Nama yang menyebut elemennya membuat canvas bisa dibaca sekilas.
    /// </summary>
    public static class SelectorLabel
    {
        /// <summary>
        /// Urutan atribut yang paling menggambarkan elemen bagi MANUSIA —
        /// bukan yang paling stabil bagi mesin. Teks yang terlihat di layar
        /// selalu lebih berarti daripada automationid, walau untuk pencocokan
        /// justru sebaliknya.
        /// </summary>
        private static readonly string[] Preferred =
        {
            "innertext", "name", "aria-label", "title", "id", "automationid", "placeholder"
        };

        /// <summary>
        /// Nama lengkap activity, mis. Set Text 'First Name'.
        /// Mengembalikan prefix apa adanya kalau tidak ada yang bisa dipakai.
        /// </summary>
        public static string Build(string prefix, string selector)
        {
            var label = Describe(selector);
            return string.IsNullOrEmpty(label) ? prefix : prefix + " '" + label + "'";
        }

        /// <summary>Ambil satu penggambaran singkat elemen dari selector.</summary>
        public static string Describe(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector)) return null;

            try
            {
                var nodes = SelectorDocument.Parse(selector);
                if (nodes.Count == 0) return null;

                // Ditelusuri dari tingkat TERDALAM ke atas: tingkat terakhir
                // adalah elemen yang benar-benar disasar, dan itu yang paling
                // menggambarkan maksud activity.
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    foreach (var key in Preferred)
                    {
                        var attr = nodes[i].Attributes.FirstOrDefault(a =>
                            string.Equals(a.Name, key, StringComparison.OrdinalIgnoreCase));

                        if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
                            return Shorten(attr.Value);
                    }
                }

                // Tidak ada atribut deskriptif: pakai tag elemen terdalam.
                var tag = nodes[nodes.Count - 1].Attributes.FirstOrDefault(a =>
                    string.Equals(a.Name, "tag", StringComparison.OrdinalIgnoreCase));

                return tag != null ? Shorten(tag.Value) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Pendekkan supaya kartu di canvas tidak melebar. Baris baru diganti
        /// spasi karena innertext bisa memuat beberapa baris.
        /// </summary>
        private static string Shorten(string value)
        {
            var v = value.Replace("\r", " ").Replace("\n", " ").Trim();
            while (v.Contains("  ")) v = v.Replace("  ", " ");

            const int max = 30;
            return v.Length <= max ? v : v.Substring(0, max - 1).TrimEnd() + "…";
        }
    }
}
