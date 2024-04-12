namespace ThirdPartyTool.Core.Models;

public class BatchDoResult
{
    public BatchDoResult(string custId, int result)
    {
        CustId = custId;
        Result = result;
    }
    
    public string CustId { get; set; }
    public int Result { get; set; }
}