using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Mega;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Core.ThirdParty
{
    public class MegaTpHelper : BaseTpHelper, ITpHelper
    {
        public int TpId => 22;
        private const string sn = "ld00";
        private const string ApiUrl = "http://mgt3.36ozhushou.com/mega-cloud/api/";
        private Dictionary<string, string> _secretCode = new Dictionary<string, string>()
        {
            { "MYR", "DFtRZBooiz6BMcwZZ5mK+3itsC4="},
            { "USD", "Oksk5nWrOH3HyKcKsDJzP6/UArI="},
            { "THB", "kqVtpyFGVvkIVVWXr/UJGSmHS2U="},
            { "IDR", "uJApcPF/MfQ3Z6tqdfmbKR5Xa5Q="},
        };
        //private Dictionary<string, string> _secretCode = new Dictionary<string, string>()
        //{
        //    { "MYR", "XSRZhAPI2FnZySk7Gh/Nh302P7Q="},
        //    { "USD", "Oksk5nWrOH3HyKcKsDJzP6/UArI="},
        //    { "THB", "89g/UfsSRu9R3cRkAUQ7lXyvzq4="},
        //    { "IDR", "aW60SR0RzhKd6K+326Jz6TZ74Os="},
        //};
        public MegaTpHelper(IServiceProvider provider) : base(provider) {}

        public IEnumerable<int> ParallelKickUsers(IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            throw new NotImplementedException();
        }

        public (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords) ParallelTransferBack(IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            throw new NotImplementedException();
        }

        public (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords) ParallelTransferRetry(IEnumerable<TransferBackRetryModel> todoList, int taskCount)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UserBalance> GetBalance(IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            var listBalance = new List<UserBalance>();



            Parallel.ForEach(todoList,
                new ParallelOptions() { MaxDegreeOfParallelism = taskCount },
                tpCustInfo =>
                {
                    try
                    {
                        var userBalance = GetBalance(tpCustInfo);
                        Logger.LogInformation($"{userBalance.UserName}: {userBalance.balance}");
                        listBalance.Add(userBalance);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                    }
                });
            return listBalance;
        }

        public IEnumerable<AdjustBonusFailedRecord> AdjustBonus(IEnumerable<TpCustInfoModel> todoList)
        {
            throw new NotImplementedException();
        }

        private UserBalance GetBalance(TpCustInfoModel tpCustInfo) 
        {
            var currency = tpCustInfo.TpCurrencyId;
            var secretCode = _secretCode[currency];

            var uuid = Guid.NewGuid();

            var sign = $"{uuid}{sn}{tpCustInfo.TpUserName}{secretCode}";

            var request = new
            {
                id = uuid,
                method = "open.mega.balance.get",
                @params = new
                {
                    random = uuid,
                    digest = ToMD5(sign).ToUpper(),
                    sn = sn,
                    loginId = tpCustInfo.TpUserName,
                },
                jsonrpc = "2.0",
            };
            var userBalance = new UserBalance()
            {
                CustId = tpCustInfo.CustId,
                UserName = tpCustInfo.UserName,
                TpUserName = tpCustInfo.TpUserName,
                Currency = tpCustInfo.CurrencyId
            };
            using (var httpClient = new HttpClient()) 
            {
                var contentPost = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = httpClient.PostAsync(ApiUrl, contentPost).Result;
                var responsebody = response.Content.ReadAsStringAsync().Result;

                var error = JsonConvert.DeserializeObject<ErrorResponse>(responsebody);
                if (error.error != null)
                {
                    userBalance.balance = -1;
                    userBalance.RawMessage = error.error.message;
                    return userBalance;
                }
                var json = JsonConvert.DeserializeObject<BalanceResponse>(responsebody);
                if (json.error == null)
                {
                    userBalance.balance = json.result ?? 0;
                    return userBalance;
                }
            }

            return userBalance;
        }

        private string ToMD5(string encodeStr)
        {
            using var cryptoMD5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(encodeStr);
            var hash = cryptoMD5.ComputeHash(bytes);
            var md5 = BitConverter.ToString(hash)
              .Replace("-", string.Empty);

            return md5;
        }
        protected bool IsJSON(string jsonString)
        {
            var value = jsonString.Trim();

            return (value.StartsWith("{") && value.EndsWith("}")) || (value.StartsWith("[") && value.EndsWith("]"));
        }
        private HttpClientHandler CreateHandler()
        {
            var proxy = "10.4.194.104";
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip,
                UseProxy = true,
                Proxy = new WebProxy(proxy, 3128)
            };

            return handler;
        }
    }
}
