using Dapper;
using MySql.Data.MySqlClient;

namespace UploadPhoto;

public class CustPhotoService
{
    private string _connectionString;

    public CustPhotoService()
    {
        _connectionString = $"server=13.229.1.114;port=3306;user=worker_caching_sha2;database=Siebog;password=Aa12345678!!!;";
    }

    public CustPhotoId? GetCustPhotoId(int sq)
    {
        var sql = "SELECT " +
                  "CustPhotoId.SQ, " +
                  "CustPhotoId.CustId, " +
                  "CustPhotoId.SiteId, " + 
                  "CustPhotoId.PicFileName, " +
                  "CustPhotoId.PhotoIdPic " +
                  "FROM CustPhotoId " +
                  $"WHERE CustPhotoId.SQ = {sq};";
        
        using var connection = new MySqlConnection(_connectionString);
        var result = connection.QueryFirstOrDefault<CustPhotoId>(sql);
        return result;
    }
    
    public int GetLastId()
    {
        var sql = "SELECT t.SQ FROM Siebog.CustPhotoId t ORDER BY SQ DESC LIMIT 1;";
        using var connection = new MySqlConnection(_connectionString);
        return connection.QueryFirst<int>(sql);
    }
}

public class CustPhotoId
{
    public int SQ { get; set; }
    public int CustId { get; set; }
    public int SiteId { get; set; }
    public string PicFileName { get; set; }
    public byte[]? PhotoIdPic { get; set; }
}