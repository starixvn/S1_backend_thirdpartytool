using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.Models
{
    public class RetryModel
    {
        public int TpId { get; set; }

        public ActionType Type { get; set; }

        public string Data { get; set; }
    }
}