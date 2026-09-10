using System;
using System.Activities;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    public enum StudioTabMatchMode
    {
        Contains,
        Exact,
        StartsWith
    }

    /// <summary>
    /// Activity: Studio Attach Tab. Cari tab yang SUDAH TERBUKA (berdasarkan
    /// Url/Title pattern), TIDAK PERNAH buka baru -- gagal (throw) kalau
    /// tidak ketemu. Ini pengganti "Attach Browser" versi NMHook lama.
    ///
    /// Implementasi cukup reuse "listTabs" yang sudah ada (tidak perlu action
    /// baru di extension) -- filter dilakukan di sisi C# ini.
    /// </summary>
    [Designer(typeof(Design.StudioAttachTabDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [System.Drawing.ToolboxBitmap(typeof(ResFinder), "Resources.attachtab.png")]
    [DisplayName("Attach Tab")]
    [Description("Memilih tab Chrome yang sudah terbuka untuk dipakai activity berikutnya.")]
    public class StudioAttachTab : CodeActivity
    {
        public StudioAttachTab()
        {
            DisplayName = "Attach Tab";
        }

        [Category("Input")]
        [DisplayName("Url")]
        [Description("Pattern URL tab yang mau di-attach. Isi salah satu (Url atau Title), boleh dua-duanya.")]
        public InArgument<string> UrlPattern { get; set; }

        [Category("Input")]
        [DisplayName("Title")]
        public InArgument<string> TitlePattern { get; set; }

        [Category("Options")]
        [DisplayName("Match Mode")]
        [DefaultValue(StudioTabMatchMode.Contains)]
        public StudioTabMatchMode MatchMode { get; set; } = StudioTabMatchMode.Contains;

        [Category("Common")]
        [DisplayName("Continue On Error")]
        [System.ComponentModel.Editor(typeof(Custom.Shared.ContinueOnErrorEditor), typeof(System.Activities.Presentation.PropertyEditing.PropertyValueEditor))]
        public InArgument<bool> ContinueOnError { get; set; }

        [Category("Output")]
        [DisplayName("TabId")]
        public OutArgument<int> TabId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var urlPattern = UrlPattern != null ? UrlPattern.Get(context) : null;
                var titlePattern = TitlePattern != null ? TitlePattern.Get(context) : null;

                if (string.IsNullOrEmpty(urlPattern) && string.IsNullOrEmpty(titlePattern))
                    throw new ArgumentException("Studio Attach Tab: isi minimal salah satu dari Url atau Title.");

                var result = StudioPipeClient.SendCommand("listTabs");
                var tabsArray = result as JArray ?? new JArray();

                var match = tabsArray.FirstOrDefault(t =>
                {
                    bool urlOk = string.IsNullOrEmpty(urlPattern) || MatchesPattern(t["url"]?.Value<string>(), urlPattern, MatchMode);
                    bool titleOk = string.IsNullOrEmpty(titlePattern) || MatchesPattern(t["title"]?.Value<string>(), titlePattern, MatchMode);
                    return urlOk && titleOk;
                });

                if (match == null)
                    throw new InvalidOperationException(
                        $"Studio Attach Tab: tab tidak ditemukan (Url: \"{urlPattern}\", Title: \"{titlePattern}\").");

                var tabId = match["id"]?.Value<int>() ?? 0;
                if (TabId != null) TabId.Set(context, tabId);
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }

        private static bool MatchesPattern(string value, string pattern, StudioTabMatchMode mode)
        {
            if (value == null) return false;
            switch (mode)
            {
                case StudioTabMatchMode.Exact:
                    return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
                case StudioTabMatchMode.StartsWith:
                    return value.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
                default:
                    return value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
