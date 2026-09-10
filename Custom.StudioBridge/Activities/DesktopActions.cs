using System;
using System.Diagnostics;
using System.Threading;
using Custom.StudioBridge.Design;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.AutomationElements.Infrastructure;
using FlaUI.Core.Definitions;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Aksi runtime untuk elemen aplikasi desktop, padanan dari perintah yang
    /// dikirim ke extension untuk sisi web.
    ///
    /// Semua aksi mencari elemennya lewat WaitFor() dengan retry sampai
    /// timeout, bukan sekali coba: aplikasi desktop kerap butuh waktu
    /// menampilkan jendela atau memuat kontrol, dan gagal di percobaan
    /// pertama bukan berarti elemennya tidak ada.
    /// </summary>
    public static class DesktopActions
    {
        /// <summary>
        /// Cari elemen, ulangi sampai timeout. Melempar kalau tidak ketemu,
        /// supaya perilakunya sama dengan sisi web (yang juga melempar lewat
        /// pesan error dari extension).
        /// </summary>
        public static AutomationElement WaitFor(string selector, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();

            while (true)
            {
                var found = DesktopSelectorResolver.Resolve(selector, 1);
                if (found.Length > 0) return found[0];

                if (sw.Elapsed >= timeout) break;
                Thread.Sleep(200);
            }

            throw new Exception("Elemen desktop tidak ditemukan untuk selector: " + selector);
        }

        public static void Click(string selector, TimeSpan timeout, bool doubleClick = false)
        {
            Click(selector, timeout, doubleClick, MouseButtonKind.Left, null, null, null, false, false);
        }

        /// <summary>
        /// Klik elemen desktop dengan pilihan lengkap: tombol mouse, tombol
        /// penahan, offset dari sudut kiri-atas elemen, fokus lebih dulu, dan
        /// klik "virtual".
        ///
        /// Virtual click memakai pola Invoke UI Automation — kursor tidak
        /// bergerak sama sekali, jadi tidak mengganggu apa pun yang sedang
        /// dikerjakan orang di depan layar. Batasannya: hanya kontrol yang
        /// mengekspos Invoke/Toggle yang mendukungnya, dan offset jelas tidak
        /// berlaku di sana karena tidak ada titik yang diklik.
        /// </summary>
        public static void Click(string selector, TimeSpan timeout, bool doubleClick,
                                 MouseButtonKind button, int? offsetX, int? offsetY,
                                 string keyModifiers, bool focusFirst, bool virtualClick)
        {
            var el = WaitFor(selector, timeout);

            if (focusFirst)
            {
                try { el.Focus(); } catch (Exception) { }
            }

            var modifiers = OpenRPA.Interfaces.KeyboardInput.GetKeys(keyModifiers);
            var pressed = new System.Collections.Generic.List<IDisposable>();

            try
            {
                foreach (var vk in modifiers)
                {
                    if (vk > 0) pressed.Add(FlaUI.Core.Input.Keyboard.Pressing(vk));
                }

                if (virtualClick && TryInvoke(el)) return;

                if (offsetX.HasValue || offsetY.HasValue)
                {
                    var rect = el.BoundingRectangle;
                    var point = new System.Drawing.Point(
                        rect.X + (offsetX ?? rect.Width / 2),
                        rect.Y + (offsetY ?? rect.Height / 2));

                    if (doubleClick) FlaUI.Core.Input.Mouse.DoubleClick(point, ToFlaUi(button));
                    else FlaUI.Core.Input.Mouse.Click(point, ToFlaUi(button));
                    return;
                }

                try
                {
                    if (button == MouseButtonKind.Right) el.RightClick(true);
                    else if (button == MouseButtonKind.Middle)
                        FlaUI.Core.Input.Mouse.Click(el.GetClickablePoint(), FlaUI.Core.Input.MouseButton.Middle);
                    else if (doubleClick) el.DoubleClick();
                    else el.Click();
                }
                catch (Exception)
                {
                    // Sebagian kontrol tidak mendukung klik lewat UI Automation
                    // (mis. gambar kustom di aplikasi Electron seperti Postman).
                    // Pola Invoke sering tetap jalan di kasus itu.
                    if (!TryInvoke(el)) throw;
                }
            }
            finally
            {
                pressed.ForEach(x => x.Dispose());
            }
        }

        private static bool TryInvoke(AutomationElement el)
        {
            var invoke = el.Patterns.Invoke.PatternOrDefault;
            if (invoke != null) { invoke.Invoke(); return true; }

            var toggle = el.Patterns.Toggle.PatternOrDefault;
            if (toggle != null) { toggle.Toggle(); return true; }

            return false;
        }

        private static FlaUI.Core.Input.MouseButton ToFlaUi(MouseButtonKind button)
        {
            switch (button)
            {
                case MouseButtonKind.Right: return FlaUI.Core.Input.MouseButton.Right;
                case MouseButtonKind.Middle: return FlaUI.Core.Input.MouseButton.Middle;
                default: return FlaUI.Core.Input.MouseButton.Left;
            }
        }

        public static void SetText(string selector, string text, TimeSpan timeout, bool emptyFirst = true)
        {
            SetText(selector, text, timeout, emptyFirst, true, false, 10, null);
        }

        /// <summary>
        /// Isi teks ke elemen desktop, dengan pilihan lengkap.
        ///
        /// simulateKeystrokes = false (default) memakai Value pattern kalau
        /// tersedia: nilai langsung terisi, tidak terganggu fokus yang
        /// berpindah maupun tata letak keyboard. Beberapa aplikasi tidak
        /// menyadari perubahan yang tidak datang dari papan ketik; untuk itu
        /// nyalakan simulateKeystrokes, yang mengetik sungguhan dan juga
        /// menghormati token seperti {ENTER} serta tombol penahan.
        /// </summary>
        public static void SetText(string selector, string text, TimeSpan timeout, bool emptyFirst,
                                   bool clickBeforeTyping, bool simulateKeystrokes, int delayBetweenKeysMs,
                                   string keyModifiers)
        {
            var el = WaitFor(selector, timeout);

            try { el.Focus(); } catch (Exception) { }

            if (!simulateKeystrokes)
            {
                var value = el.Patterns.Value.PatternOrDefault;
                if (value != null && !value.IsReadOnly.ValueOrDefault)
                {
                    value.SetValue(text ?? "");
                    return;
                }
            }

            // Aplikasi berbasis web-view (Postman, VS Code, Slack) sering tidak
            // mengekspos Value pattern sama sekali. Untuk itu satu-satunya
            // jalan adalah benar-benar mengetik.
            if (clickBeforeTyping)
            {
                try { el.Click(); } catch (Exception) { }
            }

            if (emptyFirst)
            {
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
            }

            if (string.IsNullOrEmpty(text)) return;

            var modifiers = OpenRPA.Interfaces.KeyboardInput.GetKeys(keyModifiers);
            var pressed = new System.Collections.Generic.List<IDisposable>();

            try
            {
                foreach (var vk in modifiers)
                {
                    if (vk > 0) pressed.Add(FlaUI.Core.Input.Keyboard.Pressing(vk));
                }

                if (simulateKeystrokes)
                {
                    // KeyboardInput mengurai token {ENTER}, {TAB}, {CTRL down}
                    // dan seterusnya — implementasi yang SAMA dengan yang
                    // dipakai activity Type Text bawaan OpenRPA, bukan salinan.
                    OpenRPA.Interfaces.KeyboardInput.TypeString(
                        text, TimeSpan.FromMilliseconds(delayBetweenKeysMs <= 0 ? 10 : delayBetweenKeysMs));
                }
                else
                {
                    FlaUI.Core.Input.Keyboard.Type(text);
                }
            }
            finally
            {
                pressed.ForEach(x => x.Dispose());
            }
        }

        public static string GetText(string selector, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);

            var value = el.Patterns.Value.PatternOrDefault;
            if (value != null)
            {
                var v = value.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(v)) return v;
            }

            var text = el.Patterns.Text.PatternOrDefault;
            if (text != null)
            {
                try { return text.DocumentRange.GetText(int.MaxValue); } catch (Exception) { }
            }

            // Name adalah pilihan terakhir: untuk label dan tombol, di situlah
            // teksnya berada.
            string name;
            if (el.Properties.Name.TryGetValue(out name)) return name ?? "";
            return "";
        }

        public static void Highlight(string selector, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);
            DesktopSelectorResolver.Highlight(el);
        }

        /// <summary>
        /// Menaruh fokus keyboard pada elemen tanpa mengkliknya.
        ///
        /// Menggantikan activity bawaan "Focus Element" yang memakai selector
        /// lama. Dipakai sebelum mengirim tombol, atau untuk memicu validasi
        /// yang menunggu peristiwa focus/blur.
        /// </summary>
        public static void Focus(string selector, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);
            try
            {
                el.Focus();
            }
            catch (Exception)
            {
                // Sebagian kontrol tidak mendukung SetFocus lewat UI Automation.
                // Menjadikan jendelanya aktif sudah cukup untuk sebagian besar
                // kasus, dan lebih baik daripada gagal total.
                try { el.FocusNative(); } catch (Exception) { }
            }
        }

        /// <summary>
        /// Menggerakkan penunjuk mouse ke tengah elemen tanpa menekan tombol,
        /// supaya menu atau tooltip yang muncul saat disentuh ikut terbuka.
        ///
        /// Menggantikan activity bawaan "Move Element".
        /// </summary>
        public static void Hover(string selector, TimeSpan timeout, int? offsetX, int? offsetY)
        {
            var el = WaitFor(selector, timeout);
            var point = el.GetClickablePoint();

            if (offsetX.HasValue || offsetY.HasValue)
            {
                var bounds = el.Properties.BoundingRectangle.Value;
                point = new System.Drawing.Point(
                    bounds.Left + (offsetX ?? (int)(bounds.Width / 2)),
                    bounds.Top + (offsetY ?? (int)(bounds.Height / 2)));
            }

            FlaUI.Core.Input.Mouse.MoveTo(point);
        }

        /// <summary>
        /// Apakah elemen ada SEKARANG — tanpa menunggu dan tanpa melempar.
        /// Dipakai Element Exists, yang memang bertugas menjawab ada/tidak,
        /// bukan memastikan ada.
        /// </summary>
        public static bool Exists(string selector)
        {
            try
            {
                return DesktopSelectorResolver.Resolve(selector, 1).Length > 0;
            }
            catch (Exception)
            {
                // Selector yang tidak bisa diurai berarti tidak cocok dengan apa
                // pun; itu jawaban "tidak ada", bukan kegagalan activity.
                return false;
            }
        }

        /// <summary>
        /// Baca satu properti elemen desktop. Nama properti disamakan dengan
        /// yang dipakai di selector (name, automationid, classname, ...)
        /// supaya orang tidak perlu menghafal dua kosakata berbeda.
        /// </summary>
        public static string GetProperty(string selector, string propertyName, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);
            var key = (propertyName ?? "").Trim().ToLowerInvariant();

            switch (key)
            {
                case "name":
                    string name;
                    return el.Properties.Name.TryGetValue(out name) ? (name ?? "") : "";

                case "automationid":
                case "id":
                    string automationId;
                    return el.Properties.AutomationId.TryGetValue(out automationId) ? (automationId ?? "") : "";

                case "classname":
                case "class":
                    string className;
                    return el.Properties.ClassName.TryGetValue(out className) ? (className ?? "") : "";

                case "controltype":
                case "tag":
                    FlaUI.Core.Definitions.ControlType controlType;
                    return el.Properties.ControlType.TryGetValue(out controlType) ? controlType.ToString() : "";

                case "isenabled":
                case "enabled":
                    bool enabled;
                    return el.Properties.IsEnabled.TryGetValue(out enabled) ? (enabled ? "true" : "false") : "";

                case "isoffscreen":
                    bool offscreen;
                    return el.Properties.IsOffscreen.TryGetValue(out offscreen) ? (offscreen ? "true" : "false") : "";

                case "checked":
                case "ischecked":
                    var toggle = el.Patterns.Toggle.PatternOrDefault;
                    if (toggle == null) return "";
                    return toggle.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On ? "true" : "false";

                case "value":
                case "innertext":
                case "text":
                    return GetText(selector, timeout);

                default:
                    throw new ArgumentException(
                        "Properti desktop tidak dikenali: " + propertyName +
                        ". Yang dikenali: name, automationid, classname, controltype, isenabled, " +
                        "isoffscreen, checked, value.");
            }
        }

        /// <summary>
        /// Pilih item pada combo box / list desktop.
        ///
        /// Dicocokkan ke TEKS yang terlihat, sama seperti jalur web — itu yang
        /// dilihat orang saat menyusun workflow.
        /// </summary>
        public static void SelectItem(string selector, string item, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);

            var combo = el.AsComboBox();
            if (combo != null)
            {
                // CARA UTAMA: buka dropdown lalu KLIK itemnya.
                //
                // Ini bukan pilihan gaya. Pada combo box WinForms,
                // SelectionItem.Select() (yang dipakai ComboBox.Select milik
                // FlaUI) membuat UI Automation melaporkan item itu "terpilih"
                // PADAHAL isi kontrolnya belum berubah — sudah dibuktikan:
                // setelah Select(), SelectedItem berkata "Surabaya" tapi teks
                // combo box masih kosong; setelah Expand + Click, barulah
                // benar-benar terisi.
                try
                {
                    combo.Expand();
                    Thread.Sleep(200);

                    foreach (var candidate in combo.Items)
                    {
                        var text = (candidate.Text ?? "").Trim();
                        if (!string.Equals(text, (item ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) continue;

                        candidate.Click(false);
                        Thread.Sleep(200);
                        break;
                    }

                    try { combo.Collapse(); } catch (Exception) { }
                    Thread.Sleep(150);

                    if (ComboSelectionIs(combo, item)) return;
                }
                catch (Exception)
                {
                }

                // CADANGAN: Select bawaan FlaUI. Cukup untuk WPF/UWP, dan tetap
                // diperiksa hasilnya — tidak dipercaya begitu saja.
                try
                {
                    if (combo.Select(item) != null)
                    {
                        Thread.Sleep(200);
                        if (ComboSelectionIs(combo, item)) return;
                    }
                }
                catch (Exception)
                {
                }

                throw new Exception("Pilihan tidak ada (atau tidak bisa dipilih) di combo box: " + item);
            }

            var list = el.AsListBox();
            if (list != null)
            {
                var selected = list.Select(item);
                if (selected == null) throw new Exception("Pilihan tidak ada di list: " + item);
                return;
            }

            throw new Exception("Elemen desktop ini bukan combo box atau list, jadi tidak punya pilihan.");
        }

        /// <summary>
        /// Apakah pilihan combo box SEKARANG benar-benar item yang diminta.
        ///
        /// Yang diperiksa lebih dulu adalah Value — teks yang BENAR-BENAR
        /// tampil di kontrolnya. SelectedItem sengaja dipakai belakangan dan
        /// hanya kalau Value tidak tersedia, karena SelectedItem bisa berkata
        /// "terpilih" untuk item yang belum benar-benar masuk ke kontrolnya
        /// (lihat catatan di SelectItem).
        /// </summary>
        private static bool ComboSelectionIs(FlaUI.Core.AutomationElements.ComboBox combo, string item)
        {
            var wanted = (item ?? "").Trim();

            try
            {
                var value = combo.Value;
                if (!string.IsNullOrEmpty(value))
                    return string.Equals(value.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // Value tidak didukung combo box ini; jatuh ke SelectedItem.
            }

            try
            {
                var selected = combo.SelectedItem;
                return selected != null &&
                       string.Equals((selected.Text ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Centang / hapus centang / balik keadaan checkbox desktop.
        /// mode: check, uncheck, atau toggle.
        /// </summary>
        public static bool SetCheck(string selector, string mode, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);

            var toggle = el.Patterns.Toggle.PatternOrDefault;
            if (toggle == null) throw new Exception("Elemen desktop ini tidak bisa dicentang.");

            var isChecked = toggle.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On;
            var target = mode == "toggle" ? !isChecked : mode != "uncheck";

            if (isChecked != target) toggle.Toggle();

            return toggle.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On;
        }

        /// <summary>
        /// Potret satu elemen desktop, dikembalikan sebagai PNG base64.
        ///
        /// Dipotret dari LAYAR (bukan dari elemennya langsung), karena itulah
        /// satu-satunya cara mendapatkan tampilan sebenarnya termasuk gambar
        /// dan gaya yang digambar aplikasi sendiri.
        /// </summary>
        public static string Screenshot(string selector, TimeSpan timeout)
        {
            var el = WaitFor(selector, timeout);
            var rect = el.BoundingRectangle;

            if (rect.Width <= 0 || rect.Height <= 0)
                throw new Exception("Elemen desktop tidak punya ukuran yang bisa dipotret.");

            using (var bitmap = new System.Drawing.Bitmap(rect.Width, rect.Height))
            {
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));

                using (var stream = new System.IO.MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    return Convert.ToBase64String(stream.ToArray());
                }
            }
        }
    }
}
