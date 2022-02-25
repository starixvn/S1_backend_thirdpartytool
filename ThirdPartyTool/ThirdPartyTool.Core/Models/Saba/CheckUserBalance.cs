using Newtonsoft.Json;

namespace ThirdPartyTool.Core.Models.Saba
{
    public class CheckUserBalanceResp : SabaBaseResp
    {
        public CheckUserBalanceData Data { get; set; }
    }

    public class CheckUserBalanceData
    {
        [JsonProperty("vendor_member_id")]
        public string TpUserName { get; set; }

        [JsonProperty("balance")]
        public decimal Balance { get; set; }

        [JsonProperty("bonus_balance")]
        public decimal BonusBalance { get; set; }

        [JsonProperty("outstanding")]
        public decimal Outstanding { get; set; }

        [JsonProperty("currency")]
        public int Currency { get; set; }

        [JsonProperty("error_code")]
        public int ErrorCode { get; set; }
    }
}