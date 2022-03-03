namespace ThirdPartyTool.Core.Models
{
    public class SetWalletBalanceModel
    {
        public int CustId { get; set; }
        public int SiteId { get; set; }
        public string UserName { get; set; }
        public decimal Amount { get; set; }
        public int TpId { get; set; }
        public string Memo { get; set; }
        public string RefTransId { get; set; }
    }
}