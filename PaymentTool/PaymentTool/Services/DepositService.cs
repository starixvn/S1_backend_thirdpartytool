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
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || SetFlowEndTime Start SetAll, limit={limit}, startIndex={startIndex}, endIndex={endIndex}");

        var failedCnt = await DoSetAll(limit, startIndex, endIndex);
        
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || SetFlowEndTime End, failed count={failedCnt}");

        return true;
    }
    
    public async Task<bool> Set1stDepositTransId(int limit, int startIndex, int endIndex)
    {
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || Set1stDepositTransId Start SetAll, limit={limit}, startIndex={startIndex}, endIndex={endIndex}");

        var failedCnt = await DoSetAll1stTransId(limit, startIndex, endIndex);
        
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || Set1stDepositTransId End, failed count={failedCnt}");

        return true;
    }

    private static DateTimeOffset GetFlowEndTime(in IEnumerable<DepositInfo> depositInfos)
    {
        var depositInfo = depositInfos.First();

        if (depositInfos.GroupBy(x=> x.Action).Count() == 1)
            return depositInfo.StatementTime;
        
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

    private async Task<int> DoSetAll1stTransId(int limit, int startIndex, int endIndex)
    {
        var failedCnt = 0;
        var index = startIndex;
        var maxId = index;

        while (true)
        {
            Console.WriteLine($"index => {index}");
            try
            {
                var infos = await _depositService.Get1stDepositInfos(index, limit);

                maxId = infos.Max(x => x.CustId);
                
                if (index == maxId)
                    break;
                
                var custTaskInfos = new List<CustTaskTimeInfo>();

                var data = infos.GroupBy(x => x.CustId);

                foreach (var depositInfos in data)
                {
                    var depositInfo = depositInfos.FirstOrDefault(x => x.FlowEndTime == x.TaskTime);
                    
                    if (depositInfo == null)
                    {
                        depositInfo = depositInfos.OrderBy(x => x.FlowEndTime).First();
                    }

                    custTaskInfos.Add(new CustTaskTimeInfo
                    {
                        CustId = depositInfo.CustId,
                        RefId = depositInfo.TransId
                    });
                }

                if (!await _depositService.BatchSet1stTransId(custTaskInfos))
                {
                    var str = $"index={index}, maxId={maxId}, limit={limit}, data={JsonConvert.SerializeObject(custTaskInfos)}";

                    _logger.LogInformation("BatchSetFlowEndTime update return=0, " + str);
                    AppendToFileEnd($"Set1stTransIdFailed_{_yyyyMMddHHmm}.txt", str + Environment.NewLine);
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

        return failedCnt;
    }

    private async Task<int> DoSetAll(int limit, int startIndex, int endIndex)
    {
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

                var data = logs
                    .Where(x => x.Status is DepositStatusEnum.Approved or DepositStatusEnum.Canceled or DepositStatusEnum.Rejected
                                && x.FlowEndTime != x.StatementTime)
                    .GroupBy(x => x.DepositId);

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

        return failedCnt;
    }
    
    private async Task<int> DoSetLost1StDeposit(int limit)
    {
        var failedCnt = 0;
        var custIds = new List<int>{1425214,1302457,1426041,1426042,1426045,1426046,1425255,1424065,1425988,1425540,1425729,1426055,1331645,1426060,1426072,1424783,1426075,1425953,1425955,1426076,1426081,1280536,1295698,1426029,1426010,1426093,1426104,1426130,1426131,1426136,1426138,1426139,1293657,1426141,1426150,1426153,1426156,1426157,1426170,1322320,1426178,1426179,1426180,1267030,1426181,1280492,1426198,1426204,1424568,1426236,1426243,1426266,1426222,1426269,1426064,1426271,1426274,1423590,1426279,1426283,1426282,1426284,1426255,1426290,1426289,1426262,1426293,1426294,1426295,1422447,1426297,1425959,1426287,1426303,1426305,1426308,1426321,1426322,1426323,1426327,1426330,1426336,1426335,1426332,1426334,1316894,1426339,1425366,1426340,1426343,1426345,1426347,1426344,1426348,1426356,1426355,1426357,1327588,1327438,1426369,1426373,1426377,1426380,1326854,1326857,1426383,1426333,1426213,1426329,1426392,1425127,1426397,1426400,1426398,1426401,1426388,1426134,1426404,1293513,1426409,1426406,1426410,1426413,1426419,1426424,1426420,1426434,1426435,1426441,1426440,1425902,1426453,1426394,1426458,1297961,1426472,1321218,1426479,1426475,1419157,1419245,1426495,1426244,1423496,1426511,1426512,1426513,1426522,1426520,1426526,1426525,1426524,1426533,1426534,1426535,1326284,1426484,1426509,1426544,1426542,1426545,1426543,1426541,1426547,1322915,1426550,1426549,1426551,1426560,1426557,1426561,1426564,1426565,1426567,1426320,1426579,1426582,1426587,1426585,1426580,1426586,1426569,1426572,1426597,1426600,1426602,1426607,1426605,1426604,1426596,1426615,1425184,1426618,1426620,1426619,1426432,1426603,1424603,1426438,1320601,1426639,1426634,1426641,1426645,1426647,1426652,1425283,1426654,1422888,1424945,1426671,1426423,1426635,1426674,1425812,1426680,1426690,1426412,1418843,1426701,1426705,1426708,1426675,1426710,1426721,1400545,1426727,1426276,1426638,1426741,1426743,1426746,1322918,1426755,1426353,1426757,1426761,1426768,1424832,1426783,1424792,1425667,1426791,1426792,1426793,1424316,1426798,1426799,1330400,1426803,1426817,1426814,1426815,1426821,1426823,1426822,1426718,1426829,1426830,1426831,1426832,1422368,1426839,1426844,1426842,1425368,1426849,1426845,1426563,1426854,1426853,1426711,1426864,1426874,1426877,1426857,1426882,1426871,1426869,1426896,1426898,1426901,1426902,1426904,1426905,1426907,1426908,1426913,1426914,1329711,1426921,1426922,1426867,1426924,1426678,1424079,1328079,1426929,1426899,1426926,1426935,1426281,1426940,1426866,1426941,1424089,1426943,1426946,1426950,1426955,1426546,1426965,1426971,1328931,1426982,1426981,1426988,1426966,1426980,1426999,1427006,1271155,1427003,1427008,1427009,1427015,1427020,1427000,1423508,1426958,1426149,1427039,1426311,1427040,1427051,1427048,1427059,1427064,1427069,1427034,1355240,1427072,1426970,1427090,1426972,1427092,1427094,1427104,1427107,1427110,1427112,1427115,1426447,1427119,1427109,1426518,1426530,1426581,1427100,1426455,1427147,1427149,1427153,1423557,1426889,1427103,1427161,1427164,1427127,1427166,1427168,1426523,1427155,1427167,1427176,1426532,1427082,1426838,1426591,1427156,1427192,1427193,1427198,1427180,1427200,1427174,1427204,1427206,1427213,1427214,1427217,1427224,1427056,1427226,1427221,1427231,1427230,1427234,1427240,1427233,1427250,1427264,1427268,1427262,1427272,1427274,1326558,1329335,1427275,1427285,1427273,1427289,1427296,1427300,1427261,1427312,1282592,1427343,1425509,1425291,1427341,1427374,1425009,1326977,1427385,1427338,1427386,1425256,1427392,1111639,1427545,1429259};
        
        var index = 0;
        var endIndex = (int)Math.Ceiling(custIds.Count / (decimal)limit);
        
        while (index * limit <= endIndex)
        {
            Console.WriteLine($"index => {index}");
            try
            {
                var logs = await _depositService
                    .GetDepositInfoLogsByCustIds(custIds.Skip(index * limit).Take(limit));

                if (!logs.Any())
                    break;
            
                var custTaskInfos = new List<CustTaskTimeInfo>();
                var firstDepositInfos = new List<FirstDepositInfo>();

                var data = logs.GroupBy(x => x.CustId);

                foreach (var g in data)
                {
                    var fstDepositInfo = g
                        .First(x => x.FirstDeposit)
                        .FlowEndTime;
                    
                    /*var tmpDepositInfos = g.Select(x =>
                        new FirstDepositInfo
                        {
                            DepositId = x.DepositId,
                            FirstDeposit = x.DepositId == fstDepositInfo?.DepositId
                        });

                    firstDepositInfos.AddRange(tmpDepositInfos);*/
                    
                    if (fstDepositInfo == null)
                        continue;
                    
                    custTaskInfos.Add(new CustTaskTimeInfo
                    {
                        CustId = g.Key,
                        TaskTime = fstDepositInfo.DateTime
                    });
                    
                    /*firstDepositInfos.Add(new FirstDepositInfo
                    {
                        DepositId = fstDepositInfo.DepositId,
                        FirstDeposit = true
                    });*/
                }

                /*if (!await _depositService.BatchSet1stDepositInfo(firstDepositInfos))
                {
                    var str = $"index={index}, limit={limit}, data={JsonConvert.SerializeObject(firstDepositInfos)}";

                    _logger.LogInformation("BatchSet1stDeposit update return=0, " + str);
                    AppendToFileEnd($"Set1stDepositFailed_{_yyyyMMddHHmm}.txt", str + Environment.NewLine);
                }*/

                if (!await _depositService.BatchSetCustTaskTime(custTaskInfos))
                {
                    var str = $"index={index}, limit={limit}, data={JsonConvert.SerializeObject(custTaskInfos)}";
                    
                    _logger.LogInformation("BatchSetCustTaskTime update return=0, " + str);
                    AppendToFileEnd($"SetCustTaskTimeFailed_{_yyyyMMddHHmm}.txt", str + Environment.NewLine);
                }
            }
            catch (Exception e)
            {
                AppendToFileEnd($"Lost1stDeposit_Exception_{_yyyyMMddHHmm}.txt", $"index={index}, limit={limit}, exception={e}" + Environment.NewLine);
                failedCnt++;
            }

            index++;
        }

        return failedCnt;
    }
}