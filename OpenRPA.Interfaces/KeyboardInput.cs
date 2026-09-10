using System;
using System.Collections.Generic;

namespace OpenRPA.Interfaces
{
    /// <summary>
    /// Pengetikan lewat keyboard tingkat rendah: mengurai teks bertoken
    /// ({ENTER}, {CTRL down}, ...) menjadi penekanan tombol FlaUI.
    ///
    /// DIPINDAHKAN KE SINI dari OpenRPA.Activities.TypeText (project OpenRPA)
    /// supaya bisa dipakai juga oleh activity di project Custom.* — project
    /// itu TIDAK BOLEH mereferensi project OpenRPA (OpenRPA sudah mereferensi
    /// mereka, jadi itu akan jadi referensi melingkar). Yang dipindahkan hanya
    /// dua method statisnya, isinya apa adanya; TypeText tetap punya method
    /// dengan nama yang sama dan meneruskan ke sini, jadi tidak ada satu pun
    /// pemanggil lama yang perlu diubah.
    ///
    /// Satu implementasi, dua pemakai — bukan salinan.
    /// </summary>
    public static class KeyboardInput
    {
        public static void TypeString(string text, TimeSpan clickdelay)
        {
            var disposes = new List<IDisposable>();
            var enddisposes = new List<IDisposable>();
            if (string.IsNullOrEmpty(text)) return;

            if (clickdelay.TotalMilliseconds < 10) clickdelay = TimeSpan.FromMilliseconds(10);
            var linedelay = TimeSpan.FromMilliseconds(5);

            for (var i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    int indexEnd = text.IndexOf('}', i + 1);
                    int indexNextStart = text.IndexOf('{', indexEnd + 1);
                    int indexNextEnd = text.IndexOf('}', indexEnd + 1);
                    if (indexNextStart > indexNextEnd || (indexNextStart == -1 && indexNextEnd > -1)) indexEnd = indexNextEnd;
                    var sub = text.Substring(i + 1, (indexEnd - i) - 1);
                    i = indexEnd;
                    foreach (var k in sub.Split(','))
                    {
                        string key = k.Trim();
                        bool down = false;
                        bool up = false;
                        if (key.EndsWith("down"))
                        {
                            down = true;
                            key = key.Replace(" down", "");
                        }
                        else if (key.EndsWith("up"))
                        {
                            up = true;
                            key = key.Replace(" up", "");
                        }
                        FlaUI.Core.WindowsAPI.VirtualKeyShort vk;
                        Enum.TryParse<FlaUI.Core.WindowsAPI.VirtualKeyShort>(key, true, out vk);
                        if (down)
                        {
                            if (vk > 0)
                            {
                                enddisposes.Add(FlaUI.Core.Input.Keyboard.Pressing(vk));
                            }
                            else
                            {
                                FlaUI.Core.Input.Keyboard.Type(key);
                            }
                        }
                        else if (up)
                        {
                            if (vk > 0)
                            {
                                FlaUI.Core.Input.Keyboard.Release(vk);
                            }
                            else
                            {
                                FlaUI.Core.Input.Keyboard.Type(key);
                            }
                        }
                        else
                        {
                            if (vk > 0)
                            {
                                switch (vk)
                                {
                                    case FlaUI.Core.WindowsAPI.VirtualKeyShort.LEFT: System.Windows.Forms.SendKeys.SendWait("+({LEFT})"); break;
                                    case FlaUI.Core.WindowsAPI.VirtualKeyShort.RIGHT: System.Windows.Forms.SendKeys.SendWait("+({RIGHT})"); break;
                                    case FlaUI.Core.WindowsAPI.VirtualKeyShort.UP: System.Windows.Forms.SendKeys.SendWait("+({UP})"); break;
                                    case FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN: System.Windows.Forms.SendKeys.SendWait("+({DOWN})"); break;
                                    case FlaUI.Core.WindowsAPI.VirtualKeyShort.END: System.Windows.Forms.SendKeys.SendWait("+({END})"); break;
                                    case FlaUI.Core.WindowsAPI.VirtualKeyShort.HOME: System.Windows.Forms.SendKeys.SendWait("+({HOME})"); break;
                                    default:
                                        FlaUI.Core.Input.Keyboard.Press(vk);
                                        break;
                                }
                            }
                            else
                            {
                                FlaUI.Core.Input.Keyboard.Type(key);
                            }
                        }
                        System.Threading.Thread.Sleep(clickdelay);
                    }
                    disposes.ForEach(x => { x.Dispose(); });
                }
                else
                {
                    FlaUI.Core.Input.Keyboard.Type(c);
                    System.Threading.Thread.Sleep(clickdelay);
                }
            }
            enddisposes.ForEach(x => { x.Dispose(); });
        }

        public static List<FlaUI.Core.WindowsAPI.VirtualKeyShort> GetKeys(string text)
        {
            var result = new List<FlaUI.Core.WindowsAPI.VirtualKeyShort>();
            if (string.IsNullOrEmpty(text)) return result;
            for (var i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    int indexEnd = text.IndexOf('}', i + 1);
                    int indexNextStart = text.IndexOf('{', indexEnd + 1);
                    int indexNextEnd = text.IndexOf('}', indexEnd + 1);
                    if (indexNextStart > indexNextEnd || (indexNextStart == -1 && indexNextEnd > -1)) indexEnd = indexNextEnd;
                    var sub = text.Substring(i + 1, (indexEnd - i) - 1);
                    i = indexEnd;
                    foreach (var k in sub.Split(','))
                    {
                        string key = k.Trim();
                        if (key.EndsWith("down"))
                        {
                            key = key.Replace(" down", "");
                        }
                        else if (key.EndsWith("up"))
                        {
                            key = key.Replace(" up", "");
                        }
                        FlaUI.Core.WindowsAPI.VirtualKeyShort vk;
                        Enum.TryParse<FlaUI.Core.WindowsAPI.VirtualKeyShort>(key, true, out vk);
                        result.Add(vk);
                    }
                }
            }
            return result;
        }
    }
}
