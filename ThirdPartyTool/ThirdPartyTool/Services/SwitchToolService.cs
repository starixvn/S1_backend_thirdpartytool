using System.Diagnostics;
using Newtonsoft.Json;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.ThirdParty;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Services
{
    public class SwitchToolService
    {
        private readonly ILogger<SwitchToolService> _logger;
        private readonly BaseTpHelper _baseTpHelper;
        private readonly IEnumerable<int> _supportSiteIds;
        private readonly Dictionary<int, ITpHelper> _tpHandler = new();
        private readonly Dictionary<ActionType, Action<IEnumerable<TpCustInfoModel>, int>> _actionHandler;
        private static readonly Dictionary<string, string> TargetMenu = new()
        {
            {"0", "All users"},
            {"1", "Specific users"},
            {"2", "Specific users by CustId"}
        };
        private readonly int _batchCount;
        private readonly int _taskCount;
        private readonly string _path;
        private readonly string _yyyyMMddHHmm;

        public SwitchToolService(IServiceProvider provider, IConfiguration configuration)
        {
            _logger = provider.GetRequiredService<ILogger<SwitchToolService>>();
            _baseTpHelper = provider.GetRequiredService<BaseTpHelper>();
            foreach (var tp in provider.GetServices<ITpHelper>())
            {
                _tpHandler.Add(tp.TpId, tp);
            }

            _yyyyMMddHHmm = DateTime.Now.ToString("yyyyMMddHHmm");
            _taskCount = configuration.GetValue<int>("MaxDegreeOfParallelism");
            _batchCount = configuration.GetValue<int>("BatchCount");
            _path = configuration.GetValue<string>("FilePath");
            _supportSiteIds = configuration.GetSection("SupportSiteIds")
                .Get<IEnumerable<int>>();

            _actionHandler = 
                new Dictionary<ActionType, Action<IEnumerable<TpCustInfoModel>, int>>
                {
                    { ActionType.KickUsers, KickUsers },
                    { ActionType.TransferBack, TransferBack },
                    { ActionType.GetBalance, GetBalance },
                    { ActionType.AdjustBonus, AdjustBonus }
                };
        }

        public Task StartAsync()
        {
            var tpId = GetInputTpId();
            PrintMenu();
            var strAction = Console.ReadLine();
            var action = ParseActionType(strAction);
            
            while (action != ActionType.Exit)
            {
                switch (action)
                {
                    case ActionType.KickUsers:
                    case ActionType.TransferBack:
                    case ActionType.GetBalance:
                    case ActionType.AdjustBonus:
                        DoAction(action, tpId);
                        break;

                    case ActionType.TransferRetry:
                        TransferRetry(tpId);
                        break;
                    default:
                        Console.WriteLine("Do not support: " + strAction);
                        break;
                }

                PrintMenu();
                strAction = Console.ReadLine();
                action = ParseActionType(strAction);
            };
            
            Console.WriteLine("Mission Completed Bye Bye");
            return Task.CompletedTask;
        }

        #region Private

        private void DoAction(ActionType action, int tpId)
        {
            var target = GetTargetOption();

            
            switch (target)
            {
                
                case "0":
                case "1":
                    Process(action, tpId, target);
                    
                    break;
                case "2":
                    ProcessSpecialList(action, tpId);
                    
                    break;
            }
        }
        
        private void Process(ActionType action, int tpId, string target)
        {
            var actionDesc = action.GetDescription();
            var stopWatch = new Stopwatch();
            var totalCount = 0;
            var totalCost = TimeSpan.Zero;
            
            Console.WriteLine($"Will do {actionDesc} for sites: {string.Join(",", _supportSiteIds)}");
            if (!Confirm())
                return;

            foreach (var siteId in _supportSiteIds)
            {
                stopWatch.Restart();
                
                _logger.LogInformation($"=== Start {actionDesc}, siteId: {siteId} ===");
                var list = target == "0"
                    ? _baseTpHelper.GeTptAllCustInfoList(tpId, siteId)
                    : _baseTpHelper.GetSpecificConditionCustInfoList(tpId, siteId);
                var count = list.Count();
                BatchDoAction(action, list, tpId);
                
                stopWatch.Stop();
                _logger.LogInformation($"=== End {actionDesc}, siteId: {siteId}, count: {count}, total cost: {stopWatch.Elapsed:g} ===");
                totalCount += count;
                totalCost += stopWatch.Elapsed;
            }

            _logger.LogInformation($"=== {actionDesc} End, total count: {totalCount}, total cost: {totalCost:g}  ===");
        }
        
        private void ProcessSpecialList(ActionType action, int tpId)
        {
            var actionDesc = action.GetDescription();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            
            Console.WriteLine($"The specific custom list to {actionDesc}:");
            Console.WriteLine("\tSiteId, CustId, UserName");
            
            var custInfoList = GetSpecificCustInfoList(tpId);
            foreach (var cust in custInfoList)
                Console.WriteLine($"\t{cust.SiteId}, {cust.CustId}, {cust.UserName}");
       
            if (!Confirm())
                return;

            _logger.LogInformation($"=== Start specific {actionDesc} ===");
            BatchDoAction(action, custInfoList, tpId);
            _logger.LogInformation($"=== End specific {actionDesc}, total count: {custInfoList.Count()}, cost: {stopWatch.Elapsed:g} ===");
        }

        private void BatchDoAction(ActionType action, IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var list = custInfoList;
            var count = list.Count();
            var index = 0;

            Console.WriteLine($"total count: {count}, batch count: {_batchCount}");

            while (index * _batchCount < count)
            {
                var doList = list
                    .Skip(index * _batchCount)
                    .Take(_batchCount);

                _actionHandler[action].Invoke(doList, tpId);

                index++;
                Console.WriteLine($"=== batch index: {index} ===");
            }
        }

        private void KickUsers(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var kickUserFailedList = tpHelper.ParallelKickUsers(custInfoList, _taskCount);
            if (kickUserFailedList.Any())
            {
                var fileName = $"kick_failed_{_yyyyMMddHHmm}.txt";
                var str = string.Join(',', kickUserFailedList) + Environment.NewLine;

                AppendToFileEnd(fileName, str);
            }
        }

        private void TransferBack(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var result = tpHelper.ParallelTransferBack(custInfoList, _taskCount);
            var failedList = result.FailedRecords;
            var remainList = result.RemainRecords;

            if (failedList.Any())
            {
                var fileName = $"transfer_failed_{_yyyyMMddHHmm}.txt";
                var str = string.Join(Environment.NewLine,
                        failedList.Select(x => $"{x.CustId}|{x.TransId}|{x.Message}"));

                AppendToFileEnd(fileName, Environment.NewLine + str);
            }

            if (remainList.Any())
            {
                var fileName = $"transfer_remain_{_yyyyMMddHHmm}.csv";
                var str = $"SiteId,CustId,UserName,RemainAmount{Environment.NewLine}" +
                    string.Join(Environment.NewLine,
                        remainList.Select(x => $"{x.SiteId},{x.CustId},{x.UserName},{x.RemainAmount}"));

                AppendToFileEnd(fileName, Environment.NewLine + str);
            }
        }

        private void GetBalance(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var result = tpHelper.GetBalance(custInfoList, _taskCount);

            if (result.Any())
            {
                var fileName = $"UserBalance_{_yyyyMMddHHmm}.csv";
                var str = string.Join(Environment.NewLine,
                        result.Where(a=>a.balance>0).Select(x => $"{x.TpUserName},{x.UserName},{x.Currency},{x.balance},{x.RawMessage}"));

                AppendToFileEnd(fileName, str);
            }
        }
        /// <summary>
        /// 轉完後續要變動Bonus使用
        /// </summary>
        /// <param name="custInfoList"></param>
        /// <param name="tpId">Sw TpId</param>
        private void AdjustBonus(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var failedRecords = tpHelper.AdjustBonus(custInfoList);

            if (failedRecords.Any())
            {
                var fileName = $"adjust_bonus_failed_{_yyyyMMddHHmm}.txt";
                var str = string.Join(Environment.NewLine,
                    failedRecords.Select(x => $"{x.CustId}|{x.Message}"));

                AppendToFileEnd(fileName, str);
            }
        }

        private void TransferRetry(int tpId)
        {
            var retryList = GetTransferBackRetryList().ToList();

            if (!retryList.Any())
            {
                Console.WriteLine("No retry data!");
                return;
            }
            
            var jsonCustIds = JsonConvert.SerializeObject(retryList.Select(x => x.CustId));
            var custInfoList = _baseTpHelper.GetTpAllCustInfoByCust(tpId, jsonCustIds);
            retryList.ForEach(x => x.TpCustInfo = custInfoList.FirstOrDefault(ci => ci.CustId == x.CustId));

            Console.WriteLine("The list for retry transfer back:");
            Console.WriteLine("\tSiteId, CustId, UserName, TransId");
            foreach (var item in retryList)
                Console.WriteLine($"\t{item.TpCustInfo.SiteId}, {item.CustId}, {item.TpCustInfo.UserName}, {item.TransId}");

            if (!Confirm())
                return;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            Console.WriteLine("=== Start retry transfer back ===");
            BatchTransferRetry(retryList, tpId);
            Console.WriteLine($"=== End retry transfer back, cost: {stopWatch.Elapsed:g} ===");
        }

        private void BatchTransferRetry(IEnumerable<TransferBackRetryModel> retryList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var list = retryList;
            var count = list.Count();
            var index = 0;

            Console.WriteLine($"total count: {count}, batch count: {_batchCount}");

            while (index * _batchCount < count)
            {
                var doList = list
                    .Skip(index * _batchCount)
                    .Take(_batchCount);

                var result = tpHelper.ParallelTransferRetry(doList, _taskCount);
                var failedList = result.FailedRecords;
                var remainList = result.RemainRecords;

                if (failedList.Any())
                {
                    var fileName = $"transfer_failed_{_yyyyMMddHHmm}.txt";
                    var str = string.Join(Environment.NewLine,
                        failedList.Select(x => $"{x.CustId}|{x.TransId}|{x.Message}"));

                    AppendToFileEnd(fileName, Environment.NewLine + str);
                }

                if (remainList.Any())
                {
                    var fileName = $"transfer_remain_{_yyyyMMddHHmm}.csv";
                    var str = $"SiteId,CustId,UserName,RemainAmount{Environment.NewLine}" +
                              string.Join(Environment.NewLine,
                                  remainList.Select(x => $"{x.SiteId},{x.CustId},{x.UserName},{x.RemainAmount}"));

                    if (index > 0)
                        str = Environment.NewLine + str;
                    
                    AppendToFileEnd(fileName, Environment.NewLine + str);
                }

                index++;
                Console.WriteLine($"=== batch index: {index} ===");
            }
        }


        private static void PrintMenu()
        {
            Console.WriteLine("================= Menu =================");
            foreach (ActionType actionType in Enum.GetValues(typeof(ActionType)))
            {
                if (actionType != ActionType.Undefined)
                    Console.WriteLine($"\t {(int)actionType}: {actionType}");
            }
            Console.WriteLine("========================================");
        }

        private IEnumerable<TpCustInfoModel> GetSpecificCustInfoList(int tpId)
        {
            var strCustIds = ReadStringFromInputFilePath();
            var custIdList = GetSplitList(strCustIds);
            var jsonCustIds = JsonConvert.SerializeObject(custIdList);

            return _baseTpHelper.GetTpAllCustInfoByCust(tpId, jsonCustIds);
        }

        private IEnumerable<TransferBackRetryModel> GetTransferBackRetryList()
        {
            var strRecords = ReadStringFromInputFilePath();
            var records = strRecords
                .Split(Environment.NewLine)
                .Skip(1)
                .Select(x =>
                {
                    var item = x.Split('|');
                    return new TransferBackRetryModel
                    {
                        CustId = int.Parse(item[0]),
                        TransId = item[1]
                    };
                });

            return records;
        }

        private static string ReadStringFromInputFilePath()
        {
            Console.WriteLine("Enter the file path:");
            var filePath = Console.ReadLine();

            while (!File.Exists(filePath))
            {
                Console.WriteLine("File not exists, enter the file path:");
                filePath = Console.ReadLine();
            };

            Console.WriteLine("Reading CustId list from file...");

            return File.ReadAllText(filePath);
        }

        private int GetInputTpId()
        {
            Console.WriteLine("Enter the TpId:");
            var strTpId = Console.ReadLine();
            int tpId;

            while (!int.TryParse(strTpId, out tpId) || !_tpHandler.ContainsKey(tpId))
            {
                Console.WriteLine("TpId not support, enter the TpId:");
                strTpId = Console.ReadLine();
            };

            return tpId;
        }

        private static string GetTargetOption()
        {
            Console.WriteLine(string.Join(", ", TargetMenu.Select(x => $"{x.Key}: {x.Value}")));
            var target = Console.ReadLine();

            while (!TargetMenu.ContainsKey(target ?? string.Empty))
            {
                Console.WriteLine($"{target} not support, try again");
                target = Console.ReadLine();
            };

            return target;
        }

        private static IEnumerable<string> GetSplitList(string str)
        {
            return str
                .Replace(" ", string.Empty)
                .Split(new[] { Environment.NewLine, "," }, StringSplitOptions.None)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct();
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
                using StreamWriter sw = File.AppendText(fullPath);
                sw.Write(str);
            }
        }

        private static bool Confirm()
        {
            Console.WriteLine("Please enter 'Y' to confirm that you want to do the action.");
            var str = Console.ReadLine();

            return str?.Equals("Y", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private static ActionType ParseActionType(string str)
        {
            try
            {
                var num = int.Parse(str);
                return (ActionType)num;
            }
            catch
            {
                return ActionType.Undefined;
            }
        }

        #endregion
    }
}
