using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.Models.Saba;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Core.ThirdParty
{
    public class SabaTpHelper : BaseTpHelper, ITpHelper
    {
        public int TpId => 41;

        private readonly Dictionary<int, ThirdPartyModel> _thirdPartyModel;
        private const string WalletId = "1"; // 1: Sportsbook/ 5: AG

        public SabaTpHelper(IServiceProvider provider) : base(provider)
        {
            _thirdPartyModel = provider.GetRequiredService<IEnumerable<ThirdPartyModel>>()
                .Where(item => item.TpId == TpId)
                .ToDictionary(item => item.SiteId, item => item);

        }

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
                new ParallelOptions() { MaxDegreeOfParallelism = taskCount },
                tpCustInfo =>
                {
                    var transId = string.Empty;
                    try
                    {
                        // Step 1: 取得沙巴餘額
                        var tpBalanceInfo = CheckUserBalanceAsync(tpCustInfo).Result;

                        if (tpBalanceInfo.Balance <= 0)
                            return;

                        // Step 2: 餘額 < 1 時調整轉帳下限
                        if (tpBalanceInfo.Balance < 1)
                        {
                            _ = UpdateMemberAsync(tpCustInfo, tpCustInfo.SiteId).Result;
                        }

                        // Step 3: 轉回沙巴餘額
                        var transferAmount = ToOurAmount(tpBalanceInfo.Balance, tpCustInfo.ExchangeRate);
                        var jsonTpBalanceInfo = JsonConvert.SerializeObject(tpBalanceInfo);
                        var tpTransferInfo = new TpTransferInfoModel(tpCustInfo, transferAmount, jsonTpBalanceInfo);
                        
                        transId = base.CreateTpTransferInfo(tpTransferInfo);
                        var (status, respData) = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;

                        // 有殘餘轉不乾淨的金額
                        if (respData.AfterAmount > 0)
                        {
                            remainList.Add(new TransferRemainRecord
                            {
                                SiteId = tpCustInfo.SiteId,
                                CustId = tpCustInfo.CustId,
                                UserName = tpCustInfo.UserName,
                                RemainAmount = ToOurAmount(respData.AfterAmount, tpCustInfo.ExchangeRate)
                            });
                        }

                        // 轉帳失敗不 Commit，Retry 檢查單號的時候再做
                        if (status != TransferStatus.Success)
                        {
                            failedList.Add(new TransferBackFailedRecord
                            {
                                CustId = tpCustInfo.CustId,
                                TransId = transId,
                                Message = status.ToString()
                            });

                            return;
                        }
                        
                        // Step 4: 更新轉帳結果
                        CommitTransfer(tpCustInfo, transId, transferAmount, status, respData.TransId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                        failedList.Add(new TransferBackFailedRecord
                        {
                            CustId = tpCustInfo.CustId,
                            TransId = transId,
                            Message = ex.Message
                        });
                    }
                });

            return (failedList, remainList);
        }
        
        public (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords) ParallelTransferRetry(
            IEnumerable<TransferBackRetryModel> todoList, int taskCount)
        {
            var failedList = new List<TransferBackFailedRecord>();
            var remainList = new List<TransferRemainRecord>();

            Parallel.ForEach(todoList,
                new ParallelOptions() { MaxDegreeOfParallelism = taskCount },
                retryItem =>
                {
                    var transId = string.Empty;
                    var tpCustInfo = retryItem.TpCustInfo;
                    var transferAmount = 0m;
                    try
                    {
                        if (!string.IsNullOrEmpty(retryItem.TransId))
                        {
                            var checkResult = CheckFundTransferAsync(retryItem.TransId, tpCustInfo.SiteId).Result;
                            
                            switch (checkResult.Status)
                            {
                                case TransferStatus.Success:
                                    transferAmount = ToOurAmount(checkResult.RespData.Amount, this.TpId);
                                    CommitTransfer(tpCustInfo, retryItem.TransId, transferAmount, checkResult.Status, checkResult.RespData.TransId);
                                    return;
                                case TransferStatus.Failed:
                                    CommitTransfer(tpCustInfo, retryItem.TransId, transferAmount, checkResult.Status, checkResult.RespData.TransId);
                                    break;
                                case TransferStatus.Pending:
                                    failedList.Add(new TransferBackFailedRecord
                                    {
                                        CustId = retryItem.CustId,
                                        Message = checkResult.Status.ToString(),
                                        TransId = retryItem.TransId
                                    });
                                    return;
                            }
                        }

                        // 進行轉帳重送
                        // Step 1: 取得沙巴餘額
                        var tpBalanceInfo = CheckUserBalanceAsync(tpCustInfo).Result;

                        if (tpBalanceInfo.Balance <= 0)
                            return;

                        // Step 2: 餘額 < 1 時調整轉帳下限
                        if (tpBalanceInfo.Balance < 1)
                        {
                            _ = UpdateMemberAsync(tpCustInfo, tpCustInfo.SiteId).Result;
                        }

                        // Step 3: 轉回沙巴餘額
                        transferAmount = ToOurAmount(tpBalanceInfo.Balance, tpCustInfo.ExchangeRate);
                        var jsonTpBalanceInfo = JsonConvert.SerializeObject(tpBalanceInfo);
                        var tpTransferInfo = new TpTransferInfoModel(tpCustInfo, transferAmount, jsonTpBalanceInfo);

                        transId = base.CreateTpTransferInfo(tpTransferInfo);
                        var (status, respData) = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;
                        
                        if (respData.AfterAmount > 0)
                        {
                            remainList.Add(new TransferRemainRecord
                            {
                                SiteId = tpCustInfo.SiteId,
                                CustId = tpCustInfo.CustId,
                                UserName = tpCustInfo.UserName,
                                RemainAmount = ToOurAmount(respData.AfterAmount, tpCustInfo.ExchangeRate)
                            });
                        }
                        
                        if (status != TransferStatus.Success)
                        {
                            failedList.Add(new TransferBackFailedRecord
                            {
                                CustId = tpCustInfo.CustId,
                                TransId = transId,
                                Message = status.ToString()
                            });

                            return;
                        }

                        // Step 4: 更新轉帳結果
                        CommitTransfer(tpCustInfo, transId, transferAmount, status, respData.TransId);

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, ex.Message);
                        failedList.Add(new TransferBackFailedRecord
                        {
                            CustId = retryItem.CustId,
                            TransId = transId,
                            Message = ex.Message
                        });
                    }
                });

            return (failedList, remainList);
        }

        #region Private

        private async Task<bool> KickUserAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.KickUser;

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_member_id", tpCustInfo.TpUserName }
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, KickUserResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba KickUser failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            var result = response.Result;
            if (!SabaDefinition.KickUserSuccess(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba KickUser failed: ({result.ErrorCode}) {result.Message}");
            }

            return true;
        }

        private async Task<CheckUserBalanceData> CheckUserBalanceAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.CheckUserBalance;

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_member_id", tpCustInfo.TpUserName },
                { "wallet_id", WalletId },
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, CheckUserBalanceResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba CheckUserBalanceData failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            var result = response.Result;
            if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba CheckUserBalanceData failed: ({result.ErrorCode}) {result.Message}");
            }

            return result.Data.FirstOrDefault();
        }

        private async Task<(TransferStatus Status, FundTransferRespData RespData)> FundTransferAsync(TpCustInfoModel tpCustInfo, TpTransferInfoModel tpTransferInfo, string transId)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.FundTransfer;
            var amount = ToTpAmount(tpTransferInfo.Amount, tpCustInfo.ExchangeRate);

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_member_id", tpCustInfo.TpUserName },
                { "vendor_trans_id", transId },
                { "amount ", amount.ToString("##.###") },
                { "currency", tpCustInfo.TpCurrencyId },
                { "direction", ((int)Direction.ToMain).ToString() },
                { "wallet_id ", WalletId }
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, FundTransferResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba FundTransfer failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            var result = response.Result;
            if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba FundTransfer failed: ({result.ErrorCode}) {result.Message}");
            }

            var status = SabaDefinition.GetTransferStatus(result.Data.Status);

            return (status, result.Data);
        }

        private async Task<(TransferStatus Status, CheckFundTransferRespData RespData)> CheckFundTransferAsync(string transId, int siteId)
        {
            var tpSetting = GetThirdPartyModel(siteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.CheckFundTransfer;

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_trans_id", transId },
                { "wallet_id ", WalletId }
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, CheckFundTransferResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba CheckFundTransfer failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}, TransId: {transId}");
            }

            var result = response.Result;
            if (SabaDefinition.CheckTransferErrorErrorCode.Contains(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba CheckFundTransfer failed: ({result.ErrorCode}) {result.Message}");
            }

            var status = SabaDefinition.GetCheckTransferStatus(result.ErrorCode, result.Data.Status);

            return (status, result.Data);
        }

        private async Task<bool> UpdateMemberAsync(TpCustInfoModel tpCustInfo, int siteId)
        {
            var tpSetting = GetThirdPartyModel(siteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.CheckFundTransfer;
            var minTransfer = tpCustInfo.ExchangeRate == 1000 ? "0.001" : "0.01";

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_Member_ID ", tpCustInfo.TpUserName },
                { "maxTransfer", int.MaxValue.ToString() },
                { "minTransfer", minTransfer },
                { "oddstype", "1" }
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, UpdateUserResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba UpdateMember failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}, TpUserName: {tpCustInfo.TpUserName}");
            }

            var result = response.Result;
            if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba UpdateMember failed: ({result.ErrorCode}) {result.Message}");
            }

            return true;
        }

        public void CommitTransfer(TpCustInfoModel tpCustInfo, string transId, decimal transferAmount, TransferStatus transferStatus, string thirdId)
        {
            var m = Regex.Match(transId, @"\d+");
            var transferId = int.Parse(m.Value);
            var setTpTransferInfo = new SetTpTransferInfoModel
            {
                TransferId = transferId,
                ThirdId = thirdId,
                ForStatus = TransferStatus.Pending,
                Status = transferStatus
            };

            var setWalletBalance = new SetWalletBalanceModel
            {
                CustId = tpCustInfo.CustId,
                SiteId = tpCustInfo.SiteId,
                TpId = this.TpId,
                UserName = tpCustInfo.UserName,
                Amount = transferAmount,
                Memo = "system: saba switch to seamless wallet",
                RefTransId = transId
            };

            using var conn = MainDbDataLayer.GetMySqlConnection();
            conn.Open();
            var trans = conn.BeginTransaction();

            try
            {
                var setTransferSuccess = MainDbDataLayer.SetTpTransferInfo(setTpTransferInfo, trans);
                if (setTransferSuccess && setTpTransferInfo.Status == TransferStatus.Success)
                {
                    // 轉移進行中紅利至單一錢包，轉帳金額須扣除紅利金額
                    var bonusAmount = MainDbDataLayer.SetSabaBonus(setWalletBalance.SiteId, setWalletBalance.RefTransId, trans);
                    setWalletBalance.Amount -= bonusAmount;

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
                            $"TransferId: {setTpTransferInfo.TransferId}, CustId:{setWalletBalance.CustId}, SetWalletBalance failed.");
                    }
                }
                else
                {
                    trans.Rollback();
                    throw new CustomException(
                        $"TransferId: {setTpTransferInfo.TransferId}, CustId:{setWalletBalance.CustId} SetTpTransferInfo failed.");
                }
            }
            catch (Exception ex)
            {
                trans.Rollback();
                throw new CustomException(
                    $"TransferId: {setTpTransferInfo.TransferId}, CustId:{setWalletBalance.CustId} ,CommitTpTransfer Exception.", ex);
            }
        }

        private decimal ToOurAmount(decimal amount, decimal exchangeRate)
        {
            if (exchangeRate > 1)
            {
                return amount * exchangeRate;
            }

            return amount;
        }

        private decimal ToTpAmount(decimal amount, decimal exchangeRate)
        {
            if (exchangeRate > 1)
            {
                return amount / exchangeRate;
            }

            return amount;
        }

        private ThirdPartyModel GetThirdPartyModel(int siteId)
            => _thirdPartyModel.TryGetValue(siteId, out var result) ? result : _thirdPartyModel[0];

        #endregion
    }
}