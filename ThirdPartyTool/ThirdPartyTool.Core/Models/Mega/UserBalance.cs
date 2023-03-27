using System;
using System.Collections.Generic;
using System.Text;
using ThirdPartyTool.Core.Models.Mega;

namespace ThirdPartyTool.Core.Models.Mega
{
    public class BalanceResponse
    {
        public string id { get; set; }
        public string error { get; set; }
        public string jsonrpc { get; set; }
        public decimal? result { get; set; }
    }
    public class Error
    {
        public string code { get; set; }
        public string sn { get; set; }
        public string message { get; set; }
        public string action { get; set; }
    }

    public class ErrorResponse
    {
        public string result { get; set; }
        public Error error { get; set; }
        public string id { get; set; }
        public string jsonrpc { get; set; }
    }
}
