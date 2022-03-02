namespace ThirdPartyTool.Core.Models
{
    public class TransferBackRetryModel
    {
        public int CustId { get; set; }

        public string TransId { get; set; }

        public TpCustInfoModel TpCustInfo { get; set; }
    }
}
