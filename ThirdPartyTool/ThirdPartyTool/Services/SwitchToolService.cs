using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.ThirdParty;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Services
{
    public class SwitchToolService : IHostedService
    {
        private readonly ILogger<SwitchToolService> _logger;
        private readonly BaseTpHelper _baseTpHelper;
        private readonly Dictionary<int, ITpHelper> _tpHandler = new Dictionary<int, ITpHelper>();
        private readonly IEnumerable<int> _supportSiteIds;
        private const string Path = "/tptool/switchtool/";
        public SwitchToolService(IServiceProvider provider, IConfiguration configuration)
        {
            _logger = provider.GetRequiredService<ILogger<SwitchToolService>>();
            _baseTpHelper = provider.GetRequiredService<BaseTpHelper>();
            foreach (var tp in provider.GetServices<ITpHelper>())
            {
                _tpHandler.Add(tp.TpId, tp);
            }
            _supportSiteIds = configuration.GetSection("SupportSiteIds")
                .Get<IEnumerable<int>>();
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var tpId = GetInputTpId();
            PrintMenu();
            var action = Console.ReadLine();
            var isSpecific = GetIsSpecific();


            while (action != "-1")
            {
                switch (action, isSpecific)
                {
                    case ("0", false):
                        foreach (var siteId in _supportSiteIds)
                        {
                            Console.WriteLine($"=== Start kick users, siteId: {siteId} ===");
                            var list = _baseTpHelper.GeTptAllCustInfoList(tpId, siteId);
                            KickUsers(list, tpId);
                            Console.WriteLine($"=== End kick users, siteId: {siteId} ===");
                        }
                        break;

                    case ("0", true):
                        var custListForKick = GetSpecificCustInfoList(tpId);
                        Console.WriteLine("=== Start kick specific users ===");
                        KickUsers(custListForKick, tpId);
                        Console.WriteLine("=== End kick specific users ===");
                        break;

                    case ("1", false):
                        foreach (var siteId in _supportSiteIds)
                        {
                            Console.WriteLine($"=== Start transfer back, siteId: {siteId} ===");
                            var list = _baseTpHelper.GeTptAllCustInfoList(tpId, siteId);
                            //KickUsers(list, tpId);
                            Console.WriteLine($"=== End transfer back, siteId: {siteId} ===");
                        }
                        break;

                    case ("1", true):
                        var custListForTransfer = GetSpecificCustInfoList(tpId);
                        Console.WriteLine("=== Start transfer back for specific users ===");
                        //KickUsers(custListForTransfer, tpId);
                        Console.WriteLine("=== End transfer back for specific users ===");
                        break;

                    default:
                        Console.WriteLine("does not support: " + action);
                        break;
                }

                PrintMenu();
                action = Console.ReadLine();
            };
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void TransferBack(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var result = tpHelper.ParallelTransferBack(custInfoList, 1); // todo: task count => config setting
            // todo: write to file
        }


        private static void PrintMenu()
        {
            Console.WriteLine("================= Menu =================");
            Console.WriteLine("\t 0: Kick users");
            Console.WriteLine("\t 1: Transfer back");
            Console.WriteLine("\t -1: Exit");
            Console.WriteLine("========================================");
        }

        private IEnumerable<TpCustInfoModel> GetSpecificCustInfoList(int tpId)
        {
            var strCustIds = ReadStringFromInputFilePath();
            var custIdList = GetSplitList(strCustIds);
            var jsonCustIds = JsonConvert.SerializeObject(custIdList);
            return _baseTpHelper.GetTpAllCustInfoByCust(tpId, jsonCustIds);
        }

        private void KickUsers(IEnumerable<TpCustInfoModel> custInfoList, int tpId)
        {
            var tpHelper = _tpHandler[tpId];
            var kickUserFailedList = tpHelper.ParallelKickUsers(custInfoList, 1); // todo: task count => config setting
            if (kickUserFailedList.Any())
            {
                var fileName = $"kickfailed_{DateTime.Now:yyyyMMddHHmmss}.txt";
                var str = string.Join(',', kickUserFailedList);
                WriteToFile(fileName, str);
            }
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

        private static void WriteToFile(string fileName, string str)
        {
            Directory.CreateDirectory(Path);
            File.WriteAllText($"{Path}/{fileName}", str);
        }
    }
}
