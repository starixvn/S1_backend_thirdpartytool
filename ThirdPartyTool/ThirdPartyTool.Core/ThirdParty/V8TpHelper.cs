using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Core.ThirdParty
{
    internal class V8TpHelper : BaseTpHelper, ITpHelper
    {
        
        private readonly Dictionary<int, ThirdPartyModel> _thirdPartyModel;
        
        #region Constructors

        public V8TpHelper(IServiceProvider provider) : base(provider)
        {
            _thirdPartyModel = provider.GetRequiredService<IEnumerable<ThirdPartyModel>>()
                .Where(item => item.TpId == TpId)
                .ToDictionary(item => item.SiteId, item => item);
        }

        #endregion

        public int TpId => 25;

        public IEnumerable<int> ParallelKickUsers(IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            var failedList = new List<int>();
            Parallel.ForEach(todoList,
                new ParallelOptions() { MaxDegreeOfParallelism = taskCount },
                tpCustInfo =>
                {
                    try
                    {
                        var success = KickUserAsync(tpCustInfo).Result;
            
                        if (!success)
                            failedList.Add(tpCustInfo.CustId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                        failedList.Add(tpCustInfo.CustId);
                    }
                });

            return failedList;
        }

        public (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords) ParallelTransferBack(
           IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            var failedList = new List<TransferBackFailedRecord>();
            var remainList = new List<TransferRemainRecord>();
            
            Parallel.ForEach(todoList,
                new ParallelOptions { MaxDegreeOfParallelism = taskCount },
                tpCustInfo =>
                {
                    // Step 1: 取得餘額
                    var tpBalanceInfo = CheckUserBalanceAsync(tpCustInfo).Result;
                    var transId = string.Empty;
                    try
                    {
                        if (tpBalanceInfo == null)
                            throw new CustomException($"{tpCustInfo.CustId} {tpCustInfo.TpUserName} TP balance info is null.");
            
                        if (tpBalanceInfo.balance <= (decimal)0.33 || tpCustInfo.IsTest)
                            return;
            
                        // Step 2: 轉回餘額
                        var transferAmount = tpBalanceInfo.balance.ToOurAmount(tpCustInfo.ExchangeRate);
                        var jsonTpBalanceInfo = JsonConvert.SerializeObject(tpBalanceInfo);
                        var tpTransferInfo = new TpTransferInfoModel(tpCustInfo, transferAmount, jsonTpBalanceInfo);
            
                        transId = CreateTpTransferInfo(tpTransferInfo);
                        var (status,resp,thirdId) = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;
            
                        // 有殘餘轉不乾淨的金額
                        if (resp.d.money > (decimal)0.33)
                            remainList.Add(new TransferRemainRecord(tpCustInfo, resp.d.money));
            
                        // 轉帳失敗不 Commit，Retry 檢查單號的時候再做
                        if (status != TransferStatus.Success)
                        {
                            var error = ErrorMassage.ContainsKey(resp.d.code)
                                ? ErrorMassage[resp.d.code]
                                : resp.d.code.ToString();
                            
                            var msg = !string.IsNullOrWhiteSpace(error)
                                ? $"Error Result：{resp.d.code} {error}"
                                : status.ToString();
            
                            failedList.Add(new TransferBackFailedRecord(tpCustInfo.CustId, transId, msg));
            
                            return;
                        }
            
                        // Step 3: 更新轉帳結果
                        CommitTransfer(tpCustInfo, transId, transferAmount, status, thirdId);
            
                        Task.Delay(200).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                        failedList.Add(new TransferBackFailedRecord(tpCustInfo.CustId, transId, ex.Message));
                    }
                });

            return (failedList, remainList);
        }

        public (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords) ParallelTransferRetry(
            IEnumerable<TransferBackRetryModel> todoList, int taskCount)
        {
             var failedList = new List<TransferBackFailedRecord>();
            var remainList = new List<TransferRemainRecord>();
            List<UserBalance> tpBalanceInfoList;
            
            // Step 1: 取得餘額
            try
            {
                var tpCustInfoList = todoList.Select(cust => cust.TpCustInfo).ToList();
                tpBalanceInfoList = CheckUserBalanceAsync(tpCustInfoList).Result.ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                failedList = todoList
                    .Select(x => new TransferBackFailedRecord(x.CustId, string.Empty, "Get V8 user balance failed."))
                    .ToList();
            
                return (failedList, remainList);
            }
            
            Parallel.ForEach(todoList,
                new ParallelOptions { MaxDegreeOfParallelism = taskCount },
                Retry =>
                {
                    var transId = string.Empty;
                    var tpCustInfo = Retry.TpCustInfo;
                    try
                    {
                        var tpBalanceInfo = tpBalanceInfoList.FirstOrDefault(x => x.TpUserName == tpCustInfo.TpUserName);
            
                        if (tpBalanceInfo == null)
                            throw new CustomException($"{tpCustInfo.CustId} {tpCustInfo.TpUserName} TP balance info is null.");
            
                        if (tpBalanceInfo.balance <= 0 || tpCustInfo.IsTest)
                            return;
            
                        // Step 2: 轉回餘額
                        var transferAmount = tpBalanceInfo.balance.ToOurAmount(tpCustInfo.ExchangeRate);
                        var jsonTpBalanceInfo = JsonConvert.SerializeObject(tpBalanceInfo);
                        var tpTransferInfo = new TpTransferInfoModel(tpCustInfo, transferAmount, jsonTpBalanceInfo);
            
                        transId = CreateTpTransferInfo(tpTransferInfo);
                        var (status,resp,thirdId) = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;
            
                        // 有殘餘轉不乾淨的金額
                        if (resp.d.money > 0)
                            remainList.Add(new TransferRemainRecord(tpCustInfo, resp.d.money));
            
                        // 轉帳失敗不 Commit，Retry 檢查單號的時候再做
                        if (status != TransferStatus.Success)
                        {
                            var error = ErrorMassage.ContainsKey(resp.d.code)
                                ? ErrorMassage[resp.d.code]
                                : resp.d.code.ToString();
                            
                            var msg = !string.IsNullOrWhiteSpace(error)
                                ? $"Error Result：{resp.d.code} {error}"
                                : status.ToString();
            
                            failedList.Add(new TransferBackFailedRecord(tpCustInfo.CustId, transId, msg));
            
                            return;
                        }
            
                        // Step 3: 更新轉帳結果
                        CommitTransfer(tpCustInfo, transId, transferAmount, status, thirdId);
            
                        Task.Delay(200).Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                        failedList.Add(new TransferBackFailedRecord(tpCustInfo.CustId, transId, ex.Message));
                    }
                });

            return (failedList, remainList);
        }

        #region No Use

        public IEnumerable<UserBalance> GetBalance(IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AdjustBonusFailedRecord> AdjustBonus(IEnumerable<TpCustInfoModel> todoList)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region private

        private async Task<bool> KickUserAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;

            var apiUrl = tpSetting.ContainsKey("ApiUrl") && !string.IsNullOrEmpty(tpSetting["ApiUrl"]) ? tpSetting["ApiUrl"] : throw new Exception("ApiUrl");
            var agent = tpSetting.ContainsKey("Agent") && !string.IsNullOrEmpty(tpSetting["Agent"]) ? tpSetting["Agent"] : throw new Exception("Agent");
            var desKey = tpSetting.ContainsKey("Deskey") && !string.IsNullOrEmpty(tpSetting["Deskey"]) ? tpSetting["Deskey"] : throw new Exception("Deskey");
            var md5Key = tpSetting.ContainsKey("Md5key") && !string.IsNullOrEmpty(tpSetting["Md5key"]) ? tpSetting["Md5key"] : throw new Exception("Md5key");
            
            var current = new DateTimeOffset(DateTime.UtcNow);
            var timestamp = current.ToUnixTimeMilliseconds();

            var param = new
            {
                s = 8,
                account = tpCustInfo.TpUserName,
            };
            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(param));
            var sParam = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());

            var kickUser = new
            {
                agent,
                timestamp,
                param = CommonExtension.AesEncrypt(sParam, desKey),
                key = CommonExtension.ToMD5($"{agent}{timestamp}{md5Key}").ToLower()
            };
            dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(kickUser));
            var sTransfer = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());

           
            var result = await HttpHelper.GetAsync<string>(apiUrl, sTransfer);
            var response = result.Result;
            if (CompareExtension.IsJson(response))
            {
                var json = JsonConvert.DeserializeObject<ApiResponse>(response);
                return json.d.code == 0;
            }
            
            return false;
        }

        private async Task<IEnumerable<UserBalance>> CheckUserBalanceAsync(IEnumerable<TpCustInfoModel> tpCustList)
        {
            var groupSiteCustList = tpCustList.GroupBy(x => x.SiteId);
            var balanceInfoList = new List<UserBalance>();

            foreach (var groupSite in groupSiteCustList)
            {
                var tpSetting = GetThirdPartyModel(groupSite.Key).TpSetting;
                var apiUrl = tpSetting.ContainsKey("ApiUrl") && !string.IsNullOrEmpty(tpSetting["ApiUrl"]) ? tpSetting["ApiUrl"] : throw new Exception("ApiUrl");
                var agent = tpSetting.ContainsKey("Agent") && !string.IsNullOrEmpty(tpSetting["Agent"]) ? tpSetting["Agent"] : throw new Exception("Agent");
                var desKey = tpSetting.ContainsKey("Deskey") && !string.IsNullOrEmpty(tpSetting["Deskey"]) ? tpSetting["Deskey"] : throw new Exception("Deskey");
                var md5Key = tpSetting.ContainsKey("Md5key") && !string.IsNullOrEmpty(tpSetting["Md5key"]) ? tpSetting["Md5key"] : throw new Exception("Md5key");

                foreach (var custInfo in groupSite)
                {
                    var current = new DateTimeOffset(DateTime.UtcNow);
                    var timestamp = current.ToUnixTimeMilliseconds();

                    var param = new
                    {
                        s = 7,
                        account = custInfo.TpUserName,
                    };
                    var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(param));
                    var sParam = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());

                    var balance = new
                    {
                        agent,
                        timestamp,
                        param = CommonExtension.AesEncrypt(sParam, desKey),
                        key = CommonExtension.ToMD5($"{agent}{timestamp}{md5Key}").ToLower()
                    };
                    dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(balance));
                    var sBalance = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());
                    
                    var result = await HttpHelper.GetAsync<string>(apiUrl, sBalance);
                    var response = result.Result;
                    if (CompareExtension.IsJson(response))
                    {
                        var oApiResult = new UserBalance();
                        var json = JsonConvert.DeserializeObject<ApiResponse>(response);
                        if (json.d.code == 0)
                        {
                            oApiResult.TpUserName = custInfo.TpUserName;
                            oApiResult.balance = json.d.freeMoney; // 可下分余额
                            balanceInfoList.Add(oApiResult);
                        }
                    }
                }
            }

            return balanceInfoList;
        }
        
        private async Task<UserBalance> CheckUserBalanceAsync(TpCustInfoModel custInfo)
        {
            var tpSetting = GetThirdPartyModel(custInfo.SiteId).TpSetting;
            var apiUrl = tpSetting.ContainsKey("ApiUrl") && !string.IsNullOrEmpty(tpSetting["ApiUrl"]) ? tpSetting["ApiUrl"] : throw new Exception("ApiUrl");
            var agent = tpSetting.ContainsKey("Agent") && !string.IsNullOrEmpty(tpSetting["Agent"]) ? tpSetting["Agent"] : throw new Exception("Agent");
            var desKey = tpSetting.ContainsKey("Deskey") && !string.IsNullOrEmpty(tpSetting["Deskey"]) ? tpSetting["Deskey"] : throw new Exception("Deskey");
            var md5Key = tpSetting.ContainsKey("Md5key") && !string.IsNullOrEmpty(tpSetting["Md5key"]) ? tpSetting["Md5key"] : throw new Exception("Md5key");
            
            var current = new DateTimeOffset(DateTime.UtcNow);
            var timestamp = current.ToUnixTimeMilliseconds();

            var param = new
            {
                s = 7,
                account = custInfo.TpUserName,
            };
            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(param));
            var sParam = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());

            var balance = new
            {
                agent,
                timestamp,
                param = CommonExtension.AesEncrypt(sParam, desKey),
                key = CommonExtension.ToMD5($"{agent}{timestamp}{md5Key}").ToLower()
            };
            dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(balance));
            var sBalance = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());
                    
            var result = await HttpHelper.GetAsync<string>(apiUrl, sBalance);
            var response = result.Result;
            if (CompareExtension.IsJson(response))
            {
                var oApiResult = new UserBalance();
                var json = JsonConvert.DeserializeObject<ApiResponse>(response);
                if (json.d.code == 0)
                {
                    oApiResult.TpUserName = custInfo.TpUserName;
                    oApiResult.balance = json.d.freeMoney; // 可下分余额
                    return oApiResult;
                }
            }

            return null;
        }
        private async Task<(TransferStatus Status, ApiResponse json , string orderid)> FundTransferAsync(TpCustInfoModel tpCustInfo, TpTransferInfoModel tpTransferInfo, string transId)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = tpSetting.ContainsKey("ApiUrl") && !string.IsNullOrEmpty(tpSetting["ApiUrl"]) ? tpSetting["ApiUrl"] : throw new Exception("ApiUrl");
            var agent = tpSetting.ContainsKey("Agent") && !string.IsNullOrEmpty(tpSetting["Agent"]) ? tpSetting["Agent"] : throw new Exception("Agent");
            var desKey = tpSetting.ContainsKey("Deskey") && !string.IsNullOrEmpty(tpSetting["Deskey"]) ? tpSetting["Deskey"] : throw new Exception("Deskey");
            var md5Key = tpSetting.ContainsKey("Md5key") && !string.IsNullOrEmpty(tpSetting["Md5key"]) ? tpSetting["Md5key"] : throw new Exception("Md5key");
            
            var current = new DateTimeOffset(DateTime.UtcNow);
            var timestamp = current.ToUnixTimeMilliseconds();

            var orderid = $"{agent}{transId}";
            var param = new
            {
                s = 3,
                account = tpCustInfo.TpUserName,
                money = tpTransferInfo.Amount,
                orderid = orderid
            };
            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(param));
            var sParam = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());

            var transfer = new
            {
                agent,
                timestamp,
                param = CommonExtension.AesEncrypt(sParam, desKey),
                key = CommonExtension.ToMD5($"{agent}{timestamp}{md5Key}").ToLower()
            };
            dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(transfer));
            var sTransfer = string.Join("&", dicP.Where(x => x.Value != null).Select(kp => $"{kp.Key}={kp.Value}").ToArray());
                    
            var result = await HttpHelper.GetAsync<string>(apiUrl, sTransfer);
            var response = result.Result;

            var status = TransferStatus.Failed;
            if (CompareExtension.IsJson(response))
            {
                var json = JsonConvert.DeserializeObject<ApiResponse>(response);
                if (json.d.code == 0)
                {
                    status = TransferStatus.Success;
                    return (status, json ,orderid);
                }
                if (json.d.code == 27)
                {
                    status = TransferStatus.Pending;
                    return (status, json ,orderid);
                }

                status = TransferStatus.Failed;
                return (status, json ,orderid);
            }
            
            return (status, null ,null);
        }
        
        private void CommitTransfer(TpCustInfoModel tpCustInfo,string transId,decimal transferAmount,TransferStatus transferStatus,string thirdId)
        {
            var m = Regex.Match(transId, @"\d+");
            var transferId = int.Parse(m.Value);
            var memo = "system transfer: switch to seamless wallet";

            var setTpTransferInfo = new SetTpTransferInfoModel
            {
                TransferId = transferId,
                ThirdId = thirdId,
                ForStatus = TransferStatus.Pending,
                Status = transferStatus,
                Memo = memo
            };

            using var conn = MainDbDataLayer.GetMySqlConnection();
            conn.Open();
            var trans = conn.BeginTransaction();

            try
            {
                var setTransferSuccess = MainDbDataLayer.SetTpTransferInfo(setTpTransferInfo, trans);
                if (setTransferSuccess)
                {
                    if (setTpTransferInfo.Status != TransferStatus.Success)
                    {
                        trans.Commit();
                        return;
                    }

                    var setWalletBalance = new SetWalletBalanceModel
                    {
                        CustId = tpCustInfo.CustId,
                        SiteId = tpCustInfo.SiteId,
                        TpId = this.TpId,
                        UserName = tpCustInfo.UserName,
                        Amount = transferAmount,
                        Memo = memo,
                        RefTransId = transId
                    };

                    var setWalletResult = MainDbDataLayer.SetWalletBalance(setWalletBalance, trans);
                    if (setWalletResult.Success)
                    {
                        trans.Commit();
                        Logger.LogInformation(
                            $"{setWalletBalance.CustId} {setWalletBalance.UserName} AfterAmount: {setWalletResult.AfterAmount}");
                    }
                    else
                    {
                        trans.Rollback();
                        throw new CustomException(
                            $"TransferId: {setTpTransferInfo.TransferId}, CustId:{tpCustInfo.CustId}, SetWalletBalance failed.");
                    }
                }
                else
                {
                    trans.Rollback();
                    throw new CustomException(
                        $"TransferId: {setTpTransferInfo.TransferId}, CustId:{tpCustInfo.CustId} SetTpTransferInfo failed.");
                }
            }
            catch (Exception ex)
            {
                trans.Rollback();
                throw new CustomException(
                    $"TransferId: {setTpTransferInfo.TransferId}, CustId:{tpCustInfo.CustId} ,CommitTpTransfer Exception.", ex);
            }
        }
        
        #endregion
        
        #region common

        private ThirdPartyModel GetThirdPartyModel(int siteId)
            => _thirdPartyModel.TryGetValue(siteId, out var result) ? result : _thirdPartyModel[0];
        
        private class ApiResponse
        {
            public int s { get; set; }
            public string m { get; set; }
            public info d { get; set; }
        }
        
        private class info
        {
            public int code { get; set; }
            public string url { get; set; }
            public decimal totalMoney { get; set; }
            public decimal freeMoney { get; set; }
            public decimal money { get; set; }
            public int status { get; set; }
        }
        
        Dictionary<int, string> ErrorMassage = new()
        {   {1,"TOKEN 丢失"},
            {2,"渠道不存在"},
            {3,"验证时间超时"},
            {4,"验证错误"},
            {5,"渠道白名单错误"},
            {6,"验证字段丢失"},
            {8,"不存在的请求"},
            {15,"渠道验证错误"},
            {16,"数据不存在（当前没有注单）"},
            {20,"账号禁用"},
            {22,"AES 解密失败"},
            {24,"渠道拉取数据超过时间范围"},
            {26,"订单号不存在"},
            {27,"数据库异常"},
            {28,"ip 禁用"},
            {29,"订单号与订单规则不符"},
            {30,"获取玩家在线状态失败"},
            {31,"更新的分数小于或者等于 0 "},
            {32,"更新玩家信息失败"},
            {33,"更新玩家金币失败"},
            {34,"订单重复"},
            {35,"获取玩家信息失败（请调用登录接口创建账号）"},
            {36,"KindID 不存在"},
            {37,"登录瞬间禁止下分，导致下分失败"},
            {38,"余额不足导致下分失败"},
            {39,"禁止同一账号登录带分、上分、下分并发请求，后一个请求被拒"},
            {40,"单次上下分数量不能超过一百亿"},
            {41,"拉取对局汇总统计时间范围有误"},
            {42,"代理被禁用"},
            {43,"拉单过于频繁(两次拉单时间间隔必须大于 1 秒)"},
            {999,"请求失败"},
            {1001,"注册会员账号系统异常"},
            {1002,"代理商金额不足"},
            {1003,"玩家大厅上分/下分异常"},
            {1004,"代理商渠道不存在"},
            {1005,"会员金额不足"},
            {1006,"会员游戏上分/下分异常"},
            {1007,"玩家登录游戏上分异常"},
            {1008,"玩家注销游戏下分异常"},
            {1009,"上下分出现负数(非法值)"},
            {1010,"会员退出大厅异常"},
            {1011,"订单已存在"},
            {1012,"订单号不符合规则"},
            {1013,"后台上分/下分异常"},
            {1014,"上级代理金额不足"},
            {1015,"会员代理充值异常(不能跨代理给会员充值) "},
            {1016,"玩家账户被锁"},
            {1017,"更新会员、浏览器信息异常"},
            {1018,"存储过程参数格式不正确或参数为空"},
            {1019,"更新会员玩过的游戏信息异常"},
            {1020,"玩家大厅进(出)游戏上分(下分)生成订单异常"},
            {1021,"会员订单失效或不存在"},
            {1022,"会员订单已经处理过，不能重复处理"},
            {1023,"玩家正在游戏中，不能上下分"},
            {1024,"大厅会员账号不存在"},
            {1025,"参数错误，子渠道标识不能为空"},
        };
        
        #endregion
        
    }
}
