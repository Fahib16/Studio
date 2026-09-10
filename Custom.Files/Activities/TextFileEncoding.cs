using System;
using System.Text;

namespace Custom.Files
{
    /// <summary>
    /// Pilihan encoding untuk activity teks.
    ///
    /// Dibuat sebagai enum sendiri, bukan string nama encoding, supaya tidak
    /// ada kemungkinan salah ketik yang baru ketahuan saat runtime.
    /// </summary>
    public enum TextFileEncoding
    {
        /// <summary>
        /// Saat MEMBACA: UTF-8 dengan deteksi BOM (kalau ada BOM UTF-16, itu yang dipakai).
        /// Saat MENULIS: UTF-8 tanpa BOM. Ini perilaku bawaan File.ReadAllText/WriteAllText.
        /// </summary>
        Default,

        /// <summary>UTF-8 tanpa BOM.</summary>
        Utf8,

        /// <summary>UTF-8 dengan BOM (dibutuhkan sebagian aplikasi lama, mis. Excel saat impor CSV).</summary>
        Utf8WithBom,

        /// <summary>UTF-16 little endian.</summary>
        Unicode,

        ASCII,

        /// <summary>Windows-1252, encoding "ANSI" bawaan Windows di Eropa Barat.</summary>
        Windows1252
    }

    internal static class TextFileEncodings
    {
        /// <summary>
        /// Terjemahkan pilihan enum ke objek Encoding.
        /// Mengembalikan null untuk Default, karena Default berarti "pakai
        /// perilaku bawaan File.ReadAllText/WriteAllText" — yang tidak sama
        /// persis dengan Encoding.UTF8 mana pun (baca: deteksi BOM, tulis:
        /// tanpa BOM).
        /// </summary>
        public static Encoding Resolve(TextFileEncoding value)
        {
            switch (value)
            {
                case TextFileEncoding.Utf8: return new UTF8Encoding(false);
                case TextFileEncoding.Utf8WithBom: return new UTF8Encoding(true);
                case TextFileEncoding.Unicode: return Encoding.Unicode;
                case TextFileEncoding.ASCII: return Encoding.ASCII;
                case TextFileEncoding.Windows1252: return Encoding.GetEncoding(1252);
                default: return null;
            }
        }
    }
}
