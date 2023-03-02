namespace TaggingTool.Core.Models.Customer;

public class CustTagList
{
    public int CustId { get; set; }
        
    public string StrTagList { get; set; }

    public IEnumerable<int> TagList { get; set; }
}