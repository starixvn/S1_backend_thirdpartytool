namespace ThirdPartyTool.Core.Models
{
    public class TransferBackFailedRecord
    {
        public TransferBackFailedRecord() {}

        public TransferBackFailedRecord(int custId, string transId, string message)
        {
            CustId = custId;
            TransId = transId;
            Message = message;
        }

        public int CustId { get; set; }
        public string TransId { get; set; }
        public string Message { get; set; }
    }
}