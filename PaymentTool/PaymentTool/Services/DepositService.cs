using Newtonsoft.Json;
using PaymentTool.Core.Models;
using PaymentTool.Core.Models.Enums;
using PaymentTool.Core.Service;

namespace PaymentTool.Services;

public class DepositService
{
    private readonly DepositInfoService _depositService;
    private readonly ILogger<DepositService> _logger;
    private readonly string _path;
    private readonly string _yyyyMMddHHmm;
    
    public DepositService(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        _depositService = provider.GetRequiredService<DepositInfoService>();
        _logger = provider.GetRequiredService<ILogger<DepositService>>();
        _path = configuration.GetValue<string>("FilePath");
        _yyyyMMddHHmm = DateTime.Now.ToString("yyyyMMddHHmm");
    }


    public async Task<bool> SetFlowEndTime(int limit, int startIndex, int endIndex)
    {
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || SetFlowEndTime Start, limit={limit}, startIndex={startIndex}, endIndex={endIndex}");
        var failedCnt = 0;
        
        var index = startIndex;
        var maxId = index;

        while (true)
        {
            Console.WriteLine($"index => {index}");
            try
            {
                var logs = await _depositService.GetDepositInfoLogs(index, limit);

                if (!logs.Any())
                    break;

                maxId = logs.Max(x => x.DepositId);
                
                if (index == maxId)
                    break;
                
                var flowEndTimeInfos = new List<FlowEndTimeInfo>();
                var custTaskInfos = new List<CustTaskTimeInfo>();

                var data = logs.GroupBy(x => x.DepositId);

                foreach (var depositInfos in data)
                {
                    var depositInfo = depositInfos.First();
                    var flowEndTime = GetFlowEndTime(depositInfos);

                    flowEndTimeInfos.Add(new FlowEndTimeInfo
                    {
                        DepositId = depositInfo.DepositId,
                        FlowEndTime = flowEndTime.DateTime
                    });

                    if (depositInfo.FirstDeposit)
                    {
                        custTaskInfos.Add(new CustTaskTimeInfo
                        {
                            CustId = depositInfo.CustId,
                            TaskTime = flowEndTime.DateTime
                        });
                    }

                }

                if (!await _depositService.BatchSetFlowEndTime(flowEndTimeInfos))
                {
                    var str = $"index={index}, maxId={maxId}, limit={limit}, data={JsonConvert.SerializeObject(flowEndTimeInfos)}";

                    _logger.LogInformation("BatchSetFlowEndTime update return=0, " + str);
                    AppendToFileEnd($"SetFlowEndTimeFailed_{_yyyyMMddHHmm}.txt", str + Environment.NewLine);
                }

                if (custTaskInfos.Any())
                {
                    if (!await _depositService.BatchSetCustTaskTime(custTaskInfos))
                    {
                        var str = $"index={index}, maxId={maxId}, limit={limit}, data={JsonConvert.SerializeObject(custTaskInfos)}";
                        
                        _logger.LogInformation("BatchSetCustTaskTime update return=0, " + str);
                        AppendToFileEnd($"SetCustTaskTimeFailed_{_yyyyMMddHHmm}.txt", str + Environment.NewLine);
                    }
                }
            }
            catch (Exception e)
            {
                AppendToFileEnd($"Exception_{_yyyyMMddHHmm}.txt", $"index={index}, maxId={maxId}, limit={limit}, exception={e}" + Environment.NewLine);
                failedCnt++;
            }

            index = maxId;
            
            if (endIndex != -1 && index >= endIndex)
                break;
        }

        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || SetFlowEndTime End, failed count={failedCnt}");

        return true;
    }

    private static DateTimeOffset GetFlowEndTime(in IEnumerable<DepositInfo> depositInfos)
    {
        var depositInfo = depositInfos.First();

        return depositInfo.Status switch
        {
            DepositStatusEnum.Rejected or DepositStatusEnum.Approved
                => GetTime(depositInfo, depositInfos),
            
            DepositStatusEnum.Canceled
                => depositInfo.UpdateTime, // 以前cancel沒寫入DepositLog
            
            _ => depositInfo.CreateTime
        };
    }

    private static DateTimeOffset GetTime(DepositInfo depositInfo, in IEnumerable<DepositInfo> depositInfos)
    {
        var time = depositInfos
            .Where(x => x.Action == null || x.Action == depositInfo.Status)
            .Min(x => x.LogTime);

        if (time.HasValue)
            return time.Value;
        
        if (depositInfo.DepositTime.HasValue)
            return depositInfo.DepositTime.Value;
        
        if (depositInfo.CallbackTime.HasValue)
            return depositInfo.CallbackTime.Value;

        return depositInfo.UpdateTime;
    }
    
    private void AppendToFileEnd(string fileName, string str)
    {
        var fullPath = Path.Combine(_path, fileName);
        if (!File.Exists(fileName))
        {
            Directory.CreateDirectory(_path);
            File.AppendAllText(fullPath, str);
        }
        else
        {
            using var sw = File.AppendText(fullPath);
            sw.Write(str);
        }
    }
}