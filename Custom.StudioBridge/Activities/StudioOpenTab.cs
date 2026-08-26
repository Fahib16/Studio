using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Activity: Studio Open Tab. Buka tab baru lewat extension kita sendiri
    /// (bukan NMHook OpenRPA) -- pakai StudioPipeClient -> Named Pipe ->
    /// native host -> extension -> chrome.tabs.create.
    /// </summary>
    [Designer(typeof(Design.StudioOpenTabDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public class StudioOpenTab : CodeActivity
    {
        [Category("Input")]
        [RequiredArgument]
        [DisplayName("Url")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        [DisplayName("Active")]
        [Description("True (default): tab baru langsung jadi tab aktif. False: buka di background.")]
        public InArgument<bool> Active { get; set; } = true;

        [Category("Common")]
        [DisplayName("Continue On Error")]
        public InArgument<bool> ContinueOnError { get; set; }

        [Category("Output")]
        [DisplayName("TabId")]
        public OutArgument<int> TabId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var url = Url.Get(context);
                var active = Active != null ? Active.Get(context) : true;

                var result = StudioPipeClient.SendCommand("openTab", new JObject
                {
                    ["url"] = url,
                    ["active"] = active
                });

                var tabId = result?["id"]?.Value<int>() ?? 0;
                if (TabId != null) TabId.Set(context, tabId);
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }
    }
}
