using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.ThirdParty;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Services
{
    public class SwitchToolService : IHostedService
    {
        private readonly ILogger<SwitchToolService> _logger;
        private readonly BaseTpHelper _baseTpHelper;
        private readonly IEnumerable<int> _supportSiteIds;
        private readonly Dictionary<int, ITpHelper> _tpHandler = new Dictionary<int, ITpHelper>();
        private readonly Dictionary<ActionType, Action<IEnumerable<TpCustInfoModel>, int>> _actionHandler;
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
                    { ActionType.TransferBack, TransferBack }
                };
        }

        public Task StartAsync(CancellationToken cancellationToken)
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
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #region Private

        private void DoAction(ActionType action, int tpId)
        {
            var isSpecific = GetIsSpecific();
            var actionDesc = action.GetDescription();
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            if (isSpecific)
            {
                var list = GetSpecificCustInfoList(tpId);
                _logger.LogInformation($"=== Start specific {actionDesc} ===");
                BatchDoAction(action, list, tpId);
                _logger.LogInformation($"=== End specific {actionDesc}, cost: {stopWatch.Elapsed:g} ===");
            }
            else
            {
                foreach (var siteId in _supportSiteIds)
                {
                    _logger.LogInformation($"=== Start {actionDesc}, siteId: {siteId} ===");
                    var list = _baseTpHelper.GeTptAllCustInfoList(tpId, siteId);
                    BatchDoAction(action, list, tpId);
                    _logger.LogInformation($"=== End {actionDesc}, siteId: {siteId}, cost: {stopWatch.Elapsed:g} ===");
                }
            }
        }

        private void BatchDoAction(ActionType action, IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var list = custInfoList.ToList();
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
                var str = string.Join(',', kickUserFailedList);

                AppendToFileEnd(fileName, str);
            }
        }

        private void TransferBack(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var result = tpHelper.ParallelTransferBack(custInfoList, _taskCount);
            var failedList = result.FailedRecords.ToList();
            var remainList = result.RemainRecords.ToList();

            if (failedList.Any())
            {
                var fileName = $"transfer_failed_{_yyyyMMddHHmm}.txt";
                var str = string.Join(Environment.NewLine,
                        failedList.Select(x => $"{x.CustId}|{x.TransId}|{x.Message}"));

                AppendToFileEnd(fileName, str);
            }

            if (remainList.Any())
            {
                var fileName = $"transfer_remain_{_yyyyMMddHHmm}.csv";
                var str = $"SiteId,CustId,UserName,RemainAmount{Environment.NewLine}" +
                    string.Join(Environment.NewLine,
                        remainList.Select(x => $"{x.SiteId},{x.CustId},{x.UserName},{x.RemainAmount}"));

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

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            Console.WriteLine("=== Start retry transfer back ===");
            BatchTransferRetry(retryList, tpId);
            Console.WriteLine($"=== End retry transfer back, cost: {stopWatch.Elapsed:g} ===");
        }

        private void BatchTransferRetry(IEnumerable<TransferBackRetryModel> retryList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var list = retryList.ToList();
            var count = list.Count();
            var index = 0;

            Console.WriteLine($"total count: {count}, batch count: {_batchCount}");

            while (index * _batchCount < count)
            {
                var doList = list
                    .Skip(index * _batchCount)
                    .Take(_batchCount);

                var result = tpHelper.ParallelTransferRetry(doList, _taskCount);
                var failedList = result.FailedRecords.ToList();
                var remainList = result.RemainRecords.ToList();

                if (failedList.Any())
                {
                    var fileName = $"transfer_failed_{_yyyyMMddHHmm}.txt";
                    var str = string.Join(Environment.NewLine,
                        failedList.Select(x => $"{x.CustId}|{x.TransId}|{x.Message}"));

                    AppendToFileEnd(fileName, str);
                }

                if (remainList.Any())
                {
                    var fileName = $"transfer_remain_{_yyyyMMddHHmm}.csv";
                    var str = $"SiteId,CustId,UserName,RemainAmount{Environment.NewLine}" +
                              string.Join(Environment.NewLine,
                                  remainList.Select(x => $"{x.SiteId},{x.CustId},{x.UserName},{x.RemainAmount}"));

                    AppendToFileEnd(fileName, str);
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
            var records = strRecords.Split(Environment.NewLine)
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

        private static bool GetIsSpecific()
        {
            Console.WriteLine("0: All users , 1: Specific users");
            var strIsSpecific = Console.ReadLine();

            while (strIsSpecific != "0" && strIsSpecific != "1")
            {
                Console.WriteLine($"{strIsSpecific} not support, try again");
                strIsSpecific = Console.ReadLine();
            };

            return strIsSpecific == "1";
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
            var fullPath = $"{_path}/{fileName}";
            if (!File.Exists(fileName))
            {
                Directory.CreateDirectory(_path);
                File.WriteAllText($"{_path}/{fileName}", str);
            }
            else
            {
                using StreamWriter sw = File.AppendText(fullPath);
                sw.Write(str);
            }
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
