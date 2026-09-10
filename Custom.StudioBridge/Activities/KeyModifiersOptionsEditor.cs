using OpenRPA.Interfaces;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Daftar pilihan tombol penahan di Properties panel, dipakai bersama
    /// Click dan Type Into.
    ///
    /// Dulu ada dua salinan kelas ini di project OpenRPA (di ClickElement.cs
    /// dan di UiPathStyleClick.cs); di sini cukup satu.
    /// </summary>
    internal class KeyModifiersOptionsEditor : CustomSelectEditor
    {
        public override System.Data.DataTable options
        {
            get
            {
                var lst = new System.Data.DataTable();
                lst.Columns.Add("ID", typeof(string));
                lst.Columns.Add("TEXT", typeof(string));
                lst.Rows.Add("{LCONTROL}", "Left Control");
                lst.Rows.Add("{LMENU}", "Left Menu / Alt");
                lst.Rows.Add("{LSHIFT}", "Left Shift");
                return lst;
            }
        }
    }
}
