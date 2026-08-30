using System;
using System.Activities;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace Custom.StudioBridge
{
    /// <summary>
    /// Activity: Studio Open Tab. Buka tab baru DI DALAM Chrome yang SUDAH
    /// AKTIF (extension jalan). Kalau Chrome belum kebuka sama sekali,
    /// pakai "Studio Launch Chrome" dulu SEBELUM activity ini -- pemisahan
    /// ini sengaja (saran user) supaya activity ini tetap simpel, dan
    /// urusan "nyalain Chrome dari nol" (yang ada isu profile picker
    /// tersendiri) ditangani terpisah.
    ///
    /// Punya Body ("Do" drop-zone) SEKADAR VISUAL, sama seperti Open
    /// Browser (Custom.Browser) dulu -- activity anak tetap resolve
    /// sendiri lewat TabId opsional (default tab aktif).
    /// </summary>
    [Designer(typeof(Design.StudioOpenTabDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    public sealed class StudioOpenTab : NativeActivity
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

        [Browsable(false)]
        public ActivityAction<StudioTabInfo> Body { get; set; }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddArgument(new RuntimeArgument("Url", typeof(string), ArgumentDirection.In, true));
            metadata.AddArgument(new RuntimeArgument("Active", typeof(bool), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("ContinueOnError", typeof(bool), ArgumentDirection.In));
            metadata.AddArgument(new RuntimeArgument("TabId", typeof(int), ArgumentDirection.Out));

            if (Body != null)
                metadata.AddDelegate(Body);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var continueOnError = ContinueOnError != null ? ContinueOnError.Get(context) : false;

            try
            {
                var url = Url.Get(context);
                var active = Active != null ? Active.Get(context) : true;

                var request = new JObject { ["url"] = url, ["active"] = active };
                var result = StudioPipeClient.SendCommand("openTab", request);

                var tabId = result?["id"]?.Value<int>() ?? 0;
                if (TabId != null) context.SetValue(TabId, tabId);

                if (Body != null)
                {
                    var tabInfo = new StudioTabInfo
                    {
                        Id = tabId,
                        Url = result?["url"]?.Value<string>(),
                        Title = result?["title"]?.Value<string>()
                    };
                    context.ScheduleAction(Body, tabInfo, OnBodyCompleted, OnBodyFaulted);
                }
            }
            catch (Exception) when (continueOnError)
            {
                // Telan error kalau ContinueOnError = true
            }
        }

        private void OnBodyCompleted(NativeActivityContext context, ActivityInstance completedInstance)
        {
        }

        private void OnBodyFaulted(NativeActivityFaultContext faultContext, Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
        }
    }
}
