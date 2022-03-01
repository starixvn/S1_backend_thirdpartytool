using Newtonsoft.Json;

namespace ThirdPartyTool.Core.Models.Saba
{
    internal class CheckFundTransferResp : SabaBaseResp
    {
        public CheckFundTransferRespData Data { get; set; }
    }

    public class CheckFundTransferRespData
    {
        [JsonProperty("trans_id")]
        public string TransId { get; set; }

        [JsonProperty("transfer_date")]
        public string TransferDate { get; set; }

        [JsonProperty("vender_member_id")]
        public string VenderMemberId { get; set; }

        [JsonProperty("before_amount")]
        public decimal BeforeAmount { get; set; }

        [JsonProperty("after_amount")]
        public decimal AfterAmount { get; set; }

        [JsonProperty("Amount")]
        public decimal Amount { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}