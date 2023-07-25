using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.Models.Mega;
using ThirdPartyTool.Core.Models.PT;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Core.ThirdParty
{
    internal class PTTpHelper : BaseTpHelper, ITpHelper
    {
        private readonly string storagePath = Path.Combine("", "Storage", "PT");
        private readonly Dictionary<int, ThirdPartyModel> _thirdPartyModel;
        private readonly int[] TransferPendingErrorCode = { 71, 72, 73, 301 };
        private readonly string[] CheckTransferFailStatus = { "declined", "notallowed", "missing" };

        #region Constructors

        public PTTpHelper(IServiceProvider provider) : base(provider)
        {
            _thirdPartyModel = provider.GetRequiredService<IEnumerable<ThirdPartyModel>>()
               .Where(item => item.TpId == TpId)
               .ToDictionary(item => item.SiteId, item => item);
        }

        #endregion

        public int TpId => 27;

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
            var tpBalanceInfoList = new List<PTBalanceResponse>();

            // Step 1: 取得餘額
            try
            {
                tpBalanceInfoList = CheckUserBalanceAsync(todoList).Result.ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                failedList = todoList
                    .Select(x => new TransferBackFailedRecord(x.CustId, string.Empty, "Get PT user balance failed."))
                    .ToList();

                return (failedList, remainList);
            }

            Parallel.ForEach(todoList,
                new ParallelOptions { MaxDegreeOfParallelism = taskCount },
                tpCustInfo =>
                {
                    var transId = string.Empty;
                    try
                    {
                        var tpBalanceInfo = tpBalanceInfoList.FirstOrDefault(x => x.playername == tpCustInfo.TpUserName);

                        if (tpBalanceInfo == null)
                            throw new CustomException($"{tpCustInfo.CustId} {tpCustInfo.TpUserName} TP balance info is null.");

                        if (tpBalanceInfo.balance <= 0 || tpCustInfo.IsTest)
                            return;

                        // Step 2: 轉回餘額
                        var transferAmount = tpBalanceInfo.balance.ToOurAmount(tpCustInfo.ExchangeRate);
                        var jsonTpBalanceInfo = JsonConvert.SerializeObject(tpBalanceInfo);
                        var tpTransferInfo = new TpTransferInfoModel(tpCustInfo, transferAmount, jsonTpBalanceInfo);

                        transId = CreateTpTransferInfo(tpTransferInfo);
                        var (status, resp) = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;

                        // 有殘餘轉不乾淨的金額
                        if (resp.SuccessResult?.currentplayerbalance > 0)
                            remainList.Add(new TransferRemainRecord(tpCustInfo, resp.SuccessResult.currentplayerbalance));

                        // 轉帳失敗不 Commit，Retry 檢查單號的時候再做
                        if (status != TransferStatus.Success)
                        {
                            var msg = !string.IsNullOrWhiteSpace(resp.ErrorResult.error)
                                ? $"Error Result：{resp.ErrorResult.errorcode} {resp.ErrorResult.error}"
                                : status.ToString();

                            failedList.Add(new TransferBackFailedRecord(tpCustInfo.CustId, transId, msg));

                            return;
                        }

                        // Step 3: 更新轉帳結果
                        CommitTransfer(tpCustInfo, transId, transferAmount, status,
                            resp.SuccessResult?.kiosktransactionid ?? resp.SuccessResult?.ptinternaltransactionid);

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
                            var checkResult = CheckFundTransferAsync(tpCustInfo, retryItem.TransId).Result;

                            switch (checkResult.Status)
                            {
                                case TransferStatus.Success:
                                    transferAmount = checkResult.Resp.SuccessResult.amount.ToOurAmount(tpCustInfo.ExchangeRate);
                                    CommitTransfer(tpCustInfo, retryItem.TransId, transferAmount, checkResult.Status,
                                        checkResult.Resp.SuccessResult?.kiosktransactionid ?? checkResult.Resp.SuccessResult?.ptinternaltransactionid);
                                    return;
                                case TransferStatus.Failed:
                                    CommitTransfer(tpCustInfo, retryItem.TransId, transferAmount, checkResult.Status,
                                        checkResult.Resp.SuccessResult?.kiosktransactionid ?? checkResult.Resp.SuccessResult?.ptinternaltransactionid);
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
                        // Step 1: 取得餘額
                        var tpBalanceInfo = CheckUserBalanceAsync(new List<TpCustInfoModel> { tpCustInfo })
                            .Result
                            .FirstOrDefault();

                        if (tpBalanceInfo == null)
                            throw new CustomException($"{tpCustInfo.CustId} {tpCustInfo.TpUserName} TP balance info is null.");

                        if (tpBalanceInfo.balance <= 0 || tpCustInfo.IsTest)
                            return;

                        // Step 2: 轉回餘額
                        transferAmount = tpBalanceInfo.balance.ToOurAmount(tpCustInfo.ExchangeRate);
                        var jsonTpBalanceInfo = JsonConvert.SerializeObject(tpBalanceInfo);
                        var tpTransferInfo = new TpTransferInfoModel(tpCustInfo, transferAmount, jsonTpBalanceInfo);

                        transId = CreateTpTransferInfo(tpTransferInfo);
                        var (status, resp) = FundTransferAsync(tpCustInfo, tpTransferInfo, transId).Result;

                        // 有殘餘轉不乾淨的金額
                        if (resp.SuccessResult?.currentplayerbalance > 0)
                            remainList.Add(new TransferRemainRecord(tpCustInfo, resp.SuccessResult.currentplayerbalance));

                        if (status != TransferStatus.Success)
                        {
                            var msg = !string.IsNullOrWhiteSpace(resp.ErrorResult.error)
                                ? $"Error Result：{resp.ErrorResult.errorcode} {resp.ErrorResult.error}"
                                : status.ToString();

                            failedList.Add(new TransferBackFailedRecord(tpCustInfo.CustId, transId, msg));

                            return;
                        }

                        // Step 3: 更新轉帳結果
                        CommitTransfer(tpCustInfo, transId, transferAmount, status,
                            resp.SuccessResult?.kiosktransactionid ?? resp.SuccessResult?.ptinternaltransactionid);

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

        #region Private Methods

        private async Task<bool> KickUserAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var currency = tpCustInfo.TpCurrencyId;

            var apiUrl = GetApiUrl(tpSetting) + UrlPath.Logout;
            var entityKey = GetEntityKey(tpSetting, currency);

            var header = GetHeader(entityKey);
            var certificate = GetCertificate(currency);

            var request = new
            {
                playername = tpCustInfo.TpUserName
            };

            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(request));

            var (result, statusCode) = await HttpHelper.HttpPostWithX509Async(
                apiUrl,
                dicP,
                "application/x-www-form-urlencoded",
                header,
                certificate
            );

            if (!statusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"PT KickUser failed, HttpStatus: {(int)statusCode} {statusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            if (CompareExtension.IsJson(result))
            {
                var errorResp = JsonConvert.DeserializeObject<PTErrorResponse>(result);
                if (!string.IsNullOrWhiteSpace(errorResp.error))
                    throw new CustomException($"PT KickUser failed: ({errorResp.errorcode}) {errorResp.error}");
            }
            else
                throw new CustomException("PT KickUser failed: Invalid JSON string, Cannot DeserializeObject.");

            return true;
        }

        private async Task<IEnumerable<PTBalanceResponse>> CheckUserBalanceAsync(IEnumerable<TpCustInfoModel> tpCustList)
        {
            var groupTpCustList = tpCustList.GroupBy(x => x.SiteId);
            var balanceInfoList = new List<PTBalanceResponse>();

            foreach (var group in groupTpCustList)
            {
                if (!group.Any()) continue;

                var tpSetting = GetThirdPartyModel(group.Key).TpSetting;
                var tpCustInfoList = tpCustList.Where(x => x.SiteId == group.Key);

                foreach (var tpCustInfo in tpCustInfoList)
                {
                    var currency = tpCustInfo.TpCurrencyId;

                    var apiUrl = GetApiUrl(tpSetting) + UrlPath.Balance;
                    var entityKey = GetEntityKey(tpSetting, currency);
                    var header = GetHeader(entityKey);
                    var certificate = GetCertificate(currency);

                    var request = new
                    {
                        playername = tpCustInfo.TpUserName
                    };

                    var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(request));

                    var (result, statusCode) = await HttpHelper.HttpPostWithX509Async(
                        apiUrl,
                        dicP,
                        "application/x-www-form-urlencoded",
                        header,
                        certificate
                    );

                    if (!statusCode.IsSuccessStatus())
                    {
                        throw new CustomException(
                            $"PT CheckUserBalanceData failed, HttpStatus: {(int)statusCode} {statusCode}" +
                            $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
                    }

                    if (CompareExtension.IsJson(result))
                    {
                        var errorResp = JsonConvert.DeserializeObject<PTErrorResponse>(result);
                        if (!string.IsNullOrWhiteSpace(errorResp.error))
                            throw new CustomException($"PT CheckUserBalanceData failed: ({errorResp.errorcode}) {errorResp.error}");
                        else
                        {
                            var balanceResp = JsonConvert.DeserializeObject<PTSuccessResponse<PTBalanceResponse>>(result);
                            balanceInfoList.Add(balanceResp.result);
                        }
                    }
                    else
                        throw new CustomException("PT CheckUserBalanceData failed: Invalid JSON string, Cannot DeserializeObject.");
                }
            }
            return balanceInfoList;
        }

        private async Task<(TransferStatus Status, PTTransferResponse Resp)> FundTransferAsync(TpCustInfoModel tpCustInfo, TpTransferInfoModel tpTransferInfo, string transId)
        {
            var currency = tpCustInfo.TpCurrencyId;

            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = GetApiUrl(tpSetting) + UrlPath.Withdraw;
            var entityKey = GetEntityKey(tpSetting, currency);
            var header = GetHeader(entityKey);
            var certificate = GetCertificate(currency);
            var adminName = GetAdminName(tpSetting, currency);
            var amount = tpTransferInfo.Amount.ToTpAmount(tpCustInfo.ExchangeRate);

            var request = new
            {
                playername = tpCustInfo.TpUserName,
                amount = Math.Floor(amount * 100) / 100,
                adminname = adminName,
                isForce = false,
                externaltranid = transId,
                losebonus = false
            };

            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(request));

            var (result, statusCode) = await HttpHelper.HttpPostWithX509Async(
                apiUrl,
                dicP,
                "application/x-www-form-urlencoded",
                header,
                certificate
            );

            if (!statusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"PT FundTransfer failed, HttpStatus: {(int)statusCode} {statusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            if (CompareExtension.IsJson(result))
            {
                var errorResp = JsonConvert.DeserializeObject<PTErrorResponse>(result);
                //Test When Error Happen Will Write in Faild File
                //errorResp = new PTErrorResponse() { error = "error test", errorcode = -1 };
                if (!string.IsNullOrWhiteSpace(errorResp.error))
                {
                    if (TransferPendingErrorCode.Contains(errorResp.errorcode))
                        return (TransferStatus.Pending, new PTTransferResponse(errorResp, null));
                    else if (errorResp.errorcode == 97) //97 商戶餘額不足，玩家轉帳金額 > 商戶餘額
                    {
                        errorResp.error = $"{errorResp.error} {GetAlertMessage(tpCustInfo, Math.Floor(amount * 100) / 100)}";
                        return (TransferStatus.Failed, new PTTransferResponse(errorResp, null));
                    }
                    else
                        return (TransferStatus.Failed, new PTTransferResponse(errorResp, null));
                }
                else
                {
                    var withdrawResp = JsonConvert.DeserializeObject<PTSuccessResponse<PTWithdrawResponse>>(result);
                    return (TransferStatus.Success, new PTTransferResponse(errorResp, withdrawResp.result));
                }
            }
            else
                throw new CustomException("PT FundTransfer failed: Invalid JSON string, Cannot DeserializeObject.");
        }

        private async Task<(TransferStatus Status, PTCheckFTransferResponse Resp)> CheckFundTransferAsync(TpCustInfoModel tpCustInfo, string transId)
        {
            var currency = tpCustInfo.TpCurrencyId;
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = GetApiUrl(tpSetting) + UrlPath.CheckTransaction;
            var entityKey = GetEntityKey(tpSetting, currency);
            var header = GetHeader(entityKey);
            var certificate = GetCertificate(currency);

            var checkTransfer = new
            {
                externaltransactionid = transId,
            };

            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(checkTransfer));

            var (result, statusCode) = await HttpHelper.HttpPostWithX509Async(
                apiUrl,
                dicP,
                "application/x-www-form-urlencoded",
                header,
                certificate
            );

            if (!statusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"PT CheckFundTransfer failed, HttpStatus: {(int)statusCode} {statusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            if (CompareExtension.IsJson(result))
            {
                var errorResp = JsonConvert.DeserializeObject<PTErrorResponse>(result);
                if (!string.IsNullOrWhiteSpace(errorResp.error))
                {
                    return (TransferStatus.Failed, new PTCheckFTransferResponse(errorResp, null));
                }
                else
                {
                    var checkTransactionResp = JsonConvert.DeserializeObject<PTSuccessResponse<PTCheckTransactionResponse>>(result);

                    if (checkTransactionResp.result.status == "approved")
                        return (TransferStatus.Success, new PTCheckFTransferResponse(errorResp, checkTransactionResp.result));
                    else if (CheckTransferFailStatus.Contains(checkTransactionResp.result.status))
                        return (TransferStatus.Failed, new PTCheckFTransferResponse(errorResp, checkTransactionResp.result));

                    return (TransferStatus.Pending, new PTCheckFTransferResponse(errorResp, checkTransactionResp.result));
                }
            }
            else
                throw new CustomException("PT CheckFundTransfer failed: Invalid JSON string, Cannot DeserializeObject.");
        }

        public void CommitTransfer(TpCustInfoModel tpCustInfo,
            string transId,
            decimal transferAmount,
            TransferStatus transferStatus,
            string thirdId)
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

        #region private method

        private ThirdPartyModel GetThirdPartyModel(int siteId)
            => _thirdPartyModel.TryGetValue(siteId, out var result) ? result : _thirdPartyModel[0];

        /// <summary>
        /// 取得X509Certificate2憑證
        /// </summary>
        /// <param name="currency"></param>
        /// <returns></returns>
        private X509Certificate2 GetCertificate(string currency)
        {
            var pem = Path.Combine(storagePath, "PEM", $"{currency}.pem");
            var key = Path.Combine(storagePath, "PEM", $"{currency}.key");
            var cert = X509Certificate2.CreateFromPemFile(pem, key);
            return new X509Certificate2(cert.Export(X509ContentType.Pfx));
        }

        /// <summary>
        /// 取得Http標頭
        /// </summary>
        /// <param name="entityKey"></param>
        /// <returns></returns>
        private static Dictionary<string, string> GetHeader(string entityKey)
        {
            var header = new Dictionary<string, string> { { "X_ENTITY_KEY", entityKey } };
            return header;
        }

        private static string GetEntityKey(Dictionary<string, string> inputs, string currency)
        {
            return inputs.ContainsKey($"XEntityKey_{currency}") && !string.IsNullOrEmpty(inputs[$"XEntityKey_{currency}"]) ? inputs[$"XEntityKey_{currency}"] : throw new Exception($"XEntityKey_{currency}");
        }

        private static string GetAdminName(Dictionary<string, string> inputs, string currency)
        {
            return inputs.ContainsKey($"AdminName_{currency}") && !string.IsNullOrEmpty(inputs[$"AdminName_{currency}"]) ? inputs[$"AdminName_{currency}"] : throw new Exception($"AdminName_{currency}");
        }

        private static string GetApiUrl(Dictionary<string, string> inputs)
        {
            return inputs.ContainsKey("ApiUrl") && !string.IsNullOrEmpty(inputs["ApiUrl"]) ? inputs["ApiUrl"] : throw new Exception("ApiUrl");
        }

        private string GetAlertMessage(TpCustInfoModel tpCustInfo, decimal userTransferAmount)
        {
            var tpIdMapToSiteName = new Dictionary<int, string>()
            {
                { 1, "NOVA88" },{ 2, "GOAL123" },{ 3, "BWING" }
            };

            var message = $"\r\n" +
                $"=======================\r\n" +
                $"User Transfer Balance more than the Merchant Balance. Please Add Balance.\r\n" +
                $"TpUserName：{tpCustInfo.TpUserName}\r\n" +
                $"TransferBalance：{userTransferAmount}\r\n" +
                $"Site：{tpIdMapToSiteName[tpCustInfo.SiteId]}\r\n" +
                $"TPName : PlayTech \r\n" +
                $"MerchantName : {tpIdMapToSiteName[tpCustInfo.SiteId]}{tpCustInfo.TpCurrencyId}\r\n" +
                $"Currency:{tpCustInfo.TpCurrencyId}";
            return message;
        }

        #endregion
    }
}
