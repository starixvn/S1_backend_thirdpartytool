using Newtonsoft.Json;

namespace ThirdPartyTool.Core.Models.Saba
{
    public class SabaBaseResp
    {
        [JsonProperty("error_code")]
        public string ErrorCode { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}