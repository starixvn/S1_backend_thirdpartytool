namespace ThirdPartyTool.Core.Models
{
    public class TransferRemainRecord
    {
        public int SiteId { get; set; }
        public int CustId { get; set; }
        public string UserName { get; set; }
        public decimal RemainAmount { get; set; }
    }
}