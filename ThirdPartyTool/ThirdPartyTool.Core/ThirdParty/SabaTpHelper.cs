using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Saba;
using ThirdPartyTool.Core.ThirdParty.Interfaces;

namespace ThirdPartyTool.Core.ThirdParty
{
    public class SabaTpHelper : BaseTpHelper, ITpHelper
    {
        public int TpId => 41;

        private readonly Dictionary<int, ThirdPartyModel> _thirdPartyModel;

        public SabaTpHelper(IServiceProvider provider) : base(provider)
        {
            var a = provider.GetRequiredService<IEnumerable<ThirdPartyModel>>();

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

        public async Task<bool> KickUserAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.KickUser;

            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_member_id", tpCustInfo.TpUserName }
            };

            try
            {
                var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, KickUserResp>(apiUrl, request);
                if (!response.StatusCode.IsSuccessStatus())
                {
                    throw new CustomException(
                        $"Saba KickUser failed, HttpStatus: {(int) response.StatusCode} {response.StatusCode}" +
                        $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
                }

                var result = response.Result;
                if (!SabaDefinition.GetKickUserResult(result.ErrorCode))
                {
                    throw new CustomException(
                        $"Saba KickUser failed: ({result.ErrorCode}) {result.Message}");
                }
            }
            catch (CustomException ex)
            {
                Logger.LogError(ex, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                return false;
            }

            return true;
        }
        
        public async void CheckUserBalanceAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.CheckUserBalance;
            
            var request = new Dictionary<string, string>
            {
                { "vendor_id", tpSetting["VendorId"] },
                { "vendor_member_id", tpCustInfo.TpUserName },
                { "wallet_id ", "1" }, // 1: Sportsbook/ 5: AG
            };

            var response = await HttpHelper.PostUrlEncodedAsync<Dictionary<string, string>, CheckUserBalanceResp>(apiUrl, request);
            if (!response.StatusCode.IsSuccessStatus())
            {
                throw new CustomException(
                    $"Saba CheckUserBalanceData failed, HttpStatus: {(int)response.StatusCode} {response.StatusCode}" +
                    $", SiteId: {tpCustInfo.SiteId} CustId: {tpCustInfo.CustId} TpUserName: {tpCustInfo.TpUserName}");
            }

            var result = response.Result;
            if (!SabaDefinition.GetKickUserResult(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba KickUser failed: ({result.ErrorCode}) {result.Message}");
            }
        }

        public async void FundTransferAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.FundTransfer;

            throw new NotImplementedException();

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
            if (!SabaDefinition.GetKickUserResult(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba KickUser failed: ({result.ErrorCode}) {result.Message}");
            }
        }

        public async void CheckFundTransferAsync(TpCustInfoModel tpCustInfo)
        {
            var tpSetting = GetThirdPartyModel(tpCustInfo.SiteId).TpSetting;
            var apiUrl = $"{tpSetting["ApiUrl"]}" + Path.CheckFundTransfer;

            throw new NotImplementedException();

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
            if (!SabaDefinition.GetKickUserResult(result.ErrorCode))
            {
                throw new CustomException(
                    $"Saba KickUser failed: ({result.ErrorCode}) {result.Message}");
            }
        }

        private ThirdPartyModel GetThirdPartyModel(int siteId)
            => _thirdPartyModel.TryGetValue(siteId, out var result) ? result : _thirdPartyModel[0];
    }
}