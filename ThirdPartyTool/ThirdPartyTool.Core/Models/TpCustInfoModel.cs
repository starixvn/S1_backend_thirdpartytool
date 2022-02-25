namespace ThirdPartyTool.Core.Models
{
    public class TpCustInfoModel
    {
        public int TpId { get; set; }
        public int CustId { get; set; }
        public int SiteId { get; set; }
        public int CurrencyId { get; set; }
        public string UserName { get; set; }
        public string TpUserName { get; set; }
        public string TpPwd { get; set; }
        public string TpCurrencyId { get; set; }
        public decimal ExchangeRate { get; set; }
        public bool IsTest { get; set; }
        public int ActStatus { get; set; }
        public int Status { get; set; }
        public int CStatus { get; set; }
    }
}
