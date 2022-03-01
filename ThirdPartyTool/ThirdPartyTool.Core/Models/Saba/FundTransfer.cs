using Newtonsoft.Json;

namespace ThirdPartyTool.Core.Models.Saba
{
    internal class FundTransferResp : SabaBaseResp
    {
        public FundTransferRespData Data { get; set; }
    }

    public class FundTransferRespData
    {
        [JsonProperty("trans_id")]
        public string TransId { get; set; }

        [JsonProperty("before_amount")]
        public decimal BeforeAmount { get; set; }

        [JsonProperty("after_amount")]
        public decimal AfterAmount { get; set; }

        [JsonProperty("bonus_before_amount")]
        public decimal BonusBeforeAmount { get; set; }

        [JsonProperty("bonus_after_amount")]
        public decimal BonusAfterAmount { get; set; }

        [JsonProperty("system_id")]
        public string SystemId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}