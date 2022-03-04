using ThirdPartyTool.Core.Extensions;

namespace ThirdPartyTool.Core.Models
{
    public class TransferRemainRecord
    {
        public TransferRemainRecord() {}

        public TransferRemainRecord(TpCustInfoModel tpCustInfo, decimal remainAmount)
        {
            SiteId = tpCustInfo.SiteId;
            CustId = tpCustInfo.CustId;
            UserName = tpCustInfo.UserName;
            RemainAmount = remainAmount.ToOurAmount(tpCustInfo.ExchangeRate);
        }

        public int SiteId { get; set; }
        public int CustId { get; set; }
        public string UserName { get; set; }
        public decimal RemainAmount { get; set; }
    }
}