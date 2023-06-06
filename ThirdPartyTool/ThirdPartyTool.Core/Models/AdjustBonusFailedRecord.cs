namespace ThirdPartyTool.Core.Models;

public class AdjustBonusFailedRecord
{
    public AdjustBonusFailedRecord() {}

    public AdjustBonusFailedRecord(int custId, string message)
    {
        CustId = custId;
        Message = message;
    }

    public int CustId { get; set; }
    public string Message { get; set; }
}