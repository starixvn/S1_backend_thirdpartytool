using System;
using System.Collections.Generic;
using System.Linq;
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
                .Where(item => item.TpId == TpId).ToDictionary(item => item.SiteId, item => item);

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
                        var tpBalanceInfo = CheckUserBalanceAsync(tpCustInfo).Result;

                        if (tpBalanceInfo.Balance <= 0)
                            return;

                        // if balance < 1, update user min transfer limit
                        if (tpBalanceInfo.Balance < 1)
                        {
                            _ = UpdateMemberAsync(tpCustInfo.TpUserName, tpCustInfo.SiteId).Result;
                        }

                        var tpTransferInfo = new TpTransferInfoModel
                        {
                            TpId = tpCustInfo.TpId,
                            CustId = tpCustInfo.CustId,
                            SiteId = tpCustInfo.SiteId,
                            UserName = tpBalanceInfo.TpUserName,
                            Amount = tpBalanceInfo.Balance,
                            Prefix = TransferPrefix.FT.ToString(),
                            Direction = (int)Direction.ToMain,
                            Memo = "system force transfer: switch to seamless wallet",
                            ThirdId = null,
                            ExtensionInfo = JsonConvert.SerializeObject(tpBalanceInfo) // todo: 看有沒有要改，先暫時這樣
                        };

                        transId = base.CreateTpTransferInfo(tpTransferInfo);
                        var result = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;

                        if (result.Status != TransferStatus.Success)
                            failedList.Add(new TransferBackFailedRecord
                            {
                                CustId = tpCustInfo.CustId,
                                TransId = transId,
                                Message = result.Status.ToString()
                            });

                        // 有殘餘轉不乾淨的金額
                        if (result.RespData.AfterAmount > 0)
                        {
                            remainList.Add(new TransferRemainRecord
                            {
                                SiteId = tpCustInfo.SiteId,
                                CustId = tpCustInfo.CustId,
                                UserName = tpCustInfo.UserName,
                                RemainAmount = result.RespData.AfterAmount
                            });
                        }

                        // todo: call bonus sp? and add money to wallet
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
                    try
                    {
                        if (!string.IsNullOrEmpty(retryItem.TransId))
                        {
                            var transferStatus = CheckFundTransferAsync(retryItem.TransId, retryItem.TpCustInfo.SiteId).Result;
                            // todo: check transfer
                            switch (transferStatus)
                            {
                                case TransferStatus.Success:
                                    // todo: commit?
                                    break;
                                case TransferStatus.Failed:
                                    // todo: commit and trans new
                                    break;
                                case TransferStatus.Pending:
                                    // todo: add to failed list
                                    return;
                            }
                        }


                       /* var tpBalanceInfo = CheckUserBalanceAsync(retryItem).Result;

                        if (tpBalanceInfo.Balance <= 0)
                            return;

                        // if balance < 1, update user min transfer limit
                        if (tpBalanceInfo.Balance < 1)
                        {
                            _ = UpdateMemberAsync(retryItem.TpUserName, retryItem.SiteId).Result;
                        }

                        var tpTransferInfo = new TpTransferInfoModel
                        {
                            TpId = retryItem.TpId,
                            CustId = retryItem.CustId,
                            SiteId = retryItem.SiteId,
                            UserName = tpBalanceInfo.TpUserName,
                            Amount = tpBalanceInfo.Balance,
                            Prefix = TransferPrefix.FT.ToString(),
                            Direction = (int)Direction.ToMain,
                            Memo = "system force transfer: switch to seamless wallet",
                            ThirdId = null,
                            ExtensionInfo = JsonConvert.SerializeObject(tpBalanceInfo) // todo: 看有沒有要改，先暫時這樣
                        };

                        transId = base.CreateTpTransferInfo(tpTransferInfo);
                        var result = FundTransferAsync(retryItem, tpTransferInfo, transId).Result;

                        if (result.Status != TransferStatus.Success)
                            failedList.Add(new TransferBackFailedRecord
                            {
                                CustId = retryItem.CustId,
                                TransId = transId,
                                Message = result.Status.ToString()
                            });

                        // 有殘餘轉不乾淨的金額
                        if (result.RespData.AfterAmount > 0)
                        {
                            remainList.Add(new TransferRemainRecord
                            {
                                SiteId = retryItem.SiteId,
                                CustId = retryItem.CustId,
                                UserName = retryItem.UserName,
                                RemainAmount = result.RespData.AfterAmount
                            });
                        }*/
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

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_member_id", tpCustInfo.TpUserName },
                { "vendor_trans_id", transId },
                { "amount ", tpTransferInfo.Amount.ToString("##.###") },
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

        private async Task<TransferStatus> CheckFundTransferAsync(string transId, int siteId)
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

            return status;
        }

        private async Task<bool> UpdateMemberAsync(string tpUserName, int siteId)
        {
            var tpSetting = GetThirdPartyModel(siteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.CheckFundTransfer;

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_Member_ID ", tpUserName },
                { "maxTransfer", int.MaxValue.ToString() },
                { "minTransfer", "0.01" },
                { "oddstype", "1" }
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, UpdateUserResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba UpdateMember failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}, TpUserName: {tpUserName}");
            }

            var result = response.Result;
            if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba UpdateMember failed: ({result.ErrorCode}) {result.Message}");
            }

            return true;
        }

        private ThirdPartyModel GetThirdPartyModel(int siteId)
            => _thirdPartyModel.TryGetValue(siteId, out var result) ? result : _thirdPartyModel[0];
    }
}