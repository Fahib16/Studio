using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.Net
{
    public class CountMessage : SocketCommand
    {
        public CountMessage() : base()
        {
            msg.command = "count";
        }
        public JObject query { get; set; }
        public string queryas { get; set; }
        public string collectionname { get; set; }
        public int result { get; set; }

    }
}
