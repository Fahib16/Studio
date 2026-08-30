using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge.Design
{
    /// <summary>
    /// Orkestrasi "Indicate on screen" (elemen) dan "Pick tab" (Attach Tab),
    /// dua-duanya dipakai bareng oleh Designer masing-masing activity.
    /// </summary>
    public static class IndicateHelper
    {
        public class IndicateResult
        {
            public int TabId { get; set; }
            public string Selector { get; set; }
            public string TagName { get; set; }
            public string PreviewText { get; set; }

            /// <summary>
            /// BARU: screenshot area elemen (base64 PNG), sudah di-crop
            /// sisi extension. Bisa null kalau capture gagal -- itu TIDAK
            /// fatal, Indicate tetap dianggap sukses tanpa screenshot.
            /// </summary>
            public string ScreenshotBase64 { get; set; }
        }

        private static List<TabPickerItem> GetTabItems()
        {
            var tabsResult = StudioPipeClient.SendCommand("listTabs");
            var tabsArray = tabsResult as JArray ?? new JArray();

            return tabsArray.Select(t => new TabPickerItem
            {
                Id = t["id"]?.Value<int>() ?? 0,
                Url = t["url"]?.Value<string>(),
                Title = t["title"]?.Value<string>(),
                Display = $"{t["title"]?.Value<string>()} — {t["url"]?.Value<string>()}"
            }).ToList();
        }

        public static IndicateResult Run()
        {
            int tabId;

            try
            {
                var items = GetTabItems();

                if (items.Count == 0)
                {
                    MessageBox.Show("Tidak ada tab yang terbuka. Buka tab dulu (mis. lewat Studio Open Tab) sebelum Indicate.",
                        "Studio Bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                else if (items.Count == 1)
                {
                    tabId = items[0].Id;
                }
                else
                {
                    var picker = new TabPickerWindow(items);
                    if (picker.ShowDialog() != true) return null; // user batal pilih tab
                    tabId = picker.SelectedTab.Id;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal ambil daftar tab: {ex.Message}", "Studio Bridge",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            MessageBox.Show("Setelah klik OK, buka browser dan klik elemen yang mau ditarget. Tekan Escape kalau mau batal.",
                "Studio Bridge — Indicate on screen", MessageBoxButton.OK, MessageBoxImage.Information);

            try
            {
                var pickResult = StudioPipeClient.StartIndicate(tabId);
                if (pickResult == null) return null; // user Escape

                return new IndicateResult
                {
                    TabId = tabId,
                    Selector = pickResult["selector"]?.Value<string>(),
                    TagName = pickResult["tagName"]?.Value<string>(),
                    PreviewText = pickResult["text"]?.Value<string>(),
                    ScreenshotBase64 = pickResult["screenshotBase64"]?.Value<string>()
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Indicate gagal: {ex.Message}", "Studio Bridge",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static TabPickerItem PickTabForAttach()
        {
            try
            {
                var items = GetTabItems();

                if (items.Count == 0)
                {
                    MessageBox.Show("Tidak ada tab yang terbuka.", "Studio Bridge",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var picker = new TabPickerWindow(items);
                if (picker.ShowDialog() != true) return null;

                return picker.SelectedTab;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal ambil daftar tab: {ex.Message}", "Studio Bridge",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
