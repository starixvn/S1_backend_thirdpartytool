using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PaymentTool.Core.DataLayers;
using PaymentTool.Core.Models;
using PaymentTool.Core.Models.Enums;

namespace PaymentTool.Core.Service;

public class DepositInfoService
{
    private readonly MySqlClient _mainDbClient;
    private readonly MySqlClient _reportDbClient;

    public DepositInfoService(IServiceProvider provider)
    {
        _mainDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBase.Main);
        _reportDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBase.Report);
    }
    
    public async Task<IEnumerable<DepositInfo>> GetDepositInfoLogs(int index, int limit)
    {
        var parameter = new DynamicParameters(new
        {
            _StartIndex = index,
            _Limit = limit
        });
            
        return await _reportDbClient.QueryAsync<DepositInfo>("Tool_DepositInfo_Get", parameter);
    }
    
    public async Task<(IEnumerable<DepositInfo>, int)> Get1stDepositInfos(int index, int limit)
    {
        var parameter = new DynamicParameters(new
        {
            _StartIndex = index,
            _PageSize = limit
        });
        
        parameter.Add("_RowCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
        var result = await _reportDbClient.QueryAsync<DepositInfo>("Tool_CustTaskTime_1stDeposit_Get", parameter);
        var rowCnt = parameter.Get<int?>("_RowCount") ?? 0;
        
        return (result, rowCnt);
    }
    
    public async Task<IEnumerable<int>> GetLost1stDepositCustIds()
    {
        return await _reportDbClient.QueryAsync<int>("Tool_Lost1stDepositCust_Get");
    }
    
    public async Task<IEnumerable<DepositInfo>> GetDepositInfoLogsByCustIds(IEnumerable<int> custIds)
    {
        var parameter = new DynamicParameters(new
        {
            _CustIds = JsonConvert.SerializeObject(custIds)
        });
            
        return await _reportDbClient.QueryAsync<DepositInfo>("Tool_DepositInfo_GetByCustId", parameter);
    }
    
    public async Task<bool> BatchSetFlowEndTime(IEnumerable<FlowEndTimeInfo> infos)
    {
        var parameter = new DynamicParameters(new
        {
            _Infos = JsonConvert.SerializeObject(infos)
        });
            
        return await _mainDbClient.ExecuteAsync("Tool_Batch_DepositInfo_Set", parameter) > 0;
    }
    
    public async Task<bool> BatchSet1stTransId(IEnumerable<CustTaskTimeInfo> infos)
    {
        var parameter = new DynamicParameters(new
        {
            _Infos = JsonConvert.SerializeObject(infos)
        });
            
        return await _mainDbClient.ExecuteAsync("Tool_CustTaskTime_1stDepositId_Set", parameter) > 0;
    }
    
    public async Task<bool> BatchSetCustTaskTime(IEnumerable<CustTaskTimeInfo> infos)
    {
        var parameter = new DynamicParameters(new
        {
            _Infos = JsonConvert.SerializeObject(infos)
        });
            
        return await _mainDbClient.ExecuteAsync("Tool_CustTaskTime_1StDeposit_Set", parameter) > 0;
    }
    
    public async Task<bool> BatchSet1stDepositInfo(IEnumerable<FirstDepositInfo> ids)
    {
        var parameter = new DynamicParameters(new
        {
            _Infos = JsonConvert.SerializeObject(ids)
        });
            
        return await _mainDbClient.ExecuteAsync("Tool_Batch_1stDepositInfo_Set", parameter) > 0;
    }
}