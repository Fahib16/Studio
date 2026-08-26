using System;
using System.Activities;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Representasi satu tab, dipakai sebagai Output StudioListTabs. Kelas
    /// POCO sederhana supaya hasilnya enak dipakai lewat For Each di
    /// workflow, tidak perlu parsing JSON manual.
    /// </summary>
    [Serializable]
    public class StudioTabInfo
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public bool Active { get; set; }
        public int WindowId { get; set; }

        public override string ToString() => $"[{Id}] {Title} ({Url})";
    }

    /// <summary>
    /// Activity: Studio List Tabs. Ambil daftar semua tab yang sedang
    /// terbuka lewat extension kita sendiri.
    /// </summary>
    [Designer(typeof(Design.StudioListTabsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class StudioListTabs : CodeActivity
    {
        [Category("Common")]
        [DisplayName("Continue On Error")]
        public InArgument<bool> ContinueOnError { get; set; }

        [Category("Output")]
        [DisplayName("Tabs")]
        public OutArgument<StudioTabInfo[]> Tabs { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var result = StudioPipeClient.SendCommand("listTabs");
                var array = result as JArray ?? new JArray();

                var tabs = array.Select(t => new StudioTabInfo
                {
                    Id = t["id"]?.Value<int>() ?? 0,
                    Url = t["url"]?.Value<string>(),
                    Title = t["title"]?.Value<string>(),
                    Active = t["active"]?.Value<bool>() ?? false,
                    WindowId = t["windowId"]?.Value<int>() ?? 0
                }).ToArray();

                if (Tabs != null) Tabs.Set(context, tabs);
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }
    }
}
