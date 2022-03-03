using Newtonsoft.Json;
using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.Models
{
    public class TpTransferInfoModel
    {
        public TpTransferInfoModel() {}

        public TpTransferInfoModel(TpCustInfoModel tpCustInfo, decimal transferAmount, string extensionInfo = null)
        {
            TpId = tpCustInfo.TpId;
            CustId = tpCustInfo.CustId;
            SiteId = tpCustInfo.SiteId;
            UserName = tpCustInfo.TpUserName;
            Amount = transferAmount;
            Prefix = TransferPrefix.FT.ToString();
            Direction = (int)Enums.Direction.ToMain;
            Memo = "system force transfer: saba switch to seamless wallet";
            ThirdId = null;
            ExtensionInfo = extensionInfo;
        }

        public int TpId { get; set; }
        public int CustId { get; set; }
        public string UserName { get; set; }
        public int SiteId { get; set; }
        public int Direction { get; set; }
        public decimal Amount { get; set; }
        public string Memo { get; set; }
        public string ThirdId { get; set; }
        public string Prefix { get; set; }
        public string ExtensionInfo { get; set; }
        public int BonusId { get; set; }
    }
}
