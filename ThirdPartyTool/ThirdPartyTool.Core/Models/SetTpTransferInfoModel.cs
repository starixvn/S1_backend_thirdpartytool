using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.Models
{
    public class SetTpTransferInfoModel
    {
        public int TransferId { get; set; }
        public string Memo { get; set; }
        public string ThirdId { get; set; }
        public TransferStatus ForStatus { get; set; }
        public TransferStatus Status { get; set; }
    }
}
