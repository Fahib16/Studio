using System;
using System.Linq;
using Open3270;
using Open3270.TN3270;

namespace Custom.Terminal
{
    /// <summary>
    /// Aturan koordinat layar terminal, dipakai bersama semua activity di
    /// project ini.
    ///
    /// PENTING — urutan parameter Open3270 gampang terbalik:
    /// TNEmulator.GetText(x, y, length), SetCursor(x, y), dan
    /// WaitForText(x, y, ...) semuanya memakai **x = KOLOM, y = BARIS**, dan
    /// keduanya **berbasis 0**. Ini bukan tebakan: di
    /// Open3270/src/Open3270Library/Engine/TnXMLScreen.cs, GetText(x, y, len)
    /// menghitung offset sebagai (x + y * lebarLayar), dan di Controller.cs
    /// MoveCursor menghitung ((y * columnCount) + x).
    ///
    /// Di sisi activity, Row dan Column dibuat **berbasis 1** — itu yang
    /// tertera di layar 3270 dan yang orang baca saat menyusun workflow — dan
    /// 0 berarti "tidak diisi". Konversinya hanya di sini, supaya tidak ada
    /// activity yang punya versi aturannya sendiri.
    /// </summary>
    internal static class TerminalCoordinates
    {
        /// <summary>Baris 1-based menjadi y 0-based. Baris 0 (tidak diisi) menjadi 0.</summary>
        public static int ToY(int row)
        {
            return row > 0 ? row - 1 : 0;
        }

        /// <summary>Kolom 1-based menjadi x 0-based. Kolom 0 (tidak diisi) menjadi 0.</summary>
        public static int ToX(int column)
        {
            return column > 0 ? column - 1 : 0;
        }

        public static bool HasPosition(int row, int column)
        {
            return row > 0 || column > 0;
        }

        public static IXMLScreen Screen(TNEmulator session)
        {
            if (session == null) throw new ArgumentException("Session tidak boleh kosong");

            // Refresh dulu supaya isi layarnya yang terbaru; tanpa ini yang
            // terbaca bisa layar sebelum host mengirim pembaruan terakhir.
            session.Refresh();

            var screen = session.CurrentScreenXML;
            if (screen == null) throw new InvalidOperationException("Layar terminal belum tersedia.");
            return screen;
        }

        /// <summary>
        /// Daftar field di layar saat ini. Field adalah bagian layar yang
        /// dikelola host (label dan kotak isian); inilah yang membuat Get/Set
        /// Field lebih tahan banting daripada koordinat mentah — posisi field
        /// boleh bergeser asal urutannya tetap.
        /// </summary>
        public static XMLScreenField[] Fields(TNEmulator session)
        {
            var fields = Screen(session).Fields;
            return fields ?? new XMLScreenField[0];
        }

        /// <summary>
        /// Cari indeks field yang MEMUAT posisi (row, column) 1-based.
        /// Mengembalikan -1 kalau tidak ada.
        /// </summary>
        public static int FieldIndexAt(XMLScreenField[] fields, int row, int column)
        {
            var y = ToY(row);
            var x = ToX(column);

            for (int i = 0; i < fields.Length; i++)
            {
                var loc = fields[i].Location;
                if (loc == null) continue;
                if (loc.top != y) continue;
                if (x < loc.left || x >= loc.left + loc.length) continue;
                return i;
            }

            return -1;
        }

        /// <summary>
        /// Cari indeks field pertama yang teksnya memuat label tertentu, lalu
        /// kembalikan indeks field BERIKUTNYA — pola paling umum di layar
        /// 3270: label di kiri, kotak isiannya tepat sesudahnya.
        /// </summary>
        public static int FieldIndexAfterLabel(XMLScreenField[] fields, string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return -1;

            for (int i = 0; i < fields.Length; i++)
            {
                var text = fields[i].Text;
                if (text == null) continue;
                if (text.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0) continue;

                return i + 1 < fields.Length ? i + 1 : -1;
            }

            return -1;
        }
    }
}
