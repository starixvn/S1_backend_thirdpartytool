using System.Collections.Generic;
using Newtonsoft.Json;

namespace ThirdPartyTool.Core.Models
{
    public class ThirdPartyModel
    {
        public int SiteId { set; get; }
        public int TpId { set; get; }
        public string TpName { set; get; }
        public string Setting { set; get; }
        public Dictionary<string, string> TpSetting
            => JsonConvert.DeserializeObject<Dictionary<string, string>>(Setting);
    }
}