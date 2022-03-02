using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThirdPartyTool.Core.DataLayers;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Helpers.Interfaces;
using ThirdPartyTool.Core.Models;

namespace ThirdPartyTool.Core.ThirdParty
{
    public class BaseTpHelper
    {
        protected readonly ILogger<BaseTpHelper> Logger;
        protected readonly IHttpHelper HttpHelper;
        private readonly MainDbDataLayer _mainDbDataLayer;

        public BaseTpHelper(IServiceProvider provider)
        {
            Logger = provider.GetRequiredService<ILogger<BaseTpHelper>>();
            HttpHelper = provider.GetRequiredService<IHttpHelper>();
            _mainDbDataLayer = provider.GetRequiredService<MainDbDataLayer>();
        }

        public IEnumerable<TpCustInfoModel> GeTptAllCustInfoList(int tpId, int siteId)
        {
            return _mainDbDataLayer.GetTpAllCustInfoList(tpId, siteId);
        }

        public IEnumerable<TpCustInfoModel> GetTpAllCustInfoByCust(int tpId, string jsonCustIds)
        {
            return _mainDbDataLayer.GetTpAllCustInfoByCust(tpId, jsonCustIds);
        }

        public string CreateTpTransferInfo(TpTransferInfoModel tpTransferInfo)
        {
            using var conn = _mainDbDataLayer.GetMySqlConnection();

            conn.Open();
            var trans = conn.BeginTransaction();
            var transId = _mainDbDataLayer.CreateTpTransferInfo(tpTransferInfo, trans);
            if (!string.IsNullOrEmpty(transId))
            {
                trans.Commit();
            }
            else
            {
                trans.Rollback();
                throw new CustomException("CreateTpTransferInfo return TransId is empty.");
            }

            return transId;
        }

        public bool CommitTpTransfer(SetTpTransferInfoModel tpTransferInfo, SetWalletBalanceModel setWalletBalance)
        {
            using var conn = _mainDbDataLayer.GetMySqlConnection();

            conn.Open();
            var trans = conn.BeginTransaction();
            var setTransferSuccess = _mainDbDataLayer.SetTpTransferInfo(tpTransferInfo, trans);
            if (setTransferSuccess)
            {

                var setWalletResult = _mainDbDataLayer.SetWalletBalance(setWalletBalance, trans);
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
                        $"TransferId: {tpTransferInfo.TransferId}, CustId:{setWalletBalance.CustId}, SetWalletBalance failed.");
                }
            }
            else
            {
                trans.Rollback();
                throw new CustomException(
                    $"TransferId: {tpTransferInfo.TransferId}, CustId:{setWalletBalance.CustId} SetTpTransferInfo failed.");
            }

            return true;
        }
    }
}
