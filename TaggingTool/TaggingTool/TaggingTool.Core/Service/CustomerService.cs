using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using TaggingTool.Core.DataLayers;
using TaggingTool.Core.Models.Customer;
using TaggingTool.Core.Models.Enums;
using Newtonsoft.Json;

namespace TaggingTool.Core.Service;

public class CustomerService
{
    private readonly MySqlClient _mainDbClient;
    private readonly MySqlClient _reportDbClient;

    public CustomerService(IServiceProvider provider)
    {
        _mainDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBase.Main);
        _reportDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBase.Report);
    }
    
    public async Task<IEnumerable<AccountNameInfo>> GetAllAccountNameList(int siteId)
    {
        var parameter = new DynamicParameters(new
        {
            _SiteId = siteId
        });
            
        return await _reportDbClient.QueryAsync<AccountNameInfo>("Service_CustAccountNameList_Get", parameter);
    }
    
    public async Task<IEnumerable<CustAccountNameInfo>> GetSimilarAccountName(int siteId, IEnumerable<string> accountNames)
    {
        var parameter = new DynamicParameters(new
        {
            _SiteId = siteId,
            _AccountNames = JsonConvert.SerializeObject(accountNames)
        });
        
        return await _mainDbClient.QueryAsync<CustAccountNameInfo>("Member_SimilarAccountName_Get",parameter);
    }
    
    public IEnumerable<string> GetAccountNameDirtyData()
    {
        var sql = @"select distinct cbi.AccountName from CustBankInfo as cbi
left join Customer as c on c.CustId = cbi.CustId
where c.CustId is null;";
        
        return _mainDbClient.Query<string>(sql, commandType: CommandType.Text);
    }
    
    public async Task<IEnumerable<CustTagList>> GetCustTagList(IEnumerable<int> custList)
    {
        var parameter = new DynamicParameters(new
        {
            _CustList = JsonConvert.SerializeObject(custList)
        });
            
        return await _reportDbClient.QueryAsync<CustTagList>("Service_CustTagList_Get", parameter);
    }
        
    public async Task<int> SetCustInfoTagList(IEnumerable<CustTagList> tagList)
    {
        var parameter = new DynamicParameters(new
        {
            _Infos = JsonConvert.SerializeObject(tagList)
        });
            
        return await _mainDbClient.ExecuteAsync("Service_CustInfoTagList_Set", parameter);
    }
}