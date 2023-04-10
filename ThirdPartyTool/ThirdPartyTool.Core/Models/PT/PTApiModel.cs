using System;
using System.Collections.Generic;

namespace ThirdPartyTool.Core.Models.PT
{
    internal class ApiErrorResponseModel
    {
        public int errorcode { get; set; }
        public string error { get; set; } = string.Empty;
    }

    internal class ApiSuccessResponseModel<T>
    {
        public List<T> result { get; set; }
    }

    internal class OnlineResponseModel
    {
        public bool result { get; set; }
    }

    internal class LogoutResponseModel
    {
        public string result { get; set; }
    }

    internal class BalanceResponseModel
    {
        public string currencycode { get; set; }
        public decimal balance { get; set; }
        public decimal bonusbalance { get; set; }
        public decimal rc_balance { get; set; }
        public decimal current_bet { get; set; }
        public string playername { get; set; }
    }

    internal class WithdrawResponseModel
    {
        public decimal amount { get; set; }
        public string currentplayerbalance { get; set; }
        public string executiontime { get; set; }
        public string externaltransactionid { get; set; }
        public string instantcashtype { get; set; }
        public string kiosktransactionid { get; set; }
        public DateTime kiosktransactiontime { get; set; }
        public string ptinternaltransactionid { get; set; }
        public string result { get; set; }
    }

    internal class CheckTransactionResponseModel
    {
        public string status { get; set; }
        public string type { get; set; }
        public string amount { get; set; }
        public string currentplayerbalance { get; set; }
        public string kiosktransactiontime { get; set; }
        public string kiosktransactionid { get; set; }
        public object ptinternaltransactionid { get; set; }
        public string ip { get; set; }
        public string username { get; set; }
        public string admin { get; set; }
        public string externaltransactionid { get; set; }
        public string executiontime { get; set; }
    }
}
