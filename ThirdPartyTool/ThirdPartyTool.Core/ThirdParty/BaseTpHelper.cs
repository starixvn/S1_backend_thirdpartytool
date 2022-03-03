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
        protected readonly MainDbDataLayer MainDbDataLayer;

        public BaseTpHelper(IServiceProvider provider)
        {
            Logger = provider.GetRequiredService<ILogger<BaseTpHelper>>();
            HttpHelper = provider.GetRequiredService<IHttpHelper>();
            MainDbDataLayer = provider.GetRequiredService<MainDbDataLayer>();
        }

        public IEnumerable<TpCustInfoModel> GeTptAllCustInfoList(int tpId, int siteId)
        {
            return MainDbDataLayer.GetTpAllCustInfoList(tpId, siteId);
        }

        public IEnumerable<TpCustInfoModel> GetTpAllCustInfoByCust(int tpId, string jsonCustIds)
        {
            return MainDbDataLayer.GetTpAllCustInfoByCust(tpId, jsonCustIds);
        }

        public string CreateTpTransferInfo(TpTransferInfoModel tpTransferInfo)
        {
            using var conn = MainDbDataLayer.GetMySqlConnection();

            conn.Open();
            var trans = conn.BeginTransaction();
            var transId = MainDbDataLayer.CreateTpTransferInfo(tpTransferInfo, trans);
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
    }
}
