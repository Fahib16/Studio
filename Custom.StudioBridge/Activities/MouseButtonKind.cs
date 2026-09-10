using System.ComponentModel;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Tombol mouse yang dipakai activity Studio Click.
    ///
    /// Enum sendiri, bukan OpenRPA.Input.MouseButton, karena nilainya harus
    /// bisa dikirim ke DUA jalur yang berbeda: FlaUI di sisi desktop dan
    /// nomor tombol DOM (0/1/2) di sisi web. Menempel ke enum milik salah satu
    /// pihak akan membuat sisi lain menebak-nebak.
    /// </summary>
    public enum MouseButtonKind
    {
        Left,
        Right,
        Middle
    }

    internal static class MouseButtonKinds
    {
        /// <summary>Nomor tombol menurut DOM MouseEvent: 0 kiri, 1 tengah, 2 kanan.</summary>
        public static int ToDomButton(MouseButtonKind button)
        {
            switch (button)
            {
                case MouseButtonKind.Middle: return 1;
                case MouseButtonKind.Right: return 2;
                default: return 0;
            }
        }
    }
}
