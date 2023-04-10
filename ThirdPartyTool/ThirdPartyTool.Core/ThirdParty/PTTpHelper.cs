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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.Models.PT;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Core.ThirdParty
{
    internal class PTTpHelper : BaseTpHelper, ITpHelper
    {
        private readonly string storagePath = Path.Combine(ContentRootPath, "Storage", "PT");
        private readonly Dictionary<int, ThirdPartyModel> _thirdPartyModel;
        private const string WalletId = "1"; // 1: Sportsbook/ 5: AG

        #region Constructors

        public PTTpHelper(IServiceProvider provider) : base(provider)
        {
            _thirdPartyModel = provider.GetRequiredService<IEnumerable<ThirdPartyModel>>()
               .Where(item => item.TpId == TpId)
               .ToDictionary(item => item.SiteId, item => item);
        }

        #endregion

        public int TpId => 27;

        /// <summary>
        /// No Use
        /// </summary>
        /// <param name="todoList"></param>
        /// <param name="taskCount"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public IEnumerable<UserBalance> GetBalance(IEnumerable<TpCustInfoModel> todoList, int taskCount)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords) ParallelTransferRetry(
            IEnumerable<TransferBackRetryModel> todoList, int taskCount)
        {
            throw new NotImplementedException();
        }

        #region Private Methods

        private async Task<bool> KickUserAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var currency = tpCustInfo.TpCurrencyId;

            var apiUrl = GetApiUrl(tpSetting) + UrlPath.Logout;
            var entityKey = GetEntityKey(tpSetting, currency);
            var pass = GetPass(tpSetting, currency);

            var header = GetHeader(entityKey);
            var certificate = GetCertificate(currency);

            var balance = new
            {
                playername = tpCustInfo.TpUserName
            };

            var dicP = JsonConvertExtension.FromJson<Dictionary<string, string>>(JsonConvertExtension.ToJson(balance));

            var result = await HttpHelper.HttpPostWithX509Async(
                apiUrl,
                dicP,
                "application/x-www-form-urlencoded",
                header,
                certificate
            );

            if (CompareExtension.IsJson(result))
            {
                var error = JsonConvert.DeserializeObject<ApiErrorResponseModel>(result);
                if (error.error != null)
                {
                    //oApiResult.ErrorCode = ErrorCode.Fail;
                    //oApiResult.RawMessage = error.error ?? "";
                    //oApiResult.RawErrorCode = error.errorcode.ToString() ?? "";
                }
                else
                {
                    //var json = JsonConvert.DeserializeObject<BalanceResponse>(result);
                    //oApiResult.ErrorCode = ErrorCode.Success;
                    //oApiResult.Data.balance = json.result.balance;
                }
            }
            else
            {
                //oApiResult.RawMessage = result;
                //oApiResult.RawErrorCode = "1";
                //oApiResult.ErrorCode = ErrorCode.Fail;
            }

            //var request = new Dictionary<string, string>
            //{
            //    { "vendor_id", tpSetting["VendorId"] },
            //    { "vendor_member_id", tpCustInfo.TpUserName }
            //};

            //var (result, statusCode) = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, KickUserResp>(apiUrl, request);
            //if (!statusCode.IsSuccessStatus())
            //{
            //    throw new CustomException(
            //        $"Saba KickUser failed, HttpStatus: {(int)statusCode} {statusCode}" +
            //        $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            //}

            //if (!SabaDefinition.KickUserSuccess(result.ErrorCode))
            //{
            //    throw new CustomException(
            //        $"Saba KickUser failed: ({result.ErrorCode}) {result.Message}");
            //}

            return true;
        }

        private ThirdPartyModel GetThirdPartyModel(int siteId)
            => _thirdPartyModel.TryGetValue(siteId, out var result) ? result : _thirdPartyModel[0];

        //private async Task<(TransferStatus Status, FundTransferResp Resp)> FundTransferAsync(TpCustInfoModel tpCustInfo, TpTransferInfoModel tpTransferInfo, string transId)
        //{
        //    var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
        //    var apiUrl = $"{tpSetting["ApiUrl"]}" + UrlPath.FundTransfer;
        //    var amount = tpTransferInfo.Amount.ToTpAmount(tpCustInfo.ExchangeRate);

        //    var request = new Dictionary<string, string>
        //    {
        //        { "vendor_id", tpSetting["VendorId"] },
        //        { "vendor_member_id", tpCustInfo.TpUserName },
        //        { "vendor_trans_id", transId },
        //        { "amount", amount.ToString("##.###") },
        //        { "currency", tpCustInfo.TpCurrencyId },
        //        { "direction", "0" }, // ToMain
        //        { "wallet_id", WalletId }
        //    };

        //    var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, FundTransferResp>(apiUrl, request);
        //    if (!response.StatusCode.IsSuccessStatus())
        //    {
        //        throw new CustomException(
        //            $"Saba FundTransfer failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}" +
        //            $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
        //    }

        //    var result = response.Result;
        //    if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
        //    {
        //        throw new CustomException(
        //            $"Saba FundTransfer failed: ({result.ErrorCode}) {result.Message}");
        //    }

        //    var status = SabaDefinition.GetTransferStatus(result.Data.Status);

        //    return (status, result);
        //}

        //private async Task<IEnumerable<CheckUserBalanceData>> CheckUserBalanceAsync(IEnumerable<TpCustInfoModel> tpCustList)
        //{
        //    var groupTpCustList = tpCustList.GroupBy(x => x.SiteId);
        //    var balanceInfoList = new List<CheckUserBalanceData>();

        //    foreach (var group in groupTpCustList)
        //    {
        //        if (!group.Any())
        //            continue;

        //        var tpSetting = GetThirdPartyModel(group.Key).TpSetting;
        //        var apiUrl = $"{tpSetting["ApiUrl"]}" + UrlPath.CheckUserBalance;

        //        var tpUserNames = string.Join(",", group.Select(x => x.TpUserName));
        //        var request = new Dictionary<string, string>
        //        {
        //            { "vendor_id", tpSetting["VendorId"] },
        //            { "vendor_member_ids", tpUserNames },
        //            { "wallet_id", WalletId },
        //        };

        //        var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, CheckUserBalanceResp>(apiUrl, request);
        //        if (!response.StatusCode.IsSuccessStatus())
        //        {
        //            throw new CustomException(
        //                $"Saba CheckUserBalanceData failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}, SiteId: {group.Key}");
        //        }

        //        var result = response.Result;
        //        if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
        //        {
        //            throw new CustomException(
        //                $"Saba CheckUserBalanceData failed: ({result.ErrorCode}) {result.Message}, SiteId: {group.Key}");
        //        }

        //        balanceInfoList = balanceInfoList.Union(result.Data).ToList();
        //    }

        //    return balanceInfoList;
        //}

        //private async Task<bool> UpdateMinTransferAsync(TpCustInfoModel tpCustInfo, int siteId, decimal tpBalanceAmount)
        //{
        //    var tpSetting = GetThirdPartyModel(siteId).TpSetting;
        //    var apiUrl = $"{tpSetting["ApiUrl"]}" + UrlPath.UpdateMember;

        //    var unit = tpBalanceAmount.ToString("##.###").Split('.').Last().Length;
        //    var minTransfer = (1 / (unit * 10)).ToString("##.###");

        //    var request = new Dictionary<string, string>
        //    {
        //        { "vendor_id", tpSetting["VendorId"] },
        //        { "vendor_Member_ID", tpCustInfo.TpUserName },
        //        { "maxTransfer", int.MaxValue.ToString() },
        //        { "minTransfer", minTransfer },
        //        { "oddstype", "1" }
        //    };

        //    var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, UpdateUserResp>(apiUrl, request);
        //    if (!response.StatusCode.IsSuccessStatus())
        //    {
        //        throw new CustomException(
        //            $"Saba UpdateMember failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}, TpUserName: {tpCustInfo.TpUserName}");
        //    }

        //    var result = response.Result;
        //    if (!SabaDefinition.SabaCommonSuccess(result.ErrorCode))
        //    {
        //        throw new CustomException(
        //            $"Saba UpdateMember failed: ({result.ErrorCode}) {result.Message}");
        //    }

        //    return true;
        //}

        //public void CommitTransfer(TpCustInfoModel tpCustInfo, string transId, decimal transferAmount, TransferStatus transferStatus, string thirdId)
        //{
        //    var m = Regex.Match(transId, @"\d+");
        //    var transferId = int.Parse(m.Value);
        //    var memo = "system transfer: saba switch to seamless wallet";

        //    var setTpTransferInfo = new SetTpTransferInfoModel
        //    {
        //        TransferId = transferId,
        //        ThirdId = thirdId,
        //        ForStatus = TransferStatus.Pending,
        //        Status = transferStatus,
        //        Memo = memo
        //    };

        //    using var conn = MainDbDataLayer.GetMySqlConnection();
        //    conn.Open();
        //    var trans = conn.BeginTransaction();

        //    try
        //    {
        //        var setTransferSuccess = MainDbDataLayer.SetTpTransferInfo(setTpTransferInfo, trans);
        //        if (setTransferSuccess)
        //        {
        //            if (setTpTransferInfo.Status != TransferStatus.Success)
        //            {
        //                trans.Commit();
        //                return;
        //            }

        //            var setWalletBalance = new SetWalletBalanceModel
        //            {
        //                CustId = tpCustInfo.CustId,
        //                SiteId = tpCustInfo.SiteId,
        //                TpId = this.TpId,
        //                UserName = tpCustInfo.UserName,
        //                Amount = transferAmount,
        //                Memo = memo,
        //                RefTransId = transId
        //            };

        //            // 轉移進行中紅利至單一錢包，轉帳金額須扣除紅利金額
        //            var bonusAmount = MainDbDataLayer.SetSabaBonus(tpCustInfo.CustId, transId, trans);
        //            setWalletBalance.Amount -= bonusAmount;

        //            var setWalletResult = MainDbDataLayer.SetWalletBalance(setWalletBalance, trans);
        //            if (setWalletResult.Success)
        //            {
        //                trans.Commit();
        //                Logger.LogInformation(
        //                    $"{setWalletBalance.CustId} {setWalletBalance.UserName} AfterAmount: {setWalletResult.AfterAmount}");
        //            }
        //            else
        //            {
        //                trans.Rollback();
        //                throw new CustomException(
        //                    $"TransferId: {setTpTransferInfo.TransferId}, CustId:{tpCustInfo.CustId}, SetWalletBalance failed.");
        //            }
        //        }
        //        else
        //        {
        //            trans.Rollback();
        //            throw new CustomException(
        //                $"TransferId: {setTpTransferInfo.TransferId}, CustId:{tpCustInfo.CustId} SetTpTransferInfo failed.");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        trans.Rollback();
        //        throw new CustomException(
        //            $"TransferId: {setTpTransferInfo.TransferId}, CustId:{tpCustInfo.CustId} ,CommitTpTransfer Exception.", ex);
        //    }
        //}

        //private async Task<(TransferStatus Status, CheckFundTransferResp Resp)> CheckFundTransferAsync(string transId, int siteId)
        //{
        //    var tpSetting = GetThirdPartyModel(siteId).TpSetting;
        //    var apiUrl = $"{tpSetting["ApiUrl"]}" + UrlPath.CheckFundTransfer;

        //    var request = new Dictionary<string, string>
        //    {
        //        { "vendor_id", tpSetting["VendorId"] },
        //        { "vendor_trans_id", transId },
        //        { "wallet_id", WalletId }
        //    };

        //    var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, CheckFundTransferResp>(apiUrl, request);
        //    if (!response.StatusCode.IsSuccessStatus())
        //    {
        //        throw new CustomException(
        //            $"Saba CheckFundTransfer failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}, TransId: {transId}");
        //    }

        //    var result = response.Result;
        //    if (SabaDefinition.CheckTransferErrorErrorCode.Contains(result.ErrorCode))
        //    {
        //        throw new CustomException(
        //            $"Saba CheckFundTransfer failed: ({result.ErrorCode}) {result.Message}");
        //    }

        //    var status = SabaDefinition.GetCheckTransferStatus(result.ErrorCode, result.Data?.Status);

        //    return (status, result);
        //}

        #endregion

        #region private method

        /// <summary>
        /// 取得X509Certificate2憑證
        /// </summary>
        /// <param name="currency"></param>
        /// <returns></returns>
        private X509Certificate2 GetCertificate(string currency)
        {
            var pem = Path.Combine(storagePath, "PEM", $"{currency}.pem");
            var key = Path.Combine(storagePath, "PEM", $"{currency}.key");
            return LoadCertificateFromPemFile(pem, key);
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

        private static string GetPass(Dictionary<string, string> inputs, string currency)
        {
            return inputs.ContainsKey($"Pass_{currency}") && !string.IsNullOrEmpty(inputs[$"Pass_{currency}"]) ? inputs[$"Pass_{currency}"] : throw new Exception($"Pass_{currency}");
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

        //TODO：通知or紀錄？？？
        //97 商戶餘額不足，玩家轉帳金額 > 商戶餘額

        public static X509Certificate2 LoadCertificateFromPemFile(string certPemFilePath, string? keyPemFilePath = default)
        {
            string certContents = File.ReadAllText(certPemFilePath);
            string keyContents = keyPemFilePath is null ? certContents : File.ReadAllText(keyPemFilePath);

            byte[] certificateBytes = GetBytesFromPem(certContents, keyContents);
            var signedCms = new SignedCms();
            signedCms.Decode(certificateBytes);
            var certificate = new X509Certificate2(signedCms.Certificates[0]);

            return certificate;
        }

        private static byte[] GetBytesFromPem(string pemText, string section)
        {
            string header = string.Format("-----BEGIN {0}-----", section);
            string footer = string.Format("-----END {0}-----", section);
            int start = pemText.IndexOf(header) + header.Length;
            int end = pemText.IndexOf(footer, start);
            string base64String = pemText.Substring(start, end - start);
            return Convert.FromBase64String(base64String);
        }

        #endregion
    }
}
