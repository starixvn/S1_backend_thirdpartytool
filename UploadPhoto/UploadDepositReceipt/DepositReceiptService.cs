using Dapper;
using MySql.Data.MySqlClient;

namespace DepositReceipt;

public class DepositReceiptService
{
    private readonly string _connectionString;

    public DepositReceiptService()
    {
        _connectionString = $"server=13.229.1.114;port=3306;user=worker_caching_sha2;database=Siebog;password=Aa12345678!!!;";
    }
    
    public IEnumerable<DepositReceipt> GetDepositReceiptById(int depositId)
    {
        var sql = "SELECT concat('DD',DepositId) AS TransId, " +
                  "ReceiptNo, " +
                  "DepositPic " +
                  "FROM DepositReceipt " +
                  $"WHERE DepositId = {depositId}";
                
        using var connection = new MySqlConnection(_connectionString);
        var result = connection.Query<DepositReceipt>(sql);
        return result;
    }
    
    public IEnumerable<DepositReceipt> GetDepositReceiptByTime()
    {
        const string sql = "select concat('DD',DepositId) AS TransId, " +
                           " ReceiptNo, " +
                           "DepositPic " +
                           "from DepositReceipt " +
                           "where DepositId IN ( " +
                           "select DepositId from DepositInfo " +
                           "where CreateTime between '2022-04-01' and '2023-04-01' " +
                           ") " +
                           "order by DepositId desc , ReceiptNo asc ";
                
        using var connection = new MySqlConnection(_connectionString);
        var result = connection.Query<DepositReceipt>(sql);
        return result;
    }
}


public class DepositReceipt
{
    public string TransId { get; set; }
    public int ReceiptNo { get; set; }
    public byte[]? DepositPic { get; set; }
}